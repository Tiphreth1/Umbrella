// FlightProxyController.cs
// 실제 비행 물리와 조작을 담당하는 투명 오브젝트
// 카메라가 이 오브젝트를 따라가고, 시각적 기체는 카메라 forward를 추종

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlightProxyController : MonoBehaviour
{
    [Header("비행기 물리")]
    [HideInInspector] public Rigidbody rb; // Start()에서 자동 할당
    public float aircraftMass = 1f; // 이전 작동 값
    public float enginePower = 100f; // 이전 작동 값
    public float drag = 0.001f;
    public float lateralDrag = 0.3f;
    public float inducedDragCoefficient = 0.05f;
    public float liftCoefficient = 0.0003f;
    public float minLiftSpeed = 30f;
    public float stallSpeed = 50f;

    [Header("고도 제한")]
    public float maxAltitude = 15000f;
    public float altitudeEffectStart = 8000f;

    [Header("조종 속도")]
    public float pitchSpeed = 90f; // 이전 작동 값
    public float yawSpeed = 20f;
    public float rollSpeed = 50f;
    public float aoaSpeedMultiplier = 1.5f;

    [Header("받음각(AoA) 설정")]
    public float maxAoAWithLimiter = 15f;
    public float maxAoAWithoutLimiter = 45f;
    public float aoaLimiterStrength = 30f;
    public float aoaCooldown = 5f;

    [Header("회전 제어")]
    [Range(0f, 10f)]
    public float rotationP = 5f; // 회전 비례 제어 (높을수록 빠르게 반응)
    [Range(0f, 5f)]
    public float rotationD = 2.5f; // 회전 미분 제어 (높을수록 오버슈트 감소)
    [Range(0f, 1f)]
    public float worldLevelStrength = 0.3f; // 월드 수평 복귀 강도 (0=카메라만, 1=월드만)

    // 참조
    private VirtualCursorController virtualCursor;
    private CameraController cameraController;

    // 입력
    private float throttleInput = 0.5f; // 기본값 50% (디버깅용)

    // AOA 상태
    private bool aoaInputPressed = false;
    private bool aoaInputPressedLastFrame = false;
    private bool aoaOnCooldown = false;
    private float aoaCooldownTimer = 0f;
    private float currentAoA = 0f;
    private float stallDuration = 0f;

    public bool IsAoALimiterDisabled => aoaInputPressed && !aoaOnCooldown;
    public float StallIntensity { get; private set; }
    public float CurrentAoA => currentAoA;

    void Awake()
    {
        // rb는 반드시 Awake에서 먼저 초기화
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Awake에서 isKinematic 해제 (다른 스크립트보다 먼저)
        rb.isKinematic = false;
    }

    void Start()
    {
        rb.isKinematic = false;  // 물리 활성화 필수!
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;  // 모든 제약 해제

        rb.mass = aircraftMass;  // 질량 설정
        rb.linearDamping = 0f;
        rb.angularDamping = 0.5f;  // 이전 작동 값

        rb.linearVelocity = transform.forward * 80f;

        virtualCursor = FindObjectOfType<VirtualCursorController>();
        cameraController = FindObjectOfType<CameraController>();

        // 초기 상태 확인
        Debug.Log($"[FlightProxy] Camera: {(cameraController != null ? cameraController.name : "NULL")}, VirtualCursor: {(virtualCursor != null ? "OK" : "NULL")}");
        Debug.Log($"[FlightProxy] 초기 forward: {transform.forward}, up: {transform.up}");
        Debug.Log($"[FlightProxy] 초기 euler: {transform.eulerAngles}, enginePower: {enginePower}");

        // 이 오브젝트가 별도의 빈 오브젝트면 렌더러가 없을 것
        // 혹시 있으면 비활성화 (투명 오브젝트)
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = false;
    }

    void Update()
    {
        UpdateAoACooldown();
    }

    void FixedUpdate()
    {
        // 다른 스크립트가 isKinematic을 바꿨으면 강제로 되돌림
        if (rb.isKinematic)
        {
            Debug.LogWarning("[FlightProxy] isKinematic이 True로 변경됨! 강제로 False로 복구합니다.");
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * 80f; // 초기 속도 복구
        }

        ApplyThrust();
        ApplyAerodynamics();
        ApplyControlRotation();
        ApplyAoALimiter();

    }

    void ApplyThrust()
    {
        float altitudeEfficiency = GetAltitudeEfficiency();
        Vector3 thrustForce = transform.forward * throttleInput * enginePower * altitudeEfficiency;
        rb.AddForce(thrustForce, ForceMode.Force);

        // 추력 디버그 (1초에 한번)
        if (Time.frameCount % 60 == 0)
        {
            float altitude = transform.position.y;
            float verticalSpeed = rb.linearVelocity.y;
            Vector3 euler = transform.eulerAngles;
            Debug.Log($"[FlightProxy] Alt:{altitude:F0}m VSpd:{verticalSpeed:F1} Spd:{rb.linearVelocity.magnitude:F1} Throttle:{throttleInput:F2} Euler:({euler.x:F1},{euler.y:F1},{euler.z:F1}) Forward:{transform.forward}");
        }
    }

    float GetAltitudeEfficiency()
    {
        float altitude = transform.position.y;
        if (altitude <= altitudeEffectStart) return 1f;
        if (altitude >= maxAltitude) return 0f;
        float t = (altitude - altitudeEffectStart) / (maxAltitude - altitudeEffectStart);
        return 1f - t;
    }

    void ApplyAerodynamics()
    {
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed < 0.1f)
        {
            currentAoA = 0f;
            return;
        }

        // 받음각 계산
        Vector3 localVel = transform.InverseTransformDirection(velocity.normalized);
        currentAoA = Mathf.Atan2(localVel.y, localVel.z) * Mathf.Rad2Deg;

        // 방향별 항력
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        Vector3 localDragForce = Vector3.zero;
        localDragForce.z = -localVelocity.z * Mathf.Abs(localVelocity.z) * drag * rb.mass;
        localDragForce.x = -localVelocity.x * Mathf.Abs(localVelocity.x) * lateralDrag;
        localDragForce.y = -localVelocity.y * Mathf.Abs(localVelocity.y) * lateralDrag * 0.5f;

        Vector3 worldDragForce = transform.TransformDirection(localDragForce);
        rb.AddForce(worldDragForce, ForceMode.Force);

        // 양력
        float speedFactor = 0f;
        if (speed > stallSpeed)
            speedFactor = 1f;
        else if (speed > minLiftSpeed)
        {
            float t = (speed - minLiftSpeed) / (stallSpeed - minLiftSpeed);
            speedFactor = t * t;
        }

        float altitudeEfficiency = GetAltitudeEfficiency();

        if (speedFactor > 0f && altitudeEfficiency > 0f)
        {
            float baseLift = speed * speed * liftCoefficient * rb.mass;
            float absAoA = Mathf.Abs(currentAoA);
            float aoaFactor = 0f;

            if (absAoA <= 15f)
                aoaFactor = 0.2f + (absAoA / 15f) * 0.8f;
            else if (absAoA <= 25f)
                aoaFactor = 1f;
            else if (absAoA <= 40f)
                aoaFactor = 1f - (absAoA - 25f) / 15f;

            float upDot = Vector3.Dot(transform.up, Vector3.up);
            upDot = Mathf.Clamp(upDot, -0.5f, 1f);

            float liftPower = baseLift * aoaFactor * speedFactor * upDot * altitudeEfficiency;
            rb.AddForce(transform.up * liftPower, ForceMode.Force);
        }

        // 유도 항력
        if (speed > 1f)
        {
            float absAoA = Mathf.Abs(currentAoA);
            float inducedDragFactor = (absAoA / 15f) * (absAoA / 15f);
            float inducedDrag = speed * speed * inducedDragCoefficient * inducedDragFactor * altitudeEfficiency;
            rb.AddForce(-velocity.normalized * inducedDrag, ForceMode.Force);
        }

    }

    void ApplyControlRotation()
    {
        if (cameraController == null)
        {
            Debug.LogWarning("[FlightProxy] cameraController is NULL - 회전 제어 불가!");
            return;
        }

        Transform cam = cameraController.transform;

        // === Quaternion으로 pitch/yaw 계산 ===
        Vector3 targetForward = cam.forward;
        Vector3 currentForward = transform.forward;

        Quaternion forwardRotation = Quaternion.FromToRotation(currentForward, targetForward);
        forwardRotation.ToAngleAxis(out float forwardAngle, out Vector3 forwardAxis);

        if (forwardAngle > 180f) forwardAngle -= 360f;

        Vector3 localForwardAxis = Vector3.zero;
        if (forwardAxis.sqrMagnitude > 0.001f)
        {
            forwardAxis.Normalize();
            localForwardAxis = transform.InverseTransformDirection(forwardAxis);
        }

        float pitchError = localForwardAxis.x * forwardAngle;
        float yawError = localForwardAxis.y * forwardAngle;

        // === Roll: 카메라 롤 + 월드 수평 블렌딩 ===
        Vector3 camUpLocal = transform.InverseTransformDirection(cam.up);
        float cameraRollError = Mathf.Atan2(-camUpLocal.x, camUpLocal.y) * Mathf.Rad2Deg;

        Vector3 worldUpLocal = transform.InverseTransformDirection(Vector3.up);
        float worldRollError = Mathf.Atan2(-worldUpLocal.x, worldUpLocal.y) * Mathf.Rad2Deg;

        float rollError = Mathf.Lerp(cameraRollError, worldRollError, worldLevelStrength);

        // 실속 체크
        float speed = rb.linearVelocity.magnitude;
        StallIntensity = 0f;

        if (speed < stallSpeed)
        {
            StallIntensity = 1f - (speed / stallSpeed);
            StallIntensity = StallIntensity * StallIntensity;
            stallDuration += Time.fixedDeltaTime;

            // 실속 시 조종 약화
            pitchError *= (1f - StallIntensity * 0.9f);
            yawError *= (1f - StallIntensity * 0.95f);
            rollError *= (1f - StallIntensity * 0.9f);

            // 실속 시 기수 강제 하향
            float stallPitchDown = Mathf.Lerp(45f, 80f, Mathf.Clamp01(stallDuration / 3f)) * StallIntensity;
            pitchError = Mathf.Lerp(pitchError, stallPitchDown, StallIntensity);
        }
        else
        {
            stallDuration = 0f;
        }

        // 카메라에 실속 상태 전달
        if (cameraController != null)
        {
            cameraController.SetStallIntensity(StallIntensity);
        }

        // 속도 배율
        float speedMult = IsAoALimiterDisabled ? aoaSpeedMultiplier : 1f;
        float maxRotationSpeed = Mathf.Max(pitchSpeed, rollSpeed) * speedMult;
        float maxDelta = maxRotationSpeed * Time.fixedDeltaTime;

        // 커서 복귀 속도 동기화
        if (virtualCursor != null)
        {
            virtualCursor.SetReturnSpeed(rollSpeed * speedMult);
        }

        // === 목표 회전 계산 (원본 방식: MoveRotation) ===
        Vector3 targetUp = Quaternion.AngleAxis(-rollError, targetForward) * Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(targetForward, targetUp);

        // 보간 후 적용
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDelta);
        rb.MoveRotation(newRotation);

        // 디버그 (1초에 한번)
        if (Time.frameCount % 60 == 0)
        {
            float angleDiff = Vector3.Angle(transform.forward, cam.forward);
            Debug.Log($"[FlightProxy] AngleDiff:{angleDiff:F1}° PitchErr:{pitchError:F1} YawErr:{yawError:F1}");
        }
    }

    void ApplyAoALimiter()
    {
        // 일단 비활성화하고 기본 물리 테스트
        return;

        if (IsAoALimiterDisabled) return;

        float speed = rb.linearVelocity.magnitude;
        if (speed < 5f) return;

        if (Mathf.Abs(currentAoA) > maxAoAWithLimiter)
        {
            float excessAoA = Mathf.Abs(currentAoA) - maxAoAWithLimiter;
            // AoA가 양수면 기수 내림(+), 음수면 기수 올림(-)
            float correctionTorque = Mathf.Sign(currentAoA) * excessAoA * aoaLimiterStrength;
            rb.AddTorque(transform.right * correctionTorque, ForceMode.Acceleration);
        }
    }

    void UpdateAoACooldown()
    {
        if (aoaOnCooldown)
        {
            aoaCooldownTimer -= Time.deltaTime;
            if (aoaCooldownTimer <= 0f)
            {
                aoaOnCooldown = false;
                aoaCooldownTimer = 0f;
            }
        }
    }

    // 외부 입력
    public void SetThrottleInput(float throttle)
    {
        float old = throttleInput;
        throttleInput = Mathf.Clamp01(throttle);
        if (Mathf.Abs(old - throttleInput) > 0.01f)
        {
            Debug.Log($"[FlightProxy] Throttle: {old:F2} → {throttleInput:F2}");
        }
    }

    public void SetAOAInput(bool pressed)
    {
        if (aoaOnCooldown)
        {
            aoaInputPressed = false;
            aoaInputPressedLastFrame = false;
            return;
        }

        if (!pressed && aoaInputPressedLastFrame)
        {
            aoaOnCooldown = true;
            aoaCooldownTimer = aoaCooldown;
        }

        aoaInputPressed = pressed;
        aoaInputPressedLastFrame = pressed;
    }

    // 상태 확인
    public bool IsAoAActive => IsAoALimiterDisabled;
    public bool IsAoAOnCooldown => aoaOnCooldown;
    public float AoACooldownRemaining => aoaCooldownTimer;
}
