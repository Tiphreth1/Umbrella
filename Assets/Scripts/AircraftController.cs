// AircraftController.cs
// 시각적 기체 - 비행기다운 기동 (큰 선회는 roll+pitch, 작은 조정은 yaw)
// 실제 물리는 FlightProxyController가 담당

using TMPro;
using UnityEngine;

public class AircraftController : MonoBehaviour
{
    [Header("선회 설정")]
    [Tooltip("이 각도 이하는 yaw로 조정")]
    public float yawOnlyThreshold = 15f;
    [Tooltip("선회 시 목표 롤 각도")]
    public float turnBankAngle = 60f;
    [Tooltip("롤이 이 비율 이상 완료되면 pitch 시작")]
    public float pitchStartRollRatio = 0.5f;

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

    // 상태
    private bool isInRedoutRecovery = false;
    private bool isInPushDownManeuver = false;  // 피치 다운 배면 기동 중
    private float pushDownGracePeriod = 0f;     // 배면 전환 후 레드아웃 복구 방지 시간
    private float currentRoll = 0f;  // 현재 롤 각도 추적

    void Start()
    {
        flightProxy = FindObjectOfType<FlightProxyController>();

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
        if (cameraController == null || flightProxy == null) return;

        Vector3 camForward = cameraController.transform.forward;

        // === FlightProxy 스펙 ===
        float pitchSpeed = flightProxy.pitchSpeed;
        float rollSpeed = flightProxy.rollSpeed;
        float yawSpeed = flightProxy.yawSpeed;

        // === 현재 상태 ===
        float upDot = Vector3.Dot(transform.up, Vector3.up);
        bool isInverted = upDot < invertedThreshold;

        // 피치 차이 (카메라가 위를 보면 양수)
        float pitchDiff = Vector3.SignedAngle(transform.forward, camForward, transform.right);
        bool isPullingUp = pitchDiff > pullUpThreshold;

        // yaw 차이
        Vector3 flatForward = new Vector3(transform.forward.x, 0, transform.forward.z);
        Vector3 flatCamForward = new Vector3(camForward.x, 0, camForward.z);
        float yawDiff = 0f;
        if (flatForward.sqrMagnitude > 0.001f && flatCamForward.sqrMagnitude > 0.001f)
        {
            yawDiff = Vector3.SignedAngle(flatForward.normalized, flatCamForward.normalized, Vector3.up);
        }

        float absYawDiff = Mathf.Abs(yawDiff);

        // === 레드아웃 방지 ===
        // Grace period 감소
        if (pushDownGracePeriod > 0f)
        {
            pushDownGracePeriod -= Time.deltaTime;
        }

        // 1. 정상 비행에서 강한 피치 다운 시 배면 전환
        bool isPushingDown = pitchDiff < -pushDownThreshold;
        if (!isInverted && isPushingDown && !isInRedoutRecovery)
        {
            isInPushDownManeuver = true;
        }
        else if (isInverted && isInPushDownManeuver)
        {
            // 배면 도달 시 기동 종료 + grace period 설정
            isInPushDownManeuver = false;
            pushDownGracePeriod = 1.5f;  // 1.5초간 레드아웃 복구 방지
        }
        else if (!isInverted && pitchDiff > -pushDownThreshold * 0.5f)
        {
            isInPushDownManeuver = false;
        }

        // 2. 배면 상태에서 당기기 (기존) - grace period 중엔 방지
        if (isInverted && isPullingUp && pushDownGracePeriod <= 0f)
        {
            isInRedoutRecovery = true;
        }
        else if (!isInverted)
        {
            isInRedoutRecovery = false;
        }

        // === 회전 계산 ===
        float targetPitchDelta = 0f;
        float targetYawDelta = 0f;
        float targetRollDelta = 0f;

        float dt = Time.deltaTime;

        if (isInPushDownManeuver)
        {
            // === 피치 다운 배면 기동: 롤로 배면 전환 후 당기기 ===
            // 가까운 방향으로 180도 롤 (yawDiff 방향으로)
            float targetInvertedRoll = Mathf.Sign(yawDiff != 0 ? yawDiff : 1f) * 180f;
            float rollToInverted = targetInvertedRoll - currentRoll;

            // 180도 넘는 회전 방지
            if (rollToInverted > 180f) rollToInverted -= 360f;
            if (rollToInverted < -180f) rollToInverted += 360f;

            targetRollDelta = Mathf.Clamp(rollToInverted, -rollSpeed * dt, rollSpeed * dt);
            // 롤 중에는 피치 최소화
            targetPitchDelta = Mathf.Clamp(pitchDiff * 0.2f, -pitchSpeed * dt * 0.2f, pitchSpeed * dt * 0.2f);
        }
        else if (isInRedoutRecovery)
        {
            // === 레드아웃 복구: 롤로 정상 자세 복구 ===
            float rollToLevel = -currentRoll;
            targetRollDelta = Mathf.Clamp(rollToLevel, -rollSpeed * dt, rollSpeed * dt);
            // 피치는 약하게
            targetPitchDelta = Mathf.Clamp(pitchDiff * 0.3f, -pitchSpeed * dt * 0.3f, pitchSpeed * dt * 0.3f);
        }
        else if (absYawDiff > yawOnlyThreshold)
        {
            // === 큰 선회: roll로 기울인 후 pitch ===
            float targetBankAngle = Mathf.Sign(yawDiff) * turnBankAngle;
            float rollError = targetBankAngle - currentRoll;

            // 롤 적용
            targetRollDelta = Mathf.Clamp(rollError, -rollSpeed * dt, rollSpeed * dt);

            // 롤이 충분히 됐으면 pitch 적용
            float rollRatio = Mathf.Abs(currentRoll) / turnBankAngle;
            if (rollRatio >= pitchStartRollRatio)
            {
                // 기울어진 상태에서 pitch로 당김 (실제로는 카메라 방향으로)
                targetPitchDelta = Mathf.Clamp(pitchDiff, -pitchSpeed * dt, pitchSpeed * dt);
            }

            // yaw는 최소한만
            targetYawDelta = Mathf.Clamp(yawDiff * 0.1f, -yawSpeed * dt * 0.5f, yawSpeed * dt * 0.5f);
        }
        else
        {
            // === 작은 조정: yaw + pitch, 롤은 수평 복귀 ===
            targetPitchDelta = Mathf.Clamp(pitchDiff, -pitchSpeed * dt, pitchSpeed * dt);
            targetYawDelta = Mathf.Clamp(yawDiff, -yawSpeed * dt, yawSpeed * dt);

            // 롤 수평 복귀
            float rollToLevel = -currentRoll * 0.5f;
            targetRollDelta = Mathf.Clamp(rollToLevel, -rollSpeed * dt, rollSpeed * dt);
        }

        // === 회전 적용 ===
        transform.Rotate(Vector3.right, targetPitchDelta, Space.Self);
        transform.Rotate(Vector3.up, targetYawDelta, Space.Self);
        transform.Rotate(Vector3.forward, targetRollDelta, Space.Self);

        // 현재 롤 추적
        currentRoll += targetRollDelta;
        currentRoll = Mathf.Clamp(currentRoll, -180f, 180f);
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
