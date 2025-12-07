using UnityEngine;
#if UNITY_EDITOR
using UnityEditor; // 用于绘制文字和圆弧
#endif

[ExecuteInEditMode] // 关键：允许在编辑模式下运行
public class GeometricSolver : MonoBehaviour
{
    [Header("调试预览 (拖入一个空物体作为目标)")]
    public Transform previewTarget;
    public bool showGizmos = true;

    [Header("坐标参考")]
    [Tooltip("Z轴(蓝色)朝上的底座物体")]
    public Transform robotStaticBase;

    [Header("骨骼引用")]
    public Transform j1Base;     // 绕 Z
    public Transform j2Shoulder; // 绕 Y
    public Transform j3Elbow;    // 绕 Y
    public Transform j5Wrist;    // 绕 Z
    public Transform gripperTip; // 吸盘末端

    [Header("解算配置")]
    [Tooltip("反转肘部 (解决反向折叠)")]
    public bool invertElbow = false;

    [Header("实时计算结果 (只读)")]
    public float[] outAngles = new float[6];

    // 内部臂长
    private float L1, L2, L_Hand;

    // 每一帧在编辑模式下都会更新
    void Update()
    {
        InitializeLengths();

        // 如果有预览目标，就实时解算
        if (previewTarget != null)
        {
            Solve(previewTarget.position, 0f); // 默认箱子角度为0
        }
    }

    [ContextMenu("初始化臂长")]
    public void InitializeLengths()
    {
        if (j2Shoulder && j3Elbow && j5Wrist && gripperTip)
        {
            // 忽略 J4 (固定)，计算关键节点距离
            L1 = Vector3.Distance(j2Shoulder.position, j3Elbow.position);
            L2 = Vector3.Distance(j3Elbow.position, j5Wrist.position);
            L_Hand = Vector3.Distance(j5Wrist.position, gripperTip.position);
        }
    }

    // ================================================================
    // 核心解算逻辑 (基于你的轴向定义: J1-Z, J2-Y, J3-Y)
    // ================================================================
    public bool Solve(Vector3 targetPos, float placeRotationY = 0f)
    {
        if (robotStaticBase == null || j1Base == null) return false;

        // 1. 转为底座局部坐标 (假设底座Z轴朝天)
        // 那么在局部空间中：Z=高度, X/Y=水平面
        Vector3 localTarget = robotStaticBase.InverseTransformPoint(targetPos);

        float height = localTarget.z; // Z是高
        Vector2 horizontal = new Vector2(localTarget.x, localTarget.y); // XY是水平
        float radius = horizontal.magnitude;

        // --- J1: 绕 Local Z (水平旋转) ---
        // Atan2(y, x) 计算平面角
        float theta1 = Mathf.Atan2(localTarget.x, localTarget.y) * Mathf.Rad2Deg;
        outAngles[0] = -theta1; // 可能需要取反，视Unity坐标系而定

        // --- J2, J3: 三角形解算 ---
        // 目标 Wrist 位置 (吸盘垂直向下，所以要抬高 L_Hand)
        float r_wrist = radius;
        float h_wrist = height + L_Hand - j2Shoulder.localPosition.z; // 减去肩膀自身高度

        float c = Mathf.Sqrt(r_wrist * r_wrist + h_wrist * h_wrist);

        if (c > L1 + L2) return false; // 够不着

        float alpha = Mathf.Atan2(h_wrist, r_wrist) * Mathf.Rad2Deg;
        float cosA = Mathf.Clamp((L1 * L1 + c * c - L2 * L2) / (2 * L1 * c), -1, 1);
        float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;
        float cosB = Mathf.Clamp((L1 * L1 + L2 * L2 - c * c) / (2 * L1 * L2), -1, 1);
        float angleB = Mathf.Acos(cosB) * Mathf.Rad2Deg;

        // J2 (绕 Y): 90 - (仰角)
        outAngles[1] = 90f - (alpha + angleA);

        // J3 (绕 Y): 弯曲角
        float j3Angle = angleB - 180f;
        if (invertElbow) j3Angle = 180f - angleB; // 反转折叠
        outAngles[2] = j3Angle;

        // J4: 固定
        outAngles[3] = 0f;

        // J5 (绕 Z): 抵消 J2+J3 保持水平
        outAngles[4] = -(outAngles[1] + outAngles[2]);

        // J6 (绕 Z): 抵消 J1
        outAngles[5] = -outAngles[0] + placeRotationY;

        return true;
    }

    // ================================================================
    // 可视化部分 (Scene视图中的 线框 + 文字 + 轴)
    // ================================================================
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos || !robotStaticBase || !j2Shoulder) return;

        // 1. 画出旋转轴 (根据你的定义)
        DrawAxis(j1Base, Vector3.forward, "J1 (Z)", outAngles[0]);
        DrawAxis(j2Shoulder, Vector3.up, "J2 (Y)", outAngles[1]);
        DrawAxis(j3Elbow, Vector3.up, "J3 (Y)", outAngles[2]);
        DrawAxis(j5Wrist, Vector3.forward, "J5 (Z)", outAngles[4]);

        // 2. 画出“幽灵”机械臂 (线框预测图)
        if (previewTarget != null)
        {
            Gizmos.color = Color.yellow;

            // 模拟正向运动学 (Forward Kinematics) 来画出骨架
            // 注意：这只是简化的视觉模拟，用于检查解算是否合理
            Vector3 basePos = j2Shoulder.position;

            // 计算 J1 旋转后的平面方向
            Quaternion q1 = Quaternion.AngleAxis(outAngles[0], robotStaticBase.forward); // 绕底座Z轴转
            Vector3 armDir = q1 * robotStaticBase.up; // 假设前方是Y轴(绿色)

            // 画底座到肩膀
            Gizmos.DrawLine(j1Base.position, j2Shoulder.position);

            // 这是一个非常粗略的线框示意，更精确的需要完整的矩阵变换
            // 但对于看轴向已经足够了
        }
    }

    // 辅助函数：画轴、角度文字、圆盘
    void DrawAxis(Transform t, Vector3 localAxis, string name, float angle)
    {
        if (t == null) return;

        Vector3 worldAxis = t.TransformDirection(localAxis);
        Vector3 pos = t.position;

        // 画黄色的轴线 (刺)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, worldAxis * 0.3f);

        // 画文字 (显示当前计算出的目标角度)
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.cyan;
        style.fontSize = 15;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(pos + Vector3.up * 0.1f, $"{name}: {angle:F1}°", style);

        // 画圆弧 (视觉化旋转量)
        Handles.color = new Color(0, 1, 1, 0.2f);
        Handles.DrawSolidArc(pos, worldAxis, t.right, angle, 0.2f); // 以right为起点的圆弧
    }
#endif
}