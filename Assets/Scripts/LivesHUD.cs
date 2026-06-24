using UnityEngine;

/// <summary>
/// Draws heart icons in the top-left corner of the screen.
/// Attach to the Player GameObject (GarethSetup step 3 does this automatically).
/// Requires no Canvas setup — uses OnGUI for prototype-quality display.
/// </summary>
public class LivesHUD : MonoBehaviour
{
    [Tooltip("Size of each heart icon in screen pixels.")]
    public int heartSize = 32;
    [Tooltip("Pixels from the top-left corner.")]
    public int margin = 16;

    private PlayerController player;

    // Heart shapes drawn with GL (no texture needed for prototype)
    // We use Unicode ♥ via GUI.Label as a quick stand-in.
    private GUIStyle heartStyle;

    void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    void OnGUI()
    {
        if (player == null) return;

        if (heartStyle == null)
        {
            heartStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = heartSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }

        int lives = player.CurrentLives;
        int max   = player.MaxLives;

        for (int i = 0; i < max; i++)
        {
            heartStyle.normal.textColor = i < lives ? Color.red : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            GUI.Label(new Rect(margin + i * (heartSize + 4), margin, heartSize, heartSize),
                      "♥", heartStyle);
        }
    }
}
