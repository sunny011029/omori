using UnityEngine;

[DisallowMultipleComponent]
public sealed class SchoolLocker : MonoBehaviour
{
    public SpriteRenderer closedVisual;
    public GameObject openVisual;
    public bool IsOpen { get; private set; }
    private float nextToggle;

    public bool TryToggle()
    {
        if (Time.unscaledTime < nextToggle) return false;
        nextToggle = Time.unscaledTime + .25f;
        SetOpen(!IsOpen);
        return true;
    }

    public void SetOpen(bool value)
    {
        IsOpen = value;
        if (closedVisual != null) closedVisual.enabled = !value;
        if (openVisual != null) openVisual.SetActive(value);
    }

    private void Awake() => SetOpen(false);
    private void OnDisable() => SetOpen(false);
}
