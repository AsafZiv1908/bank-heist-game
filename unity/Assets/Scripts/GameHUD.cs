using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace StudentHeist
{
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Active;
        public Text healthText, moneyText, ammoText, objectiveText, timerText, squadText, feedText, promptText, sceneText;
        public Slider healthBar;
        public Image damageOverlay;
        public GameObject pausePanel, resultPanel;
        public Text resultTitle, resultMessage, selectedText;
        float flash;
        void Awake() { Active = this; }
        void OnDestroy() { if (Active == this) Active = null; }
        void Update()
        {
            var s = HeistSession.Instance; if (!s || s.Player == null) return;
            var p = s.Player;
            healthText.text = "HP " + Mathf.CeilToInt(p.health) + " / 100"; healthBar.value = p.health;
            moneyText.text = "$ " + p.gold + "\nKills: " + s.kills;
            ammoText.text = p.side == Side.Bystander ? "UNARMED" : "Ammo: " + p.ammo + " / 12    Grenades: " + p.grenades;
            objectiveText.text = s.Objective();
            timerText.text = ((int)s.missionTime / 60).ToString("00") + ":" + ((int)s.missionTime % 60).ToString("00");
            sceneText.text = s.currentArea + (s.alarm ? "  |  ALARM ACTIVE" : "  |  NO ALARM");
            squadText.text = "TEAM\nPlayer (Commander): " + Mathf.CeilToInt(p.health) + " HP";
            foreach (var a in s.Squad) squadText.text += "\n" + a.id + ": " + (a.rescued ? "SAFE" : !a.Alive ? "KILLED" : Mathf.CeilToInt(a.health) + " HP | " + a.order);
            feedText.text = string.Join("\n", s.messages.Take(4));
            promptText.text = s.loading ? "Loading..." : SceneController.Active && SceneController.Active.player ? SceneController.Active.player.interactionPrompt : "";
            selectedText.text = "Orders: " + (s.selectedMember == "" ? "whole squad" : s.selectedMember) + "   [Tab: use mouse]";
            pausePanel.SetActive(s.paused && !s.finished); resultPanel.SetActive(s.finished);
            if (s.finished) { resultTitle.text = s.won ? "YOUR TEAM WON!" : "YOUR TEAM LOST"; resultMessage.text = s.ending + "\nGold: $" + p.gold + "    Time: " + timerText.text; }
            flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 1.4f); damageOverlay.color = new Color(1, 0, 0, flash);
        }
        public void FlashDamage() { flash = .25f; }
        public void Follow() { HeistSession.Instance.SetOrder(SquadOrder.Follow); }
        public void Attack() { HeistSession.Instance.SetOrder(SquadOrder.Attack); }
        public void ThrowGrenades() { HeistSession.Instance.SetOrder(SquadOrder.Grenade); }
        public void Retreat() { HeistSession.Instance.SetOrder(SquadOrder.Retreat); }
        public void Hold() { HeistSession.Instance.SetOrder(SquadOrder.Hold); }
        public void NextMember()
        {
            var s = HeistSession.Instance; var names = s.Squad.Where(a => a.Alive).Select(a => a.id).ToList(); names.Insert(0, "");
            s.selectedMember = names[(names.IndexOf(s.selectedMember) + 1) % names.Count];
        }
        public void Resume() { HeistSession.Instance.SetPaused(false); }
        public void Restart() { HeistSession.Instance.NewGame(HeistSession.Instance.selectedSide); }
        public void Menu() { HeistSession.Instance.MainMenu(); }
    }
}

