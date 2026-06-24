using UnityEngine;

/// <summary>
/// Generic "squash and despawn" feedback for any enemy felled by a Gareth
/// head-stomp (see PlayerController.IsStompContact). Applied on the spot by the
/// enemy's own contact handler the instant a stomp is recognised: silences the
/// enemy immediately — its state-machine script and contact collider both switch
/// off in the very same call, so a dying enemy can never land a hit on its way
/// out — then plays one short shared squash before removing it. Keeping the
/// death feel in a single place means every enemy type goes out the same way
/// without copy-pasting an animation into five separate state machines.
/// </summary>
public class StompDeath : MonoBehaviour
{
    const float Duration = 0.18f;

    float   t;
    Vector3 baseScale;

    /// <summary>
    /// Silences `brain` (the enemy's own behaviour script) and `hitbox` (its
    /// contact collider) immediately, then attaches the squash-and-remove
    /// flourish. Safe to call more than once per enemy — guards against
    /// double-application if more than one collider reports the same stomp
    /// in the same frame.
    /// </summary>
    public static void Apply(GameObject enemy, Behaviour brain, Collider2D hitbox)
    {
        if (enemy.GetComponent<StompDeath>() != null) return;

        if (brain  != null) brain.enabled  = false;
        if (hitbox != null) hitbox.enabled = false;

        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        enemy.AddComponent<StompDeath>();
    }

    void Awake() => baseScale = transform.localScale;

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / Duration);

        // Flattened and slightly widened — the classic "squashed underfoot" silhouette.
        transform.localScale = new Vector3(baseScale.x * (1f + k * 0.6f), baseScale.y * (1f - k), baseScale.z);

        if (k >= 1f) Destroy(gameObject);
    }
}
