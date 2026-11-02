using System.Linq;
using UnityEngine;

namespace StudentHeist
{
    public class Interactable : MonoBehaviour
    {
        public Interaction kind;
        public string prompt = "Use door";
        public Area destination;
        public string arrival = "Front";
        public int goldIndex;
        public void Use()
        {
            var s = HeistSession.Instance; if (!s || s.loading || s.finished) return;
            if (kind == Interaction.Door) { s.Travel(destination, arrival); return; }
            if (kind == Interaction.Gold)
            {
                if (s.selectedSide != Side.Robber) { s.Notify("Leave the bank's gold where it is."); return; }
                int amount = Mathf.Min(500, s.remainingGold[goldIndex]);
                if (amount == 0) { s.Notify("This gold table is empty."); return; }
                s.remainingGold[goldIndex] -= amount; s.Player.gold += amount;
                s.Notify("Collected $" + amount + ". Total: $" + s.Player.gold);
                if (s.Player.gold >= 2000) s.Notify("Enough gold! Use the bank's rear exit, then the sewer manhole.");
            }
            if (kind == Interaction.CivilianExit)
            {
                if (s.selectedSide != Side.Bystander) { s.Notify("Civilian safe zone. Robbers must escape behind the bank."); return; }
                foreach (var a in s.Squad.Where(a => a.Alive && a.area == Area.Street).ToList())
                {
                    var actor = SceneController.Active.FindActor(a.id);
                    if (actor && Vector3.Distance(actor.transform.position, SceneController.Active.player.transform.position) < 8)
                    { a.rescued = true; s.rescuedCount++; s.Player.gold += 250; s.Notify(a.id + " reached safety."); }
                }
                SceneController.Active.RefreshNPCs();
                if (s.rescuedCount >= 2) s.End(true, "You saved " + s.rescuedCount + " customers.");
                else s.Notify("Bring two customers here. Saved: " + s.rescuedCount + "/2.");
            }
            if (kind == Interaction.SewerExit)
            {
                if (s.selectedSide == Side.Robber && s.Player.gold >= 2000) s.End(true, "Your crew escaped through the back door and the sewers with the gold!");
                else s.Notify(s.selectedSide == Side.Robber ? "Collect at least $2,000 from the vault before escaping." : "Stop the robbery or rescue the customers to win.");
            }
        }
    }
}

