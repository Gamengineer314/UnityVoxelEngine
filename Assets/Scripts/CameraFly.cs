using UnityEngine;

public class CameraFly : MonoBehaviour {
    private const float xSensitivity = 1.5f;
    private const float ySensitivity = 1.5f;
    public float speed = 200;

    private float xRotation;
    private float yRotation;


    private void Start() {
        Cursor.lockState = CursorLockMode.Locked;
    }


    private void Update() {
        // Translation
        float xMove = 0;
        float zMove = 0;
        if (Input.GetKey(KeyCode.D)) xMove += speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) xMove -= speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.W)) zMove += speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) zMove -= speed * Time.deltaTime;
        transform.Translate(new Vector3(xMove, 0, zMove));

        // Rotation
        xRotation -= Input.GetAxis("Mouse Y") * ySensitivity;
        yRotation += Input.GetAxis("Mouse X") * xSensitivity;
        transform.rotation = Quaternion.AngleAxis(yRotation, Vector3.up) * Quaternion.AngleAxis(xRotation, Vector3.right);
    }
}
