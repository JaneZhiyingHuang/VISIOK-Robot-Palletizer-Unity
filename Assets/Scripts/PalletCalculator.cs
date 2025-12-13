using UnityEngine;
using System.Collections.Generic;

public class PalletCalculator : MonoBehaviour
{
    // ==========================================
    // 定义朝向枚举
    // ==========================================
    public enum BoxOrientation
    {
        [InspectorName("Rotate 90°")] Align_X,
        [InspectorName("Default")] Align_Z_Rotated
    }

    [Header("核心引用")]
    [Tooltip("托盘的左下角 (排列起始点)")]
    public Transform palletStartCorner;

    [Header("托盘设置")]
    [Tooltip("托盘有效区域大小 (x=长, y=深/Z)")]
    public Vector2 palletSize = new Vector2(1.2f, 1.0f); 

    [Tooltip("箱子之间的间隙")]
    public float gap = 0.01f;

    [Header("箱子设置")]
    [Tooltip("请拖入场景里的一个箱子")]
    public Transform boxReference;

    [Header("尺寸与朝向")]
    [Tooltip("读取到的原始尺寸")]
    public Vector3 rawDimensions = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("选择箱子在托盘上的放置朝向")]
    public BoxOrientation placementOrientation = BoxOrientation.Align_X;

    // 用于在Inspector里显示计算结果 (只读)
    [Header("计算结果预览 (只读)")]
    [SerializeField] private int _capacityX;
    [SerializeField] private int _capacityZ;
    [SerializeField] private int _totalCapacity;

    // ========================================================
    // 1. 获取最终计算用的尺寸 (根据朝向处理)
    // ========================================================
    private Vector3 GetFinalBoxSize()
    {
        // 如果选了“旋转90度”，就交换 X 和 Z
        // 注意：Y (高度) 保持不变
        if (placementOrientation == BoxOrientation.Align_Z_Rotated)
        {
            return new Vector3(rawDimensions.z, rawDimensions.y, rawDimensions.x);
        }
        // 否则返回原始尺寸
        return rawDimensions;
    }

    // ========================================================
    // 2. 核心计算逻辑：算出所有合法的放置点位
    // ========================================================
    public List<Vector3> CalculateAllPoints()
    {
        List<Vector3> points = new List<Vector3>();

        if (palletStartCorner == null) return points;

        Vector3 boxSize = GetFinalBoxSize();

        // 避免除以0
        if (boxSize.x <= 0.01f || boxSize.z <= 0.01f) return points;

        // 计算步长 (尺寸 + 间隙)
        float stepX = boxSize.x + gap;
        float stepZ = boxSize.z + gap;

        // 计算能放几个 (托盘尺寸 / 步长) -> 向下取整
        // 稍微加一点点容差 0.001f 防止浮点数精度问题导致本来能放下的没放下
        int countX = Mathf.FloorToInt((palletSize.x + 0.001f) / stepX);
        int countZ = Mathf.FloorToInt((palletSize.y + 0.001f) / stepZ);

        // 更新 Inspector 显示，方便调试
        _capacityX = countX;
        _capacityZ = countZ;
        _totalCapacity = countX * countZ;

        // 循环生成局部坐标，然后转世界坐标
        for (int z = 0; z < countZ; z++)
        {
            for (int x = 0; x < countX; x++)
            {
                // 计算中心点坐标
                // 坐标 = (索引 * 步长) + (箱子尺寸的一半)
                float localX = (x * stepX) + (boxSize.x / 2f);
                float localZ = (z * stepZ) + (boxSize.z / 2f);
                float localY = boxSize.y / 2f;

                Vector3 localPos = new Vector3(localX, localY, localZ);

                // 将局部坐标转换为世界坐标 (跟随 StartCorner 的旋转)
                Vector3 worldPos = palletStartCorner.TransformPoint(localPos);
                points.Add(worldPos);
            }
        }

        return points;
    }

    // ========================================================
    // 3. 供外部调用的简便接口
    // ========================================================
    public Vector3 GetDropPosition(int index)
    {
        List<Vector3> allPoints = CalculateAllPoints();
        if (index >= 0 && index < allPoints.Count)
        {
            return allPoints[index];
        }

        Debug.LogWarning($"请求的索引 {index} 超出了托盘容量 {_totalCapacity}");
        return Vector3.zero; // 或者返回最后一个点
    }

    // ========================================================
    // 【新增】获取当前摆放模式需要的旋转角度 (Y轴)
    // ========================================================
    public float GetCurrentRotationY()
    {
        // 如果枚举选的是 Rotate 90，就返回 90度，否则 0度
        return placementOrientation == BoxOrientation.Align_Z_Rotated ? 0f : 90f;
    }

    // ========================================================
    // 4. 自动读取 (逻辑保持不变)
    // ========================================================
    [ContextMenu("自动读取箱子尺寸 (Auto Detect)")]
    public void AutoDetectBoxSize()
    {
        if (boxReference == null)
        {
            Debug.LogError("❌ 请先拖入 'Box Reference'！");
            return;
        }

        // 优先读取 Renderer (视觉包围盒)
        Renderer ren = boxReference.GetComponent<Renderer>();
        if (ren != null)
        {
            rawDimensions = ren.bounds.size;
            // 某些情况下 bounds 是世界坐标下的，如果箱子本身被旋转了，读取的 x/z 可能不准
            // 如果需要更严谨，应该读取 MeshFilter.sharedMesh.bounds 然后乘缩放
            // 但这里保持你原有的逻辑
            Debug.Log($"✅ [Renderer] 读取原始尺寸: {rawDimensions}");
            return;
        }

        // 其次读取 Collider
        Collider col = boxReference.GetComponent<Collider>();
        if (col != null)
        {
            rawDimensions = col.bounds.size;
            Debug.Log($"✅ [Collider] 读取原始尺寸: {rawDimensions}");
            return;
        }
    }

    // ========================================================
    // 5. 可视化 (修正了预览逻辑)
    // ========================================================
    void OnDrawGizmos()
    {
        if (palletStartCorner == null) return;

        // A. 画出托盘范围 (红色虚线框)
        Gizmos.color = new Color(1, 0, 0, 0.3f); // 半透明红
        Gizmos.matrix = palletStartCorner.localToWorldMatrix;
        // 托盘中心点 (局部)
        Vector3 palletCenter = new Vector3(palletSize.x / 2f, 0, palletSize.y / 2f);
        Gizmos.DrawWireCube(palletCenter, new Vector3(palletSize.x, 0.05f, palletSize.y));

        // B. 画出所有计算出的箱子 (绿色实线框)
        Gizmos.color = Color.green;
        // 注意：CalculateAllPoints 返回的是世界坐标，所以我们要重置 Matrix 为 Identity
        Gizmos.matrix = Matrix4x4.identity;

        // 获取点位列表（这会顺便触发一次计算，更新 capacity 变量）
        List<Vector3> points = CalculateAllPoints();
        Vector3 finalSize = GetFinalBoxSize();

        foreach (var pos in points)
        {
            // 在每个点的位置画一个框
            // 我们需要让框跟随 StartCorner 的旋转方向
            Gizmos.matrix = Matrix4x4.TRS(pos, palletStartCorner.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, finalSize);
        }
    }
}