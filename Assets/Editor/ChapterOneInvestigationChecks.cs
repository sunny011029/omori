using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Integration checks use scene interactions and a disposable save, never the player's file.
[InitializeOnLoad]
public static class ChapterOneInvestigationChecks
{
    const string Request="Library/ChapterOneInvestigationChecks.request";
    const string Report="Library/ChapterOneInvestigationChecks.report.txt";
    const string Output="Recovery/ChapterOne/Step06";
    static bool started;
    static bool previousBackground;
    static Queue<Func<bool>> steps;
    static double next,deadline;
    static ChapterOneScene scene;
    static SchoolInteractionController ui;
    static ChapterOneProgress progress;
    static readonly List<string> errors=new List<string>();
    static ChapterOneInvestigationChecks() { EditorApplication.update+=Poll; }
    [MenuItem("Tools/School/Verify Chapter One Investigation")]
    static void RequestCheck() => File.WriteAllText(Request,"run");
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
                Bind(); Require(progress.SavePath.Contains("ChapterOneTests"),"Player save must not be used");
                previousBackground=Application.runInBackground; Application.runInBackground=true;
                string mode=File.ReadAllText(Request).Trim();
                if(mode=="preview")
                {
                    Unlock(); progress.GrantSketchbookPermission(); Near("NPC_Xiaoyu");
                    ui.player.UseKeyboardInput=true; File.Delete(Request); return;
                }
                if(mode=="resume") InitializeResume(); else if(mode=="weather") InitializeWeather(); else Initialize();
                started=true; next=Time.unscaledTimeAsDouble+.6; deadline=next+30; return;
            }
            if(Time.unscaledTimeAsDouble<next) return;
            next=Time.unscaledTimeAsDouble+.35;
            CheckVisibleText();
            if(steps.Count==0)
            {
                Require(errors.Count==0,string.Join("\n",errors)); Pass("No runtime errors/exceptions");
                File.AppendAllText(Report,"ALL PASS\n"); Finish(); return;
            }
            if(steps.Peek()()) { steps.Dequeue(); deadline=Time.unscaledTimeAsDouble+30; }
            else Require(Time.unscaledTimeAsDouble<deadline,"Timed out waiting for scene");
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL\n"+e+"\n"); Finish(); Debug.LogException(e); }
    }
    static void Bind()
    {
        scene=UnityEngine.Object.FindObjectOfType<ChapterOneScene>(); ui=scene.interaction; progress=scene.Progress;
        ui.player.UseKeyboardInput=false; ui.player.GetComponent<Rigidbody2D>().interpolation=RigidbodyInterpolation2D.None;
    }
    static void Require(bool result,string message) { if(!result) throw new Exception(message); }
    static void Pass(string text) => File.AppendAllText(Report,"PASS "+text+"\n");
    static void Add(Action action) => steps.Enqueue(()=>{action();return true;});
    static SchoolInteractable Target(string id) => ui.targets.Single(t=>t.interactionId==id);
    static void Near(string id)
    {
        Vector2 p=Target(id).ApproachPoint;
        if(scene.isSchool) { p=scene.roomRoot.InverseTransformPoint(p); p.y=p.y>=4?Mathf.Max(4.25f,p.y-.45f):Mathf.Max(.35f,p.y-.45f); }
        else p.y-=.5f;
        scene.PlacePlayer(p); ui.player.SetMoveInput(Vector2.zero);
        Require(ui.CanReach(Target(id)),"Unreachable interaction "+id);
        var foot=ui.player.GetComponent<BoxCollider2D>();
        foreach(var hit in Physics2D.OverlapBoxAll(foot.bounds.center,foot.bounds.size*.95f,0))
            Require(hit==foot || hit.isTrigger || hit.transform.IsChildOf(ui.player.transform),"Blocked feet near "+id+": "+hit.name);
    }
    static void Open(string id) { Near(id); Require(ui.TryInteract(Target(id)),"Interaction failed: "+id); }
    static void Close() => ui.CloseDialogue();
    static void Capture(string name) => ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");
    static void CheckVisibleText()
    {
        if(ui==null || !ui.DialogueOpen) return;
        var style=(GUIStyle)typeof(SchoolInteractionController).GetField("bodyStyle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
        if(style==null) return;
        float height=style.CalcHeight(new GUIContent(ui.DialogueText),850);
        Require(height<=124,"Dialogue overflows its panel: "+ui.Speaker+" needs "+height+" pixels");
    }
    static void WaitScene(string id) => steps.Enqueue(()=>
    {
        if(!SceneManager.GetActiveScene().name.Equals(id,StringComparison.OrdinalIgnoreCase) || UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null) return false;
        Bind(); return progress.Data.scene==id && !scene.IsTransitioning;
    });
    static void Unlock()
    {
        for(int i=0;i<4;i++) progress.CompleteClean(i);
        progress.TriggerAnomaly(); progress.InspectAnomaly(); progress.MeetCloud(); progress.IntroduceShop(); progress.InspectCraftTable(); scene.Refresh();
    }
    static void Initialize()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Report,"Native Unity investigation integration checks\n");
        errors.Clear(); Application.logMessageReceived+=Log; steps=new Queue<Func<bool>>();
        Add(()=>{
            Require(!progress.InvestigationUnlocked && !progress.RecordClue(ChapterOneClue.PaperTraces),"Clues unlocked before shop intro");
            scene.Investigation.ToggleJournal(); Require(!ui.DialogueOpen,"Journal unlocked early");
            Open("ArtCompetitionNotice"); Close(); Require(progress.InvestigationCount==0,"Daily notice prematurely recorded");
            Unlock(); Require(progress.InvestigationCount==0 && !progress.Data.sketchbookPermission,"Unlock polluted investigation");
            Require(!progress.RecordClue(ChapterOneClue.Sketchbook),"Private sketchbook recorded without consent");
            Pass("Investigation gated by shop introduction; private sketchbook requires permission");
        });
        Add(()=>{ Open("Recurring_2"); Require(ui.Speaker=="无法清理" && ui.DialogueText.Contains("云视"),"Paper hint regressed to tool search"); Close(); Require(progress.InvestigationCount==0,"Normal vision revealed magic clue"); });
        Add(()=>{ Open("XiaoyuSketchbook"); Require(ui.DialogueText.Contains("先问问她"),"Private sketchbook gate missing"); Close(); });
        Add(()=>{ Open("ArtCompetitionNotice"); Require(ui.DialogueText.Contains("家") && ui.DialogueText.Contains("日期"),"Notice theme/deadline absent"); Close(); Require(progress.InvestigationCount==0,"Cancelled reading recorded clue"); });
        Add(()=>{ Open("ArtCompetitionNotice"); ui.Choose(0); Require(progress.InvestigationCount==1,"Public notice cannot be recorded first"); Pass("Free exploration: public notice can be first, cancelled reading gives no clue"); });
        Add(()=>{
            Require(scene.ToggleCloudVision(),"Cannot enable vision");
            Open("NPC_Xiaoyu"); Require(ui.ChoiceCount==3,"Cloud menu must preserve conversation"); ui.Choose(0);
            Require(ui.DialogueText.Contains("没有翻开"),"Desk observation violated privacy");
        });
        Add(()=>Capture("01-desk-cloud-link"));
        Add(()=>{ ui.Choose(0); Require(progress.HasClue(ChapterOneClue.DeskCloud) && !progress.Data.sketchbookPermission,"Desk clue should not grant book permission"); });
        Add(()=>{ Open("XiaoyuSketchbook"); ui.Choose(1); Require(ui.DialogueText.Contains("先问问她"),"Cloud vision bypassed privacy"); Close(); });
        Add(()=>{ Open("NPC_Xiaoyu"); ui.Choose(1); ui.Choose(0); Require(progress.Data.sketchbookPermission,"Consent path missing in cloud vision"); Close(); Require(scene.ToggleCloudVision(),"Cannot return to ordinary vision"); });
        Add(()=>{ Open("XiaoyuSketchbook"); Require(ui.DialogueText.Contains("反复修改"),"Book page 1 wrong"); ui.Choose(1); Require(progress.InvestigationCount==2,"Partial book reading granted clue"); });
        Add(()=>{ Open("XiaoyuSketchbook"); ui.Choose(0); Require(ui.Speaker.Contains("2 / 2"),"Book page turn failed"); });
        Add(()=>Capture("02-authorized-sketchbook"));
        Add(()=>{ ui.Choose(0); Require(ui.Speaker.Contains("1 / 2"),"Return page failed"); ui.Choose(0); ui.Choose(1); Require(progress.InvestigationCount==3 && progress.HasClue(ChapterOneClue.Sketchbook),"Complete reading not recorded"); Pass("Consent survives mode change; two-page reading and explicit confirmation required"); });
        Add(()=>{ Require(scene.ToggleCloudVision(),"Vision toggle failed"); Open("Recurring_0"); Require(ui.Speaker=="无法清理","Recurring paper label changed"); ui.Choose(0); Require(ui.DialogueText.Contains("同一幅作品"),"Paper source evidence missing"); });
        Add(()=>Capture("03-paper-evidence"));
        Add(()=>{
            ui.Choose(0); Require(progress.InvestigationComplete && progress.Data.backgroundNotesMask==0,"Optional classmates required for main progress");
            Require(ui.Speaker=="调查记录已整理","Completion guidance missing");
            Require(progress.Data.phase==ChapterOnePhase.ShopIntroduced && progress.Data.finalPaperMask==0,"Step 6 advanced later chapter");
            Require(scene.chapterTargets.Count(t=>t.action==ChapterOneAction.RecurringPaper && t.visual.activeSelf)==3,"Investigation removed papers");
            Pass("Four clues finish in alternate order without classmates; no ingredient, ending, or paper cleanup awarded");
        });
        Add(()=>Capture("04-investigation-ready"));
        Add(()=>{ ui.Choose(0); Require(scene.Investigation.JournalOpen && !ui.player.enabled,"Journal failed to lock movement"); });
        Add(()=>Capture("05-journal-overview"));
        Add(()=>{
            ui.Choose(1); Require(ui.DialogueText.Contains("同一幅作品"),"Journal forgot evidence");
            ui.Choose(0); ui.Choose(0); Require(ui.Speaker.Contains("同学近况"),"Journal circular paging failed");
            scene.Investigation.ToggleJournal(); Require(!ui.DialogueOpen && ui.player.enabled && !scene.Investigation.JournalOpen,"Journal close movement restore");
            Pass("Journal overview, evidence detail, optional notes, paging, J toggle and movement lock");
        });
        Add(()=>{ Open("Recurring_1"); ui.Choose(0); ui.Choose(0); Require(progress.InvestigationCount==4 && !ui.DialogueOpen,"Duplicate clue rewarded or repeated completion dialogue"); });
        for(int i=0;i<2;i++)
        {
            int index=i; string id="NPC_Classmate0"+(i+1);
            Add(()=>{ Open(id); ui.Choose(1); Require(ui.DialogueText.Contains(index==0?"书包":"这一页"),"Classmate daily dialogue changed into another quest"); Close(); });
            Add(()=>{ Open(id); ui.Choose(0); });
            Add(()=>Capture("06-background-"+index));
            Add(()=>{ ui.Choose(0); Require(progress.InvestigationCount==4 && (progress.Data.backgroundNotesMask&(1<<index))!=0,"Optional note contaminated main count"); });
        }
        Add(()=>{
            for(int i=0;i<8;i++) Require(scene.ToggleCloudVision(),"Repeated vision toggle failed");
            Require(scene.visionClouds.All(c=>c.GetComponents<ChapterOneWeather>().Length==1),"Duplicate weather components");
            int[] marks=scene.visionClouds.Select(c=>c.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("Weather_"))).OrderBy(n=>n).ToArray();
            Require(marks.SequenceEqual(new[]{3,3,6}),"Weather accents missing or duplicated");
            progress.Reload(); Require(progress.InvestigationComplete && progress.Data.backgroundNotesMask==3,"Save reload lost clues/notes");
            Pass("Optional daily observations, repeated Q toggling, no duplicate accents, persisted clues/notes");
        });
        Add(()=>{ Open("DessertEntranceLocker"); ui.Choose(0); });
        WaitScene("MochaStore");
        Add(()=>{
            Require(progress.InvestigationComplete && progress.Data.backgroundNotesMask==3,"Travel lost investigation");
            scene.Investigation.ToggleJournal(); Require(ui.DialogueText.Contains("已记录"),"Journal unavailable in shop"); Close();
        });
        Add(()=>{ Open("ShopMatchaCloud"); Require(ui.DialogueText.Contains("慢慢听她说"),"Shop repeats first-section hint"); Close(); });
        Add(()=>{ Open("CraftTable"); Require(ui.DialogueText.Contains("微涩的抹茶粉") && ui.DialogueText.Contains("无法制作"),"Premature ingredient/crafting unlock"); ui.Choose(0); });
        Add(()=>{ Open("ReturnToSchool"); ui.Choose(0); });
        WaitScene("School");
        Add(()=>{
            Require(progress.InvestigationComplete && !scene.CloudVisionActive,"Return should preserve clues, keep vision off initially");
            Require(UnityEngine.Object.FindObjectsOfType<TopDownCharacterController>().Length==1 && UnityEngine.Object.FindObjectsOfType<ChapterOneProgress>().Length==1,"Duplicate player/progress after travel");
            Require(ui.worldCamera.GetComponent<SchoolCameraFootprint>().ContainsViewport(ui.worldCamera),"Camera escaped school");
            Pass("Shop round trip, persistent journal, safe return camera, crafting still gated");
            CheckSaveCompatibility();
        });
        // Leave an isolated completed save in MochaStore for a separate cold-start check.
        Add(()=>{ Open("DessertEntranceLocker"); ui.Choose(0); });
        WaitScene("MochaStore");
        Add(()=>{ Require(progress.Data.scene=="MochaStore" && progress.InvestigationComplete,"Cold-resume fixture incomplete"); });
    }
    static void CheckSaveCompatibility()
    {
        string current=JsonUtility.ToJson(progress.Data,true);
        File.WriteAllText(progress.SavePath,"{\"version\":1,\"cleanedMask\":15,\"finalPaperMask\":0,\"phase\":5,\"sketchbookPermission\":true,\"cloudVisionUnlocked\":true,\"inspectedCraftTable\":true,\"scene\":\"School\"}");
        progress.Reload(); Require(progress.FirstSectionComplete && progress.InvestigationCount==0 && progress.Data.sketchbookPermission,"Legacy save migration failed");
        File.WriteAllText(progress.SavePath+".bak",current); File.WriteAllText(progress.SavePath,"{\"version\":1,\"investigationMask\":16}");
        progress.Reload(); Require(progress.InvestigationComplete && progress.Data.backgroundNotesMask==3,"Invalid clue mask failed backup fallback");
        progress.Commit();
        Pass("Legacy first-section save retains progress/consent; missing clue fields default correctly; corrupt save uses backup");
    }
    static void InitializeResume()
    {
        File.WriteAllText(Report,"Native Unity investigation cold-resume checks\n"); errors.Clear(); Application.logMessageReceived+=Log;
        steps=new Queue<Func<bool>>(); WaitScene("MochaStore");
        Add(()=>{
            Require(progress.InvestigationComplete && progress.Data.backgroundNotesMask==3 && progress.Data.sketchbookPermission,"Fresh session lost investigation");
            Require(Vector2.Distance(ui.player.transform.position,scene.shopArrival)<.05f,"Cold resume unsafe spawn");
            scene.Investigation.ToggleJournal(); Require(ui.DialogueText.Contains("已记录"),"Cold-resumed journal absent"); Close();
            Pass("Fresh Play starts in saved shop and restores all four clues, two optional notes and permission");
        });
        Add(()=>{ Open("ReturnToSchool"); ui.Choose(0); });
        WaitScene("School");
        Add(()=>{
            Require(progress.InvestigationComplete && progress.Data.phase==ChapterOnePhase.ShopIntroduced,"Fresh return changed stage");
            Pass("Cold-resumed player can return to School with investigation ready for the next step");
        });
    }
    static void InitializeWeather()
    {
        File.WriteAllText(Report,"Native Unity weather visual checks\n"); errors.Clear(); Application.logMessageReceived+=Log;
        steps=new Queue<Func<bool>>();
        Add(()=>{ Unlock(); scene.PlacePlayer(new Vector2(8,.5f)); scene.ToggleCloudVision(); });
        for(int i=0;i<3;i++)
        {
            string id=new[]{"NPC_Xiaoyu","NPC_Classmate01","NPC_Classmate02"}[i];
            Add(()=>CaptureWeather(id));
        }
        Add(()=>{ scene.Investigation.ShowJournal(); });
        for(int i=0;i<6;i++) Add(()=>{ CheckVisibleText(); ui.Choose(1); });
        Add(()=>{ Close(); Pass("All journal pages fit the panel, including undiscovered hints"); });
    }
    static void CaptureWeather(string id)
    {
        var target=Target(id);
        var go=new GameObject("VerificationCamera"); var camera=go.AddComponent<Camera>(); camera.enabled=false;
        go.AddComponent<UniversalAdditionalCameraData>();
        camera.orthographic=true; camera.orthographicSize=1.1f; camera.aspect=.8f;
        camera.transform.position=target.transform.position+new Vector3(0,.95f,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.8f,.82f,.85f);
        var rt=new RenderTexture(480,600,24); var texture=new Texture2D(480,600,TextureFormat.RGB24,false); var old=RenderTexture.active;
        try
        {
            // Capture the short lightning accent deterministically without changing its timing.
            var weather=target.GetComponentInChildren<ChapterOneWeather>();
            Require(weather!=null,"Missing weather on "+id);
            weather.SendMessage("OnEnable"); weather.SendMessage("Update");
            var marks=weather.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name.StartsWith("Weather_")).ToArray();
            Require(marks.All(r=>r.enabled && r.color.a>0 && r.bounds.size.x>.001f),"Invisible weather marks on "+id);
            foreach(var r in marks) File.AppendAllText(Report,id+" "+r.name+" bounds="+r.bounds+" color="+r.color+" layer="+r.sortingLayerID+" order="+r.sortingOrder+"\n");
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,480,600),0,0); texture.Apply();
            File.WriteAllBytes(Output+"/weather-"+id+".png",texture.EncodeToPNG()); Pass("Rendered weather preview: "+id);
        }
        finally { RenderTexture.active=old; camera.targetTexture=null; UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt); }
    }
    static void Log(string message,string trace,LogType type) { if(type==LogType.Error || type==LogType.Assert || type==LogType.Exception) errors.Add(message); }
    static void Finish()
    {
        Application.logMessageReceived-=Log;
        Application.runInBackground=previousBackground;
        if(ui!=null) { Close(); ui.player.UseKeyboardInput=true; }
        EditorApplication.isPlaying=false;
    }
}
