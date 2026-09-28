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

// Targeted corrections only: never rebuild the scene or replace any floor tiles/materials.
[InitializeOnLoad]
public static class SchoolSceneDetailAdjustments
{
    const string Atlas="Assets/学校改色/img_v3_02141_ba0ed6ee-51b1-4e4e-bb12-31cec6464dhu.png";
    const string Request="Library/SchoolSceneDetails.request", Report="Library/SchoolSceneDetails.report.txt";
    static SchoolSceneDetailAdjustments() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Apply(); } catch(Exception e) { File.WriteAllText(Report,"FAIL\n"+e); Debug.LogException(e); }
    }

    [MenuItem("Tools/School/Adjust Furniture and Occlusion (Preserve Floors)")]
    public static void Apply()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/school.unity");
        if(!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Open school in Edit mode.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="School_ReferenceLayout").transform;
        Directory.CreateDirectory("Recovery/School");
        if(!EditorSceneManager.SaveScene(scene,"Recovery/School/school-before-details-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true)) throw new IOException("Backup failed.");
        string floors=FloorState(root);
        CaptureOverview(root,"Recovery/School/details-before.png");
        // Complete the existing installation, preserving the current character and all floor edits.
        SchoolGameplaySetup.Configure();
        PrepareCuts();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(s=>s.name);
        var left=root.Find("02_LeftClassroom");
        // These are spare desks along the walls in the reference, not standalone front-facing chairs.
        var wallDesks=left.Cast<Transform>().Where(t=>t.name.StartsWith("LeftWall_Chair_") || t.name.StartsWith("LeftWall_Desk_")).OrderByDescending(t=>t.localPosition.y).ToArray();
        for(int i=0;i<wallDesks.Length;i++) { SetProp(wallDesks[i],sprites["School_DeskEmpty"],.6f,6.85f-i*1.25f,.95f,1.35f); wallDesks[i].name="LeftWall_Desk_"+(i+1); }
        var upper=left.Find("UpperRight_Desk"); if(upper==null) upper=left.Find("UpperRight_Chair");
        SetProp(upper,sprites["School_DeskEmpty"],8.45f,9.85f,.95f,1.35f); upper.name="UpperRight_Desk";
        var lower=left.Find("LowerRight_Desk"); if(lower==null) lower=left.Find("LowerRight_Chair");
        SetProp(lower,sprites["School_DeskEmpty"],8.45f,4.65f,.95f,1.35f); lower.name="LowerRight_Desk";
        // Align the two desk clusters and their matching chairs while preserving the reference pattern.
        foreach(var spec in new[]{("Upper_Left",2.5f,7.75f),("Upper_Middle",3.5f,7.75f),("Upper_Right",4.5f,7.75f),("Upper_Side",6.5f,7.75f),
            ("Lower_Left",3.5f,5.75f),("Lower_Middle",4.5f,5.75f),("Lower_Right",5.5f,5.75f)})
        {
            var desk=left.Find(spec.Item1); desk.localPosition=new Vector3(spec.Item2,spec.Item3,0);
            var chair=left.Find(spec.Item1+"_Chair"); if(chair!=null) chair.localPosition=new Vector3(spec.Item2,spec.Item3-.65f,0);
        }
        var right=root.Find("03_RightClassroom");
        SidePair(right,"Back_EmptyDesk","Back_Chair",12.4f,6.55f,sprites);
        SidePair(right,"Back_WorkDesk","Back_WorkDesk_Chair",14.6f,6.55f,sprites);
        SidePair(right,"Front_WorkDesk","Front_WorkDesk_Chair",12.4f,4.75f,sprites);
        SidePair(right,"Front_EmptyDesk","Front_SideChair",14.6f,4.75f,sprites);
        SetProp(right.Find("Side_TeachingTable"),sprites["School_SideTeacherDesk"],16.7f,5.55f,1f,2.15f);
        SetProp(right.Find("Side_Chair"),sprites["School_ChairRight"],15.9f,5.7f,.65f,1.1f);
        var globe=right.Find("Side_TeachingTable/Teaching_Globe");
        globe.position=root.TransformPoint(new Vector3(16.7f,6.65f,0));
        globe.GetComponent<SortingGroup>().sortingOrder=1;
        foreach(var room in new[]{left,right}) foreach(Transform t in room)
            if(t.name.Contains("Window") || t.name.Contains("Picture") || t.name.Contains("board") || t.name.Contains("Board")) SetFixedOrder(t,-24000);
        var hall=root.Find("04_CorridorFurniture");
        foreach(var door in hall.GetComponentsInChildren<SchoolDoorPassage>())
        {
            var mat=hall.Find(door.name+"_GroundMat"); if(mat==null) mat=Child(hall,door.name+"_GroundMat");
            foreach(var sr in door.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.ToLowerInvariant().Contains("mat")).ToArray())
                Undo.SetTransformParent(sr.transform,mat,"Separate door mats from occluding doors");
            SetFixedOrder(mat,-28000);
        }
        var books=hall.Find("Scattered_Books");
        SetProp(books,sprites["School_Books"],16.35f,.14f,1.6f,1.3f,false); SetFixedOrder(books,-27900);
        SetFixedOrder(hall.Find("Wall_Extinguisher"),-24000);
        Physics2D.SyncTransforms();
        foreach(var sort in root.GetComponentsInChildren<TopDownYSort>()) sort.RefreshOrder();
        if(floors!=FloorState(root)) throw new Exception("Protected floor state changed.");
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed.");
        AssetDatabase.SaveAssets();
        CaptureOverview(root,"Recovery/School/details-after.png");
        File.WriteAllText(Report,"PASS\nAll floor Tilemaps, tile references, transforms, colours and renderer settings unchanged.\nFull 64x52 scattered-book cut, five restored left-wall desks.\nFour aligned right-facing desk/chair pairs; right-facing teaching desk/chair.\nCurtains and wall decorations behind characters; door mats separated into ground-only groups.\nScene saved with native Unity backup.\n");
        Selection.activeGameObject=root.gameObject;
        File.WriteAllText("Library/SchoolGameplayChecks.request","Verify adjusted school");
    }

    static string FloorState(Transform root)
    {
        var text=new StringBuilder();
        foreach(var map in root.GetComponentsInChildren<Tilemap>())
        {
            text.Append(EditorJsonUtility.ToJson(map)); text.Append(EditorJsonUtility.ToJson(map.GetComponent<TilemapRenderer>()));
            text.Append(EditorJsonUtility.ToJson(map.transform)); text.Append(EditorJsonUtility.ToJson(map.transform.parent));
        }
        return text.ToString();
    }

    static void PrepareCuts()
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);
        var factories=new SpriteDataProviderFactories(); factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var rects=provider.GetSpriteRects().ToList(); var nameProvider=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var names=nameProvider.GetNameFileIdPairs().ToList();
        foreach(var cut in new[]{("School_Books",128,428,64,52),("School_DeskRight",128,80,32,48),
            ("School_ChairRight",160,144,32,48),("School_SideTeacherDesk",128,208,32,80),("School_SidePapers",64,208,32,48)})
        {
            var rect=rects.FirstOrDefault(r=>r.name==cut.Item1);
            if(rect==null) { rect=new SpriteRect{name=cut.Item1,spriteID=GUID.Generate()}; rects.Add(rect); names.Add(new SpriteNameFileIdPair(rect.name,rect.spriteID)); }
            rect.rect=new Rect(cut.Item2,512-cut.Item3-cut.Item5,cut.Item4,cut.Item5); rect.alignment=SpriteAlignment.Center; rect.pivot=new Vector2(.5f,.5f);
        }
        provider.SetSpriteRects(rects.ToArray()); nameProvider.SetNameFileIdPairs(names); provider.Apply(); importer.SaveAndReimport();
    }

    static Transform Child(Transform parent,string name)
    { var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Adjust school details"); go.transform.SetParent(parent,false); return go.transform; }
    static T Ensure<T>(GameObject go) where T:Component
    { var c=go.GetComponent<T>(); return c!=null ? c : Undo.AddComponent<T>(go); }

    static void SetFixedOrder(Transform t,int order)
    {
        if(t==null) return;
        var sort=t.GetComponent<TopDownYSort>(); if(sort!=null) Undo.DestroyObjectImmediate(sort);
        Ensure<SortingGroup>(t.gameObject).sortingOrder=order;
    }

    static void SetProp(Transform t,Sprite sprite,float x,float y,float w,float h,bool solid=true)
    {
        Undo.RecordObject(t,"Align furniture"); t.localPosition=new Vector3(x,y,0);
        var visual=t.Find("Visual"); var sr=visual.GetComponent<SpriteRenderer>(); Undo.RecordObject(sr,"Correct furniture sprite"); sr.sprite=sprite;
        visual.localPosition=new Vector3(0,h*.5f,0); visual.localScale=new Vector3(w/sprite.bounds.size.x,h/sprite.bounds.size.y,1);
        sr.flipX=sr.flipY=false;
        if(solid)
        {
            var box=Ensure<BoxCollider2D>(t.gameObject); box.size=new Vector2(w*.8f,Mathf.Min(.32f,h*.2f)); box.offset=new Vector2(0,.15f);
            Ensure<TopDownYSort>(t.gameObject).GroundOffset=.15f;
        }
    }

    static void SidePair(Transform room,string deskName,string chairName,float x,float y,System.Collections.Generic.Dictionary<string,Sprite> sprites)
    {
        var desk=room.Find(deskName); SetProp(desk,sprites["School_DeskRight"],x,y,.85f,1.3f);
        var chair=room.Find(chairName);
        if(chair==null)
        {
            chair=Child(room,chairName); var v=Child(chair,"Visual"); var sr=v.gameObject.AddComponent<SpriteRenderer>();
            sr.sharedMaterial=desk.Find("Visual").GetComponent<SpriteRenderer>().sharedMaterial;
        }
        Ensure<SortingGroup>(chair.gameObject); SetProp(chair,sprites["School_ChairRight"],x-.75f,y,.6f,1.1f);
        if(deskName.Contains("Work"))
        {
            var papers=desk.Find("Tabletop_Papers"); if(papers==null) papers=Child(desk,"Tabletop_Papers");
            var sr=Ensure<SpriteRenderer>(papers.gameObject); sr.sprite=sprites["School_SidePapers"];
            sr.sharedMaterial=desk.Find("Visual").GetComponent<SpriteRenderer>().sharedMaterial; sr.sortingOrder=1;
            papers.localPosition=new Vector3(0,.9f,0); papers.localScale=new Vector3(.5f/sr.sprite.bounds.size.x,.7f/sr.sprite.bounds.size.y,1);
        }
    }

    public static void CaptureOverview(Transform root,string path)
    {
        var go=new GameObject("Temporary detail overview"); var camera=go.AddComponent<Camera>(); camera.enabled=false;
        camera.orthographic=true; camera.orthographicSize=8.15f; camera.aspect=1.25f;
        go.transform.position=root.TransformPoint(new Vector3(9,5.35f,-10)); camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.243f,.239f,.337f); go.AddComponent<UniversalAdditionalCameraData>();
        var rt=new RenderTexture(1400,1120,24); var texture=new Texture2D(1400,1120,TextureFormat.RGB24,false); var old=RenderTexture.active;
        try
        {
            SortingGroup.UpdateAllSortingGroups(); RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1400,1120),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally { RenderTexture.active=old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
    }
}
