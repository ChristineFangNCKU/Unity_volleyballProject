import json
import numpy as np
import os
import pickle
import pandas as pd

# ==========================================
# 1. 老哥的環境與路徑設定
# ==========================================
csv_path = "../Volleyball_DataLabel - 工作表1.csv" # 你的 CSV 檔案位置
gnn_dir = "../../Assets/StreamingAssets/Matches"    # GNN.json 存放的資料夾
output_dir = "../GNN_Dataset"                       # 輸出 PyTorch 張量的資料夾

os.makedirs(output_dir, exist_ok=True)
output_data = os.path.join(output_dir, "train_data.npy")
output_label = os.path.join(output_dir, "train_label.pkl")

# Neural Network Tensor Specifications
C = 3   # Channels (X, Y, Z)
T = 90  # Frames (Fixed 90)
V = 17  # Vertices (17 keypoints)
M = 1   # Person

# ==========================================
# 2. 讀取並清洗 CSV 標記資料
# ==========================================
print("📊 正在讀取並清洗 CSV 標記資料...")
df = pd.read_csv(csv_path)

# 過濾 1: 只保留 Passing, Setting, Attacking
valid_classes = ['Passing', 'Setting', 'Attacking']
df = df[df['class_name'].isin(valid_classes)].copy()

# 過濾 2: 排除「嚴重遮擋 (flag 2)」與空值
df = df[~df['occlusion_flag'].astype(str).str.contains('2')]
df = df.dropna(subset=['start_frame', 'end_frame', 'track_id'])

print(f"✅ 清洗完成！獲得 {len(df)} 筆高品質原始資料。")

# MVP 類別映射表 (轉為模型吃得懂的 0, 1, 2)
class_map = {'Passing': 0, 'Setting': 1, 'Attacking': 2}

# ==========================================
# 3. 提取數據與 Data Augmentation (左右翻轉)
# ==========================================
# 因為我們會做左右翻轉擴增，所以總資料量 N 會乘以 2
N = len(df) * 2 
numpy_data = np.zeros((N, C, T, V, M), dtype=np.float32)
labels = []
sample_names = []

# 預先載入所有 GNN 檔案以加速讀取
available_gnn_files = [f for f in os.listdir(gnn_dir) if f.endswith('.json')]
gnn_cache = {}

current_idx = 0
print(f"🚀 開始提取 3D 骨架序列並進行資料擴增 (Data Augmentation)...")

for index, row in df.iterrows():
    raw_match_id = str(row['match_id']).replace('.json', '')
    class_name = row['class_name']
    track_id = str(int(row['track_id']))
    start_frame = int(row['start_frame'])
    end_frame = int(row['end_frame'])
    label_id = class_map[class_name]
    
    # --- 智慧尋找對應的 GNN.json ---
    target_file = None
    for f in available_gnn_files:
        if raw_match_id in f:
            target_file = f
            break
            
    if not target_file:
        print(f"⚠️ 找不到對應的 GNN 檔案: {raw_match_id}，跳過。")
        continue
        
    # 載入 JSON (有快取機制)
    if target_file not in gnn_cache:
        with open(os.path.join(gnn_dir, target_file), 'r') as jf:
            gnn_cache[target_file] = json.load(jf)
    data = gnn_cache[target_file]
    
    # --- 提取骨架序列 ---
    action_length = end_frame - start_frame + 1
    frame_count = 0
    last_valid_skeleton = np.zeros((V, 3))
    
    # 用來暫存這一個動作的完整骨架 (T, V, 3)
    temp_skeleton_sequence = np.zeros((T, V, 3), dtype=np.float32)
    
    for frame in range(start_frame, start_frame + T):
        f_str = str(frame)
        skeleton_17 = None
        
        # 如果該幀存在，且該人物 (track_id) 存在
        if f_str in data and track_id in data[f_str]:
            skeleton_17 = data[f_str][track_id]
            last_valid_skeleton = np.array(skeleton_17)
        else:
            # 發生遮擋或遺失，使用上一幀的骨架 (Freeze-Padding)
            skeleton_17 = last_valid_skeleton
            
        # 存入暫存陣列 (如果動作長度小於 90，後面會自動複製 last_valid_skeleton)
        for v in range(V):
            temp_skeleton_sequence[frame_count, v, 0] = skeleton_17[v][0] # X
            temp_skeleton_sequence[frame_count, v, 1] = skeleton_17[v][1] # Y
            temp_skeleton_sequence[frame_count, v, 2] = skeleton_17[v][2] # Z
            
        frame_count += 1

    # --- 寫入原始資料 (Original) ---
    for t in range(T):
        for v in range(V):
            numpy_data[current_idx, 0, t, v, 0] = temp_skeleton_sequence[t, v, 0]
            numpy_data[current_idx, 1, t, v, 0] = temp_skeleton_sequence[t, v, 1]
            numpy_data[current_idx, 2, t, v, 0] = temp_skeleton_sequence[t, v, 2]
    
    labels.append(label_id)
    sample_names.append(f"{target_file}_TR{track_id}_F{start_frame}_Orig")
    current_idx += 1
    
    # --- 寫入擴增資料 (Augmentation: 左右翻轉) ---
    # 翻轉 X 軸座標 (X = -X)
    for t in range(T):
        for v in range(V):
            numpy_data[current_idx, 0, t, v, 0] = -temp_skeleton_sequence[t, v, 0] # ✨ X 軸加上負號
            numpy_data[current_idx, 1, t, v, 0] = temp_skeleton_sequence[t, v, 1]
            numpy_data[current_idx, 2, t, v, 0] = temp_skeleton_sequence[t, v, 2]
            
    labels.append(label_id)
    sample_names.append(f"{target_file}_TR{track_id}_F{start_frame}_Flip")
    current_idx += 1

# 裁切掉沒有填滿的陣列空間 (防呆)
numpy_data = numpy_data[:current_idx]

# ==========================================
# 4. 儲存 NPY 與 PKL
# ==========================================
np.save(output_data, numpy_data)

with open(output_label, 'wb') as f:
    pickle.dump((sample_names, labels), f)

print(f"\n🎉 轉換大功告成！")
print(f"📉 原始張量維度: (N, C, T, V, M) = {numpy_data.shape}")
print(f"📂 檔案已儲存至: {output_dir}")
print(f"💡 透過左右翻轉，我們成功把資料量擴充到了 {current_idx} 筆！準備好可以丟進模型訓練了！")