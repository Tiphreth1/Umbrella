using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VirtualCursorController : MonoBehaviour
{
    [Header("UI 설정")]
    public RectTransform virtualCursorUI;
    public Canvas canvas;

    [Header("감도 설정")]
    [Range(0.1f, 10f)]
    public float sensitivity = 2f;

    [Header("커서 제한 설정")]
    public bool confineCursor = false;
    public Vector2 screenMargin = new Vector2(10f, 10f);

    [Header("마우스 커서 설정")]
    public bool lockRealCursor = true;
    public bool hideRealCursor = true;

    [Header("입력 감지")]
    public float inputThreshold = 0.1f;

    [Header("자동 중앙 복귀")]
    [Tooltip("가장자리에서 중앙까지 복귀하는 데 걸리는 시간 (초)")]
    [Range(1f, 5f)]
    public float centerReturnTime = 2.5f;

    private Vector2 virtualCursorPos;
    private Vector2 screenCenter;
    private bool wasApplicationFocused = true;
    private bool isInitialized = false;
    private bool hasActiveMouseInput = false;

    // 카메라 컨트롤러 참조
    private CameraController cameraController;

    void Start()
    {
        if (canvas == null && virtualCursorUI != null)
        {
            canvas = virtualCursorUI.GetComponentInParent<Canvas>();
        }

        // 즉시 screenCenter 계산
        UpdateScreenCenter();

        // 프레임 끝에서 초기화 (모든 Start() 실행 후)
        StartCoroutine(InitializeAtEndOfFrame());
    }

    void UpdateScreenCenter()
    {
        screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    System.Collections.IEnumerator InitializeAtEndOfFrame()
    {
        // 현재 프레임이 끝날 때까지 대기
        yield return new WaitForEndOfFrame();

        // 다시 한번 화면 중앙 계산 (해상도 변경 대응)
        UpdateScreenCenter();
        virtualCursorPos = screenCenter;

        SetupRealCursor();
        isInitialized = true;

        Debug.Log($"[VirtualCursor] Initialized - Center: {screenCenter}, Screen: {Screen.width}x{Screen.height}, Cursor: {virtualCursorPos}");
    }

    void Update()
    {
        if (!isInitialized) return;

        // 해상도 변경 감지
        Vector2 currentScreenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        if (Vector2.Distance(currentScreenCenter, screenCenter) > 1f)
        {
            Debug.Log($"[VirtualCursor] Screen size changed, recalculating center");
            UpdateScreenCenter();
            virtualCursorPos = screenCenter;
        }

        HandleApplicationFocus();
        HandleMouseInput();
        UpdateUIPosition();
        SendInputToCamera();
    }

    void HandleMouseInput()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        Vector2 adjustedDelta = mouseDelta * sensitivity;

        bool hasMouseInput = adjustedDelta.magnitude > inputThreshold;
        hasActiveMouseInput = hasMouseInput;

        // 1. 마우스 입력 적용
        if (hasMouseInput)
        {
            virtualCursorPos += adjustedDelta;
        }

        // 2. 항상 중앙으로 약하게 복귀 (마우스 입력과 독립적)
        float maxDistance = Mathf.Max(Screen.width, Screen.height) * 0.5f;
        float returnSpeed = maxDistance / centerReturnTime; // 픽셀/초
        virtualCursorPos = Vector2.MoveTowards(virtualCursorPos, screenCenter, returnSpeed * Time.deltaTime);

        // 중앙에 충분히 가까우면 스냅 (떨림 방지)
        if (Vector2.Distance(virtualCursorPos, screenCenter) < 1f)
        {
            virtualCursorPos = screenCenter;
        }

        if (confineCursor)
        {
            virtualCursorPos.x = Mathf.Clamp(virtualCursorPos.x, screenMargin.x, Screen.width - screenMargin.x);
            virtualCursorPos.y = Mathf.Clamp(virtualCursorPos.y, screenMargin.y, Screen.height - screenMargin.y);
        }

        if (lockRealCursor && Application.isFocused)
        {
            Vector2 screenCenterInt = new Vector2(Mathf.RoundToInt(screenCenter.x), Mathf.RoundToInt(screenCenter.y));
            Mouse.current.WarpCursorPosition(screenCenterInt);
        }
    }

    void UpdateUIPosition()
    {
        if (virtualCursorUI != null && canvas != null)
        {
            Vector2 canvasPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                virtualCursorPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out canvasPos
            );
            virtualCursorUI.anchoredPosition = canvasPos;
        }
    }

    void SendInputToCamera()
    {
        if (cameraController == null) return;

        Vector2 offset = virtualCursorPos - screenCenter;
        Vector2 normalizedInput = new Vector2(
            offset.x / (Screen.width * 0.5f),
            offset.y / (Screen.height * 0.5f)
        );

        normalizedInput = Vector2.ClampMagnitude(normalizedInput, 1f);

        float pitch = -normalizedInput.y;
        float roll = normalizedInput.x;

        float deadZone = 0.03f;
        if (Mathf.Abs(pitch) < deadZone) pitch = 0f;
        if (Mathf.Abs(roll) < deadZone) roll = 0f;

        cameraController.SetPitchAndRollInput(pitch, roll);
    }

    // 카메라 컨트롤러 설정
    public void SetCameraController(CameraController camera)
    {
        cameraController = camera;
    }

    // 유틸리티 메서드들
    public Vector2 GetVirtualCursorPosition() => virtualCursorPos;
    public Vector2 GetNormalizedInput()
    {
        Vector2 offset = virtualCursorPos - screenCenter;
        return new Vector2(
            offset.x / (Screen.width * 0.5f),
            offset.y / (Screen.height * 0.5f)
        );
    }
    public bool HasActiveMouseInput() => hasActiveMouseInput;

    // 커서가 중앙 근처에 있는지 확인 (수평 복귀 판단용)
    public bool IsCursorNearCenter(float threshold = 0.05f)
    {
        Vector2 normalizedOffset = GetNormalizedInput();
        return normalizedOffset.magnitude < threshold;
    }

    public void ResetCursorToCenter()
    {
        virtualCursorPos = screenCenter;
        hasActiveMouseInput = false;
    }


    void SetupRealCursor()
    {
        if (lockRealCursor)
        {
            Cursor.lockState = CursorLockMode.Confined;
            Vector2 screenCenterInt = new Vector2(Mathf.RoundToInt(screenCenter.x), Mathf.RoundToInt(screenCenter.y));
            Mouse.current.WarpCursorPosition(screenCenterInt);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        Cursor.visible = !hideRealCursor;
    }

    void HandleApplicationFocus()
    {
        if (Application.isFocused && !wasApplicationFocused)
        {
            SetupRealCursor();
        }
        wasApplicationFocused = Application.isFocused;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && lockRealCursor)
        {
            SetupRealCursor();
        }
    }

    void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying && Camera.main != null && isInitialized)
        {
            Vector3 worldCenter = Camera.main.ScreenToWorldPoint(new Vector3(screenCenter.x, screenCenter.y, 10f));
            Vector3 worldCursor = Camera.main.ScreenToWorldPoint(new Vector3(virtualCursorPos.x, virtualCursorPos.y, 10f));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(worldCenter, 1f);

            Gizmos.color = hasActiveMouseInput ? Color.blue : Color.green;
            Gizmos.DrawSphere(worldCursor, 0.5f);
            Gizmos.DrawLine(worldCenter, worldCursor);

            if (confineCursor)
            {
                Gizmos.color = Color.cyan;
                Vector3 topLeft = Camera.main.ScreenToWorldPoint(new Vector3(screenMargin.x, Screen.height - screenMargin.y, 10f));
                Vector3 topRight = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width - screenMargin.x, Screen.height - screenMargin.y, 10f));
                Vector3 bottomLeft = Camera.main.ScreenToWorldPoint(new Vector3(screenMargin.x, screenMargin.y, 10f));
                Vector3 bottomRight = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width - screenMargin.x, screenMargin.y, 10f));

                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(topRight, bottomRight);
                Gizmos.DrawLine(bottomRight, bottomLeft);
                Gizmos.DrawLine(bottomLeft, topLeft);
            }
        }
    }
}