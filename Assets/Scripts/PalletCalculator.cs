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

    [Header("堆叠高度设置 (新逻辑)")]
    [Tooltip("允许的最大堆叠高度 (米)")]
    public float safeHeight = 2.0f;

    [Tooltip("想要堆叠的层数 (不会超过最大安全层数)")]
    public int targetLayers = 1;

    [Header("计算结果预览 (只读)")]
    [Tooltip("根据安全高度算出的最大允许层数")]
    [SerializeField] private int _calculatedMaxLayers; 
    [SerializeField] private int _capacityPerLayerX;
    [SerializeField] private int _capacityPerLayerZ;
    [SerializeField] private int _totalBoxes;

    [Header("箱子设置")]
    [Tooltip("请拖入场景里的一个箱子")]
    public Transform boxReference;

    [Header("尺寸与朝向")]
    [Tooltip("读取到的原始尺寸")]
    public Vector3 rawDimensions = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("选择【第一层】箱子在托盘上的放置朝向")]
    public BoxOrientation placementOrientation = BoxOrientation.Align_X;

    // ========================================================
    // 1. 获取特定状态下的箱子尺寸 (Helper)
    // ========================================================
    private Vector3 GetBoxSize(bool isRotated)
    {
        // 如果旋转了，交换 X 和 Z
        if (isRotated)
        {
            return new Vector3(rawDimensions.z, rawDimensions.y, rawDimensions.x);
        }
        return rawDimensions;
    }

    // ========================================================
    // 2. 判断某一层是否需要旋转 (核心交叠逻辑)
    // ========================================================
    public bool IsLayerRotated(int layerIndex)
    {
        // 判断第一层的基础状态
        // 注意：根据你之前的逻辑，Align_Z_Rotated 认为是 "0f" (不转)，Align_X 认为是 "90f" (转)
        // 这里我们要统一逻辑：
        // 假设 Align_Z_Rotated 代表 "形态A"，Align_X 代表 "形态B"

        bool isBaseMode = (placementOrientation == BoxOrientation.Align_Z_Rotated);

        // 如果是偶数层 (0, 2, 4...) -> 保持和第一层一样的状态
        // 如果是奇数层 (1, 3, 5...) -> 取反
        bool useBaseMode = (layerIndex % 2 == 0) ? isBaseMode : !isBaseMode;

        // 如果最终判定为 Align_Z_Rotated 形态，在 GetFinalBoxSize 逻辑里它是 "rawDimensions.z, y, x" (即交换过的)
        // 也就是 IsRotated = True
        return useBaseMode;
    }

    // ========================================================
    // 3. 核心计算逻辑：算出所有合法的放置点位 (支持多层)
    // ========================================================

    public int GetMaxSafeLayers()
    {
        if (rawDimensions.y <= 0.001f) return 1;
        return Mathf.FloorToInt(safeHeight / rawDimensions.y);
    }

    public List<Vector3> CalculateAllPoints()
    {
        List<Vector3> points = new List<Vector3>();

        if (palletStartCorner == null) return points;
        if (rawDimensions.y <= 0.001f) return points; // 防止高度为0死循环

        // A. 计算最大允许层数
        _calculatedMaxLayers = GetMaxSafeLayers();

        // B. 确定实际要生成的层数 (取 Min)
        int actualLayers = Mathf.Min(targetLayers, _calculatedMaxLayers);
        if (actualLayers < 1) actualLayers = 1;

        // 统计总数用
        int totalCount = 0;

        // --- 外层循环：控制高度 (Y轴) ---
        for (int layer = 0; layer < actualLayers; layer++)
        {
            // 1. 判断这一层是否旋转
            bool isRotated = IsLayerRotated(layer);
            Vector3 currentBoxSize = GetBoxSize(isRotated);

            // 2. 避免尺寸异常
            if (currentBoxSize.x <= 0.01f || currentBoxSize.z <= 0.01f) continue;

            // 3. 计算这一层的步长和容量
            float stepX = currentBoxSize.x + gap;
            float stepZ = currentBoxSize.z + gap;
            int countX = Mathf.FloorToInt((palletSize.x + 0.001f) / stepX);
            int countZ = Mathf.FloorToInt((palletSize.y + 0.001f) / stepZ);

            // 只是为了 Inspector 预览第一层的数据 
            if (layer == 0)
            {
                _capacityPerLayerX = countX;
                _capacityPerLayerZ = countZ;
            }

            // --- 内层循环：控制平面 (X/Z轴) ---
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    // 计算局部坐标
                    float localX = (x * stepX) + (currentBoxSize.x / 2f);
                    float localZ = (z * stepZ) + (currentBoxSize.z / 2f);

                    // Y轴高度 = (层数 * 箱高) + (半个箱高)
                    float localY = (layer * rawDimensions.y) + (currentBoxSize.y / 2f);

                    Vector3 localPos = new Vector3(localX, localY, localZ);
                    Vector3 worldPos = palletStartCorner.TransformPoint(localPos);

                    points.Add(worldPos);
                    totalCount++;
                }
            }
        }

        _totalBoxes = totalCount;
        return points;
    }

    // ========================================================
    // 4. 供外部调用的简便接口
    // ========================================================
    public Vector3 GetDropPosition(int index)
    {
        List<Vector3> allPoints = CalculateAllPoints();
        if (index >= 0 && index < allPoints.Count)
        {
            return allPoints[index];
        }
        return Vector3.zero;
    }

    public float GetCurrentRotationY()
    {
        return placementOrientation == BoxOrientation.Align_Z_Rotated ? 0f : 90f;
    }

    public float GetRotationForLayer(int layerIndex)
    {
        bool isRotated = IsLayerRotated(layerIndex);

        return isRotated ? 0f : 90f;
    }

    // ========================================================
    // 5. 自动读取 
    // ========================================================
    [ContextMenu("自动读取箱子尺寸 (Auto Detect)")]
    public void AutoDetectBoxSize()
    {
        if (boxReference == null)
        {
            Debug.LogError("❌ 请先拖入 'Box Reference'！");
            return;
        }

        Renderer ren = boxReference.GetComponent<Renderer>();
        if (ren != null)
        {
            rawDimensions = ren.bounds.size;
            Debug.Log($"✅ [Renderer] 读取原始尺寸: {rawDimensions}");
            return;
        }

        Collider col = boxReference.GetComponent<Collider>();
        if (col != null)
        {
            rawDimensions = col.bounds.size;
            Debug.Log($"✅ [Collider] 读取原始尺寸: {rawDimensions}");
            return;
        }
    }

    // ========================================================
    // 6. 可视化 
    // ========================================================
    void OnDrawGizmos()
    {
        if (palletStartCorner == null) return;

        // A. 画出托盘范围
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.matrix = palletStartCorner.localToWorldMatrix;
        Vector3 palletCenter = new Vector3(palletSize.x / 2f, 0, palletSize.y / 2f);
        Gizmos.DrawWireCube(palletCenter, new Vector3(palletSize.x, 0.05f, palletSize.y));

        // B. 画出所有计算出的箱子
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.identity;

        List<Vector3> points = CalculateAllPoints();

        float boxHeight = rawDimensions.y;
        if (boxHeight < 0.001f) return;

        foreach (var pos in points)
        {
            // 反算当前是第几层 (根据 Y 轴高度)
            // 世界坐标 Y 减去 托盘Y 再除以 箱高
            float relativeY = pos.y - palletStartCorner.position.y;
            int layerIndex = Mathf.FloorToInt(relativeY / boxHeight);

            // 获取这一层对应的尺寸
            bool isRotated = IsLayerRotated(layerIndex);
            Vector3 size = GetBoxSize(isRotated);

            // 绘制
            float rotAngle = GetRotationForLayer(layerIndex);

            // 构造一个在这个位置、并且旋转了对应角度的 Matrix
            Quaternion rotation = palletStartCorner.rotation * Quaternion.Euler(0, rotAngle, 0);
            Gizmos.matrix = Matrix4x4.TRS(pos, rotation, Vector3.one);

            // 用交换过的 size，只跟随托盘旋转
            Gizmos.matrix = Matrix4x4.TRS(pos, palletStartCorner.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}