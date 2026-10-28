using System;
using UnityEngine;

public enum SchoolConversationPhase { DailyRoutine, Investigation }

// Local school interaction layer. Story progression/rewards are deliberately separate.
[DisallowMultipleComponent]
public sealed class SchoolInteractionController : MonoBehaviour
{
    public TopDownCharacterController player;
    public Transform architecture;
    public Transform solidGeometry;
    public Camera worldCamera;
    public Font chineseFont;
    public SchoolInteractable[] targets;
    public ChapterOneScene chapter;
    public SchoolConversationPhase ConversationPhase { get; private set; }
    private bool localPermission;
    public bool HasSketchbookPermission
    {
        get => chapter != null && chapter.Progress != null ? chapter.Progress.Data.sketchbookPermission : localPermission;
        private set { localPermission=value; if(value && chapter != null && chapter.Progress != null) chapter.Progress.GrantSketchbookPermission(); }
    }
    public bool DialogueOpen { get; private set; }
    public SchoolInteractable CurrentTarget { get; private set; }
    public string Speaker { get; private set; }
    public string DialogueText { get; private set; }
    public int ChoiceCount => choices.Length;
    private string[] choices = Array.Empty<string>();
    private Action[] actions = Array.Empty<Action>();
    private Sprite portrait;
    private Sprite illustration;
    public Sprite Illustration => illustration;
    private bool restoreMovement;
    private GUIStyle bodyStyle, speakerStyle, buttonStyle, hintStyle;
    private float nextInteraction;
    private readonly Color ink = new Color(.23f,.19f,.32f);
    private readonly Color paper = new Color(.94f,.94f,.84f);

    // Called by the chapter state machine once the investigation is actually unlocked.
    // Permission to touch the sketchbook alone never unlocks story clues.
    public void SetConversationPhase(SchoolConversationPhase phase) => ConversationPhase=phase;

