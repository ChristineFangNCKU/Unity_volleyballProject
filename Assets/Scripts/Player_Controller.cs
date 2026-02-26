using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 【永久 ID 播放模式】掛載在每個專屬球員模型上。
/// 負責：1. 執行轉向和移動動畫。2. 處理隱形/現形 (MeshRenderer)。
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("模型設定")]
    [Tooltip("你的 3D 模型在 Unity 裡的原始預設身高 (用來當縮放分母)")]
    public float defaultModelHeight = 1.8f;
    [Tooltip("模型中心點(腰部)到臉部中心點的垂直高度差(公尺)")]
    public float pivotToHeadOffset = 0.8f;
    [Tooltip("自動算出的球員真實頭部高度 (由 PlayersManager 傳入)")]
    public float calculatedHeadHeight = 1.8f; 
    [Tooltip("模型轉向目標點的速度")]
    public float rotationSpeed = 10f;
    [Tooltip("模型移動到目標點的平滑速度")]
    public float moveSpeed = 15f; // 較快的Lerp速度確保能跟上數據

    // --- 私有變數 ---
    private Vector3 targetPosition;
    private bool hasTarget = false;
    private Animator animator; 
    private Vector3 lastPosition; 
    private MeshRenderer modelRenderer; // 【關鍵】模型的渲染器

    [Header("軌跡與散點視覺化")]
    public float lineThickness = 0.05f;
    public float pointSize = 0.03f;
    public bool showPointCloud = true; // 可從 Inspector 開關散點
    [Tooltip("軌跡與散點貼近地板的偏移量（公尺），預設 0.02m = 2cm）")]
    public float trajectoryFloorOffset = 0.02f;

    private LineRenderer lineRenderer;
    private GameObject pointCloudParent;
    private Color playerColor;
    private bool isHighlighted = false;
    private Material trajectoryMaterial;
    private GameObject pointPrefab;

    void Start()
    {
        targetPosition = transform.position;
        lastPosition = transform.position;
        hasTarget = false;

        // 獲取 MeshRenderer 和 Animator
        modelRenderer = GetComponentInChildren<MeshRenderer>();
        animator = GetComponentInChildren<Animator>();
        
        // 初始狀態設為隱形 (等待數據命令)
        if (modelRenderer != null)
        {
            modelRenderer.enabled = false; 
        }

        if (animator == null)
        {
            // 警告：您的模型需要有 Animator 元件才能播放動畫
            // Debug.LogWarning("在 " + gameObject.name + " 上找不到 Animator 元件，將無法播放動畫。"); 
        }
    }

    void Update()
    {
        // --- 1. 位置與方向更新 ---
        if (hasTarget)
        {
            // B. 位置 (Position): 平滑移動 (先處理移動)
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);

            // A. 方向 (Orientation): 永遠面朝球網 (Z = 9.0f)
            float netZPosition = 9.0f;
            Vector3 targetDirection;

            // 判斷球員在哪個半場
            if (transform.position.z < netZPosition)
            {
                // 在 Z < 9 的半場，面向正前方 (0, 0, 1)
                targetDirection = Vector3.forward; 
            }
            else
            {
                // 在 Z >= 9 的半場，面向正後方 (0, 0, -1)
                targetDirection = Vector3.back; 
            }

            // 平滑轉身面朝球網
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // --- 2. 動畫控制 ---
        Vector3 velocity = (transform.position - lastPosition) / Time.deltaTime;
        float speed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        if (animator != null)
        {
            animator.SetFloat("Speed", speed);
        }

        lastPosition = transform.position;
    }

    /// <summary>
    /// 由 PlayersManager 呼叫，設定位置並讓模型【現形】。
    /// </summary>
    public void SetTargetPosition(Vector3 newPosition)
    {
        // 進行垂直位移修正：使用動態計算出的頭部高度
        targetPosition = new Vector3(
            newPosition.x,
            newPosition.y - calculatedHeadHeight, 
            newPosition.z
        );
        
        hasTarget = true;
        // 讓模型現形 (憑空出現)
        if (modelRenderer != null)
        {
            modelRenderer.enabled = true;
        }
    }

    /// <summary>
    /// 由 PlayersManager 呼叫，當數據間隙或 track_id 消失時，讓模型【隱形】。
    /// </summary>
    public void SetNoTarget()
    {
        // 速度 Lerp 會自動停止，並觸發 Idle 動畫
        hasTarget = false; 
        // 讓模型隱形 (憑空消失)
        if (modelRenderer != null)
        {
            modelRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 由 PlayersManager 在 Instantiate 後呼叫，傳入完整歷史軌跡以及繪製用的材質與散點 Prefab
    /// </summary>
    public void InitializeTrajectory(List<Vector3> fullPath, Material defaultMaterial, GameObject pointPrefab, float headHeight)
    {
        if (fullPath == null || fullPath.Count == 0) return;

        // 【新增】接收直方圖算出來的身高
        this.calculatedHeadHeight = headHeight;
        // --- 【新增】依照真實身高縮放 3D 模型 ---
        // 計算比例 = 真實身高 / 模型預設身高
        float scaleRatio = calculatedHeadHeight / defaultModelHeight;
        
        // 使用等比例縮放 (X, Y, Z 都縮放)，這樣球員才不會變成竹竿人或被壓扁
        transform.localScale = new Vector3(scaleRatio, scaleRatio, scaleRatio);
        
        // 印出 Log 讓你確認每個球員被放大了多少
        Debug.Log($"{gameObject.name} 真實身高: {calculatedHeadHeight:F2}m, 縮放比例: {scaleRatio:F2}");
        // ----------------------------------------
        this.pointPrefab = pointPrefab;
        trajectoryMaterial = defaultMaterial;

        // 隨機生成一個較亮的顏色
        playerColor = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);

        // 設定 LineRenderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        if (trajectoryMaterial != null)
        {
            lineRenderer.material = trajectoryMaterial;
        }
        lineRenderer.startWidth = lineThickness;
        lineRenderer.endWidth = lineThickness;
        lineRenderer.positionCount = fullPath.Count;
        lineRenderer.useWorldSpace = true;

        // 【修正】強制將軌跡線貼在地板表面上方 (使用 Inspector 可調整的偏移量)
        for (int i = 0; i < fullPath.Count; i++)
        {
            Vector3 floorPos = new Vector3(fullPath[i].x, trajectoryFloorOffset, fullPath[i].z);
            lineRenderer.SetPosition(i, floorPos);
        }

        // 生成散點
        pointCloudParent = new GameObject("PointCloud");
        pointCloudParent.transform.SetParent(this.transform);
        pointCloudParent.transform.localPosition = Vector3.zero;

        if (pointPrefab != null)
        {
            foreach (Vector3 pos in fullPath)
            {
                // 【修正】散點也強制貼在地板表面上方 (使用 Inspector 可調整的偏移量)
                Vector3 floorPos = new Vector3(pos.x, trajectoryFloorOffset, pos.z);
                GameObject point = Instantiate(pointPrefab, floorPos, Quaternion.identity, pointCloudParent.transform);
                point.transform.localScale = Vector3.one * pointSize;

                Renderer pointRenderer = point.GetComponent<Renderer>();
                if (pointRenderer != null && trajectoryMaterial != null)
                {
                    pointRenderer.material = trajectoryMaterial;
                    Color c = new Color(playerColor.r, playerColor.g, playerColor.b, 0.3f);
                    pointRenderer.material.color = c;
                }
            }
        }

        // 初始為半透明
        UpdateTrajectoryAppearance();
    }

    void LateUpdate()
    {
        if (pointCloudParent != null && pointCloudParent.activeSelf != showPointCloud)
        {
            pointCloudParent.SetActive(showPointCloud);
        }
    }

    void OnMouseDown()
    {
        isHighlighted = !isHighlighted;
        UpdateTrajectoryAppearance();
    }

    private void UpdateTrajectoryAppearance()
    {
        if (lineRenderer == null) return;

        float alpha = isHighlighted ? 1.0f : 0.2f;
        Color displayColor = new Color(playerColor.r, playerColor.g, playerColor.b, alpha);

        lineRenderer.startColor = displayColor;
        lineRenderer.endColor = displayColor;
        
        if (pointCloudParent != null)
        {
            foreach (Transform child in pointCloudParent.transform)
            {
                var r = child.GetComponent<Renderer>();
                if (r != null)
                {
                    Color c = new Color(playerColor.r, playerColor.g, playerColor.b, isHighlighted ? 1f : 0.3f);
                    r.material.color = c;
                }
            }
        }

        // sortingOrder 只有在有合適材質或 Renderer 時才有效
        // lineRenderer.sortingOrder requires a Sprite/Particle shader to be visible in 2D sorting layers; keep it for possible use
        lineRenderer.sortingOrder = isHighlighted ? 10 : 0;
    }
}