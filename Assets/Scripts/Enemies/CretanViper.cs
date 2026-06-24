using UnityEngine;

/// <summary>
/// Macrovipera — Cretan Viper. Slither / Rear / Strike / Recoil state machine
/// (Level 2 Enemy Design — Enemy 4): the animated cousin of the GDD's hidden
/// spike-pit hazard. A single trigger hitbox is resized per state — small while
/// coiled (the constant "danger lurking in the undergrowth"), then stretched
/// ~1 tile forward for the brief Strike lunge — so both the body and the lunge
/// damage Gareth through the same TakeHit knockback used everywhere else
/// (a head-stomp bypasses all of that and puts the snake down outright).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class CretanViper : MonoBehaviour
{
    enum State { Slither, Rear, Strike, Recoil }

    [Header("Patrol (per-instance — very small zone, 2-3 tiles)")]
    public float patrolWidth  = 2.5f;
    public float slitherSpeed = 0.4f;

    [Header("Detection — constant across difficulties")]
    public float rearTriggerRange = 2f;
    public float detectionYBand   = 0.75f;

    [Header("Telegraph & Strike Timings")]
    public float rearDuration   = 0.4f;
    public float strikeDuration = 0.25f;
    public float recoilDuration = 1.5f;
    public float lungeDistance  = 1f;

    [Header("Hitbox — resizes per state (doc: coiled ≈32×32, extended ≈48×16)")]
    public Vector2 coiledHitboxSize = new Vector2(0.8f, 0.8f);
    public Vector2 lungeHitboxSize  = new Vector2(1f, 0.5f);

    [Header("Body — solid collider for ground physics (so it falls if it slithers off a ledge)")]
    public Vector2 bodySize   = new Vector2(0.8f, 0.8f);
    public Vector2 bodyOffset = Vector2.zero;

    [Header("Tongue Flicker — Slither only, every ~1.5 s")]
    public float    tongueFlickerInterval = 1.5f;
    public Sprite[] tongueFlickFrames;
    public float    tongueFlickFps = 10f;

    [Header("Audio (optional — soft hiss when rearing)")]
    public AudioClip hissSound;

    [Header("Animation Frames (sliced by Level 2 Enemy Setup)")]
    public Sprite[] slitherFrames;
    public Sprite[] rearFrames;
    public Sprite[] strikeFrames;
    public Sprite[] recoilFrames;
    public float slitherFps = 6f;
    public float rearFps    = 10f;
    public float strikeFps  = 16f;
    public float recoilFps  = 6f;

    [Header("Sprite Orientation")]
    public bool artFacesRight = true;

    SpriteRenderer    sr;
    BoxCollider2D     hitbox;
    Rigidbody2D       rb;
    CapsuleCollider2D body;
    SpriteFlipbook    anim;
    SpriteRenderer    tongueRenderer;
    SpriteFlipbook    tongueAnim;
    AudioSource       audioSource;
    PlayerController  player;

    State state = State.Slither;
    float originX;
    int   patrolDir = 1;
    bool  facingRight = true;
    float stateTimer;
    float tongueTimer;
    bool  contactLatched;

    void Awake()
    {
        sr     = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size      = coiledHitboxSize;
        anim = new SpriteFlipbook(sr);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        EnemyPhysics.GroundFall(gameObject, bodySize, bodyOffset, out rb, out body);

        BuildTongue();
    }

    void Start()
    {
        player      = FindFirstObjectByType<PlayerController>();
        originX     = transform.position.x;
        tongueTimer = tongueFlickerInterval;
        SetFacing(true);
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Slither: TickSlither(dt); break;
            case State.Rear:    TickRear(dt);    break;
            case State.Strike:  TickStrike(dt);  break;
            case State.Recoil:  TickRecoil(dt);  break;
        }

        anim.Tick(dt);
        if (tongueAnim != null) tongueAnim.Tick(dt);
    }

    // ── SLITHER ───────────────────────────────────────────────────────────────
    void TickSlither(float dt)
    {
        anim.Play(slitherFrames, slitherFps, true);

        float halfWidth = patrolWidth * 0.5f;
        float left  = originX - halfWidth;
        float right = originX + halfWidth;

        float nextX = transform.position.x + slitherSpeed * dt * patrolDir;
        if      (patrolDir > 0 && nextX >= right) { nextX = right; patrolDir = -1; SetFacing(false); }
        else if (patrolDir < 0 && nextX <= left)  { nextX = left;  patrolDir =  1; SetFacing(true);  }
        transform.position = new Vector3(nextX, transform.position.y, transform.position.z);

        TickTongue(dt);
        if (PlayerWithinRange(rearTriggerRange)) EnterRear();
    }

    void TickTongue(float dt)
    {
        if (tongueRenderer == null) return;

        tongueTimer -= dt;
        if (tongueTimer <= 0f && tongueFlickFrames != null && tongueFlickFrames.Length > 0)
        {
            tongueTimer = tongueFlickerInterval;
            tongueRenderer.gameObject.SetActive(true);
            tongueAnim.Play(tongueFlickFrames, tongueFlickFps, false, restart: true);
        }
        if (tongueAnim != null && tongueAnim.IsFinished)
            tongueRenderer.gameObject.SetActive(false);
    }

    // ── REAR — telegraph window, holds position ──────────────────────────────
    void EnterRear()
    {
        state      = State.Rear;
        stateTimer = rearDuration;
        if (tongueRenderer != null) tongueRenderer.gameObject.SetActive(false);
        SetFacing(player.transform.position.x >= transform.position.x);
        anim.Play(rearFrames, rearFps, false, restart: true);
        if (hissSound != null) audioSource.PlayOneShot(hissSound);
    }

    void TickRear(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f) EnterStrike();
    }

    // ── STRIKE — hitbox stretches ~1 tile forward for the lunge window ───────
    void EnterStrike()
    {
        state      = State.Strike;
        stateTimer = strikeDuration;
        anim.Play(strikeFrames, strikeFps, false, restart: true);

        Vector2 dir   = facingRight ? Vector2.right : Vector2.left;
        hitbox.size   = lungeHitboxSize;
        hitbox.offset = new Vector2(dir.x * lungeDistance * 0.5f, 0f);
    }

    void TickStrike(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f) EnterRecoil();
    }

    // ── RECOIL — hitbox shrinks back to "coiled only"; safe to step around ───
    void EnterRecoil()
    {
        state         = State.Recoil;
        stateTimer    = recoilDuration;
        hitbox.size   = coiledHitboxSize;
        hitbox.offset = Vector2.zero;
        anim.Play(recoilFrames, recoilFps, false, restart: true);
    }

    void TickRecoil(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f)
        {
            state       = State.Slither;
            tongueTimer = tongueFlickerInterval;
        }
    }

    // ── Detection ─────────────────────────────────────────────────────────────
    bool PlayerWithinRange(float range)
    {
        Vector3 p = player.transform.position;
        Vector3 m = transform.position;
        return Mathf.Abs(p.y - m.y) <= detectionYBand && Mathf.Abs(p.x - m.x) <= range;
    }

    // ── Contact — the one resizing hitbox covers body & lunge; stomped, it dies ──
    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null || contactLatched) return;
        contactLatched = true;

        if (pc.IsStompContact(hitbox))
        {
            pc.BounceFromStomp();
            StompDeath.Apply(gameObject, this, hitbox);
            return;
        }
        if (!pc.IsHurt) pc.TakeHit(transform.position);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) contactLatched = false;
    }

    // ── Facing & child visuals ────────────────────────────────────────────────
    void SetFacing(bool faceRight)
    {
        facingRight = faceRight;
        sr.flipX = artFacesRight ? !faceRight : faceRight;
        if (tongueRenderer != null)
            tongueRenderer.transform.localPosition = new Vector3(faceRight ? 0.4f : -0.4f, 0f, 0f);
    }

    void BuildTongue()
    {
        var go = new GameObject("TongueFlicker");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.4f, 0f, 0f);
        tongueRenderer = go.AddComponent<SpriteRenderer>();
        tongueRenderer.sortingOrder = sr.sortingOrder + 1;
        tongueAnim = new SpriteFlipbook(tongueRenderer);
        go.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        float baseX     = Application.isPlaying ? originX : transform.position.x;
        float halfWidth = patrolWidth * 0.5f;

        Gizmos.color = new Color(0.4f, 0.5f, 0.2f, 0.8f);
        Vector3 a = new Vector3(baseX - halfWidth, transform.position.y, 0f);
        Vector3 b = new Vector3(baseX + halfWidth, transform.position.y, 0f);
        Gizmos.DrawLine(a, b);

        Gizmos.color = new Color(0.8f, 0.1f, 0.5f, 0.3f);
        Gizmos.DrawWireCube(transform.position, new Vector3(rearTriggerRange * 2f, detectionYBand * 2f, 0f));

        Vector2 boxSize   = Application.isPlaying ? hitbox.size   : coiledHitboxSize;
        Vector2 boxOffset = Application.isPlaying ? hitbox.offset : Vector2.zero;
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireCube((Vector2)transform.position + boxOffset, boxSize);
    }
}
