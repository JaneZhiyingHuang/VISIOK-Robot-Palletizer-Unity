using UnityEngine;
using System.Collections;

public class AutoManager : MonoBehaviour
{
    [Header("脚本引用")]
    public PhysicsRobotController robotController;
    public GripperController gripper;
    public GeometricSolver solver;
    public PalletCalculator palletCalc;

    [Header("关键物体")]
    public Transform j1Base;
    public Transform j2Shoulder;
    public Transform pickPoint;

    [Header("参数调试")]
    public float boxPlaceAngle = 0f;
    public float hoverHeight = 0.4f; // 抬起/悬停高度

    [Header("轨迹可视化参数")]
    [Tooltip("弧线向上弯曲的额外高度")]
    public float arcExtraHeight = 0.2f;
    [Tooltip("弧线的平滑度(点越多越平滑)")]
    [Range(10, 100)] public int trajectoryResolution = 50;

    // J6 自动校准参数
    private float autoJ6Offset = 0f;

    // ========================================================
    // 【增强版】在 Scene 窗口画出真实的 J1 旋转轨迹
    // ========================================================
    void OnDrawGizmos()
    {
        // 安全检查
        if (pickPoint == null || palletCalc == null || j1Base == null) return;

        // 1. 实时计算 4 个关键点
        Vector3 p1_Pick = pickPoint.position;
        Vector3 p2_Lift = p1_Pick + Vector3.up * hoverHeight;
        Vector3 p4_Drop = palletCalc.GetDropPosition(0);
        Vector3 p3_Hover = p4_Drop + Vector3.up * hoverHeight;

        // --- A. 画关键点 (球体) ---
        Gizmos.color = Color.green; Gizmos.DrawSphere(p1_Pick, 0.05f); // 抓取
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(p2_Lift, 0.05f); // 抬起悬停
        Gizmos.color = new Color(1, 0.5f, 0); Gizmos.DrawSphere(p3_Hover, 0.05f); // 放置悬停
        Gizmos.color = Color.red; Gizmos.DrawSphere(p4_Drop, 0.05f); // 放置

        // --- B. 画垂直线 (直线) ---
        // 抬起和放下通常是垂直直线
        Gizmos.color = new Color(1, 1, 1, 0.5f); // 半透明白
        Gizmos.DrawLine(p1_Pick, p2_Lift);
        Gizmos.DrawLine(p3_Hover, p4_Drop);

        // --- C. 画水平旋转弧线 (核心修改) ---
        // 这条线模拟 J1 旋转 + 手臂伸缩的混合轨迹
        Gizmos.color = Color.cyan; // 青色轨迹
        DrawSwingArc(p2_Lift, p3_Hover, j1Base.position);
    }

