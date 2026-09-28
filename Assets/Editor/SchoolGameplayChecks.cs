using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class SchoolGameplayChecks
{
    const string Request="Library/SchoolGameplayChecks.request", Report="Library/SchoolGameplayChecks.report.txt";
    static TopDownCharacterController actor;
    static Rigidbody2D body;
    static TopDownCameraFollow follow;
    static SchoolCameraFootprint footprint;
    static Camera camera;
    static Transform root;
    static RenderTexture target;
    static bool started;
    static int leg, aspectIndex, samples;
    static double legStart;
    static readonly Vector2[] Route={
        new Vector2(7.5f,.45f),new Vector2(1.5f,1),new Vector2(1.5f,4.6f),new Vector2(7,6.2f),
        new Vector2(1.5f,4.6f),new Vector2(1.5f,1),new Vector2(12.5f,1),new Vector2(12.5f,4.5f),
        new Vector2(15.4f,7.4f),new Vector2(17.7f,4.4f),new Vector2(12.5f,4.5f),new Vector2(12.5f,1),
        new Vector2(7.5f,.45f),new Vector2(7.5f,-1.6f),new Vector2(7.5f,.45f)};
    static readonly Vector2Int[] Resolutions={new Vector2Int(600,600),new Vector2Int(960,540),new Vector2Int(540,960),new Vector2Int(1280,360)};

    static SchoolGameplayChecks() { EditorApplication.update+=Poll; }
    [MenuItem("Tools/School/Verify Character and Camera")]
    static void RequestCheck() { File.WriteAllText(Request,"Verify current school scene"); }

    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(!EditorApplication.isPlaying)
        {
            if(started) { File.Delete(Request); started=false; return; }
            if(GameObject.Find("School_ReferenceLayout/06_Player/BlondWhite")==null || File.Exists("Library/SchoolGameplaySetup.request")) return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
            return;
        }
        try
        {
            if(!started)
            {
                root=GameObject.Find("School_ReferenceLayout").transform;
                actor=root.Find("06_Player/BlondWhite").GetComponent<TopDownCharacterController>();
                if(!actor.GetComponent<Animator>().isInitialized) return;
                body=actor.GetComponent<Rigidbody2D>(); camera=root.Find("Main Camera").GetComponent<Camera>();
                follow=camera.GetComponent<TopDownCameraFollow>(); footprint=camera.GetComponent<SchoolCameraFootprint>();
                File.WriteAllText(Report,"School gameplay verification\n");
                actor.UseKeyboardInput=false; body.interpolation=RigidbodyInterpolation2D.None;
                Basics(); started=true; aspectIndex=leg=samples=0;
                actor.SetMoveInput(Vector2.zero); body.simulated=false; SetResolution();
                Teleport(Route[0]); legStart=EditorApplication.timeSinceStartup; return;
            }
            // Traverse the camera's room/door/entrance route in live Play mode.
            float t=Mathf.Clamp01((float)(EditorApplication.timeSinceStartup-legStart)/.22f);
            Teleport(Vector2.Lerp(Route[leg],Route[leg+1],t));
            follow.SendMessage("LateUpdate");
            Require(footprint.ContainsViewport(camera),"Camera left the school footprint at aspect="+aspectIndex+" route="+leg+" position="+camera.transform.position);
            var bounds=actor.GetComponent<SpriteRenderer>().bounds;
            Vector3 bottom=camera.WorldToViewportPoint(bounds.min), top=camera.WorldToViewportPoint(bounds.max);
            Require(bottom.x>=-.002f && bottom.y>=-.002f && top.x<=1.002f && top.y<=1.002f,"Character left the camera view at aspect="+aspectIndex+" route="+leg);
            Require(Mathf.Abs(camera.transform.position.z+10)<.001f,"Camera Z changed"); samples++;
            if(t<1) return;
            leg++; legStart=EditorApplication.timeSinceStartup;
            if(leg<Route.Length-1) return;
            File.AppendAllText(Report,"PASS camera route at "+Resolutions[aspectIndex]+", including both rooms, doors, corridor and entrance.\n");
            aspectIndex++;
            if(aspectIndex<Resolutions.Length) { leg=0; Teleport(Route[0]); SetResolution(); return; }
            Time.timeScale=0; camera.transform.position=new Vector3(-100,100,-10); follow.SendMessage("LateUpdate");
            Require(footprint.ContainsViewport(camera),"Paused camera did not clamp"); Time.timeScale=1;
            aspectIndex=0; SetResolution();
            Teleport(new Vector2(7,6.2f)); follow.ConstrainNow(); Capture("Recovery/School/player-classroom.png");
            Teleport(new Vector2(7.5f,.45f)); follow.ConstrainNow(); Capture("Recovery/School/player-corridor.png");
            File.AppendAllText(Report,"PASS paused resize/clamp, "+samples+" live viewport samples.\nALL PASS\n");
            Finish();
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL: "+e+"\n"); Debug.LogException(e); Finish(); }
    }

    static void Basics()
    {
        var mode=Physics2D.simulationMode; Physics2D.simulationMode=SimulationMode2D.Script;
        try
        {
            Require(Mathf.Abs(actor.MoveSpeed-3.5f)<.001f,"Movement speed differs from source");
            Require(body.gravityScale==0 && body.freezeRotation,"Top-down body configuration");
            foreach(var direction in new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down,Vector2.one})
            {
                Teleport(new Vector2(7.1f,6.2f)); Vector2 start=body.position; Move(direction,10);
                Require(Mathf.Abs(Vector2.Distance(start,body.position)-.7f)<.025f,"Movement/diagonal speed: "+direction);
            }
            var animator=actor.GetComponent<Animator>();
            actor.SetMoveInput(Vector2.right); animator.Update(.05f); animator.Update(.05f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk_Right"),"Right animation");
            actor.SetMoveInput(Vector2.left); animator.Update(.05f); animator.Update(.05f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk_Left"),"Left animation");
            actor.SetMoveInput(Vector2.up); animator.Update(.05f); Require(!animator.GetBool("FacingRight"),"Vertical facing changed");
            actor.SetMoveInput(Vector2.zero); animator.Update(.05f); animator.Update(.05f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle_Left"),"Idle animation");
            File.AppendAllText(Report,"PASS four directions, normalized diagonals, Walk/Idle and vertical facing.\n");
            var desk=root.Find("02_LeftClassroom/Upper_Middle"); var collider=desk.GetComponent<BoxCollider2D>();
            Teleport(new Vector2(3.5f,8.7f)); Move(Vector2.down,45); actor.GetComponent<TopDownYSort>().RefreshOrder();
            Require(actor.GetComponent<BoxCollider2D>().bounds.min.y>=collider.bounds.max.y-.025f,"Passed through desk from behind");
            Require(actor.GetComponent<SpriteRenderer>().sortingOrder<desk.GetComponent<SortingGroup>().sortingOrder,"Behind-desk sorting");
            CaptureDesk("Recovery/School/occlusion-behind.png");
            Teleport(new Vector2(3.5f,7.1f)); Move(Vector2.up,35); actor.GetComponent<TopDownYSort>().RefreshOrder();
            Require(actor.GetComponent<BoxCollider2D>().bounds.max.y<=collider.bounds.min.y+.025f,"Passed through desk from front");
            Require(actor.GetComponent<SpriteRenderer>().sortingOrder>desk.GetComponent<SortingGroup>().sortingOrder,"In-front sorting");
            CaptureDesk("Recovery/School/occlusion-front.png");
            File.AppendAllText(Report,"PASS desk collisions and matching feet-based sorting on both sides.\n");
            if(root.Find("02_LeftClassroom/LeftWall_Desk_1")!=null) DetailChecks();
            foreach(float x in new[]{1.5f,12.5f})
            {
                Teleport(new Vector2(x,1.1f)); Move(Vector2.up,48);
                Require(body.position.y>4.35f,"Classroom doorway blocked at "+x+" position="+body.position);
                Move(Vector2.down,48); Require(body.position.y<1.2f,"Doorway blocked returning to corridor");
            }
            Teleport(new Vector2(11.4f,.8f)); Move(Vector2.up,55); Require(body.position.y<1.9f,"Walked through corridor wall");
            Teleport(new Vector2(.6f,9.5f)); Move(Vector2.left,40); Require(actor.GetComponent<BoxCollider2D>().bounds.min.x>=-.02f,"Crossed exterior wall");
            Teleport(new Vector2(6.2f,10)); Move(Vector2.up,50); Require(actor.GetComponent<BoxCollider2D>().bounds.max.y<=11.0f,"Entered upper wall");
            Teleport(new Vector2(7.5f,-1.2f)); Move(Vector2.down,40); Require(body.position.y>=-1.97f,"Left entrance boundary");
            File.AppendAllText(Report,"PASS both classroom passageways, partition, exterior/back walls and entrance stop.\n");
            LegacyCameraCheck();
        }
        finally { Physics2D.simulationMode=mode; actor.SetMoveInput(Vector2.zero); }
    }

    static void LegacyCameraCheck()
    {
        var go=new GameObject("Temporary rectangular camera regression");
        try
        {
            var c=go.AddComponent<Camera>(); c.enabled=false; c.orthographic=true; c.aspect=16f/9f;
            go.transform.position=new Vector3(-100,100,-10); var f=go.AddComponent<TopDownCameraFollow>();
            f.EdgePadding=1; f.PreferredOrthographicSize=8; f.SetRoomBounds(root,new Rect(0,0,40,30)); f.ConstrainNow();
            var r=f.GetWorldRoomBounds(); var half=new Vector2(c.orthographicSize*c.aspect,c.orthographicSize);
            Vector2 p=c.transform.position;
            Require(p.x-half.x>=r.xMin-.001f && p.x+half.x<=r.xMax+.001f && p.y-half.y>=r.yMin-.001f && p.y+half.y<=r.yMax+.001f,"Mocha rectangular camera regression");
            File.AppendAllText(Report,"PASS existing rectangular camera confinement regression.\n");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void DetailChecks()
    {
        Teleport(new Vector2(7.65f,10.35f));
        foreach(string room in new[]{"02_LeftClassroom","03_RightClassroom"})
        {
            var curtain=root.Find(room+"/PurpleCurtain_Window").GetComponent<SortingGroup>();
            Require(curtain.sortingOrder<actor.GetComponent<SpriteRenderer>().sortingOrder,"Curtain covers character");
        }
        CaptureAt("Recovery/School/curtain-occlusion.png",new Vector2(7.65f,10.9f),1.75f);
        Teleport(new Vector2(1.5f,1.3f));
        foreach(string name in new[]{"LeftClassroom_Door_GroundMat","RightClassroom_Door_GroundMat"})
        {
            var mat=root.Find("04_CorridorFurniture/"+name);
            Require(mat.GetComponentInParent<SchoolDoorPassage>()==null,"Mat still inside doorway sorting");
            Require(mat.GetComponent<SortingGroup>().sortingOrder<-20000,"Door mat is not on the ground");
        }
        CaptureAt("Recovery/School/mat-occlusion.png",new Vector2(1.5f,1.9f),1.65f);
        var books=root.Find("04_CorridorFurniture/Scattered_Books/Visual").GetComponent<SpriteRenderer>().sprite;
        Require(books.rect==new Rect(128,32,64,52),"Incomplete book sprite rectangle");
        foreach(string name in new[]{"Back_EmptyDesk","Back_WorkDesk","Front_WorkDesk","Front_EmptyDesk"})
            Require(root.Find("03_RightClassroom/"+name+"/Visual").GetComponent<SpriteRenderer>().sprite.name=="School_DeskRight","Desk facing mismatch");
        File.AppendAllText(Report,"PASS complete book sprite, right-facing desk assets, curtains behind character and ground-only door mats.\n");
    }

    static void Teleport(Vector2 local)
    {
        body.velocity=Vector2.zero; body.position=root.TransformPoint(local); actor.transform.position=body.position;
        Physics2D.SyncTransforms(); actor.GetComponent<TopDownYSort>().RefreshOrder();
        foreach(var door in root.GetComponentsInChildren<SchoolDoorPassage>()) door.SendMessage("LateUpdate");
    }
    static void Move(Vector2 direction,int steps)
    { actor.SetMoveInput(direction); for(int i=0;i<steps;i++) { actor.SendMessage("FixedUpdate"); Physics2D.Simulate(.02f); } actor.SetMoveInput(Vector2.zero); }
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
    static void SetResolution()
    {
        camera.targetTexture=null; if(target!=null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        var size=Resolutions[aspectIndex]; target=new RenderTexture(size.x,size.y,24); target.Create();
        camera.targetTexture=target; camera.rect=new Rect(0,0,1,1); camera.ResetAspect(); follow.ConstrainNow();
    }
    static void CaptureDesk(string path)
    {
        var previous=camera.transform.position; float size=camera.orthographicSize;
        camera.transform.position=new Vector3(3.5f,8.1f,-10); camera.orthographicSize=1.55f;
        try { Capture(path); } finally { camera.transform.position=previous; camera.orthographicSize=size; }
    }
    static void CaptureAt(string path,Vector2 center,float size)
    {
        var previous=camera.transform.position; float previousSize=camera.orthographicSize;
        camera.transform.position=new Vector3(center.x,center.y,-10); camera.orthographicSize=size;
        try { Capture(path); } finally { camera.transform.position=previous; camera.orthographicSize=previousSize; }
    }
    static void Capture(string path)
    {
        var rt=new RenderTexture(640,640,24); var old=RenderTexture.active; float aspect=camera.aspect;
        var tex=new Texture2D(640,640,TextureFormat.RGB24,false); camera.aspect=1;
        try
        {
            SortingGroup.UpdateAllSortingGroups();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,640,640),0,0); tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG());
        }
        finally { camera.aspect=aspect; RenderTexture.active=old; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); }
    }
    static void Finish()
    {
        Time.timeScale=1; if(camera!=null) { camera.targetTexture=null; camera.rect=new Rect(0,0,1,1); camera.ResetAspect(); }
        if(target!=null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); target=null; }
        File.Delete(Request); started=false; EditorApplication.isPlaying=false;
    }
}
