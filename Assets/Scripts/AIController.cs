// AIController.cs
// AI 기체의 자율 비행을 담당합니다.
// 플레이어가 이 기체를 조종하면 GameManager가 이 컴포넌트를 비활성화합니다.

using UnityEngine;

[RequireComponent(typeof(FlightProxyController))]
public class AIController : MonoBehaviour
{
    [Header("AI 설정")]
    [Tooltip("목표 고도 (m)")]
    public float targetAltitude = 500f;
    [Tooltip("목표 속도 (m/s)")]
    public float targetSpeed = 80f;
    [Tooltip("순찰 반경 (m)")]
    public float patrolRadius = 1000f;

    [Header("행동 설정")]
    public AIBehavior behavior = AIBehavior.Patrol;

    private FlightProxyController flightProxy;
    private Vector3 targetPosition;
    private float nextWaypointTime;

    // 가상 카메라 역할을 할 transform
    private Transform virtualCamera;

    public enum AIBehavior
    {
        Idle,       // 현재 방향 유지
        Patrol,     // 순찰
        Follow,     // 특정 타겟 추적
        Evade       // 회피
    }

    void Start()
    {
        flightProxy = GetComponent<FlightProxyController>();

        // 가상 카메라 생성 (AI의 목표 방향을 나타냄)
        var vcObj = new GameObject($"{name}_VirtualCamera");
        virtualCamera = vcObj.transform;
        virtualCamera.SetParent(transform);
        virtualCamera.localPosition = Vector3.zero;
        virtualCamera.localRotation = Quaternion.identity;

        // 초기 목표 설정
        SetNewPatrolTarget();
    }

    void Update()
    {
        if (!enabled) return;

        switch (behavior)
        {
            case AIBehavior.Idle:
                UpdateIdle();
                break;
            case AIBehavior.Patrol:
                UpdatePatrol();
                break;
            case AIBehavior.Follow:
                UpdateFollow();
                break;
            case AIBehavior.Evade:
                UpdateEvade();
                break;
        }

        // 입력 적용
        ApplyInputs();
    }

    void UpdateIdle()
    {
        // 현재 방향 유지, 고도만 조절
        MaintainAltitude();
        flightProxy.SetThrottleInput(0.5f);
    }

    void UpdatePatrol()
    {
        // 목표 지점으로 비행
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0; // 수평 방향만

        if (toTarget.magnitude < 100f || Time.time > nextWaypointTime)
        {
            SetNewPatrolTarget();
        }

        // 목표 방향으로 가상 카메라 회전
        if (toTarget.magnitude > 1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            virtualCamera.rotation = Quaternion.Slerp(virtualCamera.rotation, targetRot, Time.deltaTime * 2f);
        }

        MaintainAltitude();

        // 스로틀 조절
        float currentSpeed = flightProxy.rb.linearVelocity.magnitude;
        float throttle = currentSpeed < targetSpeed ? 0.8f : 0.4f;
        flightProxy.SetThrottleInput(throttle);
    }

    void UpdateFollow()
    {
        // TODO: 추적 대상 구현
        UpdatePatrol();
    }

    void UpdateEvade()
    {
        // TODO: 회피 기동 구현
        UpdatePatrol();
    }

    void MaintainAltitude()
    {
        float currentAlt = transform.position.y;
        float altError = targetAltitude - currentAlt;

        // 고도에 따라 피치 조절
        float pitchInput = Mathf.Clamp(altError / 100f, -0.5f, 0.5f);

        // 가상 카메라 피치 조절
        Vector3 euler = virtualCamera.eulerAngles;
        euler.x = -pitchInput * 30f; // 피치 각도로 변환
        virtualCamera.eulerAngles = euler;
    }

    void SetNewPatrolTarget()
    {
        // 랜덤 순찰 지점 설정
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        targetPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        targetPosition.y = targetAltitude;

        nextWaypointTime = Time.time + 30f; // 30초 후 새 목표

        Debug.Log($"[AI] {name} new patrol target: {targetPosition}");
    }

    void ApplyInputs()
    {
        // 가상 카메라 방향을 기반으로 입력 계산
        // 이 부분은 AircraftController가 CameraController 없이도 동작하도록 수정 필요

        // 임시: 직접 토크 적용 대신 스로틀만 조절
        // 나중에 AircraftController에 AI 모드 추가 가능
    }

    void OnDestroy()
    {
        if (virtualCamera != null)
        {
            Destroy(virtualCamera.gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 목표 지점 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(targetPosition, 20f);
        Gizmos.DrawLine(transform.position, targetPosition);

        // 순찰 범위 표시
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }
}
