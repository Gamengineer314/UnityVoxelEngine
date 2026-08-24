using UnityEngine;

/// <summary>
/// Follow an object with damping for the y axis
/// </summary>
public class DampedFollower : MonoBehaviour {
    public Transform target;
    public float smoothTime = 0.1f;
    private float yVelocity = 0;

    private void Update() {
        float y = Mathf.SmoothDamp(transform.position.y, target.position.y, ref yVelocity, smoothTime);
        transform.position = new Vector3(target.position.x, y, target.position.z);
    }
}