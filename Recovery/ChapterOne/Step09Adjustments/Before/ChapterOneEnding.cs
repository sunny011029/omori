using System;
using System.Linq;
using UnityEngine;

public enum ChapterOneEndingStage { None, Tasting, Reflection, DraftPending, CloudQuestion, FinalCleaning, Checklist, Complete }

// Sharing, the NPC's own action, the remaining physical paper and chapter closure
// are separate saved beats. The cloud is intentionally never removed by this story.
public sealed class ChapterOneEnding
{
    public const float DraftSeconds=5f, ChecklistSeconds=4.2f;
    public const string ChecklistItem="把教室打扫干净";
    readonly ChapterOneScene scene;
    readonly Sprite draft,macaron;
    readonly MatchaDessertCatalog catalog;
    readonly SchoolInteractable sheila;
    SchoolInteractionController UI => scene.interaction;
    ChapterOneProgress P => scene.Progress;
    ChapterOneEndingStage Stage => P.Data.endingStage;
    float animationTime;
    bool draftAnimation,checklistAnimation;
    public bool AnimationPlaying => draftAnimation || checklistAnimation;
    public bool Active => Stage!=ChapterOneEndingStage.None;
    public string ProgressLabel => P.ChapterComplete ? "第一章 · 打扫不完的教室 · 已完成" :
        P.DraftKept ? "sheila 关联区域  "+P.FinalPaperCount+" / 3    "+Mathf.RoundToInt(P.FinalPaperCount*100f/3)+"%" : "分享甜品 · 抹茶马卡龙";
    public string Objective => P.ChapterComplete ? "可继续探索 · J 回看记录与食谱 · 学校出口回看结尾" :
        !scene.isSchool ? "返回学校，继续 sheila 的故事" :
        Stage==ChapterOneEndingStage.Checklist ? "局部清扫完成 · 与 sheila 交谈或到出口，查看章末清单" :
        Stage==ChapterOneEndingStage.FinalCleaning ? "魔法垃圾已停止再生 · 清理原位置的 3 处残留画纸" :
        Stage==ChapterOneEndingStage.CloudQuestion ? "草稿已收好 · 回到 sheila 身边，看看发生了什么" :
        Stage==ChapterOneEndingStage.DraftPending ? "sheila 准备保留第一版草稿 · 回到她身边继续" : "回到 sheila 身边，听她把话说完";

    public ChapterOneEnding(ChapterOneScene scene)
    {
        this.scene=scene;
        draft=Resources.Load<ChapterOneStoryArt>("ChapterOneStoryArt")?.originalDraft;
        catalog=Resources.Load<MatchaDessertCatalog>("MatchaDessertCatalog");
        macaron=catalog?.desserts?.FirstOrDefault(d=>d.id=="matcha_macaron")?.sprite;
        sheila=UI.targets.FirstOrDefault(t=>t.kind==SchoolInteractionKind.Xiaoyu);
    }

    public bool Handle(SchoolInteractable target)
    {
        if(scene.isSchool && target.kind==SchoolInteractionKind.Xiaoyu && P.DessertPacked)
        { OpenSheila(); return true; }
        if(scene.isSchool && target.kind==SchoolInteractionKind.Sketchbook && P.DraftKept)
        {
            UI.ShowIllustration("sheila 的画本","第一版草稿被平整地夹在画本里。\n这是 sheila 决定保留的作品。看完后，把画本轻轻合上。",draft,
                new[]{"合上画本"},new Action[]{UI.CloseDialogue}); return true;
        }
        var item=target.chapterTarget;
        if(item!=null && item.action==ChapterOneAction.RecurringPaper && P.DraftKept)
        {
            if(Stage==ChapterOneEndingStage.CloudQuestion) ShowCloudQuestion();
            else if(P.CompleteFinalPaper(item.index))
            {
                if(P.LocalAreaComplete) ShowSettlement();
                else UI.Say("收起残留画纸","这次，废纸终于可以收走了。\n原来的位置没有再出现新的纸团。\nsheila 关联区域："+P.FinalPaperCount+" / 3 处。任务只计算这些残留画纸。");
            }
            else if(!string.IsNullOrEmpty(P.SaveError)) SaveFailure();
            return true;
        }
        if(item!=null && item.action==ChapterOneAction.ShopCloud && Active)
        {
            UI.Say("抹茶云",P.DraftKept ? "云还在，纸团却没有再出现……\n甜品和灰云，到底是怎么回事呢？" :
                "你已经把甜品带给她了。\n回到学校，慢慢听她把话说完吧。",scene.cloudPortrait); return true;
        }
        return false;
    }

