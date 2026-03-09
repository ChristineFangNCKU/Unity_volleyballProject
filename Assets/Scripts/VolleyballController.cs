using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO; // 用於讀取文件
using Newtonsoft.Json; // 使用 JSON 解析
using System;
using System.Linq;

public class VolleyballController : MonoBehaviour
{
    [Header("球體設定")]
    [Tooltip("排球的直徑（單位：公尺）")]
    public float ballDiameter = 0.21f; // 標準排球直徑約為 21 公分

    [Header("播放設定")]
    [Tooltip("數據幀率（fps）")]
    public float frameRate = 60f;
    [Tooltip("開始播放的幀ID")]
    public int startFrameId = 111;
    [Tooltip("結束播放的幀ID（0表示播放到最後）")]
    public int endFrameId = 0;
    [Tooltip("是否自動開始播放")]
    public bool autoPlay = true;
    public string ballJsonFileName = "ball_data.json";
    
    [Header("除錯信息")]
    public int currentFrameId = 0;
    public bool hasValidData = false;
    [Tooltip("載入的總幀數")]
    public int totalFrames = 0;
    [Tooltip("實際的開始和結束幀ID")]
    public int actualStartFrame = 0;
    public int actualEndFrame = 0;
    
    [Header("路徑數據預覽")]
    [Tooltip("顯示完整的路徑數據（僅供檢視）")]
    public List<FrameDataDisplay> pathDataPreview = new List<FrameDataDisplay>();
    
    // **【升級】** 使用字典存儲幀數據，支持快速查詢
    private Dictionary<int, BallFrameData> frameDataDict = new Dictionary<int, BallFrameData>();
    private BallFrameData currentFrameData;
    private BallFrameData lastValidFrameData;
    
    // 時間追蹤
    private float elapsedTime = 0f;
    private bool isPlaying = false;
    private int targetFrameId = 0;

    void Start()
    {
        // **【新增】** 根據設定的直徑調整球體大小
        transform.localScale = new Vector3(ballDiameter, ballDiameter, ballDiameter);

        // 載入球的數據
        LoadBallData();
        
        // 如果設定為自動播放，開始播放
        if (autoPlay && hasValidData)
        {
            StartPlayback();
        }
    }

