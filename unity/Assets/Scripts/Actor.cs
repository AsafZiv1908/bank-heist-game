using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace StudentHeist
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class Actor : MonoBehaviour
    {
        public float speed = 3.6f, attackRange = 18, damage = 7;
        public Transform model, leftArm, rightArm, leftLeg, rightLeg;
        public TextMesh nameLabel;
        [System.NonSerialized] public ActorState state;
        public NavMeshAgent agent;
        public bool prone;
        float thinkTimer, fireTimer, grenadeTimer, noticeTimer, lootTimer;
        Vector3 lastPosition;
        AudioSource sound;
        public void Initialize(ActorState data)
        {
            state = data; agent = GetComponent<NavMeshAgent>();
            agent.speed = speed; agent.radius = .4f; agent.height = 1.9f;
            agent.stoppingDistance = 1.4f; agent.avoidancePriority = 20 + Mathf.Abs(data.id.GetHashCode()) % 60;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 1; sound.maxDistance = 24;
            nameLabel.text = data.id;
            nameLabel.characterSize = .06f;
            nameLabel.color = data.side == Side.Police ? new Color(.4f, .7f, 1) : data.side == Side.Robber ? new Color(1, .55f, .3f) : Color.white;
            lastPosition = transform.position;
            if (!data.Alive) DieVisual();
        }
        void Update()
        {
            var s = HeistSession.Instance; if (state == null || !s || s.loading) return;
            if (Camera.main && nameLabel)
            {
                nameLabel.transform.rotation = Camera.main.transform.rotation;
                nameLabel.GetComponent<Renderer>().enabled = Vector3.Distance(Camera.main.transform.position, transform.position) < 9;
            }
            if (!state.Alive || s.finished || s.paused) return;
            fireTimer -= Time.deltaTime; grenadeTimer -= Time.deltaTime; noticeTimer -= Time.deltaTime;
            bool shouldLie = state.customer && !state.recruited && s.alarm;
            if (shouldLie != prone) SetProne(shouldLie);
            if (prone) return;
            AnimateWalking();
            thinkTimer -= Time.deltaTime;
            if (thinkTimer > 0) return;
            thinkTimer = .18f;
            if (state.customer && !state.recruited) return;
            bool friendly = state.side == s.selectedSide || state.recruited;
            Transform enemy = ClosestEnemy();
            if (state.side != Side.Bystander && s.alarm && enemy && state.order != SquadOrder.Retreat)
            {
                if (Vector3.Distance(transform.position, enemy.position) < attackRange && CanSee(enemy)) Shoot(enemy);
            }
            if (friendly)
            {
                if (state.order == SquadOrder.Hold) WalkTo(state.holdPosition, .3f);
                else if (state.order == SquadOrder.Retreat) WalkTo(SceneController.Active.retreatPoint.position, 1.7f);
                else if (state.order == SquadOrder.Attack && enemy) WalkTo(enemy.position, 6);
                else
                {
                    var player = SceneController.Active.player.transform;
                    int index = s.Squad.ToList().IndexOf(state);
                    WalkTo(player.position - player.forward * (2.5f + index / 2 * 1.5f) + player.right * (index % 2 == 0 ? -1.8f : 1.8f), .8f);
                }
            }
            else
            {
                var leader = s.actors.FirstOrDefault(a => a.side == state.side && a.commander && a.Alive);
                if (state.commander) CommanderThink(enemy);
                else if (leader != null)
                {
                    var actor = SceneController.Active.FindActor(leader.id);
                    if (actor) WalkTo(actor.transform.position - actor.transform.forward * 2 + actor.transform.right * (state.id.EndsWith("2") ? -1.7f : 1.7f), 1.3f);
                }
            }
            // The NavMesh solver handles obstacles. This small separation force prevents tight piling.
            if (agent.enabled && agent.isOnNavMesh)
            {
                Vector3 push = Vector3.zero;
                foreach (var other in SceneController.Active.activeActors)
                {
                    if (!other || other == this || !other.state.Alive || other.prone) continue;
                    Vector3 delta = transform.position - other.transform.position; delta.y = 0;
                    if (delta.sqrMagnitude > .001f && delta.sqrMagnitude < 1.15f) push += delta.normalized * (1.1f - delta.magnitude);
                }
                agent.Move(push * .12f);
            }
            state.position = transform.position;
        }
        void CommanderThink(Transform enemy)
        {
            var s = HeistSession.Instance;
            if (!s.alarm) return;
            if (state.side == Side.Robber)
            {
                if (state.area == Area.Vault && state.gold < 2000)
                {
                    WalkTo(new Vector3(0, 0, 4), 1);
                    if (Vector3.Distance(transform.position, new Vector3(0, 0, 4)) < 3)
                    { lootTimer += .18f; if (lootTimer > 3) { state.gold += 500; lootTimer = 0; s.Notify(state.id + " collected gold."); } }
                    return;
                }
                if (state.area == Area.Sewers)
                {
                    WalkTo(new Vector3(0, 0, 18), 1);
                    if (transform.position.z > 16) s.End(false, "The robbers escaped through the rear exit and sewers.");
                    return;
                }
                var to = s.RobberDestination(state); var portal = SceneController.Active.PortalTo(to);
                if (portal)
                { WalkTo(portal.transform.position, 1.7f); if (Vector3.Distance(transform.position, portal.transform.position) < 3.3f) s.MoveNPCTeam(state, to, true); }
            }
            else if (enemy) WalkTo(enemy.position, 7);
            else
            {
                var robber = s.actors.FirstOrDefault(a => a.side == Side.Robber && a.Alive);
                if (robber != null && robber.area != state.area)
                {
                    Area next = s.NextToward(state.area, robber.area); var portal = SceneController.Active.PortalTo(next);
                    if (portal) { WalkTo(portal.transform.position, 1.8f); if (Vector3.Distance(transform.position, portal.transform.position) < 3.4f) s.MoveNPCTeam(state, next, true); }
                }
            }
        }
        public void WalkTo(Vector3 destination, float stop = .25f)
        {
            if (!agent || !agent.enabled || !agent.isOnNavMesh || prone || !state.Alive) return;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(destination, out hit, 4, NavMesh.AllAreas))
            { agent.stoppingDistance = stop; agent.isStopped = false; agent.SetDestination(hit.position); }
        }
        public Transform ClosestEnemy()
        {
            if (state.side == Side.Bystander) return null;
            Transform result = null; float best = 1000;
            var scene = SceneController.Active;
            foreach (var a in scene.activeActors)
            {
                if (!a || !a.state.Alive || a.state.side == state.side || a.state.side == Side.Bystander) continue;
                float d = Vector3.Distance(transform.position, a.transform.position);
                if (d < best) { best = d; result = a.transform; }
            }
            if (scene.player && scene.player.state.Alive && scene.player.state.side != state.side && scene.player.state.side != Side.Bystander)
            { if (Vector3.Distance(transform.position, scene.player.transform.position) < best) result = scene.player.transform; }
            return result;
        }
        bool CanSee(Transform target)
        {
            Vector3 from = transform.position + Vector3.up * 1.4f + transform.forward * .55f;
            Vector3 delta = target.position + Vector3.up * 1.2f - from;
            RaycastHit hit;
            return !Physics.Raycast(from, delta.normalized, out hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore) || hit.transform.root == target.root;
        }
        void Shoot(Transform target)
        {
            if (fireTimer > 0) return;
            if (state.ammo == 0) { fireTimer = 1.5f; state.ammo = 12; return; }
            fireTimer = 1.1f; state.ammo--;
            Vector3 flat = target.position - transform.position; flat.y = 0;
            if (flat.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(flat);
            Vector3 from = transform.position + Vector3.up * 1.35f + transform.forward * .6f;
            Vector3 to = target.position + Vector3.up * 1.2f;
            Effects.Tracer(from, to, new Color(1, .8f, .25f)); sound.PlayOneShot(SoundBank.Shot(), .12f);
            var actor = target.GetComponent<Actor>(); var player = target.GetComponent<PlayerController>();
            if (actor) actor.TakeDamage(damage); if (player) player.TakeDamage(damage);
        }
        public void ThrowAtEnemy()
        {
            var target = ClosestEnemy();
            if (!target || Vector3.Distance(target.position, transform.position) > 18) { HeistSession.Instance.Notify(state.id + ": no grenade target in range."); return; }
            if (state.grenades <= 0 || grenadeTimer > 0) { HeistSession.Instance.Notify(state.id + ": grenade unavailable."); return; }
            state.grenades--; grenadeTimer = 6;
            Vector3 delta = target.position - transform.position;
            Grenade.Spawn(transform.position + Vector3.up * 1.5f + delta.normalized * .7f, delta / 1.2f + Vector3.up * 5.5f, state.side);
        }
        public void TakeDamage(float amount)
        {
            if (state == null || !state.Alive || amount <= 0) return;
            state.health = Mathf.Max(0, state.health - amount);
            if (noticeTimer <= 0) { HeistSession.Instance.Notify(state.id + " hit. HP: " + state.health); noticeTimer = 2; }
            if (!state.Alive) { HeistSession.Instance.ActorDied(state); DieVisual(); }
        }
        void DieVisual()
        {
            if (agent && agent.enabled) agent.enabled = false;
            var collider = GetComponent<CapsuleCollider>(); if (collider) collider.enabled = false;
            model.localRotation = Quaternion.Euler(0, 0, 85); model.localPosition = new Vector3(0, .35f, 0);
            nameLabel.text = state.id + " (killed)"; nameLabel.color = Color.gray;
        }
        public void SetProne(bool value)
        {
            prone = value;
            if (agent && agent.enabled && agent.isOnNavMesh) agent.isStopped = value;
            model.localRotation = value ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            model.localPosition = value ? new Vector3(0, .25f, 0) : Vector3.zero;
            nameLabel.transform.localPosition = new Vector3(0, value ? .65f : 2.15f, 0);
            var cap = GetComponent<CapsuleCollider>(); cap.height = value ? .4f : 1.8f; cap.center = value ? new Vector3(0, .2f, .5f) : new Vector3(0, .9f, 0);
        }
        public void Recruit()
        {
            if (!state.customer || state.recruited || !state.Alive) return;
            state.recruited = true; state.order = SquadOrder.Follow; SetProne(false);
            HeistSession.Instance.Notify(state.id + " is following you. Take them to the street safe zone.");
        }
        void AnimateWalking()
        {
            float movement = (transform.position - lastPosition).magnitude; lastPosition = transform.position;
            float swing = movement > .002f ? Mathf.Sin(Time.time * 9) * 24 : 0;
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0); rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            if (state.customer) { leftArm.localRotation = Quaternion.Euler(-swing * .5f, 0, 0); rightArm.localRotation = Quaternion.Euler(swing * .5f, 0, 0); }
        }
    }
}

