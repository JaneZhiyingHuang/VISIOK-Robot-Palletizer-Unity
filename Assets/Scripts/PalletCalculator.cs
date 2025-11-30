using UnityEngine;

public class PalletCalculator : MonoBehaviour
{
    public Transform palletStartCorner; // 托盘角落 (空物体)

    // 我们只需要这一个函数，输入 0 就会得到第一个点
    public Vector3 GetDropPosition(int index)
    {
        // 假设放在第 1 行 第 1 列 (index = 0)
        // 如果你有更多箱子，这里以后改逻辑就行
        // 现在的逻辑：直接返回 Corner 的位置 (稍微偏移一点中心)

        float boxHalfSize = 0.25f; // 假设箱子宽0.5，中心点偏移就是0.25

        Vector3 localPos = new Vector3(boxHalfSize, 0, boxHalfSize);

        return palletStartCorner.TransformPoint(localPos);
    }
}