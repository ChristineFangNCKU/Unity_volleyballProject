using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; // 用於讀取文件
using Newtonsoft.Json; // 使用 JSON 解析

public class VolleyballController : MonoBehaviour
{
    [Header("球體設定")]
    [Tooltip("排球的直徑（單位：公尺）")]
    public float ballDiameter = 0.21f; // 標準排球直徑約為 21 公分

    [Header("數據路徑")]
    public List<Vector3> targetPositions = new List<Vector3>(); // 儲存座標點
    
    private int currentTargetIndex = 0; // 當前目標座標的索引
    private const float dataDeltaTime = 1f / 60f; // 數據的固定時間間隔，代表原始數據是 60 FPS

    void Start()
    {
        // **【新增】** 根據設定的直徑調整球體大小
        transform.localScale = new Vector3(ballDiameter, ballDiameter, ballDiameter);

        // 確保球體在開始時位於第一個座標點
        LoadAndPrepareBallData();
    }

    void LoadAndPrepareBallData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "ball_data.json");
        if (File.Exists(filePath))
        {
            string jsonData = File.ReadAllText(filePath);
            ParseJsonData(jsonData);
        }
        else
        {
            Debug.LogError("JSON 檔案不存在: " + filePath);
        }

        // 讀取完畢後，開始移動協程
        if (targetPositions.Count > 0)
        {
            // 將球的初始位置設定為第一個數據點
            transform.position = targetPositions[0];
            StartCoroutine(MoveBall());
        }
        else
        {
            Debug.LogError("沒有從 JSON 檔案中讀取到任何座標");
        }
    }

    void ParseJsonData(string jsonData)
    {
        var data = JsonConvert.DeserializeObject<BallData>(jsonData);
        if (data != null && data.ball_data != null)
        {
            foreach (var entry in data.ball_data)
            {
                // **【修正 1】** 移除 `* 10` 的座標放大，現在是 1:1 的真實比例
                // 同時保留 Y-Z 軸的交換，以符合 Unity 的座標系 (Y 軸向上)
                Vector3 position = new Vector3(
                    (float)entry.pos3d[0], 
                    (float)entry.pos3d[2], // 數據中的 Z (高度) 對應 Unity 的 Y
                    (float)entry.pos3d[1]  // 數據中的 Y (長度) 對應 Unity 的 Z
                );
                targetPositions.Add(position);
            }
        }
        else
        {
            Debug.LogError("無法解析 JSON 文件");
        }
    }

    IEnumerator MoveBall()
    {
        // 從第 0 個點移動到第 1 個點開始
        while (currentTargetIndex < targetPositions.Count - 1)
        {
            Vector3 startPosition = targetPositions[currentTargetIndex];
            Vector3 endPosition = targetPositions[currentTargetIndex + 1];

            float elapsedTime = 0f;
            
            // 在一個數據幀的時間內 (1/60秒)，平滑地從起點移動到終點
            while (elapsedTime < dataDeltaTime)
            {
                // Time.deltaTime 是遊戲實際的幀時間
                transform.position = Vector3.Lerp(startPosition, endPosition, elapsedTime / dataDeltaTime);
                elapsedTime += Time.deltaTime; 
                yield return null; // 等待下一遊戲幀
            }

            // 為了確保精確，在循環結束時強制將位置設定為終點
            transform.position = endPosition; 
            currentTargetIndex++;
        }
        Debug.Log("球體路徑播放完畢。");
    }
}

// --- 以下的數據結構定義不需要修改 ---

[System.Serializable]
public class BallDataEntry
{
    public int frame_id;
    public List<double> pos3d;
}

[System.Serializable]
public class BallData
{
    public List<BallDataEntry> ball_data;
}

