using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// The conversation saves only completed beats. Exiting a page never grants a reward.
public sealed class ChapterOneDrawingStory
{
    readonly ChapterOneScene scene;
    readonly ChapterOneStoryArt art;
    SchoolInteractionController UI => scene.interaction;
    ChapterOneProgress Progress => scene.Progress;
    SchoolInteractable sheila;
    SchoolInteractable pickup;
    GameObject powderVisual;
    public bool InventoryOpen { get; private set; }
    public bool PowderVisible => powderVisual!=null && powderVisual.activeInHierarchy;
    public ChapterOneDrawingStory(ChapterOneScene scene)
    {
        this.scene=scene; art=Resources.Load<ChapterOneStoryArt>("ChapterOneStoryArt");
        if(!scene.isSchool) return;
        sheila=UI.targets.FirstOrDefault(t=>t.kind==SchoolInteractionKind.Xiaoyu);
        if(sheila==null || art==null || art.matchaPowder==null) return;
        var go=new GameObject("MatchaPowderPickup"); go.transform.SetParent(scene.transform,false);
        go.transform.position=sheila.transform.position+new Vector3(.65f,-.25f,0);
        pickup=go.AddComponent<SchoolInteractable>(); pickup.interactionId="MatchaPowderPickup";
        pickup.kind=SchoolInteractionKind.StoryPickup; pickup.displayName="微涩的抹茶粉";
        pickup.promptOverride="拾取微涩的抹茶粉"; pickup.reach=.85f;
        go.AddComponent<SortingGroup>(); go.AddComponent<TopDownYSort>();
        powderVisual=new GameObject("PowderVisual"); powderVisual.transform.SetParent(go.transform,false);
        powderVisual.transform.localPosition=new Vector3(0,1.55f,0);
        var sr=powderVisual.AddComponent<SpriteRenderer>(); sr.sprite=art.matchaPowder;
        sr.sharedMaterial=sheila.GetComponentInChildren<SpriteRenderer>().sharedMaterial; sr.sortingOrder=10;
        powderVisual.transform.localScale=Vector3.one*(.5f/sr.sprite.bounds.size.x);
        powderVisual.AddComponent<ChapterOneFloat>(); pickup.clickVisual=powderVisual.transform;
        UI.targets=UI.targets.Concat(new[]{pickup}).ToArray(); Refresh();
    }
    public string Objective
    {
        get
        {
            if(Progress.Data.powderCollected) return scene.isSchool?"已获得微涩的抹茶粉 · 带回甜品店，按 I 查看物品":"原料已备好 · 查看制作台，准备抹茶马卡龙";
            if(Progress.Data.powderRevealed) return scene.isSchool?"开启云视，拾取 sheila 课桌旁的微涩的抹茶粉":"回到学校，拾取 sheila 课桌旁出现的抹茶粉";
            if(Progress.Data.originalDraftViewed) return "回到 sheila 身边，听她把话说完";
            if((Progress.Data.drawingQuestionsMask&4)!=0) return "sheila 愿意展示最初的草稿 · 继续与她交谈";
            if(Progress.Data.reassuranceFinished) return "慢慢聊聊她最初为什么想画这幅画";
            return "调查记录已整理 · 和 sheila 聊聊这幅画";
        }
    }
    public void Refresh()
    {
        bool available=Progress.Data.powderRevealed && !Progress.Data.powderCollected && scene.CloudVisionActive;
        if(pickup!=null) pickup.enabled=available;
        if(powderVisual!=null) powderVisual.SetActive(available);
    }
    public void DialogueClosed() => InventoryOpen=false;
    public void ToggleInventory()
    {
        if(!Progress.InvestigationUnlocked || scene.IsTransitioning) return;
        if(InventoryOpen) UI.CloseDialogue();
        else if(!UI.DialogueOpen) ShowInventory();
    }
    public void ShowInventory()
    {
        InventoryOpen=true;
        if(Progress.Data.powderCollected)
            UI.ShowIllustration("物品 · 微涩的抹茶粉 × 1","在 sheila 愿意表达之后凝结的特殊原料。\n灰云依然留在她身边。可以把这份原料带回甜品店。",art!=null?art.matchaPowder:null,
                new[]{"收起物品栏"},new Action[]{UI.CloseDialogue});
        else UI.ShowDialogue("物品栏","还没有获得特殊原料。\n先慢慢了解 sheila 的画，再听听她自己的想法。",null,new[]{"收起物品栏"},new Action[]{UI.CloseDialogue});
    }
    public bool Handle(SchoolInteractable target)
    {
        if(target!=pickup || pickup==null) return false;
        if(!scene.CloudVisionActive || !Progress.Data.powderRevealed || Progress.Data.powderCollected) return true;
        UI.ShowIllustration("微涩的抹茶粉","绿色的颗粒轻轻聚在一起。\n它们没有带走灰云，只凝结出一份新的风味。",art.matchaPowder,
            new[]{"收进物品栏","先放在这里"},new Action[]{()=>{
                if(Progress.CollectPowder()) UI.Say("获得物品","微涩的抹茶粉 × 1\n按 I 可以查看；准备好后，把它带回甜品店。",art.matchaPowder);
                else UI.CloseDialogue();
            },UI.CloseDialogue});
        return true;
    }
    public bool Begin(SchoolInteractable target)
    {
        if(target.kind!=SchoolInteractionKind.Xiaoyu || !Progress.DrawingConversationReady) return false;
        sheila=target;
        if(Progress.Data.powderCollected)
        {
            UI.ShowDialogue("sheila","谢谢你愿意听我说这些。\n我想再看看最开始的那张。",sheila.portrait,
                new[]{"再看看最初的草稿","查看物品","先不打扰了"},new Action[]{()=>ShowDraft(true),ShowInventory,UI.CloseDialogue});
        }
        else if(Progress.Data.powderRevealed)
            UI.Say("sheila","我想先看看这张草稿。\n谢谢你愿意陪我待一会儿。",sheila.portrait);
        else if(Progress.Data.originalDraftViewed) Expression();
        else if((Progress.Data.drawingQuestionsMask&4)!=0) OfferDraft();
        else if(Progress.Data.reassuranceFinished) Questions();
        else Reassurance(0);
        return true;
    }
    void Reassurance(int page)
    {
        string[] lines={"你是不是一直在画这张图？","嗯……但总觉得哪里不太对。","我觉得已经挺好看了呀。","谢谢。不过我自己还是不太满意。"};
        bool player=page%2==0;
        UI.ShowDialogue(player?"你":"sheila",lines[page],player?null:sheila.portrait,new[]{"继续"},new Action[]{()=>{
            if(page<3) Reassurance(page+1); else { Progress.FinishReassurance(); Questions(); }
        }});
    }
    void Questions()
    {
        UI.ShowDialogue("和 sheila 聊聊","她看了看画纸，手中的铅笔暂时停了下来。",sheila.portrait,
            new[]{"你最不满意哪里？","你已经画了多久？","你最开始为什么想画它？"},new Action[]{()=>Answer(0),()=>Answer(1),()=>Answer(2)});
    }
    void Answer(int index)
    {
        string[] lines={"窗户和屋顶，总觉得哪里不太对。","改了一遍又一遍，已经数不清了。","一开始画的时候，我是喜欢它的。\n后来就总想着再改好一点。"};
        UI.ShowDialogue("sheila",lines[index],sheila.portrait,new[]{"继续听"},new Action[]{()=>{
            Progress.RecordDrawingQuestion(index);
            if(index==2) OfferDraft();
            else UI.ShowDialogue("继续交谈","她还在看着那张画。可以再问问最初的想法。",null,
                new[]{"最开始为什么想画它？","换个问题聊聊","先不打扰了"},new Action[]{()=>Answer(2),Questions,UI.CloseDialogue});
        }});
    }
    void OfferDraft()
    {
        UI.ShowDialogue("sheila","最早的那张，我还留着。\n你想看看吗？",sheila.portrait,
            new[]{"看看最初的草稿","再聊聊这幅画","下次再看"},new Action[]{()=>ShowDraft(false),Questions,UI.CloseDialogue});
    }
    void ShowDraft(bool review)
    {
        UI.ShowIllustration("sheila 展示的最初草稿","小屋的线条没有被反复擦改。\n比起后来那几页，这张草稿显得更轻松、生动。",art!=null?art.originalDraft:null,
            new[]{review?"看完了":"看完，继续听她说"},new Action[]{()=>{
                if(review) UI.CloseDialogue(); else { Progress.ViewOriginalDraft(); Expression(); }
            }});
    }
    void Expression()
    {
        UI.ShowDialogue("sheila","我只是想把它画好一点。\n可现在……我都不知道自己到底在改什么了。",sheila.portrait,new[]{"我在听。"},new Action[]{()=>{
            if(!Progress.RevealPowder()) { UI.CloseDialogue(); return; }
            UI.ShowDialogue("抹茶云","有些绿色的小颗粒，正慢慢聚起来。\n按 Q 开启云视，看看 sheila 的课桌旁吧。\n灰云还在那里。我们先把这份风味收好。",art!=null?art.cloudTalking:scene.cloudPortrait,
                new[]{"去看看"},new Action[]{UI.CloseDialogue});
        }});
    }
}
