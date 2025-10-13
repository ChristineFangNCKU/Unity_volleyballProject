using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; // 用於讀取文件
using Newtonsoft.Json; // 使用 JSON 解析

public class VolleyballController : MonoBehaviour
{
    public List<Vector3> targetPositions = new List<Vector3>(); // 儲存座標點
    public float moveSpeed = 5f; // 速度（會根據 deltaTime 調整）
    private int currentTargetIndex = 0; // 當前目標座標的索引

    private const float deltaTime = 1f / 60f; // 設定為 60 FPS，即 0.0167 秒

    void Start()
    {
        // 讀取 JSON 文件（從 StreamingAssets 目錄）
        string filePath = Path.Combine(Application.streamingAssetsPath, "ball_data.json");
        if (File.Exists(filePath))
        {
            string jsonData = File.ReadAllText(filePath);
            ParseJsonData(jsonData);
        }
        else
        {
            Debug.LogError("JSON 文件不存在: " + filePath);
        }

        // 開始移動
        if (targetPositions.Count > 0)
        {
            StartCoroutine(MoveBall());
        }
        else
        {
            Debug.LogError("沒有從 JSON 文件中讀取到任何座標");
        }
    }

    void ParseJsonData(string jsonData)
    {
        var data = JsonConvert.DeserializeObject<BallData>(jsonData);
        if (data != null && data.ball_data != null)
        {
            foreach (var entry in data.ball_data)
            {
                Vector3 position = new Vector3(
                    (float)entry.pos3d[0] * 10, // 轉換座標比例
                    (float)entry.pos3d[2] * 10,
                    (float)entry.pos3d[1] * 10
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
        while (currentTargetIndex < targetPositions.Count - 1)
        {
            Vector3 startPosition = transform.position;
            Vector3 endPosition = targetPositions[currentTargetIndex + 1];

            float elapsedTime = 0f;
            float totalTime = deltaTime; // 確保每幀的移動符合 60 FPS

            while (elapsedTime < totalTime)
            {
                transform.position = Vector3.Lerp(startPosition, endPosition, elapsedTime / totalTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            transform.position = endPosition; // 確保位置完全對齊
            currentTargetIndex++;
        }
    }
}

[System.Serializable]
public class BallDataEntry
{
    public int frame_id; // 畫面 ID
    public List<double> pos3d; // 3D 座標
}

[System.Serializable]
public class BallData
{
    public List<BallDataEntry> ball_data; // 所有球數據
}