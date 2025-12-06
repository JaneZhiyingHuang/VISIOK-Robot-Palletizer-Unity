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
    public float hoverHeight = 0.4f; 

    private float autoJ6Offset = 0f;

    // ========================================================
    // 点位预测
    // ========================================================
    void OnDrawGizmos()
    {
        // 安全检查
        if (pickPoint == null || palletCalc == null || j1Base == null) return;

        Vector3 p1_Pick = pickPoint.position;
        Vector3 p2_Lift = p1_Pick + Vector3.up * hoverHeight;
        Vector3 p4_Drop = palletCalc.GetDropPosition(0);
        Vector3 p3_Hover = p4_Drop + Vector3.up * hoverHeight;

        Gizmos.color = Color.green; Gizmos.DrawSphere(p1_Pick, 0.05f);
        Gizmos.color = Color.yellow; Gizmos.DrawSphere(p2_Lift, 0.05f);
        Gizmos.color = new Color(1, 0.5f, 0); Gizmos.DrawSphere(p3_Hover, 0.05f);
        Gizmos.color = Color.red; Gizmos.DrawSphere(p4_Drop, 0.05f);

        Gizmos.color = new Color(1, 1, 1, 0.5f);
        Gizmos.DrawLine(p1_Pick, p2_Lift);
        Gizmos.DrawLine(p3_Hover, p4_Drop);
    }

    // ========================================================
    // 运行逻辑 
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
        yield return new WaitForSeconds(2f);

        // 步骤 2: 抬起
        Debug.Log($"<color=yellow>步骤 2: 抬起到安全高度 {pickHover}</color>");
        MoveRobotTo(pickHover, "Step 2: Lift"); 
        yield return new WaitForSeconds(3.0f);
        LogCurrentJointAngles("抬起后实际角度");

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

    // --- 移动函数 ---
    void MoveRobotTo(Vector3 targetPos, string stepName)
    {
        solver.Solve(targetPos, j1Base, j2Shoulder);

        Debug.Log($"[{stepName}] 目标坐标: {targetPos}\n" +
                  $">>> 计算目标角度:\n" +
                  $"J1: {solver.outAngles[0]:F2}, J2: {solver.outAngles[1]:F2}, J3: {solver.outAngles[2]:F2}, J5: {solver.outAngles[4]:F2}");

        var joints = robotController.joints;
        if (joints.Length >= 6)
        {
            if (joints[0].joint != null) joints[0].targetAngle = solver.outAngles[0];
            if (joints[1].joint != null) joints[1].targetAngle = solver.outAngles[1];
            if (joints[2].joint != null) joints[2].targetAngle = solver.outAngles[2];

            if (joints[4].joint != null) joints[4].targetAngle = solver.outAngles[4];

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

    // --- 打印当前实际物理角度 ---
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