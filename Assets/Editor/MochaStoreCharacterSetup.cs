using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Rebuilds derived render/collision objects; original painted Tilemaps remain intact.
public static class MochaStoreCharacterSetup
{
    private const string GeneratedName = "DepthSortedFurniture";
    private const string MaterialPath = "Assets/角色/Animations/BlondWhite/CharacterFrictionless.physicsMaterial2D";

    [MenuItem("Tools/Mocha Store/Rebuild Character and Furniture Depth")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MochaStore.unity");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open MochaStore first.");
        var grid = scene.GetRootGameObjects().Single(root => root.name == "Grid").transform;
        var maps = new[] {grid.Find("Furnitures1").GetComponent<Tilemap>(), grid.Find("Furnitures2").GetComponent<Tilemap>()};
        var player = grid.Find("Role/BlondWhite").gameObject;
        if (!File.Exists("Temp/MochaStore_before_controller.unity"))
            EditorSceneManager.SaveScene(scene, "Temp/MochaStore_before_controller.unity", true);

        var previous = grid.Find(GeneratedName);
        if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
        var root = new GameObject(GeneratedName);
        Undo.RegisterCreatedObjectUndo(root, "Build furniture depth and collision");
        root.transform.SetParent(grid, false);

        var available = new HashSet<Vector3Int>();
        foreach (var map in maps)
            foreach (var cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) available.Add(cell);
        int expected = maps.Sum(map => CountTiles(map));
        int copied = 0;

