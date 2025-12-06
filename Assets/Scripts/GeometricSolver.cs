using UnityEngine;

[ExecuteInEditMode]
public class GeometricSolver : MonoBehaviour
{
    [Header("1. 核心参考系 (必须赋值)")]
    [Tooltip("机器人的底座基座，必须是场景里完全不动的那个物体，不能是会旋转的J1")]
    public Transform robotStaticBase;

    [Header("2. 骨骼引用")]
    public Transform j1Base;      // 旋转轴
    public Transform j2Shoulder;  // 大臂
    public Transform j3Elbow;     // 小臂
    public Transform j5Wrist;     // 手腕
    public Transform gripperTip;  // 抓取点

    [Header("3. 角度校准 (需手动调试)")]
    public float j2Offset = 0f;
    public float j3Offset = 0f;
    public float j5Offset = 0f;

    [Header("4. 输出 (只读)")]
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

    // ============================================================
    //  核心解算函数
    // ============================================================
    public void Solve(Vector3 targetWorldPos, Transform ignored1, Transform ignored2)
    {
        if (robotStaticBase == null)
        {
            Debug.LogError("【严重错误】请在 GeometricSolver 中赋值 Robot Static Base！");
            return;
        }

        if (armLen1 == 0) InitializeLengths();

        // ---------------------------------------------------------
        // 第一步：修复 J1 乱动问题
        // 使用 "静态底座" 计算角度，而不是用 "当前J1"
        // ---------------------------------------------------------

        // 1. 算出目标点相对于 "静态底座" 的向量
        Vector3 vecToTarget = targetWorldPos - robotStaticBase.position;

        // 2. 转到底座的局部坐标系 (防止底座本身也是歪的)
        Vector3 localDir = robotStaticBase.InverseTransformDirection(vecToTarget);

        // 3. 计算角度 (Atan2 只看 X 和 Z)
        // 在 Step 2 中，因为 X 和 Z 没变，这个 theta1 算出来的值将恒定不变！
        float theta1 = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

        outAngles[0] = theta1;

        // ---------------------------------------------------------
        // 第二步：解算 J2, J3 (2D平面)
        // ---------------------------------------------------------

        // 1. 计算 Wrist (手腕) 的目标位置
        // 为了让夹爪垂直向下，手腕位置 = 目标点 + 向上偏移手长
        Vector3 wristTarget = targetWorldPos + Vector3.up * handLen;

        // 2. 将 3D 坐标投影到 J2 的 2D 垂直切面上
        // r = 水平距离 (J2 到 Wrist 的水平距离)
        // y = 垂直高度 (J2 到 Wrist 的垂直高差)
        Vector3 j2Pos = j2Shoulder.position;

        // 这里必须用 Vector2.Distance 来算水平距离，忽略 Y 轴影响
        float r = Vector2.Distance(new Vector2(wristTarget.x, wristTarget.z), new Vector2(j2Pos.x, j2Pos.z));
        float y = wristTarget.y - j2Pos.y;

        // 3. 余弦定理 (Law of Cosines)
        float c = Mathf.Sqrt(r * r + y * y); // J2 到 Wrist 的直线距离
        c = Mathf.Clamp(c, 0.01f, armLen1 + armLen2 - 0.001f); // 防穿透

        float alphaCos = (armLen1 * armLen1 + c * c - armLen2 * armLen2) / (2 * armLen1 * c);
        float betaCos = (armLen1 * armLen1 + armLen2 * armLen2 - c * c) / (2 * armLen1 * armLen2);

        float alpha = Mathf.Acos(Mathf.Clamp(alphaCos, -1f, 1f)) * Mathf.Rad2Deg;
        float beta = Mathf.Acos(Mathf.Clamp(betaCos, -1f, 1f)) * Mathf.Rad2Deg;

        // 4. 转换为电机角度
        float phi = Mathf.Atan2(y, r) * Mathf.Rad2Deg; // 仰角

        // 这里的正负号取决于你的模型建模方向
        // 通常：J2 = 仰角 + alpha, J3 = 180 - beta (外折)
        float rawJ2 = phi + alpha;
        float rawJ3 = -(180 - beta); // 负号代表手肘向上弯

        // ---------------------------------------------------------
        // 第三步：应用 Offset 并输出
        // ---------------------------------------------------------
        outAngles[1] = rawJ2 + j2Offset;
        outAngles[2] = rawJ3 + j3Offset;

        // ---------------------------------------------------------
        // 第四步：J5 平衡 (Leveling)
        // J5 = -(J2 + J3) 抵消旋转，保持末端水平
        // ---------------------------------------------------------
        outAngles[4] = -(rawJ2 + rawJ3) + j5Offset;
    }

    // 画线调试
    void OnDrawGizmos()
    {
        if (robotStaticBase && gripperTip)
        {
            // 画出基座到目标的向量 (J1 计算依据)
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(robotStaticBase.position, gripperTip.position);
        }
    }
}