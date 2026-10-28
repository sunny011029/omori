using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class ChapterOneConversationSetup
{
    const string Request="Library/ChapterOneConversationSetup.request";
    const string Art="Assets/ChapterOne/Art/Step07/";
    static ChapterOneConversationSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var action=File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if(action=="install") Install(); else Inspect(); }
        catch(Exception e) { File.WriteAllText("Library/ChapterOneConversationSetup.report.txt","FAIL\n"+e); Debug.LogException(e); }
    }
    static void Inspect()
    {
        var previous=SceneManager.GetActiveScene(); var shop=EditorSceneManager.OpenScene("Assets/Scenes/MochaStore.unity",OpenSceneMode.Additive);
        try
        {
            var root=shop.GetRootGameObjects().Single(g=>g.name=="Room1Grid").transform;
            typeof(ChapterOneSetup).GetMethod("CaptureShop",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{root,"Recovery/ChapterOne/Step07/shop-overview.png"});
            var b=new StringBuilder();
            foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true)) b.AppendLine(sr.name+" pos="+sr.transform.position+" bounds="+sr.bounds+" sprite="+(sr.sprite!=null?sr.sprite.name:"null")+" sort="+sr.sortingOrder);
            File.WriteAllText("Library/Step07-shop-sprites.txt",b.ToString());
        }
        finally { EditorSceneManager.CloseScene(shop,true); SceneManager.SetActiveScene(previous); }
        File.WriteAllText("Library/ChapterOneConversationSetup.report.txt","INSPECTED");
    }
    public static void Cut(string path,int height,(string,int,int,int,int)[] cuts,int max=256)
    {
        AssetDatabase.ImportAsset(path); var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=max; importer.spritePixelsPerUnit=100;
        importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories(); factories.Init(); var provider=factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous=provider.GetSpriteRects();
        var rects=cuts.Select(c=>new SpriteRect{name=c.Item1,spriteID=previous.FirstOrDefault(r=>r.name==c.Item1)?.spriteID??GUID.Generate(),
            rect=new Rect(c.Item2,height-c.Item3-c.Item5,c.Item4,c.Item5),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}).ToArray();
        provider.SetSpriteRects(rects); provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
    }
    static Sprite Sprite(string file,string name)=>AssetDatabase.LoadAllAssetsAtPath(Art+file).OfType<Sprite>().Single(s=>s.name==name);
    static void Install()
    {
        Directory.CreateDirectory("Recovery/ChapterOne/Step07");
        Cut(Art+"MatchaCloudCompact.png",1086,new[]{("CompactNeutral",141,82,501,384),("CompactClosedEyes",808,82,500,384),("CompactTalking",140,610,501,384),("CompactSurprised",807,610,502,384)});
        Cut(Art+"SheilaOriginalDraft.png",1254,new[]{("SheilaOriginalDraft",0,0,1254,1254)},512);
        Cut(Art+"MatchaPowder.png",1024,new[]{("MatchaPowder",463,217,615,589)});
        Directory.CreateDirectory("Assets/ChapterOne/Resources"); AssetDatabase.Refresh();
        string resource="Assets/ChapterOne/Resources/ChapterOneStoryArt.asset";
        var art=AssetDatabase.LoadAssetAtPath<ChapterOneStoryArt>(resource);
        if(art==null) { art=ScriptableObject.CreateInstance<ChapterOneStoryArt>(); AssetDatabase.CreateAsset(art,resource); }
        art.originalDraft=Sprite("SheilaOriginalDraft.png","SheilaOriginalDraft"); art.matchaPowder=Sprite("MatchaPowder.png","MatchaPowder");
        art.cloudNeutral=Sprite("MatchaCloudCompact.png","CompactNeutral"); art.cloudClosedEyes=Sprite("MatchaCloudCompact.png","CompactClosedEyes");
        art.cloudTalking=Sprite("MatchaCloudCompact.png","CompactTalking"); art.cloudSurprised=Sprite("MatchaCloudCompact.png","CompactSurprised");
        EditorUtility.SetDirty(art);
        string[] ids={"NPC_Xiaoyu","NPC_Classmate01","NPC_Classmate02"},names={"sheila","Elaine","nate"};
        for(int i=0;i<ids.Length;i++)
        {
            string path="Assets/Prefabs/School/"+ids[i]+".prefab"; var prefab=PrefabUtility.LoadPrefabContents(path);
            try { prefab.GetComponent<SchoolInteractable>().displayName=names[i]; PrefabUtility.SaveAsPrefabAsset(prefab,path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        var previous=SceneManager.GetActiveScene(); var report=new StringBuilder("Native Sprite Editor import and targeted scene update\n");
        foreach(var path in new[]{"Assets/Scenes/School.unity","Assets/Scenes/MochaStore.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var chapter=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ChapterOneScene>(true)).Single();
                var cloud=chapter.isSchool?chapter.encounterCloud:chapter.shopCloud;
                var protectedComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
                    .Where(c=>c!=null && c.gameObject!=cloud && (c is Transform || c is SpriteRenderer || c is Tilemap || c is TilemapRenderer || c is Collider2D || c is Camera || c is TopDownCharacterController || c is TopDownCameraFollow)).ToArray();
                var before=protectedComponents.Select(State).ToArray();
                chapter.cloudPortrait=art.cloudNeutral; cloud.GetComponent<SpriteRenderer>().sprite=art.cloudNeutral;
                cloud.transform.localScale=Vector3.one*((chapter.isSchool?.56f:1.25f)/art.cloudNeutral.bounds.size.x);
                foreach(var t in chapter.interaction.targets)
                {
                    int index=Array.IndexOf(ids,t.interactionId); if(index>=0) t.displayName=names[index];
                    if(t.interactionId=="ShopMatchaCloud") t.portrait=art.cloudNeutral;
                    if(PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    EditorUtility.SetDirty(t);
                }
                for(int i=0;i<protectedComponents.Length;i++) if(before[i]!=State(protectedComponents[i])) throw new Exception("Protected original component changed: "+protectedComponents[i].name);
                EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed");
                report.AppendLine("PASS "+scene.name+": compact cloud + names saved; "+protectedComponents.Length+" original layout/visual/collision/player/camera components unchanged.");
            }
            finally { if(opened) EditorSceneManager.CloseScene(scene,true); }
        }
        SceneManager.SetActiveScene(previous); AssetDatabase.SaveAssets();
        report.AppendLine("PASS 4 compact cloud expressions, draft, powder and runtime art references imported.");
        File.WriteAllText("Library/ChapterOneConversationSetup.report.txt",report.ToString());
        File.WriteAllText("Recovery/ChapterOne/Step07/scene-preservation.txt",report.ToString());
    }
    static string State(Component c)
    {
        if(c is Transform t) return t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf;
        return EditorJsonUtility.ToJson(c);
    }
}
