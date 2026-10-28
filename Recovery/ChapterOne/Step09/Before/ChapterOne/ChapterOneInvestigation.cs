using System;
using UnityEngine;

// Evidence is separate from the chapter phase: clues may be found in any order.
public enum ChapterOneClue { PaperTraces=1, DeskCloud=2, CompetitionNotice=4, Sketchbook=8 }

public sealed class ChapterOneInvestigation
{
    readonly ChapterOneScene scene;
    SchoolInteractionController UI => scene.interaction;
    ChapterOneProgress Progress => scene.Progress;
    public bool JournalOpen { get; private set; }
    static readonly ChapterOneClue[] Clues = {
        ChapterOneClue.PaperTraces, ChapterOneClue.DeskCloud, ChapterOneClue.CompetitionNotice, ChapterOneClue.Sketchbook };
    static readonly string[] Titles = { "再生画纸", "课桌与灰云", "公开比赛通知", "sheila的画本" };
    static readonly string[] Notes = {
        "几张再生画纸留着相似的笔触和同样的修改痕迹。\n它们似乎来自同一幅作品，可以顺着这些线索继续调查。",
        "云视里，课桌旁的纸张与sheila头顶的灰云之间有微弱的联系。\n先了解她为什么不断重画。灰云本身不需要被清除。",
        "校园绘画比赛的主题是“家”，提交日期已经临近。\n这是公开的信息，还不能据此判断sheila心里在想什么。",
        "经sheila同意翻看的画本里，同一幅小屋的画被反复修改。\n屋顶、窗户和门边留下了擦淡的线条与重新落下的铅笔线。" };
    static readonly string[] Leads = {
        "开启云视，靠近任意一处再生纸团，观察纸上的痕迹。",
        "开启云视，靠近sheila或她的课桌，观察灰云与纸张。",
        "去右侧教室阅读左上方墙上的比赛通知。",
        "先征得sheila同意，再翻看她桌上的画本，两页都看完后记下线索。" };

