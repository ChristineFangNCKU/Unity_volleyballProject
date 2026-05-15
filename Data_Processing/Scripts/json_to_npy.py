import json
import numpy as np
import os
import pickle
import pandas as pd

# ==========================================
# 1. 環境與路徑設定
# ==========================================
csv_path = "../Volleyball_DataLabel - 工作表1.csv"
gnn_dir = "../../Assets/StreamingAssets/Matches"
output_dir = "../GNN_Dataset"

os.makedirs(output_dir, exist_ok=True)
output_data = os.path.join(output_dir, "train_data.npy")
output_label = os.path.join(output_dir, "train_label.pkl")

C, T, V, M = 3, 90, 17, 1

# ==========================================
# 2. 讀取並清洗 CSV 標記資料
# ==========================================
print("📊 正在讀取並清洗 CSV 標記資料...")
df = pd.read_csv(csv_path)

# 過濾: 只保留核心 MVP 動作，並移除無效值
valid_classes = ['Passing', 'Setting', 'Attacking']
df = df[df['class_name'].isin(valid_classes)].copy()
# 註：已依照最新規格，移除 occlusion_flag 的檢查邏輯
df = df.dropna(subset=['start_frame', 'end_frame', 'track_id'])

class_map = {'Passing': 0, 'Setting': 1, 'Attacking': 2}

# ==========================================
# 3. 提取數據與中心點對齊 (Center Alignment)
# ==========================================
N = len(df) # 移除翻轉擴增，回歸真實資料量
numpy_data = np.zeros((N, C, T, V, M), dtype=np.float32)
labels = []
sample_names = []

available_gnn_files = [f for f in os.listdir(gnn_dir) if f.endswith('.json')]
gnn_cache = {}

current_idx = 0
print(f"🚀 開始提取 3D 骨架序列並進行中心點歸零...")

for index, row in df.iterrows():
    raw_match_id = str(row['match_id']).replace('.json', '')
    track_id = str(int(row['track_id']))
    start_frame, end_frame = int(row['start_frame']), int(row['end_frame'])
    
    target_file = next((f for f in available_gnn_files if raw_match_id in f), None)
    if not target_file: continue
        
    if target_file not in gnn_cache:
        with open(os.path.join(gnn_dir, target_file), 'r') as jf:
            gnn_cache[target_file] = json.load(jf)
    data = gnn_cache[target_file]
    
    # 暫存陣列 (T, V, 3)
    temp_sequence = np.zeros((T, V, 3), dtype=np.float32)
    last_valid_skeleton = np.zeros((V, 3))
    
    # 抽取座標
    for t_idx, frame in enumerate(range(start_frame, start_frame + T)):
        f_str = str(frame)
        if f_str in data and track_id in data[f_str]:
            last_valid_skeleton = np.array(data[f_str][track_id])
        
        # 寫入暫存
        for v in range(V):
            temp_sequence[t_idx, v, 0] = last_valid_skeleton[v][0]
            temp_sequence[t_idx, v, 1] = last_valid_skeleton[v][1]
            temp_sequence[t_idx, v, 2] = last_valid_skeleton[v][2]

    # ✨ 核心修正：中心點對齊 (以第一幀的骨盆為世界中心)
    # 骨盆座標 = (左髖 11 + 右髖 12) / 2
    root_origin = (temp_sequence[0, 11, :] + temp_sequence[0, 12, :]) / 2.0
    
    # 將全部 90 幀的每一個點，都減去這個骨盆座標
    for t in range(T):
        for v in range(V):
            temp_sequence[t, v, :] = temp_sequence[t, v, :] - root_origin

    # 寫入最終 5D Tensor (N, C, T, V, M)
    for t in range(T):
        for v in range(V):
            numpy_data[current_idx, 0, t, v, 0] = temp_sequence[t, v, 0]
            numpy_data[current_idx, 1, t, v, 0] = temp_sequence[t, v, 1]
            numpy_data[current_idx, 2, t, v, 0] = temp_sequence[t, v, 2]
    
    labels.append(class_map[row['class_name']])
    sample_names.append(f"{target_file}_TR{track_id}_F{start_frame}")
    current_idx += 1

# ==========================================
# 4. 儲存 NPY 與 PKL
# ==========================================
np.save(output_data, numpy_data[:current_idx])
with open(output_label, 'wb') as f:
    pickle.dump((sample_names[:current_idx], labels[:current_idx]), f)

print(f"\n🎉 轉換大功告成！有效張量維度: (N, C, T, V, M) = {numpy_data[:current_idx].shape}")