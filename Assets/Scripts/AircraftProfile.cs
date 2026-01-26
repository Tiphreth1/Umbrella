// AircraftProfile.cs
// ScriptableObject로 비행기 기종별 특성을 정의합니다.
// 프로젝트 창에서 우클릭 > Create > Aircraft > Profile 로 생성 가능

using UnityEngine;

[CreateAssetMenu(fileName = "NewAircraftProfile", menuName = "Aircraft/Profile")]
public class AircraftProfile : ScriptableObject
{
    [Header("기체 정보")]
    public string aircraftName = "Unknown";
    [TextArea(2, 4)]
    public string description = "";

    [Header("엔진")]
    [Tooltip("엔진 추력 (N)")]
    public float enginePower = 100f;
    [Tooltip("최대 고도 (m) - 이 이상에서 엔진 효율 0")]
    public float maxAltitude = 15000f;
    [Tooltip("고도 효과 시작점 (m)")]
    public float altitudeEffectStart = 8000f;

    [Header("공력 특성")]
    [Tooltip("전방 항력 계수 (속도² 기반)")]
    public float drag = 0.001f;
    [Tooltip("측면 항력 계수")]
    public float lateralDrag = 0.3f;
    [Tooltip("유도 항력 계수 (받음각에 따른 추가 항력)")]
    public float inducedDragCoefficient = 0.05f;
    [Tooltip("양력 계수 (속도² 기반)")]
    public float liftCoefficient = 0.0003f;

    [Header("실속 특성")]
    [Tooltip("양력이 발생하는 최소 속도 (m/s)")]
    public float minLiftSpeed = 30f;
    [Tooltip("실속 속도 - 이 속도 이하에서 양력 급감 (m/s)")]
    public float stallSpeed = 50f;

    [Header("기동성")]
    [Tooltip("피치 속도 (도/초)")]
    public float pitchSpeed = 30f;
    [Tooltip("요 속도 (도/초)")]
    public float yawSpeed = 20f;
    [Tooltip("롤 속도 (도/초)")]
    public float rollSpeed = 50f;

    [Header("안정성")]
    [Tooltip("회전 비례 제어 (높을수록 빠르게 반응)")]
    [Range(0f, 10f)]
    public float rotationP = 5f;
    [Tooltip("회전 미분 제어 (높을수록 오버슈트 감소)")]
    [Range(0f, 5f)]
    public float rotationD = 2.5f;
    [Tooltip("월드 수평 복귀 강도")]
    [Range(0f, 1f)]
    public float worldLevelStrength = 0.3f;

    [Header("받음각(AoA) 설정")]
    [Tooltip("최대 받음각 (도) - 제한기 ON 시")]
    public float maxAoAWithLimiter = 15f;
    [Tooltip("최대 받음각 (도) - 제한기 OFF 시")]
    public float maxAoAWithoutLimiter = 45f;
    [Tooltip("받음각 제한 강도")]
    public float aoaLimiterStrength = 30f;
    [Tooltip("AOA 해제 시 기체 회전 속도 배율")]
    public float aoaSpeedMultiplier = 1.5f;
    [Tooltip("AOA 해제 후 쿨타임 (초)")]
    public float aoaCooldown = 5f;

    [Header("기체 물리")]
    [Tooltip("기체 질량 (kg)")]
    public float mass = 10000f;
}
