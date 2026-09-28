using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Integration checks run against the actual scene in Play mode and then return to Edit mode.
[InitializeOnLoad]
public static class MochaStoreControllerChecks
{
    private const string Request = "Temp/MochaControllerChecks.request";
    private const string Report = "Temp/MochaControllerChecks.report.txt";
    static MochaStoreControllerChecks() { EditorApplication.update += RunWhenReady; }

    [MenuItem("Tools/Mocha Store/Verify Movement and Occlusion")]
    public static void RequestCheck()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start this check from Edit mode.");
        File.WriteAllText(Request, "Verify scene controller");
    }

    private static void RunWhenReady()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlaying)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
            return;
        }
        var player = GameObject.Find("Grid/Role/BlondWhite");
        if (player == null)
        {
            File.Delete(Request); File.WriteAllText(Report, "FAILED: Open MochaStore before testing.");
            EditorApplication.isPlaying = false; return;
        }
        var animator = player.GetComponent<Animator>();
        if (!animator.isInitialized) return;
        File.Delete(Request);
        var originalMode = Physics2D.simulationMode;
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            var control = player.GetComponent<TopDownCharacterController>();
            var body = player.GetComponent<Rigidbody2D>();
            var sort = player.GetComponent<TopDownYSort>();
            var sprite = player.GetComponent<SpriteRenderer>();
            control.UseKeyboardInput = false;
            body.interpolation = RigidbodyInterpolation2D.None;
            var start = new Vector2(-1.0f, 2.35f);
            string report = "SUCCESS\n";
            foreach (var direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down, Vector2.one })
            {
                Teleport(body, start);
                Move(control, direction, 20);
                float actual = Vector2.Distance(start,body.position);
                float expected = control.MoveSpeed * .4f;
                Require(Mathf.Abs(actual - expected) < .03f, "Directional speed mismatch: " + direction + " distance=" + actual);
                report += "Movement " + direction + ": " + actual.ToString("F3") + " units in 0.4s.\n";
            }
            control.SetMoveInput(Vector2.right); animator.Update(.02f); animator.Update(.02f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk_Right"), "Right animation");
            control.SetMoveInput(Vector2.up); animator.Update(.02f);
            Require(animator.GetBool("FacingRight"), "Vertical movement changed last facing");
            control.SetMoveInput(Vector2.left); animator.Update(.02f); animator.Update(.02f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk_Left"), "Left animation");
            control.SetMoveInput(Vector2.zero); animator.Update(.02f); animator.Update(.02f);
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle_Left"), "Stop animation");
            report += "Walk/Idle switching and retained vertical facing verified.\n";

            var counter = GameObject.Find("Grid/DepthSortedFurniture/FrontCounter");
            var obstacle = counter.GetComponent<BoxCollider2D>();
            var counterOrder = counter.GetComponent<SortingGroup>().sortingOrder;
            Teleport(body, new Vector2(0,2)); Move(control, Vector2.down, 80); sort.RefreshOrder();
            Require(body.position.y > obstacle.bounds.max.y - .03f, "Passed through counter from behind");
            Require(sprite.sortingOrder < counterOrder, "Behind-counter sorting wrong");
            report += "Counter back: stops at y=" + body.position.y.ToString("F3") + ", character order=" + sprite.sortingOrder + ", counter order=" + counterOrder + ".\n";
            CaptureOcclusion("Temp/Mocha-counter-behind.png");
            Teleport(body, new Vector2(0,-2.2f)); Move(control, Vector2.up, 80); sort.RefreshOrder();
            Require(player.GetComponent<BoxCollider2D>().bounds.max.y < obstacle.bounds.min.y + .03f, "Passed through counter from front");
            Require(sprite.sortingOrder > counterOrder, "Front-counter sorting wrong");
            report += "Counter front: stops at y=" + body.position.y.ToString("F3") + ", character order=" + sprite.sortingOrder + ", counter order=" + counterOrder + ".\n";
            CaptureOcclusion("Temp/Mocha-counter-front.png");
            Teleport(body,new Vector2(0,-10.2f)); Move(control,Vector2.down,60);
            Require(body.position.y >= -11.03f, "Left the room through bottom wall");
            report += "Room boundary collision verified.\n";
            control.enabled=false; Require(body.velocity.sqrMagnitude < .0001f, "Disabled movement did not stop");

            int verified=0;
            var grid = GameObject.Find("Grid").transform;
            foreach(var generated in grid.Find("DepthSortedFurniture").GetComponentsInChildren<Tilemap>())
            {
                var source = grid.Find(generated.name).GetComponent<Tilemap>();
                foreach(var cell in generated.cellBounds.allPositionsWithin)
                {
                    if(!generated.HasTile(cell)) continue;
                    Require(generated.GetTile(cell)==source.GetTile(cell), "Tile reference changed");
                    Require(Vector3.Distance(generated.CellToWorld(cell),source.CellToWorld(cell))<.0001f,"Tile moved");
                    Require(generated.GetTransformMatrix(cell)==source.GetTransformMatrix(cell),"Tile transform changed");
                    Require(generated.GetColor(cell)==source.GetColor(cell),"Tile color changed");
                    verified++;
                }
            }
            int sourceCount = 0;
            foreach (var sourceName in new[] { "Furnitures1", "Furnitures2" })
            {
                var source = grid.Find(sourceName).GetComponent<Tilemap>();
                foreach (var cell in source.cellBounds.allPositionsWithin) if (source.HasTile(cell)) sourceCount++;
            }
            Require(verified==sourceCount,"Unexpected tile count");
            report += verified + " original tile references, transforms and colors verified in the depth groups.\n";
            File.WriteAllText(Report,report);
            Debug.Log("MochaStore movement, collision, animation and furniture depth checks passed.");
        }
        catch(Exception error) {File.WriteAllText(Report,"FAILED\n"+error);Debug.LogException(error);}
        finally {Physics2D.simulationMode=originalMode; EditorApplication.isPlaying=false;}
    }

    private static void Teleport(Rigidbody2D body,Vector2 position)
    {body.position=position;body.transform.position=new Vector3(position.x,position.y,0);body.velocity=Vector2.zero;Physics2D.SyncTransforms();}
    private static void Move(TopDownCharacterController control,Vector2 direction,int steps)
    {control.SetMoveInput(direction);for(int i=0;i<steps;i++){control.SendMessage("FixedUpdate");Physics2D.Simulate(.02f);}}
    private static void Require(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);}

    private static void CaptureOcclusion(string path)
    {
        var camera = Camera.main;
        var originalPosition = camera.transform.position;
        var originalSize = camera.orthographicSize;
        var originalTarget = RenderTexture.active;
        var target = new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
        var image = new Texture2D(512,512,TextureFormat.RGBA32,false);
        try
        {
            camera.transform.position = new Vector3(0,0,-10);
            camera.orthographicSize = 3;
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
            RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,512,512),0,0);
            image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=originalTarget;
            camera.transform.position=originalPosition; camera.orthographicSize=originalSize;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
