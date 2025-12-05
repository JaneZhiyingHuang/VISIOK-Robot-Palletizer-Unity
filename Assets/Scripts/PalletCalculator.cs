using UnityEngine;

public class PalletCalculator : MonoBehaviour
{
    public Transform palletStartCorner; // 托盘角落 (空物体)

    // 【新增】把箱子尺寸提出来做成变量，方便修改和画图
    public float boxSize = 0.5f;

    // 我们只需要这一个函数，输入 0 就会得到第一个点
    public Vector3 GetDropPosition(int index)
    {
        // 假设放在第 1 行 第 1 列 (index = 0)
        // 如果你有更多箱子，这里以后改逻辑就行

        float boxHalfSize = boxSize / 2f; // 中心点偏移量

        // 计算相对于角落的局部坐标 (向 X 和 Z 方向各平移半个箱子身位)
        Vector3 localPos = new Vector3(boxHalfSize, 0, boxHalfSize);

        // 转成世界坐标
        return palletStartCorner.TransformPoint(localPos);
    }

    // ========================================================
    // 【新增】在 Scene 视图绘制调试图形
    // ========================================================
    void OnDrawGizmos()
    {
        // 如果没拖角落物体，就不画，防止报错
        if (palletStartCorner == null) return;

        // 设置 Gizmos 的颜色 (比如绿色代表正确位置)
        Gizmos.color = Color.green;

        // 【关键一步】设置 Gizmos 的绘制矩阵
        // 这一步是为了让画出来的方块自动跟随 palletStartCorner 的旋转和缩放
        // 这样你就能看出如果托盘歪了，箱子是不是也跟着歪了
        Gizmos.matrix = palletStartCorner.localToWorldMatrix;

        // 计算要绘制的局部中心点 (和 GetDropPosition 里的逻辑一样)
        float boxHalfSize = boxSize / 2f;
        Vector3 localCenter = new Vector3(boxHalfSize, 0, boxHalfSize);

        // 绘制一个线框立方体
        // 参数1：局部中心点
        // 参数2：立方体的尺寸 (长宽高都是 boxSize)
        // 注意：因为我们设置了 matrix，这里的坐标和尺寸都是相对于 palletStartCorner 的
        Gizmos.DrawWireCube(localCenter, new Vector3(boxSize, boxSize, boxSize));
    }
}