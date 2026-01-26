// GameManager.cs
// 게임 전반을 관리하고 플레이어 기체와 카메라를 연결합니다.

using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("카메라 시스템")]
    public CameraController cameraController;
    public VirtualCursorController virtualCursor;

    [Header("플레이어 설정")]
    [Tooltip("시작 시 플레이어로 지정할 기체 (없으면 첫 번째 기체 사용)")]
    public AircraftController initialPlayerAircraft;

    [Header("등록된 기체들")]
    [SerializeField] private List<AircraftController> allAircraft = new List<AircraftController>();

    // 현재 플레이어가 조종 중인 기체
    private AircraftController currentPlayerAircraft;
    private int currentViewIndex = 0;

    public AircraftController CurrentPlayerAircraft => currentPlayerAircraft;
    public IReadOnlyList<AircraftController> AllAircraft => allAircraft;

    void Awake()
    {
        // 싱글톤 설정
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 카메라 컨트롤러 자동 검색
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
        }

        // 가상 커서 자동 검색
        if (virtualCursor == null)
        {
            virtualCursor = FindObjectOfType<VirtualCursorController>();
        }

        // 씬의 모든 비행기 수집
        CollectAllAircraft();

        // 초기 플레이어 기체 설정
        if (initialPlayerAircraft != null)
        {
            SetPlayerAircraft(initialPlayerAircraft);
        }
        else if (allAircraft.Count > 0)
        {
            SetPlayerAircraft(allAircraft[0]);
        }
        else
        {
            Debug.LogWarning("[GameManager] No aircraft found in scene!");
        }
    }

    // 씬의 모든 비행기 수집
    public void CollectAllAircraft()
    {
        allAircraft.Clear();
        var aircraft = FindObjectsOfType<AircraftController>();
        allAircraft.AddRange(aircraft);
        Debug.Log($"[GameManager] Found {allAircraft.Count} aircraft");
    }

    // 비행기 등록 (런타임 스폰 시 사용)
    public void RegisterAircraft(AircraftController aircraft)
    {
        if (!allAircraft.Contains(aircraft))
        {
            allAircraft.Add(aircraft);
            Debug.Log($"[GameManager] Aircraft registered: {aircraft.name}");
        }
    }

    // 비행기 등록 해제 (파괴 시 사용)
    public void UnregisterAircraft(AircraftController aircraft)
    {
        if (allAircraft.Contains(aircraft))
        {
            allAircraft.Remove(aircraft);

            // 현재 플레이어 기체가 파괴되면 다른 기체로 전환
            if (currentPlayerAircraft == aircraft)
            {
                if (allAircraft.Count > 0)
                {
                    SetPlayerAircraft(allAircraft[0]);
                }
                else
                {
                    currentPlayerAircraft = null;
                    Debug.LogWarning("[GameManager] No aircraft remaining!");
                }
            }
        }
    }

    // 플레이어 기체 설정
    public void SetPlayerAircraft(AircraftController aircraft)
    {
        if (aircraft == null)
        {
            Debug.LogWarning("[GameManager] Cannot set null aircraft as player!");
            return;
        }

        // 이전 플레이어 기체 연결 해제
        if (currentPlayerAircraft != null)
        {
            // AI 컨트롤러가 있다면 활성화
            var prevAI = currentPlayerAircraft.GetComponent<AIController>();
            if (prevAI != null)
            {
                prevAI.enabled = true;
            }
        }

        currentPlayerAircraft = aircraft;
        currentViewIndex = allAircraft.IndexOf(aircraft);

        // 새 플레이어 기체의 AI 비활성화
        var newAI = aircraft.GetComponent<AIController>();
        if (newAI != null)
        {
            newAI.enabled = false;
        }

        // 카메라 연결
        if (cameraController != null)
        {
            cameraController.SetTarget(aircraft);
        }

        Debug.Log($"[GameManager] Player aircraft set: {aircraft.name}");
    }

    // 다음 기체로 시점 전환
    public void SwitchToNextAircraft()
    {
        if (allAircraft.Count <= 1) return;

        currentViewIndex = (currentViewIndex + 1) % allAircraft.Count;
        SetPlayerAircraft(allAircraft[currentViewIndex]);
    }

    // 이전 기체로 시점 전환
    public void SwitchToPreviousAircraft()
    {
        if (allAircraft.Count <= 1) return;

        currentViewIndex--;
        if (currentViewIndex < 0) currentViewIndex = allAircraft.Count - 1;
        SetPlayerAircraft(allAircraft[currentViewIndex]);
    }

    // 특정 인덱스의 기체로 전환
    public void SwitchToAircraft(int index)
    {
        if (index >= 0 && index < allAircraft.Count)
        {
            SetPlayerAircraft(allAircraft[index]);
        }
    }

    void Update()
    {
        // 시점 전환 단축키 (테스트용)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            SwitchToNextAircraft();
        }
    }
}
