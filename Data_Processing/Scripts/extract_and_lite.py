import json
import os
import time
import math

# --- File Paths ---
input_file = "../Raw_Matches/HDR80_A_Live_20230205_132958_000.mov_show_data.json"
unity_lite_output = "../../Assets/StreamingAssets/Matches/HDR80_A_Live_20230205_132958_000_Lite.json"
gnn_output = "../../Assets/StreamingAssets/Matches/Cleaned_GNN_Dataset.json"

print(f"🔥 Loading the 1GB raw JSON... Please wait.")
start_time = time.time()

with open(input_file, 'r') as f:
    data = json.load(f)

print(f"✅ Loaded in {time.time() - start_time:.1f}s. Starting extraction...")

lite_data = {"frame_data": {}}
gnn_data = {}

frame_dict = data.get("frame_data", {})
total_frames = len(frame_dict)
processed = 0

for frame_id, frame_content in frame_dict.items():
    # 1. --- Unity Lite Processing ---
    valid_track3ds = frame_content.get("track3ds", [])
    
    # 【新增防禦】: 確保 Unity 用的 pt3d 裡面也沒有 NaN
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
        found_kpts = None
        for cam_name, cam_data in cameras.items():
            for t2d in cam_data.get("track2ds", []):
                if t2d.get("track3d_id") == g_id:
                    raw_kpts = t2d.get("kpt3ds")
                    
                    if isinstance(raw_kpts, list):
                        cleaned_kpts = []
                        for k in raw_kpts:
                            # 【核心修復】: 檢查 k 是否真的是一個包含 3 個數字的陣列
                            if isinstance(k, list) and len(k) == 3:
                                # 再次確保裡面的 x, y, z 不是 NaN
                                clean_k = [0.0 if (val is None or (isinstance(val, float) and math.isnan(val))) else val for val in k]
                                cleaned_kpts.append(clean_k)
                            else:
                                # 如果 k 是 NaN、None 或是其他怪東西，直接補上 [0.0, 0.0, 0.0]
                                cleaned_kpts.append([0.0, 0.0, 0.0])
                        
                        # 防呆：確保最後一定有 17 個點
                        while len(cleaned_kpts) < 17:
                            cleaned_kpts.append([0.0, 0.0, 0.0])
                            
                        found_kpts = cleaned_kpts
                        break # 在這台攝影機找到了，跳出迴圈
            if found_kpts: break # 找到了就不用找另一台攝影機
            
        if found_kpts:
            gnn_data[frame_id][g_id] = found_kpts

    processed += 1
    if processed % 500 == 0:
        print(f"⏳ Processed {processed}/{total_frames} frames...")

# --- Write Outputs ---
print(f"💎 Extraction complete. Saving files to Unity StreamingAssets/Matches/...")

# 【安全鎖】: allow_nan=False 確保輸出的 JSON 絕對符合標準，不會讓 Unity 當機
with open(unity_lite_output, 'w') as f:
    json.dump(lite_data, f, separators=(',', ':'), allow_nan=False)
    
with open(gnn_output, 'w') as f:
    json.dump(gnn_data, f, separators=(',', ':'), allow_nan=False)

raw_size = os.path.getsize(input_file) / (1024*1024)
lite_size = os.path.getsize(unity_lite_output) / (1024*1024)
gnn_size = os.path.getsize(gnn_output) / (1024*1024)

print(f"🎉 Done in {time.time() - start_time:.1f}s!")
print(f"📉 Unity Lite JSON: {raw_size:.1f}MB -> {lite_size:.1f}MB")
print(f"🤖 GNN Dataset JSON: {gnn_size:.1f}MB")