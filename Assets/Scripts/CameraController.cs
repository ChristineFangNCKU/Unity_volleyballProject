using System.Collections; // 【已新增】引入此命名空間以使用 Coroutine
using UnityEngine;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
// ... a belső kód többi része változatlan ...
// ... the rest of the internal code is unchanged ...
// ... 其餘內部程式碼保持不變 ...
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

    // 私有變數
    private float distance;
    private float rotationYAxis = 0.0f;
    private float rotationXAxis = 0.0f;

    // 預設視角列表
    private Dictionary<int, (Vector3 pos, Vector3 rot)> presetViews;

    void Start()
    {
        // 初始化預設視角
        InitializePresetViews();

        // 設定初始位置和旋轉
        transform.position = defaultPosition;
        transform.eulerAngles = defaultRotation;

        Vector3 angles = transform.eulerAngles;
        rotationYAxis = angles.y;
        rotationXAxis = angles.x;

        if (target)
        {
            distance = Vector3.Distance(transform.position, target.position);
        }
    }

    void LateUpdate()
    {
        // LateUpdate確保在所有物件移動完畢後才更新攝影機
        if (target == null) return;

        // 處理鍵盤輸入
        HandlePresetViewInput(); // 切換預設視角 (1,2,3,4)
        HandleKeyboardPanAndZoom(); // 平移與縮放 (+,-,方向鍵)

        // 處理滑鼠/觸控板輸入
        HandleMouseAndTrackpadInput();

        // 最後根據所有輸入更新攝影機位置和旋轉
        UpdateCameraTransform();
    }

    /// <summary>
    /// 初始化所有預設的攝影機視角
    /// </summary>
    void InitializePresetViews()
    {
        presetViews = new Dictionary<int, (Vector3, Vector3)>
        {
            { 1, (defaultPosition, defaultRotation) },
            { 2, (new Vector3(4.5f, 15f, 9f), new Vector3(90f, 0f, 0f)) },
            { 3, (new Vector3(4.5f, 2f, -2f), new Vector3(10f, 0f, 0f)) },
            { 4, (new Vector3(4.5f, 2f, 20f), new Vector3(10f, 180f, 0f)) },
            // 5: 【新增】斜側上方視角
            { 5, (new Vector3(13f, 5f, 5f), new Vector3(27f, -70f, 0f)) }
        };
    }

    /// <summary>
    /// 處理切換預設視角的鍵盤輸入
    /// </summary>
    void HandlePresetViewInput()
    {
        for (int i = 1; i <= 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                if (presetViews.ContainsKey(i))
                {
                    StopAllCoroutines(); // 停止所有正在進行的移動
                    StartCoroutine(MoveToPresetView(presetViews[i]));
                }
            }
        }
    }

    /// <summary>
    /// 平滑移動到指定的預設視角
    /// </summary>
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
        
        // 更新軌道運鏡的參數以匹配新視角
        if (target) { distance = Vector3.Distance(transform.position, target.position); }
        rotationYAxis = transform.eulerAngles.y;
        rotationXAxis = transform.eulerAngles.x;
    }

    /// <summary>
    /// 【新增】處理鍵盤平移與縮放
    /// </summary>
    void HandleKeyboardPanAndZoom()
    {
        // 使用方向鍵平移目標點
        Vector3 panMovement = Vector3.zero;
        if (Input.GetKey(KeyCode.UpArrow)) { panMovement += Vector3.forward; }
        if (Input.GetKey(KeyCode.DownArrow)) { panMovement += Vector3.back; }
        if (Input.GetKey(KeyCode.LeftArrow)) { panMovement += Vector3.left; }
        if (Input.GetKey(KeyCode.RightArrow)) { panMovement += Vector3.right; }
        
        if (panMovement != Vector3.zero)
        {
            target.Translate(panMovement.normalized * panSpeed * Time.deltaTime, Space.Self);
        }

        // 使用 +/- 鍵縮放
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) // Equals 是 + 鍵
        {
            distance = Mathf.Clamp(distance - zoomSpeed * Time.deltaTime * 5f, minDistance, maxDistance);
        }
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
        {
            distance = Mathf.Clamp(distance + zoomSpeed * Time.deltaTime * 5f, minDistance, maxDistance);
        }
    }

    /// <summary>
    /// 【已更新】處理自由運鏡的滑鼠與觸控板輸入
    /// </summary>
    void HandleMouseAndTrackpadInput()
    {
        // 條件：按住右鍵 或 按住指定的觸控板輔助鍵
        if (Input.GetMouseButton(1) || Input.GetKey(trackpadRotateKey))
        {
            rotationYAxis += Input.GetAxis("Mouse X") * rotationSpeed;
            rotationXAxis -= Input.GetAxis("Mouse Y") * rotationSpeed;
            rotationXAxis = Mathf.Clamp(rotationXAxis, 0, 90); // 限制垂直角度，避免翻轉
        }

        // 滾輪 或 觸控板雙指捏合/張開手勢
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minDistance, maxDistance);
    }

    /// <summary>
    /// 根據最終參數更新攝影機的 Transform
    /// </summary>
    void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(rotationXAxis, rotationYAxis, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }
}


