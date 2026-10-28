using System;
using System.IO;
using UnityEngine;

public enum ChapterOnePhase { Cleaning, ReadyToLeave, Anomaly, SeekingTool, CloudMet, ShopIntroduced, AnomalyResolved }

[Serializable]
public sealed class ChapterOneSave
{
    public int version=1;
    public int cleanedMask;
    public int finalPaperMask;
    public ChapterOnePhase phase;
    public bool sketchbookPermission;
    public bool cloudVisionUnlocked;
    public bool inspectedCraftTable;
    // New fields default to zero when loading saves from the first playable section.
    public int investigationMask;
    public int backgroundNotesMask;
    public bool reassuranceFinished;
    public int drawingQuestionsMask;
    public bool originalDraftViewed;
    public bool powderRevealed;
    public bool powderCollected;
    public MatchaCraftStage craftStage;
    public MatchaSweetness sweetness;
    public MatchaFilling filling;
    public int stirCount;
    public long bakeReadyUtcTicks;
    public ChapterOneEndingStage endingStage;
    public bool dessertConsumed;
    public int draftReply;
    public string scene="School";
}

// The only persistent object. Scene-specific players, cameras and UI are never carried over.
public sealed class ChapterOneProgress : MonoBehaviour
{
    public static ChapterOneProgress Instance { get; private set; }
    public ChapterOneSave Data { get; private set; } = new ChapterOneSave();
    public string SavePath { get; private set; }
    public string SaveError { get; private set; }
    public string PendingScene { get; set; }
    public bool ResumePending { get; private set; }
    public bool StartingNewGame { get; private set; }
    public event Action Changed;
    public int CleanCount => CountBits(Data.cleanedMask & 15);
    public bool FirstSectionComplete => Data.phase>=ChapterOnePhase.ShopIntroduced && Data.inspectedCraftTable;
    public bool InvestigationUnlocked => Data.phase>=ChapterOnePhase.ShopIntroduced && Data.cloudVisionUnlocked;
    public int InvestigationCount => CountBits(Data.investigationMask & 15);
    public bool InvestigationComplete => InvestigationUnlocked && InvestigationCount==4;
    public bool HasClue(ChapterOneClue clue) => (Data.investigationMask & (int)clue)!=0;
    public bool DrawingConversationReady => InvestigationComplete && Data.phase==ChapterOnePhase.ShopIntroduced;
    public bool DessertPacked => Data.craftStage==MatchaCraftStage.Packed;
    public bool HasDessert => DessertPacked && !Data.dessertConsumed;
    public bool DraftKept => Data.endingStage>=ChapterOneEndingStage.CloudQuestion;
    public int FinalPaperCount => CountBits(Data.finalPaperMask & 7);
    public bool LocalAreaComplete => Data.endingStage>=ChapterOneEndingStage.Checklist;
    public bool ChapterComplete => Data.endingStage==ChapterOneEndingStage.Complete;
    public bool HasLoosePowder => Data.powderCollected && Data.craftStage<MatchaCraftStage.Stirring;
    public bool PowderInBatch => Data.craftStage>=MatchaCraftStage.Stirring && !DessertPacked;

