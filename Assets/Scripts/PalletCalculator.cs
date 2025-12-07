using UnityEngine;

public class PalletCalculator : MonoBehaviour
{
    public Transform palletStartCorner; // 托盘角落 (空物体)

    // 【新增】把箱子尺寸提出来做成变量，方便修改和画图
    public float boxSize = 0.5f;

    // 我们只需要这一个函数，输入 0 就会得到第一个点
    public Vector3 GetDropPosition(int index)
    {
        float boxHalfSize = boxSize / 2f; // 中心点偏移量

        // ================================
        // 【新增：加入 Y 轴 = 箱子高度一半】
        // ================================
        Vector3 localPos = new Vector3(
            boxHalfSize,
            boxHalfSize,  // ← 新增：让 Y 轴抬高半个箱子
            boxHalfSize
        );

        // 转成世界坐标
        return palletStartCorner.TransformPoint(localPos);
    }

    // ========================================================
    // 【新增】在 Scene 视图绘制调试图形
    // ========================================================
    void OnDrawGizmos()
    {
        if (palletStartCorner == null) return;

        Gizmos.color = Color.green;
        Gizmos.matrix = palletStartCorner.localToWorldMatrix;

        float boxHalfSize = boxSize / 2f;

        // ================================
        // 【新增：让 Gizmos 的中心点也包含 Y 轴】
        // ================================
        Vector3 localCenter = new Vector3(
            boxHalfSize,
            boxHalfSize, // ← 新增：让画出来的方块也在正确高度
            boxHalfSize
        );

        Gizmos.DrawWireCube(localCenter, new Vector3(boxSize, boxSize, boxSize));
    }
}
