using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

/// <summary>
/// 【永久 ID 播放】模式的指揮官。
/// 每個 track_id 都會擁有一個專屬模型，用於視覺化追蹤系統的生命週期。
/// 使用真實時間同步，精確處理數據間隙問題。
/// </summary>
public class PlayersManager : MonoBehaviour
{
    [Header("播放設定")]
    [Tooltip("要讀取的 JSON 檔案名稱")]
    public string jsonFileName = "HDR80_A_LIVE_20230205_132958_000.mov_show_data.json";
    [Tooltip("數據的幀率（fps）")]
    public float frameRate = 60f;
    [Tooltip("開始播放的幀ID（0表示從最小幀開始）")]
    public int startFrameId = 0;
    [Tooltip("結束播放的幀ID（0表示播放到最後）")]
    public int endFrameId = 0;
    [Tooltip("是否自動開始播放")]
    public bool autoPlay = true;

    [Header("模型設定")]
    [Tooltip("用於複製生成的球員 Prefab (必須掛載 PlayerController)")]
    public GameObject playerPrefab;
    [Tooltip("用於整理生成的球員物件的父物件")]
    public Transform playerContainer;
    [Tooltip("用於繪製軌跡的材質 (建議使用 Sprites-Default)")]
    public Material lineMaterial;
    [Tooltip("用於散點的 Prefab (一個小球，需有 Renderer)")]
    public GameObject pointPrefab;

    [Header("除錯信息")]
    public int currentFrameId = 0;
    public bool hasValidData = false;
    public int totalFrames = 0;
    public int actualStartFrame = 0;
    public int actualEndFrame = 0;
    public int totalTrackIds = 0;

    // --- 核心數據結構 ---
    private Dictionary<int, FrameData> allFramesData; // Key: frame_id
    private Dictionary<int, PlayerController> permanentPlayers; // Key: track_id, Value: Model
    private Dictionary<int, Vector3> currentFramePlayerData;
    private Dictionary<int, Vector3> lastValidPlayerData;
    
    // 時間追蹤
    private float elapsedTime = 0f;
    private bool isPlaying = false;
    private int targetFrameId = 0;

