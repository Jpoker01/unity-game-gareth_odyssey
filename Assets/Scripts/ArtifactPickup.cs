using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Place this on the target artefact GameObject at the end of the level.
/// When the player gets close and presses E (or LMB), it notifies LevelManager
/// and triggers the level-complete popup.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ArtifactPickup : MonoBehaviour
{
    [Header("Interaction")]
    public float pickupRadius = 2f;

    [Header("Prompt UI")]
    [Tooltip("Assign the UI Text used for the 'Press E' prompt. Left null = no prompt shown.")]
    public Text promptText;

    bool      playerNearby;
    Transform playerTr;

    void Start()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc) playerTr = pc.transform;

        // Make sure collider is a trigger so the player can pass through
        GetComponent<Collider2D>().isTrigger = true;

        if (promptText) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (playerTr == null) return;

        bool inRange = Vector2.Distance(transform.position, playerTr.position) <= pickupRadius;
        if (inRange != playerNearby)
        {
            playerNearby = inRange;
            if (promptText) promptText.gameObject.SetActive(inRange);
        }

        if (!inRange) return;

        // Check if level is in a state that allows pickup
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentState != LevelManager.State.Playing)
            return;

        var kb    = Keyboard.current;
        var mouse = Mouse.current;
        bool interact = (kb    != null && kb.eKey.wasPressedThisFrame)
                     || (mouse != null && mouse.leftButton.wasPressedThisFrame);

        if (interact)
        {
            if (promptText) promptText.gameObject.SetActive(false);
            if (LevelManager.Instance != null) LevelManager.Instance.NotifyArtifactCollected();
            enabled = false; // prevent double-trigger
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
