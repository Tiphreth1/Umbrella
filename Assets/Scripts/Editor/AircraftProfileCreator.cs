// AircraftProfileCreator.cs
// 에디터에서 기본 비행기 프로필들을 생성하는 유틸리티

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class AircraftProfileCreator
{
    private const string ProfilePath = "Assets/ScriptableObjects/AircraftProfiles";

    [MenuItem("Tools/Aircraft/Create Default Profiles")]
    public static void CreateDefaultProfiles()
    {
        // 폴더 생성
        if (!AssetDatabase.IsValidFolder(ProfilePath))
        {
            Directory.CreateDirectory(ProfilePath);
            AssetDatabase.Refresh();
        }

        // 전투기 프로필
        CreateFighterProfile();

        // 공격기 프로필
        CreateAttackerProfile();

        // 수송기 프로필
        CreateTransportProfile();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Default aircraft profiles created at: " + ProfilePath);
    }

    private static void CreateFighterProfile()
    {
        string path = $"{ProfilePath}/Fighter_F16.asset";
        if (AssetDatabase.LoadAssetAtPath<AircraftProfile>(path) != null) return;

        var profile = ScriptableObject.CreateInstance<AircraftProfile>();
        profile.aircraftName = "F-16 Fighting Falcon";
        profile.description = "경량 다목적 전투기. 높은 기동성과 빠른 롤 속도가 특징.";

        // 엔진
        profile.enginePower = 120f;
        profile.maxAltitude = 15000f;
        profile.altitudeEffectStart = 10000f;

        // 공력 - 낮은 양력, 낮은 항력 (고속 유지)
        profile.drag = 0.0008f;
        profile.lateralDrag = 0.25f;
        profile.inducedDragCoefficient = 0.04f;
        profile.liftCoefficient = 0.00025f;

        // 실속 - 높은 실속 속도 (저속에 약함)
        profile.minLiftSpeed = 40f;
        profile.stallSpeed = 60f;

        // 기동성 - 매우 빠름
        profile.pitchSpeed = 40f;
        profile.yawSpeed = 25f;
        profile.rollSpeed = 70f;

        // 안정성 - 민감한 반응
        profile.rotationP = 6f;
        profile.rotationD = 2f;
        profile.worldLevelStrength = 0.2f;

        // AOA
        profile.maxAoAWithLimiter = 18f;
        profile.maxAoAWithoutLimiter = 50f;
        profile.aoaLimiterStrength = 35f;
        profile.aoaSpeedMultiplier = 1.8f;
        profile.aoaCooldown = 4f;

        // 질량
        profile.mass = 8500f;

        AssetDatabase.CreateAsset(profile, path);
    }

    private static void CreateAttackerProfile()
    {
        string path = $"{ProfilePath}/Attacker_A10.asset";
        if (AssetDatabase.LoadAssetAtPath<AircraftProfile>(path) != null) return;

        var profile = ScriptableObject.CreateInstance<AircraftProfile>();
        profile.aircraftName = "A-10 Thunderbolt II";
        profile.description = "근접 지원 공격기. 안정적이고 저속에서 강한 양력.";

        // 엔진 - 중간 출력
        profile.enginePower = 80f;
        profile.maxAltitude = 12000f;
        profile.altitudeEffectStart = 7000f;

        // 공력 - 높은 양력, 높은 항력
        profile.drag = 0.0015f;
        profile.lateralDrag = 0.4f;
        profile.inducedDragCoefficient = 0.06f;
        profile.liftCoefficient = 0.00045f;

        // 실속 - 낮은 실속 속도 (저속에 강함)
        profile.minLiftSpeed = 20f;
        profile.stallSpeed = 35f;

        // 기동성 - 느림
        profile.pitchSpeed = 25f;
        profile.yawSpeed = 15f;
        profile.rollSpeed = 35f;

        // 안정성 - 안정적
        profile.rotationP = 4f;
        profile.rotationD = 3f;
        profile.worldLevelStrength = 0.4f;

        // AOA
        profile.maxAoAWithLimiter = 12f;
        profile.maxAoAWithoutLimiter = 35f;
        profile.aoaLimiterStrength = 25f;
        profile.aoaSpeedMultiplier = 1.3f;
        profile.aoaCooldown = 6f;

        // 질량
        profile.mass = 12000f;

        AssetDatabase.CreateAsset(profile, path);
    }

    private static void CreateTransportProfile()
    {
        string path = $"{ProfilePath}/Transport_C130.asset";
        if (AssetDatabase.LoadAssetAtPath<AircraftProfile>(path) != null) return;

        var profile = ScriptableObject.CreateInstance<AircraftProfile>();
        profile.aircraftName = "C-130 Hercules";
        profile.description = "대형 수송기. 매우 안정적이고 양력이 좋지만 기동성이 낮음.";

        // 엔진 - 높은 출력 (무거우니까)
        profile.enginePower = 150f;
        profile.maxAltitude = 10000f;
        profile.altitudeEffectStart = 6000f;

        // 공력 - 매우 높은 양력, 매우 높은 항력
        profile.drag = 0.002f;
        profile.lateralDrag = 0.5f;
        profile.inducedDragCoefficient = 0.08f;
        profile.liftCoefficient = 0.0006f;

        // 실속 - 매우 낮은 실속 속도
        profile.minLiftSpeed = 15f;
        profile.stallSpeed = 28f;

        // 기동성 - 매우 느림
        profile.pitchSpeed = 15f;
        profile.yawSpeed = 10f;
        profile.rollSpeed = 20f;

        // 안정성 - 매우 안정적
        profile.rotationP = 3f;
        profile.rotationD = 3.5f;
        profile.worldLevelStrength = 0.5f;

        // AOA
        profile.maxAoAWithLimiter = 10f;
        profile.maxAoAWithoutLimiter = 25f;
        profile.aoaLimiterStrength = 20f;
        profile.aoaSpeedMultiplier = 1.2f;
        profile.aoaCooldown = 8f;

        // 질량
        profile.mass = 35000f;

        AssetDatabase.CreateAsset(profile, path);
    }
}
#endif
