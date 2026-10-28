using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class SchoolInteractionSetup
{
    const string Characters="Assets/角色/cozy_cafe_characters.png";
    const string Atlas="Assets/学校改色/img_v3_02141_ba0ed6ee-51b1-4e4e-bb12-31cec6464dhu.png";
    const string Request="Library/SchoolInteractionsSetup.request";
    static SchoolInteractionSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if(action=="refresh") SchoolInteractionChecks.RefreshVisualReferences();
            else Apply();
        }
        catch(Exception e) { File.WriteAllText("Library/SchoolInteractionsSetup.report.txt","FAIL\n"+e); Debug.LogException(e); }
    }
    [MenuItem("Tools/School/Install Approved Interactions and NPCs")]
    public static void Apply()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/school.unity");
        if(!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Open school in Edit mode.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="School_ReferenceLayout").transform;
        if(root.Find("07_Interactions")!=null) throw new Exception("Interactions already installed. Use targeted edits instead of replacing the installation.");
        Directory.CreateDirectory("Recovery/School");
        if(!EditorSceneManager.SaveScene(scene,"Recovery/School/school-before-interactions-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true)) throw new IOException("Backup failed.");
        var existingTransforms=root.GetComponentsInChildren<Transform>(true);
        var existingRenderers=root.GetComponentsInChildren<SpriteRenderer>(true);
        var existingMaps=root.GetComponentsInChildren<Tilemap>(true);
        string baseline=ProtectedState(existingTransforms,existingRenderers,existingMaps);
        SchoolSceneDetailAdjustments.CaptureOverview(root,"Recovery/School/interactions-before.png");
        PrepareSprites();
        AssetDatabase.ImportAsset("Assets/Fonts/NotoSansSC/NotoSansSC-Regular.otf");
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansSC/NotoSansSC-Regular.otf");
        if(font==null) throw new Exception("Chinese font missing.");
        Directory.CreateDirectory("Assets/Prefabs/School"); AssetDatabase.Refresh();
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/学校改色/SchoolSpriteUnlit.mat");
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Characters).OfType<Sprite>().ToDictionary(s=>s.name);
        Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Add school interaction and NPC layer");
        var layer=Child(root,"07_Interactions");
        var hub=layer.gameObject.AddComponent<SchoolInteractionController>();
        hub.player=root.Find("06_Player/BlondWhite").GetComponent<TopDownCharacterController>();
        hub.worldCamera=root.Find("Main Camera").GetComponent<Camera>(); hub.chineseFont=font;
        hub.architecture=root.Find("01_Architecture"); hub.solidGeometry=root.Find("05_GameplayGeometry");
        Npc(layer,"NPC_Xiaoyu","sheila",SchoolInteractionKind.Xiaoyu,"Xiaoyu",new Vector2(2.5f,7.25f),"Back",sprites,material);
        Npc(layer,"NPC_Classmate01","Elaine",SchoolInteractionKind.ClassmateOne,"Classmate01",new Vector2(11.75f,4.9f),"SideA",sprites,material);
        Npc(layer,"NPC_Classmate02","nate",SchoolInteractionKind.ClassmateTwo,"Classmate02",new Vector2(5.5f,4.8f),"Back",sprites,material);
        var book=Target(root.Find("02_LeftClassroom/Upper_Left"),"XiaoyuSketchbook","画本",SchoolInteractionKind.Sketchbook);
        book.approachOffset=new Vector2(.75f,-.3f); book.reach=.8f;
        var notice=Target(root.Find("03_RightClassroom/LeftWall_Picture"),"ArtCompetitionNotice","比赛通知",SchoolInteractionKind.Notice);
        notice.approachOffset=new Vector2(0,-1.25f); notice.reach=1f;
        foreach(var spec in new[]{("Locker_5.5","FirstEncounterLocker"),("Locker_14.5","DessertEntranceLocker")})
            Locker(root.Find("04_CorridorFurniture/"+spec.Item1),spec.Item2,material);
        hub.targets=root.GetComponentsInChildren<SchoolInteractable>();
        Physics2D.SyncTransforms();
        if(baseline!=ProtectedState(existingTransforms,existingRenderers,existingMaps)) throw new Exception("Protected original scene content changed.");
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(undo);
        SchoolSceneDetailAdjustments.CaptureOverview(root,"Recovery/School/interactions-after.png");
        File.WriteAllText("Library/SchoolInteractionsSetup.report.txt","PASS\nNative scene saved. Original transforms, sprites, floor tiles, colours and renderer settings unchanged.\n3 NPC prefabs, 2 designated lockers, sketchbook and notice installed.\nExisting sprite names and IDs retained; 15 NPC sprite cuts and a lower cabinet-door cut added through Sprite Editor data providers.\n");
        Selection.activeGameObject=layer.gameObject;
    }
    static string ProtectedState(Transform[] transforms,SpriteRenderer[] renderers,Tilemap[] maps)
    {
        var b=new StringBuilder();
        foreach(var t in transforms) b.Append(t.GetInstanceID()).Append(t.localPosition.ToString("R")).Append(t.localRotation.ToString("R")).Append(t.localScale.ToString("R")).Append(t.gameObject.activeSelf);
        foreach(var sr in renderers) b.Append(EditorJsonUtility.ToJson(sr));
        foreach(var m in maps) b.Append(EditorJsonUtility.ToJson(m)).Append(EditorJsonUtility.ToJson(m.GetComponent<TilemapRenderer>()));
        return b.ToString();
    }
    static Transform Child(Transform parent,string name)
    {
        var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Add school interaction"); go.transform.SetParent(parent,false); return go.transform;
    }
    static SchoolInteractable Target(Transform parent,string id,string name,SchoolInteractionKind kind)
    {
        var t=Child(parent,id); var c=t.gameObject.AddComponent<SchoolInteractable>(); c.interactionId=id; c.displayName=name; c.kind=kind; c.clickVisual=parent; return c;
    }
    static void Npc(Transform parent,string id,string display,SchoolInteractionKind kind,string spritePrefix,Vector2 position,string pose,
        System.Collections.Generic.Dictionary<string,Sprite> sprites,Material material)
    {
        var t=Child(parent,id); t.localPosition=position;
        var group=t.gameObject.AddComponent<SortingGroup>();
        var sort=t.gameObject.AddComponent<TopDownYSort>(); sort.GroundOffset=-.15f;
        var visual=Child(t,"Visual"); visual.localScale=new Vector3(3.639694f,3.249369f,1);
        var sr=visual.gameObject.AddComponent<SpriteRenderer>(); sr.sprite=sprites["SchoolNPC_"+spritePrefix+"_"+pose]; sr.sharedMaterial=material;
        var feet=t.gameObject.AddComponent<BoxCollider2D>(); feet.size=new Vector2(.36f,.2f); feet.offset=new Vector2(0,.05f);
        var target=t.gameObject.AddComponent<SchoolInteractable>(); target.interactionId=id; target.displayName=display; target.kind=kind;
        target.approachOffset=new Vector2(0,-.25f); target.reach=1.15f; target.portrait=sprites["SchoolNPC_"+spritePrefix+"_Front"];
        target.cloudViewAnchor=Child(t,"CloudViewAnchor"); target.cloudViewAnchor.localPosition=new Vector3(0,1.4f,0);
        sort.RefreshOrder();
        PrefabUtility.SaveAsPrefabAssetAndConnect(t.gameObject,"Assets/Prefabs/School/"+id+".prefab",InteractionMode.AutomatedAction);
    }
    static void Locker(Transform t,string id,Material material)
    {
        var closed=t.Find("Visual").GetComponent<SpriteRenderer>();
        var component=Undo.AddComponent<SchoolLocker>(t.gameObject); component.closedVisual=closed;
        var open=Child(t,"OpenInterior"); component.openVisual=open.gameObject;
        var frame=Child(open,"OriginalFrame"); frame.localPosition=closed.transform.localPosition; frame.localScale=closed.transform.localScale;
        var sr=frame.gameObject.AddComponent<SpriteRenderer>(); sr.sprite=closed.sprite; sr.sharedMaterial=material; sr.sortingOrder=closed.sortingOrder;
        var white=AssetDatabase.LoadAllAssetsAtPath("Assets/学校改色/Tileset_32x32_2.png").OfType<Sprite>().Single(s=>s.name=="School_White");
        Rectangle(open,"CabinetInterior",white,new Vector2(0,.64f),new Vector2(.72f,1.08f),new Color(.25f,.22f,.33f),material,1);
        Rectangle(open,"InsideBack",white,new Vector2(-.035f,.66f),new Vector2(.57f,.92f),new Color(.36f,.31f,.44f),material,2);
        Rectangle(open,"Shelf",white,new Vector2(-.035f,.50f),new Vector2(.57f,.035f),new Color(.65f,.57f,.73f),material,3);
        var doorSprite=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().Single(s=>s.name=="School_LockerLowerDoor");
        var door=Child(open,"OpenDoor"); door.localPosition=new Vector3(.33f,.65f,0);
        door.localScale=new Vector3(.17f/doorSprite.bounds.size.x,1.10f/doorSprite.bounds.size.y,1);
        var d=door.gameObject.AddComponent<SpriteRenderer>(); d.sprite=doorSprite; d.sharedMaterial=material; d.sortingOrder=4;
        open.gameObject.SetActive(false);
        var target=Target(t,id,"储物柜",SchoolInteractionKind.Locker); target.locker=component; target.approachOffset=new Vector2(0,-.30f); target.reach=.95f;
    }
    static void Rectangle(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size,Color color,Material material,int order)
    {
        var t=Child(parent,name); t.localPosition=pos; t.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
        var sr=t.gameObject.AddComponent<SpriteRenderer>(); sr.sprite=sprite; sr.color=color; sr.sharedMaterial=material; sr.sortingOrder=order;
    }
    static void PrepareSprites()
    {
        AddCuts(Characters,944,new[]{
            ("SchoolNPC_Xiaoyu_Stand",31,229,23,43),("SchoolNPC_Xiaoyu_Front",350,231,23,41),("SchoolNPC_Xiaoyu_SideA",386,231,21,41),("SchoolNPC_Xiaoyu_SideB",423,231,21,41),("SchoolNPC_Xiaoyu_Back",458,231,24,35),
            ("SchoolNPC_Classmate01_Stand",32,295,23,41),("SchoolNPC_Classmate01_Front",349,297,24,39),("SchoolNPC_Classmate01_SideA",384,297,23,39),("SchoolNPC_Classmate01_SideB",423,297,23,39),("SchoolNPC_Classmate01_Back",461,297,19,33),
            ("SchoolNPC_Classmate02_Stand",30,355,26,45),("SchoolNPC_Classmate02_Front",348,357,26,43),("SchoolNPC_Classmate02_SideA",382,357,26,43),("SchoolNPC_Classmate02_SideB",421,357,26,43),("SchoolNPC_Classmate02_Back",457,357,26,37)},true);
        AddCuts(Atlas,512,new[]{("School_LockerLowerDoor",162,375,28,37)},false);
    }
    static void AddCuts(string path,int height,(string,int,int,int,int)[] cuts,bool feetPivot)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        var factory=new SpriteDataProviderFactories(); factory.Init();
        var provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var rects=provider.GetSpriteRects().ToList(); var original=rects.Select(r=>(r.name,r.spriteID,r.rect)).ToArray();
        var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>(); var pairs=names.GetNameFileIdPairs().ToList();
        foreach(var c in cuts)
        {
            if(rects.Any(r=>r.name==c.Item1)) continue;
            var r=new SpriteRect{name=c.Item1,spriteID=GUID.Generate(),rect=new Rect(c.Item2,height-c.Item3-c.Item5,c.Item4,c.Item5),alignment=feetPivot?SpriteAlignment.BottomCenter:SpriteAlignment.Center,pivot=new Vector2(.5f,feetPivot?0:.5f)};
            rects.Add(r); pairs.Add(new SpriteNameFileIdPair(r.name,r.spriteID));
        }
        provider.SetSpriteRects(rects.ToArray()); names.SetNameFileIdPairs(pairs); provider.Apply(); importer.SaveAndReimport();
        provider=factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        foreach(var o in original) if(!provider.GetSpriteRects().Any(r=>r.name==o.name && r.spriteID==o.spriteID && r.rect==o.rect)) throw new Exception("Existing sprite changed: "+o.name);
    }
}
