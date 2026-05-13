import json
import os
import time
import math

# ==========================================
# 🚨 老哥的環境設定區 (請務必確認路徑正確)
# ==========================================
# 1. 直接讀取你手動拖進來的 NAS_Temp 資料夾
NAS_SOURCE_DIR = "../NAS_Temp"

# 2. 本機暫存區 (雖然現在沒用到拷貝，但先留著當防呆路徑)
LOCAL_RAW_DIR = "../Raw_Matches"

# 3. 處理完的輕量檔案要丟去哪裡 (Unity 的 StreamingAssets)
UNITY_OUTPUT_DIR = "../../Assets/StreamingAssets/Matches"

# 決定要處理幾個檔案 (可以設大一點，因為有智慧跳過功能了)
MAX_FILES_TO_PROCESS = 100 
# ==========================================

def process_single_file(local_input_file, base_filename):
    """
    包含「Camera A/D 智慧融合」的終極提煉邏輯
    """
    unity_lite_output = os.path.join(UNITY_OUTPUT_DIR, f"{base_filename}_Lite.json")
    gnn_output = os.path.join(UNITY_OUTPUT_DIR, f"{base_filename}_GNN.json")

    print(f"  🔥 載入 1GB 原始資料中... 請稍候。")
    start_time = time.time()

    with open(local_input_file, 'r') as f:
        data = json.load(f)

    print(f"  ✅ 載入完成 (耗時 {time.time() - start_time:.1f}s)。開始提煉骨架...")

    lite_data = {"frame_data": {}}
    gnn_data = {}

    frame_dict = data.get("frame_data", {})
    total_frames = len(frame_dict)
    processed = 0

    for frame_id, frame_content in frame_dict.items():
        # 1. --- Unity Lite Processing ---
        valid_track3ds = frame_content.get("track3ds", [])
        
        # 【防禦】: 確保 Unity 用的 pt3d 裡面也沒有 NaN
        clean_track3ds = []
        for t in valid_track3ds:
            pt = t.get("pt3d")
            if isinstance(pt, list) and len(pt) == 3:
                clean_pt = [0.0 if (val is None or (isinstance(val, float) and math.isnan(val))) else val for val in pt]
                t["pt3d"] = clean_pt
                clean_track3ds.append(t)

        lite_data["frame_data"][frame_id] = {
            "frame_id": int(frame_id),
            "track3ds": clean_track3ds
        }
        
        # 2. --- GNN Processing (17 Keypoints) ---
        gnn_data[frame_id] = {}
        global_ids = [t["track_id"] for t in clean_track3ds if "track_id" in t]
        cameras = frame_content.get("cameras", {})
        
        for g_id in global_ids:
            # ==========================================
            # 🛡️ 老哥升級版：多攝影機 (Camera A & D) 智慧融合邏輯
            # ==========================================
            fused_kpts = [[0.0, 0.0, 0.0] for _ in range(17)] # 預先準備好 17 個空的點
            points_filled = 0 # 紀錄已經成功抓到幾個有效點

            # 掃描所有攝影機 (通常是 A 和 D)
            for cam_name, cam_data in cameras.items():
                # ⚡ 提早結束：如果 17 個點都已經拿到了，就不去下一台攝影機浪費時間了
                if points_filled == 17:
                    break

                # 在這台攝影機裡尋找這個人的 2D/3D 映射資料
                for t2d in cam_data.get("track2ds", []):
                    if t2d.get("track3d_id") == g_id:
                        raw_kpts = t2d.get("kpt3ds", [])
                        
                        # 開始比對這 17 個關節點
                        for i in range(min(17, len(raw_kpts))):
                            pt = raw_kpts[i]
                            
                            # 檢查這個點是否合法 (不是 None、有三個維度)
                            if isinstance(pt, list) and len(pt) >= 3:
                                x, y, z = pt[0], pt[1], pt[2]
                                
                                # 檢查是不是 NaN，且不能是全 0 (全 0 代表遮擋遺失)
                                if not (math.isnan(x) or math.isnan(y) or math.isnan(z)):
                                    if x != 0.0 or y != 0.0 or z != 0.0:
                                        
                                        # ✨ 【核心融合邏輯】：
                                        # 如果我們 fused_kpts 裡面這個關節點還是空的 [0,0,0]，
                                        # 就把這台攝影機拍到的清楚座標填進去！
                                        if fused_kpts[i] == [0.0, 0.0, 0.0]:
                                            fused_kpts[i] = [float(x), float(y), float(z)]
                                            points_filled += 1
                                            
                        # 在這台攝影機找到這個人了，換下一台攝影機繼續「補漏」
                        break 
                        
            # 判斷這個人到底有沒有抓到有效骨架 (只要有填入任何一點就算有)
            if points_filled > 0:
                gnn_data[frame_id][str(g_id)] = fused_kpts
            # ==========================================

        processed += 1
        if processed % 500 == 0:
            print(f"  ⏳ 已處理 {processed}/{total_frames} 幀...")

    # --- Write Outputs ---
    print(f"  💎 提煉完畢！正在寫入 StreamingAssets 資料夾...")

    # 【安全鎖】: allow_nan=False 確保輸出的 JSON 絕對符合標準
    with open(unity_lite_output, 'w') as f:
        json.dump(lite_data, f, separators=(',', ':'), allow_nan=False)
        
    with open(gnn_output, 'w') as f:
        json.dump(gnn_data, f, separators=(',', ':'), allow_nan=False)

    raw_size = os.path.getsize(local_input_file) / (1024*1024)
    lite_size = os.path.getsize(unity_lite_output) / (1024*1024)
    gnn_size = os.path.getsize(gnn_output) / (1024*1024)

    print(f"  📉 檔案瘦身: {raw_size:.1f}MB -> {lite_size:.1f}MB")
    print(f"  🤖 GNN 矩陣: {gnn_size:.1f}MB")


