using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class MatchaDessertSetup
{
    const string Request="Library/MatchaDessertSetup.request";
    static MatchaDessertSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Install(); } catch(Exception e) { File.WriteAllText("Library/MatchaDessertSetup.report.txt","FAIL\n"+e); Debug.LogException(e); }
    }
    static void Install()
    {
        string path="Assets/ChapterOne/Art/Step07/MatchaDesserts.png";
        ChapterOneConversationSetup.Cut(path,1024,new[]{("MatchaCake",248,140,429,356),("MatchaMacaron",913,188,364,308),("MatchaCreamPuff",248,620,442,296),("MatchaTart",910,596,392,320)});
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
        var catalogPath="Assets/ChapterOne/Resources/MatchaDessertCatalog.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<MatchaDessertCatalog>(catalogPath);
        if(catalog==null) { catalog=ScriptableObject.CreateInstance<MatchaDessertCatalog>(); AssetDatabase.CreateAsset(catalog,catalogPath); }
        catalog.desserts=new[]{
            new MatchaDessertDefinition{id="matcha_cake",displayName="抹茶蛋糕",sprite=sprites["MatchaCake"],availableAtStart=true},
            new MatchaDessertDefinition{id="matcha_macaron",displayName="抹茶马卡龙",sprite=sprites["MatchaMacaron"],storyCharacterId="NPC_Xiaoyu",storyCharacterName="sheila"},
            new MatchaDessertDefinition{id="matcha_cream_puff",displayName="抹茶泡芙",sprite=sprites["MatchaCreamPuff"],storyCharacterId="NPC_Classmate01",storyCharacterName="Elaine"},
            new MatchaDessertDefinition{id="matcha_tart",displayName="抹茶塔",sprite=sprites["MatchaTart"],storyCharacterId="NPC_Classmate02",storyCharacterName="nate"}};
        EditorUtility.SetDirty(catalog);
        var previous=SceneManager.GetActiveScene(); var shop=SceneManager.GetSceneByPath("Assets/Scenes/MochaStore.unity"); bool opened=!shop.isLoaded;
        if(opened) shop=EditorSceneManager.OpenScene("Assets/Scenes/MochaStore.unity",OpenSceneMode.Additive);
        try
        {
            var root=shop.GetRootGameObjects().Single(g=>g.name=="Room1Grid").transform;
            var counter=root.Find("DepthSortedFurniture/FrontCounter");
            var original=shop.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c is Transform||c is Renderer||c is Tilemap||c is Collider2D||c is Camera||c is TopDownCharacterController||c is TopDownCameraFollow).ToArray();
            var before=original.Select(State).ToArray();
            var cake=counter.Find("StarterMatchaCake");
            if(cake==null) { var go=new GameObject("StarterMatchaCake"); SceneManager.MoveGameObjectToScene(go,shop); cake=go.transform; cake.SetParent(counter,false); }
            cake.position=new Vector3(4.3f,.72f,0); cake.localScale=Vector3.one*(1.05f/sprites["MatchaCake"].bounds.size.x);
            var sr=cake.GetComponent<SpriteRenderer>(); if(sr==null) sr=cake.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite=sprites["MatchaCake"]; sr.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/学校改色/SchoolSpriteUnlit.mat"); sr.sortingOrder=5;
            for(int i=0;i<original.Length;i++) if(original[i].transform!=cake&&before[i]!=State(original[i])) throw new Exception("Original scene component changed: "+original[i].name);
            EditorSceneManager.MarkSceneDirty(shop); if(!EditorSceneManager.SaveScene(shop)) throw new IOException("Shop save failed");
            // Render only the shop; additive school geometry must not appear in the preview.
            var otherRoots=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=shop).SelectMany(s=>s.GetRootGameObjects()).Where(g=>g.activeSelf).ToArray();
            foreach(var go in otherRoots) go.SetActive(false);
            try { typeof(ChapterOneSetup).GetMethod("CaptureShop",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{root,"Recovery/ChapterOne/Step07/shop-dessert-overview.png"}); }
            finally { foreach(var go in otherRoots) go.SetActive(true); }
            string report="PASS Four native Sprite Editor slices and approved story mappings saved.\nPASS Starter cake displayed inside existing glass case; "+original.Length+" existing scene components preserved.\n";
            File.WriteAllText("Library/MatchaDessertSetup.report.txt",report); File.WriteAllText("Recovery/ChapterOne/Step07/dessert-preservation.txt",report);
        }
        finally { if(opened) EditorSceneManager.CloseScene(shop,true); SceneManager.SetActiveScene(previous); }
        AssetDatabase.SaveAssets();
    }
    static string State(Component c)=>c is Transform t?t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf:EditorJsonUtility.ToJson(c);
}
