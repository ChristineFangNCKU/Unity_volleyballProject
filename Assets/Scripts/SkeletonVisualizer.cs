using UnityEngine;
using System.Collections.Generic;

public class SkeletonVisualizer : MonoBehaviour
{
    [Header("Skeleton Settings")]
    public Color boneColor = Color.green;
    public float boneThickness = 0.03f;

    private List<LineRenderer> boneLines = new List<LineRenderer>();

    // COCO 17 Keypoints Bone Connections
    private readonly int[,] bones = new int[,]
    {
        {0,1}, {1,3}, {0,2}, {2,4}, // Face/Head
        {5,6}, {5,11}, {6,12}, {11,12}, // Torso
        {5,7}, {7,9}, // Left Arm
        {6,8}, {8,10}, // Right Arm
        {11,13}, {13,15}, // Left leg
        {12,14}, {14,16} // Right leg
    };

    void Awake()
    {
        // Initialize LineRenderers for the 16 bones
        for (int i = 0; i < bones.GetLength(0); i++)
        {
            GameObject lineObj = new GameObject($"Bone_{i}");
            lineObj.transform.SetParent(this.transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            
            // Use standard diffuse/color material
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = boneColor;
            lr.endColor = boneColor;
            lr.startWidth = boneThickness;
            lr.endWidth = boneThickness;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.enabled = false;
            
            boneLines.Add(lr);
        }
    }

    public void UpdateSkeleton(List<List<float>> kpts)
    {
        if (kpts == null || kpts.Count < 17)
        {
            HideSkeleton();
            return;
        }

        for (int i = 0; i < bones.GetLength(0); i++)
        {
            int p1_idx = bones[i, 0];
            int p2_idx = bones[i, 1];

            List<float> p1_data = kpts[p1_idx];
            List<float> p2_data = kpts[p2_idx];

            // If either point is [0,0,0] (Missing data masked by Python), don't draw this bone
            if (IsMissing(p1_data) || IsMissing(p2_data))
            {
                boneLines[i].enabled = false;
                continue;
            }

            // Convert raw data [x, y, z] to Unity coordinates (x, z, y)
            Vector3 pos1 = new Vector3(p1_data[0], p1_data[2], p1_data[1]);
            Vector3 pos2 = new Vector3(p2_data[0], p2_data[2], p2_data[1]);

            boneLines[i].SetPosition(0, pos1);
            boneLines[i].SetPosition(1, pos2);
            boneLines[i].enabled = true;
        }
    }

    public void HideSkeleton()
    {
        foreach (var lr in boneLines)
        {
            if (lr != null) lr.enabled = false;
        }
    }

    private bool IsMissing(List<float> pt)
    {
        if (pt == null || pt.Count < 3) return true;
        // Our Python script masked missing values as [0.0, 0.0, 0.0]
        return (Mathf.Abs(pt[0]) < 0.001f && Mathf.Abs(pt[1]) < 0.001f && Mathf.Abs(pt[2]) < 0.001f);
    }
}