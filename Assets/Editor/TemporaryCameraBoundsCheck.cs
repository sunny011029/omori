using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class TemporaryCameraBoundsCheck
{
    const string Key = "MochaCameraBoundsCheck";
    const string Report = "Temp/MochaCameraBounds.report.txt";
    static TopDownCameraFollow follow;
    static Camera camera;
    static Transform actor;
    static int stage = -1, samples;
    static double stageStart;
    static float depth;
    static readonly float[] Aspects = { 1, 16f/9, 9f/16, 32f/9, 64f/9, 1, 16f/9, 1 };

    static TemporaryCameraBoundsCheck()
    {
        EditorApplication.delayCall += Setup;
        EditorApplication.update += Tick;
    }

    static void Setup()
    {
        const string request = "Temp/MochaCameraBoundsSetup.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try
        {
            UnityEngine.Object.FindObjectOfType<TopDownCameraFollow>().EdgePadding = 1f;
            MochaStoreCameraBoundsSetup.Configure();
            File.WriteAllText(Report,"SETUP PASS: saved camera room confinement.\n");
            SessionState.SetBool(Key,true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { File.WriteAllText(Report,"FAIL setup: " + e); Debug.LogException(e); }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key,false)) Setup();
        if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (follow == null)
            {
                follow = UnityEngine.Object.FindObjectOfType<TopDownCameraFollow>();
                camera = follow.GetComponent<Camera>(); actor = follow.Target;
                actor.GetComponent<TopDownCharacterController>().enabled = false;
                actor.GetComponent<Rigidbody2D>().simulated = false;
                depth = camera.transform.position.z;
                File.AppendAllText(Report,"ROOM " + follow.GetWorldRoomBounds() + "\n");
                NextStage(); return;
            }
            follow.SendMessage("LateUpdate");
            Rect room = follow.GetWorldRoomBounds();
            for (int x=0;x<=1;x++) for (int y=0;y<=1;y++)
            {
                Vector3 corner = camera.ViewportToWorldPoint(new Vector3(x,y,-depth));
                if (corner.x < room.xMin-.002f || corner.x > room.xMax+.002f || corner.y < room.yMin-.002f || corner.y > room.yMax+.002f)
                    throw new Exception("Viewport escaped at stage " + stage + ": " + corner + " room " + room);
            }
            if (Mathf.Abs(camera.transform.position.z-depth) > .001f) throw new Exception("Camera depth changed");
            if (stage == 5 && Mathf.Abs(camera.orthographicSize-follow.PreferredOrthographicSize) > .001f)
                throw new Exception("Preferred zoom did not restore after ultra-wide");
            samples++;
            if (EditorApplication.timeSinceStartup-stageStart < .8) return;
            File.AppendAllText(Report,"PASS stage " + stage + " aspect=" + camera.aspect + " size=" + camera.orthographicSize + " camera=" + camera.transform.position + "\n");
            if (stage==0 || stage==3 || stage==7) Capture(stage);
            if (stage==Aspects.Length-1)
            {
                File.AppendAllText(Report,"PASS all viewport corners, 8 stages, " + samples + " samples. Includes resize, portrait, ultra-wide, paused and missing target.\n");
                Finish();
            }
            else NextStage();
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL " + e + "\n"); Debug.LogException(e); Finish(); }
    }

    static void NextStage()
    {
        stage++; stageStart = EditorApplication.timeSinceStartup;
        camera.aspect = Aspects[stage];
        var room = follow.GetWorldRoomBounds();
        // Visit all four corners, then stress an already-outside camera during paused resize.
        actor.position = new Vector3((stage%2==0 ? room.xMin : room.xMax), (stage%4<2 ? room.yMax : room.yMin),0);
        if (stage==6) { Time.timeScale=0; camera.transform.position=new Vector3(-100,100,depth); }
        if (stage==7) { Time.timeScale=1; follow.Target=null; camera.transform.position=new Vector3(100,-100,depth); }
    }

    static void Capture(int index)
    {
        int height=400, width=Mathf.RoundToInt(height*camera.aspect);
        var rt = new RenderTexture(width,height,24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=rt });
            RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            File.WriteAllBytes("Temp/MochaCameraBounds_" + index + ".png",texture.EncodeToPNG());
        }
        finally { RenderTexture.active=previous; UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    }

    static void Finish()
    {
        Time.timeScale=1; SessionState.SetBool(Key,false); EditorApplication.isPlaying=false;
    }
}
