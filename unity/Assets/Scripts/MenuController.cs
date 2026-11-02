using UnityEngine;
using UnityEngine.UI;

namespace StudentHeist
{
    public class MenuController : MonoBehaviour
    {
        public Text selectedText, description;
        public GameObject controlsPanel;
        public Side selectedSide = Side.Robber;
        void Start() { Time.timeScale = 1; Cursor.visible = true; Cursor.lockState = CursorLockMode.None; ChooseRobber(); }
        void Select(Side side, string text) { selectedSide = side; selectedText.text = "Selected role: " + side; description.text = text; }
        public void ChooseRobber() { Select(Side.Robber, "Lead two robbers, steal $2,000 from the vault, then escape through the rear door and the sewers."); }
        public void ChoosePolice() { Select(Side.Police, "Lead two officers and stop all three robbers before their commander escapes with the gold."); }
        public void ChooseBystander() { Select(Side.Bystander, "You are unarmed. Help two bank customers and lead them to the safe zone on the street."); }
        public void StartGame() { HeistSession.GetOrCreate().NewGame(selectedSide); }
        public void Controls() { controlsPanel.SetActive(!controlsPanel.activeSelf); }
        public void Quit() { Application.Quit(); }
    }
}

