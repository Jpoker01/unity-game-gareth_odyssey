using UnityEngine;

/// <summary>
/// Gives a ground-walking enemy real gravity and a solid body to rest on, so
/// any AI edge case that lets it wander past a ledge ends the same way it would
/// for Gareth — a fall — instead of the creature hanging in mid-air. Each enemy
/// keeps its existing trigger collider for player contact exactly as it is;
/// this adds a second, differently-typed solid collider purely for ground/wall
/// collision, so the two can never be confused by a GetComponent lookup.
/// Self-healing — adds whatever the prefab doesn't already have — so it works
/// whether or not the asset was authored with physics in mind.
/// </summary>
public static class EnemyPhysics
{
    public static void GroundFall(GameObject enemy, Vector2 bodySize, Vector2 bodyOffset,
                                  out Rigidbody2D rb, out CapsuleCollider2D body)
    {
        rb = enemy.GetComponent<Rigidbody2D>();
        if (rb == null) rb = enemy.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        body = enemy.GetComponent<CapsuleCollider2D>();
        if (body == null) body = enemy.AddComponent<CapsuleCollider2D>();
        body.isTrigger = false;
        body.size      = bodySize;
        body.offset    = bodyOffset;
    }
}
