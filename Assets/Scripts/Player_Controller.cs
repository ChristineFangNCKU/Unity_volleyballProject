using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("模型設定")]
    [Tooltip("模型中心點(腰部)到臉部中心點的垂直高度差(公尺)")]
    public float pivotToHeadOffset = 0.8f;

    // 私有變數
    private Vector3 targetPosition;
    private bool hasTarget = false;

    void Start()
    {
        // 初始設定為沒有目標
        targetPosition = transform.position;
        hasTarget = false;
    }

    void Update()
    {
        // 如果有目標位置，就平滑地移動過去
        if (hasTarget)
        {
            // 使用 Lerp 讓移動更平滑，避免瞬移
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
        }
    }

    /// <summary>
    /// 由 PlayersManager 呼叫，用來更新這個球員這一幀應該要去的位置
    /// </summary>
    /// <param name="newPosition">從 JSON 讀取的原始臉部中心點座標</param>
    public void SetTargetPosition(Vector3 newPosition)
    {
        // **【核心修正 2】** 進行垂直位移修正
        // 將目標位置向下平移，讓模型的腰部對齊臉部數據點
        targetPosition = new Vector3(
            newPosition.x,
            newPosition.y - pivotToHeadOffset, // 向下修正
            newPosition.z
        );
        
        hasTarget = true;
    }

    /// <summary>
    /// 由 PlayersManager 呼叫，當這一幀沒有數據時，告知此球員
    /// </summary>
    public void SetNoTarget()
    {
        hasTarget = false;
        // 這裡可以加入待機動畫等邏輯
    }
}