    void Start()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayersManager] 錯誤：請在 Inspector 中拖入 Player Prefab！");
            return;
        }
        
        // 載入球員數據
        LoadAndParsePlayerData();
        
        if (hasValidData)
        {
            InstantiateAllPlayers();
            
            // 如果設定為自動播放，開始播放
            if (autoPlay)
            {
                StartPlayback();
            }
        }
    }

    void Update()
    {
        if (!isPlaying || !hasValidData || permanentPlayers == null) return;

        // **【核心邏輯】** 真實時間同步
        elapsedTime += Time.deltaTime;
        
        // 根據總經過時間計算目標幀ID
        targetFrameId = (int)(elapsedTime * frameRate) + startFrameId;
        
        // **【新增】** 檢查是否達到結束幀ID
        if (endFrameId > 0 && targetFrameId > endFrameId)
        {
            Debug.Log($"球員播放完畢，已達到結束幀ID: {endFrameId}");
            StopPlayback();
            return;
        }
        
        // 嘗試獲取目標幀的數據
        if (allFramesData.TryGetValue(targetFrameId, out var frameData))
        {
            // 找到數據，更新所有球員的位置
            UpdatePlayersPositions(frameData);
            currentFrameId = targetFrameId;
        }
        else if (lastValidPlayerData != null)
        {
            // 數據間隙：保持在最後一個有效位置（模擬真實影片的靜止畫面）
            currentFrameId = targetFrameId; // 更新當前幀ID用於除錯顯示
        }
    }

    private void InstantiateAllPlayers()
    {
        HashSet<int> uniqueTrackIds = new HashSet<int>();
        
        // 掃描所有幀數據，收集所有出現過的 track_id
        foreach (var frame in allFramesData.Values)
        {
            if (frame.track3ds != null)
            {
                foreach (var track3d in frame.track3ds)
                {
                    uniqueTrackIds.Add(track3d.track_id);
                }
            }
        }
        
        permanentPlayers = new Dictionary<int, PlayerController>();
        
        foreach (int trackId in uniqueTrackIds)
        {
            GameObject newPlayerObject = Instantiate(playerPrefab, playerContainer);
            newPlayerObject.name = $"Player_ID_{trackId}";

            PlayerController controller = newPlayerObject.GetComponent<PlayerController>();
            if (controller != null)
            {
                permanentPlayers.Add(trackId, controller);
                // 初始位置設在場外
                newPlayerObject.transform.position = new Vector3(-100f, 0f, -100f);
            }
        }
        
        totalTrackIds = uniqueTrackIds.Count;
        Debug.Log($"共偵測到 {uniqueTrackIds.Count} 個獨立 Track ID，已生成 {permanentPlayers.Count} 個永久模型。");

        // --- 組合每個 track_id 的完整歷史座標，並收集 Y 軸高度 ---
        var trackPaths = new Dictionary<int, List<Vector3>>();
        var trackAllYValues = new Dictionary<int, List<float>>(); // 收集 Y 值

        var orderedFrameIds = allFramesData.Keys.ToList();
        orderedFrameIds.Sort();

        foreach (int fid in orderedFrameIds)
        {
            var f = allFramesData[fid];
            if (f.track3ds == null) continue;
            foreach (var t in f.track3ds)
            {
                if (t.pt3d == null || t.pt3d.Count < 3) continue;
                Vector3 pos = ConvertDataToUnityPosition(t.pt3d);

                if (!trackPaths.ContainsKey(t.track_id)) trackPaths[t.track_id] = new List<Vector3>();
                trackPaths[t.track_id].Add(pos);

                if (!trackAllYValues.ContainsKey(t.track_id)) trackAllYValues[t.track_id] = new List<float>();
                trackAllYValues[t.track_id].Add(pos.y);
            }
        }

        // --- 執行直方圖眾數法 (Histogram Mode) 計算每人真實身高 ---
        float binSize = 0.02f; // 設定 2 公分為一個分箱區間 (Bin)

        foreach (var kv in permanentPlayers)
        {
            int trackId = kv.Key;
            var controller = kv.Value;
            if (controller == null) continue;

            float playerTrueHeight = 1.8f; // 預設防呆高度

            if (trackAllYValues.TryGetValue(trackId, out var yList) && yList.Count > 0)
            {
                Dictionary<int, int> histogram = new Dictionary<int, int>();
                
                // 1. 將所有的 Y 值丟進對應的箱子 (O(N) 複雜度)
                foreach (float y in yList)
                {
                    int bin = Mathf.RoundToInt(y / binSize);
                    if (!histogram.ContainsKey(bin)) histogram[bin] = 0;
                    histogram[bin]++;
                }

                // 2. 找出數據最多的那個箱子 (眾數)
                int maxCount = 0;
                int modeBin = 0;
                foreach (var binKvp in histogram)
                {
                    if (binKvp.Value > maxCount)
                    {
                        maxCount = binKvp.Value;
                        modeBin = binKvp.Key;
                    }
                }

                // 3. 還原成實際高度
                playerTrueHeight = modeBin * binSize;
                Debug.Log($"[身高演算法] Track ID {trackId} 的最常出現身高(眾數)為: {playerTrueHeight:F2}m (樣本數: {maxCount}/{yList.Count})");
            }

            // 4. 將算出專屬的身高傳給 Controller 進行初始化
            if (trackPaths.TryGetValue(trackId, out var pathList))
            {
                controller.InitializeTrajectory(pathList, lineMaterial, pointPrefab, playerTrueHeight);
            }
            else
            {
                controller.InitializeTrajectory(new List<Vector3>(), lineMaterial, pointPrefab, playerTrueHeight);
            }
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
        if (allFramesData.TryGetValue(startFrameId, out var initialData))
        {
            UpdatePlayersPositions(initialData);
        }
        
        Debug.Log($"開始播放球員動畫，起始幀ID: {startFrameId}");
    }

    // **【新增】** 停止播放
    public void StopPlayback()
    {
        isPlaying = false;
        
        // 將所有球員設為無目標狀態
        if (permanentPlayers != null)
        {
            foreach (var player in permanentPlayers.Values)
            {
                if (player != null)
                {
                    player.SetNoTarget();
                }
            }
        }
        
        Debug.Log("停止播放球員動畫");
    }

    // **【新增】** 更新所有球員的位置
    void UpdatePlayersPositions(FrameData frameData)
    {
        currentFramePlayerData = new Dictionary<int, Vector3>();
        
        // 先將所有球員設為無目標
        foreach (var player in permanentPlayers.Values)
        {
            if (player != null)
            {
                player.SetNoTarget();
            }
        }

        // 更新有數據的球員位置
        if (frameData.track3ds != null)
        {
            foreach (var track3d in frameData.track3ds)
            {
                if (track3d.pt3d != null && track3d.pt3d.Count == 3)
                {
                    Vector3 position = ConvertDataToUnityPosition(track3d.pt3d);
                    currentFramePlayerData[track3d.track_id] = position;
                    
                    if (permanentPlayers.TryGetValue(track3d.track_id, out var playerController))
                    {
                        playerController.SetTargetPosition(position);
                    }
                }
            }
        }
        
        // 保存當前有效數據供數據間隙時使用
        if (currentFramePlayerData.Count > 0)
        {
            lastValidPlayerData = new Dictionary<int, Vector3>(currentFramePlayerData);
        }
    }

    private Vector3 ConvertDataToUnityPosition(List<float> pt3d)
    {
        return new Vector3(pt3d[0], pt3d[2], pt3d[1]);
    }

    /// <summary>
    /// 載入並解析球員數據 JSON 檔案
    /// </summary>
    private void LoadAndParsePlayerData()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[PlayersManager] JSON 檔案未找到: {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var rootData = JsonConvert.DeserializeObject<ShowDataRoot>(json);
            
            if (rootData != null && rootData.frame_data != null)
            {
                ParseFrameData(rootData.frame_data);
                hasValidData = allFramesData.Count > 0;
                
                if (hasValidData)
                {
                    UpdateFrameRange();
                    Debug.Log($"[PlayersManager] 成功載入 {totalFrames} 幀球員數據，幀ID範圍: {actualStartFrame}-{actualEndFrame}");
                }
                else
                {
                    Debug.LogError("[PlayersManager] 沒有找到有效的球員數據");
                }
            }
            else
            {
                Debug.LogError("[PlayersManager] JSON 解析失敗或 frame_data 為空");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayersManager] 解析 JSON 時發生錯誤: {e.Message}");
        }
    }

    /// <summary>
    /// 解析幀數據字典並轉換為內部數據結構
    /// </summary>
    private void ParseFrameData(Dictionary<string, FrameData> frameDataDict)
    {
        allFramesData = new Dictionary<int, FrameData>();
        
        foreach (var kvp in frameDataDict)
        {
            if (int.TryParse(kvp.Key, out int frameId))
            {
                var frameData = kvp.Value;
                
                // 檢查是否有有效的 track3ds 數據
                if (frameData.track3ds != null && frameData.track3ds.Count > 0)
                {
                    // 過濾掉 pt3d 為 null 的數據
                    var validTracks = frameData.track3ds.Where(t => t.pt3d != null && t.pt3d.Count == 3).ToList();
                    
                    if (validTracks.Count > 0)
                    {
                        // 創建新的 FrameData 只包含有效數據
                        var cleanFrameData = new FrameData
                        {
                            frame_id = frameData.frame_id,
                            track3ds = validTracks
                        };
                        
                        allFramesData[frameId] = cleanFrameData;
                    }
                }
            }
        }
        
        totalFrames = allFramesData.Count;
        Debug.Log($"[PlayersManager] 解析完成，共載入 {totalFrames} 幀有效數據");
    }

    /// <summary>
    /// 更新實際幀範圍並設定播放參數
    /// </summary>
    void UpdateFrameRange()
    {
        if (allFramesData.Count == 0)
        {
            actualStartFrame = 0;
            actualEndFrame = 0;
            return;
        }
        
        actualStartFrame = allFramesData.Keys.Min();
        actualEndFrame = allFramesData.Keys.Max();
        
        // 如果沒有設定開始幀ID，使用實際最小幀
        if (startFrameId <= 0)
        {
            startFrameId = actualStartFrame;
        }
        
        // 如果沒有設定結束幀ID，使用實際最大幀
        if (endFrameId <= 0)
        {
            endFrameId = actualEndFrame;
        }
    }
}

