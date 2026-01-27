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

    [Header("카메라 위치 (3인칭)")]
    [Tooltip("기체 뒤쪽 거리")]
    public float cameraDistance = 20f;
    [Tooltip("기체 위쪽 높이")]
    public float cameraHeight = 5f;

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

        // VirtualCursorController 연결
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

        // GameManager가 없으면 기존 방식으로 자동 검색 (하위 호환)
        if (GameManager.Instance == null)
        {
            var aircraft = FindObjectOfType<AircraftController>();
            if (aircraft != null)
            {
                SetTarget(aircraft);
            }
        }
    }

    // 타겟 기체 설정 (GameManager에서 호출)
    public void SetTarget(AircraftController aircraft)
    {
        // 이전 기체 연결 해제
        if (aircraftController != null)
        {
            aircraftController.SetCameraController(null);
        }

        aircraftController = aircraft;

        if (aircraft != null)
        {
            aircraft.SetCameraController(this);
            aircraftRigidbody = aircraft.rb;

            // 카메라 회전을 기체 방향으로 초기화
            transform.rotation = aircraft.transform.rotation;

            // 커서 중앙으로 리셋
            if (virtualCursor != null)
            {
                virtualCursor.ResetCursorToCenter();
            }

            Debug.Log($"[CameraController] Target set: {aircraft.name}");
        }
        else
        {
            aircraftRigidbody = null;
        }
    }

    public AircraftController CurrentTarget => aircraftController;

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
        // 마우스 입력이 없으면 카메라 방향 유지 (수평 복귀 안 함)
        // 커서만 중앙으로 복귀 (VirtualCursorController에서 처리)
        // 기체가 현재 카메라 방향으로 기수 맞춤
    }

    // 마우스 delta 기반 회전: 움직인 만큼만 회전
    // 마우스 X = Yaw (좌우 회전), 마우스 Y = Pitch (상하)
    public void ApplyMouseDelta(float pitchDelta, float yawDelta)
    {
        float currentSpeed = cameraRotationSpeed;
        if (aircraftController != null && aircraftController.IsAoAActive)
        {
            currentSpeed *= aoaRotationMultiplier;
        }

        // delta를 직접 회전에 적용 (마우스 멈추면 회전도 멈춤)
        float pitchRotation = pitchDelta * currentSpeed;
        float yawRotation = yawDelta * currentSpeed;

        transform.Rotate(Vector3.right, pitchRotation, Space.Self);
        transform.Rotate(Vector3.up, yawRotation, Space.Self);

        // 마우스 입력이 없을 때만 자동 수평 복귀 (롤만, 피치/요는 유지)
        bool hasActiveInput = Mathf.Abs(pitchDelta) > 0.001f || Mathf.Abs(yawDelta) > 0.001f;
        if (!hasActiveInput)
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
    }

    // 3인칭: 카메라가 기체 뒤쪽에서 따라감
    void FollowAircraft()
    {
        if (aircraftRigidbody != null)
        {
            // 카메라 자체의 backward + up 방향으로 오프셋
            // 카메라가 위를 보면 → 기체 아래로 이동 → 기체 하부 노출
            Vector3 offset = -transform.forward * cameraDistance + transform.up * cameraHeight;
            transform.position = aircraftRigidbody.position + offset;
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
