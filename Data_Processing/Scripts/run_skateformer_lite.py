import torch
import torch.nn as nn
import torch.optim as optim
import numpy as np
import pickle
import matplotlib.pyplot as plt
import os

# ==========================================
# 1. 載入黃金資料 (N, C, T, V, M)
# ==========================================
data_path = '../GNN_Dataset/train_data.npy'
label_path = '../GNN_Dataset/train_label.pkl'

print("📦 正在將資料送入煉丹爐...")
X_raw = np.load(data_path)  # (50, 3, 90, 17, 1)

with open(label_path, 'rb') as f:
    sample_names, y_raw = pickle.load(f)

# 轉換為 PyTorch 張量
# 拔掉 M (人數) 維度，因為我們只有 1 個人，變成 (N, C, T, V)
X = torch.tensor(X_raw[:, :, :, :, 0], dtype=torch.float32) 
y = torch.tensor(y_raw, dtype=torch.long)

print(f"✅ 資料載入完成！準備進入 Transformer 核心... 特徵維度: {X.shape}")

# ==========================================
# 2. 打造 SkateFormer 核心 (空間-時間注意力機制)
# ==========================================
class SkateFormerLite(nn.Module):
    def __init__(self, num_classes=3, in_channels=3, num_frames=90, num_joints=17, embed_dim=64):
        super().__init__()
        
        # 1. 空間特徵提取 (Spatial Embedding): 將每個關節的 3D 座標轉為高維特徵
        self.joint_embedding = nn.Linear(in_channels, embed_dim)
        
        # 2. 空間聚合 (Spatial Pooling): 將 17 個關節點融合為一個整體的「姿勢特徵」
        #self.spatial_pool = nn.AdaptiveAvgPool1d(1)
        
        # 3. 時間注意力機制 (Temporal Transformer): SkateFormer 的靈魂！
        # 讓模型學會看懂這 90 幀的「前後因果關係」
        encoder_layer = nn.TransformerEncoderLayer(
            d_model=embed_dim, 
            nhead=4, 
            dim_feedforward=128, 
            dropout=0.3, 
            batch_first=True
        )
        self.temporal_transformer = nn.TransformerEncoder(encoder_layer, num_layers=2)
        
        # 4. 分類頭 (Classification Head)
        self.classifier = nn.Sequential(
            nn.Linear(embed_dim, 32),
            nn.ReLU(),
            nn.Dropout(0.3),
            nn.Linear(32, num_classes)
        )
        
    def forward(self, x):
        # x shape: (N, C, T, V) -> 例如 (50, 3, 90, 17)
        N, C, T, V = x.shape
        
        # 轉換維度以符合 Linear 層: (N, T, V, C)
        x = x.permute(0, 2, 3, 1) 
        
        # 空間投影: (N, T, V, 64)
        x = self.joint_embedding(x) 
        
        # 壓縮關節維度 (把 17 個關節融合成 1 個姿勢向量): (N, T, 64, V) -> (N, T, 64)
        #x = x.permute(0, 1, 3, 2)
        #x = self.spatial_pool(x).squeeze(-1) 
        # 壓縮關節維度：直接對 17 個關節點 (dim=2) 取平均！ (N, T, V, 64) -> (N, T, 64)
        x = x.mean(dim=2)
        
        # 進入 Transformer 學習時間動態: (N, T, 64)
        x = self.temporal_transformer(x)
        
        # 時間全局池化 (把 90 幀濃縮成一個最終結果): (N, 64)
        x = x.mean(dim=1) 
        
        # 分類
        out = self.classifier(x)
        return out

model = SkateFormerLite(num_classes=3)
criterion = nn.CrossEntropyLoss()

# 使用較小的 Learning Rate 搭配 AdamW (Transformer 的標配)
optimizer = optim.AdamW(model.parameters(), lr=0.002, weight_decay=1e-4)

# ==========================================
# 3. 啟動煉丹爐
# ==========================================
epochs = 40
losses = []

print(f"\n🔥 SkateFormer-Lite 引擎啟動！開始 {epochs} 輪的注意力機制訓練...")
for epoch in range(epochs):
    model.train()
    optimizer.zero_grad()
    
    outputs = model(X)
    loss = criterion(outputs, y)
    loss.backward()
    optimizer.step()
    
    losses.append(loss.item())
    
    if (epoch+1) % 5 == 0:
        print(f"   📈 Epoch [{epoch+1:02d}/{epochs}] | Attention Loss: {loss.item():.4f}")

# ==========================================
# 4. 產出報告級別的精美圖表
# ==========================================
plt.figure(figsize=(9, 5))
plt.plot(range(1, epochs+1), losses, marker='o', markersize=4, color='#8c564b', linewidth=2.5, label='Training Loss')
plt.title('SkateFormer-Lite: Spatial-Temporal Attention Training', fontsize=14, fontweight='bold')
plt.xlabel('Epochs', fontsize=12)
plt.ylabel('Cross Entropy Loss', fontsize=12)
plt.legend()
plt.grid(True, linestyle='--', alpha=0.6)
plt.tight_layout()

plot_path = '../GNN_Dataset/skateformer_loss.png'
plt.savefig(plot_path, dpi=300)

print(f"\n🎉 煉丹大功告成！")
print(f"📊 模型架構: Spatial Embedding + 2-Layer Temporal Transformer")
print(f"🖼️ 訓練曲線已完美封裝至: {plot_path}")
print("==========================================================")
print("老哥指令：現在立刻打開那張圖，欣賞一下你的傑作，然後馬上給我去睡覺！")