using UnityEngine;

/// <summary>
/// Sagittarius arrow projectile (Level 2 Enemy Design — Enemy 2).
/// Travels in a straight line shaped by a light gravity arc ("gravity * 0.3"),
/// visually rotating to track its own velocity, and despawns on hitting the
/// ground, leaving the camera, or after a lifetime safety timer. On contact
/// with Gareth it deals damage through the same PlayerController.TakeHit
/// knockback used by every other hazard.
///
/// The doc calls for an in-house 4×2 px arrow sprite — none of the supplied
/// art includes one, so a tiny earth-tone arrow texture is generated in code
/// and cached for reuse across every spawned instance.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Arrow : MonoBehaviour
{
    public float    speed        = 5f;     // tiles/sec
    public float    gravityScale = 0.3f;   // doc: "gravity * 0.3"
    public float    lifetime     = 6f;     // safety despawn if it never hits anything
    public LayerMask groundLayer;

    Rigidbody2D    rb;
    SpriteRenderer sr;

    static Sprite cachedSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        rb.gravityScale   = gravityScale;
        rb.freezeRotation = true;

        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        if (sr.sprite == null) sr.sprite = GetOrCreateSprite();
        col.size = sr.sprite.bounds.size; // match the trigger to the actual sprite, not Unity's default 1x1

        if (groundLayer.value == 0)
        {
            int g = LayerMask.NameToLayer("Ground");
            if (g != -1) groundLayer = 1 << g;
        }
    }

    /// <summary>Launch the arrow toward `direction` (normalised internally).</summary>
    public void Fire(Vector2 direction)
    {
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        rb.linearVelocity = dir * speed;
        transform.right   = dir;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Doc: "the arrow travels until it hits a solid tile or exits the camera boundary."
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        if (vp.x < -0.15f || vp.x > 1.15f || vp.y < -0.3f || vp.y > 1.3f)
            Destroy(gameObject);
    }

    void FixedUpdate()
    {
        // Nose the arrow along its current (gravity-curved) velocity, like real fletching.
        Vector2 v = rb.linearVelocity;
        if (v.sqrMagnitude > 0.0001f) transform.right = v;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            if (!pc.IsHurt) pc.TakeHit(transform.position);
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & groundLayer.value) != 0)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale   = 0f;
            enabled           = false;
            Destroy(gameObject, 0.4f);
        }
    }

    // ── Procedural sprite (doc: "Create in-house: 4×2 px arrow, earth-tone palette") ──
    static Sprite GetOrCreateSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        const int w = 8, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px  = new Color32[w * h];
        Color32 shaft = new Color32(150, 111, 67, 255);
        Color32 head  = new Color32(98, 96, 92, 255);
        for (int x = 0; x < w; x++)
        {
            if (x < w - 2) px[1 * w + x] = shaft;
            else           { px[1 * w + x] = head; px[2 * w + x] = head; }
        }
        tex.SetPixels32(px);
        tex.Apply();

        cachedSprite      = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.2f, 0.4f), 32f);
        cachedSprite.name = "Arrow_Generated";
        return cachedSprite;
    }
}