    public static ChapterOneProgress Ensure()
    {
        if(Instance!=null) return Instance;
        return new GameObject("ChapterOneProgress").AddComponent<ChapterOneProgress>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() => Instance=null;
    void Awake()
    {
        if(Instance!=null && Instance!=this) { Destroy(gameObject); return; }
        Instance=this; DontDestroyOnLoad(gameObject);
        SavePath=Path.Combine(Application.persistentDataPath,"MatchaCloud","chapter01.json");
#if UNITY_EDITOR
        string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
        bool chapterTest=File.Exists(Path.Combine(project,"Library/ChapterOneChecks.request"));
        bool interactionTest=File.Exists(Path.Combine(project,"Library/SchoolInteractionChecks.request"));
        bool movementTest=File.Exists(Path.Combine(project,"Library/SchoolGameplayChecks.request"));
        bool investigationTest=File.Exists(Path.Combine(project,"Library/ChapterOneInvestigationChecks.request"));
        bool conversationTest=File.Exists(Path.Combine(project,"Library/ChapterOneConversationChecks.request"));
        bool craftingTest=File.Exists(Path.Combine(project,"Library/ChapterOneCraftingChecks.request"));
        bool endingTest=File.Exists(Path.Combine(project,"Library/ChapterOneEndingChecks.request"));
        if(chapterTest || interactionTest || movementTest || investigationTest || conversationTest || craftingTest || endingTest)
        {
            SavePath=Path.Combine(project,"Temp/ChapterOneTests/progress.json");
            // Editor verification starts with isolated state and never touches the player's save.
            if(chapterTest && File.ReadAllText(Path.Combine(project,"Library/ChapterOneChecks.request")).Trim().StartsWith("resume-",StringComparison.Ordinal))
            { Reload(); ResumePending=File.Exists(SavePath); }
            if(investigationTest && File.ReadAllText(Path.Combine(project,"Library/ChapterOneInvestigationChecks.request")).Trim()=="resume")
            { Reload(); ResumePending=File.Exists(SavePath); }
            if(conversationTest && File.ReadAllText(Path.Combine(project,"Library/ChapterOneConversationChecks.request")).Trim().StartsWith("resume",StringComparison.Ordinal))
            { Reload(); ResumePending=File.Exists(SavePath); }
            if(craftingTest && File.ReadAllText(Path.Combine(project,"Library/ChapterOneCraftingChecks.request")).Trim().StartsWith("resume",StringComparison.Ordinal))
            { Reload(); ResumePending=File.Exists(SavePath); }
            if(endingTest && File.ReadAllText(Path.Combine(project,"Library/ChapterOneEndingChecks.request")).Trim().StartsWith("resume",StringComparison.Ordinal))
            { Reload(); ResumePending=File.Exists(SavePath); }
            return;
        }
#endif
        Reload(); ResumePending=File.Exists(SavePath);
    }
    void OnDestroy() { if(Instance==this) Instance=null; }
    public bool ConsumeResume()
    { bool pending=ResumePending; ResumePending=false; return pending; }
    public bool IsClean(int index) => index>=0 && index<4 && (Data.cleanedMask & (1<<index))!=0;
    public bool IsFinalPaperClean(int index) => index>=0 && index<3 && (Data.finalPaperMask & (1<<index))!=0;
    public bool CompleteClean(int index)
    {
        if(index<0 || index>=4 || IsClean(index) || Data.phase>ChapterOnePhase.ReadyToLeave) return false;
        Data.cleanedMask|=1<<index;
        if(CleanCount==4) Data.phase=ChapterOnePhase.ReadyToLeave;
        Commit(); return true;
    }
    public bool TriggerAnomaly()
    {
        if(Data.phase!=ChapterOnePhase.ReadyToLeave || CleanCount!=4) return false;
        Data.phase=ChapterOnePhase.Anomaly; Commit(); return true;
    }
    public bool InspectAnomaly()
    {
        if(Data.phase!=ChapterOnePhase.Anomaly) return false;
        Data.phase=ChapterOnePhase.SeekingTool; Commit(); return true;
    }
    public bool MeetCloud()
    {
        if(Data.phase!=ChapterOnePhase.SeekingTool) return false;
        Data.phase=ChapterOnePhase.CloudMet; Commit(); return true;
    }
    public bool IntroduceShop()
    {
        if(Data.phase!=ChapterOnePhase.CloudMet) return false;
        Data.phase=ChapterOnePhase.ShopIntroduced; Data.cloudVisionUnlocked=true; Commit(); return true;
    }
    public bool CompleteFinalPaper(int index)
    {
        if(Data.phase!=ChapterOnePhase.AnomalyResolved || Data.endingStage!=ChapterOneEndingStage.FinalCleaning || index<0 || index>=3 || IsFinalPaperClean(index)) return false;
        return CraftCommit(()=>{
            Data.finalPaperMask|=1<<index;
            if(Data.finalPaperMask==7) Data.endingStage=ChapterOneEndingStage.Checklist;
        });
    }
    public void GrantSketchbookPermission()
    { if(!Data.sketchbookPermission) { Data.sketchbookPermission=true; Commit(); } }
    public void InspectCraftTable()
    { if(!Data.inspectedCraftTable && Data.phase>=ChapterOnePhase.CloudMet) { Data.inspectedCraftTable=true; Commit(); } }
    public bool RecordClue(ChapterOneClue clue)
    {
        if(!InvestigationUnlocked || !Enum.IsDefined(typeof(ChapterOneClue),clue) || HasClue(clue)) return false;
        if(clue==ChapterOneClue.Sketchbook && !Data.sketchbookPermission) return false;
        Data.investigationMask|=(int)clue; Commit(); return true;
    }
    public void RecordBackgroundNote(int index)
    {
        if(!InvestigationUnlocked || index<0 || index>1 || (Data.backgroundNotesMask & (1<<index))!=0) return;
        Data.backgroundNotesMask|=1<<index; Commit();
    }
    public bool FinishReassurance()
    {
        if(!DrawingConversationReady || Data.reassuranceFinished) return false;
        Data.reassuranceFinished=true; Commit(); return true;
    }
    public bool RecordDrawingQuestion(int index)
    {
        if(!DrawingConversationReady || !Data.reassuranceFinished || index<0 || index>2 || (Data.drawingQuestionsMask&(1<<index))!=0) return false;
        Data.drawingQuestionsMask|=1<<index; Commit(); return true;
    }
    public bool ViewOriginalDraft()
    {
        if(!DrawingConversationReady || (Data.drawingQuestionsMask&4)==0 || Data.originalDraftViewed) return false;
        Data.originalDraftViewed=true; Commit(); return true;
    }
    public bool RevealPowder()
    {
        if(!DrawingConversationReady || !Data.originalDraftViewed || Data.powderRevealed) return false;
        Data.powderRevealed=true; Commit(); return true;
    }
    public bool CollectPowder()
    {
        if(!DrawingConversationReady || !Data.powderRevealed || Data.powderCollected) return false;
        Data.powderCollected=true; Commit(); return true;
    }
    bool CanCraft(MatchaCraftStage stage) => DrawingConversationReady && Data.powderCollected && Data.craftStage==stage;
    // Save ingredient, recipe and result together. A failed write leaves the previous step usable.
    bool CraftCommit(Action change)
    {
        var previous=JsonUtility.FromJson<ChapterOneSave>(JsonUtility.ToJson(Data));
        change(); Commit();
        if(string.IsNullOrEmpty(SaveError)) return true;
        Data=previous; Changed?.Invoke(); return false;
    }
    public bool BeginCrafting() => CanCraft(MatchaCraftStage.None) && CraftCommit(()=>Data.craftStage=MatchaCraftStage.SelectSweetness);
    public bool SelectSweetness(MatchaSweetness value) => CanCraft(MatchaCraftStage.SelectSweetness) && Enum.IsDefined(typeof(MatchaSweetness),value) &&
        CraftCommit(()=>{ Data.sweetness=value; Data.craftStage=MatchaCraftStage.SelectFilling; });
    public bool SelectFilling(MatchaFilling value) => CanCraft(MatchaCraftStage.SelectFilling) && Enum.IsDefined(typeof(MatchaFilling),value) &&
        CraftCommit(()=>{ Data.filling=value; Data.craftStage=MatchaCraftStage.AddPowder; });
    public bool ReviseRecipe() => CanCraft(MatchaCraftStage.AddPowder) && CraftCommit(()=>{
        Data.sweetness=MatchaSweetness.Balanced; Data.filling=MatchaFilling.None; Data.craftStage=MatchaCraftStage.SelectSweetness; });
    public bool AddCraftPowder() => CanCraft(MatchaCraftStage.AddPowder) && CraftCommit(()=>Data.craftStage=MatchaCraftStage.Stirring);
    public bool StirDessert() => CanCraft(MatchaCraftStage.Stirring) && CraftCommit(()=>{
        Data.stirCount++; if(Data.stirCount==3) Data.craftStage=MatchaCraftStage.ReadyToBake; });
    public bool BeginBaking() => CanCraft(MatchaCraftStage.ReadyToBake) && CraftCommit(()=>{
        Data.bakeReadyUtcTicks=DateTime.UtcNow.AddSeconds(ChapterOneCrafting.BakeSeconds).Ticks; Data.craftStage=MatchaCraftStage.Baking; });
    public bool FinishBaking() => CanCraft(MatchaCraftStage.Baking) && DateTime.UtcNow.Ticks>=Data.bakeReadyUtcTicks &&
        CraftCommit(()=>Data.craftStage=MatchaCraftStage.ReadyToPack);
    public bool PackDessert() => CanCraft(MatchaCraftStage.ReadyToPack) && CraftCommit(()=>Data.craftStage=MatchaCraftStage.Packed);
    // Completed beats are atomic. In particular, DraftPending is persisted before the
    // animation starts; loading it replays the action instead of resolving off-screen.
    public bool ShareDessert() => DrawingConversationReady && HasDessert && Data.endingStage==ChapterOneEndingStage.None &&
        CraftCommit(()=>{ Data.dessertConsumed=true; Data.endingStage=ChapterOneEndingStage.Tasting; });
    public bool ReflectOnDraft() => Data.endingStage==ChapterOneEndingStage.Tasting &&
        CraftCommit(()=>Data.endingStage=ChapterOneEndingStage.Reflection);
    public bool PrepareToKeepDraft(int reply) => Data.endingStage==ChapterOneEndingStage.Reflection && reply>=0 && reply<=1 &&
        CraftCommit(()=>{ Data.draftReply=reply; Data.endingStage=ChapterOneEndingStage.DraftPending; });
    public bool KeepDraft() => Data.endingStage==ChapterOneEndingStage.DraftPending &&
        CraftCommit(()=>{ Data.endingStage=ChapterOneEndingStage.CloudQuestion; Data.phase=ChapterOnePhase.AnomalyResolved; });
    public bool StartFinalCleaning() => Data.endingStage==ChapterOneEndingStage.CloudQuestion &&
        CraftCommit(()=>Data.endingStage=ChapterOneEndingStage.FinalCleaning);
    public bool FinishChapter() => Data.endingStage==ChapterOneEndingStage.Checklist && Data.finalPaperMask==7 &&
        CraftCommit(()=>Data.endingStage=ChapterOneEndingStage.Complete);
    public void Arrive(string scene)
    { Data.scene=scene; PendingScene=null; StartingNewGame=false; Commit(); }
    public void NewGame()
    { Data=new ChapterOneSave(); ResumePending=false; StartingNewGame=true; PendingScene="School"; Commit(); }
    public void Reload()
    {
        SaveError=null;
        if(TryRead(SavePath,out var data) || TryRead(SavePath+".bak",out data)) Data=data;
        else if(File.Exists(SavePath)) SaveError="上次的进度暂时无法读取";
        Changed?.Invoke();
    }
    static bool TryRead(string path,out ChapterOneSave data)
    {
        data=null;
        try
        {
            if(!File.Exists(path)) return false;
            data=JsonUtility.FromJson<ChapterOneSave>(File.ReadAllText(path));
            if(data==null || data.version!=1 || data.cleanedMask<0 || data.cleanedMask>15 || data.finalPaperMask<0 || data.finalPaperMask>7 ||
                !Enum.IsDefined(typeof(ChapterOnePhase),data.phase) || (data.scene!="School" && data.scene!="MochaStore")) return false;
            if(data.phase>=ChapterOnePhase.ReadyToLeave && data.cleanedMask!=15) return false;
            if(data.phase>=ChapterOnePhase.ShopIntroduced && !data.cloudVisionUnlocked) return false;
            if(data.investigationMask<0 || data.investigationMask>15 || data.backgroundNotesMask<0 || data.backgroundNotesMask>3) return false;
            if((data.investigationMask!=0 || data.backgroundNotesMask!=0) && data.phase<ChapterOnePhase.ShopIntroduced) return false;
            if((data.investigationMask & (int)ChapterOneClue.Sketchbook)!=0 && !data.sketchbookPermission) return false;
            if(data.drawingQuestionsMask<0 || data.drawingQuestionsMask>7) return false;
            if(data.reassuranceFinished && data.investigationMask!=15) return false;
            if(data.drawingQuestionsMask!=0 && !data.reassuranceFinished) return false;
            if(data.originalDraftViewed && (data.drawingQuestionsMask&4)==0) return false;
            if(data.powderRevealed && !data.originalDraftViewed) return false;
            if(data.powderCollected && !data.powderRevealed) return false;
            if(!ChapterOneCrafting.IsValidSave(data)) return false;
            if(!ChapterOneEnding.IsValidSave(data)) return false;
            return true;
        }
        catch(IOException) { return false; }
        catch(ArgumentException) { return false; }
        catch(UnauthorizedAccessException) { return false; }
    }
    public void Commit()
    {
        SaveError=null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temp=SavePath+".tmp",json=JsonUtility.ToJson(Data,true);
            // Windows may briefly hold a just-replaced backup. Keep the atomic replace,
            // retry briefly, and surface a persistent failure without consuming the batch.
            for(int attempt=0;;attempt++)
            {
                try
                {
                    File.WriteAllText(temp,json);
                    if(File.Exists(SavePath)) File.Replace(temp,SavePath,SavePath+".bak");
                    else File.Move(temp,SavePath);
                    break;
                }
                catch(IOException) when(attempt<2) { System.Threading.Thread.Sleep(25); }
            }
        }
        catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
        { SaveError="进度暂未保存，请检查存储空间"; Debug.LogWarning("Chapter save: "+e.Message); }
        Changed?.Invoke();
    }
    static int CountBits(int bits) { int count=0; while(bits!=0) { count+=bits&1; bits>>=1; } return count; }
}
