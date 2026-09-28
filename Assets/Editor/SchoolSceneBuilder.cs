using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Builds editable scene objects from the user's recoloured palettes. Source pixels are unchanged.
[InitializeOnLoad]
public static class SchoolSceneBuilder
{
    const string ScenePath = "Assets/Scenes/school.unity";
    const string Folder = "Assets/学校改色/";
    const string Ground = Folder + "Tileset_32x32_2.png";
    const string Purple = Folder + "img_v3_02141_ba0ed6ee-51b1-4e4e-bb12-31cec6464dhu.png";
    const string Cabinets = Folder + "img_v3_02141_50019d76bc51fhu.png";
    const string Request = "Library/SchoolSceneBuild.request";
    const string Report = "Library/SchoolSceneBuild.report.txt";
    const string RootName = "School_ReferenceLayout";
    static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
    static readonly Dictionary<Vector2Int, TileBase> FloorTiles = new Dictionary<Vector2Int, TileBase>();
    static Material material;
    static Transform root;
    static Camera overview;
    static bool running;
    static readonly Color Ink = Hex("3e3d56"), Trim = Hex("f7f7f7"), Mint = Hex("b9ece2"),
        Lavender = Hex("a98bd2"), Lilac = Hex("d6a8e2"), DeepPurple = Hex("68558f"), Glass = Hex("a0d6dd");

    static SchoolSceneBuilder() { EditorApplication.update += RunRequested; }

    static void RunRequested()
    {
        if (running || !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        try { Build(); }
        catch (Exception e) { File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/School/Build Reference Layout")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
        running = true;
        try
        {
            Directory.CreateDirectory("Recovery/School");
            // Preserve any open scene's unsaved work before switching to the requested scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (!open.isDirty || string.IsNullOrEmpty(open.path)) continue;
                if (!EditorSceneManager.SaveScene(open, "Recovery/School/" + open.name + "-open-backup.unity", true)
                    || !EditorSceneManager.SaveScene(open)) throw new IOException("Could not preserve the open scene.");
            }
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            if (!EditorSceneManager.SaveScene(scene, "Recovery/School/school-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
                throw new IOException("Could not back up school.");
            PrepareSprites(); LoadFloorPalette();
            int undoGroup = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Build school reference layout");
            var previous = scene.GetRootGameObjects().FirstOrDefault(o => o.name == RootName);
            if (previous != null) Undo.DestroyObjectImmediate(previous);
            var archive = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "PreviousScene_Disabled");
            if (archive == null)
            {
                archive = new GameObject("PreviousScene_Disabled");
                Undo.RegisterCreatedObjectUndo(archive, "Preserve previous school contents");
                foreach (var old in scene.GetRootGameObjects().Where(o => o != archive).ToArray())
                    Undo.SetTransformParent(old.transform, archive.transform, "Preserve previous school contents");
            }
            archive.SetActive(false);
            root = Group(null, RootName);
            PrepareMaterial();
            BuildArchitecture(); BuildLeftClassroom(); BuildRightClassroom(); BuildCorridor();
            BuildCamera();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("School save failed.");
            AssetDatabase.SaveAssets();
            Validate(scene);
            Capture("Recovery/School/school-overview.png", 1408, 1120);
            Selection.activeGameObject = root.gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(9, 5.35f, 0), Quaternion.identity, 9.3f, true, true);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("School reference layout saved. See Recovery/School/school-overview.png.");
        }
        finally { running = false; }
    }

    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Build school"); return go.transform;
    }

    struct Cut
    {
        public string name; public int x, y, w, h;
        public Cut(string n, int px, int py, int width, int height) { name = "School_" + n; x=px; y=py; w=width; h=height; }
    }

