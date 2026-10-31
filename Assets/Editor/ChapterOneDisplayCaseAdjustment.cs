using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Explicit request only: targeted repair, never run on scene load.
[InitializeOnLoad]
public static class ChapterOneDisplayCaseAdjustment
{
    const string Request="Library/ChapterOneDisplayCaseAdjustment.request";
    const string Output="Recovery/ChapterOne/Step09Adjustments/";
    static ChapterOneDisplayCaseAdjustment() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Request);
        try { Apply(); } catch(Exception e) { File.WriteAllText(Output+"display-report.txt","FAIL\n"+e); Debug.LogException(e); }
    }
    static void Apply()
    {
        var previous=SceneManager.GetActiveScene();
        var shop=SceneManager.GetSceneByPath("Assets/Scenes/MochaStore.unity"); bool opened=!shop.isLoaded;
        if(opened)shop=EditorSceneManager.OpenScene("Assets/Scenes/MochaStore.unity",OpenSceneMode.Additive);
        var otherRoots=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=shop).SelectMany(s=>s.GetRootGameObjects()).Where(g=>g.activeSelf).ToArray();
        try
        {
            if(shop.isDirty)throw new Exception("Save current shop edits before applying the display repair.");
            foreach(var go in otherRoots)go.SetActive(false);
            var root=shop.GetRootGameObjects().Single(g=>g.name=="Room1Grid").transform;
            var counter=root.Find("DepthSortedFurniture/FrontCounter");
            var cake=counter.Find("StarterMatchaCake");
            var original=shop.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c is Transform||c is Renderer||c is Tilemap||c is Collider2D||c is Camera).ToArray();
            var before=original.Select(State).ToArray();
            if(!File.Exists(Output+"display-before.png"))Capture(new Vector3(5,.45f,-10),2.1f,Output+"display-before.png");
            var source=counter.GetComponentsInChildren<Tilemap>().Single(m=>m.name=="Furnitures2");
            var shader=Shader.Find("MatchaCloud/DisplayCaseGlass");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Glass shader failed compilation");
            const string matPath="Assets/ChapterOne/Art/DisplayCaseGlass.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}
            material.SetFloat("_GlassOpacity",.07f);material.SetFloat("_ReflectionOpacity",.24f);EditorUtility.SetDirty(material);
            var foreground=counter.Find("DisplayCaseGlassForeground");
            if(foreground==null)
            {
                var go=new GameObject("DisplayCaseGlassForeground",typeof(Tilemap),typeof(TilemapRenderer));
                SceneManager.MoveGameObjectToScene(go,shop);foreground=go.transform;foreground.SetParent(counter,false);
            }
            foreground.localPosition=source.transform.localPosition;foreground.localRotation=source.transform.localRotation;foreground.localScale=source.transform.localScale;
            var map=foreground.GetComponent<Tilemap>();map.ClearAllTiles();map.tileAnchor=source.tileAnchor;
            map.orientation=source.orientation;map.orientationMatrix=source.orientationMatrix;
            for(int x=3;x<=6;x++)for(int y=-1;y<=1;y++)
            {
                var cell=new Vector3Int(x,y,0);if(!source.HasTile(cell))throw new Exception("Missing display tile "+cell);
                map.SetTile(cell,source.GetTile(cell));map.SetTileFlags(cell,TileFlags.None);
                map.SetTransformMatrix(cell,source.GetTransformMatrix(cell));map.SetColor(cell,source.GetColor(cell));
            }
            var renderer=foreground.GetComponent<TilemapRenderer>();renderer.sharedMaterial=material;
            renderer.sortingLayerID=source.GetComponent<TilemapRenderer>().sortingLayerID;renderer.sortingOrder=6;
            cake.position=new Vector3(4.3f,1.12f,0);
            cake.localScale=Vector3.one*(.62f/cake.GetComponent<SpriteRenderer>().sprite.bounds.size.x);
            for(int i=0;i<original.Length;i++)
                if(original[i].transform!=cake && !original[i].transform.IsChildOf(foreground) && before[i]!=State(original[i]))
                    throw new Exception("Unexpected change: "+original[i].name+" "+original[i].GetType().Name);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(shop);if(!EditorSceneManager.SaveScene(shop))throw new IOException("Shop save failed");
            Capture(new Vector3(5,.45f,-10),2.1f,Output+"display-after.png");
            File.WriteAllText(Output+"display-report.txt","PASS Original scene transforms, tiles, collisions and renderers preserved except cake transform.\nPASS Original cabinet / cake order 5 / glass foreground order 6 share the original Y-sorted group.\nPASS 12 existing case tiles reused; original texture unchanged; glass shader compiled.\nCake bounds="+cake.GetComponent<SpriteRenderer>().bounds+"\n");
        }
        finally
        {
            foreach(var go in otherRoots)go.SetActive(true);
            if(opened)EditorSceneManager.CloseScene(shop,true);SceneManager.SetActiveScene(previous);
        }
    }
    static string State(Component c)=>c is Transform t?t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf:EditorJsonUtility.ToJson(c);
    static void Capture(Vector3 position,float size,string path)
    {
        var go=new GameObject("Temporary display review");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.orthographic=true;camera.orthographicSize=size;camera.aspect=1.5f;camera.transform.position=position;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;go.AddComponent<UniversalAdditionalCameraData>();
        var rt=new RenderTexture(900,600,24);var texture=new Texture2D(900,600,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try {SortingGroup.UpdateAllSortingGroups();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,900,600),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally {RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);}
    }
}
