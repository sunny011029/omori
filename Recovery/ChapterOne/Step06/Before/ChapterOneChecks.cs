using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runs the real scene components in Play mode with a separate, disposable save file.
[InitializeOnLoad]
public static class ChapterOneChecks
{
    const string Request="Library/ChapterOneChecks.request";
    static string Report="Library/ChapterOneChecks.report.txt";
    static bool started;
    static Queue<Func<bool>> steps;
    static double next,deadline;
    static readonly List<string> errors=new List<string>();
    static ChapterOneScene chapter;
    static SchoolInteractionController hub;
    static ChapterOneProgress progress;
    static ChapterOneChecks() { EditorApplication.update+=Poll; }
    [MenuItem("Tools/School/Verify Chapter One Playable Flow")]
    static void RequestCheck()=>File.WriteAllText(Request,"run");
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(!EditorApplication.isPlaying)
        {
            if(started) { File.Delete(Request); started=false; return; }
            if(GameObject.Find("School_ReferenceLayout/08_ChapterOne")==null) return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
            return;
        }
        try
        {
            if(!started)
            {
                if(ChapterOneProgress.Instance==null || UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null) return;
                Bind();
                if(File.ReadAllText(Request).Trim()=="preview-clean")
                { hub.player.UseKeyboardInput=true; Near("Clean_3"); File.Delete(Request); return; }
                if(File.ReadAllText(Request).Trim()=="preview-cloud")
                {
                    progress.Data.cleanedMask=15; progress.Data.phase=ChapterOnePhase.ShopIntroduced; progress.Data.cloudVisionUnlocked=true;
                    chapter.Refresh(); hub.player.UseKeyboardInput=true; Near("NPC_Xiaoyu"); File.Delete(Request); return;
                }
                if(File.ReadAllText(Request).Trim().StartsWith("resume-",StringComparison.Ordinal)) InitializeResume();
                else Initialize();
                started=true; next=Time.unscaledTimeAsDouble+.6; deadline=next+30; return;
            }
            if(Time.unscaledTimeAsDouble<next) return;
            next=Time.unscaledTimeAsDouble+.4;
            if(steps.Count==0)
            { Require(errors.Count==0,"Runtime errors: "+string.Join("\n",errors)); Pass("No runtime exceptions/errors; isolated save only"); File.AppendAllText(Report,"ALL PASS\n"); Finish(); return; }
            if(steps.Peek()()) { steps.Dequeue(); deadline=Time.unscaledTimeAsDouble+30; }
            else Require(Time.unscaledTimeAsDouble<deadline,"Timed out waiting for scene load");
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL\n"+e+"\n"); Finish(); Debug.LogException(e); }
    }
    static void Bind()
    {
        chapter=UnityEngine.Object.FindObjectOfType<ChapterOneScene>(); hub=chapter.interaction; progress=chapter.Progress;
        hub.player.UseKeyboardInput=false; hub.player.GetComponent<Rigidbody2D>().interpolation=RigidbodyInterpolation2D.None;
    }
    static SchoolInteractable Target(string id)=>hub.targets.Single(t=>t.interactionId==id);
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    static void Pass(string message)=>File.AppendAllText(Report,"PASS "+message+"\n");
    static void Add(Action action)=>steps.Enqueue(()=>{action();return true;});
    static void WaitScene(string name)=>steps.Enqueue(()=>
    {
        if(!SceneManager.GetActiveScene().name.Equals(name,StringComparison.OrdinalIgnoreCase) || UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null) return false;
        Bind(); return progress.Data.scene==name && !chapter.IsTransitioning;
    });
    static void Near(string id)
    {
        var target=Target(id); Vector2 p=target.ApproachPoint;
        if(chapter.isSchool)
        {
            p=chapter.roomRoot.InverseTransformPoint(p);
            p.y=p.y>=4 ? Mathf.Max(4.25f,p.y-.45f) : Mathf.Max(.35f,p.y-.45f);
        }
        else p.y-=.5f;
        chapter.PlacePlayer(p); hub.player.SetMoveInput(Vector2.zero);
        Require(hub.CanReach(target),"Target is unreachable: "+id+" player="+hub.player.transform.position);
        NoFootOverlap(id);
    }
    static void Open(string id) { Near(id); Require(hub.TryInteract(Target(id)),"Rejected interaction "+id); }
    static void Close()=>hub.CloseDialogue();
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot("Recovery/ChapterOne/"+name+".png");
    static void NoFootOverlap(string context)
    {
        Physics2D.SyncTransforms(); var feet=hub.player.GetComponent<BoxCollider2D>();
        foreach(var c in Physics2D.OverlapBoxAll(feet.bounds.center,feet.bounds.size*.95f,0))
            Require(c==feet || c.isTrigger || c.transform.IsChildOf(hub.player.transform),"Blocked feet at "+context+": "+c.name);
    }
    static void SceneIntegrity()
    {
        Require(UnityEngine.Object.FindObjectsOfType<TopDownCharacterController>().Length==1,"Duplicate player after travel");
        Require(UnityEngine.Object.FindObjectsOfType<ChapterOneProgress>().Length==1,"Duplicate progress singleton");
        Require(UnityEngine.Object.FindObjectsOfType<TopDownCameraFollow>().Length==1,"Duplicate following camera");
        foreach(var go in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(var t in go.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing component on "+t.name);
        NoFootOverlap("scene arrival"); CameraInside();
    }
    static void CameraInside()
    {
        var camera=hub.worldCamera; var follow=camera.GetComponent<TopDownCameraFollow>(); follow.ConstrainNow();
        if(chapter.isSchool) Require(camera.GetComponent<SchoolCameraFootprint>().ContainsViewport(camera),"School camera escaped room");
        else
        {
            Rect b=follow.GetWorldRoomBounds(); Vector2 half=new Vector2(camera.orthographicSize*camera.aspect,camera.orthographicSize),p=camera.transform.position;
            Require(p.x-half.x>=b.xMin-.01f && p.x+half.x<=b.xMax+.01f && p.y-half.y>=b.yMin-.01f && p.y+half.y<=b.yMax+.01f,"Shop camera escaped room");
        }
    }
    static void Initialize()
    {
        Report="Library/ChapterOneChecks.report.txt";
        File.WriteAllText(Report,"Native Unity chapter one Play-mode integration checks\n"); errors.Clear(); Application.logMessageReceived+=Log;
        Require(progress.SavePath.Contains("ChapterOneTests"),"Test attempted to use player save");
        steps=new Queue<Func<bool>>();
        Add(()=>{
            Require(progress.Data.phase==ChapterOnePhase.Cleaning && progress.CleanCount==0,"Fresh game state");
            Require(hub.targets.Length==15 && hub.targets.Select(t=>t.interactionId).Distinct().Count()==15,"School target identity/count");
            Require(!chapter.ToggleCloudVision(),"Cloud vision unlocked too early");
            Require(!progress.TriggerAnomaly() && !progress.MeetCloud() && !progress.IntroduceShop(),"Out-of-order story transition accepted");
            chapter.Travel("MochaStore"); Require(!chapter.IsTransitioning,"Premature shop travel");
            Open("DessertEntranceLocker"); Require(Target("DessertEntranceLocker").locker.IsOpen && !hub.DialogueOpen,"Ordinary cabinet should still open");
            Pass("Fresh state, 15 unique targets, locked story transitions, cabinet opens before portal unlock");
        });
        Add(()=>{ Open("NPC_Xiaoyu"); hub.Choose(0); Require(hub.HasSketchbookPermission,"Sketchbook consent failed"); Close(); });
        for(int i=0;i<4;i++)
        {
            int index=i;
            Add(()=>{
                Open("Clean_"+index); Require(progress.CleanCount==index+1,"Cleanup count wrong");
                Require(!Target("Clean_"+index).chapterTarget.visual.activeSelf,"Cleaned item still visible");
                Require(!progress.CompleteClean(index),"Duplicate cleanup counted"); Require(!hub.player.enabled,"Dialogue did not lock movement");
            });
            if(i==3) Add(()=>Capture("01-books-tidied"));
            Add(Close);
        }
        Add(()=>{
            Require(progress.Data.phase==ChapterOnePhase.ReadyToLeave,"100 percent did not wait for exit");
            Require(chapter.chapterTargets.Where(t=>t.action==ChapterOneAction.RecurringPaper).All(t=>!t.visual.activeSelf),"Paper appeared before exit");
            Require(chapter.roomRoot.Find("04_CorridorFurniture/Right_LargeBookcase").gameObject.activeSelf,"Bookcase removed with books");
            chapter.PlacePlayer(new Vector2(7.5f,-1.1f));
        });
        Add(()=>{
            Require(progress.Data.phase==ChapterOnePhase.Anomaly && hub.DialogueOpen,"Walking to exit did not trigger anomaly");
            Require(chapter.chapterTargets.Count(t=>t.action==ChapterOneAction.RecurringPaper && t.visual.activeSelf)==3,"Expected three recurring papers");
            Close(); Pass("Four tasks reach 100%; only scattered books hide; exit triggers exactly three abnormal papers");
        });
        Add(()=>{ Open("Recurring_2"); Require(hub.Speaker=="无法清理" && progress.Data.phase==ChapterOnePhase.SeekingTool,"Recurring paper response/stage"); });
        Add(()=>Capture("02-cannot-clean"));
        Add(Close);
        Add(()=>{ Open("Recurring_2"); Require(progress.CleanCount==4 && Target("Recurring_2").chapterTarget.visual.activeSelf && !progress.TriggerAnomaly(),"Recurring paper wrongly removed/rewarded"); Close(); Pass("Repeat interactions say cannot clean, keep papers and do not inflate progress"); });
        Add(()=>{ Open("FirstEncounterLocker"); Require(chapter.encounterLocker.IsOpen && chapter.encounterCloud.activeInHierarchy,"Cloud missing from opened cabinet"); hub.Choose(0); Close(); Require(progress.Data.phase==ChapterOnePhase.SeekingTool,"Cancelled encounter advanced story"); });
        Add(()=>{ Open("FirstEncounterLocker"); hub.Choose(0); });
        Add(()=>Capture("03-locker-encounter"));
        Add(()=>{ hub.Choose(0); hub.Choose(0); hub.Choose(0); Require(progress.Data.phase==ChapterOnePhase.CloudMet && hub.player.enabled,"Encounter did not finish/release movement"); Pass("Cancelled encounter resumes; complete conversation unlocks designated portal once"); });
        Add(()=>{ Open("DessertEntranceLocker"); Require(chapter.portalGlow.activeInHierarchy,"Portal glow not visible"); hub.Choose(0); chapter.Travel("MochaStore"); Require(chapter.IsTransitioning,"Portal did not start load"); });
        WaitScene("MochaStore");
        Add(()=>{ SceneIntegrity(); Require(progress.Data.sketchbookPermission && progress.CleanCount==4,"Progress lost on travel"); Open("ShopMatchaCloud"); hub.Choose(0); Close(); Require(progress.Data.phase==ChapterOnePhase.CloudMet,"Cancelled introduction advanced story"); });
        Add(()=>{ Open("ShopMatchaCloud"); hub.Choose(0); });
        Add(()=>Capture("04-shop-introduction"));
        Add(()=>{ hub.Choose(0); hub.Choose(0); hub.Choose(0); Require(progress.Data.phase==ChapterOnePhase.ShopIntroduced && progress.Data.cloudVisionUnlocked,"Shop introduction unlock missing"); });
        Add(()=>{ Open("CraftTable"); Require(hub.DialogueText.Contains("微涩的抹茶粉") && hub.DialogueText.Contains("无法制作"),"Ingredient spelling/gate incorrect"); });
        Add(()=>Capture("05-missing-ingredient"));
        Add(()=>{ hub.Choose(0); Require(progress.FirstSectionComplete,"First section completion missing"); progress.Reload(); Require(progress.FirstSectionComplete && progress.Data.sketchbookPermission,"Save reload lost state"); Pass("Shop introduction, exact ingredient name, cloud-vision unlock, missing-ingredient gate and save reload"); });
        Add(()=>{ Open("ReturnToSchool"); hub.Choose(0); });
        WaitScene("School");
        Add(()=>{
            SceneIntegrity(); Require(hub.HasSketchbookPermission && progress.CleanCount==4,"School return lost state");
            Require(chapter.chapterTargets.Where(t=>t.action==ChapterOneAction.Clean).All(t=>!t.visual.activeSelf),"Cleaned items respawned during travel");
            Require(chapter.ToggleCloudVision() && chapter.visionClouds.All(g=>g.activeSelf),"Cloud vision failed");
            Near("NPC_Xiaoyu");
        });
        Add(()=>Capture("06-cloud-vision"));
        Add(()=>{ Require(chapter.ToggleCloudVision() && chapter.visionClouds.All(g=>!g.activeSelf),"Cloud vision failed to turn off"); Open("XiaoyuSketchbook"); Require(hub.DialogueText.Contains("反复修改"),"Investigation text still gated"); Close(); Pass("Return spawn, persistent cleanup/consent, cloud-vision on/off and investigation stage"); });
        Add(()=>{ Open("DessertEntranceLocker"); hub.Choose(0); });
        WaitScene("MochaStore");
        Add(()=>{ SceneIntegrity(); Open("ShopMatchaCloud"); Require(hub.ChoiceCount==1 && hub.DialogueText.Contains("制作台"),"Completed introduction replayed"); Close(); });
        Add(()=>{ Open("ReturnToSchool"); hub.Choose(0); });
        WaitScene("School");
        Add(()=>{ SceneIntegrity(); Pass("Second full round trip preserves progress without duplicate players/cameras or repeated intro"); chapter.PauseMenu(); hub.Choose(1); hub.Choose(1); Require(progress.FirstSectionComplete,"Cancel restart reset state"); Close(); });
        Add(()=>{ chapter.PauseMenu(); hub.Choose(1); hub.Choose(0); });
        WaitScene("School");
        Add(()=>{
            Require(progress.CleanCount==0 && progress.Data.phase==ChapterOnePhase.Cleaning && !progress.Data.sketchbookPermission,"New-game reset incomplete");
            Require(chapter.chapterTargets.Where(t=>t.action==ChapterOneAction.Clean).All(t=>t.visual.activeSelf),"New-game cleanup visuals not restored");
            Require(chapter.chapterTargets.Where(t=>t.action==ChapterOneAction.RecurringPaper).All(t=>!t.visual.activeSelf),"New-game recurring paper visible");
            Pass("Restart confirmation, cancellation, clean reset and visual restoration");
        });
    }
    static void InitializeResume()
    {
        Report="Library/ChapterOneResume.report.txt";
        File.WriteAllText(Report,"Native Unity cold-resume verification with isolated save\n");
        errors.Clear(); Application.logMessageReceived+=Log;
        Require(progress.SavePath.Contains("ChapterOneTests"),"Resume test attempted player save");
        steps=new Queue<Func<bool>>(); WaitScene("MochaStore");
        Add(()=>{
            SceneIntegrity(); Require(progress.FirstSectionComplete && progress.CleanCount==4 && progress.Data.sketchbookPermission,"Cold start did not restore saved state");
            Require(!progress.ResumePending,"Resume was not consumed");
            Require(Vector2.Distance(hub.player.transform.position,chapter.shopArrival)<.05f,"Cold resume did not use safe shop spawn");
            Pass("Fresh Play session from School automatically resumes saved MochaStore scene and all progress");
            Open("ReturnToSchool"); hub.Choose(0);
        });
        WaitScene("School");
        Add(()=>{
            SceneIntegrity(); Require(hub.HasSketchbookPermission && chapter.chapterTargets.Where(t=>t.action==ChapterOneAction.Clean).All(t=>!t.visual.activeSelf),"Cold-resumed cleanup/consent lost after return");
            Pass("Cold-resumed game returns to School with cleanup, permission, camera and spawn intact");
        });
    }
    static void Log(string message,string trace,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message); }
    static void Finish()
    {
        Application.logMessageReceived-=Log;
        if(hub!=null && hub.player!=null) { hub.CloseDialogue(); hub.player.UseKeyboardInput=true; }
        EditorApplication.isPlaying=false;
    }
}