    public ChapterOneInvestigation(ChapterOneScene scene) { this.scene=scene; }
    public string Objective
    {
        get
        {
            if(Progress.InvestigationComplete) return "调查记录已整理 · 回到sheila身边，准备聊聊这幅画";
            for(int i=0;i<Clues.Length;i++) if(!Progress.HasClue(Clues[i])) return Leads[i];
            return "按 J 回看调查记录";
        }
    }
    public void DialogueClosed() => JournalOpen=false;
    public void ToggleJournal()
    {
        if(!Progress.InvestigationUnlocked || scene.IsTransitioning) return;
        if(JournalOpen) UI.CloseDialogue();
        else if(!UI.DialogueOpen) ShowJournal(0);
    }
    public void ShowJournal(int page=0)
    {
        if(!Progress.InvestigationUnlocked || scene.IsTransitioning) return;
        page=Mathf.Clamp(page,0,5);
        string text;
        string title="调查记录  "+(page+1)+" / 6";
        if(page==0)
        {
            title+="  ·  "+Progress.InvestigationCount+" / 4";
            text="";
            for(int i=0;i<Clues.Length;i++) text+=(Progress.HasClue(Clues[i])?"已记录：":"待了解：")+Titles[i]+(i==3?"":i%2==0?"    ":"\n");
            text+="\n用下方选项回看详情；同学近况属于可选观察。";
        }
        else if(page<=4)
        {
            int index=page-1; title+="  ·  "+Titles[index];
            text=Progress.HasClue(Clues[index])?Notes[index]:"尚未记录。\n"+Leads[index];
        }
        else
        {
            title+="  ·  同学近况（可选）";
            text=((Progress.Data.backgroundNotesMask&1)!=0?"Elaine：云里偶尔闪过微光，还在收拾书包。":"Elaine：还没有观察。")+"\n"+
                ((Progress.Data.backgroundNotesMask&2)!=0?"nate：身边浮着淡雾，想看完这一页再回去。":"nate：还没有观察。")+"\n两位同学不计入四条调查线索。";
        }
        JournalOpen=true;
        UI.ShowDialogue(title,text,null,new[]{"上一页","下一页","收起记录"},new Action[]{
            ()=>ShowJournal((page+5)%6),()=>ShowJournal((page+1)%6),UI.CloseDialogue});
    }
    public bool Handle(SchoolInteractable target)
    {
        if(!scene.isSchool || !Progress.InvestigationUnlocked || Progress.Data.phase!=ChapterOnePhase.ShopIntroduced) return false;
        switch(target.kind)
        {
            case SchoolInteractionKind.Xiaoyu:
            case SchoolInteractionKind.ClassmateOne:
            case SchoolInteractionKind.ClassmateTwo:
                if(scene.CloudVisionActive)
                    UI.ShowDialogue(target.displayName,"灰云正浮在同学身边。\n可以先观察，也可以像平时一样交谈。",target.portrait,
                        new[]{"观察灰云","像平时一样交谈","先离开"},new Action[]{()=>ObserveCloud(target),()=>Talk(target),UI.CloseDialogue});
                else Talk(target);
                return true;
            case SchoolInteractionKind.Sketchbook:
                if(scene.CloudVisionActive)
                    UI.ShowDialogue("sheila的课桌","画本放在桌上，灰云在课桌旁浮动。",null,
                        new[]{"观察课桌旁的灰云","翻看画本","先离开"},new Action[]{ObserveDesk,()=>OpenBook(0),UI.CloseDialogue});
                else OpenBook(0);
                return true;
            case SchoolInteractionKind.Notice:
                Evidence(ChapterOneClue.CompetitionNotice,"比赛通知","校园绘画比赛\n本次绘画主题：家。\n提交日期已经临近。用自己的画，描绘心中的家。");
                return true;
        }
        if(target.chapterTarget!=null && target.chapterTarget.action==ChapterOneAction.RecurringPaper)
        {
            if(scene.CloudVisionActive)
                UI.ShowDialogue("无法清理","纸团没有消失。\n云视下，纸上的笔触和残留的痕迹显得更清楚了。",null,
                    new[]{"观察纸上的痕迹","先离开"},new Action[]{()=>Evidence(ChapterOneClue.PaperTraces,"再生画纸",Notes[0]),UI.CloseDialogue});
            else UI.Say("无法清理","纸团没有消失。\n按 Q 开启云视，可以进一步观察这些画纸。");
            return true;
        }
        return false;
    }
    void ObserveCloud(SchoolInteractable target)
    {
        if(!scene.CloudVisionActive) { UI.CloseDialogue(); return; }
        if(target.kind==SchoolInteractionKind.Xiaoyu) { ObserveDesk(); return; }
        int index=target.kind==SchoolInteractionKind.ClassmateOne?0:1;
        string text=index==0?"灰云里偶尔闪过一点微光。\n同学仍在慢慢收拾书包，准备回家。":"一层淡淡的雾在同学身边缓缓飘动。\n同学正低头看着书，想读完这一页。";
        UI.ShowDialogue("同学近况",text+"\n这是此刻的天气，不是一项需要清扫的任务。",target.portrait,
            new[]{"记在近况里"},new Action[]{()=>{ Progress.RecordBackgroundNote(index); UI.CloseDialogue(); }});
    }
    void ObserveDesk()
    {
        if(!scene.CloudVisionActive) { UI.CloseDialogue(); return; }
        Evidence(ChapterOneClue.DeskCloud,"课桌旁的细雨",
            "sheila头顶的灰云落着细雨。课桌边的纸张与灰云之间，\n有一缕若有若无的联系。没有翻开她的私人画本。\n这些纸张的来由，也许要从她正在画的画说起。");
    }
    void Talk(SchoolInteractable target)
    {
        if(target.kind==SchoolInteractionKind.Xiaoyu)
            UI.ShowDialogue("sheila",Progress.Data.sketchbookPermission?"画本就在桌上。翻的时候轻一点哦。":"嗯？我还想再改一点。",target.portrait,
                new[]{"可以看看你的画本吗？",Progress.InvestigationComplete?"聊聊这幅画":"你还不回家吗？","先不打扰了"},new Action[]{
                    ()=>{ Progress.GrantSketchbookPermission(); UI.Say("sheila","可以呀，我把画本放在桌上。\n不过……还没有画好。",target.portrait); },
                    ()=>{ if(Progress.InvestigationComplete) scene.DrawingStory.Begin(target); else UI.Say("sheila","想再画一会儿。总觉得这里还能再改一改。",target.portrait); },UI.CloseDialogue});
        else UI.Say(target.displayName,target.kind==SchoolInteractionKind.ClassmateOne?
            "我收拾好书包就走。你也别待得太晚啦。":"今天教室里好安静。\n我把这一页看完就回去。",target.portrait);
    }
    void OpenBook(int page)
    {
        if(!Progress.Data.sketchbookPermission) { UI.Say("画本","这是sheila的画本。\n先问问她，再翻看吧。"); return; }
        string text=page==0?"纸上画着一间小屋。\n屋顶、窗户和门边，都留下了反复修改的痕迹。":
            "几处线条被擦淡了，旁边还有新的铅笔线。\n她在同一幅画上修改了很多次。\n看完后，把画本轻轻合上。";
        if(page==0) UI.ShowDialogue("sheila的画本  1 / 2",text,null,new[]{"翻到下一页","合上画本"},new Action[]{()=>OpenBook(1),UI.CloseDialogue});
        else UI.ShowDialogue("sheila的画本  2 / 2",text,null,new[]{"返回上一页","合上并记下线索"},new Action[]{()=>OpenBook(0),()=>Record(ChapterOneClue.Sketchbook)});
    }
    void Evidence(ChapterOneClue clue,string title,string text)
    {
        UI.ShowDialogue(title,text,null,new[]{Progress.HasClue(clue)?"合上记录":"记下这条线索"},new Action[]{()=>Record(clue)});
    }
    void Record(ChapterOneClue clue)
    {
        bool wasComplete=Progress.InvestigationComplete;
        Progress.RecordClue(clue);
        if(!wasComplete && Progress.InvestigationComplete) ShowSummary();
        else UI.CloseDialogue();
    }
    void ShowSummary()
    {
        UI.ShowDialogue("调查记录已整理","再生画纸与sheila反复修改的画有关，比赛的提交日期也近了。\n这些线索还不能替她说出心里的想法。\n在她愿意的时候，可以慢慢聊聊这幅画。",null,
            new[]{"回看调查记录","先陪她待一会儿"},new Action[]{()=>ShowJournal(),UI.CloseDialogue});
    }
}
