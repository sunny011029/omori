using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class TopDownYSort : MonoBehaviour
{
    [Tooltip("World-space offset from this object's origin to its ground contact point.")]
    [SerializeField] private float groundOffset;
    private SpriteRenderer spriteRenderer;
    private SortingGroup sortingGroup;

    public float GroundOffset { get => groundOffset; set { groundOffset = value; RefreshOrder(); } }
    public static int OrderAt(float groundY) => Mathf.Clamp(-Mathf.RoundToInt(groundY * 100f), -29000, 29000);

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        sortingGroup = GetComponent<SortingGroup>();
        RefreshOrder();
    }

    private void LateUpdate() => RefreshOrder();
    private void OnValidate() => RefreshOrder();

    public void RefreshOrder()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (sortingGroup == null) sortingGroup = GetComponent<SortingGroup>();
        int order = OrderAt(transform.position.y + groundOffset);
        if (sortingGroup != null) sortingGroup.sortingOrder = order;
        else if (spriteRenderer != null) spriteRenderer.sortingOrder = order;
    }
}
