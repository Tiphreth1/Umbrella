// CameraController.cs
// This script is responsible for the player's view.
// It rotates instantly and follows the physical position of the aircraft.

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("카메라 회전")]
    public float cameraRotationSpeed = 100f;
    public float aoaRotationMultiplier = 2f; // AOA 발동 시 카메라 감도 배율
    public Camera playerCamera;

    // The aircraft's Rigidbody, which represents the physical body
    public Rigidbody aircraftRigidbody;

    // Input values
    private float pitchInput;
    private float rollInput;
    private float throttleInput;

    // AOA 입력 폴링용
    private InputAction aoaAction;

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

        // AOA 액션 참조 가져오기 (폴링 방식)
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            aoaAction = playerInput.actions.FindAction("AOA");
        }
    }

    void Update()
    {
        // AOA 입력 폴링 (매 프레임 확인)
        PollAOAInput();

        // Rotate the camera instantly based on player input
        ApplyCameraRotation();

        // Make the camera follow the aircraft's physical position
        FollowAircraft();
    }

    void PollAOAInput()
    {
        if (aoaAction == null || aircraftController == null) return;

        bool isPressed = aoaAction.IsPressed();
        aircraftController.SetAOAInput(isPressed);
    }

    void ApplyCameraRotation()
    {
        // AOA 발동 중이면 카메라 감도 증가 (더 빠른 회전 명령 가능)
        float currentSpeed = cameraRotationSpeed;
        if (aircraftController != null && aircraftController.IsAoAActive)
        {
            currentSpeed *= aoaRotationMultiplier;
        }

        float pitchRotation = pitchInput * currentSpeed * Time.deltaTime;
        float rollRotation = -rollInput * currentSpeed * Time.deltaTime;

        transform.Rotate(Vector3.right, pitchRotation, Space.Self);
        transform.Rotate(Vector3.forward, rollRotation, Space.Self);

        // 마우스를 움직이지 않을 때 자동 수평 복귀
        bool hasMouseMovement = virtualCursor != null && virtualCursor.HasActiveMouseInput();
        if (!hasMouseMovement)
        {
            ApplyAutoLevel();
        }
    }

    void ApplyAutoLevel()
    {
        if (aircraftController == null) return;

        Vector3 currentForward = transform.forward;

        // forward가 거의 수직이면 수평 복귀 불가 (gimbal lock 방지)
        if (Mathf.Abs(Vector3.Dot(currentForward, Vector3.up)) > 0.99f)
            return;

        // 목표 up: forward에 수직이면서 월드 up에 최대한 가까운 벡터
        Vector3 targetUp = Vector3.up - Vector3.Dot(Vector3.up, currentForward) * currentForward;
        targetUp.Normalize();

        // 현재 up과 목표 up 사이의 각도 (수평까지 남은 각도)
        float angleToLevel = Vector3.SignedAngle(transform.up, targetUp, currentForward);

        // 기체의 rollSpeed에 맞춰 최대 회전 속도 제한
        float maxRotation = aircraftController.rollSpeed * Time.deltaTime;
        float actualRotation = Mathf.Clamp(angleToLevel, -maxRotation, maxRotation);

        // forward 축을 기준으로 회전 (roll만 변경)
        transform.Rotate(Vector3.forward, actualRotation, Space.Self);

        // 커서도 부드럽게 중앙으로 이동 시작
        if (virtualCursor != null)
        {
            virtualCursor.StartAutoLevelCentering();
        }
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

    // OnAOA 콜백은 더 이상 사용하지 않음 (PollAOAInput에서 매 프레임 처리)
    public void OnAOA(InputValue value)
    {
        // 폴링 방식으로 대체됨
    }

    public void SetPitchAndRollInput(float pitch, float roll)
    {
        pitchInput = pitch;
        rollInput = roll;
    }

    // 현재 롤 입력값 (외부에서 확인용)
    public float RollInput => rollInput;
}
