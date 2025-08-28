using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFreeLookSwitch_Realtime : MonoBehaviour
{
    public Transform[] cameraPositions;
    public float mouseSensitivity = 2f;
    public InputActionAsset inputActions;

    private int currentCamIndex = 0;
    private float xRotation = 0f;
    private float yRotation = 0f;
    private InputAction lookAction;
    private InputAction switchAction;

    void Awake()
    {
        var map = inputActions.FindActionMap("Camera");
        lookAction = map.FindAction("Look");
        switchAction = map.FindAction("SwitchCamera");
    }

    void OnEnable()
    {
        lookAction.Enable();
        switchAction.Enable();
    }
    void OnDisable()
    {
        lookAction.Disable();
        switchAction.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!(CameraOrbitManager.Instance?.IsOrbiting ?? false)) // Only allow camera control if NOT orbiting
        {
            if (switchAction.triggered)
            {
                currentCamIndex = (currentCamIndex + 1) % cameraPositions.Length;
                xRotation = 0f;
                yRotation = 0f;
            }

            Transform camPos = cameraPositions[currentCamIndex];
            transform.position = camPos.position;

            Vector2 lookDelta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
            yRotation += lookDelta.x;
            xRotation -= lookDelta.y;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            Quaternion baseRot = camPos.rotation;
            Quaternion lookRot = Quaternion.Euler(xRotation, yRotation, 0);
            transform.rotation = baseRot * lookRot;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

}
