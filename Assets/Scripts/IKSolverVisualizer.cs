using UnityEngine;

[ExecuteInEditMode]
public class IKSolverVisualizer : MonoBehaviour
{
    [Header("1. 引用关键骨骼")]
    public Transform j1Base;      // J1 骨骼
    public Transform j2Shoulder;  // J2 骨骼
    public Transform j3Elbow;     // J3 骨骼
    public Transform j6Hand;      // 吸盘

    [Header("2. 目标点 (红球)")]
    public Transform target;      // IK_Target

    [Header("3. 机械臂尺寸")]
    public float arm1Length = 1.05f;
    public float arm2Length = 1.774f;

    [Header("4. 【核心调试】拖动这些滑块！")]
    [Range(-180, 180)] public float j1_Offset = 0f;
    [Range(-180, 180)] public float j2_Offset = 0f;
    [Range(-180, 180)] public float j3_Offset = 0f;

    [Header("5. 逻辑开关")]
    public bool j1_RotateAroundZ = true; // J1 是绕 Z 转吗？
    public bool invert_Elbow = false;    // 胳膊肘反向？

    // 内部计算变量
    private float theta1, theta2, theta3;

    void Update()
    {
        if (target == null || j1Base == null || j2Shoulder == null) return;
        SolveMath();
    }

    void SolveMath()
    {
        // --- A. 算 J1 (底座) ---
        // 把目标转到 J1 局部坐标
        Vector3 localT = j1Base.parent.InverseTransformPoint(target.position);

        // 根据你的描述，J1 绕 Z 转，所以看 X,Y 平面
        if (j1_RotateAroundZ)
            theta1 = Mathf.Atan2(localT.y, localT.x) * Mathf.Rad2Deg;
        else
            theta1 = Mathf.Atan2(localT.x, localT.z) * Mathf.Rad2Deg;

        // --- B. 算三角形 (J2, J3) ---
        // 1. 距离计算
        float dist = Vector3.Distance(j2Shoulder.position, target.position);
        // 限制长度
        dist = Mathf.Clamp(dist, 0.01f, arm1Length + arm2Length - 0.001f);

        // 2. 内角计算 (余弦定理)
        // a=arm1, b=arm2, c=dist
        // Cos(A) = (b^2 + c^2 - a^2) / 2bc
        float cosAlpha = (arm1Length * arm1Length + dist * dist - arm2Length * arm2Length) / (2 * arm1Length * dist);
        float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)) * Mathf.Rad2Deg;

        // Cos(C) = (a^2 + b^2 - c^2) / 2ab
        float cosGamma = (arm1Length * arm1Length + arm2Length * arm2Length - dist * dist) / (2 * arm1Length * arm2Length);
        float gamma = Mathf.Acos(Mathf.Clamp(cosGamma, -1f, 1f)) * Mathf.Rad2Deg;

        // 3. 仰角计算
        // 投影到 J1 的旋转平面
        Vector3 j1Axis = j1_RotateAroundZ ? j1Base.parent.forward : j1Base.parent.up;
        Vector3 toTarget = target.position - j2Shoulder.position;
        float yDist = Vector3.Dot(toTarget, j1Axis); // 垂直高度
        float xDist = Mathf.Sqrt(dist * dist - yDist * yDist); // 水平距离
        float beta = Mathf.Atan2(yDist, xDist) * Mathf.Rad2Deg;

        // --- C. 组合最终角度 ---
        theta2 = beta + alpha; // 默认抬起逻辑
        theta3 = gamma - 180;  // 默认弯曲逻辑

        if (invert_Elbow)
        {
            theta2 = beta - alpha;
            theta3 = 180 - gamma;
        }
    }

    // ========================================================
    // 🎨 画出 "幽灵手臂" (Ghost Arm)
    // ========================================================
    void OnDrawGizmos()
    {
        if (j1Base == null || j2Shoulder == null) return;

        // 1. 模拟 J1 旋转
        // 基准旋转 (父级)
        Quaternion baseRot = j1Base.parent.rotation;
        // 计算 J1 的旋转 (加上 Offset)
        Vector3 j1AxisVec = j1_RotateAroundZ ? Vector3.forward : Vector3.up;
        Quaternion q1 = Quaternion.AngleAxis(theta1 + j1_Offset, j1AxisVec);
        Quaternion rotJ1 = baseRot * q1;

        // 2. 模拟 J2 旋转 (肩膀)
        // 假设 J2 绕 X 轴 (Right) 抬起
        Quaternion q2 = Quaternion.AngleAxis(theta2 + j2_Offset, Vector3.right);
        Quaternion rotJ2 = rotJ1 * q2;

        // 算出一根 "虚拟大臂" 的向量
        // 假设骨头是沿着 Y 轴 (Up) 长的。如果你的模型骨头是横着长的，这里会画歪
        Vector3 arm1Vec = rotJ2 * Vector3.up * arm1Length;
        Vector3 elbowPos = j2Shoulder.position + arm1Vec;

        // 3. 模拟 J3 旋转 (肘部)
        Quaternion q3 = Quaternion.AngleAxis(theta3 + j3_Offset, Vector3.right);
        Quaternion rotJ3 = rotJ2 * q3;

        Vector3 arm2Vec = rotJ3 * Vector3.up * arm2Length;
        Vector3 handPos = elbowPos + arm2Vec;

        // --- 开始画线 ---
        Gizmos.color = Color.white; // 幽灵手臂是白色的

        // 画大臂
        Gizmos.DrawLine(j2Shoulder.position, elbowPos);
        Gizmos.DrawWireSphere(elbowPos, 0.05f);

        // 画小臂
        Gizmos.DrawLine(elbowPos, handPos);
        Gizmos.DrawWireSphere(handPos, 0.05f);

        // 画目标连线 (检查是否对齐)
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(handPos, target.position);
        }
    }
}