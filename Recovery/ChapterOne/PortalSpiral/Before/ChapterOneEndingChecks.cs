using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ChapterOneEndingChecks
{
    const string Request="Library/ChapterOneEndingChecks.request",Report="Library/ChapterOneEndingChecks.report.txt",Output="Recovery/ChapterOne/VisualPolish/";
    static bool started,background,preview;
    static double next,deadline;
    static Queue<Func<bool>> steps;
    static ChapterOneScene scene;
    static SchoolInteractionController ui;
    static ChapterOneProgress p;
    static readonly List<string> errors=new List<string>();
    static ChapterOneEndingChecks() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(File.Exists("Library/ChapterOneEndingManual.request") && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        { Manual(); return; }
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(!EditorApplication.isPlaying)
        {
            if(started) { File.Delete(Request); started=false; return; }
            if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
            return;
        }
        try
        {
            if(!started)
            {
                if(UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null) return;
                Bind(); Require(p.SavePath.Contains("ChapterOneTests"),"Must use isolated save");
                background=Application.runInBackground; Application.runInBackground=true;
                string mode=File.ReadAllText(Request).Trim(); preview=mode=="preview";
                Directory.CreateDirectory(Output); File.WriteAllText(Report,"Native Unity Step 9: "+mode+"\n");
                errors.Clear(); Application.logMessageReceived+=Log; steps=new Queue<Func<bool>>();
                if(preview) { Add(()=>LoadPacked()); Add(()=>{Near("NPC_Xiaoyu");ui.player.UseKeyboardInput=true;}); }
                else if(mode.StartsWith("resume")) Resume(mode);
                else if(mode=="visual") Visual();
                else if(mode=="matrix") Matrix();
                else if(mode=="persistence") Persistence();
                else Run();
                started=true; next=Time.unscaledTimeAsDouble+1; deadline=next+30; return;
            }
            if(Time.unscaledTimeAsDouble<next) return;
            next=Time.unscaledTimeAsDouble+.35; CheckText();
            if(steps.Count==0)
            {
                Require(errors.Count==0,string.Join("\n",errors)); Pass("No runtime exceptions; all dialogue fits");
                File.AppendAllText(Report,"ALL PASS\n"); Finish(); return;
            }
            if(steps.Peek()()) { steps.Dequeue(); deadline=Time.unscaledTimeAsDouble+30; }
            else Require(Time.unscaledTimeAsDouble<deadline,"Timed out waiting for scene or animation");
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL\n"+e+"\n");preview=false;Finish();Debug.LogException(e); }
    }
    static void Bind() {scene=UnityEngine.Object.FindObjectOfType<ChapterOneScene>();ui=scene.interaction;p=scene.Progress;ui.player.UseKeyboardInput=false;}
    static void Add(Action action)=>steps.Enqueue(()=>{action();return true;});
    static void Require(bool value,string message) {if(!value)throw new Exception(message);}
    static void Pass(string message)=>File.AppendAllText(Report,"PASS "+message+"\n");
    static SchoolInteractable Target(string id)=>ui.targets.Single(t=>t.interactionId==id);
    static void Close()=>ui.CloseDialogue();
    static void Near(string id)
    {
        Vector2 v=Target(id).ApproachPoint;
        if(scene.isSchool)v=scene.roomRoot.InverseTransformPoint(v);
        v.y-=.45f;scene.PlacePlayer(v);ui.player.SetMoveInput(Vector2.zero);Require(ui.CanReach(Target(id)),"Not reachable: "+id);
        var foot=ui.player.GetComponent<BoxCollider2D>();
        foreach(var hit in Physics2D.OverlapBoxAll(foot.bounds.center,foot.bounds.size*.95f,0))
            Require(hit==foot||hit.isTrigger||hit.transform.IsChildOf(ui.player.transform),"Blocked feet: "+id+" / "+hit.name);
    }
    static void Open(string id) {Near(id);Require(ui.TryInteract(Target(id)),"Interaction rejected: "+id);}
    static void Choose(int index) {if(!ui.DialogueOpen)scene.Ending.OpenSheila();ui.Choose(index);}
    static void Stage(ChapterOneEndingStage stage)=>Require(p.Data.endingStage==stage,"Expected "+stage+", got "+p.Data.endingStage);
    static void CheckText()
    {
        if(ui==null||!ui.DialogueOpen)return;
        var style=(GUIStyle)typeof(SchoolInteractionController).GetField("bodyStyle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        if(style!=null)Require(style.CalcHeight(new GUIContent(ui.DialogueText),850)<=124,"Overflow: "+ui.Speaker+" / "+ui.DialogueText);
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+name+".png");
    static void Snapshot(string name) {p.Arrive(scene.SceneId);File.Copy(p.SavePath,Output+name+"-save.json",true);}
    static void LoadPacked(MatchaSweetness sweet=MatchaSweetness.Low,MatchaFilling filling=MatchaFilling.Milk)
    {
        Close();var d=JsonUtility.FromJson<ChapterOneSave>(File.ReadAllText("Recovery/ChapterOne/Step08/packed-save.json"));
        d.scene=scene.SceneId;d.sweetness=sweet;d.filling=filling;
        Directory.CreateDirectory(Path.GetDirectoryName(p.SavePath));File.WriteAllText(p.SavePath,JsonUtility.ToJson(d));p.Reload();Stage(ChapterOneEndingStage.None);
        Require(p.HasDessert&&!p.DraftKept&&!p.ChapterComplete,"Legacy packed save migration");
    }
    static void WaitDraft()=>steps.Enqueue(()=>{
        if(p.Data.endingStage==ChapterOneEndingStage.DraftPending&&!scene.Ending.AnimationPlaying)scene.Ending.OpenSheila();
        return p.DraftKept;
    });
    static void WaitEnding()=>steps.Enqueue(()=>{
        if(!p.ChapterComplete&&!scene.Ending.AnimationPlaying)scene.Ending.StartChecklist();
        return p.ChapterComplete;
    });
    static void WaitScene(string id)=>steps.Enqueue(()=>{
        if(!SceneManager.GetActiveScene().name.Equals(id,StringComparison.OrdinalIgnoreCase)||UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null)return false;
        Bind();return p.Data.scene==id&&!scene.IsTransitioning;
    });
    static void CheckWeather()
    {
        if(!scene.CloudVisionActive)scene.ToggleCloudVision();
        Require(scene.visionClouds.All(c=>c.activeSelf),"A gray cloud was removed");
        var cloud=Target("NPC_Xiaoyu").GetComponentInChildren<ChapterOneWeather>(true);
        Require(cloud!=null,"Sheila weather absent");
        Require(cloud.transform.Find("Body").GetComponent<SpriteRenderer>().color.r>.9f,"Resolved cloud not white");
        Require(scene.visionClouds.Where(c=>c!=cloud.gameObject).All(c=>c.transform.Find("Body").GetComponent<SpriteRenderer>().color.r<.8f),"Other classmates changed");
        for(int i=0;i<6;i++)Require(cloud.transform.Find("Weather_"+i).GetComponent<SpriteRenderer>().enabled==false,"Rain/link state "+i);
    }
    static void Run()
    {
        Add(()=>{Require(!p.ShareDessert()&&!p.ReflectOnDraft()&&!p.KeepDraft()&&!p.StartFinalCleaning()&&!p.FinishChapter()&&!p.CompleteFinalPaper(0),"Early ending gate");LoadPacked();});
        Add(()=>{Open("NPC_Xiaoyu");Require(ui.Illustration!=null&&ui.ChoiceCount==2&&!ui.player.enabled,"Share invitation UI");});
        Add(()=>Capture("01-invitation"));
        Add(()=>{Choose(1);Require(p.HasDessert&&!ui.DialogueOpen&&ui.player.enabled,"Defer consumed dessert/locked movement");});
        Add(()=>{Open("NPC_Xiaoyu");Choose(0);Stage(ChapterOneEndingStage.Tasting);Require(!p.HasDessert&&!p.ShareDessert()&&p.Data.phase==ChapterOnePhase.ShopIntroduced&&p.Data.finalPaperMask==0,"Sharing auto-resolved/duplicated");Snapshot("tasting");});
        Add(()=>Capture("02-taste-response"));
        Add(()=>{Close();scene.DrawingStory.ToggleInventory();Require(ui.DialogueText.Contains("数量为 0"),"Consumed item still in inventory");});
        Add(()=>{Close();scene.Ending.OpenSheila();Choose(0);Stage(ChapterOneEndingStage.Reflection);});
        Add(()=>Capture("03-her-choice"));
        Add(()=>{Choose(1);Stage(ChapterOneEndingStage.DraftPending);Require(!p.DraftKept&&!p.CompleteFinalPaper(0),"Draft resolves before animation");});
        Add(()=>Capture("04-keeping-draft"));
        Add(()=>{Close();Snapshot("draft-pending");p.Reload();Stage(ChapterOneEndingStage.DraftPending);Require(!p.DraftKept,"Interrupted action resolved off-screen");scene.Travel("MochaStore");});WaitScene("MochaStore");
        Add(()=>{Require(scene.portalGlow.GetComponent<ChapterOnePortalEffect>()!=null&&scene.portalGlow.transform.Find("AnimatedGreenMagic").childCount==41,"Portal animation absent");Require(!p.DraftKept&&scene.Objective.Contains("返回学校"),"Pending draft while away");Open("CraftTable");Require(ui.DialogueText.Contains("已分享")||ui.Speaker.Contains("已分享"),"Stale crafting result");Close();scene.Travel("School");});WaitScene("School");
        Add(()=>Open("NPC_Xiaoyu"));WaitDraft();
        Add(()=>{Stage(ChapterOneEndingStage.CloudQuestion);Require(p.Data.phase==ChapterOnePhase.AnomalyResolved&&p.Data.finalPaperMask==0&&!p.KeepDraft(),"Animation checkpoint");Require(ui.DialogueText=="看，sheila 的云变白了！","Cloud question missing");Capture("05-cloud-remains");});
        Add(()=>{Choose(0);Require(ui.DialogueText=="可是……纸团好像不再出现了。","Heroine observation missing");});
        Add(()=>{Choose(0);Stage(ChapterOneEndingStage.FinalCleaning);CheckWeather();Pass("Sharing consumes once; taste leaves anomaly unchanged; interrupted draft replays; only completed action whitens sheila cloud and stops rain and links");});
        Add(()=>{Open("XiaoyuSketchbook");Require(ui.DialogueText.Contains("保留")&&p.Data.finalPaperMask==0,"Draft mistaken for trash");Close();});
        Add(()=>{Open("Recurring_2");Require(p.FinalPaperCount==1&&!Target("Recurring_2").enabled&&!ui.DialogueOpen&&ui.player.enabled,"Paper pickup should be silent and keep movement");Close();Snapshot("partial-cleaning");p.Reload();Require(p.FinalPaperCount==1&&!p.CompleteFinalPaper(2),"Duplicate cleanup reward");scene.Travel("MochaStore");});WaitScene("MochaStore");
        Add(()=>{Open("ShopMatchaCloud");Require(!ui.DialogueText.Contains("带给")&&ui.DialogueText.Contains("纸团"),"Stale shop story");Close();scene.Travel("School");});WaitScene("School");
        Add(()=>{Require(!Target("Recurring_2").enabled&&!Target("Recurring_2").chapterTarget.visual.activeSelf,"Clean paper respawn on travel");Open("Recurring_0");Require(p.FinalPaperCount==2&&!p.LocalAreaComplete&&!ui.DialogueOpen&&ui.player.enabled,"Second pickup should be silent");Close();});
        Add(()=>{Open("Recurring_1");Stage(ChapterOneEndingStage.Checklist);Require(p.LocalAreaComplete&&!p.ChapterComplete&&p.CleanCount==4,"Local/global completion conflated");Require(!ui.DialogueOpen&&ui.player.enabled&&scene.Objective.Contains("sheila")&&!scene.Objective.Contains("出口"),"Final pickup must only update HUD");Snapshot("checklist");});
        Add(()=>Capture("06-local-completion"));
        Add(()=>{scene.LeaveSchool();Require(!scene.Ending.AnimationPlaying&&ui.Speaker=="先等一下"&&ui.DialogueText.Contains("sheila"),"Exit bypassed sheila");Close();});
        Add(()=>{Open("NPC_Xiaoyu");Require(ui.Speaker=="sheila","Missing closing conversation");Choose(0);Require(ui.DialogueText.Contains("100%"),"Missing settlement after conversation");Choose(0);Require(scene.Ending.AnimationPlaying&&!p.ChapterComplete,"Checklist skipped");});
        Add(()=>Capture("07-crossed-checklist"));
        Add(()=>{Close();p.Reload();Stage(ChapterOneEndingStage.Checklist);Require(!p.ChapterComplete,"Interrupted ending marked complete");scene.Ending.StartChecklist();});
        WaitEnding();
        Add(()=>{Require(ui.DialogueText.Contains("同一条事项")&&ui.Speaker.StartsWith("第一章"),"Ending missing or chapter 2 started");Require(!p.FinishChapter()&&!p.HasDessert&&!p.BeginCrafting()&&!p.CollectPowder(),"Duplicate rewards after ending");Snapshot("complete");});
        Add(()=>Capture("08-restored-checklist"));
        Add(()=>{Choose(0);Require(ui.player.enabled&&!ui.DialogueOpen,"Free exploration not restored");CheckWeather();scene.Investigation.ShowJournal(6);Require(ui.DialogueText.Contains("云变白")&&!ui.DialogueText.Contains("治愈"),"Diary invents cure");});
        Add(()=>Capture("09-diary"));
        Add(()=>{scene.Investigation.ShowJournal(7);ui.Choose(0);Require(ui.Speaker.Contains("抹茶蛋糕")&&!ui.Speaker.Contains("未解锁"),"Starting cake locked");ui.Choose(1);Require(ui.Speaker.Contains("抹茶马卡龙")&&!ui.Speaker.Contains("未解锁")&&ui.DialogueText.Contains("低甜 · 牛奶"),"Macaron recipe not collected");});
        Add(()=>Capture("10-recipe-collection"));
        Add(()=>{ui.Choose(1);Require(ui.Speaker.Contains("未解锁")&&ui.DialogueText.Contains("Elaine"),"Elaine recipe prematurely unlocked");ui.Choose(1);Require(ui.Speaker.Contains("未解锁")&&ui.DialogueText.Contains("nate"),"Nate recipe prematurely unlocked");Close();scene.Travel("MochaStore");});WaitScene("MochaStore");
        Add(()=>{Require(p.ChapterComplete&&scene.ProgressLabel.Contains("已完成"),"Completed travel lost");Open("CraftTable");Require(ui.Speaker.Contains("已分享"),"Repeat tutorial after ending");Close();scene.Travel("School");});WaitScene("School");
        Add(()=>{Require(p.ChapterComplete&&scene.chapterTargets.Where(t=>t.action==ChapterOneAction.RecurringPaper).All(t=>!t.visual.activeSelf&&!t.interaction.enabled),"End cleanup lost");scene.LeaveSchool();Require(!ui.DialogueOpen&&!scene.Ending.AnimationPlaying,"Exit should not reopen completed ending");Close();Pass("All three original points cleaned manually; collection and factual diary; same checklist restored; free exploration and cross-scene state persist");});
    }
    static void Visual()
    {
        Add(()=>{LoadPacked();Near("NPC_Xiaoyu");if(!scene.CloudVisionActive)scene.ToggleCloudVision();});
        Add(()=>Capture("cloud-before"));
        Add(()=>{p.ShareDessert();p.ReflectOnDraft();p.PrepareToKeepDraft(0);p.KeepDraft();CheckWeather();});
        Add(()=>Capture("cloud-white"));
        Add(()=>scene.Travel("MochaStore"));WaitScene("MochaStore");
        Add(()=>{Near("ReturnToSchool");scene.PlacePlayer(new Vector2(-5.3f,1f));Require(scene.portalGlow.GetComponent<ChapterOnePortalEffect>()!=null,"Missing animated portal");});
        Add(()=>Capture("portal-01"));
        Add(()=>Capture("portal-02"));
        Add(()=>{var m=scene.portalGlow.transform.Find("AnimatedGreenMagic/Mote_0");Require(m!=null,"Mote missing");Pass("White cloud, other NPC clouds unchanged, portal rendered across animation frames");});
    }
    static void Matrix()
    {
        var responses=new HashSet<string>();
        foreach(MatchaSweetness sweet in Enum.GetValues(typeof(MatchaSweetness)))
        foreach(MatchaFilling filling in Enum.GetValues(typeof(MatchaFilling)))
        {
            Add(()=>{LoadPacked(sweet,filling);scene.Ending.OpenSheila();Choose(0);Stage(ChapterOneEndingStage.Tasting);Require(responses.Add(ui.DialogueText),"Identical flavour reaction");Require(p.Data.finalPaperMask==0&&!p.DraftKept,"Taste cleans world");Choose(0);Choose(((int)sweet+(int)filling)%2);});
            WaitDraft();
            Add(()=>{Choose(0);Choose(0);for(int i=0;i<3;i++)Require(p.CompleteFinalPaper(i),"Combination cleanup");scene.Ending.StartChecklist();});
            WaitEnding();
            Add(()=>{Require(p.ChapterComplete&&p.Data.sweetness==sweet&&p.Data.filling==filling&&p.LocalAreaComplete,"Combination fails story");Close();Pass("Flavour "+sweet+" + "+filling+" completes same chapter; both respectful replies supported");});
        }
    }
    static void Resume(string mode)
    {
        WaitScene("School");
        Add(()=>{
            Require(!p.HasDessert&&p.Data.dessertConsumed,"Cold reload restores consumed dessert");
            if(mode=="resume-tasting") {Stage(ChapterOneEndingStage.Tasting);scene.Ending.OpenSheila();Require(ui.DialogueText.Contains("奶香"),"Lost flavour feedback");}
            else if(mode=="resume-draft-pending") {Stage(ChapterOneEndingStage.DraftPending);Require(!p.DraftKept&&p.Data.finalPaperMask==0,"Draft skipped on cold reload");scene.Ending.OpenSheila();Require(scene.Ending.AnimationPlaying,"Pending action not replayed");Close();}
            else if(mode=="resume-partial-cleaning") {Stage(ChapterOneEndingStage.FinalCleaning);Require(p.FinalPaperCount==1&&!Target("Recurring_2").enabled&&Target("Recurring_0").enabled,"Partial cleanup lost");}
            else if(mode=="resume-checklist") {Stage(ChapterOneEndingStage.Checklist);Require(p.LocalAreaComplete&&!p.ChapterComplete,"Ending skipped on cold reload");scene.LeaveSchool();Require(ui.Speaker=="先等一下"&&!scene.Ending.AnimationPlaying,"Exit must not resume ending");Close();scene.Ending.OpenSheila();Require(ui.Speaker=="sheila","Cannot resume via sheila");}
            else {Stage(ChapterOneEndingStage.Complete);Require(p.FinalPaperCount==3&&!p.ShareDessert()&&!p.FinishChapter(),"Completed save duplicated state");scene.Investigation.ShowJournal(6);Require(ui.DialogueText.Contains("3 处"),"Diary lost");}
            Close();Pass("Cold startup restores "+mode);
        });
    }
    static void Persistence()
    {
        Add(()=>{LoadPacked();Require(!p.KeepDraft()&&!p.CompleteFinalPaper(0)&&!p.FinishChapter(),"Out-of-order API allowed");});
        Add(()=>{
            string path=p.SavePath,bad="Temp/ChapterOneTests/BlockedEndingSave";Directory.CreateDirectory(bad);
            typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,bad);
            try {Require(!p.ShareDessert()&&p.HasDessert&&!p.Data.dessertConsumed,"Failed save consumes dessert");}
            finally {typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,path);}
            Require(p.ShareDessert()&&p.ReflectOnDraft()&&p.PrepareToKeepDraft(0),"Retry after save error");
            typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,bad);
            try {Require(!p.KeepDraft()&&!p.DraftKept&&p.Data.phase==ChapterOnePhase.ShopIntroduced,"Failed save resolves anomaly");}
            finally {typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,path);}
            Require(p.KeepDraft()&&p.StartFinalCleaning(),"Resume failed draft save");
            typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,bad);
            try {Require(!p.CompleteFinalPaper(0)&&p.FinalPaperCount==0&&Target("Recurring_0").enabled,"Failed save removes paper");}
            finally {typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,path);}
            for(int i=0;i<3;i++)p.CompleteFinalPaper(i);
            typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,bad);
            try {Require(!p.FinishChapter()&&!p.ChapterComplete,"Failed save grants completion");}
            finally {typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,path);}
            Require(p.FinishChapter(),"Retry chapter save");Pass("Save failures roll back dessert, draft action, cleanup and completion");
        });
        Add(()=>{
            string valid=JsonUtility.ToJson(p.Data,true);File.WriteAllText(p.SavePath+".bak",valid);
            p.Data.dessertConsumed=false;File.WriteAllText(p.SavePath,JsonUtility.ToJson(p.Data));p.Reload();Require(p.ChapterComplete&&p.Data.dessertConsumed,"Invalid ending chain accepted");
            var corrupt=JsonUtility.FromJson<ChapterOneSave>(valid);corrupt.finalPaperMask=0;Require(!ChapterOneEnding.IsValidSave(corrupt),"Missing final trash accepted");
            corrupt=JsonUtility.FromJson<ChapterOneSave>(valid);corrupt.phase=ChapterOnePhase.ShopIntroduced;Require(!ChapterOneEnding.IsValidSave(corrupt),"Wrong phase accepted");
            LoadPacked();Require(p.Data.endingStage==ChapterOneEndingStage.None&&p.HasDessert,"Step 8 compatibility");
            p.NewGame();Require(!p.DessertPacked&&!p.DraftKept&&!p.LocalAreaComplete&&!p.ChapterComplete&&p.FinalPaperCount==0,"New game fails to reset");
            Pass("Corrupt save fallback, legacy Step 8 migration and new game reset");
        });
    }
    static void Log(string text,string stack,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
    // Manual keyboard checks may position the isolated test player, but never advance
    // dialogue/story state. All interactions after positioning use the real Game input.
    static void Manual()
    {
        const string path="Library/ChapterOneEndingManual.request";
        try
        {
            Require(EditorApplication.isPlaying,"Manual checks require Play mode");Bind();
            Require(p.SavePath.Contains("ChapterOneTests"),"Manual checks require isolated save");
            string command=File.ReadAllText(path).Trim();
            if(command.StartsWith("near ")) Near(command.Substring(5));
            else if(command.StartsWith("snapshot "))
            {
                string name=command.Substring(9);
                Require(name.All(c=>char.IsLetterOrDigit(c)||c=='-'),"Invalid snapshot name");
                File.Copy(p.SavePath,Output+name+"-save.json",true);
                File.WriteAllText(Output+name+"-state.txt","Scene="+scene.SceneId+"\nStage="+p.Data.endingStage+"\nDialogue="+ui.DialogueOpen+"\nSpeaker="+ui.Speaker+
                    "\nText="+ui.DialogueText+"\nPlayer="+ui.player.transform.position.ToString("F5")+"\nMovement="+ui.player.enabled+"\nIsolatedSave="+p.SavePath);
                ScreenCapture.CaptureScreenshot(Output+name+".png");
            }
            ui.player.UseKeyboardInput=true;
        }
        catch(Exception e) {File.WriteAllText(Output+"manual-error.txt",e.ToString());}
        finally {File.Delete(path);}
    }
    static void Finish()
    {
        Application.logMessageReceived-=Log;started=false;File.Delete(Request);
        if(!preview) {Application.runInBackground=background;EditorApplication.isPlaying=false;}
    }
}
