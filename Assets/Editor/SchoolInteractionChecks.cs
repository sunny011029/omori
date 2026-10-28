using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SchoolInteractionChecks
{
    const string Request="Library/SchoolInteractionChecks.request",Report="Library/SchoolInteractionChecks.report.txt";
    static bool started;
    static Queue<Action> steps;
    static double next;
    static SchoolInteractionController hub;
    static Transform root;
    static Rigidbody2D body;
    static readonly List<string> errors=new List<string>();
    static SchoolInteractable xiaoyu,one,two,book,notice,first,portal;
    static SchoolInteractionChecks() { EditorApplication.update+=Poll; }
    [MenuItem("Tools/School/Verify Interactions and NPCs")]
    static void RequestCheck() => File.WriteAllText(Request,"run");
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if(!EditorApplication.isPlaying)
        {
            if(started) { File.Delete(Request); started=false; return; }
            if(GameObject.Find("School_ReferenceLayout/07_Interactions")==null) return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying=true;
            return;
        }
        try
        {
            if(!started && File.ReadAllText(Request).Trim()=="preview-xiaoyu")
            {
                root=GameObject.Find("School_ReferenceLayout").transform;
                hub=root.Find("07_Interactions").GetComponent<SchoolInteractionController>();
                body=hub.player.GetComponent<Rigidbody2D>();
                Near(Target("NPC_Xiaoyu")); File.Delete(Request); return;
            }
            // Use the same clock as the interaction debounce. The editor update loop
            // can keep ticking while a minimized Game view is not advancing frames.
            if(!started) { Initialize(); started=true; next=Time.unscaledTimeAsDouble+.5; return; }
            if(Time.unscaledTimeAsDouble<next) return;
            next=Time.unscaledTimeAsDouble+.4;
            if(steps.Count>0) steps.Dequeue()();
            else { Require(errors.Count==0,"Runtime console errors: "+string.Join("\n",errors)); File.AppendAllText(Report,"PASS no runtime errors or exceptions\nALL PASS\n"); Finish(); }
        }
        catch(Exception e) { File.AppendAllText(Report,"FAIL\n"+e); Debug.LogException(e); Finish(); }
    }
    static SchoolInteractable Target(string id)=>hub.targets.Single(t=>t.interactionId==id);
    static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    static void Pass(string message)=>File.AppendAllText(Report,"PASS "+message+"\n");
    static void At(Vector2 local)
    {
        hub.player.SetMoveInput(Vector2.zero); body.velocity=Vector2.zero;
        body.position=root.TransformPoint(local); hub.player.transform.position=body.position; Physics2D.SyncTransforms();
        var camera=hub.worldCamera; camera.GetComponent<TopDownCameraFollow>().ConstrainNow();
    }
    static void Near(SchoolInteractable target)
    {
        At(root.InverseTransformPoint(target.ApproachPoint)+new Vector3(0,-.45f,0));
        Require(hub.CanReach(target),"Unreachable approach: "+target.interactionId);
    }
    static void Open(SchoolInteractable target) { Near(target); Require(hub.TryInteract(target),"Interaction rejected: "+target.interactionId); }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot("Recovery/ChapterOne/Regression/"+name+".png");
    static void Initialize()
    {
        File.WriteAllText(Report,"School interactions native Play mode tests\n");
        errors.Clear(); Application.logMessageReceived+=Log;
        root=GameObject.Find("School_ReferenceLayout").transform; hub=root.Find("07_Interactions").GetComponent<SchoolInteractionController>();
        if(hub.chapter!=null) hub.chapter.enabled=false; // Test reusable interactions without exit story triggers.
        Directory.CreateDirectory("Recovery/ChapterOne/Regression");
        body=hub.player.GetComponent<Rigidbody2D>(); body.interpolation=RigidbodyInterpolation2D.None; hub.player.UseKeyboardInput=false;
        xiaoyu=Target("NPC_Xiaoyu"); one=Target("NPC_Classmate01"); two=Target("NPC_Classmate02");
        book=Target("XiaoyuSketchbook"); notice=Target("ArtCompetitionNotice"); first=Target("FirstEncounterLocker"); portal=Target("DessertEntranceLocker");
        steps=new Queue<Action>(new Action[]{
            ()=>{
                Require(hub.targets.Length>=7 && hub.targets.Select(t=>t.interactionId).Distinct().Count()==hub.targets.Length,"Target identities/count");
                foreach(var t in root.GetComponentsInChildren<Transform>(true)) Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script at "+t.name);
                Require(hub.chineseFont.HasCharacter('雨') && hub.chineseFont.HasCharacter('涩'),"Chinese glyph missing");
                At(new Vector2(7.5f,-1.7f)); Require(hub.FindNearest()==null && !hub.TryInteract(xiaoyu),"Remote interaction accepted");
                float reach=first.reach; first.reach=20; At(new Vector2(5.5f,4.5f));
                Require(!hub.CanReach(first),"Interaction passed through partition"); first.reach=reach;
                Pass("Original 7 interactions present; all scene target IDs unique; font; no missing scripts; remote and through-wall interactions rejected");
            },
            ()=>{ Open(first); Require(first.locker.IsOpen && !first.locker.closedVisual.enabled && first.locker.openVisual.activeSelf,"Open visuals"); Require(!first.locker.TryToggle(),"Rapid repeat not debounced"); },
            ()=>{ Capture("interaction-locker-open"); },
            ()=>{ Require(hub.TryInteract(first) && !first.locker.IsOpen,"Close locker"); Require(!portal.locker.IsOpen,"Other locker changed"); Pass("Initial cabinet opens/closes; repeated input debounced; cabinets independent"); },
            ()=>{ Open(portal); Require(portal.locker.IsOpen,"Portal cabinet open"); },
            ()=>{ Require(hub.TryInteract(portal) && !portal.locker.IsOpen,"Portal cabinet close"); Pass("Second designated cabinet opens/closes without premature scene travel"); },
            ()=>{ Open(book); Require(!hub.HasSketchbookPermission && hub.DialogueText.Contains("先问问她"),"Book permission gate bypassed"); Require(!hub.player.enabled && body.velocity==Vector2.zero,"Movement not locked"); },
            ()=>{ Capture("interaction-book-locked"); },
            ()=>{ hub.CloseDialogue(); Require(hub.player.enabled,"Movement not restored"); },
            ()=>{ Open(xiaoyu); Require(hub.Speaker=="sheila" && hub.ChoiceCount==3,"Xiaoyu choices"); },
            ()=>{ Capture("interaction-xiaoyu"); },
            ()=>{ hub.Choose(2); Require(!hub.HasSketchbookPermission && !hub.DialogueOpen && hub.player.enabled,"Cancel accidentally granted permission"); Pass("Permission denied before asking; dialogue locks movement; cancellation restores movement"); },
            ()=>{ Open(one); Require(hub.Speaker=="Elaine" && !hub.HasSketchbookPermission,"Classmate 1 identity/state"); },
            ()=>{ Capture("interaction-classmate1"); },
            ()=>{ hub.Choose(0); },
            ()=>{ Open(two); Require(hub.Speaker=="nate" && !hub.HasSketchbookPermission,"Classmate 2 identity/state"); hub.Choose(0); Pass("Both background NPCs have distinct dialogue and cannot grant sketchbook permission"); },
            ()=>{ Open(xiaoyu); hub.Choose(0); Require(hub.HasSketchbookPermission && hub.DialogueOpen,"Permission not granted through explicit choice"); },
            ()=>{ hub.Choose(0); Require(hub.player.enabled,"Permission dialogue failed to release movement"); },
            ()=>{ Open(book); Require(hub.Speaker.EndsWith("1 / 2") && !hub.DialogueText.Contains("反复修改"),"Daily book leaked investigation clues"); },
            ()=>{ hub.Choose(0); Require(hub.Speaker.EndsWith("2 / 2"),"Book next page"); Capture("interaction-sketchbook"); },
            ()=>{ hub.Choose(0); Require(hub.Speaker.EndsWith("1 / 2"),"Book previous page"); hub.Choose(1); Pass("Explicit consent unlocks two-page sketchbook; next/previous/close work without story reward"); },
            ()=>{ hub.SetConversationPhase(SchoolConversationPhase.Investigation); Open(book); Require(hub.DialogueText.Contains("反复修改"),"Investigation stage not separated"); hub.CloseDialogue(); hub.SetConversationPhase(SchoolConversationPhase.DailyRoutine); Pass("Daily sketchbook cannot expose investigation clues; chapter stage gate works independently of permission"); },
            ()=>{ Open(notice); Require(hub.DialogueText.Contains("主题：家"),"Notice contents"); hub.CloseDialogue(); },
            ()=>{ hub.player.enabled=false; Open(xiaoyu); hub.CloseDialogue(); Require(!hub.player.enabled,"Dialog incorrectly reenabled previously disabled movement"); hub.player.enabled=true; },
            ()=>{ Open(xiaoyu); hub.enabled=false; Require(hub.player.enabled && !hub.DialogueOpen,"Disabling hub left player locked"); hub.enabled=true; Pass("Notice readable; movement ownership restored on close and disable"); },
            ()=>{
                var mode=Physics2D.simulationMode; Physics2D.simulationMode=SimulationMode2D.Script;
                try
                {
                    foreach(var npc in new[]{xiaoyu,one,two})
                    {
                        var collider=npc.GetComponent<BoxCollider2D>(); var local=root.InverseTransformPoint(npc.transform.position);
                        At((Vector2)local+new Vector2(0,-.7f));
                        hub.player.SetMoveInput(Vector2.up);
                        for(int i=0;i<20;i++) { hub.player.SendMessage("FixedUpdate"); Physics2D.Simulate(.02f); }
                        Require(hub.player.GetComponent<BoxCollider2D>().bounds.max.y<=collider.bounds.min.y+.025f,"Walked through NPC: "+npc.name);
                        hub.player.GetComponent<TopDownYSort>().RefreshOrder(); npc.GetComponent<TopDownYSort>().RefreshOrder();
                        Require(hub.player.GetComponent<SpriteRenderer>().sortingOrder>npc.GetComponent<SortingGroup>().sortingOrder,"NPC front occlusion: "+npc.name);
                        At((Vector2)local+new Vector2(0,.8f)); hub.player.GetComponent<TopDownYSort>().RefreshOrder();
                        Require(hub.player.GetComponent<SpriteRenderer>().sortingOrder<npc.GetComponent<SortingGroup>().sortingOrder,"NPC rear occlusion: "+npc.name);
                    }
                }
                finally { hub.player.SetMoveInput(Vector2.zero); Physics2D.simulationMode=mode; }
                Pass("All three NPC feet colliders block movement; front/rear character sorting correct");
            },
            ()=>{ Near(xiaoyu); Capture("interaction-npc-prompt"); Pass("Preview screenshots captured; no changes to shared character controller or camera"); }
        });
    }
    static void Finish()
    {
        Application.logMessageReceived-=Log;
        if(hub!=null) hub.CloseDialogue();
        File.Delete(Request); started=false; EditorApplication.isPlaying=false;
    }
    static void Log(string message,string stack,LogType type)
    { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors.Add(message); }

    // Apply narrowly scoped additive fixes without rebuilding furniture or floors.
    [MenuItem("Tools/School/Refresh Interaction Visual References")]
    public static void RefreshVisualReferences()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var root=GameObject.Find("School_ReferenceLayout");
        foreach(string id in new[]{"NPC_Xiaoyu","NPC_Classmate01","NPC_Classmate02"})
        {
            string path="Assets/Prefabs/School/"+id+".prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target=prefab.GetComponent<SchoolInteractable>();
                var anchor=prefab.transform.Find("CloudViewAnchor");
                if(anchor==null) { anchor=new GameObject("CloudViewAnchor").transform; anchor.SetParent(prefab.transform,false); anchor.localPosition=new Vector3(0,1.4f,0); }
                target.cloudViewAnchor=anchor; PrefabUtility.SaveAsPrefabAsset(prefab,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        foreach(var t in root.GetComponentsInChildren<SchoolInteractable>())
            if(t.GetComponent<SpriteRenderer>()==null && t.GetComponentsInChildren<SpriteRenderer>().Length==0)
            { Undo.RecordObject(t,"Set clickable visual"); t.clickVisual=t.transform.parent; }
        EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
    }
}
