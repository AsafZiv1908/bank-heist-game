using UnityEngine;

namespace StudentHeist
{
    public class Grenade : MonoBehaviour
    {
        public Side owner;
        public float fuse = 1.6f, radius = 4.5f, damage = 65;
        public static void Spawn(Vector3 from, Vector3 velocity, Side side)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Thrown grenade";
            go.transform.position = from; go.transform.localScale = Vector3.one * .22f;
            go.GetComponent<Renderer>().material.color = new Color(.18f, .25f, .13f);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = .5f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.linearVelocity = velocity;
            go.AddComponent<Grenade>().owner = side;
        }
        void Update() { fuse -= Time.deltaTime; if (fuse <= 0) Explode(); }
        void Explode()
        {
            var scene = SceneController.Active;
            foreach (var a in scene.activeActors)
            {
                if (!a || !a.state.Alive || a.state.side == owner || a.state.side == Side.Bystander) continue;
                if (Vector3.Distance(transform.position, a.transform.position) < radius && !Physics.Linecast(transform.position + Vector3.up * .2f, a.transform.position + Vector3.up, 1 << 0)) a.TakeDamage(damage);
            }
            if (scene.player.state.side != owner && Vector3.Distance(transform.position, scene.player.transform.position) < radius) scene.player.TakeDamage(damage);
            Effects.Blast(transform.position); Destroy(gameObject);
        }
    }
    public static class Effects
    {
        static Material lineMaterial;
        public static void Tracer(Vector3 start, Vector3 end, Color color)
        {
            if (!lineMaterial) lineMaterial = Resources.Load<Material>("Tracer");
            var go = new GameObject("Bullet trace"); var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.startColor = line.endColor = color; line.startWidth = .025f; line.endWidth = .01f;
            line.positionCount = 2; line.SetPosition(0, start); line.SetPosition(1, end); Object.Destroy(go, .065f);
        }
        public static void Blast(Vector3 position)
        {
            var go = new GameObject("Grenade burst"); go.transform.position = position;
            var particles = go.AddComponent<ParticleSystem>(); var main = particles.main;
            main.duration = .3f; main.loop = false; main.startLifetime = .5f; main.startSpeed = 8; main.startSize = .18f; main.startColor = new Color(1, .6f, .12f); main.gravityModifier = .7f;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 45) });
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("Blast");
            particles.Play(); AudioSource.PlayClipAtPoint(SoundBank.Boom(), position, .6f); Object.Destroy(go, 2);
        }
    }
    public static class SoundBank
    {
        static AudioClip shot, boom, alarm;
        static AudioClip Make(string name, float seconds, float frequency, bool noise)
        {
            int size = (int)(22050 * seconds); var samples = new float[size]; var random = new System.Random(42);
            for (int i = 0; i < size; i++)
            { float t = (float)i / 22050; float wave = noise ? (float)random.NextDouble() * 2 - 1 : Mathf.Sin(t * frequency * Mathf.PI * 2 + Mathf.Sin(t * 12) * 8); samples[i] = wave * Mathf.Pow(1 - (float)i / size, 2) * .4f; }
            var clip = AudioClip.Create(name, size, 1, 22050, false); clip.SetData(samples, 0); return clip;
        }
        public static AudioClip Shot() { return shot ? shot : shot = Make("Shot", .14f, 100, true); }
        public static AudioClip Boom() { return boom ? boom : boom = Make("Grenade", .65f, 70, true); }
        public static AudioClip Alarm() { return alarm ? alarm : alarm = Make("Alarm", 2, 650, false); }
    }
}

