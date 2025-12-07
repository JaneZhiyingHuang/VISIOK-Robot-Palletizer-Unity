using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GeometricSolver : MonoBehaviour
{
    [Header("调试与预览")]
    [Tooltip("拖入目标物体，实时看扇形变化")]
    public Transform previewTarget;
    public bool showGizmos = true;

    [Header("核心引用")]
    public Transform robotStaticBase;
    public Transform j1Base;
    public Transform j2Shoulder;
    public Transform j3Elbow;
    public Transform j5Wrist;
    public Transform gripperTip;

    [Header("配置")]
    [Tooltip("反转肘部弯曲方向")]
    public bool invertElbow = false;

    [Header("计算结果 (相对于初始姿态的增量)")]
    public float[] outAngles = new float[6];

    // --- 内部变量：记录初始状态 ---
    private float L1, L2, L_Hand;

    // 初始的几何参数 (作为 0 度基准)
    [SerializeField, HideInInspector] private float initJ1Angle;    // J1 初始水平角
    [SerializeField, HideInInspector] private float initJ2Elevation; // J2 初始仰角
    [SerializeField, HideInInspector] private float initJ3Inner;     // J3 初始内角

    // 记录初始向量，用于画图 (扇形的起始边)
    private Vector3 visInitJ1Dir;
    private Vector3 visInitJ2Dir;
    private Vector3 visInitJ3Dir;

    private bool isInitialized = false;

    void OnEnable()
    {
        // 脚本激活时，记录当前姿态为 0 度
        RecordZeroPose();
    }

    // 每一帧计算
    void Update()
    {
        if (!isInitialized) RecordZeroPose();

        if (previewTarget != null)
        {
            Solve(previewTarget.position, 0f);
        }
    }

    [ContextMenu("1. 将当前姿态设为 0 度 (重置)")]
    public void RecordZeroPose()
    {
        if (!j2Shoulder || !j3Elbow || !j5Wrist || !gripperTip) return;

        // 1. 初始化臂长
        L1 = Vector3.Distance(j2Shoulder.position, j3Elbow.position);
        L2 = Vector3.Distance(j3Elbow.position, j5Wrist.position);
        L_Hand = Vector3.Distance(j5Wrist.position, gripperTip.position);

        // 2. 记录初始 J1 (底座) 角度 [平面投影]
        Vector3 dirToTip = gripperTip.position - j1Base.position;
        Vector3 flatDir = Vector3.ProjectOnPlane(dirToTip, Vector3.up); // 投影到水平面 (World Y)
        // 记录相对于世界X轴的角度作为基准
        initJ1Angle = Vector3.SignedAngle(Vector3.right, flatDir, Vector3.up);
        visInitJ1Dir = flatDir.normalized;

        // 3. 记录初始 J2 (大臂) 仰角 [几何仰角]
        // 计算初始的水平距离(r)和垂直高度(h)
        float y_diff = gripperTip.position.y - j2Shoulder.position.y;
        float r_dist = flatDir.magnitude; // 近似取末端的水平距离
        // 记录此时的物理仰角
        // 注意：这里我们反向推导初始的几何状态，简化为记录 Wrist 相对于 Shoulder 的仰角
        // 更精确的做法是记录 L1 向量的仰角
        Vector3 j2Dir = j3Elbow.position - j2Shoulder.position;
        float j2FlatLen = new Vector2(j2Dir.x, j2Dir.z).magnitude;
        initJ2Elevation = Mathf.Atan2(j2Dir.y, j2FlatLen) * Mathf.Rad2Deg;
        visInitJ2Dir = j2Dir.normalized;

        // 4. 记录初始 J3 (小臂) 内角
        // 也就是 J2->J3 和 J3->J5 的夹角
        Vector3 v1 = (j2Shoulder.position - j3Elbow.position).normalized;
        Vector3 v2 = (j5Wrist.position - j3Elbow.position).normalized;
        initJ3Inner = Vector3.Angle(v1, v2);
        visInitJ3Dir = (j5Wrist.position - j3Elbow.position).normalized;

        isInitialized = true;
        // Debug.Log("已记录当前姿态为 0 度基准。");
    }

    public bool Solve(Vector3 targetPos, float placeRotationY = 0f)
    {
        if (!isInitialized) return false;

        // =================================================
        // J1: 绕 World Y (水平旋转)
        // =================================================
        Vector3 dirToTarget = targetPos - j1Base.position;
        Vector3 flatDir = Vector3.ProjectOnPlane(dirToTarget, Vector3.up);

        // 计算现在的绝对角度
        float currJ1Angle = Vector3.SignedAngle(Vector3.right, flatDir, Vector3.up);

        // 输出 = 现在 - 初始 (Delta)
        outAngles[0] = (currJ1Angle - initJ1Angle);


        // =================================================
        // J2, J3: 绕 World Z (垂直平面解算)
        // =================================================
        // 1. 几何解算 (Law of Cosines)
        float y_diff = targetPos.y - j2Shoulder.position.y;
        float r_dist = flatDir.magnitude; // 水平距离

        // 目标 Wrist 位置 (吸盘垂直向下逻辑)
        float r_wrist = r_dist;
        float h_wrist = y_diff + L_Hand;

        float c = Mathf.Sqrt(r_wrist * r_wrist + h_wrist * h_wrist);
        if (c > L1 + L2) return false;

        // 2. 计算当前的几何仰角
        float alpha = Mathf.Atan2(h_wrist, r_wrist) * Mathf.Rad2Deg; // 整体仰角
        float cosA = Mathf.Clamp((L1 * L1 + c * c - L2 * L2) / (2 * L1 * c), -1, 1);
        float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg; // J2 内角
        float cosB = Mathf.Clamp((L1 * L1 + L2 * L2 - c * c) / (2 * L1 * L2), -1, 1);
        float angleB = Mathf.Acos(cosB) * Mathf.Rad2Deg; // J3 内角

        // 3. 计算 Delta (增量)

        // J2: 当前需要的仰角 = alpha + angleA
        // 输出 = 当前仰角 - 初始仰角
        float currentElevation = alpha + angleA;
        outAngles[1] = currentElevation - initJ2Elevation;

        // J3: 当前需要的内角 = angleB
        // 输出 = 当前内角 - 初始内角
        // 弯曲方向处理
        float targetInner = angleB;
        if (invertElbow) targetInner = -angleB; // 简单的反转逻辑

        // 注意：J3 通常定义伸直为180或0，这里直接比较“变化量”
        // 初始是 90度，现在算出来需要 90度，那 Delta 就是 0
        // 但要注意方向，内角变小通常意味着弯曲
        outAngles[2] = (targetInner - initJ3Inner);
        // 修正：如果反向折叠，逻辑可能需要反过来，这里先按标准增量走

        // =================================================
        // J5, J6: 跟随逻辑
        // =================================================
        // J4 固定
        outAngles[3] = 0f;

        // J5: 绕 World Z
        // 目标：抵消 J2 和 J3 的增量，保持和初始一样的姿态 (通常是垂直)
        outAngles[4] = -(outAngles[1] + outAngles[2]);

        // J6: 绕 World Y
        // 目标：抵消 J1 的增量
        outAngles[5] = -outAngles[0] + placeRotationY;

        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos || j1Base == null || !isInitialized) return;

        // ----------------------------------------------------
        // 1. J1 可视化 (绕 World Y, 蓝色扇形)
        // ----------------------------------------------------
        // 扇形起点：visInitJ1Dir (初始方向)
        // 旋转轴：Vector3.up (World Y)
        // 角度：outAngles[0]
        DrawSector(j1Base.position, Vector3.up, visInitJ1Dir, -outAngles[0], "J1 (World Y)");


        // ----------------------------------------------------
        // 2. J2 可视化 (绕 World Z, 蓝色扇形)
        // ----------------------------------------------------
        // 这里有个技巧：如果 J1 转了，J2 的平面也转了。
        // 为了画出正确的视觉效果，我们需要把初始向量也旋转一下，或者只在平面内画
        // 但既然你说绕 World Z，那我们就画在 Z 平面上

        // 为了视觉清晰，我们画出 "大臂当前方向" vs "大臂初始方向"
        // 初始方向: visInitJ2Dir
        // 旋转轴: J1转动后的 Right 轴 (或者是 World Z，取决于你的定义)
        // 你的需求：绕世界 Z。那我们就用 Vector3.forward
        // 扇形起点：需要投影到屏幕上或者局部空间
        // 简单画法：以初始大臂向量为起点，画出 J2 的 Delta

        // 修正：为了配合 J1 的旋转，J2 的视觉轴应该是 J1.right (局部 Z)
        // 但严格遵守你的 "World Z"，我们用 Vector3.forward
        DrawSector(j2Shoulder.position, Vector3.forward, visInitJ2Dir, outAngles[1], "J2 (World Z)");


        // ----------------------------------------------------
        // 3. J3 可视化 (绕 World Z)
        // ----------------------------------------------------
        // 视觉起点：小臂的初始朝向
        DrawSector(j3Elbow.position, Vector3.forward, visInitJ3Dir, outAngles[2], "J3 (World Z)");


        // ----------------------------------------------------
        // 4. J5, J6
        // ----------------------------------------------------
        DrawSector(j5Wrist.position, Vector3.forward, Vector3.right, outAngles[4], "J5 (World Z)");
        DrawSector(gripperTip.position, Vector3.up, Vector3.forward, outAngles[5], "J6 (World Y)");

        // 画目标连线
        if (previewTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(j2Shoulder.position, previewTarget.position);
        }
    }

    void DrawSector(Vector3 center, Vector3 axis, Vector3 from, float angle, string label)
    {
        float radius = 0.5f;

        // 1. 扇形填充 (你要求的蓝色)
        Handles.color = new Color(0, 0.5f, 1f, 0.2f); // 浅蓝半透明
        Handles.DrawSolidArc(center, axis, from, angle, radius);

        // 2. 边框
        Handles.color = Color.blue;
        Handles.DrawWireArc(center, axis, from, angle, radius);

        // 3. 起始线 (虚线表示初始位置)
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(center, center + from * radius, 2f);

        // 4. 文字
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.cyan;
        style.fontSize = 14;
        Handles.Label(center + axis * 0.2f, $"{label}: {angle:F1}°", style);
    }
#endif
}