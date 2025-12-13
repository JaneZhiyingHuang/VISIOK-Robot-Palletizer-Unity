using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GeometricSolver : MonoBehaviour
{
    // ==========================================
    // 定义 J6 对齐模式
    // ==========================================
    public enum J6AlignMode
    {
        [InspectorName("保持平行于世界 X 轴 (红轴)")] Align_World_X,
        [InspectorName("保持平行于世界 Z 轴 (蓝轴)")] Align_World_Z
    }

    [Header("调试与预览")]
    public Transform previewTarget;
    public bool showGizmos = true;

    [Header("核心引用")]
    public Transform robotStaticBase;
    public Transform j1Base;
    public Transform j2Shoulder;
    public Transform j3Elbow;
    public Transform j5Wrist;
    [Tooltip("请把 J6 (旋转手腕/法兰) 的物体拖到这里")]
    public Transform j6Hand;
    public Transform gripperTip;

    [Header("J6 (吸盘) 自动对齐配置")]
    public J6AlignMode j6AxisMode = J6AlignMode.Align_World_X;

    [Header("计算结果 (增量)")]
    public float[] outAngles = new float[6];

    // --- 内部变量 ---
    private float L1, L2, L_Hand;

    // 记录初始时刻的“数学理论角度”
    [SerializeField, HideInInspector] private float mathJ1_Init;
    [SerializeField, HideInInspector] private float mathJ2_Init;
    [SerializeField, HideInInspector] private float mathJ3_Init;

    // 可视化用的向量
    private Vector3 visInitJ1Dir;
    private Vector3 visInitJ2Dir;
    private Vector3 visInitJ3Dir;
    private Vector3 visInitJ6Dir;

    private bool isInitialized = false;

    void OnEnable() { if (!Application.isPlaying) RecordZeroPose(); }
    void Start() { InitializeLengths(); if (Application.isPlaying) RecordZeroPose(); }

    void Update()
    {
        if (!isInitialized) RecordZeroPose();
        if (previewTarget != null) Solve(previewTarget.position, 0f);
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
    // 1. 记录零位
    // ==============================================================================
    [ContextMenu("重置零位 (Record Zero)")]
    public void RecordZeroPose()
    {
        if (!j2Shoulder || !gripperTip) return;
        InitializeLengths();

        // 记录向量用于画图
        Vector3 dirToTip = gripperTip.position - j1Base.position;
        visInitJ1Dir = Vector3.ProjectOnPlane(dirToTip, Vector3.up).normalized;
        visInitJ2Dir = (j3Elbow.position - j2Shoulder.position).normalized;
        visInitJ3Dir = (j5Wrist.position - j3Elbow.position).normalized;

        // J6 画图初始方向 (红轴或蓝轴)
        visInitJ6Dir = (j6AxisMode == J6AlignMode.Align_World_X) ? Vector3.right : Vector3.forward;

        // 记录 J1/J2/J3 的初始数学值
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
    // 2. 主计算入口
    // ==============================================================================
    public bool Solve(Vector3 targetPos, float placeRotationY = 0f, bool updateJ6 = true)
    {
        if (!isInitialized) return false;

        float[] targetAngles = CalculateMathAngles(targetPos);
        if (targetAngles == null) return false;

        // --- J1, J2, J3 (Delta 逻辑) ---
        outAngles[0] = (targetAngles[0] - mathJ1_Init);
        outAngles[1] = (targetAngles[1] - mathJ2_Init);
        outAngles[2] = (targetAngles[2] - mathJ3_Init);

        // --- J4 ---
        outAngles[3] = 0f;

        // --- J5: 抵消 J2 + J3 (垂直平衡) ---
        outAngles[4] = -(outAngles[1] + outAngles[2]);

        // --- J6: 核心逻辑 (90 - J1) ---
        if (updateJ6)
        {
            float j6CounterRotate = -(90f - outAngles[0]);
            float axisOffset = (j6AxisMode == J6AlignMode.Align_World_Z) ? 90f : 0f;
            outAngles[5] = j6CounterRotate + axisOffset + placeRotationY;
        }
        return true;
    }

    // ==============================================================================
    // 3. 纯数学解算
    // ==============================================================================
    private float[] CalculateMathAngles(Vector3 targetPos)
    {
        // J1
        Vector3 dirToTarget = targetPos - j1Base.position;
        Vector3 armFlatDir = Vector3.ProjectOnPlane(dirToTarget, Vector3.up).normalized;
        float j1Angle = Vector3.SignedAngle(Vector3.right, armFlatDir, Vector3.up);

        // J2/J3
        Vector3 shoulderFlat = new Vector3(j2Shoulder.position.x, 0, j2Shoulder.position.z);
        Vector3 targetFlat = new Vector3(targetPos.x, 0, targetPos.z);
        float r_dist = Vector3.Distance(shoulderFlat, targetFlat);
        float y_diff = targetPos.y - j2Shoulder.position.y;
        float h_wrist = y_diff + L_Hand;

        float c = Mathf.Sqrt(r_dist * r_dist + h_wrist * h_wrist);
        if (c > L1 + L2) return null;

        float alpha = Mathf.Atan2(h_wrist, r_dist) * Mathf.Rad2Deg;
        float cosA = Mathf.Clamp((L1 * L1 + c * c - L2 * L2) / (2 * L1 * c), -1, 1);
        float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;
        float cosB = Mathf.Clamp((L1 * L1 + L2 * L2 - c * c) / (2 * L1 * L2), -1, 1);
        float angleB = Mathf.Acos(cosB) * Mathf.Rad2Deg;

        float j2Angle = alpha + angleA;
        float j3Angle = angleB; 

        return new float[] { j1Angle, j2Angle, j3Angle };
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos || !isInitialized || j1Base == null) return;

        DrawJointSector(j1Base, Vector3.up, visInitJ1Dir, -outAngles[0], "J1");
        DrawJointSector(j2Shoulder, Vector3.forward, visInitJ2Dir, outAngles[1], "J2");
        DrawJointSector(j3Elbow, Vector3.forward, visInitJ3Dir, outAngles[2], "J3");
        DrawJointSector(j5Wrist, Vector3.forward, Vector3.right, outAngles[4], "J5");

        Transform j6Transform = j6Hand != null ? j6Hand : gripperTip;
        DrawJointSector(j6Transform, Vector3.up, visInitJ6Dir, outAngles[5], "J6");

        if (previewTarget)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(j2Shoulder.position, previewTarget.position);

            Vector3 targetAxis = (j6AxisMode == J6AlignMode.Align_World_X) ? Vector3.right : Vector3.forward;
            Gizmos.color = (j6AxisMode == J6AlignMode.Align_World_X) ? Color.red : Color.blue;
            Gizmos.DrawRay(j6Transform.position, targetAxis * 1.0f);
        }
    }

    void DrawJointSector(Transform t, Vector3 worldAxis, Vector3 fromDir, float angle, string label)
    {
        if (t == null) return;
        Vector3 pos = t.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, worldAxis * 0.6f);
        Handles.color = new Color(0, 0.5f, 1f, 0.2f);
        Handles.DrawSolidArc(pos, worldAxis, fromDir, angle, 0.5f);
        Handles.color = Color.blue;
        Handles.DrawWireArc(pos, worldAxis, fromDir, angle, 0.5f);
        GUIStyle style = new GUIStyle();
        style.normal.textColor = new Color(0.5f, 0.8f, 1f);
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(pos + worldAxis * 0.3f, $"{label}: {angle:F1}°", style);
    }
#endif
}