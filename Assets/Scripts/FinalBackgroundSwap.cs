using UnityEngine;
using UnityEngine.UI;

public class FinalBackgroundSwap : MonoBehaviour
{
    public RawImage background;
    public Texture heavenImage;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            background.texture = heavenImage;
    }
}