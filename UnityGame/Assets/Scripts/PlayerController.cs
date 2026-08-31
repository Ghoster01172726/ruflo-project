using UnityEngine;

// Простое передвижение от первого лица: WASD двигает тело, мышь крутит камеру
// (по горизонтали вращается сам игрок, по вертикали — только камера).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float gravity = -9.81f;

    private CharacterController controller;
    private float verticalLookRotation;
    private Vector3 verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalLookRotation -= mouseY;
        verticalLookRotation = Mathf.Clamp(verticalLookRotation, -80f, 80f);

        if (cameraTransform != null)
        {
            cameraTransform.localRotation = Quaternion.Euler(verticalLookRotation, 0f, 0f);
        }
    }

    private void HandleMove()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = (transform.right * x + transform.forward * z) * moveSpeed;

        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }
        verticalVelocity.y += gravity * Time.deltaTime;

        controller.Move((move + verticalVelocity) * Time.deltaTime);
    }
}
