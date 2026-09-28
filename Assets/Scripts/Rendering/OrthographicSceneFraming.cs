using UnityEngine;

// Keeps the authored overview visible when the Game window changes aspect ratio.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class OrthographicSceneFraming : MonoBehaviour
{
    [SerializeField] private Vector2 frameSize = new Vector2(20f, 16.3f);
    private Camera viewCamera;

    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();
    private void OnValidate() => Refresh();

    public void Refresh()
    {
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (viewCamera == null || !viewCamera.orthographic) return;
        viewCamera.orthographicSize = Mathf.Max(Mathf.Max(.01f, frameSize.y) * .5f,
            Mathf.Max(.01f, frameSize.x) / (2f * Mathf.Max(.01f, viewCamera.aspect)));
    }
}
