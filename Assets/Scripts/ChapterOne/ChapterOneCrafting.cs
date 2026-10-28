using System;
using System.Linq;
using UnityEngine;

// Appended save fields keep all previous chapter and NPC identifiers stable.
public enum MatchaCraftStage { None, SelectSweetness, SelectFilling, AddPowder, Stirring, ReadyToBake, Baking, ReadyToPack, Packed }
public enum MatchaSweetness { Balanced, Low, High }
public enum MatchaFilling { None, Milk, RedBean }

// One tutorial batch, with nine equally valid flavours. Sharing is a separate story beat.
public sealed class ChapterOneCrafting
{
    public const double BakeSeconds=3;
    readonly ChapterOneScene scene;
    readonly Sprite macaron;
    double nextBakeAttempt;
    int displayedSeconds=-1;
    SchoolInteractionController UI => scene.interaction;
    ChapterOneProgress P => scene.Progress;
    public ChapterOneCrafting(ChapterOneScene scene)
    {
        this.scene=scene;
        var catalog=Resources.Load<MatchaDessertCatalog>("MatchaDessertCatalog");
        macaron=catalog?.desserts?.FirstOrDefault(d=>d.id=="matcha_macaron")?.sprite;
    }
    public static string SweetnessName(MatchaSweetness value) => value==MatchaSweetness.Low?"低甜":value==MatchaSweetness.High?"高甜":"均衡";
    public static string FillingName(MatchaFilling value) => value==MatchaFilling.Milk?"牛奶":value==MatchaFilling.RedBean?"红豆":"不添加";
    public string Flavour => SweetnessName(P.Data.sweetness)+" · "+FillingName(P.Data.filling);
    public double BakeRemaining => Math.Max(0,(P.Data.bakeReadyUtcTicks-DateTime.UtcNow.Ticks)/(double)TimeSpan.TicksPerSecond);
    public string Objective => P.DessertPacked ? "抹茶马卡龙已装盒 · 按 I 查看，可带回学校" :
        scene.isSchool ? (P.PowderInBatch?"甜品留在制作台上 · 返回甜品店继续制作":"带上微涩的抹茶粉，回甜品店制作马卡龙") :
        P.Data.craftStage==MatchaCraftStage.None?"原料已备好 · 到右侧制作台制作抹茶马卡龙":
        P.Data.craftStage==MatchaCraftStage.ReadyToPack?"马卡龙已经烤好 · 到制作台装盘、放入便携盒":"制作会自动保存 · 到右侧制作台继续";
    public string ProgressLabel => P.DessertPacked?"便携盒 · 抹茶马卡龙 × 1":P.PowderInBatch?"制作中 · 抹茶马卡龙":"微涩的抹茶粉 × 1";