        // Authored multi-tile objects. Separate adjacent tables, plants and U-shaped counter sections.
        var regions = new List<Region> {
            new Region("LeftRoundTable", -18, 6, 6, 2),
            new Region("LeftTableUpper", -17, 1, 4, 4),
            new Region("LeftTableMiddle", -17, -4, 4, 4),
            new Region("LeftTableLower", -17, -9, 4, 4),
            new Region("DividerUpper", -10, -3, 2, 4),
            new Region("PlantUpper", -10, 1, 2, 3),
            new Region("DividerLower", -10, -10, 2, 4),
            new Region("PlantLower", -10, -6, 2, 3),
            new Region("BackCounter", -9, 5, 25, 7),
            new Region("FrontCounter", -5, -1, 21, 3),
            new Region("TableUpperLeft", -5, -5, 6, 2),
            new Region("TableUpperRight", 1, -5, 6, 2),
            new Region("TableLowerLeft", -5, -9, 6, 2),
            new Region("TableLowerRight", 1, -9, 6, 2)
        };
        // A north/south counter has a continuous floor footprint, so each section sorts at its own depth.
        for (int y = 1; y <= 4; y++) regions.Add(new Region("SideCounter_" + y, 14, y, 2, 1));
        foreach (var region in regions)
        {
            var cells = available.Where(p => region.bounds.Contains(new Vector2Int(p.x, p.y))).ToArray();
            available.ExceptWith(cells);
            if (cells.Length > 0) copied += BuildObject(root.transform, maps, region.name, cells, true);
        }
        // Remaining isolated stools and wall decorations.
        int other = 0;
        while (available.Count > 0)
        {
            var queue = new Queue<Vector3Int>(); var cells = new List<Vector3Int>();
            var first = available.OrderBy(p=>p.y).ThenBy(p=>p.x).First();
            available.Remove(first); queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); cells.Add(cell);
                foreach (var step in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                    if (available.Remove(cell + step)) queue.Enqueue(cell + step);
            }
            copied += BuildObject(root.transform, maps, "Prop_" + other++, cells.ToArray(), cells.Min(p=>p.y) < 8);
        }
        if (copied != expected) throw new InvalidOperationException("Tile copy mismatch: " + copied + "/" + expected);
        foreach (var map in maps)
        {
            Undo.RecordObject(map.GetComponent<TilemapRenderer>(), "Use complete-object furniture rendering");
            map.GetComponent<TilemapRenderer>().enabled = false;
        }
        var houseRenderer = grid.Find("House").GetComponent<TilemapRenderer>();
        Undo.RecordObject(houseRenderer, "Keep floor behind characters");
        houseRenderer.sortingOrder = -30000;

        var body = GetOrAdd<Rigidbody2D>(player);
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("CharacterFrictionless") {friction = 0f, bounciness = 0f};
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        var feet = GetOrAdd<BoxCollider2D>(player);
        var scale = player.transform.lossyScale;
        feet.size = new Vector2(0.5f / Mathf.Abs(scale.x), 0.3f / Mathf.Abs(scale.y));
        feet.offset = new Vector2(0, 0.15f / Mathf.Abs(scale.y));
        feet.sharedMaterial = material;
        GetOrAdd<TopDownCharacterController>(player);
        GetOrAdd<TopDownYSort>(player).RefreshOrder();
        player.GetComponent<SpriteRenderer>().spriteSortPoint = SpriteSortPoint.Pivot;
        AddBoundary(root.transform, "LeftWall", new Vector2(-18.65f,-1.25f), new Vector2(.3f,20.1f));
        AddBoundary(root.transform, "RightWall", new Vector2(16.15f,-1.25f), new Vector2(.3f,20.1f));
        AddBoundary(root.transform, "BackWall", new Vector2(-1.25f,8.65f), new Vector2(35.1f,.3f));
        AddBoundary(root.transform, "FrontWall", new Vector2(-1.25f,-11.15f), new Vector2(35.1f,.3f));
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        Selection.activeGameObject = player;
        File.WriteAllText("Temp/MochaControllerSetup.report.txt", "SUCCESS\nCopied " + copied + "/" + expected +
            " painted tiles without changing their world transforms.\nPreserved player position=" + player.transform.position +
            " scale=" + player.transform.localScale + "\nFurniture groups=" + root.GetComponentsInChildren<SortingGroup>().Length +
            "\nAdded keyboard controller, foot collider, frictionless Rigidbody2D, Y sorting and room boundaries.\n");
    }

    private static int BuildObject(Transform parent, Tilemap[] sources, string name, Vector3Int[] cells, bool solid)
    {
        int minX = cells.Min(p=>p.x), maxX = cells.Max(p=>p.x), minY = cells.Min(p=>p.y);
        var ground = sources[0].CellToWorld(new Vector3Int(minX, minY, 0));
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.position = new Vector3(ground.x, ground.y + .15f, 0);
        var group = obj.AddComponent<SortingGroup>();
        group.sortingLayerID = sources[0].GetComponent<TilemapRenderer>().sortingLayerID;
        obj.AddComponent<TopDownYSort>().RefreshOrder();
        int count = 0;
        foreach (var source in sources)
        {
            var child = new GameObject(source.name, typeof(Tilemap), typeof(TilemapRenderer));
            child.transform.SetParent(obj.transform, false);
            child.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            child.transform.localScale = source.transform.localScale;
            var map = child.GetComponent<Tilemap>();
            map.tileAnchor = source.tileAnchor; map.orientation = source.orientation;
            map.orientationMatrix = source.orientationMatrix; map.color = source.color;
            map.animationFrameRate = source.animationFrameRate;
            foreach (var cell in cells)
            {
                if (!source.HasTile(cell)) continue;
                map.SetTile(cell, source.GetTile(cell));
                map.SetTileFlags(cell, TileFlags.None);
                map.SetTransformMatrix(cell, source.GetTransformMatrix(cell));
                map.SetColor(cell, source.GetColor(cell));
                map.SetTileFlags(cell, source.GetTileFlags(cell));
                count++;
            }
            var renderer = child.GetComponent<TilemapRenderer>();
            var original = source.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = original.sharedMaterial;
            renderer.sortingLayerID = original.sortingLayerID;
            renderer.sortingOrder = original.sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
        }
        if (solid)
        {
            float width = sources[0].CellToWorld(new Vector3Int(maxX + 1,minY,0)).x - ground.x;
            var collider = obj.AddComponent<BoxCollider2D>();
            float depth = name.StartsWith("SideCounter_") ? 1.05f : .65f;
            collider.size = new Vector2(Mathf.Max(.3f,width-.16f), depth);
            collider.offset = new Vector2(width*.5f, depth*.5f);
        }
        return count;
    }

    private static int CountTiles(Tilemap map)
    { int count=0; foreach(var p in map.cellBounds.allPositionsWithin) if(map.HasTile(p)) count++; return count; }
    private static T GetOrAdd<T>(GameObject obj) where T : Component
    { var component = obj.GetComponent<T>(); return component != null ? component : Undo.AddComponent<T>(obj); }
    private static void AddBoundary(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var obj=new GameObject(name); obj.transform.SetParent(parent,false); obj.transform.position=position;
        obj.AddComponent<BoxCollider2D>().size=size;
    }
    private sealed class Region
    {
        public readonly string name; public readonly RectInt bounds;
        public Region(string name,int x,int y,int w,int h) {this.name=name; bounds=new RectInt(x,y,w,h);}
    }
}