// --- JSON 數據結構定義 (根據實際 JSON 結構) ---

/// <summary>
/// JSON 檔案的根結構
/// </summary>
[System.Serializable]
public class ShowDataRoot
{
    public Dictionary<string, FrameData> frame_data;
    public GlobalData global_data;
}

/// <summary>
/// 全域數據結構（目前為空）
/// </summary>
[System.Serializable]
public class GlobalData
{
    // 目前 JSON 中的 global_data 為空物件
}

/// <summary>
/// 每一幀的數據結構
/// </summary>
[System.Serializable]
public class FrameData
{
    public int frame_id;
    public CameraData cameras;
    public List<Track3D> track3ds;
}

/// <summary>
/// 攝影機數據結構
/// </summary>
[System.Serializable]
public class CameraData
{
    public CameraAData A;
    // 可以擴展其他攝影機 B, C, D 等
}

/// <summary>
/// 攝影機 A 的數據
/// </summary>
[System.Serializable]
public class CameraAData
{
    public List<Track2D> track2ds;
    // 其他攝影機相關數據可以在這裡擴展
}

/// <summary>
/// 2D 追蹤數據
/// </summary>
[System.Serializable]
public class Track2D
{
    public int track_id;
    public List<float> bbox;
    public List<List<float>> kpts;
    // 其他 2D 相關數據
}

/// <summary>
/// 3D 追蹤數據（這是我們主要需要的）
/// </summary>
[System.Serializable]
public class Track3D
{
    public int track_id;
    public List<float> pt3d;
}