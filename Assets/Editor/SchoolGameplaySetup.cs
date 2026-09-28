using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SchoolGameplaySetup
{
    const string Request="Library/SchoolGameplaySetup.request";
    static SchoolGameplaySetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Configure(); }
        catch(Exception e) { File.WriteAllText("Library/SchoolGameplaySetup.report.txt","FAIL\n"+e); Debug.LogException(e); }
    }

    [MenuItem("Tools/School/Install Character and Camera")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/school.unity");
        if(!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open school first.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="School_ReferenceLayout").transform;
        Directory.CreateDirectory("Recovery/School");
        if(!EditorSceneManager.SaveScene(scene,"Recovery/School/school-before-gameplay-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true))
            throw new IOException("Could not preserve the open school scene.");
        var sourceScene=SceneManager.GetSceneByPath("Assets/Scenes/MochaStore.unity");
        bool openedSource=!sourceScene.IsValid() || !sourceScene.isLoaded;
        if(openedSource) sourceScene=EditorSceneManager.OpenScene("Assets/Scenes/MochaStore.unity",OpenSceneMode.Additive);
        try
        {
            var source=sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TopDownCharacterController>()).Single();
            var sourceFollow=sourceScene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TopDownCameraFollow>()).Single();
            SceneManager.SetActiveScene(scene);
            var playerRoot=root.Find("06_Player"); bool first=playerRoot==null;
            if(first)
            {
                playerRoot=Child(root,"06_Player");
                // Preserve the copied actor's component settings; adapt its parent to the school's tile scale.
                playerRoot.localScale=Vector3.one*.5f;
                var copy=UnityEngine.Object.Instantiate(source.gameObject);
                copy.name="BlondWhite"; SceneManager.MoveGameObjectToScene(copy,scene);
                copy.transform.SetParent(playerRoot,false); copy.transform.localScale=source.transform.localScale;
                copy.transform.position=root.TransformPoint(new Vector3(7.5f,.45f,0));
                Undo.RegisterCreatedObjectUndo(copy,"Copy MochaStore character");
            }
            var actor=playerRoot.GetComponentInChildren<TopDownCharacterController>(true);
            actor.enabled=true; actor.UseKeyboardInput=true; actor.MoveSpeed=source.MoveSpeed;
            actor.GetComponent<SpriteRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/学校改色/SchoolSpriteUnlit.mat");
            actor.GetComponent<TopDownYSort>().RefreshOrder();
            actor.GetComponent<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
            ConfigureFurniture(root);
            ConfigureArchitecture(root,actor.transform);
            if(first)
            {
                // Clear the small classroom's doorway, where the original decorative chair sat in the walking path.
                foreach(string name in new[]{"Front_WorkDesk","Front_WorkDesk_Chair"})
                {
                    var t=root.Find("03_RightClassroom/"+name);
                    if(t!=null) t.localPosition+=Vector3.left*.4f;
                }
            }
            var camera=root.Find("Main Camera").GetComponent<Camera>();
            var framing=camera.GetComponent<OrthographicSceneFraming>(); if(framing!=null) Undo.DestroyObjectImmediate(framing);
            var footprint=Get<SchoolCameraFootprint>(camera.gameObject);
            footprint.Configure(root,new[]{
                Rect.MinMaxRect(-.28f,-.28f,9.28f,12.88f),
                Rect.MinMaxRect(10.72f,-.28f,18.28f,9.88f),
                Rect.MinMaxRect(-.28f,-.28f,18.28f,3.94f),
                Rect.MinMaxRect(5.72f,-1.98f,9.28f,3.94f)});
            var follow=Get<TopDownCameraFollow>(camera.gameObject);
            follow.Target=actor.transform; follow.Offset=sourceFollow.Offset*.5f; follow.SmoothTime=sourceFollow.SmoothTime;
            follow.PreferredOrthographicSize=sourceFollow.PreferredOrthographicSize*.5f;
            camera.transform.position=new Vector3(actor.transform.position.x,actor.transform.position.y,-10);
            follow.ConstrainNow();
            var background=root.Find("Camera Letterbox Background");
            if(background==null) background=Child(root,"Camera Letterbox Background");
            var bg=Get<Camera>(background.gameObject); bg.clearFlags=CameraClearFlags.SolidColor; bg.backgroundColor=camera.backgroundColor;
            bg.cullingMask=0; bg.depth=camera.depth-10; bg.orthographic=true;
            background.position=new Vector3(0,0,-10); Get<UniversalAdditionalCameraData>(background.gameObject);
            EditorUtility.SetDirty(follow); EditorUtility.SetDirty(footprint);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("School scene save failed.");
            File.WriteAllText("Library/SchoolGameplaySetup.report.txt",
                "PASS\nCopied BlondWhite from MochaStore. Same controller, Animator, speed="+actor.MoveSpeed+
                ", Rigidbody2D and feet collider. School parent scale=0.5.\nFurniture feet sorting configured.\nTwo automatic door passages and complete room collisions.\nCamera uses school footprint union.\n");
            Selection.activeGameObject=actor.gameObject;
            Debug.Log("School character, occlusion and confined camera saved.");
        }
        finally
        {
            if(openedSource) EditorSceneManager.CloseScene(sourceScene,true);
            SceneManager.SetActiveScene(scene);
        }
    }

    static T Get<T>(GameObject go) where T:Component
    {
        var component=go.GetComponent<T>();
        return component!=null ? component : Undo.AddComponent<T>(go);
    }
    static Transform Child(Transform parent,string name)
    { var go=new GameObject(name); go.transform.SetParent(parent,false); Undo.RegisterCreatedObjectUndo(go,"Configure school gameplay"); return go.transform; }

    static void ConfigureFurniture(Transform root)
    {
        foreach(string room in new[]{"02_LeftClassroom","03_RightClassroom","04_CorridorFurniture"})
        {
            var section=root.Find(room);
            foreach(var group in section.GetComponentsInChildren<SortingGroup>())
            {
                if(group.transform.parent.GetComponentInParent<SortingGroup>()!=null) continue; // tabletop objects remain within their furniture
                if(group.name=="Scattered_Books" || group.name.EndsWith("_GroundMat")) { group.sortingOrder=-28000; continue; }
                var floorCollider=group.GetComponent<BoxCollider2D>();
                Get<TopDownYSort>(group.gameObject).GroundOffset=floorCollider!=null ? floorCollider.bounds.center.y-group.transform.position.y : 0;
            }
            foreach(Transform t in section)
            {
                if(!t.name.Contains("Window") && t.name!="Wall_Extinguisher") continue;
                Get<SortingGroup>(t.gameObject);
                var oldSort=t.GetComponent<TopDownYSort>();
                if(oldSort!=null) Undo.DestroyObjectImmediate(oldSort);
                t.GetComponent<SortingGroup>().sortingOrder=-24000;
            }
        }
    }

    static void ConfigureArchitecture(Transform root,Transform actor)
    {
        var geometry=root.Find("05_GameplayGeometry");
        if(geometry==null)
        {
            geometry=Child(root,"05_GameplayGeometry");
            var partition=Child(geometry,"CorridorOccludingWall"); Get<SortingGroup>(partition.gameObject);
            Get<TopDownYSort>(partition.gameObject).GroundOffset=root.TransformPoint(new Vector3(0,2,0)).y-partition.position.y;
            var original=root.Find("01_Architecture/Corridor_FrontWall");
            foreach(var renderer in original.GetComponentsInChildren<SpriteRenderer>())
            {
                var bounds=renderer.bounds;
                foreach(var span in new[]{new Vector2(0,1),new Vector2(2,12),new Vector2(13,18)})
                {
                    float left=root.TransformPoint(new Vector3(span.x,0,0)).x, right=root.TransformPoint(new Vector3(span.y,0,0)).x;
                    var t=Child(partition,renderer.name+" "+span.x);
                    var copy=t.gameObject.AddComponent<SpriteRenderer>(); EditorUtility.CopySerialized(renderer,copy);
                    t.position=new Vector3((left+right)*.5f,bounds.center.y,bounds.center.z);
                    t.localScale=new Vector3((right-left)/copy.sprite.bounds.size.x/root.lossyScale.x,bounds.size.y/copy.sprite.bounds.size.y/root.lossyScale.y,1);
                }
            }
            original.gameObject.SetActive(false);
            foreach(string cap in new[]{"Partition left of door","Partition between doors","Partition right of door"})
            {
                var t=root.Find("01_Architecture/"+cap); if(t!=null) Undo.SetTransformParent(t,partition,"Sort partition wall as a whole");
            }
            Box(geometry,"Left classroom back wall",new Rect(0,10.97f,9,.12f));
            Box(geometry,"Right classroom back wall",new Rect(11,7.97f,7,.12f));
            Box(geometry,"Entry bottom",new Rect(6,-2.04f,3,.1f));
            foreach(var span in new[]{new Vector2(0,1),new Vector2(2,12),new Vector2(13,18)})
                Box(geometry,"Solid corridor partition "+span.x,new Rect(span.x,2,span.y-span.x,1.95f));
        }
        foreach(var spec in new[]{("LeftClassroom_Door",1f),("RightClassroom_Door",12f)})
        {
            var door=root.Find("04_CorridorFurniture/"+spec.Item1);
            Get<SortingGroup>(door.gameObject);
            Get<TopDownYSort>(door.gameObject).GroundOffset=root.TransformPoint(new Vector3(0,2,0)).y-door.position.y;
            var panels=door.GetComponentsInChildren<SpriteRenderer>().Where(r=>!r.name.ToLowerInvariant().Contains("mat")).ToArray();
            Get<SchoolDoorPassage>(door.gameObject).Configure(actor,root,new Rect(spec.Item2-.55f,.8f,2.1f,4f),panels);
        }
    }

    static void Box(Transform parent,string name,Rect rect)
    {
        var t=Child(parent,name); t.localPosition=rect.center;
        var collider=t.gameObject.AddComponent<BoxCollider2D>(); collider.size=rect.size;
    }
}
