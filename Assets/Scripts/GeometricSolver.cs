using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // 必须引入，用于画蓝色扇形
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

    // --- 内部变量 ---
    private float L1, L2, L_Hand;

    // 记录“初始状态”下的数学理论角度
    // 我们不存物理角度，只存数学算出来的初始值，这样偏差会互相抵消
    [SerializeField, HideInInspector] private float mathJ1_Init;
    [SerializeField, HideInInspector] private float mathJ2_Init;
    [SerializeField, HideInInspector] private float mathJ3_Init;

    // 用于画图的“起始边”方向
    private Vector3 visInitJ1Dir;
    private Vector3 visInitJ2Dir;
    private Vector3 visInitJ3Dir;

    private bool isInitialized = false;

    void OnEnable() { if (!Application.isPlaying) RecordZeroPose(); }
    void Start() { InitializeLengths(); if (Application.isPlaying) RecordZeroPose(); }

    void Update()
    {
        // 确保随时都有初始值
        if (!isInitialized) RecordZeroPose();

        // 编辑模式下实时解算
        if (previewTarget != null)
        {
            Solve(previewTarget.position, 0f);
        }
    }

    [ContextMenu("初始化臂长")]
    public void InitializeLengths()
    {
        if (j2Shoulder && j3Elbow && j5Wrist && gripperTip)
        {
            L1 = Vector3.Distance(j2Shoulder.position, j3Elbow.position);
            L2 = Vector3.Distance(j3Elbow.position, j5Wrist.position);
            L_Hand = Vector3.Distance(j5Wrist.position, gripperTip.position);
        }
    }

    // ==============================================================================
    // 1. 记录零位 (逻辑升级：记录数学理论初值)
    // ==============================================================================
    [ContextMenu("重置零位 (Record Zero)")]
    public void RecordZeroPose()
    {
        if (!j2Shoulder || !gripperTip) return;
        InitializeLengths();

        // --- A. 记录用于画图的向量 (物理现状) ---
        // J1 起始线: 底座到末端的水平投影
        Vector3 dirToTip = gripperTip.position - j1Base.position;
        visInitJ1Dir = Vector3.ProjectOnPlane(dirToTip, Vector3.up).normalized;

        // J2 起始线: 大臂向量
        visInitJ2Dir = (j3Elbow.position - j2Shoulder.position).normalized;

        // J3 起始线: 小臂向量
        visInitJ3Dir = (j5Wrist.position - j3Elbow.position).normalized;

        // --- B. 记录用于计算的数学角度 (理论现状) ---
        // 把“当前吸盘的位置”当作目标，反算一套数学角度出来作为基准
        float[] initAngles = CalculateMathAngles(gripperTip.position);

        if (initAngles != null)
        {
            mathJ1_Init = initAngles[0];
            mathJ2_Init = initAngles[1];
            mathJ3_Init = initAngles[2];
            isInitialized = true;
        }
    }

    // ==============================================================================
    // 2. 主计算入口 (计算差值)
    // ==============================================================================
    public bool Solve(Vector3 targetPos, float placeRotationY = 0f)
    {
        if (!isInitialized) return false;

        // 计算目标位置的数学解
        float[] targetAngles = CalculateMathAngles(targetPos);

        if (targetAngles == null) return false; // 无解 (够不着)

        // -----------------------------------------------------
        // 核心逻辑：输出 = 目标数学角 - 初始数学角
        // 这样可以消除所有物理安装误差
        // -----------------------------------------------------

        // J1: 
        outAngles[0] = (targetAngles[0] - mathJ1_Init);

        // J2:
        outAngles[1] = (targetAngles[1] - mathJ2_Init);

        // J3:
        outAngles[2] = (targetAngles[2] - mathJ3_Init);

        // J4 固定
        outAngles[3] = 0f;

        // J5 (跟随): 抵消 J2+J3 的增量，保持吸盘姿态不变
        outAngles[4] = -(outAngles[1] + outAngles[2]);

        // J6 (跟随): 抵消 J1 的增量
        outAngles[5] = -outAngles[0] + placeRotationY;

        return true;
    }

    // ==============================================================================
    // 3. 纯数学解算核心 (输入坐标 -> 输出绝对数学角度)
    // ==============================================================================
    private float[] CalculateMathAngles(Vector3 targetPos)
    {
        // --- J1 解算 (水平面) ---
        Vector3 dirToTarget = targetPos - j1Base.position;
        Vector3 flatDir = Vector3.ProjectOnPlane(dirToTarget, Vector3.up);
        // 计算相对于世界X轴的绝对角度
        float j1Angle = Vector3.SignedAngle(Vector3.right, flatDir, Vector3.up);

        // --- J2/J3 三角形解算 (垂直面) ---

        // 水平距离r (相对于肩膀的垂直线)
        Vector3 shoulderFlat = new Vector3(j2Shoulder.position.x, 0, j2Shoulder.position.z);
        Vector3 targetFlat = new Vector3(targetPos.x, 0, targetPos.z);
        float r_dist = Vector3.Distance(shoulderFlat, targetFlat);

        // 垂直高度h
        float y_diff = targetPos.y - j2Shoulder.position.y;

        // 目标 Wrist 位置 (吸盘垂直向下逻辑)
        float h_wrist = y_diff + L_Hand;
        float c = Mathf.Sqrt(r_dist * r_dist + h_wrist * h_wrist);

        if (c > L1 + L2) return null; // 够不着

        // 余弦定理
        float alpha = Mathf.Atan2(h_wrist, r_dist) * Mathf.Rad2Deg; // 仰角

        float cosA = Mathf.Clamp((L1 * L1 + c * c - L2 * L2) / (2 * L1 * c), -1, 1);
        float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg; // J2 内角

        float cosB = Mathf.Clamp((L1 * L1 + L2 * L2 - c * c) / (2 * L1 * L2), -1, 1);
        float angleB = Mathf.Acos(cosB) * Mathf.Rad2Deg; // J3 内角

        // 组装结果
        float j2Angle = alpha + angleA; // 绝对仰角

        // 处理肘部反转 (解决折叠问题)
        float j3Angle = angleB;
        if (invertElbow) j3Angle = -angleB;

        return new float[] { j1Angle, j2Angle, j3Angle };
    }

