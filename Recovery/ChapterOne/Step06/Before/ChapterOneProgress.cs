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
        if(chapterTest || interactionTest || movementTest)
        {
            SavePath=Path.Combine(project,"Temp/ChapterOneTests/progress.json");
            // Editor verification starts with isolated state and never touches the player's save.
            if(chapterTest && File.ReadAllText(Path.Combine(project,"Library/ChapterOneChecks.request")).Trim().StartsWith("resume-",StringComparison.Ordinal))
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
        if(Data.phase!=ChapterOnePhase.AnomalyResolved || index<0 || index>=3 || IsFinalPaperClean(index)) return false;
        Data.finalPaperMask|=1<<index; Commit(); return true;
    }
    public void GrantSketchbookPermission()
    { if(!Data.sketchbookPermission) { Data.sketchbookPermission=true; Commit(); } }
    public void InspectCraftTable()
    { if(!Data.inspectedCraftTable && Data.phase>=ChapterOnePhase.CloudMet) { Data.inspectedCraftTable=true; Commit(); } }
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
            string temp=SavePath+".tmp"; File.WriteAllText(temp,JsonUtility.ToJson(Data,true));
            if(File.Exists(SavePath)) File.Replace(temp,SavePath,SavePath+".bak");
            else File.Move(temp,SavePath);
        }
        catch(Exception e) when(e is IOException || e is UnauthorizedAccessException)
        { SaveError="进度暂未保存，请检查存储空间"; Debug.LogWarning("Chapter save: "+e.Message); }
        Changed?.Invoke();
    }
    static int CountBits(int bits) { int count=0; while(bits!=0) { count+=bits&1; bits>>=1; } return count; }
}
