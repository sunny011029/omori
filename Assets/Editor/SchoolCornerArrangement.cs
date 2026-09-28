using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class SchoolCornerArrangement
{
    const string Request="Library/SchoolCorner.request";
    static SchoolCornerArrangement() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string action=File.ReadAllText(Request).Trim(); File.Delete(Request);
        try { if(action=="inspect") Inspect(); else if(action=="apply") Apply(); }
        catch(Exception e) { File.WriteAllText("Library/SchoolCorner.report.txt","FAIL\n"+e); Debug.LogException(e); }
    }
    static string Path(Transform t) => t.parent==null ? t.name : Path(t.parent)+"/"+t.name;
    static Transform Root() => SceneManager.GetSceneByPath("Assets/Scenes/school.unity").GetRootGameObjects().Single(g=>g.name=="School_ReferenceLayout").transform;
    static readonly Vector3Int[] SmallBookCells={new Vector3Int(26,-7,0),new Vector3Int(27,-7,0),new Vector3Int(26,-6,0),new Vector3Int(27,-6,0)};
    static bool SmallBook(Tilemap map,Vector3Int cell)
    {
        if(map.transform.parent.name!="LeftClassroom_Floor" || !SmallBookCells.Contains(cell)) return false;
        var sprite=map.GetSprite(cell);
        return sprite!=null && sprite.texture.name=="img_v3_02141_ba0ed6ee-51b1-4e4e-bb12-31cec6464dhu" &&
            (sprite.rect.x==128 || sprite.rect.x==160) && (sprite.rect.y==32 || sprite.rect.y==64);
    }
    static string ProtectedFloorState(Transform root)
    {
        var s=new StringBuilder();
        foreach(var map in root.GetComponentsInChildren<Tilemap>())
        {
            s.Append(Path(map.transform)).Append(map.color).Append(map.tileAnchor);
            s.Append(EditorJsonUtility.ToJson(map.transform)).Append(EditorJsonUtility.ToJson(map.transform.parent));
            s.Append(EditorJsonUtility.ToJson(map.GetComponent<TilemapRenderer>()));
            foreach(var cell in map.cellBounds.allPositionsWithin)
            {
                var tile=map.GetTile(cell); if(tile==null || SmallBook(map,cell)) continue;
                s.Append(cell).Append(AssetDatabase.GetAssetPath(tile)).Append(map.GetColor(cell)).Append(map.GetTransformMatrix(cell));
            }
        }
        return s.ToString();
    }
    [MenuItem("Tools/School/Arrange Right Corridor Bookcase")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Use Edit mode.");
        var root=Root(); var scene=root.gameObject.scene; var hall=root.Find("04_CorridorFurniture");
        Directory.CreateDirectory("Recovery/School");
        if(!EditorSceneManager.SaveScene(scene,"Recovery/School/school-before-corner-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true)) throw new IOException("Backup failed.");
        string floor=ProtectedFloorState(root);
        var books=hall.Find("Scattered_Books"); var bookRenderer=books.Find("Visual").GetComponent<SpriteRenderer>();
        string retainedBooks=EditorJsonUtility.ToJson(books)+EditorJsonUtility.ToJson(bookRenderer.transform)+EditorJsonUtility.ToJson(bookRenderer);
        SchoolSceneDetailAdjustments.CaptureOverview(root,"Recovery/School/corner-before.png");
        Undo.IncrementCurrentGroup(); int undoGroup=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Arrange school right corridor");
        var old=hall.Find("Right_Bookcase"); var cabinet=hall.Find("Right_LargeBookcase");
        if(cabinet==null)
        {
            if(old==null) throw new Exception("Right bookcase was not found.");
            cabinet=UnityEngine.Object.Instantiate(old.gameObject,hall).transform;
            cabinet.name="Right_LargeBookcase"; Undo.RegisterCreatedObjectUndo(cabinet.gameObject,"Add larger matching bookcase");
        }
        var sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/学校改色/img_v3_02141_50019d76bc51fhu.png").OfType<Sprite>().Single(s=>s.name=="School_CabinetTall");
        const float width=2.2f; float height=width*sprite.rect.height/sprite.rect.width;
        cabinet.localPosition=new Vector3(16.6f,1.5f,0); cabinet.localScale=Vector3.one;
        var visual=cabinet.Find("Visual"); visual.GetComponent<SpriteRenderer>().sprite=sprite;
        visual.localPosition=new Vector3(0,height*.5f,0); visual.localScale=new Vector3(width/sprite.bounds.size.x,height/sprite.bounds.size.y,1);
        var box=cabinet.GetComponent<BoxCollider2D>(); box.size=new Vector2(width*.82f,.32f); box.offset=new Vector2(0,.16f);
        cabinet.GetComponent<TopDownYSort>().GroundOffset=.16f;
        if(old!=null) Undo.DestroyObjectImmediate(old.gameObject);
        var extinguisher=hall.Find("Wall_Extinguisher"); if(extinguisher!=null) Undo.DestroyObjectImmediate(extinguisher.gameObject);
        int removed=0;
        foreach(var map in root.GetComponentsInChildren<Tilemap>())
        {
            var cells=SmallBookCells.Where(c=>SmallBook(map,c)).ToArray(); if(cells.Length==0) continue;
            Undo.RecordObject(map,"Remove only the small duplicate books");
            foreach(var cell in cells) { map.SetTile(cell,null); removed++; }
        }
        Physics2D.SyncTransforms();
        if(floor!=ProtectedFloorState(root)) throw new Exception("Protected floor data changed.");
        if(retainedBooks!=EditorJsonUtility.ToJson(books)+EditorJsonUtility.ToJson(bookRenderer.transform)+EditorJsonUtility.ToJson(bookRenderer)) throw new Exception("The rightmost books changed.");
        Bounds bounds=visual.GetComponent<SpriteRenderer>().bounds;
        if(bounds.max.x>root.TransformPoint(new Vector3(17.95f,0,0)).x || bounds.max.y>root.TransformPoint(new Vector3(0,3.94f,0)).y)
            throw new Exception("Bookcase exceeds the wall alcove.");
        if(hall.Find("Right_Bookcase")!=null || hall.Find("Wall_Extinguisher")!=null) throw new Exception("Old corner objects still present.");
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Undo.CollapseUndoOperations(undoGroup);
        SchoolSceneDetailAdjustments.CaptureOverview(root,"Recovery/School/corner-after.png");
        File.WriteAllText("Library/SchoolCorner.report.txt","PASS\nScene saved.\nRemoved old small cabinet and fire extinguisher.\nLarge matching bookcase: width="+width+", height="+height+"; original sprite proportions preserved.\nRemoved duplicate small-book tiles: "+removed+".\nRightmost scattered books unchanged.\nAll other Tilemap cells, floor colours, transforms and renderer settings unchanged.\nBookcase fits wall alcove and keeps feet collision/Y sorting.\n");
        Selection.activeGameObject=cabinet.gameObject;
    }
    static void Inspect()
    {
        var root=Root(); var report=new StringBuilder();
        foreach(var r in root.GetComponentsInChildren<SpriteRenderer>())
        {
            var p=root.InverseTransformPoint(r.bounds.center);
            if(p.x<12 || p.y>3.6f || p.y<-.2f) continue;
            report.AppendLine("SPRITE "+Path(r.transform)+" position="+p+" size="+r.bounds.size+" sprite="+r.sprite.name+" rect="+r.sprite.rect);
        }
        foreach(var map in root.GetComponentsInChildren<Tilemap>())
        foreach(var cell in map.cellBounds.allPositionsWithin)
        {
            var sprite=map.GetSprite(cell); if(sprite==null) continue;
            var p=root.InverseTransformPoint(map.GetCellCenterWorld(cell));
            if(p.x<12 || p.y>3.6f || p.y<-.2f) continue;
            report.AppendLine("TILE "+Path(map.transform)+" cell="+cell+" position="+p+" asset="+AssetDatabase.GetAssetPath(map.GetTile(cell))+" sprite="+sprite.name+" rect="+sprite.rect+" texture="+sprite.texture.name);
        }
        File.WriteAllText("Library/SchoolCorner.inspect.txt",report.ToString());
    }
}
