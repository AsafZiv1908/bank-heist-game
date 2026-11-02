using System;
using System.IO;
using System.Linq;
using StudentHeist;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Small integration checks in the real Unity Play Mode. No extra test package needed.
[InitializeOnLoad]
public static class HeistPlayTests
{
    static int stage;
    static double next;
    static int checks;
    static bool finishing;
    static double autonomousDeadline;
    static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../work/unity"));
    static HeistPlayTests()
    {
        if (SessionState.GetBool("HeistTestsRunning", false))
        { EditorApplication.update += Tick; SceneController.SceneReady += DisableCombat; next = EditorApplication.timeSinceStartup + 3; }
    }
    public static void Run()
    {
        SessionState.SetBool("HeistTestsRunning", true); stage = checks = 0; finishing = false;
        SceneController.SceneReady -= DisableCombat; SceneController.SceneReady += DisableCombat;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        next = EditorApplication.timeSinceStartup + 5;
    }
    static void Check(bool success, string name)
    { if (!success) throw new Exception("TEST FAILED: " + name); checks++; Debug.Log("HEIST_PASS: " + name); }
    static void DisableCombat(SceneController scene) { foreach (var actor in scene.activeActors) actor.damage = 0; }
    static void CheckRoutes(SceneController scene)
    {
        foreach (var door in Object.FindObjectsByType<Interactable>().Where(i => i.kind == Interaction.Door || i.kind == Interaction.SewerExit))
        {
            NavMeshHit start, end;
            Check(NavMesh.SamplePosition(scene.frontSpawn.position, out start, 3, NavMesh.AllAreas) && NavMesh.SamplePosition(door.transform.position, out end, 3, NavMesh.AllAreas), scene.area + ": entrance and " + door.prompt + " are navigable");
            NavMesh.SamplePosition(door.transform.position, out end, 3, NavMesh.AllAreas);
            var path = new NavMeshPath();
            Check(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, scene.area + ": complete route to " + door.prompt);
        }
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || finishing) return;
        if (!EditorApplication.isPlaying) return;
        try
        {
            var s = HeistSession.Instance; var sc = SceneController.Active;
            if (s && s.loading) { next = EditorApplication.timeSinceStartup + .2; return; }
            // Keep tests deterministic while the real navigation and queue continue running.
            if (sc) foreach (var a in sc.activeActors) if (a) a.damage = 0;
            switch (stage)
            {
                case 0:
                    Check(Object.FindFirstObjectByType<MenuController>() != null, "MainMenu has working role selector");
                    Check(Object.FindObjectsByType<Button>().All(b => b.onClick.GetPersistentEventCount() > 0), "Visible menu buttons have saved click callbacks");
                    Capture("01-main-menu"); Object.FindFirstObjectByType<MenuController>().ChooseRobber(); Object.FindFirstObjectByType<MenuController>().StartGame(); break;
                case 1:
                    CheckRoutes(sc);
                    Check(s.selectedSide == Side.Robber && sc.player != null, "Robber menu starts first-person player");
                    Check(sc.activeActors.All(a => !a.state.Alive || a.agent.isOnNavMesh), "Street NPCs on baked NavMesh");
                    Check(s.Squad.Count() == 2, "Robber has two teammates"); Capture("02-street");
                    s.Player.health = 73; s.Player.gold = 125; s.Player.ammo = 7; s.Squad.First().health = 61; s.Squad.First().gold = 40;
                    s.Travel(Area.Bank, "Front"); break;
                case 2:
                    CheckRoutes(sc);
                    Check(s.currentArea == Area.Bank && s.Player.health == 73 && s.Player.ammo == 7 && s.Player.gold == 125, "Player HP, GOLD and ammo persist in Bank");
                    Check(s.Squad.First().health == 61 && s.Squad.First().gold == 40, "NPC HP and GOLD persist");
                    Check(s.alarm && sc.activeActors.Where(a => a.state.customer).All(a => a.prone), "Customers lie down when robbers enter");
                    Check(sc.customerQueue.Count == 6, "FIFO queue contains six customers");
                    Check(Vector3.Distance(sc.activeActors.First(a=>a.state.id==s.Squad.First().id).transform.position,sc.activeActors.First(a=>a.state.id==s.Squad.Last().id).transform.position)>.65f,"Squadmates keep separate positions");
                    s.SetOrder(SquadOrder.Hold); Check(s.Squad.All(a => a.order == SquadOrder.Hold), "Hold command works");
                    s.SetOrder(SquadOrder.Retreat); Check(s.Squad.All(a => a.order == SquadOrder.Retreat), "Retreat command works");
                    s.SetOrder(SquadOrder.Attack); Check(s.Squad.All(a => a.order == SquadOrder.Attack), "Attack command works");
                    s.SetOrder(SquadOrder.Follow); Capture("03-bank");
                    var cop=sc.activeActors.First(a=>a.state.side==Side.Police);cop.TakeDamage(26);Check(cop.state.health==74,"NPC damage lowers HP");
                    s.Travel(Area.Vault,"Front");break;
                case 3:
                    CheckRoutes(sc);
                    Check(s.currentArea==Area.Vault && s.Player.health==73,"Vault transition retains player status");
                    var gold=Object.FindObjectsByType<Interactable>().Where(i=>i.kind==Interaction.Gold).OrderBy(i=>i.goldIndex).ToArray();
                    for(int i=0;i<3;i++)gold[0].Use();gold[1].Use();Check(s.Player.gold==2125,"Gold tables add money and deplete supplies");
                    Capture("04-vault");s.Travel(Area.Bank,"Vault");break;
                case 4:
                    Check(sc.activeActors.First(a=>a.state.side==Side.Police).state.health==74,"Enemy damage persists when returning to Bank");
                    var rear=sc.PortalTo(Area.BackAlley);Check(rear!=null,"Bank has separate rear escape door");rear.Use();break;
                case 5:
                    CheckRoutes(sc);
                    Check(s.currentArea==Area.BackAlley,"Rear door loads BackAlley");Capture("05-back-alley");sc.PortalTo(Area.Sewers).Use();break;
                case 6:
                    CheckRoutes(sc);
                    Check(s.currentArea==Area.Sewers&&s.Player.gold==2125,"Manhole loads Sewers with gold retained");Capture("06-sewers");
                    Object.FindObjectsByType<Interactable>().First(i=>i.kind==Interaction.SewerExit).Use();
                    Check(s.finished&&s.won,"Robber sewer escape wins mission");break;
                case 7:
                    Capture("07-victory");s.NewGame(Side.Police);break;
                case 8:
                    Check(s.selectedSide==Side.Police&&s.Squad.Count()==2,"Police role has officer squad");
                    var leader=sc.activeActors.First(a=>a.state.side==Side.Robber&&a.state.commander);leader.TakeDamage(100);
                    Check(s.actors.Any(a=>a.side==Side.Robber&&a.Alive&&a.commander),"NPC commander is replaced after death");
                    foreach(var a in sc.activeActors.Where(a=>a.state.side==Side.Robber).ToArray())a.TakeDamage(100);break;
                case 9:
                    Check(s.finished&&s.won,"Police win by stopping all robbers");s.NewGame(Side.Bystander);break;
                case 10:
                    Check(!sc.player.weapon.gameObject.activeSelf,"Bystander is unarmed");s.Travel(Area.Bank,"Front");break;
                case 11:
                    Check(!s.alarm,"Bank customers stand before breach");Capture("08-customer-queue");
                    sc.customerQueue.serviceSeconds=.1f;break;
                case 12:
                    Check(sc.customerQueue.Count<6,"FIFO service advances customer queue");
                    s.TriggerAlarm();foreach(var a in sc.activeActors.Where(a=>a.state.customer).Take(2))a.Recruit();
                    Check(s.Squad.Count()==2,"Bystander recruits customers");s.Travel(Area.Street,"Rear");break;
                case 13:
                    foreach(var a in sc.activeActors.Where(a=>a.state.recruited))a.agent.Warp(sc.player.transform.position+Vector3.right*(a.state.id.EndsWith("1")?1.5f:-1.5f));
                    Object.FindObjectsByType<Interactable>().First(i=>i.kind==Interaction.CivilianExit).Use();Check(s.finished&&s.won&&s.rescuedCount==2,"Two customers evacuate safely");s.NewGame(Side.Robber);break;
                case 14:
                    var target=s.actors.First(a=>a.side==Side.Police);target.area=Area.Street;target.position=sc.player.transform.position+Vector3.forward*6;sc.RefreshNPCs();
                    var enemy=sc.FindActor(target.id);enemy.damage=0;enemy.agent.isStopped=true;Physics.SyncTransforms();
                    sc.player.viewCamera.transform.LookAt(enemy.transform.position+Vector3.up*1.2f);sc.player.Fire();
                    Check(enemy.state.health==74 && s.Player.ammo==11,"First-person raycast shooting damages target and consumes ammo");
                    sc.player.Reload();break;
                case 15:
                    Check(s.Player.ammo==12,"Reload refills magazine after delay");
                    var grenadeTarget=sc.activeActors.First(a=>a.state.side==Side.Police);Grenade.Spawn(grenadeTarget.transform.position+Vector3.up*.3f,Vector3.zero,Side.Robber);break;
                case 16:
                    Check(sc.activeActors.First(a=>a.state.side==Side.Police).state.health==9,"Physics grenade explodes and applies damage");
                    int grenades=s.Player.grenades;sc.player.ThrowGrenade();Check(s.Player.grenades==grenades-1,"Player grenade command consumes supply");
                    s.SetPaused(true);Check(Time.timeScale==0&&s.paused,"Pause stops game time");break;
                case 17:
                    Check(GameHUD.Active.pausePanel.activeSelf,"Pause UI is visible");s.SetPaused(false);Check(Time.timeScale==1,"Resume restores game time");
                    var mate=sc.FindActor(s.Squad.First().id);mate.agent.Warp(sc.player.transform.position+new Vector3(2,0,1));int oldGrenades=mate.state.grenades;mate.ThrowAtEnemy();Check(mate.state.grenades==oldGrenades-1,"NPC grenade command consumes teammate supply");
                    sc.player.TakeDamage(100);Check(s.finished&&!s.won,"Player death displays defeat");break;
                case 18:
                    Capture("09-defeat");s.NewGame(Side.Police);break;
                case 19:
                    s.missionTime=26;s.TriggerAlarm();Time.timeScale=8;autonomousDeadline=EditorApplication.timeSinceStartup+45;break;
                case 20:
                    if(!s.finished && EditorApplication.timeSinceStartup<autonomousDeadline){next=EditorApplication.timeSinceStartup+.5;return;}
                    Check(s.finished&&!s.won&&s.ending.Contains("sewers"),"Autonomous NPC commander completes bank-vault-rear-door-sewer escape");
                    s.NewGame(Side.Robber);break;
                case 21:
                    s.missionTime=601;break;
                case 22:
                    Check(s.finished&&!s.won&&s.ending.Contains("Time ran out"),"Mission timer ends overdue operation");
                    Finish(true,"HEIST_PLAY_TESTS_SUCCESS: "+checks+" integration checks passed.");return;
            }
            stage++;next=EditorApplication.timeSinceStartup+(stage==12||stage==15||stage==16?2:1.3);
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory(Output);var camera=Camera.main;if(!camera)camera=Object.FindFirstObjectByType<Camera>();if(!camera)return;
        var canvas=Object.FindFirstObjectByType<Canvas>();var mode=canvas.renderMode;var oldCam=canvas.worldCamera;
        var rt=new RenderTexture(1280,720,24);var previous=camera.targetTexture;camera.targetTexture=rt;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.4f;Canvas.ForceUpdateCanvases();camera.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
        RenderTexture.active=old;camera.targetTexture=previous;canvas.renderMode=mode;canvas.worldCamera=oldCam;Object.Destroy(image);Object.Destroy(rt);
    }
    static void Finish(bool success,string report)
    {
        finishing=true;SessionState.SetBool("HeistTestsRunning",false);EditorApplication.update-=Tick;Time.timeScale=1;
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"play-test-results.txt"),report);Debug.Log(report);
        EditorApplication.isPlaying=false;
        EditorApplication.delayCall+=()=>EditorApplication.Exit(success?0:1);
    }
}

