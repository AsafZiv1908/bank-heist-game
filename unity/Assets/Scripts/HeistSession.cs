using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentHeist
{
    public class HeistSession : MonoBehaviour
    {
        public static HeistSession Instance { get; private set; }
        public List<ActorState> actors = new List<ActorState>();
        public Side selectedSide;
        public Area currentArea;
        public bool alarm, paused, finished, won, loading;
        public string ending, arrival = "Front";
        public float missionTime;
        public int kills, rescuedCount;
        public int[] remainingGold = { 1500, 1500 };
        public string selectedMember = "";
        public readonly List<string> messages = new List<string>();
        public float messageUntil;
        float offscreenClock;
        public ActorState Player { get { return actors.Find(a => a.isPlayer); } }
        public IEnumerable<ActorState> Squad { get { return actors.Where(a => !a.isPlayer && (selectedSide == Side.Bystander ? a.recruited : a.side == selectedSide && !a.customer)); } }

        public static HeistSession GetOrCreate()
        {
            if (Instance == null) new GameObject("Persistent Game Session").AddComponent<HeistSession>();
            return Instance;
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void NewGame(Side side)
        {
            Time.timeScale = 1;
            actors.Clear(); messages.Clear();
            selectedSide = side; currentArea = Area.Street;
            alarm = paused = finished = won = loading = false;
            missionTime = offscreenClock = 0; kills = rescuedCount = 0;
            remainingGold = new[] { 1500, 1500 }; arrival = "Front"; selectedMember = "";
            Add("Player", side, Area.Street, new Vector3(0, 0, -12), true, true);
            if (side != Side.Bystander)
            {
                Add(side == Side.Robber ? "Robber 2" : "Officer 2", side, Area.Street, new Vector3(-2, 0, -14));
                Add(side == Side.Robber ? "Robber 3" : "Officer 3", side, Area.Street, new Vector3(2, 0, -14));
            }
            if (side != Side.Robber) for (int i = 0; i < 3; i++) Add("Robber " + (i + 1), Side.Robber, Area.Street, new Vector3(9 + i * 2, 0, -4), false, i == 0);
            if (side != Side.Police) for (int i = 0; i < 3; i++) Add("Officer " + (i + 1), Side.Police, Area.Bank, new Vector3(7 + i * 1.6f, 0, 5), false, i == 0);
            for (int i = 0; i < 6; i++)
            {
                var a = Add("Customer " + (i + 1), Side.Bystander, Area.Bank, new Vector3(-7, 0, 5 - i * 1.35f));
                a.customer = true; a.health = 70;
            }
            Notify("Mission started. " + (side == Side.Robber ? "Enter the bank." : side == Side.Police ? "Stop the robbery crew." : "Help two customers escape."));
            SceneManager.LoadScene("Street");
        }
        ActorState Add(string id, Side side, Area area, Vector3 pos, bool player = false, bool commander = false)
        {
            var a = new ActorState { id = id, side = side, area = area, position = pos, isPlayer = player, commander = commander };
            actors.Add(a); return a;
        }
        public void Notify(string message)
        {
            messages.Insert(0, message); if (messages.Count > 6) messages.RemoveAt(6);
            messageUntil = Time.unscaledTime + 4;
            Debug.Log("HEIST: " + message);
        }
        public void TriggerAlarm()
        {
            if (alarm) return;
            alarm = true;
            Notify("BANK ROBBERY! Customers are lying on the floor.");
            if (SceneController.Active) SceneController.Active.PlayAlarm();
        }
        void Update()
        {
            if (Player == null || finished || paused || loading) return;
            missionTime += Time.deltaTime;
            if (selectedSide != Side.Robber && missionTime > 25) TriggerAlarm();
            if (missionTime >= 600) End(false, "Time ran out. The operation failed.");
            if (selectedSide == Side.Police && actors.Where(a => a.side == Side.Robber).All(a => !a.Alive))
            { Player.gold = 1500; End(true, "All robbers stopped. The bank is safe."); }
            if (selectedSide == Side.Bystander && rescuedCount + actors.Count(a => a.customer && a.Alive) < 2)
                End(false, "Too few customers survived the robbery.");
            // Simulate travel only for teams in an unloaded scene. Combat uses actual agents.
            offscreenClock += Time.deltaTime;
            if (offscreenClock > 12)
            {
                offscreenClock = 0;
                foreach (var leader in actors.Where(a => a.commander && !a.isPlayer && a.Alive && a.area != currentArea).ToList())
                {
                    if (!alarm) continue;
                    if (leader.side == Side.Robber)
                    {
                        if (leader.area == Area.Vault && leader.gold < 2000) { leader.gold += 500; continue; }
                        if (leader.area == Area.Sewers) { End(false, "The robbers escaped through the sewers."); break; }
                        MoveNPCTeam(leader, RobberDestination(leader), false);
                    }
                    else if (leader.side == Side.Police)
                    {
                        var target = actors.FirstOrDefault(a => a.side == Side.Robber && a.Alive);
                        if (target != null && target.area != leader.area) MoveNPCTeam(leader, NextToward(leader.area, target.area), false);
                    }
                }
            }
        }
        public Area RobberDestination(ActorState a)
        {
            if (a.area == Area.Street) return Area.Bank;
            if (a.area == Area.Bank) return a.gold >= 2000 ? Area.BackAlley : Area.Vault;
            if (a.area == Area.Vault) return Area.Bank;
            if (a.area == Area.BackAlley) return Area.Sewers;
            return Area.Sewers;
        }
        public Area NextToward(Area from, Area to)
        {
            if (from == to) return to;
            if (from == Area.Street || from == Area.Vault) return Area.Bank;
            if (from == Area.Sewers) return Area.BackAlley;
            if (from == Area.BackAlley) return to == Area.Sewers ? to : Area.Bank;
            return to == Area.Sewers ? Area.BackAlley : to;
        }
        public void MoveNPCTeam(ActorState leader, Area to, bool fromLoadedScene)
        {
            int i = 0;
            foreach (var a in actors.Where(a => a.side == leader.side && !a.isPlayer && a.Alive).ToList())
            {
                a.area = to; a.position = DefaultSpawn(to) + new Vector3((i++ - 1) * 1.5f, 0, 0);
            }
            if (to == Area.Bank && leader.side == Side.Robber) TriggerAlarm();
            if (SceneController.Active) SceneController.Active.RefreshNPCs();
        }
        public static Vector3 DefaultSpawn(Area area)
        {
            if (area == Area.Street) return new Vector3(0, 0, 9);
            if (area == Area.Bank) return new Vector3(0, 0, -11);
            if (area == Area.Vault) return new Vector3(0, 0, -8);
            if (area == Area.BackAlley) return new Vector3(0, 0, -14);
            return new Vector3(0, 0, -8);
        }
        public void Travel(Area destination, string spawn)
        {
            if (loading || finished) return;
            SceneController.Active.SaveActors();
            foreach (var a in Squad.Where(a => a.Alive)) a.area = destination;
            Player.area = destination; currentArea = destination; arrival = spawn;
            if (selectedSide == Side.Robber && destination == Area.Bank) TriggerAlarm();
            StartCoroutine(LoadArea(destination));
        }
        IEnumerator LoadArea(Area destination)
        {
            loading = true;
            yield return SceneManager.LoadSceneAsync(destination.ToString());
            loading = false;
            Notify("Entered " + destination + ". Health, gold and squad status retained.");
        }
        public void SetOrder(SquadOrder order)
        {
            if (finished || loading) return;
            if (selectedSide == Side.Bystander && (order == SquadOrder.Attack || order == SquadOrder.Grenade))
            { Notify("Customers are unarmed. Use Follow, Retreat or Hold."); return; }
            var members = Squad.Where(a => a.Alive && (selectedMember == "" || selectedMember == a.id)).ToList();
            if (members.Count == 0) { Notify("No available teammates. Help a customer first."); return; }
            foreach (var a in members)
            {
                var actor = SceneController.Active.FindActor(a.id);
                if (order == SquadOrder.Grenade) { if (actor) actor.ThrowAtEnemy(); }
                else { a.order = order; a.holdPosition = actor ? actor.transform.position : a.position; }
            }
            Notify((selectedMember == "" ? "Squad" : selectedMember) + ": " + order);
        }
        public void ActorDied(ActorState a)
        {
            Notify(a.id + " was killed.");
            if (a.isPlayer) { End(false, "You were killed. Your team lost."); return; }
            if (a.commander)
            {
                a.commander = false;
                var next = actors.FirstOrDefault(b => b.side == a.side && b.Alive && !b.isPlayer);
                if (next != null) { next.commander = true; Notify(next.id + " is the new NPC commander."); }
            }
        }
        public void End(bool success, string reason)
        {
            if (finished) return;
            finished = true; won = success; ending = reason;
            Notify((success ? "TEAM WON: " : "TEAM LOST: ") + reason);
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        public void SetPaused(bool value)
        {
            if (finished) return;
            paused = value; Time.timeScale = paused ? 0 : 1;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }
        public void MainMenu()
        {
            Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            SceneManager.LoadScene("MainMenu"); Destroy(gameObject);
        }
        public string Objective()
        {
            if (selectedSide == Side.Police) return "Stop the robbers before they escape: " + actors.Count(a => a.side == Side.Robber && !a.Alive) + "/3";
            if (selectedSide == Side.Bystander) return "Help 2 customers. Bring them to the street safe zone: " + rescuedCount + "/2";
            if (Player.gold < 2000) return currentArea == Area.Vault ? "Take at least $2,000 gold. Press E by the gold tables." : "Enter the bank and find the vault.";
            if (currentArea == Area.BackAlley) return "Find the manhole at the end of the back alley.";
            if (currentArea == Area.Sewers) return "Follow the sewer tunnel to the green exit.";
            return "Gold secured. Escape through the BANK REAR EXIT.";
        }
    }
}

