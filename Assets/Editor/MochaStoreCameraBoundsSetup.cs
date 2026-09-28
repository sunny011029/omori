using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MochaStoreCameraBoundsSetup
{
    [MenuItem("Tools/Mocha Store/Update Camera Room Bounds")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/MochaStore.unity");
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open MochaStore first.");
        var roots = scene.GetRootGameObjects();
        var floor = roots.Single(root=>root.name=="Grid").transform.Find("House").GetComponent<Tilemap>();
        var follow = roots.SelectMany(root=>root.GetComponentsInChildren<TopDownCameraFollow>()).Single();
        var min = new Vector3Int(int.MaxValue,int.MaxValue,0);
        var max = new Vector3Int(int.MinValue,int.MinValue,0);
        int count = 0;
        // cellBounds retains deleted cells. Only include the actual painted green room.
        foreach (var cell in floor.cellBounds.allPositionsWithin)
        {
            if (!floor.HasTile(cell)) continue;
            min = Vector3Int.Min(min,cell); max = Vector3Int.Max(max,cell); count++;
        }
        if (count == 0) throw new InvalidOperationException("Room Tilemap is empty.");
        if (count != (max.x-min.x+1)*(max.y-min.y+1))
            throw new InvalidOperationException("Room is no longer a filled rectangle; define its visible interior explicitly.");
        if (!EditorSceneManager.SaveScene(scene,"Temp/MochaStore_before_camera_bounds.unity",true))
            throw new InvalidOperationException("Could not back up the open scene.");
        Vector3 low = floor.transform.InverseTransformPoint(floor.CellToWorld(min));
        Vector3 high = floor.transform.InverseTransformPoint(floor.CellToWorld(max+new Vector3Int(1,1,0)));
        var camera = follow.GetComponent<Camera>();
        Undo.RecordObjects(new UnityEngine.Object[]{follow,camera,follow.transform},"Confine camera to green room");
        if (!follow.ConfineToRoom) follow.PreferredOrthographicSize = camera.orthographicSize;
        follow.SetRoomBounds(floor.transform,Rect.MinMaxRect(low.x,low.y,high.x,high.y));
        EditorUtility.SetDirty(follow);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
        Selection.activeGameObject = camera.gameObject;
        Debug.Log("Camera confined to green room: " + follow.GetWorldRoomBounds());
    }
}
