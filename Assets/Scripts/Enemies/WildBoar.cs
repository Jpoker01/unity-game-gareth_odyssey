using UnityEngine;

/// <summary>
/// Agrios Xoiros — Cretan Wild Boar. Graze / Alert / Charge / Recover state
/// machine (Level 2 Enemy Design — Enemy 3). Wanders its territory slowly,
/// freezes with a telegraph when Gareth gets close, then locks onto his
/// position and charges in a straight line until it hits a wall or ledge.
/// Stomped from above it's defeated outright; any other contact costs a life
/// via the shared TakeHit knockback system.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class WildBoar : MonoBehaviour
{
    enum State { Graze, Alert, Charge, Recover }

    [Header("Territory (per-instance — wanders this zone, centred on spawn)")]
    public float territoryWidth = 6f;
    public float grazeSpeed     = 0.6f;

    [Header("Detection — horizontal-only, same Y-band ±1 tile")]
    public float alertRadius    = 5f;
    public float detectionYBand = 1f;

    [Header("Alert Delay by Difficulty (telegraph window)")]
    public float alertDelayEasy   = 1.0f;
    public float alertDelayMedium = 0.8f;
    public float alertDelayHard   = 0.6f;    // Hard & Impossible

    [Header("Charge")]
    public float chargeSpeed       = 4.0f;
    public float wallCheckDistance = 0.6f;
    public float ledgeCheckDrop    = 1.0f;

    [Header("Recover")]
    public float recoverDuration = 1.2f;

    [Header("Contact Box")]
    public Vector2 contactSize   = new Vector2(1.2f, 0.85f);
    public Vector2 contactOffset = new Vector2(0f, -0.1f);

    [Header("Body — solid collider for ground physics (a safety net alongside its own ledge-detection AI)")]
    public Vector2 bodySize   = new Vector2(1.2f, 0.85f);
    public Vector2 bodyOffset = new Vector2(0f, -0.1f);

    [Header("Animation Frames (sliced by Level 2 Enemy Setup)")]
    public Sprite[] grazeFrames;
    public Sprite[] alertFrames;
    public Sprite[] chargeFrames;
    public Sprite[] recoverFrames;
    public float grazeFps   = 6f;
    public float alertFps   = 8f;
    public float chargeFps  = 14f;
    public float recoverFps = 6f;

    [Header("Sprite Orientation")]
    [Tooltip("The supplied boar artwork already faces LEFT — leave this on so flips stay correct.")]
    public bool artFacesLeft = true;

    [Header("Ground / Wall Detection")]
    public LayerMask groundLayer;

    SpriteRenderer    sr;
    BoxCollider2D     col;
    Rigidbody2D       rb;
    CapsuleCollider2D body;
    SpriteFlipbook    anim;
    PlayerController  player;

    State state = State.Graze;
    float originX;
    int   grazeDir = 1;
    bool  chargeFacingRight;
    float stateTimer;
    float chargeDistance;
    bool  contactLatched;

    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = contactSize;
        col.offset    = contactOffset;
        anim = new SpriteFlipbook(sr);

        if (groundLayer.value == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g != -1) groundLayer = 1 << g;
        }

        EnemyPhysics.GroundFall(gameObject, bodySize, bodyOffset, out rb, out body);
    }

    void Start()
    {
        player  = FindFirstObjectByType<PlayerController>();
        originX = transform.position.x;
        SetFacing(grazeDir > 0);
    }

    void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;

        switch (state)
        {
            case State.Graze:   TickGraze(dt);   break;
            case State.Alert:   TickAlert(dt);   break;
            case State.Charge:  TickCharge(dt);  break;
            case State.Recover: TickRecover(dt); break;
        }

        anim.Tick(dt);
    }

    // ── GRAZE ─────────────────────────────────────────────────────────────────
    void TickGraze(float dt)
    {
        anim.Play(grazeFrames, grazeFps, true);

        float halfWidth = territoryWidth * 0.5f;
        float left  = originX - halfWidth;
        float right = originX + halfWidth;

        float nextX = transform.position.x + grazeSpeed * dt * grazeDir;
        if      (grazeDir > 0 && nextX >= right) { nextX = right; grazeDir = -1; SetFacing(false); }
        else if (grazeDir < 0 && nextX <= left)  { nextX = left;  grazeDir =  1; SetFacing(true);  }

        transform.position = new Vector3(nextX, transform.position.y, transform.position.z);

        if (PlayerInAlertBand()) EnterAlert();
    }

    // ── ALERT (telegraph — direction locks here, not tracked during charge) ──
    void EnterAlert()
    {
        state             = State.Alert;
        stateTimer        = AlertDelayForDifficulty();
        chargeFacingRight = player.transform.position.x >= transform.position.x;
        SetFacing(chargeFacingRight);
        anim.Play(alertFrames, alertFps, false, restart: true);
    }

    void TickAlert(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f) EnterCharge();
    }

    // ── CHARGE ────────────────────────────────────────────────────────────────
    void EnterCharge()
    {
        state          = State.Charge;
        chargeDistance = 0f;
        SetFacing(chargeFacingRight);
        anim.Play(chargeFrames, chargeFps, true, restart: true);
    }

    void TickCharge(float dt)
    {
        anim.Play(chargeFrames, chargeFps, true);

        Vector2 dir  = chargeFacingRight ? Vector2.right : Vector2.left;
        float   step = chargeSpeed * dt;

        if (HitWallAhead(dir) || !GroundAhead(dir))
        {
            EnterRecover();
            return;
        }

        transform.position += (Vector3)(dir * step);
        chargeDistance     += step;

        // Safety net for the doc's CHARGE → DESPAWN branch — should never fire
        // in a level whose charge lanes are bounded by walls or ledges.
        if (chargeDistance > territoryWidth + 30f)
            ResetToTerritoryCentre();
    }

    bool HitWallAhead(Vector2 dir)
    {
        Vector2 origin = (Vector2)transform.position + col.offset + dir * (col.size.x * 0.5f);
        return Physics2D.Raycast(origin, dir, wallCheckDistance, groundLayer);
    }

    bool GroundAhead(Vector2 dir)
    {
        Vector2 feet   = (Vector2)transform.position + col.offset - new Vector2(0f, col.size.y * 0.5f);
        Vector2 origin = feet + dir * (col.size.x * 0.5f + 0.1f);
        return Physics2D.Raycast(origin, Vector2.down, ledgeCheckDrop, groundLayer);
    }

    void ResetToTerritoryCentre()
    {
        transform.position = new Vector3(originX, transform.position.y, transform.position.z);
        state    = State.Graze;
        grazeDir = 1;
        SetFacing(true);
    }

    // ── RECOVER (bounding box stays active — still costs a life to touch) ────
    void EnterRecover()
    {
        state      = State.Recover;
        stateTimer = recoverDuration;
        anim.Play(recoverFrames, recoverFps, false, restart: true);
    }

    void TickRecover(float dt)
    {
        stateTimer -= dt;
        if (stateTimer <= 0f)
        {
            state    = State.Graze;
            grazeDir = transform.position.x > originX ? -1 : 1; // wander back toward territory centre
            SetFacing(grazeDir > 0);
        }
    }

    // ── Detection ─────────────────────────────────────────────────────────────
    bool PlayerInAlertBand()
    {
        Vector3 p = player.transform.position;
        Vector3 m = transform.position;
        return Mathf.Abs(p.y - m.y) <= detectionYBand && Mathf.Abs(p.x - m.x) <= alertRadius;
    }

    float AlertDelayForDifficulty()
    {
        if (player == null) return alertDelayMedium;
        int max = player.MaxLives;
        if (max >= 999) return alertDelayEasy;
        if (max <= 3)   return alertDelayHard;
        return alertDelayMedium;
    }

    // ── Contact — active in every state incl. Recover; a head-stomp still defeats it ─
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

    // ── Facing — source art faces LEFT by default ────────────────────────────
    void SetFacing(bool faceRight) => sr.flipX = artFacesLeft ? faceRight : !faceRight;

    void OnDrawGizmosSelected()
    {
        float baseX     = Application.isPlaying ? originX : transform.position.x;
        float halfWidth = territoryWidth * 0.5f;

        Gizmos.color = new Color(0.55f, 0.35f, 0.1f, 0.8f);
        Vector3 a = new Vector3(baseX - halfWidth, transform.position.y, 0f);
        Vector3 b = new Vector3(baseX + halfWidth, transform.position.y, 0f);
        Gizmos.DrawLine(a, b);

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireCube(transform.position, new Vector3(alertRadius * 2f, detectionYBand * 2f, 0f));
    }
}
