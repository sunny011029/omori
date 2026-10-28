using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ChapterOneConversationChecks
{
    const string Request="Library/ChapterOneConversationChecks.request",Report="Library/ChapterOneConversationChecks.report.txt",Output="Recovery/ChapterOne/Step07/";
    static bool started,background; static double next,deadline; static Queue<Func<bool>> steps;
    static ChapterOneScene scene; static SchoolInteractionController ui; static ChapterOneProgress p;
    static readonly List<string> errors=new List<string>();
    static ChapterOneConversationChecks() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(!EditorApplication.isPlaying)
        {
            if(started) { File.Delete(Request); started=false; return; }
            if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true; return;
        }
        try
        {
            if(!started)
            {
                if(UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null) return;
                Bind(); Require(p.SavePath.Contains("ChapterOneTests"),"Isolated save required");
                background=Application.runInBackground; Application.runInBackground=true;
                string mode=File.ReadAllText(Request).Trim();
                if(mode=="preview")
                {
                    Unlock(); p.FinishReassurance(); p.RecordDrawingQuestion(2); p.ViewOriginalDraft(); p.RevealPowder();
                    scene.ToggleCloudVision(); Near("MatchaPowderPickup"); ui.player.UseKeyboardInput=true; File.Delete(Request); return;
                }
                File.WriteAllText(Report,"Native Unity step 7 checks: "+mode+"\n");
                errors.Clear(); Application.logMessageReceived+=Log; steps=new Queue<Func<bool>>();
                if(mode.StartsWith("resume")) Resume(mode); else Initialize();
                started=true; next=Time.unscaledTimeAsDouble+1; deadline=next+30; return;
            }
            if(Time.unscaledTimeAsDouble<next) return;
            next=Time.unscaledTimeAsDouble+.4; CheckText();
            if(steps.Count==0) { Require(errors.Count==0,string.Join("\n",errors)); Pass("No runtime exceptions; all displayed dialogue fits"); File.AppendAllText(Report,"ALL PASS\n"); Finish(); return; }
            if(steps.Peek()()) { steps.Dequeue(); deadline=Time.unscaledTimeAsDouble+30; }
            else Require(Time.unscaledTimeAsDouble<deadline,"Scene load timeout");
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL\n"+e+"\n"); Finish(); Debug.LogException(e); }
    }
    static void Bind() { scene=UnityEngine.Object.FindObjectOfType<ChapterOneScene>(); ui=scene.interaction; p=scene.Progress; ui.player.UseKeyboardInput=false; }
    static void Require(bool good,string text) { if(!good) throw new Exception(text); }
    static void Pass(string text)=>File.AppendAllText(Report,"PASS "+text+"\n");
    static void Add(Action action)=>steps.Enqueue(()=>{action();return true;});
    static void Close()=>ui.CloseDialogue();
    static SchoolInteractable Target(string id)=>ui.targets.Single(t=>t.interactionId==id);
    static void Near(string id)
    {
        Vector2 v=Target(id).ApproachPoint; if(scene.isSchool) v=scene.roomRoot.InverseTransformPoint(v); v.y-=.45f;
        scene.PlacePlayer(v); ui.player.SetMoveInput(Vector2.zero); Require(ui.CanReach(Target(id)),"Not reachable: "+id);
        var foot=ui.player.GetComponent<BoxCollider2D>();
        foreach(var hit in Physics2D.OverlapBoxAll(foot.bounds.center,foot.bounds.size*.95f,0)) Require(hit==foot||hit.isTrigger||hit.transform.IsChildOf(ui.player.transform),"Blocked feet: "+id+" / "+hit.name);
    }
    static void Open(string id) { Near(id); Require(ui.TryInteract(Target(id)),"Interaction rejected: "+id); }
    static void Begin() { Open("NPC_Xiaoyu"); if(scene.CloudVisionActive) ui.Choose(1); ui.Choose(1); }
    static void CheckText()
    {
        if(ui==null||!ui.DialogueOpen) return;
        var style=(GUIStyle)typeof(SchoolInteractionController).GetField("bodyStyle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
        if(style!=null) Require(style.CalcHeight(new GUIContent(ui.DialogueText),850)<=124,"Overflow: "+ui.Speaker+" / "+ui.DialogueText);
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+name+".png");
    static void WaitScene(string name)=>steps.Enqueue(()=>{
        if(!SceneManager.GetActiveScene().name.Equals(name,StringComparison.OrdinalIgnoreCase)||UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null) return false;
        Bind(); return !scene.IsTransitioning&&p.Data.scene==name;
    });
    static void Unlock()
    {
        for(int i=0;i<4;i++) p.CompleteClean(i); p.TriggerAnomaly(); p.InspectAnomaly(); p.MeetCloud(); p.IntroduceShop(); p.InspectCraftTable();
        p.GrantSketchbookPermission(); foreach(ChapterOneClue clue in Enum.GetValues(typeof(ChapterOneClue))) p.RecordClue(clue);
    }
    static void Initialize()
    {
        Add(()=>{
            Require(!p.FinishReassurance()&&!p.CollectPowder(),"Story unlocked early");
            Require(Target("NPC_Xiaoyu").displayName=="sheila"&&Target("NPC_Classmate01").displayName=="Elaine"&&Target("NPC_Classmate02").displayName=="nate","NPC name casing");
            var art=Resources.Load<ChapterOneStoryArt>("ChapterOneStoryArt"); Require(art!=null&&art.matchaPowder!=null&&art.originalDraft!=null,"Missing story art");
            Require(art.cloudNeutral.bounds.size.x/art.cloudNeutral.bounds.size.y<1.4f,"Cloud stretched");
            Require(!Target("MatchaPowderPickup").enabled&&!scene.DrawingStory.PowderVisible,"Early powder visible");
            Unlock(); Pass("Story prerequisites, names, compact cloud art and dormant pickup");
        });
        Add(()=>{ Begin(); Require(ui.Speaker=="你","Opening reassurance missing"); ui.Choose(0); Close(); Require(!p.Data.reassuranceFinished,"Partial reassurance saved complete"); });
        Add(()=>Begin()); for(int i=0;i<4;i++) Add(()=>ui.Choose(0));
        Add(()=>{ Require(p.Data.reassuranceFinished&&!p.Data.powderRevealed&&ui.ChoiceCount==3,"Reassurance wrongly solves story"); ui.Choose(0); Require(ui.DialogueText=="窗户和屋顶，总觉得哪里不太对。","Approved answer one"); });
        Add(()=>{ ui.Choose(0); Require(p.Data.drawingQuestionsMask==1,"Question bit one"); ui.Choose(1); ui.Choose(1); Require(ui.DialogueText=="改了一遍又一遍，已经数不清了。","Approved answer two"); });
        Add(()=>{ ui.Choose(0); Require(p.Data.drawingQuestionsMask==3,"Question bit two"); ui.Choose(0); });
        Add(()=>{ Require(ui.DialogueText.Contains("一开始画的时候"),"Approved answer three"); ui.Choose(0); Require(p.Data.drawingQuestionsMask==7&&!p.Data.originalDraftViewed,"Questions grant draft prematurely"); ui.Choose(2); });
        Add(()=>{ Begin(); Require(ui.DialogueText.Contains("最早的那张"),"Resume draft offer"); ui.Choose(0); Require(ui.Illustration!=null&&!p.Data.originalDraftViewed,"Draft image/consent"); });
        Add(()=>Capture("01-original-draft"));
        Add(()=>{ Close(); Require(!p.Data.originalDraftViewed&&!p.Data.powderRevealed,"Esc rewards draft"); });
        Add(()=>{ Begin(); ui.Choose(0); ui.Choose(0); Require(p.Data.originalDraftViewed&&!p.Data.powderRevealed&&!p.Data.powderCollected,"Expression prematurely rewards"); });
        Add(()=>Capture("02-expression"));
        Add(()=>{ Close(); p.Reload(); Require(p.Data.originalDraftViewed&&!p.Data.powderRevealed,"Interrupted expression save"); });
        Add(()=>{ Begin(); Require(ui.DialogueText.Contains("到底在改什么"),"Expression resume"); ui.Choose(0); Require(p.Data.powderRevealed&&!p.Data.powderCollected,"Powder autocollected"); Close(); Require(!scene.DrawingStory.PowderVisible,"Normal vision reveals powder"); });
        Add(()=>{ scene.ToggleCloudVision(); Require(scene.DrawingStory.PowderVisible&&scene.visionClouds.All(c=>c.activeSelf),"Powder/cloud state"); Near("MatchaPowderPickup"); });
        Add(()=>Capture("03-powder-visible"));
        Add(()=>{ Open("MatchaPowderPickup"); ui.Choose(1); Require(!p.Data.powderCollected&&scene.DrawingStory.PowderVisible,"Cancelled pickup consumed"); Pass("All answers, draft consent, interruption/resume and explicit pickup gating"); });
        Add(()=>{ p.Arrive("School"); File.Copy(p.SavePath,Output+"unpicked-save.json",true); scene.Travel("MochaStore"); }); WaitScene("MochaStore");
        Add(()=>{ Require(!p.Data.powderCollected,"Travel autocollected"); scene.Travel("School"); }); WaitScene("School");
        Add(()=>{ scene.ToggleCloudVision(); Require(scene.DrawingStory.PowderVisible,"Uncollected powder lost across scenes"); Open("MatchaPowderPickup"); ui.Choose(0); Require(p.Data.powderCollected&&!scene.DrawingStory.PowderVisible&&!Target("MatchaPowderPickup").enabled,"Pickup not consumed"); Require(!p.CollectPowder(),"Duplicate reward"); Close(); });
        Add(()=>{ scene.DrawingStory.ToggleInventory(); Require(ui.Speaker.Contains("× 1")&&ui.Illustration!=null&&!ui.player.enabled,"Inventory or movement lock"); });
        Add(()=>Capture("04-inventory"));
        Add(()=>{ scene.DrawingStory.ToggleInventory(); Require(!ui.DialogueOpen&&ui.player.enabled,"Inventory close restores movement"); });
        Add(()=>{ Open("Recurring_0"); Require(ui.Speaker=="无法清理","Ingredient cleared recurring trash"); Close(); Require(p.Data.finalPaperMask==0&&p.Data.phase==ChapterOnePhase.ShopIntroduced,"Future chapter advanced"); scene.Travel("MochaStore"); }); WaitScene("MochaStore");
        Add(()=>{ Open("ShopMatchaCloud"); Require(ui.DialogueText.Contains("带回来了"),"Shop ignores ingredient"); });
        Add(()=>Capture("05-compact-shop-cloud"));
        Add(()=>Close());
        Add(()=>{ Open("CraftTable"); Require(ui.DialogueText.Contains("已经备好")&&!ui.DialogueText.Contains("还缺少"),"Craft inventory hint stale"); ui.Choose(0); scene.DrawingStory.ShowInventory(); });
        Add(()=>Capture("06-shop-inventory"));
        Add(()=>{ Close(); p.Arrive("MochaStore"); File.Copy(p.SavePath,Output+"collected-save.json",true); Pass("Reachable pickup, unique inventory item, round-trip persistence, gray clouds/trash retained, shop recipe preview"); });
    }
    static void Resume(string mode)
    {
        bool collected=mode=="resume-collected"; WaitScene(collected?"MochaStore":"School");
        Add(()=>{ Require(p.Data.powderRevealed&&p.Data.powderCollected==collected,"Cold resume flags"); Require(p.Data.drawingQuestionsMask==7,"Cold resume conversation");
            if(!collected) { scene.ToggleCloudVision(); Require(scene.DrawingStory.PowderVisible,"Cold resume pickup lost"); }
            scene.DrawingStory.ToggleInventory(); Require(ui.DialogueText.Contains(collected?"凝结":"还没有"),"Cold resume inventory"); Close(); Pass("Cold reload restores conversation, pending pickup and inventory correctly"); });
        if(collected)
        {
            Add(()=>{ p.NewGame(); Require(!p.Data.reassuranceFinished&&!p.Data.powderCollected&&p.Data.drawingQuestionsMask==0,"New game retains reward"); Unlock(); p.FinishReassurance(); p.RecordDrawingQuestion(2); Require(p.Data.drawingQuestionsMask==4&&p.ViewOriginalDraft()&&p.RevealPowder(),"Direct third question blocked by optional questions"); Pass("New game resets story; third question alone can advance without mandatory first two"); });
            Add(()=>{ string old="{\"version\":1,\"cleanedMask\":15,\"phase\":5,\"scene\":\"School\",\"sketchbookPermission\":true,\"cloudVisionUnlocked\":true,\"investigationMask\":15}";
                File.WriteAllText(p.SavePath,old); p.Reload(); Require(!p.Data.reassuranceFinished&&!p.Data.powderRevealed&&!p.Data.powderCollected,"Old save compatibility");
                File.WriteAllText(p.SavePath+".bak",old); File.WriteAllText(p.SavePath,old.Replace("\"investigationMask\":15","\"investigationMask\":15,\"powderCollected\":true")); p.Reload(); Require(!p.Data.powderCollected,"Invalid reward chain accepted"); Pass("Older saves migrate safely; invalid reward chain falls back to valid backup"); });
        }
    }
    static void Log(string text,string trace,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(text); }
    static void Finish() { Application.logMessageReceived-=Log; Application.runInBackground=background; started=false; File.Delete(Request); EditorApplication.isPlaying=false; }
}
