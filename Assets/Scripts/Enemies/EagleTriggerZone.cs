using UnityEngine;

/// <summary>
/// Invisible activation collider for an Aquila swoop (Level 2 Enemy Design —
/// Enemy 5): "an invisible collider embedded at a specific point in the level"
/// that spawns the eagle once Gareth crosses it. Defaults to firing once, since
/// the doc asks that eagles "stay rare — no more than 2-3 encounters total."
/// Place several zone+eagle pairs to get more swoops across a level section.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EagleTriggerZone : MonoBehaviour
{
    [Tooltip("The eagle this zone activates when Gareth crosses it.")]
    public WarEagle eagle;

    [Tooltip("Untick to let this zone re-fire (e.g. on a backtrack-friendly section). Defaults on, matching the doc's 'keep eagles rare' guidance.")]
    public bool singleUse = true;

    bool consumed;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (eagle == null || eagle.IsActive) return;
        if (other.GetComponent<PlayerController>() == null) return;

        eagle.Activate();
        if (singleUse) consumed = true;
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Vector2 centre = (Vector2)transform.position + col.offset;
        Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.35f);
        Gizmos.DrawCube(centre, col.size);
        Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(centre, col.size);
    }
}
