using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace StudentHeist
{
    public class SceneController : MonoBehaviour
    {
        public static SceneController Active;
        public static event System.Action<SceneController> SceneReady;
        public Area area;
        public NavMeshData navigation;
        public GameObject playerPrefab, robberPrefab, policePrefab, customerPrefab;
        public Transform frontSpawn, rearSpawn, vaultSpawn, retreatPoint;
        public QueueController customerQueue;
        public PlayerController player;
        public List<Actor> activeActors = new List<Actor>();
        NavMeshDataInstance navInstance;
        AudioSource audioSource;

        void Awake()
        {
            Active = this;
            if (navigation) navInstance = NavMesh.AddNavMeshData(navigation);
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        void Start()
        {
            var s = HeistSession.GetOrCreate();
            if (s.Player == null) { s.NewGame(Side.Robber); return; }
            s.currentArea = area;
            var spawn = s.arrival == "Rear" ? rearSpawn : s.arrival == "Vault" ? vaultSpawn : frontSpawn;
            if (!spawn) spawn = frontSpawn;
            var go = Instantiate(playerPrefab, spawn.position, spawn.rotation);
            player = go.GetComponent<PlayerController>(); player.Initialize(s.Player);
            int index = 0;
            foreach (var state in s.Squad.Where(a => a.Alive && a.area == area))
            { state.position = spawn.position - spawn.forward * 1.8f + spawn.right * (index++ % 2 == 0 ? -1.7f : 1.7f); }
            RefreshNPCs();
            if (customerQueue) customerQueue.Initialize();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            SceneReady?.Invoke(this);
        }
        public void RefreshNPCs()
        {
            var s = HeistSession.Instance; if (!s) return;
            foreach (var actor in activeActors.ToList()) if (!actor || actor.state.area != area || actor.state.rescued)
            { activeActors.Remove(actor); if (actor) Destroy(actor.gameObject); }
            foreach (var state in s.actors.Where(a => !a.isPlayer && a.area == area && !a.rescued))
            {
                if (FindActor(state.id)) continue;
                var prefab = state.side == Side.Robber ? robberPrefab : state.side == Side.Police ? policePrefab : customerPrefab;
                Vector3 pos = state.position;
                NavMeshHit hit; if (NavMesh.SamplePosition(pos, out hit, 6, NavMesh.AllAreas)) pos = hit.position;
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = state.id;
                var actor = go.GetComponent<Actor>(); actor.Initialize(state); activeActors.Add(actor);
            }
        }
        public Actor FindActor(string id) { return activeActors.Find(a => a && a.state != null && a.state.id == id); }
        public Interactable PortalTo(Area destination) { return FindObjectsByType<Interactable>().FirstOrDefault(i => i.kind == Interaction.Door && i.destination == destination); }
        public void SaveActors()
        {
            foreach (var a in activeActors) if (a) a.state.position = a.transform.position;
            if (player) player.state.position = player.transform.position;
        }
        public void PlayAlarm() { if (audioSource) audioSource.PlayOneShot(SoundBank.Alarm()); }
        void OnDestroy() { if (navInstance.valid) navInstance.Remove(); if (Active == this) Active = null; }
    }
}

