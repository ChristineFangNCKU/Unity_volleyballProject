using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    [Header("控制目標")]
    [Tooltip("攝影機圍繞旋轉的中心點")]
    public Transform target; // 攝影機的目標，即球場中心

    [Header("軌道運鏡設定")]
    [Tooltip("滑鼠/觸控板旋轉的靈敏度")]
    public float rotationSpeed = 2.0f;
    [Tooltip("滾輪/觸控板縮放的靈敏度")]
    public float zoomSpeed = 2.0f;
    [Tooltip("鏡頭與目標的最小距離")]
    public float minDistance = 5.0f;
    [Tooltip("鏡頭與目標的最大距離")]
    public float maxDistance = 30.0f;

    [Header("鍵盤與觸控板支援")]
    [Tooltip("按住此鍵搭配觸控板來旋轉視角 (Mac)")]
    public KeyCode trackpadRotateKey = KeyCode.LeftAlt;
    [Tooltip("使用鍵盤平移視角的移動速度")]
    public float panSpeed = 10.0f;

    [Header("預設視角")]
    [Tooltip("預設的起始視角，也是按鍵 1 的視角")]
    public Vector3 defaultPosition = new Vector3(4.5f, 8f, -5f);
    public Vector3 defaultRotation = new Vector3(30f, 0f, 0f);

    [Header("UI 提示設定")]
    [Tooltip("顯示操作說明的 UI Panel 物件")]
    public GameObject controlsUIPanel;
    [Tooltip("提示在開始時顯示的時長（秒）")]
    public float displayDuration = 6f;
    [Tooltip("用來開關提示的按鍵")]
    public KeyCode toggleHelpKey = KeyCode.H;

    // 私有變數
    private float distance;
    private float rotationYAxis = 0.0f;
    private float rotationXAxis = 0.0f;
    private Dictionary<int, (Vector3 pos, Vector3 rot)> presetViews;
    private CanvasGroup controlsCanvasGroup;

    void Start()
    {
        InitializePresetViews();
        transform.position = defaultPosition;
        transform.eulerAngles = defaultRotation;

        Vector3 angles = transform.eulerAngles;
        rotationYAxis = angles.y;
        rotationXAxis = angles.x;

        if (target)
        {
            distance = Vector3.Distance(transform.position, target.position);
        }

        // --- UI 初始化 ---
        if (controlsUIPanel != null)
        {
            // 獲取或添加 CanvasGroup 以控制淡入淡出
            controlsCanvasGroup = controlsUIPanel.GetComponent<CanvasGroup>();
            if (controlsCanvasGroup == null)
            {
                controlsCanvasGroup = controlsUIPanel.AddComponent<CanvasGroup>();
            }
            StartCoroutine(ShowAndFadeControls());
        }
    }

    void Update()
    {
        // 將所有輸入相關的邏輯放在 Update 中
        if (target == null) return;
        
        HandlePresetViewInput();
        HandleKeyboardPanAndZoom();
        HandleMouseAndTrackpadInput();
        HandleHelpUIToggle();
    }

    void LateUpdate()
    {
        // LateUpdate 中只執行攝影機的最終移動，確保跟得上所有物件
        if (target == null) return;
        UpdateCameraTransform();
    }

    void InitializePresetViews()
    {
        presetViews = new Dictionary<int, (Vector3, Vector3)>
        {
            { 1, (defaultPosition, defaultRotation) },
            { 2, (new Vector3(4.5f, 15f, 9f), new Vector3(90f, 0f, 0f)) },
            { 3, (new Vector3(4.5f, 2f, -2f), new Vector3(10f, 0f, 0f)) },
            { 4, (new Vector3(4.5f, 2f, 20f), new Vector3(10f, 180f, 0f)) },
            { 5, (new Vector3(-3f, 10f, 3f), new Vector3(40f, 45f, 0f)) }
        };
    }

    void HandlePresetViewInput()
    {
        for (int i = 1; i <= 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                if (presetViews.ContainsKey(i))
                {
                    StopAllCoroutines();
                    StartCoroutine(MoveToPresetView(presetViews[i]));
                }
            }
        }
    }

    IEnumerator MoveToPresetView((Vector3 pos, Vector3 rot) view)
    {
        Quaternion targetRotation = Quaternion.Euler(view.rot);
        float duration = 0.5f;
        float elapsedTime = 0f;
        Vector3 startingPos = transform.position;
        Quaternion startingRot = transform.rotation;

        while (elapsedTime < duration)
        {
            transform.position = Vector3.Lerp(startingPos, view.pos, elapsedTime / duration);
            transform.rotation = Quaternion.Slerp(startingRot, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = view.pos;
        transform.rotation = targetRotation;
        
        if (target) { distance = Vector3.Distance(transform.position, target.position); }
        rotationYAxis = transform.eulerAngles.y;
        rotationXAxis = transform.eulerAngles.x;
    }

    void HandleKeyboardPanAndZoom()
    {
        Vector3 panMovement = Vector3.zero;
        if (Input.GetKey(KeyCode.UpArrow)) { panMovement += transform.forward; }
        if (Input.GetKey(KeyCode.DownArrow)) { panMovement += -transform.forward; }
        if (Input.GetKey(KeyCode.LeftArrow)) { panMovement += -transform.right; }
        if (Input.GetKey(KeyCode.RightArrow)) { panMovement += transform.right; }
        panMovement.y = 0; // 確保平移是在水平面上
        
        if (panMovement != Vector3.zero)
        {
            target.Translate(panMovement.normalized * panSpeed * Time.deltaTime, Space.World);
        }

        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
        {
            distance = Mathf.Clamp(distance - zoomSpeed * Time.deltaTime * 5f, minDistance, maxDistance);
        }
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
        {
            distance = Mathf.Clamp(distance + zoomSpeed * Time.deltaTime * 5f, minDistance, maxDistance);
        }
    }

    void HandleMouseAndTrackpadInput()
    {
        if (Input.GetMouseButton(1) || Input.GetKey(trackpadRotateKey))
        {
            rotationYAxis += Input.GetAxis("Mouse X") * rotationSpeed;
            rotationXAxis -= Input.GetAxis("Mouse Y") * rotationSpeed;
            rotationXAxis = Mathf.Clamp(rotationXAxis, 0, 90);
        }
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minDistance, maxDistance);
    }

    /// <summary>
    /// 【新增】處理幫助介面的開關邏輯
    /// </summary>
    void HandleHelpUIToggle()
    {
        if (controlsUIPanel != null && Input.GetKeyDown(toggleHelpKey))
        {
            StopAllCoroutines(); // 停止所有正在進行的移動或淡出動畫
            
            bool isVisible = controlsCanvasGroup.alpha > 0;
            if (isVisible)
            {
                controlsCanvasGroup.alpha = 0;
                controlsUIPanel.SetActive(false);
            }
            else
            {
                controlsCanvasGroup.alpha = 1;
                controlsUIPanel.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 【新增】在遊戲開始時顯示並淡出操作提示
    /// </summary>
    IEnumerator ShowAndFadeControls()
    {
        controlsUIPanel.SetActive(true);
        controlsCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        float fadeDuration = 1.0f;
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            controlsCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        controlsCanvasGroup.alpha = 0f;
        controlsUIPanel.SetActive(false);
    }

    void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(rotationXAxis, rotationYAxis, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }
}