def main():
    os.makedirs(LOCAL_RAW_DIR, exist_ok=True)
    os.makedirs(UNITY_OUTPUT_DIR, exist_ok=True)

    print("🔍 掃描 NAS_Temp 資料夾中的 JSON 檔案...")
    if not os.path.exists(NAS_SOURCE_DIR):
        print(f"❌ 找不到路徑: {NAS_SOURCE_DIR}")
        print("請確認你有把網頁下載的檔案放進這個資料夾！")
        return

    all_files = [f for f in os.listdir(NAS_SOURCE_DIR) if f.endswith('.json')]
    print(f"👀 總共找到 {len(all_files)} 個 JSON 檔案。")

    target_files = all_files[:MAX_FILES_TO_PROCESS]

    for idx, file_name in enumerate(target_files, 1):
        print(f"\n=============================================")
        print(f"🚀 開始處理第 {idx}/{len(target_files)} 個檔案: {file_name}")
        print(f"=============================================")
        
        nas_file_path = os.path.join(NAS_SOURCE_DIR, file_name)
        base_filename = os.path.splitext(file_name)[0]

        # ==========================================
        # 🛡️ 智慧跳過防呆邏輯
        # ==========================================
        unity_lite_output = os.path.join(UNITY_OUTPUT_DIR, f"{base_filename}_Lite.json")
        gnn_output = os.path.join(UNITY_OUTPUT_DIR, f"{base_filename}_GNN.json")

        if os.path.exists(unity_lite_output) and os.path.exists(gnn_output):
            print(f"⏭️ 聰明如你！{base_filename} 早就處理過了，直接跳過，幫 Mac 省算力！")
            os.remove(nas_file_path)
            print(f"🗑️ 處理完成，已自動刪除原始大檔釋放空間。")
            continue 
        # ==========================================

        try:
            print(f"🚀 檔案已在本機，開始智慧融合處理...")
            process_single_file(nas_file_path, base_filename)

            # 處理完之後，如果你想釋放硬碟空間，可以把下面這行的 # 拿掉，它會自動幫你刪掉 1GB 的原檔
            os.remove(nas_file_path)
            print(f"🗑️ 處理完成，已自動刪除原始大檔釋放空間。")
            
        except Exception as e:
            print(f"❌ 處理 {file_name} 時發生錯誤: {e}")
            print("跳過此檔案，繼續下一個...")

    print("\n🎉 恭喜老哥！所有目標檔案的精華已全數提煉完畢！")

if __name__ == "__main__":
    main()