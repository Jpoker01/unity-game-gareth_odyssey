using UnityEngine;

/// <summary>
/// Legionarius — Roman Sentry. Patrol / Alert / Contact state machine
/// (Level 2 Enemy Design — Enemy 1). Marches a fixed waypoint path, halts and
/// faces Gareth when he enters its detection band, and otherwise behaves as a
/// stationary blocking obstacle. A stomp from above defeats it outright; any
/// other touch costs a life via the same PlayerController.TakeHit knockback
/// used everywhere else in the game.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class RomanSentry : MonoBehaviour
{
    enum State { Patrol, Alert }

    [Header("Patrol (per-instance — typically 4-8 tiles wide, centred on spawn)")]
    public float patrolWidth   = 6f;
    public float walkSpeed     = 1.2f;
    public float waypointPause = 0.8f;

    [Header("Detection — constant across difficulties")]
    public float detectionRadius = 4f;
    public float detectionYBand  = 1f;
    public float alertLingerTime = 1.5f;

    [Header("Contact Box (≈1 tile, centred on the sentry)")]
    public Vector2 contactSize   = new Vector2(1f, 1f);
    public Vector2 contactOffset = Vector2.zero;

    [Header("Body — solid collider for ground physics (falls if it ever walks off a ledge)")]
    public Vector2 bodySize   = new Vector2(1f, 1f);
    public Vector2 bodyOffset = Vector2.zero;

    [Header("Animation Frames (sliced by Level 2 Enemy Setup)")]
    public Sprite[] walkFrames;
    public Sprite[] idleRestFrames;
    public Sprite[] idleAlertFrames;
    public float walkFps  = 6f;
    public float idleFps  = 4f;
    public float alertFps = 6f;

    [Header("Sprite Orientation")]
    [Tooltip("Tick if the source artwork already faces right by default.")]
    public bool artFacesRight = true;

    SpriteRenderer    sr;
    BoxCollider2D     col;
    Rigidbody2D       rb;
    CapsuleCollider2D body;
    SpriteFlipbook    anim;
    PlayerController  player;
    TextMesh          alertMark;

    State state = State.Patrol;
    float originX;
    int   dir = 1;            // patrol travel direction: +1 toward +X
    float pauseTimer;
    float lingerTimer;
    float alertMarkTimer;
    bool  contactLatched;     // true while overlapping the player — prevents repeat-frame TakeHit calls

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
        player  = FindFirstObjectByType<PlayerController>();
        originX = transform.position.x;
        SetFacing(true);
        BuildAlertMark();
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        bool playerInBand = PlayerInDetectionBand(detectionRadius);

        switch (state)
        {
            case State.Patrol: TickPatrol(dt, playerInBand); break;
            case State.Alert:  TickAlert(dt, playerInBand);  break;
        }

        if (alertMarkTimer > 0f)
        {
            alertMarkTimer -= dt;
            if (alertMarkTimer <= 0f && alertMark != null) alertMark.gameObject.SetActive(false);
        }

        anim.Tick(dt);
    }

    // ── PATROL ────────────────────────────────────────────────────────────────
    void TickPatrol(float dt, bool playerDetected)
    {
        if (playerDetected) { EnterAlert(); return; }

        float halfWidth = patrolWidth * 0.5f;
        float left  = originX - halfWidth;
        float right = originX + halfWidth;

        if (pauseTimer > 0f)
        {
            anim.Play(idleRestFrames, idleFps, true);
            pauseTimer -= dt;
            if (pauseTimer <= 0f) dir = -dir;
            return;
        }

        SetFacing(dir > 0);
        anim.Play(walkFrames, walkFps, true);

        float nextX = transform.position.x + walkSpeed * dt * dir;
        bool reachedRight = dir > 0 && nextX >= right;
        bool reachedLeft  = dir < 0 && nextX <= left;

        if (reachedRight || reachedLeft)
        {
            nextX = reachedRight ? right : left;
            pauseTimer = waypointPause;
        }
        transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
    }

    // ── ALERT ─────────────────────────────────────────────────────────────────
    void EnterAlert()
    {
        state       = State.Alert;
        lingerTimer = alertLingerTime;
        SetFacing(player.transform.position.x >= transform.position.x);
        anim.Play(idleAlertFrames, alertFps, true, restart: true);
        ShowAlertMark();
    }

    void TickAlert(float dt, bool playerDetected)
    {
        SetFacing(player.transform.position.x >= transform.position.x);
        anim.Play(idleAlertFrames, alertFps, true);

        if (playerDetected) { lingerTimer = alertLingerTime; return; }

        lingerTimer -= dt;
        if (lingerTimer <= 0f)
        {
            state      = State.Patrol;
            pauseTimer = 0f;
        }
    }

    // ── Detection ─────────────────────────────────────────────────────────────
    bool PlayerInDetectionBand(float radius)
    {
        Vector3 p = player.transform.position;
        Vector3 m = transform.position;
        return Mathf.Abs(p.y - m.y) <= detectionYBand && Mathf.Abs(p.x - m.x) <= radius;
    }

    // ── Contact (CONTACT) — a head-stomp defeats it; any other touch hits Gareth ─
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
        if (other.GetComponent<PlayerController>() != null)
            contactLatched = false;
    }

    // ── Facing & telegraph ────────────────────────────────────────────────────
    void SetFacing(bool faceRight) => sr.flipX = artFacesRight ? !faceRight : faceRight;

    void ShowAlertMark()
    {
        if (alertMark == null) return;
        alertMark.gameObject.SetActive(true);
        alertMarkTimer = 0.5f;
    }

    void BuildAlertMark()
    {
        var go = new GameObject("AlertMark");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.text          = "!";
        tm.characterSize = 0.15f;
        tm.fontSize      = 48;
        tm.anchor        = TextAnchor.LowerCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = new Color(0.85f, 0.1f, 0.1f);
        tm.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 10;

        alertMark = tm;
        go.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        float baseX     = Application.isPlaying ? originX : transform.position.x;
        float halfWidth = patrolWidth * 0.5f;

        Gizmos.color = new Color(1f, 0.65f, 0f, 0.8f);
        Vector3 a = new Vector3(baseX - halfWidth, transform.position.y, 0f);
        Vector3 b = new Vector3(baseX + halfWidth, transform.position.y, 0f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(a, 0.12f);
        Gizmos.DrawWireSphere(b, 0.12f);

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawWireCube(transform.position, new Vector3(detectionRadius * 2f, detectionYBand * 2f, 0f));
    }
}
