using UnityEngine;

[DisallowMultipleComponent]
public sealed class ChapterOneAudio : MonoBehaviour
{
    public static ChapterOneAudio Instance { get; private set; }
    [Range(0,1)] public float musicVolume=.25f;
    [Range(0,1)] public float interactionVolume=.5f;
    public AudioClip musicClip,interactionClip;
    AudioSource music,effects;
    float nextSound;
    bool focused=true,paused;
    public int InteractionPlayCount { get; private set; }
    public AudioSource MusicSource => music;
    public static ChapterOneAudio Ensure()
    {
        if(Instance==null)new GameObject("ChapterOneAudio").AddComponent<ChapterOneAudio>();
        return Instance;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic(){Instance=null;}
    void Awake()
    {
        if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
        Instance=this;DontDestroyOnLoad(gameObject);
        musicClip=Resources.Load<AudioClip>("Audio/CozyLoop_Placeholder");
        interactionClip=Resources.Load<AudioClip>("Audio/Interact_Placeholder");
        music=gameObject.AddComponent<AudioSource>();effects=gameObject.AddComponent<AudioSource>();
        music.playOnAwake=false;music.loop=true;music.spatialBlend=0;music.volume=musicVolume;music.clip=musicClip;
        effects.playOnAwake=false;effects.loop=false;effects.spatialBlend=0;effects.volume=interactionVolume;
        if(musicClip!=null)music.Play();
    }
    public void PlayInteraction()
    {
        if(interactionClip==null || !focused || paused || Time.unscaledTime<nextSound)return;
        nextSound=Time.unscaledTime+.065f;
        effects.PlayOneShot(interactionClip);InteractionPlayCount++;
    }
    void Update(){music.volume=musicVolume;effects.volume=interactionVolume;}
    void OnApplicationFocus(bool value){focused=value;RefreshPause();}
    void OnApplicationPause(bool value){paused=value;RefreshPause();}
    void RefreshPause()
    {
        if(music==null)return;
        if(!focused||paused){music.Pause();effects.Pause();}
        else {music.UnPause();effects.UnPause();}
    }
    void OnDestroy(){if(Instance==this)Instance=null;}
}
