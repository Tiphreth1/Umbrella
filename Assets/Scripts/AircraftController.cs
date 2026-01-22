// AircraftController.cs
// This script is the single source of truth for the aircraft's physical movement and rotation.
// All physics-based movement and rotation are calculated and applied here.

using TMPro;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AircraftController : MonoBehaviour
{
    [Header("비행기 물리")]
    public Rigidbody rb;
    public float enginePower = 100f;
    public float drag = 0.01f; // 전방 항력 (낮을수록 관성 유지)
    public float lateralDrag = 0.3f; // 측면 항력 (높을수록 기체 방향으로 정렬)
    public float liftCoefficient = 2f; // 양력 계수
    public float minLiftSpeed = 30f; // 양력이 발생하는 최소 속도
    public float stallSpeed = 50f; // 실속 속도 (이 속도 이하에서 양력 급감)

    [Header("조종 속도")]
    public float pitchSpeed = 30f;
    public float yawSpeed = 20f;
    public float rollSpeed = 50f;

    [Header("안정성 설정")]
    [Range(0f, 10f)]
    public float rotationP = 5f; // 회전 비례 제어 (높을수록 빠르게 반응)
    [Range(0f, 5f)]
    public float rotationD = 2.5f; // 회전 미분 제어 (높을수록 오버슈트 감소, 덜렁거림 방지)

    [Header("UI")]
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI speedText;

    [Header("받음각(AoA) 설정")]
    [Tooltip("최대 받음각 (도) - 제한기 ON 시")]
    public float maxAoAWithLimiter = 15f; // 제한기 ON 시 최대 받음각
    [Tooltip("최대 받음각 (도) - 제한기 OFF 시")]
    public float maxAoAWithoutLimiter = 45f; // 제한기 OFF 시 최대 받음각
    [Tooltip("받음각 제한 강도")]
    public float aoaLimiterStrength = 30f;
    [Tooltip("AOA 해제 후 쿨타임 (초)")]
    public float aoaCooldown = 5f;

    // A reference to the CameraController to get the target direction
    private CameraController cameraController;
    // The current input values for throttle, pitch, and roll
    private float throttleInput;
    private float pitchInput;
    private float rollInput;

    // AOA 제한기 상태
    private bool aoaInputPressed = false; // 현재 키 입력 상태
    private bool aoaInputPressedLastFrame = false; // 이전 프레임 입력 상태 (에지 검출용)
    private bool aoaOnCooldown = false; // 쿨타임 중인지
    private float aoaCooldownTimer = 0f; // 쿨타임 타이머

    // 현재 받음각 (디버깅용)
    private float currentAoA = 0f;

    // AOA 제한기가 실제로 해제되었는지 (입력 + 쿨타임 아님)
    private bool IsAoALimiterDisabled => aoaInputPressed && !aoaOnCooldown;

    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        // Use realistic gravity setting
        rb.useGravity = true;
        // CRITICAL: Rigidbody의 기본 Drag를 0으로 설정 (우리가 직접 계산하므로)
        rb.linearDamping = 0f;
        rb.angularDamping = 0f; // 각속도 댐핑도 우리가 직접 계산
        rb.linearVelocity = transform.forward * 80f; // 80 m/s (288 km/h)로 시작

        Debug.Log($"Aircraft initialized - Mass: {rb.mass}kg");
    }

    void Update()
    {
        // Update UI displays
        if (altitudeText != null)
        {
            altitudeText.text = $"Altitude: {transform.position.y:F1} m";
        }
        if (speedText != null)
        {
            speedText.text = $"Speed: {rb.linearVelocity.magnitude:F1} m/s";
        }

        // AOA 쿨타임 처리
        UpdateAoACooldown();
    }

    void FixedUpdate()
    {
        // Apply physics in FixedUpdate for consistent simulation
        ApplyThrust();
        ApplyAerodynamics(); // 양력과 항력을 함께 처리
        ApplyControlTorque();
        ApplyAngularDamping();
        ApplyAoALimiter(); // AOA 제한기 적용
    }

    // Applies forward thrust based on throttle input
    // 기체의 forward 방향으로 추진력 적용 (원래대로)
    void ApplyThrust()
    {
        Vector3 thrustForce = transform.forward * throttleInput * enginePower;
        rb.AddForce(thrustForce, ForceMode.Force);
    }

    // Applies a simple drag force to slow the aircraft
    void ApplyDrag()
    {
        rb.linearVelocity *= (1f - drag * Time.fixedDeltaTime);
    }

    // 양력과 방향별 항력 적용
    void ApplyAerodynamics()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed < 0.1f)
        {
            currentAoA = 0f;
            return;
        }

        // 받음각(Angle of Attack) 계산
        Vector3 localVel = transform.InverseTransformDirection(velocity.normalized);
        currentAoA = Mathf.Atan2(localVel.y, localVel.z) * Mathf.Rad2Deg;

        // 1. 방향별 항력
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);

        Vector3 localDragForce = Vector3.zero;
        // 전방 항력: 선형 비례 (관성 유지)
        localDragForce.z = -localVelocity.z * drag * rb.mass;
        // 측면 항력: 제곱 비례 (미끄러짐 강하게 억제, 기체 방향으로 정렬)
        localDragForce.x = -localVelocity.x * Mathf.Abs(localVelocity.x) * lateralDrag;
        // 수직 항력: 제곱 비례
        localDragForce.y = -localVelocity.y * Mathf.Abs(localVelocity.y) * lateralDrag * 0.5f;

        Vector3 worldDragForce = transform.TransformDirection(localDragForce);
        rb.AddForce(worldDragForce, ForceMode.Force);

        // 2. 양력 (Lift) - 속도와 받음각 기반
        // 실속 속도 이하에서는 양력 급감
        float speedFactor = 0f;
        if (speed > stallSpeed)
        {
            // 실속 속도 이상: 정상 양력
            speedFactor = 1f;
        }
        else if (speed > minLiftSpeed)
        {
            // 실속 구간: 양력 급감
            float t = (speed - minLiftSpeed) / (stallSpeed - minLiftSpeed);
            speedFactor = t * t; // 제곱으로 급격히 감소
        }
        // minLiftSpeed 이하: 양력 없음

        if (speedFactor > 0f)
        {
            float baseLift = speed * liftCoefficient * rb.mass / 100f;

            // 받음각에 따른 양력 계산
            float aoaFactor = 0f;
            float absAoA = Mathf.Abs(currentAoA);

            if (absAoA <= 15f)
            {
                // 선형 증가
                aoaFactor = 0.2f + (absAoA / 15f) * 0.8f;
            }
            else if (absAoA <= 25f)
            {
                // 최대 양력 구간
                aoaFactor = 1f;
            }
            else if (absAoA <= 40f)
            {
                // 실속 시작 - 급격한 감소
                aoaFactor = 1f - (absAoA - 25f) / 15f;
            }
            else
            {
                // 완전 실속
                aoaFactor = 0f;
            }

            // 기체가 위를 향하는 정도
            float upDot = Vector3.Dot(transform.up, Vector3.up);
            upDot = Mathf.Clamp(upDot, -0.5f, 1f); // 뒤집히면 역양력

            float liftPower = baseLift * aoaFactor * speedFactor * upDot;
            Vector3 liftForce = transform.up * liftPower;
            rb.AddForce(liftForce, ForceMode.Force);

            // 디버그
            Debug.DrawRay(transform.position, transform.up * (liftPower / 100f), Color.green, 0.1f);
        }
    }

    // Applies torque to rotate the aircraft towards the camera's direction
    void ApplyControlTorque()
    {
        if (cameraController == null) return;

        // Get the target direction from the camera
        Quaternion targetRotation = cameraController.transform.rotation;
        Quaternion currentRotation = transform.rotation;

        // Calculate the shortest rotation difference
        Quaternion rotationDifference = targetRotation * Quaternion.Inverse(currentRotation);

        // Convert to angle-axis
        rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);

        // Normalize angle to [-180, 180]
        if (angle > 180f)
            angle -= 360f;

        // 월드 공간의 회전 축을 정규화
        if (axis.sqrMagnitude > 0.001f)
            axis.Normalize();
        else
            return;

        // 로컬 축으로 변환
        Vector3 localAxis = transform.InverseTransformDirection(axis);

        // 현재 로컬 각속도 (degree/초)
        Vector3 localAngularVelocityDeg = transform.InverseTransformDirection(rb.angularVelocity) * Mathf.Rad2Deg;

        // 각 축별 목표 각속도 계산 (degree/초)
        // 목표와의 각도 차이에 비례하되, 최대 속도 제한
        Vector3 targetAngularVelocity = new Vector3(
            Mathf.Clamp(localAxis.x * angle * rotationP, -pitchSpeed, pitchSpeed),
            Mathf.Clamp(localAxis.y * angle * rotationP, -yawSpeed, yawSpeed),
            Mathf.Clamp(localAxis.z * angle * rotationP, -rollSpeed, rollSpeed)
        );

        // 목표 각속도와 현재 각속도의 차이
        Vector3 angularVelocityError = targetAngularVelocity - localAngularVelocityDeg;

        // 토크 계산 (각가속도)
        // rotationP: 목표 추종 속도, rotationD: 댐핑 (오버슈트 방지)
        Vector3 localTorque = angularVelocityError * rotationP - localAngularVelocityDeg * rotationD * 0.1f;

        // degree를 radian으로 변환하여 적용
        Vector3 worldTorque = transform.TransformDirection(localTorque * Mathf.Deg2Rad);
        rb.AddTorque(worldTorque, ForceMode.Acceleration);
    }

    // PD 컨트롤러에 D항이 포함되어 있으므로 별도 damping 불필요
    void ApplyAngularDamping()
    {
        // PD 컨트롤러의 D항이 damping 역할을 하므로 비활성화
        // 필요시 추가 damping:
        // rb.angularVelocity *= 0.99f;
    }

    // Set input values from external scripts (CameraController)
    public void SetThrottleInput(float throttle)
    {
        throttleInput = Mathf.Clamp01(throttle);
    }

    public void SetPitchAndRollInput(float pitch, float roll)
    {
        pitchInput = pitch;
        rollInput = roll;
    }

    // AOA 입력 처리 (홀드 키)
    public void SetAOAInput(bool pressed)
    {
        // 쿨타임 중이면 무시 (쿨타임 끝나면 바로 발동되도록 lastFrame은 false 유지)
        if (aoaOnCooldown)
        {
            aoaInputPressed = false;
            aoaInputPressedLastFrame = false;
            return;
        }

        // 에지 검출: 누르는 순간 (false → true)
        if (pressed && !aoaInputPressedLastFrame)
        {
            Debug.Log("AOA 제한기 해제!");
        }
        // 에지 검출: 떼는 순간 (true → false)
        else if (!pressed && aoaInputPressedLastFrame)
        {
            aoaOnCooldown = true;
            aoaCooldownTimer = aoaCooldown;
            Debug.Log($"AOA 쿨타임 시작: {aoaCooldown}초");
        }

        aoaInputPressed = pressed;
        aoaInputPressedLastFrame = pressed;
    }

    // AOA 쿨타임 업데이트
    void UpdateAoACooldown()
    {
        if (aoaOnCooldown)
        {
            aoaCooldownTimer -= Time.deltaTime;
            if (aoaCooldownTimer <= 0f)
            {
                aoaOnCooldown = false;
                aoaCooldownTimer = 0f;
                Debug.Log("AOA 쿨타임 종료 - 사용 가능");
            }
        }
    }

    // Connects the CameraController to this script
    public void SetCameraController(CameraController camera)
    {
        cameraController = camera;
    }

    // Method to get the current position and rotation
    public Vector3 GetPosition() => transform.position;
    public Quaternion GetRotation() => transform.rotation;

    void OnDrawGizmos()
    {
        if (rb == null) return;

        Vector3 pos = transform.position;

        // 기체 forward 방향 (빨강) - 추진력 방향
        Gizmos.color = Color.red;
        Gizmos.DrawRay(pos, transform.forward * 8f);

        // 현재 속도 방향 (청록)
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(pos, rb.linearVelocity.normalized * 6f);

            // 로컬 속도 벡터 (측면 이동 확인용)
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            Gizmos.color = new Color(1f, 0.5f, 0f); // 주황색
            Gizmos.DrawRay(pos + Vector3.down * 2f, transform.TransformDirection(new Vector3(localVel.x, 0, 0)).normalized * 3f);
        }

        // 카메라(목표) 방향 (녹색)
        if (cameraController != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(pos, cameraController.transform.forward * 10f);

            // 회전 차이 각도 표시
            Quaternion diff = cameraController.transform.rotation * Quaternion.Inverse(transform.rotation);
            diff.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

#if UNITY_EDITOR
            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
            UnityEditor.Handles.Label(pos + Vector3.up * 3f,
                $"Rot Diff: {Mathf.Abs(angle):F1}°\n" +
                $"Speed: {rb.linearVelocity.magnitude:F1} m/s\n" +
                $"Local Vel: ({localVel.x:F1}, {localVel.y:F1}, {localVel.z:F1})");
#endif
        }

        // 기체 축들
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(pos, transform.right * 3f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, transform.up * 3f);
    }

    // 받음각 제한기 (AoA Limiter)
    void ApplyAoALimiter()
    {
        // AOA 제한기가 해제되었으면 제한 없음
        if (IsAoALimiterDisabled) return;

        float speed = rb.linearVelocity.magnitude;
        if (speed < 5f) return; // 저속에서는 제한 안함

        // 현재 받음각이 제한을 초과하면 기수를 내리는 토크 적용
        float maxAoA = maxAoAWithLimiter;

        if (Mathf.Abs(currentAoA) > maxAoA)
        {
            // 초과된 받음각
            float excessAoA = Mathf.Abs(currentAoA) - maxAoA;

            // 기수를 내리는 방향으로 토크 적용
            float correctionTorque = -Mathf.Sign(currentAoA) * excessAoA * aoaLimiterStrength;
            rb.AddTorque(transform.right * correctionTorque, ForceMode.Acceleration);
        }
    }

    // AOA 상태 확인용 프로퍼티
    public bool IsAoAActive => IsAoALimiterDisabled; // AOA가 현재 활성화 상태인지
    public bool IsAoAOnCooldown => aoaOnCooldown;
    public float AoACooldownRemaining => aoaCooldownTimer;
    public float CurrentAoA => currentAoA;
}