    void Update()
    {
        if (!isPlaying || !hasValidData) return;

        // **【核心邏輯】** 真實時間同步
        elapsedTime += Time.deltaTime;
        
        // 根據總經過時間計算目標幀ID
        targetFrameId = (int)(elapsedTime * frameRate) + startFrameId;
        
        // **【新增】** 檢查是否達到結束幀ID
        if (endFrameId > 0 && targetFrameId > endFrameId)
        {
            Debug.Log($"播放完畢，已達到結束幀ID: {endFrameId}");
            StopPlayback();
            return;
        }
        
        // 嘗試獲取目標幀的數據
        if (frameDataDict.TryGetValue(targetFrameId, out currentFrameData))
        {
            // 找到數據，更新球的位置
            UpdateBallPosition(currentFrameData);
            lastValidFrameData = currentFrameData;
            currentFrameId = targetFrameId;
        }
        else if (lastValidFrameData != null)
        {
            // 數據間隙：保持在最後一個有效位置（模擬真實影片的靜止畫面）
            // 不需要做任何事，球會自然停留在上一幀的位置
            currentFrameId = targetFrameId; // 更新當前幀ID用於除錯顯示
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
                    // **【新增】** 檢查數據完整性
                    if (entry.pos3d == null || entry.pos3d.Count < 3)
                    {
                        Debug.LogWarning($"Frame {entry.frame_id}: pos3d 數據不完整，跳過此幀");
                        continue;
                    }

                    // **【新增】** 檢查座標值是否為有效數值
                    double x = entry.pos3d[0];
                    double y = entry.pos3d[1]; 
                    double z = entry.pos3d[2];

                    if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) ||
                        double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z))
                    {
                        Debug.LogWarning($"Frame {entry.frame_id}: 座標包含無效值 (NaN/Infinity)，跳過此幀");
                        continue;
                    }

                    // **【升級】** 建立 BallFrameData 並存入字典
                    Vector3 position = new Vector3(
                        (float)x, 
                        (float)z, // 數據中的 Z (高度) 對應 Unity 的 Y
                        (float)y  // 數據中的 Y (長度) 對應 Unity 的 Z
                    );

                    BallFrameData frameData = new BallFrameData
                    {
                        frameId = entry.frame_id,
                        position = position
                    };

                    // **【核心】** 使用 frame_id 作為 Key 存入字典
                    frameDataDict[entry.frame_id] = frameData;
                    validDataCount++;
                }

                hasValidData = validDataCount > 0;
                totalFrames = validDataCount;
                
                // **【新增】** 更新實際開始和結束幀ID
                UpdateFrameRange();
                
                // **【新增】** 建立路徑數據預覽
                BuildPathDataPreview();
                
                Debug.Log($"成功載入 {validDataCount} 幀球體數據，幀ID範圍: {GetFrameRange()}");
            }
            else
            {
                Debug.LogError("無法解析 JSON 文件或 ball_data 為空");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析 JSON 時發生錯誤: {e.Message}");
        }
    }

    // **【新增】** 啟動播放
    public void StartPlayback()
    {
        if (!hasValidData)
        {
            Debug.LogError("沒有有效數據，無法開始播放");
            return;
        }

        elapsedTime = 0f;
        isPlaying = true;
        currentFrameId = startFrameId;
        
        // 嘗試設置初始位置
        if (frameDataDict.TryGetValue(startFrameId, out BallFrameData initialData))
        {
            UpdateBallPosition(initialData);
            lastValidFrameData = initialData;
        }
        
        Debug.Log($"開始播放球體動畫，起始幀ID: {startFrameId}");
    }

    // **【新增】** 停止播放
    public void StopPlayback()
    {
        isPlaying = false;
        Debug.Log("停止播放球體動畫");
    }

    // **【新增】** 更新球的位置
    void UpdateBallPosition(BallFrameData frameData)
    {
        transform.position = frameData.position;
    }

    // **【新增】** 更新實際幀範圍
    void UpdateFrameRange()
    {
        if (frameDataDict.Count == 0)
        {
            actualStartFrame = 0;
            actualEndFrame = 0;
            return;
        }
        
        actualStartFrame = int.MaxValue;
        actualEndFrame = int.MinValue;
        
        foreach (int frameId in frameDataDict.Keys)
        {
            if (frameId < actualStartFrame) actualStartFrame = frameId;
            if (frameId > actualEndFrame) actualEndFrame = frameId;
        }
        
        // 如果沒有設定結束幀ID，自動設為實際最後一幀
        if (endFrameId <= 0)
        {
            endFrameId = actualEndFrame;
        }
    }
    
    // **【新增】** 建立路徑數據預覽
    void BuildPathDataPreview()
    {
        pathDataPreview.Clear();
        
        // 按幀ID順序排序並建立預覽列表
        var sortedFrames = frameDataDict.Keys.ToList();
        sortedFrames.Sort();
        
        foreach (int frameId in sortedFrames)
        {
            var frameData = frameDataDict[frameId];
            pathDataPreview.Add(new FrameDataDisplay
            {
                frameId = frameId,
                position = frameData.position,
                x = frameData.position.x,
                y = frameData.position.y,
                z = frameData.position.z
            });
        }
        
        Debug.Log($"建立路徑預覽：共 {pathDataPreview.Count} 個數據點");
    }

    // **【新增】** 獲取幀範圍信息
    string GetFrameRange()
    {
        if (frameDataDict.Count == 0) return "無數據";
        
        return $"{actualStartFrame}-{actualEndFrame}";
    }

}

// **【新增】** 球體幀數據結構
[System.Serializable]
public class BallFrameData
{
    public int frameId;
    public Vector3 position;
}

// **【新增】** Inspector 顯示用的幀數據結構
[System.Serializable]
public class FrameDataDisplay
{
    [Tooltip("幀ID")]
    public int frameId;
    [Tooltip("3D位置")]
    public Vector3 position;
    [Tooltip("X座標")]
    public float x;
    [Tooltip("Y座標")]
    public float y;
    [Tooltip("Z座標")]
    public float z;
}

// --- JSON 數據結構定義 ---

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

