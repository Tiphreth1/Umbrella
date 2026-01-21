// CameraController.cs
// This script is responsible for the player's view.
// It rotates instantly and follows the physical position of the aircraft.

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("카메라 회전")]
    public float cameraRotationSpeed = 100f;
    public Camera playerCamera;

    // The aircraft's Rigidbody, which represents the physical body
    public Rigidbody aircraftRigidbody;

    // Input values
    private float pitchInput;
    private float rollInput;
    private float throttleInput;

    // Reference to the AircraftController for communication
    private AircraftController aircraftController;
    private VirtualCursorController virtualCursor;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        // Find the AircraftController and connect it
        aircraftController = FindObjectOfType<AircraftController>();
        if (aircraftController != null)
        {
            aircraftController.SetCameraController(this);
            // Get the Rigidbody from the AircraftController to follow it
            aircraftRigidbody = aircraftController.rb;
        }

        // Find the VirtualCursorController
        virtualCursor = FindObjectOfType<VirtualCursorController>();
        if (virtualCursor != null)
        {
            virtualCursor.SetCameraController(this);
        }
    }

    void Update()
    {
        // Rotate the camera instantly based on player input
        ApplyCameraRotation();

        // Make the camera follow the aircraft's physical position
        FollowAircraft();
    }

    void ApplyCameraRotation()
    {
        float pitchRotation = pitchInput * cameraRotationSpeed * Time.deltaTime;
        float rollRotation = -rollInput * cameraRotationSpeed * Time.deltaTime;

        transform.Rotate(Vector3.right, pitchRotation, Space.Self);
        transform.Rotate(Vector3.forward, rollRotation, Space.Self);
    }

    // Sync the camera's position with the aircraft's Rigidbody
    void FollowAircraft()
    {
        if (aircraftRigidbody != null)
        {
            transform.position = aircraftRigidbody.position;
        }
    }

    // Input System callbacks
    public void OnThrottle(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        throttleInput = Mathf.Clamp01(input.y);

        if (aircraftController != null)
        {
            aircraftController.SetThrottleInput(throttleInput);
        }
    }

    public void SetPitchAndRollInput(float pitch, float roll)
    {
        pitchInput = pitch;
        rollInput = roll;
    }
}
