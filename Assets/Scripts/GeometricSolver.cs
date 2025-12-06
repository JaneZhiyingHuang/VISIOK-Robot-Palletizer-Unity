using UnityEngine;

[ExecuteInEditMode]
public class GeometricSolver : MonoBehaviour
{
    [Header("1. 核心引用 (必须赋值)")]
    [Tooltip("必须是场景里完全不动的那个底座！如果没有父物体，就建一个空物体包住整个机器人，然后拖那个空物体。")]
    public Transform robotStaticBase;

    [Header("2. 骨骼引用")]
    public Transform j1Base;
    public Transform j2Shoulder;
    public Transform j3Elbow;
    public Transform j5Wrist;
    public Transform gripperTip;

    [Header("3. 关键：角度校准")]
    [Tooltip("J1 的额外修正值。如果机器人指歪了，调整这个。")]
    public float j1Offset = 0f;
    public float j2Offset = 0f;
    public float j3Offset = 0f;
    public float j5Offset = 0f;

    [Header("4. 输出")]
    public float[] outAngles = new float[6];

    // 内部变量
    private float armLen1, armLen2, handLen;

    void Start() { InitializeLengths(); }

    [ContextMenu("初始化臂长")]
    public void InitializeLengths()
    {
        if (j2Shoulder && j3Elbow && j5Wrist && gripperTip)
        {
            armLen1 = Vector3.Distance(j2Shoulder.position, j3Elbow.position);
            armLen2 = Vector3.Distance(j3Elbow.position, j5Wrist.position);
            handLen = Vector3.Distance(j5Wrist.position, gripperTip.position);
        }
    }

    public void Solve(Vector3 targetWorldPos, Transform ignored1, Transform ignored2)
    {
        if (robotStaticBase == null) return;
        if (armLen1 == 0) InitializeLengths();

        // =========================================================
        // Step A: J1 解算 (修复乱动与角度不对的核心)
        // =========================================================

        // 1. 获取相对于【完全不动的基座】的向量
        // 这一步保证了：只要目标 x,z 不变，dirLocal.x, z 就永远不变
        Vector3 dirLocal = robotStaticBase.InverseTransformPoint(targetWorldPos);

        // 2. 使用 Atan2(x, z) -> Unity 标准 forward 是 Z 轴
        // 结果范围：-180 到 180
        float mathAngleJ1 = Mathf.Atan2(dirLocal.x, dirLocal.z) * Mathf.Rad2Deg;

        // 3. 应用 Offset (把数学角度对齐到你的模型角度)
        outAngles[0] = mathAngleJ1 + j1Offset;

        // =========================================================
        // Step B: J2/J3 解算 (高度与伸展)
        // =========================================================

        // 1. 目标修正：Wrist 的位置 = 抓取点向上退回 HandLength
        Vector3 wristTarget = targetWorldPos + Vector3.up * handLen;

        // 2. 投影到 2D 截面
        // 计算水平距离 r (忽略 Y 轴)
        Vector2 flatDiff = new Vector2(wristTarget.x - j2Shoulder.position.x, wristTarget.z - j2Shoulder.position.z);
        float r = flatDiff.magnitude;
        float y = wristTarget.y - j2Shoulder.position.y;

        // 3. 余弦定理
        float c = Mathf.Sqrt(r * r + y * y);
        c = Mathf.Clamp(c, 0.01f, armLen1 + armLen2 - 0.001f);

        float alphaCos = (armLen1 * armLen1 + c * c - armLen2 * armLen2) / (2 * armLen1 * c);
        float betaCos = (armLen1 * armLen1 + armLen2 * armLen2 - c * c) / (2 * armLen1 * armLen2);

        float alpha = Mathf.Acos(Mathf.Clamp(alphaCos, -1f, 1f)) * Mathf.Rad2Deg;
        float beta = Mathf.Acos(Mathf.Clamp(betaCos, -1f, 1f)) * Mathf.Rad2Deg;

        // 4. 转换物理角度
        float phi = Mathf.Atan2(y, r) * Mathf.Rad2Deg;

        outAngles[1] = (phi + alpha) + j2Offset;   // J2
        outAngles[2] = -(180 - beta) + j3Offset;   // J3 (假设 Elbow Up)

        // =========================================================
        // Step C: J5 平行维持
        // =========================================================
        // 这里的逻辑：抵消 J2 和 J3 的旋转
        // 如果 J2 +Offset 是实际物理角度，这里可能要反向抵消
        // 简化版：只要让末端水平，J5 = -(J2 + J3)
        // 注意：这里用不带 Offset 的基础数学趋势计算更准，或者手动调 offset
        outAngles[4] = -((phi + alpha) + (-(180 - beta))) + j5Offset;
    }

    // =========================================================
    // 可视化辅助线 (非常重要)
    // =========================================================
    void OnDrawGizmos()
    {
        if (robotStaticBase == null || gripperTip == null) return;

        // 1. 画一条线，显示机器人【认为】目标在哪里 (J1 朝向)
        // 这条线如果歪了，说明 J1 Offset 不对
        Gizmos.color = Color.magenta;
        Vector3 targetDir = Quaternion.Euler(0, outAngles[0], 0) * robotStaticBase.forward;
        Gizmos.DrawRay(j1Base.position, targetDir * 2f);

        // 2. 画一条线连接基座和真实目标
        Gizmos.color = Color.green;
        Gizmos.DrawLine(robotStaticBase.position, gripperTip.position);
    }
}