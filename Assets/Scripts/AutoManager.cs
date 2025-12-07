using UnityEngine;
using System.Collections;
using System.Collections.Generic; // 引入 List

public class AutoManager : MonoBehaviour
{
    [Header("脚本引用")]
    public PhysicsRobotController robotController;
    public GripperController gripper;
    public GeometricSolver solver;
    public PalletCalculator palletCalc;

    [Header("关键物体")]
    public Transform pickPoint;
    public Transform j1Base;

    [Header("参数调试")]
    public float hoverHeight = 0.4f;
    public float boxPlaceAngle = 0f;

    // 【新增】用来存储每个关节“初始绝对角度”的列表
    private float[] initialJointAngles;

    // ========================================================
    // Gizmos (保持不变)
    // ========================================================
    void OnDrawGizmos()
    {
        if (pickPoint == null || palletCalc == null) return;
        Vector3 p1 = pickPoint.position;
        Vector3 p2 = p1 + Vector3.up * hoverHeight;
        Vector3 p4 = palletCalc.GetDropPosition(0);
        Vector3 p3 = p4 + Vector3.up * hoverHeight;

        Gizmos.color = Color.green; Gizmos.DrawSphere(p1, 0.05f);
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(p2, 0.05f);
        Gizmos.color = new Color(1, 0.5f, 0); Gizmos.DrawSphere(p3, 0.05f);
        Gizmos.color = Color.red; Gizmos.DrawSphere(p4, 0.05f);

        Gizmos.color = new Color(1, 1, 1, 0.5f);
        Gizmos.DrawLine(p1, p2); Gizmos.DrawLine(p2, p3); Gizmos.DrawLine(p3, p4);
    }

    // ========================================================
    // Start & Init
    // ========================================================
    void Start()
    {
        // 1. 【新增】记录初始角度
        RecordInitialAngles();

        // 2. 启动流程
        StartCoroutine(RunOneBox());
    }

    // 记录初始绝对角度的函数
    void RecordInitialAngles()
    {
        if (robotController == null || robotController.joints == null) return;

        int count = robotController.joints.Length;
        initialJointAngles = new float[count];

        for (int i = 0; i < count; i++)
        {
            var j = robotController.joints[i];
            if (j.joint != null)
            {
                // 获取旋转轴
                Vector3 axis = j.joint.axis;
                Vector3 euler = j.joint.transform.localEulerAngles;

                // 智能判断读哪个轴 (X/Y/Z)
                float rawAngle = 0f;
                if (Mathf.Abs(axis.x) > 0.5f) rawAngle = euler.x;
                else if (Mathf.Abs(axis.y) > 0.5f) rawAngle = euler.y;
                else rawAngle = euler.z;

                initialJointAngles[i] = rawAngle; // 存下来！这就是我们的“0度基准”
            }
        }
    }

    IEnumerator RunOneBox()
    {
        Debug.Log("<color=cyan>=== 任务开始 ===</color>");
        yield return new WaitForSeconds(0.5f);

        Vector3 dropPos = palletCalc.GetDropPosition(0);
        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = dropPos + Vector3.up * hoverHeight;

        // Step 1
        Debug.Log($"<color=yellow>步骤 1: 直接抓取 (Pick)P{pickPos}</color>");
        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // Step 2
        Debug.Log($"<color=yellow>步骤 2: 抬起到安全高度 {pickHover}</color>");
        MoveRobotTo(pickHover, 0f, "Step 2: Lift");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("抬起后状态");

        // Step 3
        Debug.Log($"<color=yellow>步骤 3: 飞向托盘上方 {dropHover}</color>");
        MoveRobotTo(dropHover, boxPlaceAngle, "Step 3: Fly to Hover");
        yield return new WaitForSeconds(3.0f);
        LogCurrentJointAngles("悬停点状态");

        // Step 4
        Debug.Log($"<color=yellow>步骤 4: 下降放置 {dropPos}</color>");
        MoveRobotTo(dropPos, boxPlaceAngle, "Step 4: Down to Drop");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("放置点状态");

        // Step 5
        Debug.Log("<color=yellow>步骤 5: 放下 (Release)</color>");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // Step 6
        Debug.Log("<color=yellow>步骤 6: 离开 (Retract)</color>");
        MoveRobotTo(dropHover, boxPlaceAngle, "Step 6: Retract");
        yield return new WaitForSeconds(2.0f);

        Debug.Log("<color=cyan>=== 任务结束 ===</color>");
    }

    void MoveRobotTo(Vector3 targetPos, float rotationY, string stepName)
    {
        if (!solver.Solve(targetPos, rotationY))
        {
            Debug.LogError($"[IK失败] {stepName}");
            return;
        }
        for (int i = 0; i < 6; i++)
        {
            if (i < robotController.joints.Length)
                robotController.joints[i].targetAngle = solver.outAngles[i];
        }
    }

    // ========================================================
    // 4. 【核心修改】Log 逻辑：使用相对角度 (Delta)
    // ========================================================
    void LogCurrentJointAngles(string context)
    {
        Debug.Log($"\n—— {context} ——");

        if (solver != null && solver.gripperTip != null)
        {
            Vector3 currentPos = solver.gripperTip.position;
            Debug.Log($"<color=green>【当前末端坐标】 World: {currentPos.ToString("F4")}</color>");
        }

        for (int i = 0; i < robotController.joints.Length; i++)
        {
            var jointControl = robotController.joints[i];
            if (jointControl.joint == null) continue;

            // 1. 获取当前绝对角度
            Vector3 axis = jointControl.joint.axis;
            Vector3 currentEuler = jointControl.joint.transform.localEulerAngles;
            float currentRaw = 0f;
            if (Mathf.Abs(axis.x) > 0.5f) currentRaw = currentEuler.x;
            else if (Mathf.Abs(axis.y) > 0.5f) currentRaw = currentEuler.y;
            else currentRaw = currentEuler.z;

            // 2. 获取初始绝对角度
            float initRaw = initialJointAngles[i];

            // 3. 【关键】计算相对角度 (Current - Initial)
            // Mathf.DeltaAngle 会自动处理 360 度循环问题，算出最短差值
            // 比如：当前 10度，初始 0度 -> 结果 10度
            // 比如：当前 -92度，初始 -102度 -> 结果 10度
            float actualRelative = Mathf.DeltaAngle(initRaw, currentRaw);

            // 4. 计算误差
            float target = jointControl.targetAngle;
            float error = Mathf.Abs(actualRelative - target);

            string color = error > 5f ? "red" : "white";

            // 打印时，actualRelative 就是你想要的“从0度转了多少”
            Debug.Log($"<color={color}>J{i + 1}: 目标={target:F1}° / 实际={actualRelative:F1}° (误差:{error:F1})</color>");
        }
    }
}