    private void Update()
    {
        if (player == null || (chapter != null && chapter.IsTransitioning)) return;
        if (Input.GetKeyDown(KeyCode.J) && chapter != null && chapter.Investigation != null)
        { chapter.Investigation.ToggleJournal(); return; }
        if (Input.GetKeyDown(KeyCode.I) && chapter != null && chapter.DrawingStory != null)
        { chapter.DrawingStory.ToggleInventory(); return; }
        if (DialogueOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseDialogue();
            else if (Input.GetKeyDown(KeyCode.Alpha1)) Choose(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Choose(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) Choose(2);
            else if (choices.Length == 1 && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return))) Choose(0);
            return;
        }
        CurrentTarget = FindNearest();
        if (Input.GetKeyDown(KeyCode.Escape) && chapter != null) { chapter.PauseMenu(); return; }
        if (Input.GetKeyDown(KeyCode.E)) TryInteract(CurrentTarget);
        // Clicking an object uses the same proximity and wall checks as the keyboard.
        if (Input.GetMouseButtonDown(0) && CurrentTarget != null && worldCamera != null)
        {
            Vector2 p = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            foreach (var sr in (CurrentTarget.clickVisual != null ? CurrentTarget.clickVisual : CurrentTarget.transform).GetComponentsInChildren<SpriteRenderer>())
                if (sr.enabled && sr.bounds.Contains(new Vector3(p.x,p.y,sr.bounds.center.z)))
                { TryInteract(CurrentTarget); break; }
        }
    }

    public bool CanReach(SchoolInteractable target)
    {
        if (target == null || !target.isActiveAndEnabled || player == null) return false;
        if (chapter != null && !chapter.CanUse(target)) return false;
        Vector2 from = player.transform.position;
        Vector2 to = target.ApproachPoint;
        if (Vector2.Distance(from,to) > target.reach) return false;
        // Furniture can be talked across. Walls cannot. Cast to the reachable floor anchor,
        // not the wall-mounted picture or the cabinet's upper decorative portion.
        foreach (var hit in Physics2D.LinecastAll(from,to))
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            var t = hit.collider.transform;
            if ((solidGeometry != null && t.IsChildOf(solidGeometry)) ||
                (architecture != null && t.IsChildOf(architecture))) return false;
        }
        return true;
    }

    public SchoolInteractable FindNearest()
    {
        SchoolInteractable nearest = null;
        float best = float.PositiveInfinity;
        if (targets == null) return null;
        foreach (var target in targets)
        {
            if (!CanReach(target)) continue;
            float d = ((Vector2)player.transform.position-target.ApproachPoint).sqrMagnitude;
            if (d < best) { best=d; nearest=target; }
        }
        return nearest;
    }

    public bool TryInteract(SchoolInteractable target)
    {
        if (DialogueOpen || Time.unscaledTime < nextInteraction || !CanReach(target)) return false;
        nextInteraction = Time.unscaledTime + .15f;
        CurrentTarget = target;
        if (chapter != null && chapter.Handle(target)) return true;
        switch (target.kind)
        {
            case SchoolInteractionKind.Locker: return target.locker.TryToggle();
            case SchoolInteractionKind.Xiaoyu:
                Show("sheila", ConversationPhase==SchoolConversationPhase.Investigation ? "你想问画上的事吗？" :
                    HasSketchbookPermission ? "画本就在桌上。翻的时候轻一点哦。" : "嗯？我还想再改一点。",target.portrait,
                    new[]{"可以看看你的画本吗？","你还不回家吗？","先不打扰了"},new Action[]{
                        () => { HasSketchbookPermission=true; Say("sheila","可以呀，就在桌上。\n不过……还没有画好。",target.portrait); },
                        () => Say("sheila","想再画一会儿。总觉得这里还能再改一改。",target.portrait), CloseDialogue}); break;
            case SchoolInteractionKind.ClassmateOne:
                Say("Elaine","我收拾好书包就走。你也别待得太晚啦。",target.portrait); break;
            case SchoolInteractionKind.ClassmateTwo:
                Say("nate","今天教室里好安静。\n我把这一页看完就回去。",target.portrait); break;
            case SchoolInteractionKind.Sketchbook:
                if (!HasSketchbookPermission) Say("画本","这是sheila的画本。\n先问问她，再翻看吧。");
                else ShowSketchbook(0); break;
            case SchoolInteractionKind.Notice:
                Say("比赛通知","校园绘画比赛\n本次绘画主题：家。\n用自己的画，描绘心中的家。"); break;
        }
        return true;
    }

    private void ShowSketchbook(int page)
    {
        string text = page == 0 ? "纸上画着一间小屋。\n屋顶、窗户和门边，都留下了反复修改的痕迹。" :
            "几处线条被擦淡了，旁边还有新的铅笔线。\n她似乎在同一个地方修改了很多次。";
        if (ConversationPhase==SchoolConversationPhase.DailyRoutine)
            text=page==0 ? "是一幅还没完成的房屋线稿。\nsheila正在为它添上细节。" : "她在纸页边缘试了几种颜色。\n翻看完毕后，把画本轻轻合上。";
        Show("sheila的画本  "+(page+1)+" / 2",text,null,
            new[]{page==0 ? "翻到下一页" : "返回上一页","合上画本"},
            new Action[]{()=>ShowSketchbook(1-page),CloseDialogue});
    }

    public void Say(string speaker,string text,Sprite face=null) => Show(speaker,text,face,new[]{"继续"},new Action[]{CloseDialogue});
    public void ShowDialogue(string speaker,string text,Sprite face,string[] labels,Action[] callbacks) => Show(speaker,text,face,labels,callbacks);
    public void ShowIllustration(string speaker,string text,Sprite picture,string[] labels,Action[] callbacks)
    { Show(speaker,text,null,labels,callbacks); illustration=picture; }
    private void Show(string speaker,string text,Sprite face,string[] labels,Action[] callbacks)
    {
        if (!DialogueOpen) { restoreMovement=player.enabled; player.enabled=false; }
        DialogueOpen=true; Speaker=speaker; DialogueText=text; portrait=face; illustration=null; choices=labels; actions=callbacks;
    }
    public void Choose(int index)
    {
        if (!DialogueOpen || index < 0 || index >= actions.Length) return;
        actions[index]?.Invoke();
    }
    public void CloseDialogue()
    {
        if (DialogueOpen && player != null) player.enabled=restoreMovement;
        DialogueOpen=false; choices=Array.Empty<string>(); actions=Array.Empty<Action>();
        illustration=null;
        if(chapter != null && chapter.Investigation != null) chapter.Investigation.DialogueClosed();
        if(chapter != null && chapter.DrawingStory != null) chapter.DrawingStory.DialogueClosed();
        nextInteraction=Time.unscaledTime+.15f;
    }
    private void OnDisable() { CloseDialogue(); CurrentTarget=null; }
    private void OnApplicationFocus(bool focus) { if (!focus) CloseDialogue(); }

    private void InitializeStyles()
    {
        bodyStyle=new GUIStyle(GUI.skin.label){font=chineseFont,fontSize=25,wordWrap=true,richText=false};
        bodyStyle.normal.textColor=ink;
        speakerStyle=new GUIStyle(bodyStyle){fontSize=27,fontStyle=FontStyle.Bold};
        buttonStyle=new GUIStyle(bodyStyle){fontSize=22,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,12,0,0)};
        buttonStyle.normal.textColor=buttonStyle.hover.textColor=buttonStyle.active.textColor=buttonStyle.focused.textColor=ink;
        buttonStyle.onNormal.textColor=buttonStyle.onHover.textColor=buttonStyle.onActive.textColor=buttonStyle.onFocused.textColor=ink;
        hintStyle=new GUIStyle(bodyStyle){fontSize=21,alignment=TextAnchor.MiddleCenter};
    }
    private void Panel(Rect rect,Color fill)
    {
        var old=GUI.color; GUI.color=ink; GUI.DrawTexture(rect,Texture2D.whiteTexture);
        GUI.color=fill; GUI.DrawTexture(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),Texture2D.whiteTexture); GUI.color=old;
    }
    private bool Button(Rect rect,string text)
    {
        Panel(rect,rect.Contains(Event.current.mousePosition) ? new Color(.80f,.87f,.80f) : new Color(.87f,.84f,.93f));
        return GUI.Button(rect,text,buttonStyle);
    }
    private void OnGUI()
    {
        if (player == null || chineseFont == null) return;
        if (bodyStyle == null) InitializeStyles();
        var oldMatrix=GUI.matrix;
        // Lay out within the visible game viewport, including portrait and ultrawide windows.
        Rect viewport=worldCamera!=null ? worldCamera.pixelRect : new Rect(0,0,Screen.width,Screen.height);
        float scale=Mathf.Min(viewport.width/1280f,viewport.height/720f);
        Vector2 origin=new Vector2(viewport.x+(viewport.width-1280*scale)/2,Screen.height-viewport.yMax+(viewport.height-720*scale)/2);
        float extraHeight=(viewport.height-720*scale)/2;
        GUI.matrix=Matrix4x4.TRS(origin-Vector2.up*extraHeight,Quaternion.identity,Vector3.one*scale);
        if (chapter != null && chapter.Progress != null)
        {
            Panel(new Rect(28,24,810,132),paper);
            GUI.Label(new Rect(48,36,770,34),chapter.ProgressLabel,speakerStyle);
            GUI.Label(new Rect(48,78,770,70),chapter.Objective,bodyStyle);
            Panel(new Rect(858,24,394,132),paper);
            GUI.Label(new Rect(866,38,378,104),chapter.Progress.Data.cloudVisionUnlocked ?
                (chapter.CloudVisionActive ? "云视已开启 · Q 关闭" : "Q 开启云视")+"\nJ 记录 · I 物品\nE 交互 · Esc 菜单" : "WASD / 方向键移动\nE 交互 · Esc 菜单",hintStyle);
        }
        GUI.matrix=Matrix4x4.TRS(origin+Vector2.up*extraHeight,Quaternion.identity,Vector3.one*scale);
        if (DialogueOpen)
        {
            if(illustration!=null)
            {
                Panel(new Rect(456,164,368,180),paper);
                Rect uv=new Rect(illustration.rect.x/illustration.texture.width,illustration.rect.y/illustration.texture.height,
                    illustration.rect.width/illustration.texture.width,illustration.rect.height/illustration.texture.height);
                float h=164,w=h*illustration.rect.width/illustration.rect.height;
                if(w>348) { w=348; h=w*illustration.rect.height/illustration.rect.width; }
                GUI.DrawTextureWithTexCoords(new Rect(640-w/2,254-h/2,w,h),illustration.texture,uv);
            }
            Panel(new Rect(100,354,1080,354),paper);
            float left=140;
            if (portrait != null)
            {
                Panel(new Rect(128,378,108,120),new Color(.83f,.88f,.82f));
                Rect uv=new Rect(portrait.rect.x/portrait.texture.width,portrait.rect.y/portrait.texture.height,
                    portrait.rect.width/portrait.texture.width,portrait.rect.height/portrait.texture.height);
                float h=Mathf.Min(96,96*portrait.rect.height/portrait.rect.width),w=h*portrait.rect.width/portrait.rect.height;
                GUI.DrawTextureWithTexCoords(new Rect(182-w/2,390,w,h),portrait.texture,uv);
                left=258;
            }
            GUI.Label(new Rect(left,376,850,38),Speaker,speakerStyle);
            GUI.Label(new Rect(left,424,850,124),DialogueText,bodyStyle);
            for (int i=0;i<choices.Length;i++)
                if (Button(new Rect(136,542+i*42,1008,38),(i+1)+"  "+choices[i])) { Choose(i); break; }
            GUI.Label(new Rect(140,674,1000,28),"数字键 / 鼠标选择    ·    Esc 结束对话",hintStyle);
        }
        else if (CurrentTarget != null && CanReach(CurrentTarget))
        {
            if (Button(new Rect(410,644,460,50),"E   "+CurrentTarget.Prompt)) TryInteract(CurrentTarget);
        }
        if (chapter != null && chapter.IsTransitioning)
        {
            GUI.matrix=Matrix4x4.TRS(origin,Quaternion.identity,Vector3.one*scale);
            Panel(new Rect(410,300,460,100),paper);
            GUI.Label(new Rect(430,330,420,40),"正在穿过柜门……",hintStyle);
        }
        GUI.matrix=oldMatrix;
    }
}
