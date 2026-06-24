using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;
    public Vector2 offset = new Vector2(0f, 1.5f);

    Vector3 vel;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref vel, smoothTime);
    }
}
