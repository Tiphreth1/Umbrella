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
    public float enginePower = 50f;
    public float drag = 0.02f;
    public float lateralDrag = 0.5f; // 측면 항력 (전진 방향이 아닌 힘에 저항)
    public float liftCoefficient = 5f; // 양력 계수
    public float minLiftSpeed = 10f; // 양력이 발생하는 최소 속도

    [Header("조종 속도")]
    public float pitchSpeed = 30f;
    public float yawSpeed = 20f;
    public float rollSpeed = 50f;

    [Header("안정성 설정")]
    [Range(0f, 1f)]
    public float angularDamping = 0.5f; // 각속도 감쇠
    [Range(0.1f, 5f)]
    public float rotationSmoothness = 1.5f; // 회전 부드러움

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

    // A reference to the CameraController to get the target direction
    private CameraController cameraController;
    // The current input values for throttle, pitch, and roll
    private float throttleInput;
    private float pitchInput;
    private float rollInput;

    // 받음각 제한기 상태
    private bool aoaLimiterEnabled = true;
    // 현재 받음각 (디버깅용)
    private float currentAoA = 0f;

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

        // 받음각 제한기 토글 (Left Shift)
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            aoaLimiterEnabled = !aoaLimiterEnabled;
            Debug.Log($"AoA Limiter: {(aoaLimiterEnabled ? "ON" : "OFF")}");
        }
    }

    void FixedUpdate()
    {
        // Apply physics in FixedUpdate for consistent simulation
        ApplyThrust();
        ApplyAerodynamics(); // 양력과 항력을 함께 처리
        ApplyControlTorque();
        ApplyAngularDamping();
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

        // 1. 방향별 항력 (Directional Drag)
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);

        Vector3 localDragForce = Vector3.zero;
        localDragForce.z = -localVelocity.z * Mathf.Abs(localVelocity.z) * drag;
        localDragForce.x = -localVelocity.x * Mathf.Abs(localVelocity.x) * lateralDrag;
        localDragForce.y = -localVelocity.y * Mathf.Abs(localVelocity.y) * lateralDrag * 0.7f;

        Vector3 worldDragForce = transform.TransformDirection(localDragForce);
        rb.AddForce(worldDragForce, ForceMode.Force);

        // 2. 양력 (Lift) - 받음각 기반
        if (speed > minLiftSpeed)
        {
            float speedRatio = speed / minLiftSpeed;
            float baseLift = speedRatio * speedRatio * liftCoefficient * rb.mass; // 질량에 비례

            // 받음각에 따른 양력 계산 (받음각 곡선)
            // -15° ~ +15° 범위에서 최대 양력, 그 이상은 감소
            float aoaFactor = 0f;
            float absAoA = Mathf.Abs(currentAoA);

            if (absAoA <= 15f)
            {
                // 선형 증가
                aoaFactor = absAoA / 15f;
            }
            else if (absAoA <= 30f)
            {
                // 점진적 감소 (실속 전)
                aoaFactor = 1f - (absAoA - 15f) / 15f * 0.5f;
            }
            else
            {
                // 급격한 감소 (실속)
                aoaFactor = 0.5f - (absAoA - 30f) / 30f * 0.5f;
                aoaFactor = Mathf.Max(0f, aoaFactor);
            }

            // 기체가 위를 향하는 정도
            float upDot = Vector3.Dot(transform.up, Vector3.up);
            upDot = Mathf.Clamp01(upDot);

            // 최소 양력 보정 (수평 비행 시에도 약간의 양력)
            float minLiftFactor = 0.3f;
            aoaFactor = Mathf.Max(aoaFactor, minLiftFactor);

            float liftPower = baseLift * aoaFactor * upDot;
            Vector3 liftForce = transform.up * liftPower;
            rb.AddForce(liftForce, ForceMode.Force);

            // 디버그
            Debug.DrawRay(transform.position, transform.up * (liftPower / 1000f), Color.green, 0.1f);
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

        // 회전이 거의 완료되었으면 스킵
        if (Mathf.Abs(angle) < 0.5f)
        {
            // 아주 작은 차이는 직접 보정
            if (Mathf.Abs(angle) > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, Time.fixedDeltaTime * 10f);
            }
            return;
        }

        // 월드 공간의 회전 축을 정규화
        axis.Normalize();

        // 각도를 라디안으로 변환
        float angleRad = angle * Mathf.Deg2Rad;

        // 각 축별로 토크 계산 (월드 축 기준)
        Vector3 torqueVector = axis * angleRad * rotationSmoothness;

        // 각 로컬 축에 투영하여 개별 속도 제한 적용
        Vector3 localTorque = transform.InverseTransformDirection(torqueVector);

        localTorque.x = Mathf.Clamp(localTorque.x * pitchSpeed, -pitchSpeed, pitchSpeed);
        localTorque.y = Mathf.Clamp(localTorque.y * yawSpeed, -yawSpeed, yawSpeed);
        localTorque.z = Mathf.Clamp(localTorque.z * rollSpeed, -rollSpeed, rollSpeed);

        // 로컬 토크를 월드 공간으로 변환하여 적용
        Vector3 worldTorque = transform.TransformDirection(localTorque);
        rb.AddTorque(worldTorque, ForceMode.Acceleration);
    }

    // 각속도 댐핑 적용으로 나풀거림 방지
    void ApplyAngularDamping()
    {
        rb.angularVelocity *= (1f - angularDamping * Time.fixedDeltaTime * 10f);
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
        if (!aoaLimiterEnabled) return;

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
}