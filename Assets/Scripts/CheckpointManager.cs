using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public Vector3 LastCheckpointPosition { get; private set; }
    public bool    HasCheckpoint          { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RegisterCheckpoint(Vector3 position)
    {
        LastCheckpointPosition = position;
        HasCheckpoint = true;
    }

    /// <summary>
    /// Returns the position the player should respawn at after hitting a death zone.
    /// Uses the last activated checkpoint if one exists; otherwise scans the scene
    /// for the nearest Checkpoint whose X is at or behind the player's X.
    /// </summary>
    public Vector3 GetRespawnPosition(Vector3 playerPosition)
    {
        if (HasCheckpoint) return LastCheckpointPosition;

        // Fallback: find the rightmost checkpoint that is still at or behind the player.
        var allCheckpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        Checkpoint best = null;
        float bestX = float.NegativeInfinity;

        foreach (var cp in allCheckpoints)
        {
            float cpX = cp.transform.position.x;
            if (cpX <= playerPosition.x && cpX > bestX)
            {
                best  = cp;
                bestX = cpX;
            }
        }

        return best != null ? best.transform.position : playerPosition;
    }
}
