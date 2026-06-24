using UnityEngine;
using UnityEngine.UI;

public class CaveBackgroundSwap : MonoBehaviour
{
    public RawImage background;
    public Texture caveImage;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            background.texture = caveImage;
    }
}