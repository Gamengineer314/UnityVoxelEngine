using UnityEngine;
using Voxels.Physics;

/// <summary>
/// Read inputs and control the player, allowing it to walk or fly
/// </summary>
public class PlayerController : MonoBehaviour {
    public GameObject cameraObject;
    public float walkSpeed = 200;
    public float flySpeed = 200;
    public float sensitivity = 1.5f;
    public float jumpHeight = 4;

    private VoxelCharacterController controller;
    private float xRotation;
    private float yRotation;
    private float lastJumpTime = float.NegativeInfinity;
    private bool isFlying = false;

    private void Start() {
        controller = transform.parent.GetComponent<VoxelCharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update() {
        // Translation
        float speed = isFlying ? flySpeed : walkSpeed;
        Vector3 velocity = Vector3.zero;
        if (Input.GetKey(KeyCode.D)) velocity += transform.right * speed;
        if (Input.GetKey(KeyCode.A)) velocity -= transform.right * speed;
        if (Input.GetKey(KeyCode.W)) velocity += transform.forward * speed;
        if (Input.GetKey(KeyCode.S)) velocity -= transform.forward * speed;
        if (isFlying && Input.GetKey(KeyCode.Space)) velocity.y += speed;
        if (isFlying && Input.GetKey(KeyCode.LeftShift)) velocity.y -= speed;
        if (velocity != Vector3.zero) controller.VelocityMove(velocity);

        // Jump
        if (Input.GetKeyDown(KeyCode.Space)) {
            if (Time.time - lastJumpTime < 0.35f) {
                if (isFlying) isFlying = false;
                else {
                    isFlying = true;
                    controller.ResetGravity();
                }
            }
            lastJumpTime = Time.time;
        }
        if (!isFlying && controller.isGrounded && Input.GetKey(KeyCode.Space)) controller.Jump(jumpHeight);
        if (!isFlying) controller.GravityMove();

        // Rotation
        xRotation -= Input.GetAxis("Mouse Y") * sensitivity;
        yRotation += Input.GetAxis("Mouse X") * sensitivity;
        transform.rotation = Quaternion.AngleAxis(yRotation, Vector3.up);
        cameraObject.transform.rotation = transform.rotation * Quaternion.AngleAxis(xRotation, Vector3.right);
    }
}