using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Jump  (variable height — release Jump early for a short hop, hold it for the full arc)")]
    [Tooltip("How high a fully-held jump peaks, in tiles. This grid is 1 tile = 1 world unit, so 3 here means 3 tiles exactly — the launch speed is derived from this height plus gravity (v0 = sqrt(2gh)), not hand-tuned.")]
    public float maxJumpHeightTiles = 3f;
    [Tooltip("Releasing Jump while Gareth is still rising multiplies his remaining upward speed by this. Lower = a stubbier hop on a quick tap; 1 = no cut at all, so even a tap reaches full height.")]
    [Range(0.1f, 1f)] public float jumpCutMultiplier = 0.5f;

    [Header("Ground Detection")]
    public float     groundCheckDistance = 0.08f;
    public LayerMask groundLayer;

    // Character body = 22 of the 32 px cell (10 px empty at top), pivot at cell centre.
    // At 16 PPU: height = 22/16 = 1.375 u, centre offset = (−1.0 + 0.375)/2 = −0.3125 u
    [Header("Collider")]
    public Vector2 colliderSize   = new Vector2(0.6f,  1.375f);
    public Vector2 colliderOffset = new Vector2(0f,   -0.3125f);

    [Header("Lives  (GDD §2.7.6)")]
    public int maxLives = 3;

    [Header("Hurt")]
    public float hurtKnockbackX       = 5f;
    public float hurtKnockbackY       = 4f;
    public float invincibilityDuration = 1.5f;

    [Header("Stomp Kill  (land on an enemy from above to defeat it)")]
    [Tooltip("How high Gareth bounces after stomping an enemy, in tiles — derived into a launch speed the same physically-exact way the jump is, so it stays consistent if gravity ever changes.")]
    public float stompBounceHeightTiles = 1.5f;

    [Header("Events")]
    public UnityEvent onInteract;
    public UnityEvent onToolUse;
    public UnityEvent onLifeLost;
    public UnityEvent onDeath;

    // ── Public state ──────────────────────────────────────────────────────────
    public int   CurrentLives    { get; private set; }
    public bool  IsGrounded      { get; private set; }
    public bool  IsHurt          { get; private set; }
    public bool  IsFacingRight   { get; private set; } = true;
    public int   MaxLives        => maxLives;
    public float VelocityY       => rb.linearVelocity.y;
    public float HorizontalInput => horizontal;

    // ── Private ───────────────────────────────────────────────────────────────
    Rigidbody2D    rb;
    SpriteRenderer sr;
    Collider2D     col;
    Animator       anim; // optional — disabled by PlayerSpriteAnimation if present

    float horizontal;
    bool  jumpQueued;
    bool  jumpCutQueued;
    float jumpVelocity;         // launch speed for a full 3-tile jump — derived in Awake (v0 = sqrt(2gh))
    float stompBounceVelocity;  // launch speed for the post-stomp bounce — derived the same way

    static readonly int ParamSpeed      = Animator.StringToHash("Speed");
    static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
    static readonly int ParamVelocityY  = Animator.StringToHash("VelocityY");
    static readonly int ParamHurt       = Animator.StringToHash("Hurt");
    static readonly int ParamUseItem    = Animator.StringToHash("UseItem");

    // ── Unity messages ────────────────────────────────────────────────────────
    void Awake()
    {
        rb   = GetComponent<Rigidbody2D>();
        sr   = GetComponent<SpriteRenderer>();
        col  = GetComponent<Collider2D>();
        anim = GetComponent<Animator>(); // null if no Animator; fine

        // Box or Capsule — whichever this body actually has — both expose size/offset
        // individually (Collider2D itself doesn't define them), so configure whichever
        // concrete type is present to match the visible character body.
        if      (col is BoxCollider2D     box) { box.size = colliderSize; box.offset = colliderOffset; }
        else if (col is CapsuleCollider2D cap) { cap.size = colliderSize; cap.offset = colliderOffset; }

        CurrentLives = maxLives;

        // Derive launch speeds from the heights designers actually think in (tiles) plus
        // this body's own real gravity, so "max height" is a physically-exact guarantee
        // instead of a hand-tuned velocity that merely happens to look about right.
        jumpVelocity        = SpeedToReach(maxJumpHeightTiles);
        stompBounceVelocity = SpeedToReach(stompBounceHeightTiles);
    }

    /// <summary>
    /// The exact initial upward speed a body must launch at — under ITS OWN gravity
    /// (Physics2D.gravity.y * rb.gravityScale) — to decelerate to a standstill exactly
    /// `heightInTiles` world units higher: the standard projectile-apex relation
    /// v0 = sqrt(2 * g * h). Lets every "how high" tunable here be authored the way a
    /// designer thinks (in tiles) while staying physically exact at any gravity setting.
    /// </summary>
    float SpeedToReach(float heightInTiles)
    {
        float g = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
        return Mathf.Sqrt(2f * g * Mathf.Max(0.01f, heightInTiles));
    }

    void Update()
    {
        if (!IsHurt) ReadInput();
        SyncAnimator();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        if (!IsHurt) ApplyMovement();
    }

    // ── Input ─────────────────────────────────────────────────────────────────
    void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        horizontal = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  horizontal = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal =  1f;

        if      (horizontal >  0f && !IsFacingRight) SetFacing(true);
        else if (horizontal <  0f &&  IsFacingRight) SetFacing(false);

        bool jumpPressed  = kb.spaceKey.wasPressedThisFrame
                         || kb.wKey.wasPressedThisFrame
                         || kb.upArrowKey.wasPressedThisFrame;
        bool jumpReleased = kb.spaceKey.wasReleasedThisFrame
                         || kb.wKey.wasReleasedThisFrame
                         || kb.upArrowKey.wasReleasedThisFrame;

        // "How hard you press" on digital buttons reads as "how long you hold" — tap
        // for a short hop, hold through the rise for the full 3-tile arc. See ApplyMovement.
        if (jumpPressed  && IsGrounded) jumpQueued    = true;
        if (jumpReleased)               jumpCutQueued = true;

        var  mouse = Mouse.current;
        bool lmb   = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (kb.eKey.wasPressedThisFrame || lmb)
            onInteract?.Invoke();

        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            if (anim != null && anim.enabled) anim.SetTrigger(ParamUseItem);
            onToolUse?.Invoke();
        }
    }

    // ── Physics ───────────────────────────────────────────────────────────────
    void ApplyMovement()
    {
        float velY = rb.linearVelocity.y;

        if (jumpQueued)
        {
            // Every jump launches at the SAME full speed — a 3-tile arc waiting to happen.
            // Variable height comes entirely from whether that rise gets cut short below,
            // not from how hard the launch itself begins (there's no way to "charge" a jump
            // on a digital button without adding input lag, so this is the responsive way).
            velY = jumpVelocity;
            jumpQueued    = false;
            jumpCutQueued = false;
        }
        else if (jumpCutQueued)
        {
            // Released mid-rise: chop the remaining upward speed once, so gravity pulls
            // Gareth over into the fall early — the shorter the press, the lower the peak.
            // sqrt(2gh) means halving the speed quarters the height, so even a tap still
            // gives a small, deliberate hop rather than a flat stop. Releasing after the
            // apex (or while grounded) just finds velY <= 0 here and changes nothing.
            if (velY > 0f) velY *= jumpCutMultiplier;
            jumpCutQueued = false;
        }

        rb.linearVelocity = new Vector2(horizontal * moveSpeed, velY);
    }

    void CheckGrounded()
    {
        Bounds  bounds = col.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f);
        Vector2 size   = new Vector2(bounds.size.x * 0.9f, groundCheckDistance);
        IsGrounded = Physics2D.OverlapBox(origin, size, 0f, groundLayer);
    }

    void SyncAnimator()
    {
        if (anim == null || !anim.enabled) return;
        anim.SetFloat(ParamSpeed,      IsHurt ? 0f : Mathf.Abs(horizontal));
        anim.SetBool (ParamIsGrounded, IsGrounded);
        anim.SetFloat(ParamVelocityY,  rb.linearVelocity.y);
    }

    void SetFacing(bool right)
    {
        IsFacingRight = right;
        sr.flipX = !right;
    }

    // ── Damage system ─────────────────────────────────────────────────────────
    public void TakeHit(Vector2 sourcePosition)
    {
        if (IsHurt) return;

        CurrentLives--;
        onLifeLost?.Invoke();

        float dirX = transform.position.x > sourcePosition.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dirX * hurtKnockbackX, hurtKnockbackY);

        if (anim != null && anim.enabled) anim.SetTrigger(ParamHurt);

        if (CurrentLives <= 0) { HandleDeath(); return; }
        StartCoroutine(InvincibilityRoutine());
    }

    IEnumerator InvincibilityRoutine()
    {
        IsHurt = true;
        float elapsed = 0f;
        while (elapsed < invincibilityDuration)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.12f);
            elapsed += 0.12f;
        }
        sr.enabled = true;
        IsHurt = false;
    }

    void HandleDeath()
    {
        IsHurt = true;
        rb.linearVelocity = Vector2.zero;
        if (anim != null && anim.enabled) anim.SetTrigger(ParamHurt);
        onDeath?.Invoke();
        enabled = false;
    }

    // ── Stomp kill  (landing on an enemy from above defeats it) ──────────────
    // Every enemy routes player contact through its own OnTriggerEnter2D, and each
    // one calls IsStompContact on itself before ever calling TakeHit. Because both
    // sides ask this exact same question — independently, with no shared state —
    // a single touch can never both cost Gareth a life AND spare the enemy, or
    // defeat the enemy AND still hurt him: whichever object's trigger fires first,
    // the other reaches the identical verdict from the identical geometry.
    /// <summary>
    /// True when this contact reads as Gareth landing on top of `otherCollider`:
    /// he isn't rising, and his lowest point sits above the target's vertical
    /// centre — i.e. he's coming down "through" its head, not bumping its side
    /// or clipping its underside on the way up.
    /// </summary>
    public bool IsStompContact(Collider2D otherCollider)
    {
        if (otherCollider == null)      return false;
        if (rb.linearVelocity.y > 0.1f) return false; // rising — a hit from below, never a stomp

        return col.bounds.min.y > otherCollider.bounds.center.y;
    }

    /// <summary>Pops Gareth upward off a freshly-defeated enemy so chained stomps read cleanly.</summary>
    public void BounceFromStomp()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stompBounceVelocity);
    }

    // ── Hazard / Death-zone detection ─────────────────────────────────────────
    // "Hazard"   → spike tiles etc.: lose a life + knockback (TakeHit).
    // "DeathZone"→ pits / kill floors: lose a life + teleport to last checkpoint.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hazard"))
            TakeHit(other.transform.position);
        else if (other.CompareTag("DeathZone"))
            TakeDeathZoneHit();
    }

    void TakeDeathZoneHit()
    {
        if (IsHurt) return;

        CurrentLives--;
        onLifeLost?.Invoke();

        if (CurrentLives <= 0) { HandleDeath(); return; }

        Vector3 respawnPos = transform.position; // safe default
        var cm = CheckpointManager.Instance;
        if (cm != null) respawnPos = cm.GetRespawnPosition(transform.position);

        StartCoroutine(DeathZoneRespawnRoutine(respawnPos));
    }

    IEnumerator DeathZoneRespawnRoutine(Vector3 respawnPos)
    {
        IsHurt = true;
        rb.linearVelocity = Vector2.zero;
        if (anim != null && anim.enabled) anim.SetTrigger(ParamHurt);

        // Brief pause so the player can register the hit before snapping away.
        yield return new WaitForSeconds(0.35f);

        transform.position = respawnPos;
        rb.linearVelocity  = Vector2.zero;

        // Invincibility blink to prevent double-hits on arrival.
        float elapsed = 0f;
        while (elapsed < invincibilityDuration)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.12f);
            elapsed += 0.12f;
        }
        sr.enabled = true;
        IsHurt = false;
    }

    // ── Difficulty helper ─────────────────────────────────────────────────────
    public void SetLives(int lives) { maxLives = lives; CurrentLives = lives; }

    public void Respawn(Vector3 position, int lives)
    {
        maxLives             = lives;
        CurrentLives         = lives;
        IsHurt               = false;
        enabled              = true;
        transform.position   = position;
        rb.linearVelocity    = Vector2.zero;
        sr.enabled           = true;
        if (anim != null && anim.enabled) anim.ResetTrigger(ParamHurt);
    }

    // ── Debug gizmo ───────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (col == null) col = GetComponent<Collider2D>();
        if (col == null) return;
        Bounds  bounds = col.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f);
        Vector2 size   = new Vector2(bounds.size.x * 0.9f, groundCheckDistance);
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(origin, size);
    }
}
