using UnityEngine;

// Small runtime accents reuse the existing cloud's pixel sprite and material.
// No changes to NPC art, world colliders, or scene furniture are required.
public sealed class ChapterOneWeather : MonoBehaviour
{
    SchoolInteractionKind kind;
    SpriteRenderer[] marks;
    Vector3[] origins;
    float elapsed;
    SpriteRenderer[] cloudParts;
    Color[] greyColors;
    public void Configure(SchoolInteractionKind value)
    {
        if(marks!=null) return;
        kind=value;
        var source=GetComponentInChildren<SpriteRenderer>(true);
        if(source==null || source.sprite==null) return;
        cloudParts=GetComponentsInChildren<SpriteRenderer>(true);
        greyColors=new Color[cloudParts.Length];
        for(int i=0;i<cloudParts.Length;i++) greyColors[i]=cloudParts[i].color;
        marks=new SpriteRenderer[kind==SchoolInteractionKind.Xiaoyu?6:3];
        origins=new Vector3[marks.Length];
        for(int i=0;i<marks.Length;i++)
        {
            var go=new GameObject("Weather_"+i); go.transform.SetParent(transform,false);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=source.sprite; sr.sharedMaterial=source.sharedMaterial;
            sr.sortingLayerID=source.sortingLayerID; sr.sortingOrder=15;
            Vector2 size,position; Color color;
            if(kind==SchoolInteractionKind.Xiaoyu)
            {
                bool link=i>=3;
                size=link?new Vector2(.04f,.04f):new Vector2(.025f,.07f);
                position=link?new Vector2(.43f+(i-3)*.12f,-.12f-(i-3)*.16f):new Vector2(-.38f+i*.38f,-.07f);
                color=link?new Color(.62f,.83f,.73f,.7f):new Color(.64f,.73f,.83f,.8f);
            }
            else if(kind==SchoolInteractionKind.ClassmateOne)
            {
                size=new Vector2(.07f,.09f); position=new Vector2(.35f+(i==1?-.05f:0),-.02f-i*.06f);
                color=new Color(.91f,.85f,.61f,.85f);
            }
            else
            {
                size=new Vector2(.33f+i*.08f,.04f); position=new Vector2(i%2==0?-.3f:.24f,-.06f-i*.07f);
                color=new Color(.72f,.72f,.78f,.55f);
            }
            go.transform.localScale=new Vector3(size.x/source.sprite.bounds.size.x,size.y/source.sprite.bounds.size.y,1);
            go.transform.localPosition=position; sr.color=color; marks[i]=sr; origins[i]=position;
        }
        ApplyStability();
    }
    public void ApplyStability()
    {
        if(kind!=SchoolInteractionKind.Xiaoyu || marks==null) return;
        bool resolved=ChapterOneProgress.Instance!=null && ChapterOneProgress.Instance.DraftKept;
        for(int i=0;i<marks.Length;i++) marks[i].enabled=!resolved;
        for(int i=0;i<cloudParts.Length;i++)
            cloudParts[i].color=resolved ? (cloudParts[i].name=="Rim"?new Color(.72f,.82f,.83f):cloudParts[i].name=="Top"?Color.white:new Color(.94f,.98f,1f)) : greyColors[i];
    }
    void OnEnable() { elapsed=0; ApplyStability(); }
    void Update()
    {
        if(marks==null) return;
        elapsed+=Time.deltaTime;
        ApplyStability();
        for(int i=0;i<marks.Length;i++)
        {
            if(kind==SchoolInteractionKind.Xiaoyu)
            {
                if(i<3) marks[i].transform.localPosition=origins[i]+Vector3.down*(Mathf.Repeat(elapsed*.27f+i*.07f,.23f));
                else
                {
                    var c=marks[i].color; c.a=.35f+.3f*(.5f+.5f*Mathf.Sin(elapsed*1.5f-i)); marks[i].color=c;
                }
            }
            else if(kind==SchoolInteractionKind.ClassmateOne) marks[i].enabled=Mathf.Repeat(elapsed,4.8f)<.38f;
            else marks[i].transform.localPosition=origins[i]+Vector3.right*(Mathf.Sin(elapsed*.7f+i)*.06f);
        }
    }
}
