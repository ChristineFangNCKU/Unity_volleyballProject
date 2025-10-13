using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class Player_Controller : MonoBehaviour
{
    public string jsonFileName = "HDR80_A_Live_20230205_132958_000_top12_players_pt3d.json";
    public float frameRate = 60f;
    public float scaleFactor = 10f; // 新增：可調整的比例縮放係數
    public int targetTrackId = 0; // 目標玩家的 track_id

    private List<Vector3> positions = new List<Vector3>();
    private int currentFrame = 0;
    private float timer = 0f;

    void Start()
    {
        LoadPlayer0Positions();
        if (positions.Count > 0)
        {
            // 初始化位置
            if (positions[0] != Vector3.negativeInfinity)
            {
                transform.position = positions[0];
            }
        }
    }

    void Update()
    {
        if (positions.Count == 0) return;
        timer += Time.deltaTime;
        float frameTime = 1f / frameRate;
        while (timer >= frameTime)
        {
            timer -= frameTime;
            currentFrame++;
            if (currentFrame >= positions.Count)
            {
                currentFrame = positions.Count - 1; // 停止在最後一幀
            }
            if (positions[currentFrame] != Vector3.negativeInfinity)
            {
                transform.position = positions[currentFrame];
            }
        }
    }

    void LoadPlayer0Positions()
    {
        string path = Path.Combine(Application.streamingAssetsPath, jsonFileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"JSON file not found: {path}");
            return;
        }
        string json = File.ReadAllText(path);
        var root = JsonConvert.DeserializeObject<RootData>(json);

        // 確認 pt3d 字典中是否存在 "0" 的鍵值
        // 找到對應 track_id 的資料
        if (root.pt3d == null || !root.pt3d.ContainsKey(targetTrackId.ToString()))
        {
            Debug.LogError($"JSON data missing 'pt3d' or key '{targetTrackId}' for Player {targetTrackId}.");
            return;
        }
        var pt3dList = root.pt3d[targetTrackId.ToString()];
        foreach (var pt3d in pt3dList)
        {
            if (pt3d == null || pt3d.Count != 3)
            {
                // 缺失值用 Vector3.negativeInfinity 佔位
                positions.Add(Vector3.negativeInfinity);
            }
            else
            {
                // 關鍵修改：座標軸轉換與縮放
                // 將資料的 x, y, z 對應到 Unity 的 x, z, y
                // 並乘以 scaleFactor 進行縮放
                positions.Add(new Vector3(pt3d[0] * scaleFactor, pt3d[2] * scaleFactor , pt3d[1] * scaleFactor));
            }
        }
    }

    // 用於反序列化JSON
    [System.Serializable]
    public class RootData
    {
        public List<int> track_id_map;
        public Dictionary<string, List<List<float>>> pt3d;
    }
}