    // 辅助函数：画出绕 Pivot 旋转的弧线
    void DrawSwingArc(Vector3 start, Vector3 end, Vector3 pivot)
    {
        int segments = 30; // 弧线精细度
        Vector3 prevPos = start;

        // 计算相对于底座的向量
        Vector3 startOffset = start - pivot;
        Vector3 endOffset = end - pivot;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;

            // 1. 角度插值 (Slerp): 模拟 J1 的均匀旋转
            // Slerp 会沿着球体表面插值，这正好对应机械臂的旋转特性
            Vector3 currentDir = Vector3.Slerp(startOffset, endOffset, t);

            // 2. 长度插值 (Lerp): 模拟 J2/J3 的均匀伸缩
            // 因为起点和终点的“臂长”可能不同，我们需要平滑过渡
            float currentDist = Mathf.Lerp(startOffset.magnitude, endOffset.magnitude, t);

            // 3. 组合：方向 * 长度 = 新的轨迹点
            // 保持 currentDir 的方向，但将其长度修改为 currentDist
            Vector3 currentPos = pivot + currentDir.normalized * currentDist;

            // 画线
            Gizmos.DrawLine(prevPos, currentPos);
            prevPos = currentPos;
        }
    }

    // 辅助函数：绘制直线轨迹片段
    void DrawLinearTrajectory(Vector3 start, Vector3 end)
    {
        Vector3 prevPos = start;
        for (int i = 1; i <= trajectoryResolution / 2; i++) // 直线点少一点也没事
        {
            float t = i / (float)(trajectoryResolution / 2);
            Vector3 currentPos = Vector3.Lerp(start, end, t);
            Gizmos.DrawLine(prevPos, currentPos);
            prevPos = currentPos;
        }
    }

    // 辅助函数：计算二次贝塞尔曲线上的点
    // t: 0到1的进度, p0: 起点, p1: 控制点, p2: 终点
    Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        Vector3 p = uu * p0; // (1-t)^2 * P0
        p += 2 * u * t * p1; // 2(1-t)t * P1
        p += tt * p2;        // t^2 * P2
        return p;
    }

    // ========================================================
    // 以下是运行逻辑 (保持不变)
    // ========================================================
    void Start()
    {
        if (robotController.joints[0].joint != null && robotController.joints[5].joint != null)
        {
            float currentJ1 = robotController.joints[0].targetAngle;
            float currentJ6 = robotController.joints[5].targetAngle;
            autoJ6Offset = currentJ6 + currentJ1;
        }
        StartCoroutine(RunOneBox());
    }

    IEnumerator RunOneBox()
    {
        Debug.Log("<color=cyan>=== 任务开始 ===</color>");
        yield return new WaitForSeconds(1f);

        Vector3 dropPos = palletCalc.GetDropPosition(0);
        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = dropPos + Vector3.up * hoverHeight;

        // 步骤 1: 抓取
        Debug.Log("<color=yellow>步骤 1: 抓取 (Pick)</color>");
        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // 步骤 2: 抬起
        Debug.Log($"<color=yellow>步骤 2: 抬起到安全高度 {pickHover}</color>");
        MoveRobotTo(pickHover, "Step 2: Lift"); // 传入步骤名方便打日志
        yield return new WaitForSeconds(3.0f);
        LogCurrentJointAngles("抬起后实际角度"); // 检查有没有到位

        // 步骤 3: 飞行
        Debug.Log($"<color=yellow>步骤 3: 飞向托盘上方 {dropHover}</color>");
        MoveRobotTo(dropHover, "Step 3: Fly to Hover");
        yield return new WaitForSeconds(3.0f);
        LogCurrentJointAngles("到达悬停点实际角度");

        // 步骤 4: 下降
        Debug.Log($"<color=yellow>步骤 4: 下降放置 {dropPos}</color>");
        MoveRobotTo(dropPos, "Step 4: Down to Drop");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("下降到底实际角度");

        // 步骤 5: 放下
        Debug.Log("<color=yellow>步骤 5: 放下 (Release)</color>");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // 步骤 6: 离开
        Debug.Log("<color=yellow>步骤 6: 离开 (Retract)</color>");
        MoveRobotTo(dropHover, "Step 6: Retract");
        yield return new WaitForSeconds(2.0f);

        Debug.Log("<color=cyan>=== 任务结束 ===</color>");
    }

    // --- 核心修改：带日志的移动函数 ---
    void MoveRobotTo(Vector3 targetPos, string stepName)
    {
        // 1. 数学计算
        solver.Solve(targetPos, j1Base, j2Shoulder);

        // 2. 打印计算结果 (大脑的想法)
        Debug.Log($"[{stepName}] 目标坐标: {targetPos}\n" +
                  $">>> 计算目标角度:\n" +
                  $"J1: {solver.outAngles[0]:F2}, J2: {solver.outAngles[1]:F2}, J3: {solver.outAngles[2]:F2}, J5: {solver.outAngles[4]:F2}");

        // 3. 应用到关节
        var joints = robotController.joints;
        if (joints.Length >= 6)
        {
            if (joints[0].joint != null) joints[0].targetAngle = solver.outAngles[0];
            if (joints[1].joint != null) joints[1].targetAngle = solver.outAngles[1];
            if (joints[2].joint != null) joints[2].targetAngle = solver.outAngles[2];

            // J4
            if (joints[4].joint != null) joints[4].targetAngle = solver.outAngles[4];

            // J6 计算与日志
            if (joints[5].joint != null)
            {
                float targetJ1 = solver.outAngles[0];
                float targetJ6 = (boxPlaceAngle - targetJ1) + autoJ6Offset;
                while (targetJ6 > 180) targetJ6 -= 360;
                while (targetJ6 < -180) targetJ6 += 360;
                joints[5].targetAngle = targetJ6;

                Debug.Log($"J6 计算: 目标({boxPlaceAngle}) - J1({targetJ1:F2}) = {targetJ6:F2}");
            }
        }
    }

    // --- 新增：打印当前实际物理角度 ---
    void LogCurrentJointAngles(string context)
    {
        var joints = robotController.joints;
        if (joints.Length < 6) return;

        // 获取 HingeJoint 当前的物理角度 (angle)
        float j1 = joints[0].joint != null ? joints[0].joint.angle : 0;
        float j2 = joints[1].joint != null ? joints[1].joint.angle : 0;
        float j3 = joints[2].joint != null ? joints[2].joint.angle : 0;
        float j5 = joints[4].joint != null ? joints[4].joint.angle : 0;
        float j6 = joints[5].joint != null ? joints[5].joint.angle : 0;

        Debug.Log($"<color=orange>[物理反馈 - {context}]</color>\n" +
                  $"J1: {j1:F2}, J2: {j2:F2}, J3: {j3:F2}, J5: {j5:F2}, J6: {j6:F2}");
    }
}