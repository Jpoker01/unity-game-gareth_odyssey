using UnityEngine;

/// <summary>
/// Sagittarius — Roman Archer. Idle / Draw / Release state machine
/// (Level 2 Enemy Design — Enemy 2). Stands stationary on an elevated platform
/// and fires on a timer. Continuously faces the player (flips left/right each
/// frame) and fires in that same direction. A stomp from above defeats it
/// outright; any other touch costs a life via PlayerController.TakeHit.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class RomanArcher : MonoBehaviour
{
    enum State { Idle, Draw, Release }

    [Header("Idle Duration by Difficulty (archer fire rate)")]
    public float idleDurationEasy   = 2.5f;
    public float idleDurationMedium = 2.0f;
    public float idleDurationHard   = 1.5f;

    [Header("Draw — telegraph window")]
    public float drawDuration = 0.6f;

    [Header("Arrow — spawned at a fixed offset that flips with facing direction")]
    public Arrow  arrowPrefab;
    public float  arrowSpeed     = 5f;
    public float  bowTipForward  = 0.5f;
    public float  bowTipHeight   = 0.1f;

    [Header("Animation Frames (sliced by Level 2 Enemy Setup)")]
    public Sprite[] idleFrames;
    public Sprite[] drawFrames;
    public Sprite[] releaseFrames;
    public float idleFps    = 5f;
    public float drawFps    = 5f;
    public float releaseFps = 10f;

    [Header("Sprite Orientation")]
    [Tooltip("Tick if the source artwork already faces right by default.")]
    public bool artFacesRight = true;

    [Header("Contact Box (trigger — for stomp and hit detection)")]
    public Vector2 contactSize   = new Vector2(1f, 1f);
    public Vector2 contactOffset = Vector2.zero;

    [Header("Body — solid collider for ground physics (rests on its platform under real gravity)")]
    public Vector2 bodySize   = new Vector2(1f, 1f);
    public Vector2 bodyOffset = Vector2.zero;

    SpriteRenderer    sr;
    BoxCollider2D     col;
    SpriteFlipbook    anim;
    PlayerController  player;
    Rigidbody2D       rb;
    CapsuleCollider2D body;

    State state;
    float timer;
    bool  facingRight = true;
    bool  contactLatched;

    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = contactSize;
        col.offset    = contactOffset;
        anim = new SpriteFlipbook(sr);

        EnemyPhysics.GroundFall(gameObject, bodySize, bodyOffset, out rb, out body);
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        facingRight = player != null ? player.transform.position.x >= transform.position.x : true;
        SetFacing(facingRight);
        EnterIdle();
    }

    void Update()
    {
        if (player != null)
        {
            facingRight = player.transform.position.x >= transform.position.x;
            SetFacing(facingRight);
        }

        float dt = Time.deltaTime;
        switch (state)
        {
            case State.Idle:    TickIdle(dt);    break;
            case State.Draw:    TickDraw(dt);    break;
            case State.Release: TickRelease(dt); break;
        }
        anim.Tick(dt);
    }

    // ── IDLE ──────────────────────────────────────────────────────────────────
    void EnterIdle()
    {
        state = State.Idle;
        timer = IdleDurationForDifficulty();
        anim.Play(idleFrames, idleFps, true, restart: true);
    }

    void TickIdle(float dt)
    {
        timer -= dt;
        if (timer <= 0f) EnterDraw();
    }

    // ── DRAW (telegraph) ──────────────────────────────────────────────────────
    void EnterDraw()
    {
        state = State.Draw;
        timer = drawDuration;
        anim.Play(drawFrames, drawFps, false, restart: true);
    }

    void TickDraw(float dt)
    {
        timer -= dt;
        if (timer <= 0f) EnterRelease();
    }

    // ── RELEASE ───────────────────────────────────────────────────────────────
    void EnterRelease()
    {
        state = State.Release;
        anim.Play(releaseFrames, releaseFps, false, restart: true);
        FireArrow();

        float clipLength = (releaseFrames != null && releaseFrames.Length > 0 && releaseFps > 0f)
            ? releaseFrames.Length / releaseFps
            : 0.2f;
        timer = clipLength;
    }

    void TickRelease(float dt)
    {
        timer -= dt;
        if (timer <= 0f) EnterIdle();
    }

    void FireArrow()
    {
        if (arrowPrefab == null) return;
        Vector2 dir = facingRight ? Vector2.right : Vector2.left;
        Vector3 spawnPos = transform.position + (Vector3)(dir * bowTipForward) + new Vector3(0f, bowTipHeight, 0f);
        Arrow arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        arrow.speed = arrowSpeed;
        arrow.Fire(dir);
    }

    // ── Contact — a head-stomp defeats it; any other touch hits Gareth ────────
    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null || contactLatched) return;
        contactLatched = true;

        if (pc.IsStompContact(col))
        {
            pc.BounceFromStomp();
            StompDeath.Apply(gameObject, this, col);
            return;
        }
        if (!pc.IsHurt) pc.TakeHit(transform.position);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null) contactLatched = false;
    }

    // ── Difficulty (no enum exists — inferred from MaxLives: 999/5/3/1) ───────
    float IdleDurationForDifficulty()
    {
        if (player == null) return idleDurationMedium;
        int max = player.MaxLives;
        if (max >= 999) return idleDurationEasy;
        if (max <= 3)   return idleDurationHard;
        return idleDurationMedium;
    }

    void SetFacing(bool faceRight) => sr.flipX = artFacesRight ? !faceRight : faceRight;

    void OnDrawGizmosSelected()
    {
        Vector3 dir = (facingRight ? Vector3.right : Vector3.left) * 9f;
        Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawLine(transform.position, transform.position + dir);
        Gizmos.DrawWireSphere(transform.position + dir, 0.1f);
    }
}
