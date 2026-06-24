using UnityEngine;

/// <summary>
/// Aquila — Roman War Eagle (optional Enemy 5). Offscreen / Swoop state machine
/// (Level 2 Enemy Design — Enemy 5). Inert until an EagleTriggerZone activates
/// it, then flies a fixed, pre-baked quadratic-Bézier arc from right to left at
/// a constant 3.5 tiles/sec — always the same path, fully learnable. A DKC-style
/// shadow appears first and the actual damage hitbox only arms `shadowLeadTime`
/// seconds later, giving the player a brief "dodge window" before contact costs
/// a life through the same PlayerController.TakeHit knockback used everywhere
/// — though a perfectly-timed stomp from above brings it down instead.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class WarEagle : MonoBehaviour
{
    enum State { Offscreen, Swoop }

    [Header("Pre-baked Path — place 3 empty Transforms (right → left arc)")]
    [Tooltip("Spawn point — just past the camera's right edge, ~5-6 tiles above the danger zone.")]
    public Transform pathEntry;
    [Tooltip("Bézier control point — pulls the arc's middle down to shape the dive (gently descend then ascend).")]
    public Transform pathPeak;
    [Tooltip("Despawn point — just past the camera's left edge.")]
    public Transform pathExit;

    [Header("Flight")]
    public float speed = 3.5f;   // doc: "faster than Gareth's run — impossible to outrun directly"

    [Header("Shadow Telegraph (the Donkey Kong Country falling-enemy trick)")]
    [Tooltip("Seconds between the shadow appearing and the eagle's hitbox arming — the player's dodge window.")]
    public float shadowLeadTime = 0.5f;
    [Tooltip("How far below the flight path the ground sits here — the shadow rides beneath the eagle at this fixed local offset.")]
    public float shadowGroundOffset = 4f;
    public Sprite  shadowSprite;        // optional override; generated in-house if left empty
    public Vector2 shadowSize = new Vector2(1.1f, 0.45f);

    [Header("Animation Frames (sliced by Level 2 Enemy Setup — fly_loop, 4 frames)")]
    public Sprite[] flyFrames;
    public float    flyFps = 8f;

    [Header("Contact Box")]
    public Vector2 contactSize = new Vector2(0.9f, 0.7f);

    [Header("Sprite Orientation")]
    [Tooltip("Tick if the source artwork already faces right by default.")]
    public bool artFacesRight = true;

    SpriteRenderer sr;
    BoxCollider2D  col;
    SpriteFlipbook anim;
    SpriteRenderer shadowRenderer;

    State state = State.Offscreen;
    float t;
    float pathLength;
    bool  contactLatched;

    static Sprite cachedShadowSprite;

    /// <summary>True while mid-swoop — lets a trigger zone avoid double-activating.</summary>
    public bool IsActive => state == State.Swoop;

    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = contactSize;
        anim = new SpriteFlipbook(sr);

        BuildShadow();
        EnterOffscreen();
    }

    void Update()
    {
        if (state != State.Swoop) return;
        float dt = Time.deltaTime;
        TickSwoop(dt);
        anim.Tick(dt);
    }

    // ── OFFSCREEN — fully inert until a trigger zone calls Activate() ────────
    void EnterOffscreen()
    {
        CancelInvoke(nameof(ArmHitbox));
        state          = State.Offscreen;
        sr.enabled     = false;
        col.enabled    = false;
        contactLatched = false;
        if (shadowRenderer != null) shadowRenderer.gameObject.SetActive(false);
    }

    /// <summary>Called by an EagleTriggerZone the instant Gareth crosses it.</summary>
    public void Activate()
    {
        if (state == State.Swoop) return;
        if (pathEntry == null || pathPeak == null || pathExit == null)
        {
            Debug.LogWarning($"{name}: assign pathEntry / pathPeak / pathExit before activating.", this);
            return;
        }

        state      = State.Swoop;
        t          = 0f;
        pathLength = ApproxBezierLength(pathEntry.position, pathPeak.position, pathExit.position, 16);

        transform.position = pathEntry.position;
        SetFacing(pathExit.position.x >= pathEntry.position.x);

        sr.enabled     = true;
        col.enabled    = false;   // arms only once the dodge window elapses — see ArmHitbox
        contactLatched = false;
        anim.Play(flyFrames, flyFps, true, restart: true);

        if (shadowRenderer != null) shadowRenderer.gameObject.SetActive(true);
        Invoke(nameof(ArmHitbox), shadowLeadTime);
    }

    void ArmHitbox()
    {
        if (state == State.Swoop) col.enabled = true;
    }

    // ── SWOOP — constant-speed travel along the fixed Bézier arc ─────────────
    void TickSwoop(float dt)
    {
        anim.Play(flyFrames, flyFps, true);

        float step = pathLength > 0.01f ? (speed * dt) / pathLength : 1f;
        t += step;

        if (t >= 1f) { EnterOffscreen(); return; }

        transform.position = QuadraticBezier(pathEntry.position, pathPeak.position, pathExit.position, t);
    }

    static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float u)
    {
        float v = 1f - u;
        return v * v * p0 + 2f * v * u * p1 + u * u * p2;
    }

    static float ApproxBezierLength(Vector3 p0, Vector3 p1, Vector3 p2, int segments)
    {
        float total = 0f;
        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 cur = QuadraticBezier(p0, p1, p2, (float)i / segments);
            total += Vector3.Distance(prev, cur);
            prev = cur;
        }
        return total;
    }

    // ── Contact — armed only after the dodge window elapses; stompable mid-swoop too ─
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

    void SetFacing(bool faceRight) => sr.flipX = artFacesRight ? !faceRight : faceRight;

    void BuildShadow()
    {
        var go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -shadowGroundOffset, 0f);
        go.transform.localScale    = new Vector3(shadowSize.x, shadowSize.y, 1f);

        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite       = shadowSprite != null ? shadowSprite : GetOrCreateShadowSprite();
        shadowRenderer.sortingOrder = sr.sortingOrder - 1;
        go.SetActive(false);
    }

    // ── Procedural shadow (doc lists `shadow` as a 1-frame asset to author) ──
    static Sprite GetOrCreateShadowSprite()
    {
        if (cachedShadowSprite != null) return cachedShadowSprite;

        const int w = 32, h = 14;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px  = new Color32[w * h];
        Vector2 centre = new Vector2((w - 1) * 0.5f, (h - 1) * 0.5f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x - centre.x) / centre.x;
            float ny = (y - centre.y) / centre.y;
            float d  = Mathf.Sqrt(nx * nx + ny * ny);
            float a  = Mathf.Clamp01(1f - d) * 0.5f;
            px[y * w + x] = new Color(0f, 0f, 0f, a);
        }
        tex.SetPixels32(px);
        tex.Apply();

        cachedShadowSprite      = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        cachedShadowSprite.name = "EagleShadow_Generated";
        return cachedShadowSprite;
    }

    void OnDrawGizmosSelected()
    {
        if (pathEntry == null || pathPeak == null || pathExit == null) return;

        Gizmos.color = new Color(0.95f, 0.7f, 0.1f, 0.85f);
        Vector3 prev = pathEntry.position;
        const int seg = 24;
        for (int i = 1; i <= seg; i++)
        {
            Vector3 cur = QuadraticBezier(pathEntry.position, pathPeak.position, pathExit.position, (float)i / seg);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawWireSphere(pathEntry.position, 0.15f);
        Gizmos.DrawWireSphere(pathPeak.position,  0.15f);
        Gizmos.DrawWireSphere(pathExit.position,  0.15f);
    }
}