    public void Tick()
    {
        if(P.Data.craftStage!=MatchaCraftStage.Baking) return;
        bool showing=UI.DialogueOpen && UI.Speaker=="制作 · 烘焙";
        if(BakeRemaining<=0 && Time.unscaledTimeAsDouble>=nextBakeAttempt)
        {
            nextBakeAttempt=Time.unscaledTimeAsDouble+2; // Do not repeatedly write on storage failure.
            if(P.FinishBaking() && showing) OpenTable();
        }
        else if(showing && displayedSeconds!=(int)Math.Ceiling(BakeRemaining)) OpenTable();
    }
    public void OpenTable()
    {
        if(scene.isSchool) return;
        P.InspectCraftTable();
        if(!P.Data.powderCollected)
        {
            UI.ShowDialogue("制作台","还缺少特殊原料：微涩的抹茶粉。\n现在无法制作，先回学校看看吧。",null,new[]{"记住了"},new Action[]{UI.CloseDialogue}); return;
        }
        switch(P.Data.craftStage)
        {
            case MatchaCraftStage.None:
                Show("制作台","抹茶马卡龙 · 微涩的抹茶粉 × 1，已经备好。\n基础食材由店里提供。先选一种你想做的味道吧。",
                    new[]{"开始制作","稍后再来"},new Action[]{()=>Act(P.BeginCrafting),UI.CloseDialogue}); break;
            case MatchaCraftStage.SelectSweetness:
                Show("制作 · 选择甜度","第一份甜品是抹茶马卡龙。\n低甜、均衡、高甜，各有自己的味道。",
                    new[]{"低甜","均衡","高甜"},new Action[]{()=>Act(()=>P.SelectSweetness(MatchaSweetness.Low)),()=>Act(()=>P.SelectSweetness(MatchaSweetness.Balanced)),()=>Act(()=>P.SelectSweetness(MatchaSweetness.High))}); break;
            case MatchaCraftStage.SelectFilling:
                Show("制作 · 选择辅助食材","已选甜度："+SweetnessName(P.Data.sweetness)+"。\n加入牛奶或红豆，也可以保留原本的抹茶风味。",
                    new[]{"牛奶","红豆","不添加"},new Action[]{()=>Act(()=>P.SelectFilling(MatchaFilling.Milk)),()=>Act(()=>P.SelectFilling(MatchaFilling.RedBean)),()=>Act(()=>P.SelectFilling(MatchaFilling.None))}); break;
            case MatchaCraftStage.AddPowder:
                Show("制作 · 加入抹茶粉",Flavour+" · 抹茶马卡龙\n加入微涩的抹茶粉 × 1，开始这一份甜品。\n还没加入原料，可以重新选择口味。",
                    new[]{"加入微涩的抹茶粉","重新选择口味","稍后再来"},new Action[]{()=>Act(P.AddCraftPowder),()=>Act(P.ReviseRecipe),UI.CloseDialogue}); break;
            case MatchaCraftStage.Stirring:
                Show("制作 · 搅拌",Flavour+"\n轻轻搅拌，让抹茶粉与面糊混合。\n已搅拌 "+P.Data.stirCount+" / 3 次。",
                    new[]{"搅拌一次","先放一会儿"},new Action[]{()=>Act(P.StirDessert),UI.CloseDialogue},P.Data.stirCount/3f); break;
            case MatchaCraftStage.ReadyToBake:
                Show("制作 · 准备烘焙","面糊已经混合均匀，挤成小圆饼。\n将它们送入烤箱，等待香气慢慢散出来。",
                    new[]{"开始烘焙","稍后继续"},new Action[]{()=>Act(P.BeginBaking),UI.CloseDialogue},1); break;
            case MatchaCraftStage.Baking:
                displayedSeconds=(int)Math.Ceiling(BakeRemaining);
                Show("制作 · 烘焙","抹茶的香气慢慢散开……\n约 "+displayedSeconds+" 秒后烤好。可以先离开，烤好后会等你回来。",
                    new[]{"先离开制作台"},new Action[]{UI.CloseDialogue},1-(float)(BakeRemaining/BakeSeconds)); break;
            case MatchaCraftStage.ReadyToPack:
                Show("制作 · 烤好了",Flavour+" · 抹茶马卡龙\n圆饼已经烤好、放凉，夹上馅料。\n把这份甜品装盘，放进便携盒吧。",
                    new[]{"装盘并放入便携盒","稍后装盒"},new Action[]{()=>Act(P.PackDessert),UI.CloseDialogue},1); break;
            case MatchaCraftStage.Packed: ShowPacked(); break;
        }
    }
    void Act(Func<bool> action)
    {
        if(action()) OpenTable();
        else if(!string.IsNullOrEmpty(P.SaveError))
            UI.ShowDialogue("这一步暂未保存","原料和进度仍停留在上一步。\n检查存储空间后，可以重新打开制作台继续。",null,new[]{"稍后继续"},new Action[]{UI.CloseDialogue});
        else OpenTable(); // A stale/out-of-order action cannot consume ingredients.
    }
    void Show(string title,string text,string[] labels,Action[] actions,float progress=-1)
    { UI.ShowIllustration(title,text,macaron,labels,actions); UI.SetIllustrationProgress(progress); }
    void ShowPacked()
    {
        if(P.Data.dessertConsumed)
        {
            Show("抹茶马卡龙 · 已分享",Flavour+"\n微涩的抹茶粉已经用在分享给 sheila 的甜品里。\n"+(P.LocalAreaComplete?"这次的食谱已收录，可以从 J 记录中查看。":"回到学校，继续听 sheila 把话说完吧。"),
                new[]{P.LocalAreaComplete?"查看甜品收藏":"离开制作台"},new Action[]{()=>{ if(P.LocalAreaComplete) scene.Ending.ShowRecipes(1); else UI.CloseDialogue(); }}); return;
        }
        Show("抹茶马卡龙 · 已装盒",Flavour+" · 便携盒 × 1\n微涩的抹茶粉已经用在这份甜品里。\n按 I 可查看成品，带着它回学校吧。",
            new[]{"收好便携盒"},new Action[]{UI.CloseDialogue});
    }
    public bool ShowInventory()
    {
        if(P.Data.dessertConsumed)
        {
            Show("物品 · 甜品已分享",Flavour+" · 抹茶马卡龙\n便携盒里的这一份已经分享给 sheila，随身数量为 0。\n"+(P.LocalAreaComplete?"食谱已收录在 J 记录的甜品收藏里。":"她的故事还在继续。"),
                new[]{"收起物品栏"},new Action[]{UI.CloseDialogue}); return true;
        }
        if(P.DessertPacked)
        {
            Show("物品 · 抹茶马卡龙 × 1",Flavour+"\n放在便携盒里的一份甜品，准备带给 sheila。\n这份口味已经记下，微涩的抹茶粉已用于制作。",
                new[]{"收起物品栏"},new Action[]{UI.CloseDialogue}); return true;
        }
        if(!P.PowderInBatch) return false;
        Show("物品 · 制作中的甜品",Flavour+" · 抹茶马卡龙\n微涩的抹茶粉已经加入这份甜品，留在店内制作台。\n回到制作台继续，装盒后才能随身携带。",
            new[]{"收起物品栏"},new Action[]{UI.CloseDialogue}); return true;
    }
    public static bool IsValidSave(ChapterOneSave data)
    {
        if(!Enum.IsDefined(typeof(MatchaCraftStage),data.craftStage) || !Enum.IsDefined(typeof(MatchaSweetness),data.sweetness) ||
            !Enum.IsDefined(typeof(MatchaFilling),data.filling)) return false;
        if(data.craftStage!=MatchaCraftStage.None && (!data.powderCollected || data.phase<ChapterOnePhase.ShopIntroduced || data.investigationMask!=15)) return false;
        if(data.craftStage<=MatchaCraftStage.SelectSweetness && (data.sweetness!=MatchaSweetness.Balanced || data.filling!=MatchaFilling.None)) return false;
        if(data.craftStage==MatchaCraftStage.SelectFilling && data.filling!=MatchaFilling.None) return false;
        if(data.craftStage<MatchaCraftStage.Stirring && data.stirCount!=0) return false;
        if(data.craftStage==MatchaCraftStage.Stirring && (data.stirCount<0 || data.stirCount>2)) return false;
        if(data.craftStage>=MatchaCraftStage.ReadyToBake && data.stirCount!=3) return false;
        if(data.craftStage<MatchaCraftStage.Baking && data.bakeReadyUtcTicks!=0) return false;
        if(data.craftStage>=MatchaCraftStage.Baking && (data.bakeReadyUtcTicks<=0 || data.bakeReadyUtcTicks>DateTime.MaxValue.Ticks)) return false;
        return true;
    }
}
