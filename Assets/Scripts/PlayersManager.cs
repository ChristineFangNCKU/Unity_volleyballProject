using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

public class PlayersManager : MonoBehaviour
{
    [Header("數據設定")]
    [Tooltip("要讀取的 JSON 檔案名稱")]
    public string jsonFileName = "HDR80_A_Live_20230205_132958_000_top12_players_pt3d.json";
    [Tooltip("數據的幀率")]
    public float frameRate = 60f;

    [Header("場景物件")]
    [Tooltip("將場景中所有掛載了 PlayerController 的球員物件拖到這裡")]
    public List<PlayerController> players;

    // 私有變數
    private List<FrameData> allFramesData; // 儲存所有【轉置後】的幀數據
    private int currentFrameIndex = 0;
    private float frameTimer = 0f;

    void Start()
    {
        LoadAndParsePlayerData();
    }

    void Update()
    {
        if (allFramesData == null || players == null || players.Count == 0 || currentFrameIndex >= allFramesData.Count)
        {
            return; // 如果沒有數據或沒有球員，則不執行
        }

        // --- 時間推進 ---
        frameTimer += Time.deltaTime;
        float timePerFrame = 1f / frameRate;

        if (frameTimer >= timePerFrame)
        {
            frameTimer -= timePerFrame;
            currentFrameIndex++;
            if (currentFrameIndex >= allFramesData.Count)
            {
                Debug.Log("播放完畢。");
                enabled = false; // 停止 Update 循環
                return;
            }

            // --- 執行核心匹配邏輯 ---
            AssignPlayerPositionsForCurrentFrame();
        }
    }

    /// <summary>
    /// **【核心演算法】** 為當前幀的數據分配給最合適的球員
    /// </summary>
    private void AssignPlayerPositionsForCurrentFrame()
    {
        // 1. 取得當前幀所有被偵測到的球員數據點 (我們只關心攝影機A)
        var currentDetections = new List<PlayerData>();
        if (allFramesData[currentFrameIndex]?.cameras?.A != null)
        {
            currentDetections = allFramesData[currentFrameIndex].cameras.A
                .Where(p => p.pt3d != null && p.pt3d.Count == 3) // 只取有有效3D座標的點
                .ToList();
        }

        // 2. 準備一個尚未被分配數據的場景球員列表
        var availablePlayers = new List<PlayerController>(players);

        // 3. 貪婪匹配演算法：為每個偵測點找到最近的空閒球員
        foreach (var detection in currentDetections)
        {
            if (availablePlayers.Count == 0) break; // 如果所有球員都分配完了，就提前結束

            PlayerController closestPlayer = null;
            float minDistance = float.MaxValue;

            // 尋找距離這個偵測點最近的那個尚未被分配的球員
            foreach (var player in availablePlayers)
            {
                float distance = Vector3.Distance(player.transform.position, ConvertDataToUnityPosition(detection.pt3d));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPlayer = player;
                }
            }

            // 4. 分配數據並更新列表
            if (closestPlayer != null)
            {
                // 告訴這個球員他下一幀的目標位置
                closestPlayer.SetTargetPosition(ConvertDataToUnityPosition(detection.pt3d));
                // 將他從「空閒」列表中移除，避免他被重複分配
                availablePlayers.Remove(closestPlayer);
            }
        }

        // 5. 處理那些在這一幀沒有匹配到任何數據的球員
        foreach (var unassignedPlayer in availablePlayers)
        {
            unassignedPlayer.SetNoTarget(); // 告訴他們原地待命
        }
    }

    private Vector3 ConvertDataToUnityPosition(List<float> pt3d)
    {
        return new Vector3(pt3d[0], pt3d[2], pt3d[1]);
    }

    /// <summary>
    /// **【已更新】** 讀取並解析以 Player 為中心的 JSON，並將其轉置為以 Frame 為中心的結構
    /// </summary>
    private void LoadAndParsePlayerData()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[PlayersManager] JSON 檔案未找到: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        // 1. 使用正確的 Root 結構來解析 JSON 物件
        var rawData = JsonConvert.DeserializeObject<PlayerJsonRoot>(json);

        if (rawData == null || rawData.pt3d == null)
        {
            Debug.LogError("[PlayersManager] JSON 解析失敗或 'pt3d' 數據不存在！");
            return;
        }
        
        // --- 2. 數據轉置 (Data Transposing) ---
        allFramesData = new List<FrameData>();
        // 找到最長的幀數，以確定總時長
        int maxFrames = 0;
        foreach (var playerFrames in rawData.pt3d.Values)
        {
            if (playerFrames.Count > maxFrames)
            {
                maxFrames = playerFrames.Count;
            }
        }

        // 逐幀建立我們的理想數據結構
        for (int i = 0; i < maxFrames; i++)
        {
            var frame = new FrameData
            {
                frame_id = i,
                cameras = new CameraData { A = new List<PlayerData>() }
            };

            // 遍歷所有球員，看他們在當前幀(i)是否有數據
            foreach (var playerEntry in rawData.pt3d)
            {
                // 檢查該球員在這一幀是否有數據
                if (i < playerEntry.Value.Count && playerEntry.Value[i] != null && playerEntry.Value[i].Count == 3)
                {
                    frame.cameras.A.Add(new PlayerData
                    {
                        track_id = int.Parse(playerEntry.Key), // Key 是 track_id
                        pt3d = playerEntry.Value[i]
                    });
                }
            }
            allFramesData.Add(frame);
        }

        Debug.Log($"[PlayersManager] 成功加載並轉置 {allFramesData.Count} 幀的數據。");
    }
}

// --- 【已更新】以下是為了匹配您的 JSON 結構而定義的 C# 類別 ---

// 新增一個 Root 類別來匹配 JSON 的最外層物件
[System.Serializable]
public class PlayerJsonRoot
{
    public List<int> track_id_map;
    // Key 是 track_id (string), Value 是該球員所有幀的座標列表
    public Dictionary<string, List<List<float>>> pt3d;
}


// 以下的類別定義保持不變，因為它們描述的是我們【轉置後】的理想結構
[System.Serializable]
public class FrameData
{
    public int frame_id;
    public CameraData cameras;
}

[System.Serializable]
public class CameraData
{
    public List<PlayerData> A;
    public List<PlayerData> B;
}

[System.Serializable]
public class PlayerData
{
    public int track_id;
    public List<float> pt3d;
}

