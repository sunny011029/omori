using UnityEngine;

// Opens the existing doorway automatically while walking through; mats stay on the ground.
[DisallowMultipleComponent]
public sealed class SchoolDoorPassage : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform building;
    [SerializeField] private Rect approachArea;
    [SerializeField] private SpriteRenderer[] doorPanels;
    public bool IsOpen { get; private set; }
    public void Configure(Transform player,Transform space,Rect approach,SpriteRenderer[] panels)
    { target=player; building=space; approachArea=approach; doorPanels=panels; }
    private void LateUpdate()
    {
        if(doorPanels==null) return;
        IsOpen=target!=null && building!=null && approachArea.Contains(building.InverseTransformPoint(target.position));
        foreach(var panel in doorPanels) if(panel!=null) panel.enabled=!IsOpen;
    }
    private void OnDisable() { if(doorPanels!=null) foreach(var panel in doorPanels) if(panel!=null) panel.enabled=true; }
}
