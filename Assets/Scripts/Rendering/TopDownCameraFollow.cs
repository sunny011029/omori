using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public sealed class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("World-space framing offset relative to the character's feet.")]
    [SerializeField] private Vector2 offset;
    [Tooltip("Follow smoothing in seconds. Set to zero for immediate following.")]
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;
    [Header("Room confinement")]
    [SerializeField] private bool confineToRoom;
    [Tooltip("Transform of the room's floor Tilemap.")]
    [SerializeField] private Transform roomSpace;
    [Tooltip("Occupied room rectangle in the room Transform's local coordinates.")]
    [SerializeField] private Rect localRoomBounds;
    [Tooltip("Inset in world units to avoid showing transparent edge pixels.")]
    [SerializeField, Min(0f)] private float edgePadding = 1f;
    [Tooltip("Preferred orthographic size. The view only zooms in further if the window is too wide or tall for the room.")]
    [SerializeField, Min(0.01f)] private float preferredOrthographicSize = 8f;
    private Vector2 velocity;
    private float cameraDepth;
    private Camera viewCamera;

    public Transform Target
    {
        get => target;
        set { target = value; velocity = Vector2.zero; }
    }
    public Vector2 Offset { get => offset; set => offset = value; }
    public float SmoothTime { get => smoothTime; set => smoothTime = Mathf.Max(0f, value); }
    public float PreferredOrthographicSize { get => preferredOrthographicSize; set => preferredOrthographicSize = Mathf.Max(.01f, value); }
    public bool ConfineToRoom => confineToRoom;
    public float EdgePadding { get => edgePadding; set => edgePadding = Mathf.Max(0f, value); }

    public void SetRoomBounds(Transform space, Rect bounds)
    {
        roomSpace = space;
        localRoomBounds = bounds;
        confineToRoom = true;
        ConstrainNow();
    }

    private void OnEnable()
    {
        cameraDepth = transform.position.z;
        viewCamera = GetComponent<Camera>();
        velocity = Vector2.zero;
        ConstrainNow();
    }

    // Sample the interpolated character Transform after movement, not its fixed-step body position.
    private void LateUpdate()
    {
        // Recompute view extents each frame, including while paused or missing a target.
        bool confined = TryGetCameraLimits(out Vector2 min, out Vector2 max);
        Vector2 next = transform.position;
        if (target != null && Time.deltaTime > 0f)
        {
            Vector2 desired = (Vector2)target.position + offset;
            if (confined) desired = Clamp(desired, min, max);
            if (smoothTime <= 0f)
            {
                next = desired;
                velocity = Vector2.zero;
            }
            else
            {
                next = Vector2.SmoothDamp(next, desired, ref velocity,
                    smoothTime, Mathf.Infinity, Time.deltaTime);
            }
        }
        else velocity = Vector2.zero;
        // Clamp the final position as well, so resizing cannot expose the outside during smoothing.
        if (confined)
        {
            Vector2 clamped = Clamp(next, min, max);
            if (clamped.x != next.x) velocity.x = 0f;
            if (clamped.y != next.y) velocity.y = 0f;
            next = clamped;
        }
        transform.position = new Vector3(next.x, next.y, cameraDepth);
    }

    public Rect GetWorldRoomBounds()
    {
        if (roomSpace == null) return default;
        Vector2 a = roomSpace.TransformPoint(new Vector3(localRoomBounds.xMin, localRoomBounds.yMin, 0));
        Vector2 b = roomSpace.TransformPoint(new Vector3(localRoomBounds.xMax, localRoomBounds.yMax, 0));
        Vector2 min = Vector2.Min(a, b), max = Vector2.Max(a, b);
        Vector2 inset = Vector2.Min(Vector2.one * Mathf.Max(0f, edgePadding), (max - min) * .49f);
        return Rect.MinMaxRect(min.x + inset.x, min.y + inset.y, max.x - inset.x, max.y - inset.y);
    }

    public void ConstrainNow()
    {
        if (!TryGetCameraLimits(out Vector2 min, out Vector2 max)) return;
        Vector2 position = Clamp(transform.position, min, max);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        velocity = Vector2.zero;
    }

    private bool TryGetCameraLimits(out Vector2 min, out Vector2 max)
    {
        min = max = default;
        if (!confineToRoom || roomSpace == null || localRoomBounds.width <= 0f || localRoomBounds.height <= 0f) return false;
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (!viewCamera.orthographic) return false;
        Rect room = GetWorldRoomBounds();
        if (room.width <= 0f || room.height <= 0f) return false;
        float aspect = Mathf.Max(.0001f, viewCamera.aspect);
        // Fit the viewport itself, not just the camera's center. Reversible after an aspect change.
        viewCamera.orthographicSize = Mathf.Min(preferredOrthographicSize, room.height * .5f, room.width / (2f * aspect));
        Vector2 halfView = new Vector2(viewCamera.orthographicSize * aspect, viewCamera.orthographicSize);
        min = room.min + halfView;
        max = room.max - halfView;
        // At an exact fit, avoid a reversed clamp interval due to floating point rounding.
        if (min.x > max.x) min.x = max.x = room.center.x;
        if (min.y > max.y) min.y = max.y = room.center.y;
        return true;
    }

    private static Vector2 Clamp(Vector2 point, Vector2 min, Vector2 max)
        => new Vector2(Mathf.Clamp(point.x, min.x, max.x), Mathf.Clamp(point.y, min.y, max.y));

    private void OnDisable() => velocity = Vector2.zero;
}
