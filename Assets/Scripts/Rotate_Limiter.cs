using UnityEngine;

public class Rotate_Limiter : MonoBehaviour
{
    // 假设 J3 是绕 X 轴弯曲的
    // 下垂模式：通常意味着角度必须 > 0 (0 到 150度)
    // 如果它变成负数 (比如 -10度)，那就是往上翘了

    [Range(0f, 160f)]
    public float maxBendAngle = 130f; // 最大弯曲多少度

    void LateUpdate()
    {
        // 获取当前局部旋转
        Vector3 currentRot = transform.localEulerAngles;

        // 转换角度逻辑 (-180 到 180)
        float angleX = currentRot.x;
        if (angleX > 180) angleX -= 360;

        // ==========================================
        // 核心逻辑：禁止负数 (禁止往上翘)
        // ==========================================
        if (angleX < 0)
        {
            angleX = 0; // 强行拉回水平线
        }

        // 限制最大弯曲
        if (angleX > maxBendAngle)
        {
            angleX = maxBendAngle;
        }

        // 应用回去 (保持 Y 和 Z 不变)
        transform.localRotation = Quaternion.Euler(angleX, currentRot.y, currentRot.z);
    }
}