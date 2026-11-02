using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StudentHeist
{
    public class QueueController : MonoBehaviour
    {
        public Transform[] queueSlots;
        public Transform waitingArea;
        public float serviceSeconds = 10;
        readonly Queue<Actor> customers = new Queue<Actor>();
        float timer;
        public int Count { get { return customers.Count; } }
        public void Initialize()
        {
            customers.Clear();
            foreach (var a in SceneController.Active.activeActors.Where(a => a.state.customer && a.state.Alive && !a.state.recruited).OrderBy(a => a.state.id)) customers.Enqueue(a);
            UpdateSlots();
        }
        void Update()
        {
            var s = HeistSession.Instance;
            if (!s || s.paused || s.finished || s.alarm || customers.Count <= 3) return;
            timer += Time.deltaTime;
            if (timer < serviceSeconds) return;
            timer = 0;
            var served = customers.Dequeue();
            if (served) { served.WalkTo(waitingArea.position + Vector3.right * customers.Count * .8f); s.Notify(served.state.id + " finished at the teller. Next customer."); }
            UpdateSlots();
        }
        void UpdateSlots()
        {
            int i = 0;
            foreach (var a in customers) if (a && i < queueSlots.Length) a.WalkTo(queueSlots[i++].position, .12f);
        }
    }
}

