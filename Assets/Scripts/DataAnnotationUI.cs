using UnityEngine;
using System.Collections.Generic;

public class DataAnnotationUI : MonoBehaviour
{
    [Header("Dependencies")]
    public PlayersManager playersManager;
    public Camera mainCamera;

    // --- Internal Toggles ---
    private bool showDevPanel = true;
    private bool showIDs = true;

    private GUIStyle bigTextStyle;
    private GUIStyle idTextStyle;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void OnGUI()
    {
        if (playersManager == null) return;

        // 初始化字體樣式
        if (bigTextStyle == null)
        {
            bigTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            bigTextStyle.normal.textColor = Color.yellow;
            
            // 給 ID 文字加上一點陰影效果，讓它在淺色地板上也看得清楚
            idTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            idTextStyle.normal.textColor = Color.cyan;
        }

        // --- Feature A: 右上角巨大 Frame 計數器 ---
        GUI.Label(new Rect(Screen.width - 250, 20, 200, 50), $"Frame: {playersManager.currentFrameId}", bigTextStyle);

        // --- Feature B: 左上角控制面板 ---
        showDevPanel = GUI.Toggle(new Rect(20, 20, 150, 20), showDevPanel, " 顯示開發者面板");
        
        if (showDevPanel)
        {
            GUI.Box(new Rect(10, 50, 200, 60), "Dev Tools");
            showIDs = GUI.Toggle(new Rect(20, 80, 180, 20), showIDs, " 顯示球員 Track ID");
        }

        // --- Feature C: 在球員頭頂畫出 Track ID ---
        if (showIDs)
        {
            var players = playersManager.GetActivePlayers();
            if (players != null)
            {
                foreach (var kvp in players)
                {
                    PlayerController player = kvp.Value;
                    // 確保球員物件有啟用，且模型有被渲染出來
                    if (player.gameObject.activeInHierarchy && player.GetComponentInChildren<MeshRenderer>().enabled)
                    {
                        // 將 3D 世界座標轉換為 2D 螢幕座標 (往上偏移一點，剛好在頭頂)
                        Vector3 worldPos = player.transform.position + new Vector3(0, player.calculatedHeadHeight + 0.3f, 0);
                        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

                        // 確保球員在攝影機前方 (Z > 0)
                        if (screenPos.z > 0)
                        {
                            // OnGUI 的 Y 軸是反的，需要用 Screen.height 去減
                            float guiY = Screen.height - screenPos.y;
                            
                            // 畫一點黑色陰影，再畫青色文字
                            GUI.color = Color.black;
                            GUI.Label(new Rect(screenPos.x - 19, guiY + 1, 100, 50), $"ID: {kvp.Key}", idTextStyle);
                            GUI.color = Color.cyan;
                            GUI.Label(new Rect(screenPos.x - 20, guiY, 100, 50), $"ID: {kvp.Key}", idTextStyle);
                            GUI.color = Color.white; // 恢復預設顏色
                        }
                    }
                }
            }
        }
    }
}