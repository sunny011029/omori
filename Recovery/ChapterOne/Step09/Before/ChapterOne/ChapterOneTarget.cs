using UnityEngine;

public enum ChapterOneAction { Clean, RecurringPaper, SchoolExit, ShopCloud, CraftTable, ReturnToSchool }

public sealed class ChapterOneTarget : MonoBehaviour
{
    public ChapterOneAction action;
    public int index;
    public GameObject visual;
    public SchoolInteractable interaction;
}
