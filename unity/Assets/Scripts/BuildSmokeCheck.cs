using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StudentHeist
{
    // Dormant in normal play. Used to check the actual Windows build with -heist-smoke.
    public class BuildSmokeCheck : MonoBehaviour
    {
        string output;
        bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Activate()
        {
            if (!Environment.GetCommandLineArgs().Contains("-heist-smoke")) return;
            var go = new GameObject("Windows build smoke check"); DontDestroyOnLoad(go); go.AddComponent<BuildSmokeCheck>();
        }
        void Awake()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-heist-output");
            output = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath(Application.dataPath + "/../QA");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
            SceneController.SceneReady += DisableCombat;
        }
        void OnDestroy() { Application.logMessageReceived -= OnLog; SceneController.SceneReady -= DisableCombat; }
        void DisableCombat(SceneController scene) { foreach (var a in scene.activeActors) a.damage = 0; }
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            { failed = true; File.AppendAllText(Path.Combine(output, "build-errors.txt"), message + "\n" + stack + "\n"); }
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2);
            var menu = FindFirstObjectByType<MenuController>();
            if (!menu) { Finish(false, "Menu missing from Windows build."); yield break; }
            menu.ChooseRobber(); menu.StartGame(); yield return new WaitForSecondsRealtime(2);
            var s = HeistSession.Instance;
            if (!s || !SceneController.Active.player) { Finish(false, "First-person player did not start."); yield break; }
            s.Player.gold = 2000; s.Player.health = 89; s.Travel(Area.Bank, "Front"); yield return new WaitForSecondsRealtime(2);
            if (s.Player.health != 89 || s.currentArea != Area.Bank) { Finish(false, "Scene persistence failed."); yield break; }
            Capture("Windows-Bank.png");
            if (!Resources.Load<Material>("Tracer") || !Resources.Load<Material>("Blast")) { Finish(false, "Combat materials missing from build."); yield break; }
            Grenade.Spawn(new Vector3(3, 1, -7), Vector3.zero, Side.Robber); yield return new WaitForSecondsRealtime(2);
            SceneController.Active.PortalTo(Area.BackAlley).Use(); yield return new WaitForSecondsRealtime(1);
            SceneController.Active.PortalTo(Area.Sewers).Use(); yield return new WaitForSecondsRealtime(1);
            if (s.currentArea != Area.Sewers || s.Player.gold != 2000) { Finish(false, "Sewer route failed."); yield break; }
            Capture("Windows-Sewers.png");
            FindObjectsByType<Interactable>().First(i => i.kind == Interaction.SewerExit).Use(); yield return new WaitForSecondsRealtime(.5f);
            Finish(s.finished && s.won && !failed, "Menu, first-person player, bank/alley/sewer transitions, HP/GOLD persistence, grenade assets and victory checked in the Windows executable.");
        }
        void Capture(string name)
        {
            var camera = Camera.main; var canvas = FindFirstObjectByType<Canvas>();
            var rt = new RenderTexture(1280, 720, 24); var previous = camera.targetTexture; camera.targetTexture = rt;
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .4f;
            Canvas.ForceUpdateCanvases(); camera.Render();
            var active = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG());
            RenderTexture.active = active; camera.targetTexture = previous; canvas.renderMode = mode; canvas.worldCamera = oldCamera; Destroy(texture); Destroy(rt);
        }
        void Finish(bool success, string reason)
        {
            string report = (success ? "WINDOWS_SMOKE_SUCCESS: " : "WINDOWS_SMOKE_FAILED: ") + reason;
            File.WriteAllText(Path.Combine(output, "build-smoke-results.txt"), report); Debug.Log(report); Application.Quit(success ? 0 : 1);
        }
    }
}
