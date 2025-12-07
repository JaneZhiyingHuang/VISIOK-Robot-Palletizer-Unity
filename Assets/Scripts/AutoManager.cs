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
    public Transform pickPoint;     // 抓取点
    public Transform j1Base;        // 仅用于 Gizmos 判断，防止报错

    [Header("参数调试")]
    public float hoverHeight = 0.4f;   // 安全高度
    public float boxPlaceAngle = 0f;   // 箱子放置时的旋转角度 (J6修正)

    // ========================================================
    // 1. 保留原先的 Gizmos 可视化 (4个点 + 连线)
    // ========================================================
    void OnDrawGizmos()
    {
        // 安全检查
        if (pickPoint == null || palletCalc == null) return;

        // 计算四个关键点位
        Vector3 p1_Pick = pickPoint.position;
        Vector3 p2_Lift = p1_Pick + Vector3.up * hoverHeight;
        Vector3 p4_Drop = palletCalc.GetDropPosition(0);
        Vector3 p3_Hover = p4_Drop + Vector3.up * hoverHeight;

        // 绘制点 (球体)
        Gizmos.color = Color.green; Gizmos.DrawSphere(p1_Pick, 0.05f); // 抓取点
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(p2_Lift, 0.05f); // 抬起点
        Gizmos.color = new Color(1, 0.5f, 0); Gizmos.DrawSphere(p3_Hover, 0.05f); // 悬停点
        Gizmos.color = Color.red; Gizmos.DrawSphere(p4_Drop, 0.05f); // 放置点

        // 绘制连线 (路径预览)
        Gizmos.color = new Color(1, 1, 1, 0.5f);
        Gizmos.DrawLine(p1_Pick, p2_Lift);
        Gizmos.DrawLine(p2_Lift, p3_Hover);
        Gizmos.DrawLine(p3_Hover, p4_Drop);
    }

    // ========================================================
    // 2. 运行流程
    // ========================================================
    void Start()
    {
        StartCoroutine(RunOneBox());
    }

    IEnumerator RunOneBox()
    {
        Debug.Log("<color=cyan>=== 任务开始 ===</color>");

        // 给物理引擎一点时间初始化碰撞 (非常重要，防止Enter还没触发就PickUp)
        yield return new WaitForSeconds(0.5f);

        // 获取坐标
        Vector3 dropPos = palletCalc.GetDropPosition(0);
        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = dropPos + Vector3.up * hoverHeight;

        // =====================================================
        // Step 1: 抓取 (Pick) - 修正版
        // =====================================================
        Debug.Log($"<color=yellow>步骤 1: 直接抓取 (Pick)P{pickPos}</color>");

        gripper.PickUp(); // 直接抓！
        yield return new WaitForSeconds(0.5f);

        // =====================================================
        // Step 2: 抬起 (Lift)
        // =====================================================
        Debug.Log($"<color=yellow>步骤 2: 抬起到安全高度 {pickHover}</color>");

        // 抬起时，J6 保持 0 度 (初始角度)
        MoveRobotTo(pickHover, 0f, "Step 2: Lift");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("抬起后状态");

        // =====================================================
        // Step 3: 飞向托盘 (Fly)
        // =====================================================
        Debug.Log($"<color=yellow>步骤 3: 飞向托盘上方 {dropHover}</color>");
        MoveRobotTo(dropHover, boxPlaceAngle, "Step 3: Fly to Hover");
        yield return new WaitForSeconds(3.0f);
        LogCurrentJointAngles("悬停点状态");

        // =====================================================
        // Step 4: 下降 (Down)
        // =====================================================
        Debug.Log($"<color=yellow>步骤 4: 下降放置 {dropPos}</color>");
        MoveRobotTo(dropPos, boxPlaceAngle, "Step 4: Down to Drop");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("放置点状态");

        // =====================================================
        // Step 5: 放下 (Release)
        // =====================================================
        Debug.Log("<color=yellow>步骤 5: 放下 (Release)</color>");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // =====================================================
        // Step 6: 离开 (Retract)
        // =====================================================
        Debug.Log("<color=yellow>步骤 6: 离开 (Retract)</color>");
        MoveRobotTo(dropHover, boxPlaceAngle, "Step 6: Retract");
        yield return new WaitForSeconds(2.0f);

        Debug.Log("<color=cyan>=== 任务结束 ===</color>");
    }

    // ========================================================
    // 3. 移动逻辑 (适配新的 GeometricSolver)
    // ========================================================
    void MoveRobotTo(Vector3 targetPos, float rotationY, string stepName)
    {
        // 调用 Solver (现在支持传入 rotationY 来控制 J6)
        if (!solver.Solve(targetPos, rotationY))
        {
            Debug.LogError($"[IK失败] {stepName}: 目标 {targetPos} 超出范围或无法到达！");
            return;
        }

        // 将求出的角度写入关节驱动
        for (int i = 0; i < 6; i++)
        {
            if (i < robotController.joints.Length)
            {
                robotController.joints[i].targetAngle = solver.outAngles[i];
            }
        }
    }

    // ========================================================
    // 4. 保留原先的 Log 调试函数
    // ========================================================
    void LogCurrentJointAngles(string context)
    {
        Debug.Log($"\n—— {context} ——");

        // 打印当前末端世界坐标
        if (solver != null && solver.gripperTip != null)
        {
            Vector3 currentPos = solver.gripperTip.position;
            // 保留4位小数，方便看精度
            Debug.Log($"<color=green>【当前末端坐标】 World: {currentPos.ToString("F4")}</color>");
        }

        // 打印每个关节的 目标值 vs 实际值
        for (int i = 0; i < robotController.joints.Length; i++)
        {
            if (robotController.joints[i].joint == null) continue;

            // 获取当前物理角度 (-180 ~ 180)
            float actual = robotController.joints[i].joint.transform.localEulerAngles.z;
            if (actual > 180) actual -= 360;

            float target = robotController.joints[i].targetAngle;
            float error = Mathf.Abs(actual - target);

            // 如果误差大于 5 度，用红色高亮显示，方便排查物理卡顿
            string color = error > 5f ? "red" : "white";

            Debug.Log($"<color={color}>J{i + 1}: 目标={target:F1}° / 实际={actual:F1}° (误差:{error:F1})</color>");
        }
    }
}