    public void OpenSheila()
    {
        if(!scene.isSchool || !P.DessertPacked) return;
        switch(Stage)
        {
            case ChapterOneEndingStage.None:
                UI.ShowIllustration("分享甜品","我做了一份抹茶马卡龙，你愿意尝尝吗？\nsheila：好呀。你做的是什么味道？\n便携盒里是："+scene.Crafting.Flavour+"。",macaron,
                    new[]{"递上便携盒","先收好，稍后再来"},new Action[]{()=>Act(P.ShareDessert,ShowTasting),UI.CloseDialogue}); break;
            case ChapterOneEndingStage.Tasting: ShowTasting(); break;
            case ChapterOneEndingStage.Reflection: ShowReflection(); break;
            case ChapterOneEndingStage.DraftPending: StartDraftAnimation(); break;
            case ChapterOneEndingStage.CloudQuestion: ShowCloudQuestion(); break;
            case ChapterOneEndingStage.FinalCleaning:
                UI.ShowDialogue("sheila","第一版已经收好了。比赛的事还是有点担心……\n不过，我想先休息一下，之后再继续画。",sheila?.portrait,
                    new[]{"看看保留下来的草稿","继续整理残留画纸"},new Action[]{ShowKeptDraft,UI.CloseDialogue}); break;
            case ChapterOneEndingStage.Checklist: ShowSettlement(); break;
            case ChapterOneEndingStage.Complete:
                UI.ShowDialogue("sheila","第一版已经收好了。比赛的事还是有点担心……\n不过，我想先休息一下，之后再继续画。",sheila?.portrait,
                    new[]{"看看保留下来的草稿","回看这一章的记录","先不打扰了"},new Action[]{ShowKeptDraft,()=>scene.Investigation.ShowJournal(6),UI.CloseDialogue}); break;
        }
    }
    void ShowKeptDraft() => UI.ShowIllustration("保留下来的第一版","她把第一版草稿夹进画本，没有再揉掉。\n灰云还在落着细雨，地上的纸团却停止了再生。",draft,
        new[]{"轻轻合上画本"},new Action[]{UI.CloseDialogue});
    public static string TasteResponse(MatchaSweetness sweetness,MatchaFilling filling)
    {
        string sweet=sweetness==MatchaSweetness.Low?"哇，有点苦……不过后面还挺香的。":
            sweetness==MatchaSweetness.High?"这个好甜！你是不是放了很多糖？":"甜味和抹茶香一起慢慢散开了。";
        string extra=filling==MatchaFilling.Milk?"还有一点奶香，吃起来软软的。":
            filling==MatchaFilling.RedBean?"里面有红豆呢，能尝到小小的豆粒。":"没有加别的味道，抹茶的香气很清楚。";
        return "sheila 尝了一口抹茶马卡龙。\n"+sweet+"\n"+extra;
    }
    void ShowTasting() => UI.ShowIllustration("sheila · 尝一口",TasteResponse(P.Data.sweetness,P.Data.filling),macaron,
        new[]{"听她继续说"},new Action[]{()=>Act(P.ReflectOnDraft,ShowReflection)});
    void ShowReflection() => UI.ShowDialogue("sheila","她看着手里的甜品，犹豫了一下。\n其实……我觉得我画的第一版也没有那么差。",sheila?.portrait,
        new[]{"那你想不想再看看第一版？","你也不用现在就决定。"},new Action[]{
            ()=>Act(()=>P.PrepareToKeepDraft(0),StartDraftAnimation),()=>Act(()=>P.PrepareToKeepDraft(1),StartDraftAnimation)});

    void StartDraftAnimation()
    {
        if(Stage!=ChapterOneEndingStage.DraftPending) return;
        string reply=P.Data.draftReply==0?"嗯……让我再看看。我想把这张留着。":"嗯……不过，这张我想先留着。";
        UI.ShowIllustration("sheila · 收好第一版",reply+"\n她把第一版草稿放进画本，轻轻压平纸页。\n比赛还是有点让人在意。我想先休息一下，之后再继续画。",draft,
            new[]{"先停一下（之后可以继续）"},new Action[]{UI.CloseDialogue});
        animationTime=0; draftAnimation=true; checklistAnimation=false; UI.SetDraftAnimation(0);
    }
    void ShowCloudQuestion() => UI.ShowDialogue("抹茶云","诶？云怎么还在？",scene.cloudPortrait,new[]{"看向课桌旁"},new Action[]{()=>
        UI.ShowDialogue("我","可是……纸团好像不再出现了。",null,new[]{"完成最后的清扫"},new Action[]{()=>Act(P.StartFinalCleaning,UI.CloseDialogue)})});

