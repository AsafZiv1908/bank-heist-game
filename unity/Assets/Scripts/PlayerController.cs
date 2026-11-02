using UnityEngine;

namespace StudentHeist
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 4.8f;
        public float mouseSensitivity = 2.2f;
        public Camera viewCamera;
        public Transform weapon;
        [System.NonSerialized] public ActorState state;
        public string interactionPrompt = "";
        public bool pointerMode;
        public float reloadTimer;
        CharacterController controller;
        AudioSource sound;
        float pitch, verticalSpeed, shotTimer, grenadeTimer, recoil;
        Vector3 weaponRest;
        public void Initialize(ActorState data)
        {
            state = data; controller = GetComponent<CharacterController>();
            sound = gameObject.AddComponent<AudioSource>();
            weaponRest = weapon.localPosition;
            weapon.gameObject.SetActive(state.side != Side.Bystander);
        }
        void Update()
        {
            var s = HeistSession.Instance; if (state == null || !s || s.loading) return;
            if (Input.GetKeyDown(KeyCode.Escape)) s.SetPaused(!s.paused);
            if (s.paused || s.finished) return;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                pointerMode = !pointerMode;
                Cursor.lockState = pointerMode ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = pointerMode;
            }
            shotTimer -= Time.deltaTime; grenadeTimer -= Time.deltaTime;
            if (reloadTimer > 0)
            { reloadTimer -= Time.deltaTime; if (reloadTimer <= 0) { state.ammo = 12; s.Notify("Reloaded."); } }
            if (!pointerMode)
            {
                float x = Input.GetAxis("Mouse X") * mouseSensitivity, y = Input.GetAxis("Mouse Y") * mouseSensitivity;
                transform.Rotate(0, x, 0); pitch = Mathf.Clamp(pitch - y, -80, 80);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch - recoil * 4, 0, 0);
                float forward = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                float sideways = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                Vector3 motion = Vector3.ClampMagnitude(transform.forward * forward + transform.right * sideways, 1);
                if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                verticalSpeed += Physics.gravity.y * Time.deltaTime;
                controller.Move((motion * moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? 1.4f : 1) + Vector3.up * verticalSpeed) * Time.deltaTime);
                if (Input.GetMouseButton(0)) Fire();
                if (Input.GetKeyDown(KeyCode.G)) ThrowGrenade();
                weapon.localPosition = weaponRest + new Vector3(Mathf.Sin(Time.time * 9) * motion.magnitude * .012f, Mathf.Cos(Time.time * 18) * motion.magnitude * .008f, -recoil * .12f);
            }
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 6);
            if (Input.GetKeyDown(KeyCode.R)) Reload();
            for (int i = 0; i < 5; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) s.SetOrder((SquadOrder)i);
            var interact = NearestInteraction(); var person = NearestCustomer();
            interactionPrompt = person ? "E - Help " + person.state.id : interact ? "E - " + interact.prompt : "";
            if (Input.GetKeyDown(KeyCode.E)) { if (person) person.Recruit(); else if (interact) interact.Use(); }
            state.position = transform.position;
        }
        public Interactable NearestInteraction()
        {
            Interactable nearest = null; float best = 3.2f;
            foreach (var i in FindObjectsByType<Interactable>())
            { float d = Vector3.Distance(transform.position + Vector3.up, i.transform.position); if (d < best) { nearest = i; best = d; } }
            return nearest;
        }
        Actor NearestCustomer()
        {
            if (state.side != Side.Bystander || !SceneController.Active) return null;
            Actor nearest = null; float best = 3;
            foreach (var a in SceneController.Active.activeActors)
            { if (!a || !a.state.customer || !a.state.Alive || a.state.recruited) continue; float d = Vector3.Distance(transform.position, a.transform.position); if (d < best) { nearest = a; best = d; } }
            return nearest;
        }
        public void Fire()
        {
            if (state.side == Side.Bystander || shotTimer > 0 || reloadTimer > 0) return;
            if (state.ammo <= 0) { Reload(); return; }
            state.ammo--; shotTimer = .2f; recoil = 1;
            sound.PlayOneShot(SoundBank.Shot(), .3f);
            Vector3 origin = viewCamera.transform.position, direction = viewCamera.transform.forward, end = origin + direction * 60;
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, 70, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var enemy = hit.collider.GetComponentInParent<Actor>();
                if (enemy && enemy.state.side != state.side) { bool alive = enemy.state.Alive; enemy.TakeDamage(26); if (alive && !enemy.state.Alive) HeistSession.Instance.kills++; }
            }
            Effects.Tracer(weapon.position + viewCamera.transform.forward * .5f, end, Color.yellow);
        }
        public void Reload()
        {
            if (state.side == Side.Bystander || state.ammo == 12 || reloadTimer > 0) return;
            reloadTimer = 1.4f; HeistSession.Instance.Notify("Reloading...");
        }
        public void ThrowGrenade()
        {
            if (state.side == Side.Bystander) return;
            if (state.grenades <= 0 || grenadeTimer > 0) { HeistSession.Instance.Notify("No grenade available. Cooldown: " + Mathf.CeilToInt(grenadeTimer)); return; }
            state.grenades--; grenadeTimer = 5;
            Grenade.Spawn(viewCamera.transform.position + viewCamera.transform.forward * .7f, viewCamera.transform.forward * 11 + Vector3.up * 3, state.side);
        }
        public void TakeDamage(float amount)
        {
            if (state == null || !state.Alive || amount <= 0) return;
            state.health = Mathf.Max(0, state.health - amount);
            HeistSession.Instance.Notify("Player hit! HP: " + Mathf.CeilToInt(state.health));
            if (GameHUD.Active) GameHUD.Active.FlashDamage();
            if (!state.Alive) HeistSession.Instance.ActorDied(state);
        }
    }
}

