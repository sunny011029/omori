using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ChapterOneCraftingChecks
{
    const string Request="Library/ChapterOneCraftingChecks.request", Report="Library/ChapterOneCraftingChecks.report.txt", Output="Recovery/ChapterOne/Step08/";
    static ChapterOneScene scene; static SchoolInteractionController ui; static ChapterOneProgress p;
    static bool started,background,preview; static double next,deadline; static Queue<Func<bool>> steps;
    static readonly List<string> errors=new List<string>();
    static ChapterOneCraftingChecks() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating) return;
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
                Bind(); Require(p.SavePath.Contains("ChapterOneTests"),"Must use isolated save");
                background=Application.runInBackground; Application.runInBackground=true;
                string mode=File.ReadAllText(Request).Trim(); preview=mode=="preview";
                File.WriteAllText(Report,"Native Unity Step 8: "+mode+"\n");
                errors.Clear(); Application.logMessageReceived+=Log; steps=new Queue<Func<bool>>();
                if(preview) { Add(Unlock); Add(()=>scene.Travel("MochaStore")); WaitScene("MochaStore"); Add(()=>{Near("CraftTable");ui.player.UseKeyboardInput=true;}); }
                else if(mode.StartsWith("resume")) Resume(mode);
                else if(mode=="matrix") Matrix();
                else Run();
                started=true; next=Time.unscaledTimeAsDouble+1; deadline=next+30; return;
            }
            if(Time.unscaledTimeAsDouble<next) return;
            next=Time.unscaledTimeAsDouble+.35; CheckText();
            if(steps.Count==0)
            {
                Require(errors.Count==0,string.Join("\n",errors)); Pass("No runtime errors; dialogue fits");
                File.AppendAllText(Report,"ALL PASS\n"); Finish(); return;
            }
            if(steps.Peek()()) { steps.Dequeue(); deadline=Time.unscaledTimeAsDouble+30; }
            else Require(Time.unscaledTimeAsDouble<deadline,"Timed out waiting for scene or baking");
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL\n"+e+"\n"); preview=false; Finish(); Debug.LogException(e); }
    }
    static void Bind() { scene=UnityEngine.Object.FindObjectOfType<ChapterOneScene>();ui=scene.interaction;p=scene.Progress;ui.player.UseKeyboardInput=false; }
    static void Require(bool good,string message) { if(!good)throw new Exception(message); }
    static void Pass(string message)=>File.AppendAllText(Report,"PASS "+message+"\n");
    static void Add(Action action)=>steps.Enqueue(()=>{action();return true;});
    static void Close()=>ui.CloseDialogue();
    // Native focus loss closes the panel by design; resume it before the next simulated selection.
    static void Choose(int index) { if(!ui.DialogueOpen) scene.Crafting.OpenTable(); ui.Choose(index); }
    static void CheckText()
    {
        if(ui==null||!ui.DialogueOpen)return;
        var style=(GUIStyle)typeof(SchoolInteractionController).GetField("bodyStyle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        if(style!=null)Require(style.CalcHeight(new GUIContent(ui.DialogueText),850)<=124,"Overflow: "+ui.Speaker+" / "+ui.DialogueText);
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Output+name+".png");
    static void Near(string id)
    {
        var target=ui.targets.Single(t=>t.interactionId==id); Vector2 v=target.ApproachPoint;
        if(scene.isSchool)v=scene.roomRoot.InverseTransformPoint(v);v.y-=.45f;scene.PlacePlayer(v);ui.player.SetMoveInput(Vector2.zero);
        Require(ui.CanReach(target),"Unreachable: "+id);
        var foot=ui.player.GetComponent<BoxCollider2D>();
        foreach(var hit in Physics2D.OverlapBoxAll(foot.bounds.center,foot.bounds.size*.95f,0))
            Require(hit==foot||hit.isTrigger||hit.transform.IsChildOf(ui.player.transform),"Blocked feet: "+hit.name);
    }
    static void Open(string id) {Near(id);Require(ui.TryInteract(ui.targets.Single(t=>t.interactionId==id)),"Interaction rejected: "+id);}
    static void WaitScene(string name)=>steps.Enqueue(()=>{
        if(!SceneManager.GetActiveScene().name.Equals(name,StringComparison.OrdinalIgnoreCase)||UnityEngine.Object.FindObjectOfType<ChapterOneScene>()==null)return false;
        Bind();return !scene.IsTransitioning&&p.Data.scene==name;
    });
    static void WaitBake()=>steps.Enqueue(()=>p.Data.craftStage==MatchaCraftStage.ReadyToPack);
    static void Unlock()
    {
        for(int i=0;i<4;i++)p.CompleteClean(i);p.TriggerAnomaly();p.InspectAnomaly();p.MeetCloud();p.IntroduceShop();p.InspectCraftTable();
        p.GrantSketchbookPermission();foreach(ChapterOneClue clue in Enum.GetValues(typeof(ChapterOneClue)))p.RecordClue(clue);
        p.FinishReassurance();p.RecordDrawingQuestion(2);p.ViewOriginalDraft();p.RevealPowder();p.CollectPowder();
    }
    static void Snapshot(string name) {p.Arrive(scene.SceneId);File.Copy(p.SavePath,Output+name+"-save.json",true);}
    static void Stage(MatchaCraftStage expected)=>Require(p.Data.craftStage==expected,"Expected "+expected+", got "+p.Data.craftStage);
    static void Run()
    {
        Add(()=>{
            Require(!p.BeginCrafting()&&!p.AddCraftPowder()&&!p.PackDessert(),"Early craft granted");
            for(int i=0;i<4;i++)p.CompleteClean(i);p.TriggerAnomaly();p.InspectAnomaly();p.MeetCloud();p.IntroduceShop();scene.Travel("MochaStore");
        });WaitScene("MochaStore");
        Add(()=>{Open("CraftTable");Require(ui.DialogueText.Contains("缺少")&&!p.BeginCrafting(),"Missing powder ignored");Close();Unlock();Pass("Crafting requires acquired powder; existing table reachable");});
        Add(()=>{Open("CraftTable");Require(ui.DialogueText.Contains("已经备好")&&ui.Illustration!=null,"Recipe preview missing");Choose(1);Stage(MatchaCraftStage.None);});
        Add(()=>{Open("CraftTable");Choose(0);Stage(MatchaCraftStage.SelectSweetness);Require(!ui.player.enabled,"Movement not locked");});
        Add(()=>Capture("01-sweetness"));
        Add(()=>{Choose(2);Stage(MatchaCraftStage.SelectFilling);Snapshot("selection");Close();p.Reload();Stage(MatchaCraftStage.SelectFilling);});
        Add(()=>{Open("CraftTable");Require(ui.DialogueText.Contains("高甜"),"Selected sweetness lost");Choose(1);Stage(MatchaCraftStage.AddPowder);Choose(1);Stage(MatchaCraftStage.SelectSweetness);Choose(0);Choose(0);});
        Add(()=>Capture("02-recipe-confirmation"));
        Add(()=>{Require(p.HasLoosePowder&&!p.PackDessert()&&!p.StirDessert()&&!p.BeginBaking(),"Out-of-order actions consume");Choose(0);Stage(MatchaCraftStage.Stirring);Require(p.PowderInBatch&&!p.HasLoosePowder&&!p.AddCraftPowder()&&!p.ReviseRecipe(),"Powder reservation unsafe");});
        Add(()=>{Choose(0);Require(p.Data.stirCount==1,"First stir: "+p.SaveError);});
        Add(()=>Capture("03-stirring"));
        Add(()=>{Snapshot("stirring");Close();p.Reload();Require(p.Data.stirCount==1&&p.PowderInBatch,"Interrupted stir lost");scene.DrawingStory.ToggleInventory();Require(ui.DialogueText.Contains("留在店内")&&!ui.player.enabled,"Reserved batch inventory");});
        Add(()=>Capture("04-batch-inventory"));
        Add(()=>{scene.DrawingStory.ToggleInventory();Require(ui.player.enabled&&!ui.DialogueOpen,"Inventory lock remains");scene.Travel("School");});WaitScene("School");
        Add(()=>{Require(!scene.DrawingStory.PowderVisible&&!p.CollectPowder(),"Reserved ingredient respawn");Require(scene.Objective.Contains("返回甜品店"),"School batch objective");scene.Travel("MochaStore");});WaitScene("MochaStore");
        Add(()=>{Open("CraftTable");Require(p.Data.stirCount==1,"Batch travel lost");Choose(0);Require(p.Data.stirCount==2,"Second stir");Choose(0);Stage(MatchaCraftStage.ReadyToBake);Require(!p.StirDessert()&&!p.PackDessert(),"More than three stirs/early packing");Choose(0);Stage(MatchaCraftStage.Baking);Require(!p.FinishBaking(),"Baking skipped timer");Snapshot("baking");});
        Add(()=>Capture("05-baking"));
        Add(()=>{Close();p.Reload();Stage(MatchaCraftStage.Baking);});WaitBake();
        Add(()=>{Require(!ui.DialogueOpen,"Baking reopens dismissed panel");Open("CraftTable");Require(ui.DialogueText.Contains("烤好"),"Ready state");});
        Add(()=>Capture("06-ready-to-pack"));
        Add(()=>{
            string path=p.SavePath; string bad="Temp/ChapterOneTests/BlockedSave";Directory.CreateDirectory(bad);
            typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,bad);
            try {Require(!p.PackDessert()&&p.PowderInBatch&&!p.DessertPacked&&!string.IsNullOrEmpty(p.SaveError),"Failed save consumed batch");}
            finally {typeof(ChapterOneProgress).GetProperty("SavePath").SetValue(p,path);}
            Stage(MatchaCraftStage.ReadyToPack);Choose(0);Stage(MatchaCraftStage.Packed);Require(p.SaveError==null,"Write retry failed");
            Require(!p.PackDessert()&&!p.BeginCrafting()&&!p.CollectPowder()&&!p.HasLoosePowder&&!p.PowderInBatch,"Duplicate reward/consumption");
            Require(p.Data.sweetness==MatchaSweetness.Low&&p.Data.filling==MatchaFilling.Milk,"Final recipe lost");
            Pass("Recipe revision, exactly three stirs, safe baking, interruption, travel, save failure rollback, one packed result");
        });
        Add(()=>Capture("07-packed"));
        Add(()=>{Close();scene.DrawingStory.ToggleInventory();Require(ui.Speaker.Contains("马卡龙 × 1")&&ui.DialogueText.Contains("低甜 · 牛奶"),"Finished inventory");});
        Add(()=>Capture("08-finished-inventory"));
        Add(()=>{scene.DrawingStory.ToggleInventory();OpenLaterCloud();});
        Add(()=>{Open("ShopMatchaCloud");Require(!ui.DialogueText.Contains("带回来了")&&ui.DialogueText.Contains("做好"),"Stale cloud dialogue");Close();});
        Add(()=>{Open("CraftTable");Stage(MatchaCraftStage.Packed);Require(ui.DialogueText.Contains("已经用在")&&ui.ChoiceCount==1,"Duplicate craft UI");Close();Snapshot("packed");scene.Travel("School");});WaitScene("School");
        Add(()=>{Require(p.DessertPacked&&scene.ProgressLabel.Contains("便携盒"),"Cross-scene finished inventory");scene.ToggleCloudVision();Require(scene.visionClouds.All(c=>c.activeSelf)&&!scene.DrawingStory.PowderVisible,"Crafting clears gray clouds or respawns powder");Open("Recurring_0");Require(ui.Speaker=="无法清理"&&p.Data.finalPaperMask==0&&p.Data.phase==ChapterOnePhase.ShopIntroduced,"Future story resolved early");Close();Pass("Packed item survives travel; gray clouds and recurring papers remain; story phase unchanged");});
        Add(()=>{var catalog=Resources.Load<MatchaDessertCatalog>("MatchaDessertCatalog");Require(catalog.desserts.Length==4&&catalog.desserts.Count(d=>d.availableAtStart)==1&&catalog.desserts.Single(d=>d.availableAtStart).id=="matcha_cake","Other recipes unlocked");Pass("Existing four dessert definitions and starting cake preserved");});
    }
    // Keep the input cooldown boundary between closing inventory and the next proximity action.
    static void OpenLaterCloud() {Require(ui.player.enabled&&!ui.DialogueOpen,"Inventory close restores movement");}
    static void Matrix()
    {
        foreach(MatchaSweetness sweet in Enum.GetValues(typeof(MatchaSweetness)))
        foreach(MatchaFilling filling in Enum.GetValues(typeof(MatchaFilling)))
        {
            Add(()=>{p.NewGame();Unlock();Require(p.BeginCrafting(),"Combination start");});
            Add(()=>Require(p.SelectSweetness(sweet),"Combination sweetness"));
            Add(()=>Require(p.SelectFilling(filling),"Combination filling"));
            Add(()=>Require(p.AddCraftPowder(),"Combination powder"));
            for(int i=0;i<3;i++) Add(()=>Require(p.StirDessert(),"Combination stir"));
            Add(()=>{Require(p.BeginBaking(),"Combination bake");p.Reload();});WaitBake();
            Add(()=>{Require(p.PackDessert(),"Combination packing");p.Reload();Require(p.DessertPacked&&p.Data.sweetness==sweet&&p.Data.filling==filling,"Combination persist");Pass("Flavour "+sweet+" + "+filling+" produces one dessert without grading");});
        }
        Add(()=>{
            string valid=JsonUtility.ToJson(p.Data,true);File.WriteAllText(p.SavePath+".bak",valid);p.Data.stirCount=0;File.WriteAllText(p.SavePath,JsonUtility.ToJson(p.Data));p.Reload();Require(p.Data.stirCount==3&&p.DessertPacked,"Invalid stage chain accepted");
            string old="{\"version\":1,\"cleanedMask\":15,\"phase\":5,\"scene\":\"School\",\"sketchbookPermission\":true,\"cloudVisionUnlocked\":true,\"investigationMask\":15}";
            File.WriteAllText(p.SavePath,old);p.Reload();Stage(MatchaCraftStage.None);Require(!p.DessertPacked&&!p.Data.powderCollected,"Legacy save grants dessert");
            p.NewGame();Stage(MatchaCraftStage.None);Require(p.Data.stirCount==0&&p.Data.bakeReadyUtcTicks==0,"New game retains batch");Pass("Invalid crafting state falls back to backup; legacy saves and new game reset safely");
        });
    }
    static void Resume(string mode)
    {
        WaitScene("MochaStore");
        Add(()=>{
            if(mode=="resume-selection") {Stage(MatchaCraftStage.SelectFilling);Require(p.Data.sweetness==MatchaSweetness.High&&p.HasLoosePowder,"Cold selection");}
            else if(mode=="resume-stirring") {Stage(MatchaCraftStage.Stirring);Require(p.Data.stirCount==1&&p.PowderInBatch&&!p.HasLoosePowder,"Cold batch");}
            else if(mode=="resume-baking") {scene.Crafting.Tick();Stage(MatchaCraftStage.ReadyToPack);Require(!p.DessertPacked&&p.PowderInBatch,"Cold baking");}
            else {Stage(MatchaCraftStage.Packed);Require(p.Data.sweetness==MatchaSweetness.Low&&p.Data.filling==MatchaFilling.Milk&&!p.BeginCrafting()&&!p.CollectPowder(),"Cold packed recipe");}
            Open("CraftTable");Require(!ui.player.enabled&&ui.Illustration!=null,"Resumed UI");Pass("Cold reload correctly restores "+mode);Close();
        });
    }
    static void Log(string text,string trace,LogType type) {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
    static void Finish()
    {
        Application.logMessageReceived-=Log;started=false;File.Delete(Request);
        if(!preview) {Application.runInBackground=background;EditorApplication.isPlaying=false;}
    }
}