    public void ShowSettlement()
    {
        if(!P.LocalAreaComplete) return;
        UI.ShowDialogue("区域清扫完成！","sheila 关联区域 · 异常循环已稳定 · 100%\n抹茶马卡龙食谱已收录，这次的口味是："+scene.Crafting.Flavour+"。\n常规清扫：4 / 4。局部残留画纸：3 / 3。她的灰云还在。",null,
            new[]{"整理自己的清单","回看人物记录","稍后继续"},new Action[]{StartChecklist,()=>scene.Investigation.ShowJournal(6),UI.CloseDialogue});
    }
    public void StartChecklist()
    {
        if(!P.LocalAreaComplete) return;
        UI.ShowDialogue("我的清单","这片终于干净了。\n我划掉那条待办事项，合上记事本。",null,
            new[]{"先收起来（之后可以继续）"},new Action[]{UI.CloseDialogue});
        animationTime=0; draftAnimation=false; checklistAnimation=true; UI.SetChecklistAnimation(0);
    }
    public void Tick()
    {
        if(!AnimationPlaying) return;
        if(!UI.DialogueOpen || scene.IsTransitioning ||
            (draftAnimation && UI.Speaker!="sheila · 收好第一版") || (checklistAnimation && UI.Speaker!="我的清单"))
        { DialogueClosed(); return; }
        animationTime+=Time.unscaledDeltaTime;
        if(draftAnimation)
        {
            UI.SetDraftAnimation(animationTime/DraftSeconds);
            if(animationTime<DraftSeconds) return;
            draftAnimation=false;
            Act(P.KeepDraft,ShowCloudQuestion);
        }
        else
        {
            UI.SetChecklistAnimation(animationTime/ChecklistSeconds);
            if(animationTime<ChecklistSeconds) return;
            checklistAnimation=false;
            if(P.ChapterComplete) ShowEnding();
            else Act(P.FinishChapter,ShowEnding);
        }
    }
    public void DialogueClosed() { draftAnimation=false; checklistAnimation=false; animationTime=0; }
    public void ShowEnding()
    {
        if(!P.ChapterComplete) return;
        UI.ShowDialogue("第一章 · 打扫不完的教室","“"+ChecklistItem+"”——刚划掉的同一条事项，悄悄恢复了。\n这片终于干净了，可清单为什么又恢复了？\nsheila 的故事暂告一段落。",null,
            new[]{"继续自由探索","查看食谱收藏","回看章末清单"},new Action[]{UI.CloseDialogue,()=>ShowRecipes(0),StartChecklist});
        UI.SetChecklistAnimation(1);
    }
    public bool HandleExit()
    {
        if(!Active) return false;
        if(P.ChapterComplete) ShowEnding();
        else if(P.LocalAreaComplete) ShowSettlement();
        else UI.Say("先等一下",Objective);
        return true;
    }
    public string DiaryText => P.DraftKept ? "sheila 把第一版草稿收进画本，决定先休息、之后再画。\n她仍担心比赛；灰云继续落雨，纸团却停止了再生。\n"+
        (P.LocalAreaComplete?"我收走了 3 处残留画纸，只完成了她的关联区域。":"原来的废纸还在，得由我逐一收走。") :
        "sheila 尝了我做的抹茶马卡龙："+scene.Crafting.Flavour+"。\n她说第一版也没有那么差。\n这时灰云和地上的纸团都还在。";
    public void ShowRecipes(int page=0)
    {
        if(catalog?.desserts==null || catalog.desserts.Length==0) return;
        page=Mathf.Clamp(page,0,catalog.desserts.Length-1);
        var recipe=catalog.desserts[page];
        bool unlocked=recipe.availableAtStart || (recipe.id=="matcha_macaron" && P.LocalAreaComplete);
        string text=recipe.availableAtStart?"起始甜品 · 甜品店橱窗里的抹茶蛋糕。":
            unlocked?"与 sheila 相处后收录的食谱。\n本章制作："+scene.Crafting.Flavour+" · 微涩的抹茶粉。\n这是这次分享的记录，每种口味都有自己的风味。":
            "尚未收录。\n对应同学："+recipe.storyCharacterName+"。";
        UI.ShowIllustration("甜品收藏  "+(page+1)+" / "+catalog.desserts.Length+" · "+recipe.displayName+(unlocked?"":"（未解锁）"),text,unlocked?recipe.sprite:null,
            new[]{"上一款","下一款","收起食谱"},new Action[]{()=>ShowRecipes((page+catalog.desserts.Length-1)%catalog.desserts.Length),()=>ShowRecipes((page+1)%catalog.desserts.Length),UI.CloseDialogue});
    }
    void Act(Func<bool> change,Action next)
    {
        if(change()) next();
        else if(!string.IsNullOrEmpty(P.SaveError)) SaveFailure();
    }
    void SaveFailure() => UI.ShowDialogue("这一步暂未保存","故事仍停留在上一个完成的节点。\n检查存储空间后，可以重新交谈或查看清单继续。",null,new[]{"稍后继续"},new Action[]{UI.CloseDialogue});
    public static bool IsValidSave(ChapterOneSave d)
    {
        if(!Enum.IsDefined(typeof(ChapterOneEndingStage),d.endingStage) || d.draftReply<0 || d.draftReply>1) return false;
        bool started=d.endingStage!=ChapterOneEndingStage.None;
        if(started && (d.craftStage!=MatchaCraftStage.Packed || !d.dessertConsumed)) return false;
        if(!started && d.dessertConsumed) return false;
        if(d.endingStage<ChapterOneEndingStage.DraftPending && d.draftReply!=0) return false;
        bool stable=d.endingStage>=ChapterOneEndingStage.CloudQuestion;
        if(stable!=(d.phase==ChapterOnePhase.AnomalyResolved)) return false;
        if(d.endingStage<ChapterOneEndingStage.FinalCleaning && d.finalPaperMask!=0) return false;
        if((d.endingStage>=ChapterOneEndingStage.Checklist)!=(d.finalPaperMask==7)) return false;
        return true;
    }
}
