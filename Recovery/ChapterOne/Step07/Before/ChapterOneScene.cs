using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
public sealed class ChapterOneScene : MonoBehaviour
{
    public bool isSchool=true;
    public Transform roomRoot;
    public SchoolInteractionController interaction;
    public ChapterOneTarget[] chapterTargets;
    public SchoolLocker encounterLocker;
    public GameObject encounterCloud;
    public GameObject portalGlow;
    public GameObject shopCloud;
    public GameObject[] visionClouds;
    public Sprite cloudPortrait;
    public Vector2 schoolArrival=new Vector2(14.5f,.7f);
    public Vector2 shopArrival=new Vector2(-6.7f,2.2f);
    public ChapterOneProgress Progress { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool CloudVisionActive { get; private set; }
    public ChapterOneInvestigation Investigation { get; private set; }
    bool insideExit,encounterStarted;
    public string SceneId=>isSchool?"School":"MochaStore";
    public string ProgressLabel=>Progress.Data.phase==ChapterOnePhase.ShopIntroduced ?
        "调查线索  "+Progress.InvestigationCount+" / 4" : "常规清扫  "+Progress.CleanCount+" / 4    "+(Progress.CleanCount*25)+"%";
    public string Objective
    {
        get
        {
            if(Progress==null) return "";
            if(!string.IsNullOrEmpty(Progress.SaveError)) return Progress.SaveError;
            var phase=Progress.Data.phase;
            if(!isSchool)
            {
                if(phase<ChapterOnePhase.CloudMet) return "从绿色入口返回学校";
                if(phase==ChapterOnePhase.CloudMet) return "与柜台上的抹茶云交谈";
                if(!Progress.Data.inspectedCraftTable) return "查看抹茶云右侧的制作台";
                return Progress.InvestigationComplete ? "调查记录已整理 · 可返回学校，与小雨继续相处" : "从绿色入口返回学校，调查再生画纸与小雨的画";
            }
            switch(phase)
            {
                case ChapterOnePhase.Cleaning: return "清扫两间教室和走廊，整理右下角书本";
                case ChapterOnePhase.ReadyToLeave: return "整理完成 · 走向下方出口";
                case ChapterOnePhase.Anomaly: return "查看走廊里重新出现的纸团";
                case ChapterOnePhase.SeekingTool: return "找工具 · 查看左侧四连柜中的第三个";
                case ChapterOnePhase.CloudMet: return "走进右侧大书柜左边的独立柜子";
                case ChapterOnePhase.ShopIntroduced: return Investigation.Objective;
                default: return "把剩下的废纸整理好";
            }
        }
    }
    void Awake()
    {
        Progress=ChapterOneProgress.Ensure(); interaction.chapter=this; Investigation=new ChapterOneInvestigation(this);
        if(visionClouds!=null) foreach(var cloud in visionClouds)
        {
            if(cloud==null) continue;
            var npc=cloud.GetComponentInParent<SchoolInteractable>();
            if(npc==null) continue;
            var weather=cloud.GetComponent<ChapterOneWeather>();
            if(weather==null) weather=cloud.AddComponent<ChapterOneWeather>();
            weather.Configure(npc.kind);
        }
    }
    void OnEnable() { if(Progress!=null) Progress.Changed+=Refresh; }
    void OnDisable() { if(Progress!=null) Progress.Changed-=Refresh; }
    void Start()
    {
        bool resume=Progress.ConsumeResume();
        if(resume && Progress.Data.scene!=SceneId && Progress.Data.phase>=ChapterOnePhase.CloudMet)
        { Travel(Progress.Data.scene); return; }
        if(!Progress.StartingNewGame && (Progress.PendingScene==SceneId || (resume && Progress.Data.phase>=ChapterOnePhase.CloudMet)))
            PlacePlayer(isSchool ? schoolArrival : shopArrival);
        Refresh();
        if(Progress.PendingScene==SceneId) Progress.Arrive(SceneId);
    }
    void Update()
    {
        if(Progress==null || IsTransitioning || interaction.DialogueOpen) return;
        if(Input.GetKeyDown(KeyCode.Q)) ToggleCloudVision();
        if(interaction.DialogueOpen) return;
        if(!isSchool) return;
        Vector3 p=roomRoot.InverseTransformPoint(interaction.player.transform.position);
        bool entered=p.x>=6.3f && p.x<=8.7f && p.y<-.75f;
        if(entered && !insideExit) LeaveSchool();
        insideExit=entered;
    }
    public bool ToggleCloudVision()
    {
        if(!Progress.Data.cloudVisionUnlocked || IsTransitioning || interaction.DialogueOpen) return false;
        CloudVisionActive=!CloudVisionActive; Refresh(); return true;
    }
    public void Refresh()
    {
        if(Progress==null) return;
        interaction.SetConversationPhase(Progress.Data.phase>=ChapterOnePhase.ShopIntroduced ? SchoolConversationPhase.Investigation : SchoolConversationPhase.DailyRoutine);
        foreach(var target in chapterTargets)
        {
            bool active=true;
            if(target.action==ChapterOneAction.Clean) active=!Progress.IsClean(target.index);
            else if(target.action==ChapterOneAction.RecurringPaper)
                active=Progress.Data.phase>=ChapterOnePhase.Anomaly && !Progress.IsFinalPaperClean(target.index);
            if(target.visual!=null) target.visual.SetActive(active);
            target.interaction.enabled=active;
        }
        if(encounterCloud!=null) encounterCloud.SetActive((encounterStarted && Progress.Data.phase==ChapterOnePhase.SeekingTool) || Progress.Data.phase==ChapterOnePhase.CloudMet);
        if(portalGlow!=null) portalGlow.SetActive(!isSchool || Progress.Data.phase>=ChapterOnePhase.CloudMet);
        if(shopCloud!=null) shopCloud.SetActive(Progress.Data.phase>=ChapterOnePhase.CloudMet);
        if(visionClouds!=null) foreach(var cloud in visionClouds) if(cloud!=null) cloud.SetActive(CloudVisionActive && Progress.Data.cloudVisionUnlocked);
        foreach(var t in interaction.targets)
        {
            if(t.interactionId=="DessertEntranceLocker") t.promptOverride=Progress.Data.phase>=ChapterOnePhase.CloudMet?"查看柜中入口":null;
            if(t.interactionId=="FirstEncounterLocker") t.promptOverride=Progress.Data.phase==ChapterOnePhase.SeekingTool?"打开柜门找工具":null;
            if(t.kind==SchoolInteractionKind.Xiaoyu || t.kind==SchoolInteractionKind.ClassmateOne || t.kind==SchoolInteractionKind.ClassmateTwo)
                t.promptOverride=CloudVisionActive ? "观察灰云 / 与"+t.displayName+"交谈" : null;
        }
    }
    public bool CanUse(SchoolInteractable target)
    {
        if(IsTransitioning) return false;
        var t=target.chapterTarget;
        if(t==null) return true;
        if(t.action==ChapterOneAction.Clean) return !Progress.IsClean(t.index);
        if(t.action==ChapterOneAction.RecurringPaper) return Progress.Data.phase>=ChapterOnePhase.Anomaly && !Progress.IsFinalPaperClean(t.index);
        if(t.action==ChapterOneAction.ShopCloud) return Progress.Data.phase>=ChapterOnePhase.CloudMet;
        return true;
    }
    public bool Handle(SchoolInteractable target)
    {
        if(Investigation.Handle(target)) return true;
        if(isSchool && target.interactionId=="FirstEncounterLocker")
        {
            if(Progress.Data.phase==ChapterOnePhase.SeekingTool)
            {
                encounterStarted=true; encounterLocker.SetOpen(true); Refresh();
                Flow(new[]{
                    "柜子里没有扫帚，却飘着一小团绿色的云。",
                    "你是在找能把那些纸团扫走的工具吗？",
                    "我是抹茶云。那不是普通的垃圾，只靠清扫是带不走的。",
                    "先跟我去一个地方吧。打开右边大书柜左侧的独立柜子，就能找到入口。"},
                    ()=>{ Progress.MeetCloud(); interaction.CloseDialogue(); });
                return true;
            }
            if(Progress.Data.phase==ChapterOnePhase.CloudMet)
            {
                encounterLocker.SetOpen(true);
                interaction.ShowDialogue("抹茶云","入口就在右侧大书柜左边的独立柜子里。\n我在甜品店等你。",cloudPortrait,
                    new[]{"知道了","关上柜门"},new Action[]{interaction.CloseDialogue,()=>{ encounterLocker.SetOpen(false); interaction.CloseDialogue(); }});
                return true;
            }
        }
        if(isSchool && target.interactionId=="DessertEntranceLocker" && Progress.Data.phase>=ChapterOnePhase.CloudMet)
        {
            target.locker.SetOpen(true);
            interaction.ShowDialogue("柜中入口","柜子深处泛着柔和的绿色光。",null,new[]{"进入甜品店","关上柜门"},
                new Action[]{()=>Travel("MochaStore"),()=>{ target.locker.SetOpen(false); interaction.CloseDialogue(); }});
            return true;
        }
        var item=target.chapterTarget;
        if(item==null) return false;
        switch(item.action)
        {
            case ChapterOneAction.Clean:
                if(Progress.CompleteClean(item.index))
                    interaction.Say(item.index==3?"整理书本":"清扫", Progress.CleanCount==4 ?
                        "都整理好了。\n可以从下方出口离开学校了。" : item.index==3 ? "散落的书本已经整理好了。" : "把地上的杂物收拾干净了。");
                break;
            case ChapterOneAction.RecurringPaper:
                if(Progress.Data.phase==ChapterOnePhase.AnomalyResolved)
                { if(Progress.CompleteFinalPaper(item.index)) interaction.Say("清扫","这次，废纸终于可以收走了。"); }
                else
                {
                    Progress.InspectAnomaly();
                    interaction.Say("无法清理","纸团没有消失。\n也许该去走廊的储物柜里找找工具。");
                }
                break;
            case ChapterOneAction.SchoolExit: LeaveSchool(); break;
            case ChapterOneAction.ShopCloud:
                if(Progress.Data.phase==ChapterOnePhase.CloudMet)
                    Flow(new[]{
                        "欢迎来到我的甜品店。这里的甜品，需要一点特别的原料。",
                        "你要找的第一种原料，叫作“微涩的抹茶粉”。现在还不能开始制作。",
                        "我把云视借给你。按 Q，可以看见同学们身边的灰云；再按一次就能回到普通视野。",
                        "灰云不是坏东西，也不需要马上消失。先回学校，慢慢听听小雨想说什么吧。\n制作台就在我右边，绿色入口可以带你回去。"},
                        ()=>{ Progress.IntroduceShop(); interaction.CloseDialogue(); });
                else interaction.Say("抹茶云",!Progress.Data.inspectedCraftTable?
                    "先去看看右侧的制作台吧。\n按 Q 可以切换云视；来时的绿色入口会带你回学校。":
                    Progress.InvestigationComplete?"你已经看到了纸张和画之间的联系。\n她自己的想法，还是慢慢听她说吧。制作台还需要微涩的抹茶粉。":
                    "制作台还需要微涩的抹茶粉。先回学校看看吧。\n用云视观察画纸和小雨的课桌，读读比赛通知；翻画本前，记得先问她。",cloudPortrait);
                break;
            case ChapterOneAction.CraftTable:
                interaction.ShowDialogue("制作台","还缺少特殊原料：微涩的抹茶粉。\n现在无法制作，先回学校看看吧。",null,new[]{"记住了"},
                    new Action[]{()=>{ Progress.InspectCraftTable(); interaction.CloseDialogue(); }});
                break;
            case ChapterOneAction.ReturnToSchool:
                interaction.ShowDialogue("绿色入口","从这里可以回到学校的走廊。",null,new[]{"返回学校","再逛一会儿"},
                    new Action[]{()=>Travel("School"),interaction.CloseDialogue});
                break;
        }
        return true;
    }
    public void LeaveSchool()
    {
        if(IsTransitioning || interaction.DialogueOpen) return;
        if(Progress.TriggerAnomaly())
        {
            if(encounterLocker!=null) encounterLocker.SetOpen(false);
            interaction.Say("……","刚才明明已经打扫干净了。\n走廊里，怎么又出现了纸团？");
        }
        else if(Progress.Data.phase==ChapterOnePhase.Cleaning) interaction.Say("还没整理完","把教室和走廊收拾好，再回家吧。");
        else interaction.Say("先等一下",Progress.Data.phase>=ChapterOnePhase.ShopIntroduced ?
            "先去和小雨聊聊吧。" : "那些重新出现的纸团，让人有点在意。");
    }
    void Flow(string[] pages,Action finish,int page=0)
    {
        interaction.ShowDialogue(page==0 && isSchool?"储物柜":"抹茶云",pages[page],page==0 && isSchool?null:cloudPortrait,
            new[]{page==pages.Length-1?"记住了":"继续"},new Action[]{()=>{ if(page+1<pages.Length) Flow(pages,finish,page+1); else finish(); }});
    }
    public void PauseMenu()
    {
        if(IsTransitioning) return;
        interaction.ShowDialogue("稍作休息","进度会在完成清扫、对话和场景切换时自动保存。",null,new[]{"继续游戏","重新开始本章"},
            new Action[]{interaction.CloseDialogue,()=>interaction.ShowDialogue("重新开始","这会重置本章的清扫和剧情进度。",null,new[]{"重新开始","取消"},
                new Action[]{()=>{ Progress.NewGame(); Travel("School"); },PauseMenu})});
    }
    public void Travel(string destination)
    {
        if(IsTransitioning || (destination!="School" && destination!="MochaStore")) return;
        if(destination=="MochaStore" && Progress.Data.phase<ChapterOnePhase.CloudMet) return;
        StartCoroutine(Load(destination));
    }
    IEnumerator Load(string destination)
    {
        interaction.CloseDialogue(); IsTransitioning=true;
        bool previousMovement=interaction.player.enabled; interaction.player.enabled=false;
        string scenePath=destination=="School"?"Assets/Scenes/School.unity":"Assets/Scenes/MochaStore.unity";
        if(!Application.CanStreamedLevelBeLoaded(scenePath))
        {
            IsTransitioning=false; interaction.player.enabled=previousMovement;
            interaction.Say("入口暂时没有回应","请稍后再试。"); yield break;
        }
        Progress.PendingScene=destination;
        yield return new WaitForSecondsRealtime(.2f);
        var operation=SceneManager.LoadSceneAsync(scenePath,LoadSceneMode.Single);
        while(!operation.isDone) yield return null;
    }
    public void PlacePlayer(Vector2 point)
    {
        Vector3 position=isSchool?roomRoot.TransformPoint(point):(Vector3)point;
        var body=interaction.player.GetComponent<Rigidbody2D>(); body.velocity=Vector2.zero; body.position=position;
        interaction.player.transform.position=position; Physics2D.SyncTransforms();
        var camera=interaction.worldCamera; var follow=camera.GetComponent<TopDownCameraFollow>();
        camera.transform.position=new Vector3(position.x+follow.Offset.x,position.y+follow.Offset.y,camera.transform.position.z); follow.ConstrainNow();
    }
}
