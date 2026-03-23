import json
import numpy as np
import os
import pickle

# --- 1. Settings & Paths ---
# Script is located at Data_Processing/Scripts/
input_json = "../../Assets/StreamingAssets/Matches/Cleaned_GNN_Dataset.json"
output_dir = "../GNN_Dataset"
output_data = os.path.join(output_dir, "train_data.npy")
output_label = os.path.join(output_dir, "train_label.pkl")

# --- 2. Action Segments (Ground Truth) ---
# Action Classes: 0=Passing, 1=Setting, 2=Attacking
action_segments = [
    {"action_class": 0, "start_frame": 965, "end_frame": 1038, "track_id": 9},  # Passing forearm (74 frames)
    {"action_class": 1, "start_frame": 504, "end_frame": 528,  "track_id": 11}, # Setting (25 frames)
    {"action_class": 2, "start_frame": 758, "end_frame": 830,  "track_id": 11}  # Attacking (73 frames)
]

# --- 3. Neural Network Tensor Specifications ---
N = len(action_segments) # Number of samples (3)
C = 3                    # Channels: X, Y, Z coordinates
T = 90                   # Time/Frames: Fixed to 90 to contain the longest action (74 frames)
V = 17                   # Vertices: 17 COCO keypoints
M = 1                    # Max Persons: 1 person per sample

print("🔥 啟動 GNN 矩陣轉換器 (JSON to Numpy)...")

def main():
    os.makedirs(output_dir, exist_ok=True)

    if not os.path.exists(input_json):
        print(f"❌ 找不到輸入檔案: {input_json}")
        return

    with open(input_json, 'r') as f:
        gnn_data = json.load(f)

    # Initialize empty tensor with shape (N, C, T, V, M), type float32 for PyTorch
    numpy_data = np.zeros((N, C, T, V, M), dtype=np.float32)
    labels = []

    # --- 4. Data Extraction & Padding ---
    for i, segment in enumerate(action_segments):
        start_f = segment["start_frame"]
        end_f = segment["end_frame"]
        t_id = str(segment["track_id"])  # JSON keys are strings
        a_class = segment["action_class"]
        
        labels.append(a_class)
        frame_count = 0
        last_valid_skeleton = np.zeros((V, C)) # Fallback for missing frames
        
        for f_id in range(start_f, end_f + 1):
            if frame_count >= T:
                break  # Trim if the action exceeds 90 frames
                
            str_f_id = str(f_id)
            
            # Check if this frame and track_id exist in our cleaned data
            if str_f_id in gnn_data and t_id in gnn_data[str_f_id]:
                skeleton_17 = gnn_data[str_f_id][t_id]  # Shape: [17, 3]
                last_valid_skeleton = np.array(skeleton_17)
            else:
                # Use the last valid skeleton if tracking was lost for a frame
                skeleton_17 = last_valid_skeleton
                
            # Fill the matrix: numpy_data[N, C, T, V, M]
            for v in range(V):
                numpy_data[i, 0, frame_count, v, 0] = skeleton_17[v][0] # X
                numpy_data[i, 1, frame_count, v, 0] = skeleton_17[v][1] # Y
                numpy_data[i, 2, frame_count, v, 0] = skeleton_17[v][2] # Z
                
            frame_count += 1
            
        # Zero-Padding / Freeze-Padding:
        # If action is shorter than 90 frames, copy the last valid pose until frame 90
        while frame_count < T:
            for v in range(V):
                numpy_data[i, 0, frame_count, v, 0] = last_valid_skeleton[v][0]
                numpy_data[i, 1, frame_count, v, 0] = last_valid_skeleton[v][1]
                numpy_data[i, 2, frame_count, v, 0] = last_valid_skeleton[v][2]
            frame_count += 1

    # --- 5. Save Output ---
    np.save(output_data, numpy_data)

    # Save Labels in Pickle format (Tuple: Sample Names, Label IDs)
    label_tuple = ([f"action_sample_{i}" for i in range(N)], labels)
    with open(output_label, 'wb') as f:
        pickle.dump(label_tuple, f)

    print(f"✅ 轉換成功！")
    print(f"📊 產生矩陣形狀: {numpy_data.shape} (N, C, T, V, M)")
    print(f"💾 檔案已儲存至: {output_data} 與 {output_label}")

if __name__ == "__main__":
    main()