#if UNITY_EDITOR
    // ==============================================================================
    // 4. 可视化 (蓝色扇形回归!)
    // ==============================================================================
    void OnDrawGizmos()
    {
        if (!showGizmos || !isInitialized || j1Base == null) return;

        // J1: 绕 World Y (Vector3.up)
        // 注意：因为 J1 算出来是正数代表逆时针，画图时可能需要取反，视视觉效果而定
        // 这里为了对应数学逻辑，直接画 Delta
        DrawJointSector(j1Base, Vector3.up, visInitJ1Dir, -outAngles[0], "J1");

        // J2: 绕 World Z (Vector3.forward)
        DrawJointSector(j2Shoulder, Vector3.forward, visInitJ2Dir, outAngles[1], "J2");

        // J3: 绕 World Z (Vector3.forward)
        DrawJointSector(j3Elbow, Vector3.forward, visInitJ3Dir, outAngles[2], "J3");

        // J5: 绕 World Z
        DrawJointSector(j5Wrist, Vector3.forward, Vector3.right, outAngles[4], "J5");

        // J6: 绕 World Y
        DrawJointSector(gripperTip, Vector3.up, Vector3.forward, outAngles[5], "J6");

        // 画目标连线
        if (previewTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(j2Shoulder.position, previewTarget.position);
        }
    }

    // 辅助函数：画你喜欢的蓝色扇形
    void DrawJointSector(Transform t, Vector3 worldAxis, Vector3 fromDir, float angle, string label)
    {
        if (t == null) return;
        Vector3 pos = t.position;
        float radius = 0.5f;

        // 1. 画旋转轴 (黄色刺)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, worldAxis * 0.6f);

        // 2. 画半透明蓝色扇形 
        Handles.color = new Color(0, 0.5f, 1f, 0.2f); // 浅蓝填充
        Handles.DrawSolidArc(pos, worldAxis, fromDir, angle, radius);

        // 3. 画实线边框
        Handles.color = Color.blue;
        Handles.DrawWireArc(pos, worldAxis, fromDir, angle, radius);

        // 4. 画初始位置虚线
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(pos, pos + fromDir * radius, 2f);

        // 5. 文字标签
        GUIStyle style = new GUIStyle();
        style.normal.textColor = new Color(0.5f, 0.8f, 1f);
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(pos + worldAxis * 0.3f, $"{label}: {angle:F1}°", style);
    }
#endif
}