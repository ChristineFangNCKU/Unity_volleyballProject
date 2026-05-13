using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; 
using Newtonsoft.Json; 
using System;
using System.Linq;

public class VolleyballController : MonoBehaviour
{
    [Header("球體設定")]
    [Tooltip("排球的直徑（單位：公尺）")]
    public float ballDiameter = 0.21f; 

    [Header("同步設定 (核心)")]
    [Tooltip("拖入場景上的 PlayersManager，排球將完全與其時間軸同步")]
    public PlayersManager masterTimeline;
    public string ballJsonFileName = "ball_data.json";
    
    [Header("除錯信息")]
    public int currentFrameId = 0;
    public bool hasValidData = false;
    public int totalFrames = 0;
    public int actualStartFrame = 0;
    public int actualEndFrame = 0;
    
    // 儲存幀數據的字典
    private Dictionary<int, BallFrameData> frameDataDict = new Dictionary<int, BallFrameData>();
    private MeshRenderer ballRenderer; // 控制排球隱形/現形

    void Start()
    {
        transform.localScale = new Vector3(ballDiameter, ballDiameter, ballDiameter);
        ballRenderer = GetComponentInChildren<MeshRenderer>();

        if (masterTimeline == null)
        {
            Debug.LogError("🚨 VolleyballController 缺少 masterTimeline！請在 Inspector 中把 PlayersManager 拖進來！");
        }

        // 載入球的數據
        LoadBallData();
    }

    void Update()
    {
        // 如果沒有同步對象，或者中央時鐘沒在播放，就不動作
        if (masterTimeline == null || !masterTimeline.IsPlaying || !hasValidData) return;

        // 【核心邏輯】直接讀取 PlayersManager 的當前幀數！完美同步！
        currentFrameId = masterTimeline.currentFrameId;
        
        // 去字典裡面找這一幀有沒有排球數據
        if (frameDataDict.TryGetValue(currentFrameId, out BallFrameData currentFrameData))
        {
            // 有數據：更新位置，並讓排球現形
            transform.position = currentFrameData.position;
            if (ballRenderer != null) ballRenderer.enabled = true;
        }
        else
        {
            // 沒數據：直接讓排球隱形 (解決時間段不一致的問題)
            if (ballRenderer != null) ballRenderer.enabled = false;
        }
    }

    void LoadBallData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Matches", ballJsonFileName);
        if (File.Exists(filePath))
        {
            string jsonData = File.ReadAllText(filePath);
            ParseJsonData(jsonData);
        }
        else
        {
            Debug.LogError("JSON 檔案不存在: " + filePath);
        }
    }

    void ParseJsonData(string jsonData)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<BallData>(jsonData);
            if (data != null && data.ball_data != null)
            {
                int validDataCount = 0;
                foreach (var entry in data.ball_data)
                {
                    if (entry.pos3d == null || entry.pos3d.Count < 3) continue;

                    double x = entry.pos3d[0];
                    double y = entry.pos3d[1]; 
                    double z = entry.pos3d[2];

                    if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ||
                        double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z)) continue;

                    Vector3 position = new Vector3((float)x, (float)z, (float)y);

                    frameDataDict[entry.frame_id] = new BallFrameData { frameId = entry.frame_id, position = position };
                    validDataCount++;
                }

                hasValidData = validDataCount > 0;
                totalFrames = validDataCount;
                
                if (hasValidData)
                {
                    actualStartFrame = frameDataDict.Keys.Min();
                    actualEndFrame = frameDataDict.Keys.Max();
                }
                Debug.Log($"🏐 成功載入 {validDataCount} 幀排球數據，實際範圍: {actualStartFrame}-{actualEndFrame}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析排球 JSON 時發生錯誤: {e.Message}");
        }
    }
}

[System.Serializable]
public class BallFrameData
{
    public int frameId;
    public Vector3 position;
}

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