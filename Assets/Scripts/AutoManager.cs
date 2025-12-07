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
    public Transform pickPoint;
    public Transform j1Base; // 仅用于Gizmos安全检查

    [Header("参数调试")]
    public float hoverHeight = 0.4f;
    public float boxPlaceAngle = 0f;

    private float[] initialJointAngles;

    // ========================================================
    // Start & Init
    // ========================================================
    void Start()
    {
        RecordInitialAngles();
        StartCoroutine(RunOneBox());
    }

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
                // 使用提取出的辅助函数，代码更简洁
                initialJointAngles[i] = GetJointRawAngle(j.joint);
            }
        }
    }

    // ========================================================
    // 核心流程
    // ========================================================
    IEnumerator RunOneBox()
    {
        Debug.Log("<color=cyan>=== 任务开始 ===</color>");
        yield return new WaitForSeconds(0.5f);

        Vector3 dropPos = palletCalc.GetDropPosition(0);
        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = dropPos + Vector3.up * hoverHeight;

        // Step 1: Pick
        Debug.Log($"<color=yellow>步骤 1: 抓取 {pickPos}</color>");
        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // Step 2: Lift
        Debug.Log($"<color=yellow>步骤 2: 抬起 {pickHover}</color>");
        MoveRobotTo(pickHover, 0f, "Step 2");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("抬起后状态");

        // Step 3: Fly
        Debug.Log($"<color=yellow>步骤 3: 移动 {dropHover}</color>");
        MoveRobotTo(dropHover, boxPlaceAngle, "Step 3");
        yield return new WaitForSeconds(3.0f);
        LogCurrentJointAngles("悬停点状态");

        // Step 4: Down
        Debug.Log($"<color=yellow>步骤 4: 下降 {dropPos}</color>");
        MoveRobotTo(dropPos, boxPlaceAngle, "Step 4");
        yield return new WaitForSeconds(2.0f);
        LogCurrentJointAngles("放置点状态");

        // Step 5: Release
        Debug.Log("<color=yellow>步骤 5: 放下</color>");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // Step 6: Retract
        Debug.Log("<color=yellow>步骤 6: 撤回</color>");
        MoveRobotTo(dropHover, boxPlaceAngle, "Step 6");
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
    // Log 逻辑 (已精简：隐藏 J5/J6)
    // ========================================================
    void LogCurrentJointAngles(string context)
    {
        Debug.Log($"\n—— {context} ——");

        if (solver != null && solver.gripperTip != null)
        {
            Debug.Log($"<color=green>【末端坐标】World: {solver.gripperTip.position.ToString("F4")}</color>");
        }

        for (int i = 0; i < robotController.joints.Length; i++)
        {
            // 【关键修改】只显示前4个关节 (J1-J4)，跳过 J5(4) 和 J6(5)
            if (i >= 4) continue;

            var jointControl = robotController.joints[i];
            if (jointControl.joint == null) continue;

            // 1. 获取当前原始角度 (调用辅助函数)
            float currentRaw = GetJointRawAngle(jointControl.joint);

            // 2. 获取初始原始角度
            float initRaw = initialJointAngles[i];

            // 3. 计算相对角度
            float actualRelative = Mathf.DeltaAngle(initRaw, currentRaw);

            // 4. 计算误差
            float target = jointControl.targetAngle;
            float error = Mathf.Abs(actualRelative - target);

            string color = error > 5f ? "red" : "white";
            Debug.Log($"<color={color}>J{i + 1}: 目标={target:F1}° / 实际={actualRelative:F1}° (误差:{error:F1})</color>");
        }
    }

    // ========================================================
    // 辅助函数：智能读取关节角度 (X/Y/Z)
    // ========================================================
    float GetJointRawAngle(HingeJoint joint)
    {
        Vector3 axis = joint.axis;
        Vector3 euler = joint.transform.localEulerAngles;

        if (Mathf.Abs(axis.x) > 0.5f) return euler.x;
        if (Mathf.Abs(axis.y) > 0.5f) return euler.y;
        return euler.z;
    }

    // ========================================================
    // Gizmos
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
}