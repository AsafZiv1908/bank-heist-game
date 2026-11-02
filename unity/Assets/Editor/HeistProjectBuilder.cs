using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudentHeist;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Creates editable Unity assets, prefabs, Canvas controls and actual scene files.
public static class HeistProjectBuilder
{
    const string Root = "Assets/BankHeist";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static GameObject playerPrefab, robberPrefab, policePrefab, customerPrefab;
    static Font font;
    static Transform environment;
    static SceneController director;
    static readonly string[] SceneNames = { "MainMenu", "Street", "Bank", "Vault", "BackAlley", "Sewers" };

    [MenuItem("Bank Heist/Rebuild Demo Scenes and Prefabs")]
    public static void Generate()
    {
        Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Textures");
        Directory.CreateDirectory(Root + "/Prefabs"); Directory.CreateDirectory(Root + "/Navigation"); Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh(); font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Configure(); MakeMaterials(); MakePrefabs();
        BuildMenu();
        foreach (Area area in Enum.GetValues(typeof(Area))) BuildScene(area);
        EditorBuildSettings.scenes = SceneNames.Select(n => new EditorBuildSettingsScene("Assets/Scenes/" + n + ".unity", true)).ToArray();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Validate(); Debug.Log("HEIST_GENERATION_SUCCESS");
    }
    public static void GenerateAndTest() { Generate(); HeistPlayTests.Run(); }
    [MenuItem("Bank Heist/Open Game Menu")]
    public static void OpenForUser()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("Window/General/Game");
    }
    static void Configure()
    {
        PlayerSettings.companyName = "Student Project"; PlayerSettings.productName = "Bank Heist 3D";
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true; PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input = settings.FindProperty("activeInputHandler"); if (input != null) input.intValue = 0;
        settings.ApplyModifiedPropertiesWithoutUndo();
        QualitySettings.vSyncCount = 1; QualitySettings.pixelLightCount = 6; QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 45; QualitySettings.shadowResolution = ShadowResolution.Medium;
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(8).stringValue = "NPC";
        tags.FindProperty("layers").GetArrayElementAtIndex(9).stringValue = "Player"; tags.ApplyModifiedPropertiesWithoutUndo();
    }
    static Material Mat(string name, Color color, float smooth = .12f)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; m.SetFloat("_Glossiness", smooth); mats[name] = m; return m;
    }
    static void MakeMaterials()
    {
        mats.Clear();
        Directory.CreateDirectory("Assets/Resources");
        foreach (var entry in new[] { new[] { "Tracer", "Sprites/Default" }, new[] { "Blast", "Particles/Standard Unlit" } })
        {
            string path = "Assets/Resources/" + entry[0] + ".mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.CreateAsset(new Material(Shader.Find(entry[1])), path);
        }
        Mat("Plaster", new Color(.68f,.69f,.66f)); Mat("Concrete",new Color(.43f,.45f,.43f));
        Mat("Dark",new Color(.09f,.105f,.11f)); Mat("Asphalt",new Color(.16f,.18f,.19f));
        Mat("Wood",new Color(.34f,.22f,.14f)); Mat("Metal",new Color(.29f,.33f,.36f),.4f);
        Mat("Gold",new Color(.85f,.61f,.13f),.5f); mats["Gold"].SetFloat("_Metallic",.55f);
        Mat("Police",new Color(.13f,.22f,.38f)); Mat("Robber",new Color(.23f,.25f,.25f));
        Mat("Skin",new Color(.66f,.48f,.35f)); Mat("Customer",new Color(.53f,.42f,.29f));
        Mat("Gloves",new Color(.15f,.32f,.46f)); Mat("White",new Color(.84f,.83f,.76f));
        Mat("Red",new Color(.62f,.10f,.08f)); Mat("Green",new Color(.14f,.42f,.22f));
        Mat("Water",new Color(.11f,.22f,.17f),.7f); Mat("Brick",new Color(.39f,.28f,.21f));
        Mat("Light",new Color(.92f,.91f,.7f)); mats["Light"].EnableKeyword("_EMISSION"); mats["Light"].SetColor("_EmissionColor",new Color(.7f,.7f,.5f));
        Mat("Glass",new Color(.32f,.51f,.57f,.24f),.6f);
        var glass=mats["Glass"]; glass.SetFloat("_Mode",3); glass.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);glass.EnableKeyword("_ALPHABLEND_ON");glass.renderQueue=3000;
        var texture = new Texture2D(64,64); texture.filterMode=FilterMode.Point; texture.wrapMode=TextureWrapMode.Repeat;
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){bool light=(x/32+y/32)%2==0;bool seam=x%32==0||y%32==0;texture.SetPixel(x,y,seam?new Color(.25f,.27f,.25f):light?new Color(.65f,.66f,.59f):new Color(.26f,.32f,.29f));}
        texture.Apply(); File.WriteAllBytes(Root+"/Textures/BankTiles.png",texture.EncodeToPNG());AssetDatabase.Refresh();
        var floor=Mat("BankTiles",Color.white);floor.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/BankTiles.png");floor.mainTextureScale=new Vector2(14,16);
        var brickTex=new Texture2D(64,64);for(int y=0;y<64;y++)for(int x=0;x<64;x++){bool mortar=y%16<2||(x+(y/16%2)*16)%32<2;float n=((x*17+y*31)%13)/160f;brickTex.SetPixel(x,y,mortar?new Color(.2f,.2f,.18f):new Color(.43f+n,.30f+n,.22f+n));}brickTex.Apply();File.WriteAllBytes(Root+"/Textures/Brick.png",brickTex.EncodeToPNG());AssetDatabase.Refresh();mats["Brick"].mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/Brick.png");mats["Brick"].mainTextureScale=new Vector2(8,3);
    }
    static GameObject Cube(string name, Vector3 pos, Vector3 scale, string material, Transform parent=null, bool solid=true)
    {
        return Shape(PrimitiveType.Cube,name,pos,scale,material,parent,solid);
    }
    static GameObject Shape(PrimitiveType type,string name,Vector3 pos,Vector3 scale,string material,Transform parent=null,bool solid=true)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent?parent:environment,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mats[material];
        if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    static Transform Group(string name,Transform parent=null,Vector3 pos=default(Vector3))
    {var g=new GameObject(name);g.transform.SetParent(parent?parent:environment,false);g.transform.localPosition=pos;return g.transform;}
    static TextMesh Label(string text,Vector3 pos,float size= .2f,Color? color=null,Quaternion? rotation=null,Transform parent=null)
    {
        var g=new GameObject(text);g.transform.SetParent(parent?parent:environment,false);g.transform.localPosition=pos;g.transform.localRotation=rotation??Quaternion.identity;
        var t=g.AddComponent<TextMesh>();t.text=text;t.font=font;t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color??Color.white;
        t.GetComponent<MeshRenderer>().sharedMaterial=font.material;return t;
    }
    static void MakePrefabs()
    {
        environment=null;
        robberPrefab=MakeActor(Side.Robber);policePrefab=MakeActor(Side.Police);customerPrefab=MakeActor(Side.Bystander);
        var root=new GameObject("First Person Player");root.tag="Player";root.layer=9;
        var c=root.AddComponent<CharacterController>();c.height=1.85f;c.radius=.36f;c.center=Vector3.up*.95f;c.stepOffset=.3f;
        var player=root.AddComponent<PlayerController>();var camera=new GameObject("Player Camera");camera.transform.SetParent(root.transform);camera.transform.localPosition=new Vector3(0,1.68f,0);camera.tag="MainCamera";
        player.viewCamera=camera.AddComponent<Camera>();player.viewCamera.fieldOfView=70;player.viewCamera.nearClipPlane=.05f;camera.AddComponent<AudioListener>();
        var weapon=Group("First Person Pistol and Hands",camera.transform,new Vector3(.27f,-.26f,.5f));player.weapon=weapon;
        Cube("Pistol slide",new Vector3(0,0,.07f),new Vector3(.095f,.12f,.34f),"Metal",weapon,false);
        Cube("Pistol grip",new Vector3(0,-.1f,-.025f),new Vector3(.075f,.19f,.1f),"Dark",weapon,false);
        Cube("Barrel",new Vector3(0,.005f,.26f),new Vector3(.065f,.065f,.05f),"Dark",weapon,false);
        Cube("Front sight",new Vector3(0,.075f,.17f),new Vector3(.02f,.025f,.025f),"White",weapon,false);
        Cube("Right glove",new Vector3(.02f,-.14f,-.03f),new Vector3(.14f,.14f,.17f),"Gloves",weapon,false);
        var arm=Cube("Sleeve",new Vector3(.10f,-.25f,-.18f),new Vector3(.17f,.22f,.4f),"Robber",weapon,false);arm.transform.localRotation=Quaternion.Euler(-15,-20,0);
        Cube("Left glove",new Vector3(-.09f,-.1f,.015f),new Vector3(.11f,.12f,.15f),"Gloves",weapon,false);
        playerPrefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/FirstPersonPlayer.prefab");Object.DestroyImmediate(root);
    }
    static GameObject MakeActor(Side side)
    {
        var root=new GameObject(side+" NPC");root.layer=8;var actor=root.AddComponent<Actor>();var agent=root.GetComponent<NavMeshAgent>();agent.radius=.4f;agent.height=1.85f;agent.baseOffset=0;
        var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,.9f,0);collider.height=1.8f;collider.radius=.34f;
        var body=Group("Character Model",root.transform);actor.model=body;string suit=side==Side.Police?"Police":side==Side.Robber?"Robber":"Customer";
        Cube("Torso",new Vector3(0,1.12f,0),new Vector3(.58f,.62f,.32f),suit,body,false);
        Cube("Belt",new Vector3(0,.86f,0),new Vector3(.6f,.10f,.34f),"Dark",body,false);
        Shape(PrimitiveType.Sphere,"Head",new Vector3(0,1.65f,0),new Vector3(.34f,.40f,.34f),"Skin",body,false);
        if(side!=Side.Bystander)
        {
            Cube("Protective vest",new Vector3(0,1.16f,.18f),new Vector3(.48f,.47f,.09f),"Dark",body,false);
            for(int i=0;i<3;i++)Cube("Vest pouch",new Vector3((i-1)*.15f,1.08f,.26f),new Vector3(.1f,.18f,.08f),suit,body,false);
            if(side==Side.Police){Shape(PrimitiveType.Sphere,"Helmet",new Vector3(0,1.78f,0),new Vector3(.4f,.2f,.4f),"Dark",body,false);Cube("Visor",new Vector3(0,1.70f,.19f),new Vector3(.3f,.09f,.04f),"Metal",body,false);}
            else{Cube("Simple mask",new Vector3(0,1.61f,.17f),new Vector3(.30f,.23f,.07f),"White",body,false);for(int i=-1;i<=1;i+=2)Cube("Eye slit",new Vector3(i*.08f,1.68f,.215f),new Vector3(.065f,.028f,.015f),"Dark",body,false);}
        }
        actor.leftLeg=Group("Left leg pivot",body,new Vector3(-.16f,.82f,0));actor.rightLeg=Group("Right leg pivot",body,new Vector3(.16f,.82f,0));
        foreach(var leg in new[]{actor.leftLeg,actor.rightLeg}){Cube("Trouser leg",new Vector3(0,-.36f,0),new Vector3(.22f,.70f,.25f),suit,leg,false);Cube("Boot",new Vector3(0,-.75f,.045f),new Vector3(.24f,.15f,.37f),"Dark",leg,false);}
        actor.leftArm=Group("Left arm pivot",body,new Vector3(-.38f,1.36f,0));actor.rightArm=Group("Right arm pivot",body,new Vector3(.38f,1.36f,0));
        foreach(var arm in new[]{actor.leftArm,actor.rightArm}){Cube("Sleeve",new Vector3(0,-.23f,0),new Vector3(.19f,.44f,.2f),suit,arm,false);Cube("Hand",new Vector3(0,-.49f,0),new Vector3(.17f,.15f,.18f),side==Side.Bystander?"Skin":"Gloves",arm,false);if(side!=Side.Bystander)arm.localRotation=Quaternion.Euler(-70,0,0);}
        if(side!=Side.Bystander){Cube("Rifle",new Vector3(.23f,1.18f,.49f),new Vector3(.10f,.14f,.65f),"Dark",body,false);Cube("Rifle magazine",new Vector3(.23f,1.07f,.47f),new Vector3(.08f,.16f,.1f),"Metal",body,false);}
        actor.nameLabel=Label(side.ToString(),new Vector3(0,2.15f,0),.11f,Color.white,null,root.transform);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+side+"NPC.prefab");Object.DestroyImmediate(root);return prefab;
    }
    static void NewScene(string name,bool outdoor)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        environment=new GameObject("Environment - editable level geometry").transform;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=outdoor?new Color(.48f,.52f,.59f):new Color(.53f,.53f,.50f);
        RenderSettings.fog=outdoor;RenderSettings.fogColor=new Color(.28f,.32f,.38f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=35;RenderSettings.fogEndDistance=100;
        var sun=new GameObject("Directional Light");var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.intensity=outdoor?.9f:.35f;light.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-30,0);
    }
    static void PointLight(Vector3 p,float range=10,float intensity=1.6f,Color? color=null)
    {var g=Group("Ceiling light",environment,p).gameObject;var l=g.AddComponent<Light>();l.type=LightType.Point;l.range=range;l.intensity=intensity;l.color=color??new Color(1,.96f,.81f);}
    static void Room(float w,float d,string floor,string wall,bool ceiling=true)
    {
        Cube("Floor",new Vector3(0,-.2f,0),new Vector3(w,.4f,d),floor);
        Cube("West wall",new Vector3(-w/2,2,0),new Vector3(.35f,4,d),wall);
        Cube("East wall",new Vector3(w/2,2,0),new Vector3(.35f,4,d),wall);
        // Walkable front and rear doorway openings.
        foreach(float z in new[]{-d/2,d/2})
        {Cube("Wall left of door",new Vector3(-(w/4+1),2,z),new Vector3(w/2-2,4,.35f),wall);Cube("Wall right of door",new Vector3(w/4+1,2,z),new Vector3(w/2-2,4,.35f),wall);Cube("Door lintel",new Vector3(0,3.5f,z),new Vector3(4,1,.35f),wall);}
        if(ceiling)Cube("Ceiling",new Vector3(0,4.1f,0),new Vector3(w,.2f,d),"Concrete");
    }
    static Transform Spawn(string name,Vector3 position,float yaw=0)
    {var t=Group(name,null,position);t.rotation=Quaternion.Euler(0,yaw,0);return t;}
    static void Door(string label,Vector3 p,Area target,string arrival="Front",float yaw=0)
    {
        var root=Group(label+" door",environment,p);root.localRotation=Quaternion.Euler(0,yaw,0);
        Cube("Door panel",new Vector3(0,1.35f,.1f),new Vector3(2.6f,2.7f,.18f),"Metal",root);
        Cube("Door frame top",new Vector3(0,2.85f,.05f),new Vector3(3,.18f,.35f),"Dark",root);
        foreach(float x in new[]{-1.45f,1.45f})Cube("Door frame",new Vector3(x,1.4f,0),new Vector3(.15f,2.8f,.35f),"Dark",root);
        Cube("Handle",new Vector3(.8f,1.2f,-.08f),new Vector3(.3f,.07f,.08f),"Gold",root,false);
        var t=Label(label,new Vector3(0,3.12f,-.12f),.18f,Color.white,null,root);t.transform.localRotation=Quaternion.identity;
        var marker=Group("Interact - "+label,root,new Vector3(0,1,-.65f)).gameObject;var interact=marker.AddComponent<Interactable>();interact.kind=Interaction.Door;interact.destination=target;interact.arrival=arrival;interact.prompt=label;
    }
    static void BuildScene(Area area)
    {
        bool outdoor=area==Area.Street||area==Area.BackAlley;NewScene(area.ToString(),outdoor);
        director=new GameObject("Scene Controller - spawns and navigation").AddComponent<SceneController>();director.area=area;director.playerPrefab=playerPrefab;director.robberPrefab=robberPrefab;director.policePrefab=policePrefab;director.customerPrefab=customerPrefab;
        if(area==Area.Street)Street();if(area==Area.Bank)Bank();if(area==Area.Vault)Vault();if(area==Area.BackAlley)Alley();if(area==Area.Sewers)Sewer();
        if(!director.rearSpawn)director.rearSpawn=director.frontSpawn;if(!director.vaultSpawn)director.vaultSpawn=director.frontSpawn;
        director.retreatPoint=Spawn("NPC Retreat Point",director.frontSpawn.position+director.frontSpawn.forward*1.5f);
        var camera=new GameObject("Editor Preview Camera (disabled during play)");camera.tag="MainCamera";camera.transform.position=director.frontSpawn.position+Vector3.up*1.7f;camera.transform.rotation=director.frontSpawn.rotation;camera.AddComponent<Camera>();camera.AddComponent<PreviewCamera>();
        MakeHUD();BakeNavMesh(area);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/Scenes/"+area+".unity");
    }
    static void Street()
    {
        Room(46,42,"Concrete","Brick",false);Cube("Road",new Vector3(0,.01f,-4),new Vector3(45,.03f,13),"Asphalt");
        for(int x=-20;x<=20;x+=5)Cube("Road marking",new Vector3(x,.04f,-4),new Vector3(2.5f,.025f,.15f),"White",null,false);
        for(int z=-10;z<=1;z+=2)Cube("Crosswalk",new Vector3(0,.04f,z),new Vector3(4,.025f,.75f),"White",null,false);
        Cube("Bank facade",new Vector3(0,4,15),new Vector3(34,8,5),"Plaster");
        Cube("Bank sign",new Vector3(0,5.5f,12.35f),new Vector3(14,1.3f,.2f),"Dark");Label("MERIDIAN BANK",new Vector3(0,5.5f,12.2f),.33f,Color.white,Quaternion.identity);
        for(int x=-14;x<=14;x+=7){if(x==0)continue;Cube("Facade window",new Vector3(x,3,12.4f),new Vector3(3.5f,3.4f,.1f),"Glass");Cube("Stone column",new Vector3(x+2.3f,3,11.8f),new Vector3(.7f,6,.8f),"Concrete");}
        Door("ENTER BANK",new Vector3(0,0,12.2f),Area.Bank);
        for(int i=0;i<3;i++){float x=-18+i*18;Cube("Street building",new Vector3(x,4.5f,-18),new Vector3(13,9,5),i%2==0?"Brick":"Concrete");for(int y=2;y<8;y+=2)for(int q=-1;q<=1;q++)Cube("Window",new Vector3(x+q*3,y,-15.4f),new Vector3(1.5f,1.2f,.1f),"Glass");}
        Car(new Vector3(-10,0,-3),"Dark",false);Car(new Vector3(12,0,-7),"White",true);
        for(int x=-15;x<=15;x+=10){Cube("Street lamp pole",new Vector3(x,2.4f,7),new Vector3(.12f,4.8f,.12f),"Metal");Shape(PrimitiveType.Sphere,"Lamp",new Vector3(x,4.7f,7),Vector3.one*.5f,"Light");PointLight(new Vector3(x,4.5f,7),12,2);}
        var safe=Group("Civilian safe zone",null,new Vector3(-16,.01f,7));Cube("Safe zone floor",Vector3.zero,new Vector3(6,.05f,5),"Green",safe,false);Label("CIVILIAN SAFE ZONE",new Vector3(0,2,0),.15f,Color.white,Quaternion.identity,safe);var exit=safe.gameObject.AddComponent<Interactable>();exit.kind=Interaction.CivilianExit;exit.prompt="Evacuate customers";
        director.frontSpawn=Spawn("Front Spawn",new Vector3(0,.1f,-12));director.rearSpawn=Spawn("Bank Return Spawn",new Vector3(0,.1f,8),180);
    }
    static void Car(Vector3 p,string color,bool police)
    {
        var t=Group(police?"Police car":"Parked car",null,p);Cube("Body",new Vector3(0,.7f,0),new Vector3(2,.65f,4.2f),color,t);Cube("Cabin",new Vector3(0,1.25f,-.25f),new Vector3(1.7f,.65f,2.2f),"Glass",t);
        foreach(int x in new[]{-1,1})foreach(float z in new[]{-1.4f,1.4f}){var wheel=Shape(PrimitiveType.Cylinder,"Wheel",new Vector3(x,.43f,z),new Vector3(.7f,.16f,.7f),"Dark",t);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
        if(police){Cube("Red light",new Vector3(-.4f,1.67f,-.2f),new Vector3(.4f,.15f,.25f),"Red",t);Cube("Blue light",new Vector3(.4f,1.67f,-.2f),new Vector3(.4f,.15f,.25f),"Police",t);}
    }
    static void Bank()
    {
        Room(28,32,"BankTiles","Plaster");
        Door("TO STREET",new Vector3(0,0,-15.5f),Area.Street,"Rear",180);
        Door("VAULT",new Vector3(0,0,15.4f),Area.Vault);
        // Rear escape door is visually separate from the front entrance.
        Door("REAR EXIT",new Vector3(12.7f,0,10.5f),Area.BackAlley,"Front",90);
        for(int x=-10;x<=10;x+=20)for(int z=-8;z<=8;z+=8){Cube("Lobby pillar",new Vector3(x,2,z),new Vector3(.6f,4,.6f),"Plaster");Cube("Pillar base",new Vector3(x,.15f,z),new Vector3(.8f,.3f,.8f),"Dark");}
        for(int x=-8;x<=8;x+=16)
        {
            Cube("Teller counter",new Vector3(x,.6f,8),new Vector3(6,1.2f,1.15f),"Wood");Cube("Countertop",new Vector3(x,1.25f,8),new Vector3(6.3f,.12f,1.3f),"Dark");
            for(int q=-2;q<=2;q+=2){Cube("Teller monitor",new Vector3(x+q,1.6f,8),new Vector3(.65f,.4f,.12f),"Dark");Cube("Screen",new Vector3(x+q,1.61f,7.92f),new Vector3(.55f,.31f,.02f),"Police");}
        }
        Label("CUSTOMER SERVICE",new Vector3(-7,2.8f,10),.13f,Color.black,Quaternion.identity);
        var queue=new GameObject("Customer Queue (FIFO)").AddComponent<QueueController>();queue.queueSlots=new Transform[6];
        for(int i=0;i<6;i++){queue.queueSlots[i]=Spawn("Queue slot "+i,new Vector3(-7,0,5-i*1.35f));foreach(float x in new[]{-8.3f,-5.7f}){Shape(PrimitiveType.Cylinder,"Queue post",new Vector3(x,.5f,5-i*1.35f),new Vector3(.09f,.5f,.09f),"Metal");if(i<5)Cube("Queue rope",new Vector3(x,.85f,4.3f-i*1.35f),new Vector3(.035f,.04f,1.35f),"Red",null,false);}}
        queue.waitingArea=Spawn("Customers leaving teller",new Vector3(5,0,-7));director.customerQueue=queue;
        for(int side=-1;side<=1;side+=2)
        {
            var bench=Group("Waiting bench",null,new Vector3(side*7,0,-7));Cube("Seat",new Vector3(0,.48f,0),new Vector3(3,.15f,.6f),"Wood",bench);Cube("Back",new Vector3(0,.9f,.3f),new Vector3(3,.75f,.1f),"Wood",bench);foreach(float x in new[]{-1.2f,1.2f})Cube("Leg",new Vector3(x,.25f,0),new Vector3(.1f,.5f,.5f),"Metal",bench);
        }
        for(int side=-1;side<=1;side+=2){var plant=Group("Potted plant",null,new Vector3(side*11,0,-12));Shape(PrimitiveType.Cylinder,"Pot",new Vector3(0,.35f,0),new Vector3(.65f,.35f,.65f),"Wood",plant);Shape(PrimitiveType.Sphere,"Leaves",new Vector3(0,1,0),new Vector3(1,1.3f,1),"Green",plant,false);}
        for(int z=-10;z<=10;z+=5){Cube("Ceiling panel strip",new Vector3(0,3.92f,z),new Vector3(27,.04f,.05f),"Metal",null,false);for(int x=-6;x<=6;x+=6){Cube("Fluorescent light",new Vector3(x,3.8f,z),new Vector3(1.8f,.08f,.6f),"Light",null,false);PointLight(new Vector3(x,3.4f,z),8,1.1f);}}
        for(int x=-12;x<=12;x+=4)Cube("Ceiling grid",new Vector3(x,3.93f,0),new Vector3(.05f,.04f,31),"Metal",null,false);
        Cube("Red ceiling pipe",new Vector3(-12,3.5f,0),new Vector3(.10f,.10f,31),"Red",null,false);
        director.frontSpawn=Spawn("Front Spawn",new Vector3(0,.1f,-11));director.vaultSpawn=Spawn("Vault Return Spawn",new Vector3(0,.1f,12),180);director.rearSpawn=Spawn("Alley Return Spawn",new Vector3(10,.1f,10),270);
    }
    static void Vault()
    {
        Room(20,24,"Concrete","Concrete");Door("BANK LOBBY",new Vector3(0,0,-11.5f),Area.Bank,"Vault",180);
        Shape(PrimitiveType.Cylinder,"Open vault door",new Vector3(3,1.6f,-9.7f),new Vector3(3.2f,.18f,3.2f),"Metal").transform.rotation=Quaternion.Euler(90,0,25);
        for(int side=-1;side<=1;side+=2)for(int z=-5;z<=8;z+=3)
        {Cube("Deposit box cabinet",new Vector3(side*8.7f,1.4f,z),new Vector3(1.2f,2.8f,2.5f),"Metal");for(int y=0;y<4;y++)Cube("Deposit handle",new Vector3(side*8.02f,.4f+y*.6f,z),new Vector3(.1f,.1f,.5f),"Gold");}
        for(int i=0;i<2;i++)
        {
            var table=Group("Gold table "+i,null,new Vector3(i==0?-4.5f:4.5f,0,4));Cube("Table",new Vector3(0,.85f,0),new Vector3(2.6f,.2f,1.4f),"Metal",table);foreach(float x in new[]{-1,1})Cube("Table legs",new Vector3(x,.4f,0),new Vector3(.12f,.8f,1.2f),"Dark",table);
            for(int row=0;row<3;row++)for(int col=0;col<4;col++)Cube("Gold bar",new Vector3((col-1.5f)*.5f,1.03f,row*.35f-.35f),new Vector3(.43f,.16f,.27f),"Gold",table);
            var marker=Group("Take gold",table,new Vector3(0,1,-.8f)).gameObject;var loot=marker.AddComponent<Interactable>();loot.kind=Interaction.Gold;loot.goldIndex=i;loot.prompt="Take $500 gold";Label("GOLD - PRESS E",new Vector3(0,1.9f,0),.16f,new Color(1,.8f,.3f),Quaternion.identity,table);
        }
        for(int z=-6;z<=6;z+=6){Cube("Vault light",new Vector3(0,3.8f,z),new Vector3(2,.1f,.5f),"Light",null,false);PointLight(new Vector3(0,3.3f,z),13,2.5f);}
        director.frontSpawn=Spawn("Front Spawn",new Vector3(0,.1f,-8));
    }
    static void Alley()
    {
        Room(14,40,"Concrete","Brick",false);Door("BANK REAR DOOR",new Vector3(0,0,-19.4f),Area.Bank,"Rear",180);
        for(int z=-12;z<=10;z+=11){Cube("Wall drain pipe",new Vector3(-6.6f,2,z),new Vector3(.18f,4,.18f),"Metal");PointLight(new Vector3(5.5f,3,z),10,2);Cube("Wall lamp",new Vector3(6.5f,3,z),new Vector3(.3f,.4f,.25f),"Light");}
        Cube("Dumpster",new Vector3(-5,.8f,-5),new Vector3(2.3f,1.6f,3),"Green");Cube("Dumpster lid",new Vector3(-5,1.65f,-5),new Vector3(2.4f,.16f,3.1f),"Dark");
        for(int i=0;i<3;i++)Cube("Wood crate",new Vector3(5,.55f,2+i*1.3f),new Vector3(1,1.1f,1.1f),"Wood");
        Shape(PrimitiveType.Cylinder,"Open manhole",new Vector3(0,.015f,14),new Vector3(2.7f,.04f,2.7f),"Dark",null,false);
        Shape(PrimitiveType.Cylinder,"Manhole cover",new Vector3(2,.06f,14),new Vector3(2.5f,.06f,2.5f),"Metal");
        for(int i=-1;i<=1;i++)Cube("Ladder rung",new Vector3(0,.08f,14+i*.4f),new Vector3(1,.1f,.08f),"Metal",null,false);
        var sewer=Group("Enter sewer interaction",null,new Vector3(0,.3f,13)).gameObject;var action=sewer.AddComponent<Interactable>();action.kind=Interaction.Door;action.destination=Area.Sewers;action.prompt="Climb down into the sewers";
        Label("SEWER ENTRANCE",new Vector3(0,2,17),.26f,new Color(.75f,1,.7f),Quaternion.identity);
        director.frontSpawn=Spawn("Front Spawn",new Vector3(0,.1f,-15));director.rearSpawn=Spawn("Sewer Return Spawn",new Vector3(0,.1f,11),180);
    }
    static void Sewer()
    {
        Room(9,40,"Concrete","Brick");Cube("Sewer water channel",new Vector3(0,.018f,0),new Vector3(2,.03f,39),"Water",null,false);
        for(int z=-16;z<=16;z+=4){Cube("Ceiling beam",new Vector3(0,3.7f,z),new Vector3(9,.3f,.25f),"Metal");foreach(float x in new[]{-4.3f,4.3f})Cube("Support",new Vector3(x,2,z),new Vector3(.25f,4,.3f),"Metal");}
        Cube("Main sewer pipe",new Vector3(-3.8f,2.5f,0),new Vector3(.35f,.35f,39),"Red",null,false);
        for(int z=-12;z<=16;z+=7){PointLight(new Vector3(3.7f,2.9f,z),9,2,new Color(.8f,.9f,.65f));Cube("Tunnel lamp",new Vector3(4,2.9f,z),new Vector3(.2f,.35f,.2f),"Light",null,false);}
        Door("BACK TO ALLEY",new Vector3(0,0,-19.3f),Area.BackAlley,"Rear",180);
        var exit=Group("Sewer Escape - End Mission",null,new Vector3(0,1,18)).gameObject;var interaction=exit.AddComponent<Interactable>();interaction.kind=Interaction.SewerExit;interaction.prompt="Escape through the sewers";
        Cube("Escape floor marker",new Vector3(0,.04f,18),new Vector3(7,.04f,2.5f),"Green",null,false);Label("ESCAPE\nPRESS E",new Vector3(0,2,19),.33f,new Color(.7f,1,.6f),Quaternion.identity);
        director.frontSpawn=Spawn("Front Spawn",new Vector3(0,.1f,-15));
    }
    static void BakeNavMesh(Area area)
    {
        var sources=new List<NavMeshBuildSource>();
        foreach(var box in Object.FindObjectsByType<BoxCollider>())
        {
            if(box.isTrigger||box.bounds.min.y>2.7f)continue;
            sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,transform=box.transform.localToWorldMatrix*Matrix4x4.Translate(box.center),size=box.size,area=0 });
        }
        // Cylinders/cars also have collision; include them as conservative box obstacles.
        foreach(var col in Object.FindObjectsByType<Collider>())
        {if(col is BoxCollider||col.isTrigger||col.bounds.min.y>2.7f)continue;sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(col.bounds.center,Quaternion.identity,Vector3.one),size=col.bounds.size,area=0});}
        var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=.43f;settings.agentHeight=1.9f;settings.agentClimb=.3f;settings.overrideVoxelSize=true;settings.voxelSize=.12f;
        var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(new Vector3(0,1.2f,0),new Vector3(80,3,80)),Vector3.zero,Quaternion.identity);
        if(!data)throw new Exception("NavMesh build failed for "+area);
        string path=Root+"/Navigation/"+area+".asset";AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(data,path);director.navigation=data;
    }
    static RectTransform UIObject(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;return r;}
    static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high,Color color)
    {var r=UIObject(name,parent,min,max,low,high);r.gameObject.AddComponent<Image>().color=color;return r.gameObject;}
    static Text UIText(string name,string value,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high,int size=20,TextAnchor align=TextAnchor.MiddleLeft)
    {var r=UIObject(name,parent,min,max,low,high);var t=r.gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;t.color=Color.white;t.alignment=align;t.raycastTarget=false;return t;}
    static Button Button(string name,string value,Transform parent,Vector2 anchor,Vector2 p,Vector2 size,UnityAction action,Color? color=null)
    {
        var r=UIObject(name,parent,anchor,anchor,p,p+size);r.gameObject.AddComponent<Image>().color=color??new Color(.20f,.26f,.33f,.98f);var b=r.gameObject.AddComponent<Button>();UnityEventTools.AddPersistentListener(b.onClick,action);
        UIText("Label",value,r,Vector2.zero,Vector2.one,new Vector2(5,2),new Vector2(-5,-2),18,TextAnchor.MiddleCenter);return b;
    }
    static Canvas Canvas()
    {var g=new GameObject("Canvas - editable game interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var c=g.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=g.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));return c;}
    static void BuildMenu()
    {
        NewScene("MainMenu",false);Room(24,25,"BankTiles","Plaster");Cube("Bank counter",new Vector3(0,.6f,5),new Vector3(16,1.2f,1.4f),"Wood");
        for(int x=-8;x<=8;x+=4){Cube("Column",new Vector3(x,2,2),new Vector3(.6f,4,.6f),"Concrete");PointLight(new Vector3(x,3,0),10,2);}
        var camera=new GameObject("Menu Camera");camera.transform.position=new Vector3(9,2.1f,-10);camera.transform.rotation=Quaternion.Euler(4,-25,0);camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
        var canvas=Canvas();var menu=canvas.gameObject.AddComponent<MenuController>();var panel=Panel("Main Menu Panel",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-290,-300),new Vector2(290,300),new Color(.10f,.13f,.16f,.95f));
        UIText("Title","BANK HEIST 3D",panel.transform,Vector2.zero,Vector2.one,new Vector2(25,495),new Vector2(-25,-20),38,TextAnchor.MiddleCenter);
        UIText("Subtitle","A simple first-person squad game",panel.transform,Vector2.zero,Vector2.one,new Vector2(25,455),new Vector2(-25,-85),18,TextAnchor.MiddleCenter);
        Button("Robber button","1. ROBBER",panel.transform,Vector2.zero,new Vector2(40,385),new Vector2(500,48),menu.ChooseRobber,new Color(.39f,.24f,.2f));
        Button("Police button","2. POLICE / GUARD",panel.transform,Vector2.zero,new Vector2(40,328),new Vector2(500,48),menu.ChoosePolice,new Color(.18f,.29f,.43f));
        Button("Bystander button","3. BYSTANDER",panel.transform,Vector2.zero,new Vector2(40,271),new Vector2(500,48),menu.ChooseBystander,new Color(.36f,.34f,.23f));
        menu.selectedText=UIText("Selected Role","Selected role: Robber",panel.transform,Vector2.zero,Vector2.one,new Vector2(40,229),new Vector2(-40,-334),18,TextAnchor.MiddleCenter);
        menu.description=UIText("Role Objective","",panel.transform,Vector2.zero,Vector2.one,new Vector2(40,134),new Vector2(-40,-378),18,TextAnchor.MiddleCenter);
        Button("Start Game","START GAME",panel.transform,Vector2.zero,new Vector2(40,72),new Vector2(500,48),menu.StartGame,new Color(.2f,.38f,.24f));
        Button("Controls","Controls",panel.transform,Vector2.zero,new Vector2(40,20),new Vector2(242,38),menu.Controls);Button("Quit","Quit",panel.transform,Vector2.zero,new Vector2(298,20),new Vector2(242,38),menu.Quit);
        menu.controlsPanel=Panel("Controls Panel",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-340,-265),new Vector2(340,265),new Color(.09f,.12f,.15f,.99f));
        UIText("Controls Text","CONTROLS\n\nWASD / arrows: Move   |   Mouse: Look\nLeft click: Shoot   |   R: Reload   |   G: Grenade\nE: Door / gold / rescue / escape\n1: Follow   2: Attack   3: Grenades\n4: Retreat   5: Hold position\nTab: Unlock cursor and click squad buttons\nEsc: Pause   |   Left Shift: Run\n\nRobber escape route:\nVault -> Bank rear door -> Back alley\n-> Sewer manhole -> Tunnel exit\n\nHealth and supplies carry between scenes.",menu.controlsPanel.transform,Vector2.zero,Vector2.one,new Vector2(30,75),new Vector2(-30,-25),19,TextAnchor.UpperLeft);
        Button("Close controls","Back",menu.controlsPanel.transform,Vector2.zero,new Vector2(240,20),new Vector2(200,42),menu.Controls);menu.controlsPanel.SetActive(false);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/Scenes/MainMenu.unity");
    }
    static void MakeHUD()
    {
        var canvas=Canvas();var h=canvas.gameObject.AddComponent<GameHUD>();Color bg=new Color(.08f,.10f,.12f,.8f);
        var topLeft=Panel("Stats panel",canvas.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(15,-98),new Vector2(195,-15),bg);
        h.moneyText=UIText("Money and kills","$ 0\nKills: 0",topLeft.transform,Vector2.zero,Vector2.one,new Vector2(12,5),new Vector2(-10,-5),23);
        h.objectiveText=UIText("Objective","Enter the bank.",canvas.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-390,-78),new Vector2(390,-34),21,TextAnchor.MiddleCenter);
        h.timerText=UIText("Timer","00:00",canvas.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-80,-34),new Vector2(80,-5),20,TextAnchor.MiddleCenter);
        var hp=Panel("Health panel",canvas.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(-230,-88),new Vector2(-15,-15),bg);
        h.healthText=UIText("Health text","HP 100 / 100",hp.transform,Vector2.zero,Vector2.one,new Vector2(10,29),new Vector2(-10,-4),21);
        var slider=Panel("Health bar",hp.transform,Vector2.zero,Vector2.zero,new Vector2(10,10),new Vector2(205,26),new Color(.25f,.1f,.1f));h.healthBar=slider.AddComponent<Slider>();h.healthBar.minValue=0;h.healthBar.maxValue=100;h.healthBar.value=100;h.healthBar.interactable=false;
        var fill=Panel("Health fill",slider.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.85f,.16f,.14f));h.healthBar.fillRect=(RectTransform)fill.transform;h.healthBar.targetGraphic=fill.GetComponent<Image>();
        UIText("Crosshair","+",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-15,-15),new Vector2(15,15),27,TextAnchor.MiddleCenter);
        h.promptText=UIText("Interaction prompt","",canvas.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-340,166),new Vector2(340,207),23,TextAnchor.MiddleCenter);
        h.sceneText=UIText("Area and alarm","Street",canvas.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-220,-103),new Vector2(220,-79),15,TextAnchor.MiddleCenter);
        var team=Panel("Team panel",canvas.transform,Vector2.zero,Vector2.zero,new Vector2(15,98),new Vector2(355,239),bg);h.squadText=UIText("Squad status","TEAM",team.transform,Vector2.zero,Vector2.one,new Vector2(10,5),new Vector2(-10,-5),17);
        h.feedText=UIText("Damage and event feed","",canvas.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-490,108),new Vector2(-20,237),17,TextAnchor.LowerRight);
        h.ammoText=UIText("Ammunition","Ammo: 12 / 12",canvas.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-420,70),new Vector2(-20,105),20,TextAnchor.MiddleRight);
        h.selectedText=UIText("Selected order recipients","Orders: whole squad",canvas.transform,Vector2.zero,Vector2.zero,new Vector2(18,65),new Vector2(700,93),16);
        UnityAction[] commands={h.Follow,h.Attack,h.ThrowGrenades,h.Retreat,h.Hold};string[] labels={"1 Follow","2 Attack","3 Grenades","4 Retreat","5 Hold"};for(int i=0;i<5;i++)Button(labels[i],labels[i],canvas.transform,Vector2.zero,new Vector2(16+i*154,18),new Vector2(145,40),commands[i]);
        Button("Select teammate","Select teammate",canvas.transform,new Vector2(1,0),new Vector2(-220,18),new Vector2(200,40),h.NextMember);
        var damage=Panel("Damage overlay",canvas.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.clear);h.damageOverlay=damage.GetComponent<Image>();h.damageOverlay.raycastTarget=false;
        h.pausePanel=Panel("Pause menu",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-230,-170),new Vector2(230,170),new Color(.09f,.12f,.15f,.98f));
        UIText("Paused title","PAUSED",h.pausePanel.transform,Vector2.zero,Vector2.one,new Vector2(20,240),new Vector2(-20,-20),32,TextAnchor.MiddleCenter);
        Button("Resume","Resume",h.pausePanel.transform,Vector2.zero,new Vector2(70,175),new Vector2(320,45),h.Resume);Button("Restart","Restart mission",h.pausePanel.transform,Vector2.zero,new Vector2(70,112),new Vector2(320,45),h.Restart);Button("Menu","Main menu",h.pausePanel.transform,Vector2.zero,new Vector2(70,48),new Vector2(320,45),h.Menu);h.pausePanel.SetActive(false);
        h.resultPanel=Panel("Win and lose menu",canvas.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-310,-190),new Vector2(310,190),new Color(.09f,.12f,.15f,.99f));
        h.resultTitle=UIText("Result title","YOUR TEAM WON!",h.resultPanel.transform,Vector2.zero,Vector2.one,new Vector2(20,278),new Vector2(-20,-20),33,TextAnchor.MiddleCenter);
        h.resultMessage=UIText("Result message","",h.resultPanel.transform,Vector2.zero,Vector2.one,new Vector2(30,110),new Vector2(-30,-102),22,TextAnchor.MiddleCenter);
        Button("Play again","Play again",h.resultPanel.transform,Vector2.zero,new Vector2(65,38),new Vector2(235,48),h.Restart);Button("Choose role","Main menu",h.resultPanel.transform,Vector2.zero,new Vector2(320,38),new Vector2(235,48),h.Menu);h.resultPanel.SetActive(false);
    }
    [MenuItem("Bank Heist/Validate Project")]
    public static void Validate()
    {
        foreach(string n in SceneNames)if(!File.Exists("Assets/Scenes/"+n+".unity"))throw new Exception("Missing scene "+n);
        foreach(Area a in Enum.GetValues(typeof(Area)))if(!AssetDatabase.LoadAssetAtPath<NavMeshData>(Root+"/Navigation/"+a+".asset"))throw new Exception("Missing NavMesh "+a);
        if(EditorBuildSettings.scenes.Count(s=>s.enabled)!=6)throw new Exception("Build must contain six scenes");
        Debug.Log("HEIST_VALIDATION_SUCCESS: six scenes, baked navigation and prefabs are ready.");
    }
    [MenuItem("Bank Heist/Build Windows Game")]
    public static void BuildWindows()
    {
        Validate();Directory.CreateDirectory("Builds/Windows");
        var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),"Builds/Windows/BankHeist3D.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
        Debug.Log("HEIST_BUILD_SUCCESS");
    }
}




