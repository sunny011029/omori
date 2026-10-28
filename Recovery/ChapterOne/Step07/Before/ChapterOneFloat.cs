using UnityEngine;

public sealed class ChapterOneFloat : MonoBehaviour
{
    public float amplitude=.04f;
    public float speed=1.6f;
    Vector3 origin;
    void OnEnable() { origin=transform.localPosition; }
    void Update() { transform.localPosition=origin+Vector3.up*(Mathf.Sin(Time.time*speed)*amplitude); }
    void OnDisable() { transform.localPosition=origin; }
}