    static void AddCuts(string path, params Cut[] cuts)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int height = texture.height;
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var rectangles = provider.GetSpriteRects().ToList();
        var nameProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var names = nameProvider.GetNameFileIdPairs().ToList();
        foreach (var cut in cuts)
        {
            var existing = rectangles.FirstOrDefault(r => r.name == cut.name);
            var rect = existing ?? new SpriteRect { name = cut.name, spriteID = GUID.Generate() };
            rect.rect = new Rect(cut.x, height-cut.y-cut.h, cut.w, cut.h);
            rect.alignment = SpriteAlignment.Center; rect.pivot = new Vector2(.5f,.5f);
            if (existing == null) { rectangles.Add(rect); names.Add(new SpriteNameFileIdPair(rect.name,rect.spriteID)); }
        }
        provider.SetSpriteRects(rectangles.ToArray()); nameProvider.SetNameFileIdPairs(names); provider.Apply();
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Where(s=>s.name.StartsWith("School_")))
            Sprites[sprite.name.Substring(7)] = sprite;
    }

    static void PrepareSprites()
    {
        Sprites.Clear();
        AddCuts(Ground, new Cut("White",225,40,2,2));
        AddCuts(Purple,
            new Cut("ChairFront",0,0,32,64), new Cut("ChairBack",32,80,32,48),
            new Cut("DeskEmpty",64,16,32,48), new Cut("DeskPaper",96,16,32,48),
            new Cut("DeskCompass",32,16,32,48), new Cut("DeskComputer",128,16,32,48),
            new Cut("TeacherDesk",160,8,64,56), new Cut("LongDesk",16,216,48,72),
            new Cut("Globe",192,216,32,64), new Cut("Locker",160,352,32,64),
            new Cut("Chalkboard",192,368,64,48), new Cut("Bulletin",0,416,64,32),
            new Cut("Books",128,448,64,64), new Cut("BoardSmall",104,352,48,48));
        AddCuts(Cabinets, new Cut("CabinetTall",28,22,102,107),
            new Cut("CabinetNarrow",106,280,56,65), new Cut("CabinetSmall",32,280,56,65));
    }

    static void LoadFloorPalette()
    {
        FloorTiles.Clear();
        // These references come from the actual user palette, including any per-tile edits.
        foreach (string prefabName in new[]{"ground_School_Green", "school-purple", "school-柜子"})
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefabName + ".prefab");
            if (prefab == null) throw new IOException("Missing palette: " + prefabName);
            if (prefabName != "ground_School_Green") continue;
            foreach (var map in prefab.GetComponentsInChildren<Tilemap>(true))
            foreach (var p in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<Tile>(p); if (tile == null || tile.sprite == null) continue;
                var rect = tile.sprite.rect;
                FloorTiles[new Vector2Int(Mathf.RoundToInt(rect.x/32),Mathf.RoundToInt((320-rect.yMax)/32))] = tile;
            }
        }
    }

    static void PrepareMaterial()
    {
        const string path = "Assets/学校改色/SchoolSpriteUnlit.mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("2D unlit shader missing.");
            material = new Material(shader); AssetDatabase.CreateAsset(material,path);
        }
    }

    static SpriteRenderer Solid(Transform parent, string name, float x, float y, float width, float height, Color color, int order)
    {
        var t = Group(parent,name); t.position = new Vector3(x+width*.5f,y+height*.5f,0);
        var renderer = t.gameObject.AddComponent<SpriteRenderer>(); renderer.sprite=Sprites["White"];
        t.localScale=new Vector3(width/renderer.sprite.bounds.size.x,height/renderer.sprite.bounds.size.y,1);
        renderer.color=color; renderer.sharedMaterial=material; renderer.sortingOrder=order; return renderer;
    }

    static Transform Prop(Transform parent, string name, string sprite, float x, float bottom, float width, float height, bool collider = true)
    {
        var t=Group(parent,name); t.position=new Vector3(x,bottom,0);
        var visual=Group(t,"Visual"); var sr=visual.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite=Sprites[sprite]; sr.sharedMaterial=material;
        visual.localPosition=new Vector3(0,height*.5f,0);
        visual.localScale=new Vector3(width/sr.sprite.bounds.size.x,height/sr.sprite.bounds.size.y,1);
        var sort=t.gameObject.AddComponent<SortingGroup>(); sort.sortingOrder=1000-Mathf.RoundToInt(bottom*10);
        if (collider) { var box=t.gameObject.AddComponent<BoxCollider2D>(); box.size=new Vector2(width*.8f,Mathf.Min(.32f,height*.2f)); box.offset=new Vector2(0,.15f); }
        return t;
    }

    static void Floor(Transform parent,string name, float x,float y,int width,int height,int variant)
    {
        var g=Group(parent,name); g.position=new Vector3(x,y,0); g.localScale=Vector3.one*.5f;
        g.gameObject.AddComponent<Grid>(); var tileObject=Group(g,"Tiles");
        var map=tileObject.gameObject.AddComponent<Tilemap>(); var renderer=tileObject.gameObject.AddComponent<TilemapRenderer>();
        renderer.sharedMaterial=material; renderer.sortingOrder=-30000;
        for(int cx=0;cx<width*2;cx++) for(int cy=0;cy<height*2;cy++)
        {
            Vector2Int source;
            if (variant==0) source=new Vector2Int(1+cx%2,3+(1-cy%2));
            else if (variant==1) source=new Vector2Int(2,7);
            else source=new Vector2Int(3+cx%2,6+cy%2);
            map.SetTile(new Vector3Int(cx,cy,0),FloorTiles[source]);
        }
    }

    static void Panel(Transform parent,string name,float x,float y,float width,float height)
    {
        var g=Group(parent,name);
        Solid(g,"Mint plaster",x,y,width,height,Mint,-25000);
        Solid(g,"Warm top trim",x,y+height-.13f,width,.06f,Hex("efc49c"),-24990);
        Solid(g,"Inset rail",x,y+.3f,width,.07f,Hex("f1ddbc"),-24990);
        Solid(g,"Wainscot",x,y+.07f,width,.23f,Hex("97c9c7"),-24990);
        Solid(g,"Skirting outline",x,y,width,.07f,Ink,-24980);
    }

    static void Boundary(Transform parent,string name,float x,float y,float width,float height,bool collision=true)
    {
        var g=Group(parent,name);
        Solid(g,"Outline",x-.04f,y-.04f,width+.08f,height+.08f,Ink,-24500);
        Solid(g,"White wall cap",x,y,width,height,Trim,-24490);
        if(collision) { var box=g.gameObject.AddComponent<BoxCollider2D>(); box.offset=new Vector2(x+width*.5f,y+height*.5f); box.size=new Vector2(width,height); }
    }

    static void BuildArchitecture()
    {
        var g=Group(root,"01_Architecture");
        Floor(g,"LeftClassroom_Floor",0,4,9,7,0); Floor(g,"RightClassroom_Floor",11,4,7,4,1);
        Floor(g,"ConnectingCorridor_Floor",0,0,18,4,0); Floor(g,"CentralEntrance_Floor",6,-2,3,2,1);
        Panel(g,"LeftClassroom_BackWall",0,11,9,1.6f); Panel(g,"RightClassroom_BackWall",11,8,7,1.6f);
        Panel(g,"Corridor_FrontWall",0,2,18,1.65f);
        Boundary(g,"Left exterior",-.3f,0,.3f,12.9f);
        Boundary(g,"Large room top",0,12.6f,9,.3f); Boundary(g,"Large room right",9,4,.3f,8.9f);
        Boundary(g,"Small room left",10.7f,4,.3f,5.9f); Boundary(g,"Small room top",11,9.6f,7,.3f);
        Boundary(g,"Right exterior",18,0,.3f,9.9f);
        Boundary(g,"Partition left of door",0,3.65f,1,.3f);
        Boundary(g,"Partition between doors",2,3.65f,10,.3f);
        Boundary(g,"Partition right of door",13,3.65f,5,.3f);
        Boundary(g,"Bottom wall left",-.3f,-.3f,6.3f,.3f); Boundary(g,"Bottom wall right",9,-.3f,9.3f,.3f);
        Boundary(g,"Entrance left",5.7f,-2,.3f,2); Boundary(g,"Entrance right",9,-2,.3f,2);
        var entry=Group(g,"Entrance_Mat");
        Solid(entry,"Outer edge",6.05f,-1.85f,2.9f,1.8f,DeepPurple,-29000);
        Solid(entry,"Mint border",6.13f,-1.77f,2.74f,1.64f,Hex("82b7bc"),-28990);
        Solid(entry,"Inner rail",6.22f,-1.68f,2.56f,1.46f,Lavender,-28980);
        Solid(entry,"Centre",6.3f,-1.6f,2.4f,1.3f,Hex("b9c9b3"),-28970);
    }

    static void Window(Transform parent,string name,float x,float y,float w=2f,float h=1.5f)
    {
        var g=Group(parent,name); int o=50;
        Solid(g,"Frame outline",x,y,w,h,Ink,o); Solid(g,"Frame",x+.06f,y+.06f,w-.12f,h-.12f,Lavender,o+1);
        Solid(g,"Glass",x+.18f,y+.17f,w-.36f,h-.36f,Glass,o+2);
        Solid(g,"Glass highlight",x+w*.51f,y+.23f,.1f,h-.52f,Hex("d8f6ee"),o+3);
        Solid(g,"Centre mullion",x+w*.48f,y+.1f,.08f,h-.2f,Trim,o+4);
        Solid(g,"Bottom sill",x-.04f,y-.06f,w+.08f,.13f,DeepPurple,o+5);
        // Pixel-aligned curtain folds, matching the supplied purple palette.
        for(int side=0;side<2;side++) for(int j=0;j<5;j++)
        {
            float px=side==0 ? x+.09f+j*.12f : x+w-.21f-j*.12f;
            Solid(g,"Curtain fold "+side+"-"+j,px,y+.14f+j*.11f,.12f,h-.2f-j*.11f,j%2==0?Lilac:Lavender,o+6);
        }
        Solid(g,"Curtain rod",x-.04f,y+h-.04f,w+.08f,.1f,DeepPurple,o+8);
        Solid(g,"Left tie",x+.1f,y+.41f,.32f,.07f,Mint,o+9);
        Solid(g,"Right tie",x+w-.42f,y+.41f,.32f,.07f,Mint,o+9);
    }

    static void Door(Transform parent,string name,float x)
    {
        var g=Group(parent,name); int o=100;
        Solid(g,"Door frame",x-.04f,2,1.08f,1.9f,Ink,o);
        Solid(g,"Door panel",x+.03f,2.06f,.94f,1.8f,DeepPurple,o+1);
        Solid(g,"Inset",x+.1f,2.14f,.8f,1.65f,Lavender,o+2);
        Solid(g,"Window frame",x+.15f,2.89f,.7f,.76f,Ink,o+3);
        Solid(g,"Window glass",x+.2f,2.94f,.6f,.66f,Glass,o+4);
        for(int i=0;i<5;i++) Solid(g,"Glass reflection "+i,x+.25f+i*.07f,3.43f-i*.07f,.08f,.08f,Trim,o+5);
        Solid(g,"Handle",x+.77f,2.69f,.11f,.07f,Trim,o+6);
        Solid(g,"Door mat outline",x+.08f,1.03f,.84f,.91f,DeepPurple,100);
        Solid(g,"Door mat border",x+.16f,1.11f,.68f,.75f,Lilac,101);
        Solid(g,"Door mat centre",x+.23f,1.18f,.54f,.61f,DeepPurple,102);
        Solid(g,"Mat motif horizontal",x+.36f,1.43f,.29f,.1f,Mint,103);
        Solid(g,"Mat motif vertical",x+.46f,1.33f,.1f,.29f,Mint,103);
    }

    static void Desk(Transform p,string name,string kind,float x,float y,bool chair)
    {
        Prop(p,name,kind,x,y,.95f,1.35f);
        if(chair) Prop(p,name+"_Chair","ChairBack",x,y-.52f,.83f,.85f);
    }

    static void BuildLeftClassroom()
    {
        var g=Group(root,"02_LeftClassroom");
        Prop(g,"TopLeft_Bookcase","CabinetTall",.95f,10.5f,1.45f,2.25f);
        Prop(g,"Chalkboard","Chalkboard",3.85f,11.06f,1.65f,1.25f,false);
        Prop(g,"Noticeboard","Bulletin",5.85f,11.65f,1.5f,.75f,false);
        Window(g,"PurpleCurtain_Window",6.8f,11.05f,2f,1.5f);
        Prop(g,"Teacher_Desk","TeacherDesk",5.45f,9.55f,1.7f,1.48f);
        Prop(g,"Teacher_Chair","ChairBack",5.45f,10.75f,.85f,1.0f);
        Desk(g,"Upper_Left","DeskCompass",2.5f,7.75f,true);
        Desk(g,"Upper_Middle","DeskEmpty",3.5f,7.75f,false);
        Desk(g,"Upper_Right","DeskPaper",4.5f,7.75f,true);
        Desk(g,"Upper_Side","DeskComputer",6.5f,7.75f,true);
        Desk(g,"Lower_Left","DeskEmpty",3.5f,5.75f,true);
        Desk(g,"Lower_Middle","DeskCompass",4.5f,5.75f,false);
        Desk(g,"Lower_Right","DeskEmpty",5.5f,5.75f,false);
        foreach(float y in new[]{6.8f,5.8f,4.8f}) Prop(g,"LeftWall_Chair_"+y,"ChairFront",.55f,y,.85f,1.0f);
        Prop(g,"UpperRight_Chair","ChairFront",8.5f,10.05f,.85f,1.15f);
        Prop(g,"LowerRight_Chair","ChairFront",8.5f,4.8f,.85f,1.0f);
        Prop(g,"LowerMiddle_Chair","ChairFront",4.5f,4.65f,.85f,1.0f);
        Prop(g,"LowerSide_Chair","ChairFront",5.5f,4.65f,.85f,1.0f);
    }

    static void BuildRightClassroom()
    {
        var g=Group(root,"03_RightClassroom");
        Prop(g,"LeftWall_Picture","BoardSmall",11.45f,8.65f,.48f,.65f,false);
        Prop(g,"RightWall_Picture","BoardSmall",12.45f,8.65f,.48f,.65f,false);
        Window(g,"PurpleCurtain_Window",13.35f,8.07f,2f,1.45f);
        Prop(g,"Wide_TeachingBoard","Chalkboard",16.85f,8.45f,1.95f,1.15f,false);
        Desk(g,"Back_EmptyDesk","DeskEmpty",12.2f,6.65f,false);
        Prop(g,"Back_Chair","ChairFront",11.55f,6.6f,.6f,1.1f);
        Desk(g,"Back_WorkDesk","DeskComputer",14.2f,6.65f,false);
        Desk(g,"Front_WorkDesk","DeskPaper",12.2f,4.65f,true);
        Desk(g,"Front_EmptyDesk","DeskEmpty",14.3f,4.65f,false);
        Prop(g,"Front_SideChair","ChairFront",13.55f,4.65f,.6f,1.1f);
        var table=Prop(g,"Side_TeachingTable","LongDesk",16.6f,5.5f,1.15f,2.05f);
        var globe=Prop(table,"Teaching_Globe","Globe",16.58f,6.65f,.8f,1.3f,false);
        // Tabletop objects sort inside their table group, above its surface.
        globe.GetComponent<SortingGroup>().sortingOrder=1;
        Prop(g,"Side_Chair","ChairFront",17.45f,5.75f,.65f,1.1f);
    }

    static void BuildCorridor()
    {
        var g=Group(root,"04_CorridorFurniture"); Door(g,"LeftClassroom_Door",1); Door(g,"RightClassroom_Door",12);
        foreach(float x in new[]{3.5f,4.5f,5.5f,6.5f,9.5f,10.5f,14.5f})
            Prop(g,"Locker_"+x,"Locker",x,1.65f,.9f,1.9f);
        Prop(g,"Middle_Bookcase","CabinetNarrow",8.05f,1.5f,1.5f,1.8f);
        Prop(g,"Right_Bookcase","CabinetSmall",16.05f,1.5f,1.5f,1.8f);
        Prop(g,"Scattered_Books","Books",16.3f,.18f,1.7f,1.3f,false);
        var extinguisher=Group(g,"Wall_Extinguisher");
        Solid(extinguisher,"Mount",17.48f,1.64f,.5f,1.07f,Ink,980);
        Solid(extinguisher,"Bottle",17.57f,1.7f,.32f,.82f,Lilac,981);
        Solid(extinguisher,"Label",17.58f,1.96f,.3f,.2f,Trim,982);
        Solid(extinguisher,"Neck",17.65f,2.5f,.13f,.17f,Lavender,982);
        Solid(extinguisher,"Handle",17.66f,2.63f,.24f,.07f,DeepPurple,983);
    }

    static void BuildCamera()
    {
        var go=Group(root,"Main Camera").gameObject; go.tag="MainCamera";
        overview=go.AddComponent<Camera>(); overview.orthographic=true; overview.orthographicSize=8.15f;
        overview.transform.position=new Vector3(9,5.35f,-10); overview.clearFlags=CameraClearFlags.SolidColor;
        overview.backgroundColor=Ink; overview.nearClipPlane=.1f; overview.farClipPlane=100;
        go.AddComponent<AudioListener>(); var urp=go.AddComponent<UniversalAdditionalCameraData>(); urp.renderPostProcessing=false;
        go.AddComponent<OrthographicSceneFraming>().Refresh();
    }

    static void Validate(Scene scene)
    {
        var renderers=root.GetComponentsInChildren<SpriteRenderer>();
        if(renderers.Any(r=>r.sprite==null)) throw new Exception("A school sprite is missing.");
        var maps=root.GetComponentsInChildren<Tilemap>();
        int count=maps.Sum(m=>m.GetTilesBlock(m.cellBounds).Count(t=>t!=null));
        if(maps.Length!=4 || count!=676) throw new Exception("Unexpected floor layout: " + count);
        if(root.Find("02_LeftClassroom")==null || root.Find("03_RightClassroom")==null || root.Find("04_CorridorFurniture")==null)
            throw new Exception("Missing room group.");
        foreach(float aspect in new[]{1f,16f/9f,9f/16f})
        {
            overview.aspect=aspect; overview.GetComponent<OrthographicSceneFraming>().Refresh();
            foreach(Vector3 corner in new[]{new Vector3(-.35f,-2.05f,0),new Vector3(18.35f,12.95f,0)})
            {
                var viewport=overview.WorldToViewportPoint(corner);
                if(viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1) throw new Exception("Overview cropped at aspect " + aspect);
            }
        }
        overview.ResetAspect(); overview.GetComponent<OrthographicSceneFraming>().Refresh();
        File.WriteAllText(Report,"PASS\nSaved: " + scene.path + "\nFour editable floor Tilemaps: " + count + " tiles.\nSprite renderers: " + renderers.Length + "\nMissing sprites: 0\nTwo classrooms, corridor, two doors and central entrance.\nCamera framing: square, landscape and portrait passed.\nPrevious scene contents retained inactive.\n");
    }

    static void Capture(string path,int width,int height)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false); var rt=new RenderTexture(width,height,24);
        var old=RenderTexture.active; overview.aspect=(float)width/height;
        overview.GetComponent<OrthographicSceneFraming>().Refresh();
        try
        {
            RenderPipeline.SubmitRenderRequest(overview,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally { overview.ResetAspect(); overview.GetComponent<OrthographicSceneFraming>().Refresh(); RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    }
}
