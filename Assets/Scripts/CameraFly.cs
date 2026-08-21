using UnityEngine;
using Voxels.Physics;

public class CameraFly : MonoBehaviour {
    private const float xSensitivity = 1.5f;
    private const float ySensitivity = 1.5f;
    public float speed = 200;
    public GameObject testSphere;
    public GameObject testCube;

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

        /*if (VoxelPhysics.Instance.Raycast(new Ray(transform.position, transform.forward), float.PositiveInfinity, -1, out VoxelRaycastHit hit)) {
            testSphere.SetActive(true);
            testSphere.transform.position = transform.position + hit.movement;
        }
        else {
            testSphere.SetActive(false);
        }*/

        if (VoxelPhysics.Instance.MoveBox(new Box(-1, 1) + transform.position, transform.forward, float.PositiveInfinity, -1, out VoxelRaycastHit hit)) {
            testCube.SetActive(true);
            testCube.transform.position = transform.position + hit.movement;
        }
        else {
            testCube.SetActive(false);
        }
    }
}
