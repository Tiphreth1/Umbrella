// AircraftController.cs
// 시각적 기체 - 비행기다운 기동 (큰 선회는 roll+pitch, 작은 조정은 yaw)
// 실제 물리는 FlightProxyController가 담당

using TMPro;
using UnityEngine;

public class AircraftController : MonoBehaviour
{
    [Header("선회 설정")]
    [Tooltip("커서 끝에서의 목표 뱅크 각도")]
    public float turnBankAngle = 60f;

    [Header("레드아웃 방지")]
    public float invertedThreshold = 0f;
    public float pullUpThreshold = 10f;
    [Tooltip("이 각도 이상 피치 다운 시 배면 전환")]
    public float pushDownThreshold = 20f;

    [Header("UI")]
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI speedText;

    // 참조
    private CameraController cameraController;
    private FlightProxyController flightProxy;
    private VirtualCursorController virtualCursor;

    // 상태
    private bool isInRedoutRecovery = false;
    private bool isInPushDownManeuver = false;  // 피치 다운 배면 기동 중
    private float pushDownGracePeriod = 0f;     // 배면 전환 후 레드아웃 복구 방지 시간
    private float currentRoll = 0f;  // 현재 롤 각도 추적

    void Start()
    {
        flightProxy = FindObjectOfType<FlightProxyController>();
        virtualCursor = FindObjectOfType<VirtualCursorController>();

        if (GetComponent<FlightProxyController>() != null)
        {
            Debug.LogError("[AircraftController] FlightProxyController와 같은 오브젝트에 있으면 안됩니다!");
        }
    }

    void Update()
    {
        if (flightProxy != null)
        {
            if (altitudeText != null)
                altitudeText.text = $"Altitude: {flightProxy.transform.position.y:F0} m";
            if (speedText != null)
                speedText.text = $"Speed: {flightProxy.rb.linearVelocity.magnitude:F1} m/s";
        }
    }

    void LateUpdate()
    {
        if (flightProxy != null)
        {
            transform.position = flightProxy.transform.position;
        }

        UpdateVisualRotation();
    }

    void UpdateVisualRotation()
    {
        if (virtualCursor == null) return;

        // === 테스트: 커서 = 목표 롤 각도 ===
        Vector2 cursor = virtualCursor.GetNormalizedInput();
        float cursorAngle = Mathf.Atan2(cursor.x, cursor.y) * Mathf.Rad2Deg;  // 12시=0, 3시=90

        // 현재 롤 (순수 로컬 Z축 기준)
        float currentRoll = transform.localEulerAngles.z;
        if (currentRoll > 180f) currentRoll -= 360f;  // -180~180 범위로

        // 목표 롤 = 커서 각도 * 거리 (커서가 중앙으로 갈수록 자연스럽게 0으로)
        float cursorDist = cursor.magnitude;
        float targetRoll = -cursorAngle * Mathf.Clamp01(cursorDist);

        // 롤 차이
        float rollError = targetRoll - currentRoll;
        if (rollError > 180f) rollError -= 360f;
        if (rollError < -180f) rollError += 360f;

        float rollSpeed = 90f;
        float rollDelta = Mathf.Clamp(rollError, -rollSpeed * Time.deltaTime, rollSpeed * Time.deltaTime);

        transform.Rotate(Vector3.forward, rollDelta, Space.Self);
    }

    public void SetCameraController(CameraController camera)
    {
        cameraController = camera;
    }

    // FlightProxy 상태 전달 (UI용)
    public bool IsAoAActive => flightProxy != null && flightProxy.IsAoAActive;
    public bool IsAoAOnCooldown => flightProxy != null && flightProxy.IsAoAOnCooldown;
    public float AoACooldownRemaining => flightProxy != null ? flightProxy.AoACooldownRemaining : 0f;
    public float CurrentAoA => flightProxy != null ? flightProxy.CurrentAoA : 0f;
}
