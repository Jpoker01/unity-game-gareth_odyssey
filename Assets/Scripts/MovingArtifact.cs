using UnityEngine;

public class FloatingItem : MonoBehaviour
{
    [SerializeField] float amplitude = 0.25f;   // how far it moves up/down (world units)
    [SerializeField] float frequency = 1f;       // how fast it bobs (cycles per second)

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * amplitude;
        transform.position = startPos + new Vector3(0f, y, 0f);
    }
}