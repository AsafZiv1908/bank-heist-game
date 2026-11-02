using System;
using UnityEngine;

namespace StudentHeist
{
    public enum Side { Robber, Police, Bystander }
    public enum Area { Street, Bank, Vault, BackAlley, Sewers }
    public enum SquadOrder { Follow, Attack, Grenade, Retreat, Hold }
    public enum Interaction { Door, Gold, Recruit, CivilianExit, SewerExit }

    // This data survives scene changes. Scene GameObjects can be destroyed safely.
    [Serializable]
    public class ActorState
    {
        public string id;
        public Side side;
        public Area area;
        public Vector3 position;
        public float health = 100;
        public int gold;
        public int ammo = 12;
        public int grenades = 3;
        public bool isPlayer;
        public bool commander;
        public bool customer;
        public bool recruited;
        public bool rescued;
        public SquadOrder order = SquadOrder.Follow;
        public Vector3 holdPosition;
        public bool Alive { get { return health > 0 && !rescued; } }
    }
}

