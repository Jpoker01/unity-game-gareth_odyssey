using UnityEngine;

/// <summary>
/// Code-driven sprite animation for Gareth.
/// Reads PlayerController state and swaps SpriteRenderer.sprite directly — no Animator needed.
/// GarethSetup step 4 auto-populates the frame arrays from the sprite sheet.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerController))]
public class PlayerSpriteAnimation : MonoBehaviour
{
    [Header("Frame Arrays (auto-populated by Gareth Odyssey > Setup Animation Sprites)")]
    public Sprite[] idle;
    public Sprite[] walk;
    public Sprite[] jump;
    public Sprite[] fall;
    public Sprite[] hurt;
    public Sprite[] toolUse;

    [Header("Playback FPS")]
    public float idleFps   = 8f;
    public float walkFps   = 12f;
    public float jumpFps   = 12f;
    public float fallFps   = 12f;
    public float hurtFps   = 12f;
    public float toolFps   = 14f;

    SpriteRenderer  sr;
    PlayerController pc;

    Sprite[] cur;
    float    fps;
    float    timer;
    int      frame;
    bool     loop;
    bool     oneShot; // true while a non-looping clip that was explicitly triggered is playing

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        pc = GetComponent<PlayerController>();

        // We own the sprite — disable Unity's Animator so it doesn't overwrite us.
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        pc.onToolUse.AddListener(TriggerToolUse);
    }

    void OnDestroy()
    {
        if (pc != null) pc.onToolUse.RemoveListener(TriggerToolUse);
    }

    void Update()
    {
        SelectClip();
        Tick();
        if (cur != null && cur.Length > 0)
            sr.sprite = cur[frame];
    }

    // ── Clip selection ───────────────────────────────────────────────────────

    void SelectClip()
    {
        // Hurt always interrupts everything.
        if (pc.IsHurt)           { PlayIfNew(hurt, hurtFps, false); return; }
        // Respect one-shot clips (tool use) until they finish.
        if (oneShot)             return;
        // Airborne
        if (!pc.IsGrounded)
        {
            if (pc.VelocityY > 0.1f) PlayIfNew(jump, jumpFps, false);
            else                      PlayIfNew(fall, fallFps, false);
            return;
        }
        // Ground movement
        if (Mathf.Abs(pc.HorizontalInput) > 0.01f) PlayIfNew(walk, walkFps, true);
        else                                         PlayIfNew(idle, idleFps, true);
    }

    void PlayIfNew(Sprite[] clip, float newFps, bool doLoop)
    {
        if (cur == clip || clip == null || clip.Length == 0) return;
        cur   = clip;
        fps   = newFps;
        loop  = doLoop;
        timer = 0f;
        frame = 0;
    }

    void TriggerToolUse()
    {
        if (toolUse == null || toolUse.Length == 0) return;
        cur     = toolUse;
        fps     = toolFps;
        loop    = false;
        timer   = 0f;
        frame   = 0;
        oneShot = true;
    }

    // ── Frame advance ────────────────────────────────────────────────────────

    void Tick()
    {
        if (cur == null || cur.Length == 0 || fps <= 0f) return;
        timer += Time.deltaTime;
        float spf = 1f / fps;
        while (timer >= spf)
        {
            timer -= spf;
            frame++;
            if (frame >= cur.Length)
            {
                if (loop)   { frame = 0; }
                else        { frame = cur.Length - 1; oneShot = false; return; }
            }
        }
    }
}
