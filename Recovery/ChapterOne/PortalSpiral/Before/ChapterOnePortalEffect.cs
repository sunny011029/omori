using UnityEngine;

// Reuses the existing pixel rectangle and material; children are transient visuals.
[DisallowMultipleComponent]
public sealed class ChapterOnePortalEffect : MonoBehaviour
{
    SpriteRenderer[] glow=new SpriteRenderer[5], motes=new SpriteRenderer[12];
    SpriteRenderer[] ring;
    Transform space;
    Vector2 size;
    float elapsed;
    public void Configure()
    {
        if(space!=null)return;
        var originals=GetComponentsInChildren<SpriteRenderer>(true);
        if(originals.Length==0)return;
        var source=originals[0];var bounds=source.bounds;
        foreach(var sr in originals)bounds.Encapsulate(sr.bounds);
        size=bounds.size;
        var go=new GameObject("AnimatedGreenMagic");space=go.transform;space.SetParent(transform,false);
        space.position=bounds.center;
        var scale=transform.lossyScale;space.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        for(int i=0;i<glow.Length;i++)glow[i]=Make(source,"Glow_"+i,source.sortingOrder+1+i);
        for(int i=0;i<motes.Length;i++)motes[i]=Make(source,"Mote_"+i,source.sortingOrder+7);
        if(size.x>1f)
        {
            ring=new SpriteRenderer[24];
            for(int i=0;i<ring.Length;i++)ring[i]=Make(source,"Rune_"+i,source.sortingOrder+6);
        }
        Animate(0);
    }
    SpriteRenderer Make(SpriteRenderer source,string name,int order)
    {
        var go=new GameObject(name);go.transform.SetParent(space,false);
        var sr=go.AddComponent<SpriteRenderer>();sr.sprite=source.sprite;sr.sharedMaterial=source.sharedMaterial;
        sr.sortingLayerID=source.sortingLayerID;sr.sortingOrder=order;return sr;
    }
    void Shape(SpriteRenderer sr,Vector2 position,Vector2 dimensions,Color color)
    {
        sr.transform.localPosition=position;
        sr.transform.localScale=new Vector3(dimensions.x/sr.sprite.bounds.size.x,dimensions.y/sr.sprite.bounds.size.y,1);
        sr.color=color;
    }
    void OnEnable(){elapsed=0;}
    void Update(){elapsed+=Time.deltaTime;if(space!=null)Animate(elapsed);}
    void Animate(float t)
    {
        float breath=.5f+.5f*Mathf.Sin(t*1.7f);
        for(int i=0;i<glow.Length;i++)
        {
            float expand=1.35f-i*.12f+breath*.08f;
            Shape(glow[i],Vector2.zero,size*expand,new Color(.28f,.96f,.57f,.035f+breath*.025f));
        }
        for(int i=0;i<motes.Length;i++)
        {
            float p=Mathf.Repeat(t*(.22f+i%3*.035f)+i/12f,1);
            float x=Mathf.Sin(i*2.4f+t*.7f)*size.x*.48f;
            float y=(p-.25f)*(size.y+1f);
            float side=.045f+(i%3)*.018f;
            Shape(motes[i],new Vector2(x,y),Vector2.one*side,new Color(.64f,1f,.72f,Mathf.Sin(p*Mathf.PI)*.85f));
        }
        if(ring!=null)for(int i=0;i<ring.Length;i++)
        {
            float angle=i*Mathf.PI*2/ring.Length;
            float pulse=1+breath*.1f;
            Shape(ring[i],new Vector2(Mathf.Cos(angle)*size.x*.62f*pulse,Mathf.Sin(angle)*size.y*.65f*pulse),
                new Vector2(.09f,.045f),new Color(.57f,1,.68f,.4f+breath*.35f));
        }
    }
}
