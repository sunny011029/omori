using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class ChapterOneSetup
{
    const string Request="Library/ChapterOneSetup.request";
    static ChapterOneSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if(action=="install") Install(); else if(action=="presentation") RefreshPresentation(); else Inspect(); }
        catch(Exception e) { File.WriteAllText("Library/ChapterOneSetup.report.txt","FAIL\n"+e); Debug.LogException(e); }
    }
    static void Inspect()
    {
        var previous=SceneManager.GetActiveScene();
        string path="Assets/Scenes/MochaStore.unity";
        var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var b=new StringBuilder();
            foreach(var go in scene.GetRootGameObjects())
            {
                b.AppendLine("ROOT "+go.name+" "+go.transform.position+" scale="+go.transform.lossyScale);
                foreach(var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if(t.GetComponent<TopDownCharacterController>()!=null || t.GetComponent<Camera>()!=null || t.GetComponent<BoxCollider2D>()!=null)
                    {
                        b.AppendLine("OBJECT "+PathOf(t)+" position="+t.position+" local="+t.localPosition+" scale="+t.lossyScale);
                        var box=t.GetComponent<BoxCollider2D>(); if(box!=null) b.AppendLine("  BOX "+box.bounds+" size="+box.size+" offset="+box.offset);
                    }
                }
                foreach(var map in go.GetComponentsInChildren<Tilemap>(true))
                    if(map.name=="House") b.AppendLine("FLOOR "+PathOf(map.transform)+" bounds="+map.GetComponent<TilemapRenderer>().bounds+" cells="+map.cellBounds+" cell0="+map.CellToWorld(Vector3Int.zero));
            }
            File.WriteAllText("Library/ChapterOneSceneInspection.txt",b.ToString());
            File.WriteAllText("Library/ChapterOneSetup.report.txt","INSPECTED\n");
        }
        finally { if(opened) EditorSceneManager.CloseScene(scene,true); if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;

    static Material material;
    static Sprite white,cloud;
    static Transform Child(Transform parent,string name,Vector2 position=default)
    {
        var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Add chapter one flow");
        if(go.scene!=parent.gameObject.scene) SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);
        go.transform.SetParent(parent,false); go.transform.localPosition=position; return go.transform;
    }
    static GameObject Picture(Transform parent,string name,Sprite sprite,Vector2 position,float width,int order)
    {
        var t=Child(parent,name,position); t.localScale=Vector3.one*(width/sprite.bounds.size.x);
        var sr=t.gameObject.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.sharedMaterial=material; sr.sortingOrder=order;
        return t.gameObject;
    }
    static GameObject Rectangle(Transform parent,string name,Vector2 pos,Vector2 size,Color color,int order)
    {
        var t=Child(parent,name,pos); t.localScale=new Vector3(size.x/white.bounds.size.x,size.y/white.bounds.size.y,1);
        var sr=t.gameObject.AddComponent<SpriteRenderer>(); sr.sprite=white; sr.sharedMaterial=material; sr.color=color; sr.sortingOrder=order;
        return t.gameObject;
    }
    static ChapterOneTarget Target(Transform layer,string id,ChapterOneAction action,int index,Vector2 pos,string prompt,float reach=1.1f)
    {
        var t=Child(layer,id,pos); var target=t.gameObject.AddComponent<ChapterOneTarget>();
        target.action=action; target.index=index;
        var interaction=t.gameObject.AddComponent<SchoolInteractable>(); interaction.kind=SchoolInteractionKind.ChapterObject;
        interaction.interactionId=id; interaction.displayName=prompt; interaction.promptOverride=prompt; interaction.reach=reach;
        interaction.chapterTarget=target; target.interaction=interaction; return target;
    }
    [MenuItem("Tools/School/Install Chapter One Playable Flow")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required.");
        var school=SceneManager.GetActiveScene();
        if(!school.name.Equals("school",StringComparison.OrdinalIgnoreCase)) throw new Exception("Open School before installing.");
        var root=school.GetRootGameObjects().Single(g=>g.name=="School_ReferenceLayout").transform;
        if(root.Find("08_ChapterOne")!=null) throw new Exception("Chapter flow is already installed; use targeted changes.");
        Directory.CreateDirectory("Recovery/ChapterOne");
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        if(!EditorSceneManager.SaveScene(school,"Recovery/ChapterOne/School-before-"+stamp+".unity",true)) throw new IOException("School backup failed");
        var shop=EditorSceneManager.OpenScene("Assets/Scenes/MochaStore.unity",OpenSceneMode.Additive);
        if(!EditorSceneManager.SaveScene(shop,"Recovery/ChapterOne/MochaStore-before-"+stamp+".unity",true)) throw new IOException("Shop backup failed");
        var protectedObjects=school.GetRootGameObjects().Concat(shop.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<Component>(true))
            .Where(c=>c is Transform || c is SpriteRenderer || c is Tilemap || c is TilemapRenderer || c is Collider2D || c is Camera || c is TopDownCharacterController || c is TopDownCameraFollow).ToArray();
        string before=Protected(protectedObjects);
        ImportSprites();
        material=AssetDatabase.LoadAssetAtPath<Material>("Assets/学校改色/SchoolSpriteUnlit.mat");
        white=AssetDatabase.LoadAllAssetsAtPath("Assets/学校改色/Tileset_32x32_2.png").OfType<Sprite>().Single(s=>s.name=="School_White");
        cloud=AssetDatabase.LoadAllAssetsAtPath("Assets/ChapterOne/Art/MatchaCloud.png").OfType<Sprite>().Single(s=>s.name=="Matcha_Neutral");
        var props=AssetDatabase.LoadAllAssetsAtPath("Assets/ChapterOne/Art/CleaningProps.png").OfType<Sprite>().ToDictionary(s=>s.name);
        var layer=Child(root,"08_ChapterOne"); var chapter=layer.gameObject.AddComponent<ChapterOneScene>();
        chapter.roomRoot=root; chapter.interaction=root.Find("07_Interactions").GetComponent<SchoolInteractionController>();
        chapter.interaction.chapter=chapter; chapter.cloudPortrait=cloud;
        var cleanPositions=new[]{new Vector2(1.65f,4.65f),new Vector2(17.1f,4.4f),new Vector2(8,.72f),new Vector2(16.35f,.14f)};
        string[] propNames={"PaperBall","Wrapper","DiscardedPaper"}; float[] widths={.42f,.6f,.65f};
        for(int i=0;i<4;i++)
        {
            var target=Target(layer,"Clean_"+i,ChapterOneAction.Clean,i,cleanPositions[i],i==3?"整理这些书本":"清扫地上的杂物");
            target.visual=i==3?root.Find("04_CorridorFurniture/Scattered_Books").gameObject:
                Picture(target.transform,"Visual",props[propNames[i]],Vector2.zero,widths[i],-27800);
            target.interaction.clickVisual=target.visual.transform;
        }
        var paperPositions=new[]{new Vector2(6.8f,5.4f),new Vector2(16.3f,4.7f),new Vector2(10.7f,.85f)};
        for(int i=0;i<3;i++)
        {
            var target=Target(layer,"Recurring_"+i,ChapterOneAction.RecurringPaper,i,paperPositions[i],"查看重新出现的纸团");
            target.visual=Picture(target.transform,"Visual",props["PaperBall"],Vector2.zero,.44f,-27790);
            target.interaction.clickVisual=target.visual.transform; target.visual.SetActive(false); target.interaction.enabled=false;
        }
        Target(layer,"SchoolExit",ChapterOneAction.SchoolExit,0,new Vector2(7.5f,-.9f),"离开学校",.65f);
        chapter.encounterLocker=root.Find("04_CorridorFurniture/Locker_5.5").GetComponent<SchoolLocker>();
        chapter.encounterCloud=Picture(chapter.encounterLocker.openVisual.transform,"MatchaCloud",cloud,new Vector2(-.03f,.7f),.56f,5);
        chapter.encounterCloud.AddComponent<ChapterOneFloat>(); chapter.encounterCloud.SetActive(false);
        chapter.portalGlow=Rectangle(root.Find("04_CorridorFurniture/Locker_14.5/OpenInterior"),"PortalLight",new Vector2(-.03f,.65f),new Vector2(.53f,.9f),new Color(.59f,.91f,.75f),3);
        chapter.portalGlow.SetActive(false);
        chapter.visionClouds=chapter.interaction.targets.Where(t=>t.cloudViewAnchor!=null).Select(t=>GreyCloud(t.cloudViewAnchor)).ToArray();
        chapter.chapterTargets=layer.GetComponentsInChildren<ChapterOneTarget>(true);
        chapter.interaction.targets=root.GetComponentsInChildren<SchoolInteractable>(true);

        var grid=shop.GetRootGameObjects().Single(g=>g.name=="Room1Grid").transform;
        var shopLayer=Child(grid,"ChapterOneInteractions"); var shopChapter=shopLayer.gameObject.AddComponent<ChapterOneScene>();
        var hub=shopLayer.gameObject.AddComponent<SchoolInteractionController>();
        hub.player=grid.Find("Role/BlondWhite").GetComponent<TopDownCharacterController>();
        hub.worldCamera=shop.GetRootGameObjects().Single(g=>g.name=="Main Camera").GetComponent<Camera>();
        hub.chineseFont=chapter.interaction.chineseFont; hub.chapter=shopChapter;
        shopChapter.isSchool=false; shopChapter.roomRoot=grid; shopChapter.interaction=hub; shopChapter.cloudPortrait=cloud;
        var matcha=Target(shopLayer,"ShopMatchaCloud",ChapterOneAction.ShopCloud,0,new Vector2(0,4),"与抹茶云交谈",1.8f);
        shopChapter.shopCloud=Picture(matcha.transform,"Visual",cloud,new Vector2(0,3.6f),1.8f,-390);
        shopChapter.shopCloud.AddComponent<ChapterOneFloat>(); matcha.interaction.portrait=cloud; matcha.interaction.clickVisual=shopChapter.shopCloud.transform;
        var craft=Target(shopLayer,"CraftTable",ChapterOneAction.CraftTable,0,new Vector2(5,4),"查看制作台",1.8f);
        craft.interaction.clickVisual=grid.Find("DepthSortedFurniture/BackCounter");
        // A small mint placemat marks the existing counter; no furniture is moved.
        Rectangle(craft.transform,"CraftPlacemat",new Vector2(0,3.25f),new Vector2(1.6f,.32f),new Color(.72f,.91f,.69f),-380);
        var portal=Target(shopLayer,"ReturnToSchool",ChapterOneAction.ReturnToSchool,0,new Vector2(-6.8f,1.1f),"返回学校",1.7f);
        var glow=Child(portal.transform,"GreenEntrance");
        Rectangle(glow,"Outline",Vector2.zero,new Vector2(1.6f,.75f),new Color(.31f,.57f,.45f),-27900);
        Rectangle(glow,"Light",Vector2.zero,new Vector2(1.35f,.52f),new Color(.68f,.94f,.78f),-27899);
        Rectangle(glow,"Step",Vector2.zero,new Vector2(.6f,.11f),new Color(.91f,1,.81f),-27898);
        shopChapter.portalGlow=glow.gameObject; portal.interaction.clickVisual=glow;
        shopChapter.chapterTargets=shopLayer.GetComponentsInChildren<ChapterOneTarget>(true); hub.targets=shopLayer.GetComponentsInChildren<SchoolInteractable>(true);
        if(before!=Protected(protectedObjects)) throw new Exception("Protected original scene transforms, visuals, colliders or player/camera settings changed.");
        var otherScenes=EditorBuildSettings.scenes.Where(s=>!s.path.Equals("Assets/Scenes/School.unity",StringComparison.OrdinalIgnoreCase) && !s.path.Equals("Assets/Scenes/MochaStore.unity",StringComparison.OrdinalIgnoreCase));
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/School.unity",true),new EditorBuildSettingsScene("Assets/Scenes/MochaStore.unity",true)}.Concat(otherScenes).ToArray();
        EditorSceneManager.MarkSceneDirty(school); EditorSceneManager.MarkSceneDirty(shop);
        if(!EditorSceneManager.SaveScene(school) || !EditorSceneManager.SaveScene(shop)) throw new IOException("Chapter scene save failed");
        AssetDatabase.SaveAssets();
        root.gameObject.SetActive(false);
        try { CaptureShop(grid,"Recovery/ChapterOne/shop-installed.png"); }
        finally { root.gameObject.SetActive(true); }
        EditorSceneManager.CloseScene(shop,true); SceneManager.SetActiveScene(school);
        SchoolSceneDetailAdjustments.CaptureOverview(root,"Recovery/ChapterOne/school-installed.png");
        Selection.activeGameObject=layer.gameObject;
        File.WriteAllText("Library/ChapterOneSetup.report.txt","PASS\nSchool and MochaStore saved.\nAll original transforms, renderer state, floor tiles, colliders, players and camera settings preserved.\n4 cleanup points, 3 recurring points, encounter/portal, shop counter targets and return entrance installed.\nExisting Step02 images copied unchanged and sliced using native Sprite Editor data providers.\nBuild scenes: School, MochaStore.\n");
    }
    static string Protected(Component[] components)
    {
        var b=new StringBuilder();
        foreach(var c in components)
        {
            if(c is Transform t) b.Append(t.GetInstanceID()).Append(t.localPosition.ToString("R")).Append(t.localRotation.ToString("R")).Append(t.localScale.ToString("R")).Append(t.gameObject.activeSelf);
            else b.Append(EditorJsonUtility.ToJson(c));
        }
        return b.ToString();
    }
    static GameObject GreyCloud(Transform parent)
    {
        var t=Child(parent,"CloudVision");
        // Discrete rectangles form a faceless pixel cloud, separate from the Matcha character.
        Rectangle(t,"Rim",new Vector2(0,.03f),new Vector2(.74f,.24f),new Color(.30f,.29f,.36f),10);
        Rectangle(t,"Body",new Vector2(0,.07f),new Vector2(.66f,.23f),new Color(.57f,.56f,.64f),11);
        Rectangle(t,"Left",new Vector2(-.21f,.18f),new Vector2(.23f,.2f),new Color(.57f,.56f,.64f),11);
        Rectangle(t,"Top",new Vector2(.04f,.21f),new Vector2(.27f,.26f),new Color(.64f,.63f,.70f),12);
        t.gameObject.AddComponent<ChapterOneFloat>(); t.gameObject.SetActive(false); return t.gameObject;
    }
    static void ImportSprites()
    {
        Cut("Assets/ChapterOne/Art/MatchaCloud.png",1086,new[]{("Matcha_Neutral",64,161,632,307),("Matcha_ClosedEyes",751,161,634,306),("Matcha_Talking",64,638,632,305),("Matcha_Surprised",751,637,634,306)});
        Cut("Assets/ChapterOne/Art/CleaningProps.png",724,new[]{("PaperBall",201,243,291,307),("DiscardedPaper",654,113,720,507),("Wrapper",1485,241,583,370)});
    }
    static void Cut(string path,int height,(string,int,int,int,int)[] cuts)
    {
        AssetDatabase.ImportAsset(path); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=256; importer.spritePixelsPerUnit=100;
        importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories(); factories.Init(); var provider=factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var existing=provider.GetSpriteRects();
        var rects=cuts.Select(c=>new SpriteRect{name=c.Item1,spriteID=existing.FirstOrDefault(r=>r.name==c.Item1)?.spriteID ?? GUID.Generate(),
            rect=new Rect(c.Item2,height-c.Item3-c.Item5,c.Item4,c.Item5),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}).ToArray();
        provider.SetSpriteRects(rects); provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
    }
    static void CaptureShop(Transform root,string path)
    {
        var go=new GameObject("Temporary shop overview"); var camera=go.AddComponent<Camera>(); camera.enabled=false;
        camera.orthographic=true; camera.orthographicSize=15; camera.aspect=1.5f; camera.transform.position=new Vector3(-1,1,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.24f,.24f,.34f); go.AddComponent<UniversalAdditionalCameraData>();
        var rt=new RenderTexture(1500,1000,24); var texture=new Texture2D(1500,1000,TextureFormat.RGB24,false); var old=RenderTexture.active;
        try { SortingGroup.UpdateAllSortingGroups(); RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt}); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1500,1000),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); }
        finally { RenderTexture.active=old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }
    static void RefreshPresentation()
    {
        var school=SceneManager.GetActiveScene(); var root=school.GetRootGameObjects().Single(g=>g.name=="School_ReferenceLayout");
        var shop=EditorSceneManager.OpenScene("Assets/Scenes/MochaStore.unity",OpenSceneMode.Additive);
        var grid=shop.GetRootGameObjects().Single(g=>g.name=="Room1Grid").transform;
        var cloudVisual=grid.Find("ChapterOneInteractions/ShopMatchaCloud/Visual");
        Undo.RecordObject(cloudVisual,"Position new Matcha cloud above counter"); cloudVisual.localPosition=new Vector3(0,3.6f,0);
        var mat=grid.Find("ChapterOneInteractions/CraftTable/CraftPlacemat");
        Undo.RecordObject(mat,"Position new crafting marker on countertop"); mat.localPosition=new Vector3(0,3.25f,0);
        var sprite=mat.GetComponent<SpriteRenderer>().sprite; mat.localScale=new Vector3(1.6f/sprite.bounds.size.x,.32f/sprite.bounds.size.y,1);
        EditorSceneManager.MarkSceneDirty(shop); EditorSceneManager.SaveScene(shop);
        root.SetActive(false);
        try { CaptureShop(grid,"Recovery/ChapterOne/shop-installed.png"); }
        finally { root.SetActive(true); EditorSceneManager.CloseScene(shop,true); SceneManager.SetActiveScene(school); }
        File.WriteAllText("Library/ChapterOnePresentation.report.txt","PASS\nOnly newly added cloud/placemat positions adjusted; MochaStore saved.\n");
    }
}
