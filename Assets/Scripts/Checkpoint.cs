using System.Collections;
using UnityEngine;
 
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    public bool IsActivated { get; private set; }
 
    [Header("Respawn")]
    [Tooltip("Where the player respawns, relative to this checkpoint.")]
    public Vector2 respawnOffset = new Vector2(0f, 0.5f);

    public Vector3 RespawnPosition => transform.position + (Vector3)respawnOffset;
    
    [Header("Sprites")]
    [Tooltip("Lit / active texture. Use the SAME Pixels-Per-Unit and pivot as the inactive sprite so it lines up.")]
    public Sprite onSprite;
 
    [Header("Transition")]
    [Tooltip("Seconds to cross-fade from inactive to active. 0 = instant swap.")]
    public float crossfadeDuration = 0.4f;
 
    [Header("References (auto-found if left empty)")]
    [Tooltip("The renderer showing the inactive obelisk.")]
    public SpriteRenderer statueRenderer;
 
    SpriteRenderer _overlay;
    Coroutine _routine;
 
    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
    }
 
    void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
 
        if (statueRenderer == null)
            statueRenderer = GetComponentInChildren<SpriteRenderer>();
 
        if (statueRenderer != null && onSprite != null)
        {
            var go = new GameObject("ActiveOverlay");
 
            // Parent to the same object as statueRenderer, not to statueRenderer itself,
            // so we don't inherit its local offset twice and cause a position shift.
            Transform overlayParent = statueRenderer.transform.parent != null
                ? statueRenderer.transform.parent
                : statueRenderer.transform;
            go.transform.SetParent(overlayParent, true);

            go.transform.position = statueRenderer.transform.position;
            go.transform.rotation = statueRenderer.transform.rotation;
            go.transform.localScale = statueRenderer.transform.lossyScale;
 
            _overlay = go.AddComponent<SpriteRenderer>();
            _overlay.sprite         = onSprite;
            _overlay.sortingLayerID = statueRenderer.sortingLayerID;
            _overlay.sortingOrder   = statueRenderer.sortingOrder + 1;
            _overlay.color          = new Color(1f, 1f, 1f, 0f);
        }
    }
 
    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsActivated) return;
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        Activate();
    }
 
    public void Activate()
    {
        if (IsActivated) return;
        IsActivated = true;
 
        if (CheckpointManager.Instance != null)
            CheckpointManager.Instance.RegisterCheckpoint(RespawnPosition);
 
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(CrossfadeRoutine());
    }
 
    IEnumerator CrossfadeRoutine()
    {
        if (_overlay == null)
        {
            if (statueRenderer != null && onSprite != null) statueRenderer.sprite = onSprite;
            yield break;
        }
 
        float t = 0f;
        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float k = crossfadeDuration <= 0f ? 1f : Mathf.Clamp01(t / crossfadeDuration);
            k = k * k * (3f - 2f * k);
            SetOverlayAlpha(k);
            yield return null;
        }
        SetOverlayAlpha(1f);
 
        // Bake result onto base renderer and drop the overlay.
        if (statueRenderer != null) statueRenderer.sprite = onSprite;
        _overlay.enabled = false;
    }
 
    void SetOverlayAlpha(float a)
    {
        var c = _overlay.color;
        c.a = a;
        _overlay.color = c;
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size   = col.size;
            Gizmos.color = IsActivated ? new Color(0f, 1f, 0.4f, 0.45f)
                : new Color(1f, 0.85f, 0f, 0.30f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = IsActivated ? new Color(0f, 1f, 0.4f, 1f)
                : new Color(1f, 0.85f, 0f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }

        // respawn marker
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + (Vector3)respawnOffset, 0.15f);
    }
}
