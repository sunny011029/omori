using UnityEngine;

// Keep a compact silhouette for every expression; only the sprite changes.
public sealed class ChapterOneCloudExpression : MonoBehaviour
{
    public ChapterOneScene scene;
    SpriteRenderer spriteRenderer;
    ChapterOneStoryArt art;
    void Awake() { spriteRenderer=GetComponent<SpriteRenderer>(); art=Resources.Load<ChapterOneStoryArt>("ChapterOneStoryArt"); }
    void Update()
    {
        if(art==null||scene==null||spriteRenderer==null) return;
        if(scene.interaction.DialogueOpen && scene.interaction.Speaker=="抹茶云")
            spriteRenderer.sprite=Mathf.Repeat(Time.unscaledTime, .55f)<.28f?art.cloudTalking:art.cloudNeutral;
        else spriteRenderer.sprite=Mathf.Repeat(Time.unscaledTime,4.6f)<.16f?art.cloudClosedEyes:art.cloudNeutral;
    }
}
