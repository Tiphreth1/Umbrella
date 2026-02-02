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

    // Input values
    private float pitchInput;
    private float rollInput;
    private float throttleInput;

    // AOA 입력 폴링용
    private InputAction aoaAction;

    // Reference to controllers
    private AircraftController aircraftController; // 시각적 기체
    private FlightProxyController flightProxy;     // 실제 물리/조작
    private VirtualCursorController virtualCursor;

    // 실속 상태
    private float currentStallIntensity = 0f;

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

        // FlightProxy 찾기
        flightProxy = FindObjectOfType<FlightProxyController>();

        // GameManager가 없으면 기존 방식으로 자동 검색 (하위 호환)
        if (GameManager.Instance == null)
        {
            var aircraft = FindObjectOfType<AircraftController>();
            if (aircraft != null)
            {
                SetTarget(aircraft);
            }
        }

        // FlightProxy 방향으로 카메라 초기화
        if (flightProxy != null)
        {
            transform.rotation = flightProxy.transform.rotation;
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

            // FlightProxy 방향으로 초기화
            if (flightProxy != null)
            {
                transform.rotation = flightProxy.transform.rotation;
            }

            // 커서 중앙으로 리셋
            if (virtualCursor != null)
            {
                virtualCursor.ResetCursorToCenter();
            }

            Debug.Log($"[CameraController] Target set: {aircraft.name}");
        }
    }

    public AircraftController CurrentTarget => aircraftController;

    void Update()
    {
        // AOA 입력 폴링 (매 프레임 확인)
        PollAOAInput();

        // Rotate the camera instantly based on player input
        ApplyCameraRotation();

        // 실속 효과 적용
        ApplyStallEffect();

        // Make the camera follow the aircraft's physical position
        FollowAircraft();
    }

    void PollAOAInput()
    {
        if (aoaAction == null || flightProxy == null) return;

        bool isPressed = aoaAction.IsPressed();
        flightProxy.SetAOAInput(isPressed);
    }

    void ApplyCameraRotation()
    {
        // 마우스 입력이 없으면 카메라 방향 유지 (수평 복귀 안 함)
        // 커서만 중앙으로 복귀 (VirtualCursorController에서 처리)
        // 기체가 현재 카메라 방향으로 기수 맞춤
    }

    // 마우스 delta 기반 회전: 움직인 만큼만 회전
    // 마우스 X = Yaw (좌우 회전), 마우스 Y = Pitch (상하)
    // 카메라는 roll 없음 - yaw는 월드 기준
    public void ApplyMouseDelta(float pitchDelta, float yawDelta)
    {
        float currentSpeed = cameraRotationSpeed;
        if (flightProxy != null && flightProxy.IsAoAActive)
        {
            currentSpeed *= aoaRotationMultiplier;
        }

        float pitchRotation = pitchDelta * currentSpeed;
        float yawRotation = yawDelta * currentSpeed;

        // Pitch: 로컬 X축 기준 (카메라가 바라보는 방향에서 상하)
        transform.Rotate(Vector3.right, pitchRotation, Space.Self);
        // Yaw: 월드 Y축 기준 (roll 발생 방지)
        transform.Rotate(Vector3.up, yawRotation, Space.World);
    }

    // 카메라 위치: FlightProxy 위치를 따라감
    void FollowAircraft()
    {
        if (flightProxy != null)
        {
            transform.position = flightProxy.transform.position;
        }
    }

    // Input System callbacks
    public void OnThrottle(InputValue value)
    {
        Vector2 input = value.Get<Vector2>();
        throttleInput = Mathf.Clamp01(input.y);

        Debug.Log($"[Camera] OnThrottle received: {input}, throttle: {throttleInput:F2}");

        // FlightProxy에 스로틀 전달
        if (flightProxy != null)
        {
            flightProxy.SetThrottleInput(throttleInput);
        }
        else
        {
            Debug.LogWarning("[Camera] FlightProxy is NULL - throttle not sent!");
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

    // 실속 강도 설정 (AircraftController에서 호출)
    public void SetStallIntensity(float intensity)
    {
        currentStallIntensity = intensity;
    }

    // 실속 시 카메라 강제 하향 (roll 없음)
    void ApplyStallEffect()
    {
        // FlightProxy에서 실속 강도 가져오기
        float stallIntensity = flightProxy != null ? flightProxy.StallIntensity : currentStallIntensity;

        if (stallIntensity <= 0f) return;

        // 현재 카메라가 얼마나 아래를 보는지 확인
        // forward.y가 -1에 가까우면 완전히 아래를 봄
        float lookingDown = -transform.forward.y; // 0 = 수평, 1 = 완전 아래

        // 이미 충분히 아래를 보고 있으면 더 이상 회전 안 함
        if (lookingDown > 0.9f) return;

        // 실속 시 카메라가 급격하게 아래를 보도록 강제
        float stallPitchSpeed = 120f * stallIntensity * (1f - lookingDown);  // 아래 볼수록 느려짐
        transform.Rotate(Vector3.right, stallPitchSpeed * Time.deltaTime, Space.Self);

        // 실속 시 카메라 흔들림 (피치만, roll 없음)
        if (stallIntensity > 0.2f)
        {
            float shakePitch = Mathf.Sin(Time.time * 8f) * 3f * stallIntensity;
            transform.Rotate(Vector3.right, shakePitch * Time.deltaTime * 5f, Space.Self);
        }
    }
}
