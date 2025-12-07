using UnityEngine;

public class PalletCalculator : MonoBehaviour
{
    // ==========================================
    // 定义朝向枚举
    // ==========================================
    public enum BoxOrientation
    {
        [InspectorName("默认方向 (保持读取的长宽)")] Align_X,
        [InspectorName("旋转90度 (交换长宽)")] Align_Z_Rotated
    }

    [Header("核心引用")]
    public Transform palletStartCorner; // 托盘角落 (参考点)

    [Header("箱子设置")]
    [Tooltip("请拖入场景里的一个箱子")]
    public Transform boxReference;

    [Header("尺寸与朝向")]
    [Tooltip("读取到的原始尺寸")]
    public Vector3 rawDimensions = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("选择箱子在托盘上的放置朝向")]
    public BoxOrientation placementOrientation = BoxOrientation.Align_X;

    // ========================================================
    // 辅助函数：获取最终计算用的尺寸 (根据朝向处理)
    // ========================================================
    private Vector3 GetFinalSize()
    {
        // 如果选了“旋转90度”，就交换 X 和 Z
        if (placementOrientation == BoxOrientation.Align_Z_Rotated)
        {
            return new Vector3(rawDimensions.z, rawDimensions.y, rawDimensions.x);
        }
        // 否则返回原始尺寸
        return rawDimensions;
    }

    // ========================================================
    // 自动读取 (保存到 rawDimensions)
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
    // 计算放置点 (使用 FinalSize)
    // ========================================================
    public Vector3 GetDropPosition(int index)
    {
        Vector3 finalSize = GetFinalSize();

        float halfX = finalSize.x / 2f;
        float halfY = finalSize.y / 2f;
        float halfZ = finalSize.z / 2f;

        Vector3 localPos = new Vector3(halfX, halfY, halfZ);
        return palletStartCorner.TransformPoint(localPos);
    }

    // ========================================================
    // 可视化 (绿框会随选项改变形状)
    // ========================================================
    void OnDrawGizmos()
    {
        if (palletStartCorner == null) return;

        Gizmos.color = Color.green;
        Gizmos.matrix = palletStartCorner.localToWorldMatrix;

        // 获取经过旋转处理后的最终尺寸
        Vector3 finalSize = GetFinalSize();

        Gizmos.DrawWireCube(
            new Vector3(finalSize.x / 2f, finalSize.y / 2f, finalSize.z / 2f),
            finalSize
        );
    }
}