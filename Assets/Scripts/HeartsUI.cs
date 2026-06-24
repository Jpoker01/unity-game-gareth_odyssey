using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas-based lives display. Attach this component to a UI Text GameObject.
/// GarethSetup step 6 creates and wires this automatically.
/// Shows filled red hearts for remaining lives, grey hearts for lost lives.
/// On Easy (unlimited lives) it shows a heart and an infinity symbol.
/// </summary>
[RequireComponent(typeof(Text))]
public class HeartsUI : MonoBehaviour
{
    [Tooltip("Assign the PlayerController here, or it is auto-found on Start.")]
    public PlayerController player;

    Text label;
    int  lastLives = -1;
    int  lastMax   = -1;

    void Awake() => label = GetComponent<Text>();

    void Start()
    {
        if (player == null)
            player = Object.FindFirstObjectByType<PlayerController>();
        Rebuild();
    }

    void Update()
    {
        if (player == null) return;
        if (player.CurrentLives != lastLives || player.MaxLives != lastMax)
            Rebuild();
    }

    void Rebuild()
    {
        if (label == null || player == null) return;
        label.supportRichText = true;
        lastLives = player.CurrentLives;
        lastMax   = player.MaxLives;

        if (lastMax >= 999)
        {
            label.text = "<color=#FF4444>♥</color> ∞";
            return;
        }

        int max = Mathf.Min(lastMax, 10); // cap visual at 10 so it fits the screen
        var sb = new StringBuilder();
        for (int i = 0; i < max; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(i < lastLives ? "<color=#FF4444>♥</color>" : "<color=#555555>♥</color>");
        }
        label.text = sb.ToString();
    }
}
