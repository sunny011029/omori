using UnityEngine;

public enum SchoolInteractionKind { Xiaoyu, ClassmateOne, ClassmateTwo, Sketchbook, Notice, Locker, ChapterObject }

[DisallowMultipleComponent]
public sealed class SchoolInteractable : MonoBehaviour
{
    public string interactionId;
    public string displayName;
    public SchoolInteractionKind kind;
    public Vector2 approachOffset;
    [Min(.2f)] public float reach = 1.25f;
    public Sprite portrait;
    public Transform clickVisual;
    public Transform cloudViewAnchor;
    public SchoolLocker locker;
    public ChapterOneTarget chapterTarget;
    public string promptOverride;
    public Vector2 ApproachPoint => transform.TransformPoint(approachOffset);
    public string Prompt => !string.IsNullOrEmpty(promptOverride) ? promptOverride : kind == SchoolInteractionKind.Locker
        ? (locker.IsOpen ? "关闭柜门" : "打开柜门") : kind == SchoolInteractionKind.Sketchbook
        ? "查看画本" : kind == SchoolInteractionKind.Notice ? "阅读比赛通知" : "和" + displayName + "说话";
}
