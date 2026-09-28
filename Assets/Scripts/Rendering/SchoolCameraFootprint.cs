using UnityEngine;

// The school's footprint is a union, not its outer bounding box (which includes the courtyard).
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class SchoolCameraFootprint : MonoBehaviour
{
    [SerializeField] private Transform building;
    [SerializeField] private Rect[] areas = new Rect[0]; // left, right, corridor, entrance
    [SerializeField] private float transitionDistance = 1.25f;
    [SerializeField] private float zoomSmoothTime = .15f;
    private float zoomVelocity;
    private SpriteRenderer subject;

    public void Configure(Transform space, Rect[] rectangles) { building=space; areas=rectangles; }
    public bool IsConfigured => building != null && areas.Length == 4;
    public Rect WorldArea(int index)
    {
        var r=areas[index]; Vector2 a=building.TransformPoint(r.min), b=building.TransformPoint(r.max);
        return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
    }

    public void PrepareView(Camera camera, Transform target, float preferredSize, float deltaTime)
    {
        // Pillarbox unusually wide windows so even the narrow entrance can show the entire character.
        float fullAspect=camera.targetTexture!=null ? (float)camera.targetTexture.width/camera.targetTexture.height
            : (float)Mathf.Max(1,Screen.width)/Mathf.Max(1,Screen.height);
        float viewportWidth=Mathf.Min(1f,(16f/9f)/fullAspect);
        camera.rect=new Rect((1f-viewportWidth)*.5f,0,viewportWidth,1);
        camera.aspect=fullAspect*viewportWidth;
        float aspect=camera.aspect;
        Vector2 p=building.InverseTransformPoint(target != null ? target.position : transform.position);
        float hall=Fit(2,aspect,preferredSize), desired=hall;
        if(p.y>4f && (p.x<9.3f || p.x>10.7f))
        {
            int room=p.x<9.3f ? 0 : 1;
            desired=Mathf.Lerp(hall,Fit(room,aspect,preferredSize),Mathf.SmoothStep(0,1,(p.y-4f)/transitionDistance));
        }
        else if(p.y<.75f && p.x>5.7f && p.x<9.3f)
            desired=Mathf.Lerp(Fit(3,aspect,preferredSize),hall,Mathf.SmoothStep(0,1,(p.y+.1f)/.85f));
        float size=deltaTime>0 ? Mathf.SmoothDamp(camera.orthographicSize,desired,ref zoomVelocity,zoomSmoothTime,Mathf.Infinity,deltaTime) : desired;
        if(Mathf.Abs(size-desired)<.002f) size=desired;
        if(p.y<-.15f) size=Mathf.Min(size,Fit(3,aspect,preferredSize));
        float largest=0;
        for(int i=0;i<areas.Length;i++) largest=Mathf.Max(largest,Fit(i,aspect,preferredSize));
        if(target != null && (subject==null || subject.transform!=target)) subject=target.GetComponent<SpriteRenderer>();
        // During a room exit, zoom smoothing must not keep a view too large for the corridor.
        // Limit the view to regions that can actually contain the complete character now.
        float visibleFit=0;
        if(subject!=null)
        {
            Bounds bounds=subject.bounds;
            for(int index=0;index<areas.Length;index++)
            {
                Rect area=WorldArea(index);
                if(area.xMin<=bounds.min.x-.03f && area.xMax>=bounds.max.x+.03f &&
                    area.yMin<=bounds.min.y-.03f && area.yMax>=bounds.max.y+.03f)
                    visibleFit=Mathf.Max(visibleFit,Fit(index,aspect,preferredSize));
            }
        }
        camera.orthographicSize=Mathf.Min(size,visibleFit>0 ? visibleFit : largest);
    }

    float Fit(int i,float aspect,float preferred)
    {
        Rect r=WorldArea(i);
        return Mathf.Min(preferred,r.height*.5f,r.width/(2f*aspect));
    }

    public Vector2 Project(Vector2 position,Camera camera,bool keepCharacterVisible=true)
    {
        Vector2 half=new Vector2(camera.orthographicSize*camera.aspect,camera.orthographicSize);
        if(keepCharacterVisible && subject!=null && TryProject(position,half,true,out var visible)) return visible;
        return TryProject(position,half,false,out var safe) ? safe : position;
    }

    bool TryProject(Vector2 position,Vector2 half,bool includeSubject,out Vector2 best)
    {
        best=position; float score=float.PositiveInfinity;
        for(int i=0;i<areas.Length;i++)
        {
            Rect r=WorldArea(i); Vector2 low=r.min+half, high=r.max-half;
            if(low.x>high.x+.0001f || low.y>high.y+.0001f) continue;
            if(low.x>high.x) low.x=high.x=r.center.x;
            if(low.y>high.y) low.y=high.y=r.center.y;
            if(includeSubject)
            {
                Bounds b=subject.bounds;
                low=Vector2.Max(low,(Vector2)b.max+Vector2.one*.03f-half);
                high=Vector2.Min(high,(Vector2)b.min-Vector2.one*.03f+half);
                if(low.x>high.x || low.y>high.y) continue;
            }
            Vector2 candidate=new Vector2(Mathf.Clamp(position.x,low.x,high.x),Mathf.Clamp(position.y,low.y,high.y));
            float distance=(candidate-position).sqrMagnitude;
            if(distance<score) { score=distance; best=candidate; }
        }
        return !float.IsPositiveInfinity(score);
    }

    public bool ContainsViewport(Camera camera,float tolerance=.002f)
    {
        Vector2 half=new Vector2(camera.orthographicSize*camera.aspect,camera.orthographicSize), p=camera.transform.position;
        for(int i=0;i<areas.Length;i++)
        {
            Rect r=WorldArea(i);
            if(p.x-half.x>=r.xMin-tolerance && p.x+half.x<=r.xMax+tolerance && p.y-half.y>=r.yMin-tolerance && p.y+half.y<=r.yMax+tolerance) return true;
        }
        return false;
    }

    private void OnDisable()
    {
        var camera=GetComponent<Camera>(); camera.rect=new Rect(0,0,1,1); camera.ResetAspect();
    }
}
