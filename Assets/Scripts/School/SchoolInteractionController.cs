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
    private float illustrationProgress=-1;
    private float draftAnimation=-1,checklistAnimation=-1;
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
        bool accepted=TryInteractCore(target);
        if(accepted)ChapterOneAudio.Ensure().PlayInteraction();
        return accepted;
    }
    private bool TryInteractCore(SchoolInteractable target)
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
    public void SetIllustrationProgress(float value) => illustrationProgress=value<0?-1:Mathf.Clamp01(value);
    public void SetDraftAnimation(float value) => draftAnimation=Mathf.Clamp01(value);
    public void SetChecklistAnimation(float value) => checklistAnimation=Mathf.Clamp01(value);
    private void Show(string speaker,string text,Sprite face,string[] labels,Action[] callbacks)
    {
        if (!DialogueOpen) { restoreMovement=player.enabled; player.enabled=false; }
        DialogueOpen=true; Speaker=speaker; DialogueText=text; portrait=face; illustration=null; illustrationProgress=-1;
        draftAnimation=checklistAnimation=-1; choices=labels; actions=callbacks;
    }
    public void Choose(int index)
    {
        if (!DialogueOpen || index < 0 || index >= actions.Length) return;
        ChapterOneAudio.Ensure().PlayInteraction();
        actions[index]?.Invoke();
    }
    public void CloseDialogue()
    {
        if (DialogueOpen && player != null) player.enabled=restoreMovement;
        DialogueOpen=false; choices=Array.Empty<string>(); actions=Array.Empty<Action>();
        illustration=null;
        if(chapter != null && chapter.Investigation != null) chapter.Investigation.DialogueClosed();
        if(chapter != null && chapter.DrawingStory != null) chapter.DrawingStory.DialogueClosed();
        if(chapter != null && chapter.Ending != null) chapter.Ending.DialogueClosed();
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
    private void DrawPicture(Sprite sprite,Rect rect)
    {
        if(sprite==null) return;
        GUI.DrawTextureWithTexCoords(rect,sprite.texture,new Rect(sprite.rect.x/sprite.texture.width,sprite.rect.y/sprite.texture.height,
            sprite.rect.width/sprite.texture.width,sprite.rect.height/sprite.texture.height));
    }
    private void DrawStoryAnimation()
    {
        if(draftAnimation>=0)
        {
            Panel(new Rect(456,164,368,180),paper);
            Panel(new Rect(524,192,242,126),new Color(.57f,.47f,.67f));
            Panel(new Rect(541,200,215,110),new Color(.99f,.98f,.88f));
            Panel(new Rect(535,199,9,112),ink);
            float slide=Mathf.SmoothStep(0,1,Mathf.Clamp01(draftAnimation/.7f));
            var a=new Rect(472,172,162,108); var b=new Rect(554,207,181,96);
            DrawPicture(illustration,new Rect(Mathf.Lerp(a.x,b.x,slide),Mathf.Lerp(a.y,b.y,slide),Mathf.Lerp(a.width,b.width,slide),Mathf.Lerp(a.height,b.height,slide)));
            if(draftAnimation>.65f)
            {
                // A small hand presses the page flat, then withdraws. UI-only geometry.
                float press=Mathf.Sin(Mathf.InverseLerp(.65f,1,draftAnimation)*Mathf.PI);
                Panel(new Rect(710,294-press*26,37,20),new Color(.94f,.78f,.70f));
            }
            GUI.Label(new Rect(470,315,340,25),draftAnimation<.7f?"夹入第一版草稿":"轻轻压平纸页",hintStyle);
        }
        else if(checklistAnimation>=0)
        {
            Panel(new Rect(340,164,600,180),paper);
            float t=checklistAnimation;
            if(t>=.3f && t<.57f)
            {
                Panel(new Rect(424,184,432,128),new Color(.57f,.47f,.67f));
                GUI.Label(new Rect(450,222,380,40),"我的记事本",hintStyle);
            }
            else
            {
                Panel(new Rect(369,186,542,118),new Color(.99f,.98f,.88f));
                float crossed=t<.3f?Mathf.Clamp01(t/.22f):1-Mathf.InverseLerp(.57f,.9f,t);
                GUI.Label(new Rect(435,208,440,44),ChapterOneEnding.ChecklistItem,speakerStyle);
                Panel(new Rect(393,218,24,24),paper);
                if(crossed>0)
                {
                    Color previous=GUI.color; GUI.color=new Color(ink.r,ink.g,ink.b,crossed);
                    GUI.DrawTexture(new Rect(398,223,14,14),Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(433,232,350*crossed,3),Texture2D.whiteTexture); GUI.color=previous;
                }
            }
            GUI.Label(new Rect(354,312,572,28),t<.3f?"划掉完成的事项":t<.57f?"合上记事本":"同一条事项，悄悄恢复了",hintStyle);
        }
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
            if(draftAnimation>=0 || checklistAnimation>=0) DrawStoryAnimation();
            else if(illustration!=null)
            {
                Panel(new Rect(456,164,368,180),paper);
                Rect uv=new Rect(illustration.rect.x/illustration.texture.width,illustration.rect.y/illustration.texture.height,
                    illustration.rect.width/illustration.texture.width,illustration.rect.height/illustration.texture.height);
                float h=illustrationProgress>=0?132:164,w=h*illustration.rect.width/illustration.rect.height;
                if(w>348) { w=348; h=w*illustration.rect.height/illustration.rect.width; }
                GUI.DrawTextureWithTexCoords(new Rect(640-w/2,(illustrationProgress>=0?240:254)-h/2,w,h),illustration.texture,uv);
                if(illustrationProgress>=0)
                {
                    Panel(new Rect(474,315,332,16),new Color(.83f,.85f,.78f));
                    Color previous=GUI.color; GUI.color=new Color(.48f,.65f,.38f);
                    GUI.DrawTexture(new Rect(478,319,324*illustrationProgress,8),Texture2D.whiteTexture); GUI.color=previous;
                }
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
