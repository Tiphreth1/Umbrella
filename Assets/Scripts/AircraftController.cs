// AircraftController.cs
// 시각적 기체 전용 - FlightProxy를 따라가며 선회 롤 추가
// 실제 물리/조작은 FlightProxyController가 담당

using TMPro;
using UnityEngine;

public class AircraftController : MonoBehaviour
{
    [Header("조종 속도 (시각적)")]
    public float rotationSpeed = 180f;  // 초당 회전 각도

    [Header("선회 롤")]
    [Range(0f, 2f)]
    public float turnRollMultiplier = 1.5f;  // yaw 차이 → 롤 변환 비율
    public float maxTurnRoll = 60f;          // 최대 롤 각도

    [Header("UI")]
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI speedText;

    // 참조
    private CameraController cameraController;
    private FlightProxyController flightProxy;

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
        // 위치는 FlightProxy 따라감
        if (flightProxy != null)
        {
            transform.position = flightProxy.transform.position;
        }

        UpdateVisualRotation();
    }

    void UpdateVisualRotation()
    {
        if (flightProxy == null) return;

        // FlightProxy의 회전을 따라감
        Vector3 proxyForward = flightProxy.transform.forward;
        Vector3 proxyUp = flightProxy.transform.up;

        // === 선회 롤 계산 (카메라 방향과의 yaw 차이로 롤 추가) ===
        float yawDiff = 0f;
        if (cameraController != null)
        {
            Vector3 camForward = cameraController.transform.forward;
            Vector3 flatForward = new Vector3(proxyForward.x, 0, proxyForward.z);
            Vector3 flatCamForward = new Vector3(camForward.x, 0, camForward.z);

            if (flatForward.sqrMagnitude > 0.001f && flatCamForward.sqrMagnitude > 0.001f)
            {
                yawDiff = Vector3.SignedAngle(flatForward.normalized, flatCamForward.normalized, Vector3.up);
            }
        }

        // 선회 방향으로 롤 적용
        float targetRoll = Mathf.Clamp(yawDiff * turnRollMultiplier, -maxTurnRoll, maxTurnRoll);
        Vector3 targetUp = Quaternion.AngleAxis(-targetRoll, proxyForward) * proxyUp;

        // === 목표 회전: FlightProxy forward + 선회 롤 ===
        Quaternion targetRotation = Quaternion.LookRotation(proxyForward, targetUp);

        // === 부드러운 보간 ===
        float maxDelta = rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDelta);
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
