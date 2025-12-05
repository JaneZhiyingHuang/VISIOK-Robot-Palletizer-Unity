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
    public float hoverHeight = 0.4f; // 【变量提取】把 0.4 变成变量，方便在 Scene 里调

    // J6 自动校准参数
    private float autoJ6Offset = 0f;

    // ========================================================
    // 【新增】核心功能：在 Scene 窗口画出轨迹
    // ========================================================
    void OnDrawGizmos()
    {
        // 安全检查：如果没拖物体，就不画，防止报错
        if (pickPoint == null || palletCalc == null) return;

        // 1. 实时计算 4 个关键点 (和下面 RunOneBox 逻辑一模一样)
        Vector3 p1_Pick = pickPoint.position;
        Vector3 p2_PickHover = p1_Pick + Vector3.up * hoverHeight;

        Vector3 p4_Drop = palletCalc.GetDropPosition(0); // 算出第 0 个箱子的位置
        Vector3 p3_DropHover = p4_Drop + Vector3.up * hoverHeight;

        // 2. 画点 (球体)
        Gizmos.color = Color.green; // 抓取点 (绿色)
        Gizmos.DrawSphere(p1_Pick, 0.05f);

        Gizmos.color = Color.yellow; // 空中悬停点 (黄色)
        Gizmos.DrawSphere(p2_PickHover, 0.05f);
        Gizmos.DrawSphere(p3_DropHover, 0.05f);

        Gizmos.color = Color.red;   // 放置点 (红色)
        Gizmos.DrawSphere(p4_Drop, 0.05f);

        // 3. 画线 (轨迹连线)
        Gizmos.color = Color.white;
        Gizmos.DrawLine(p1_Pick, p2_PickHover);      // 抬起
        Gizmos.DrawLine(p2_PickHover, p3_DropHover); // 飞行
        Gizmos.DrawLine(p3_DropHover, p4_Drop);      // 下降
    }

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
        yield return new WaitForSeconds(1f);

        // --- 重新计算一遍 (确保运行逻辑和画图逻辑一致) ---
        Vector3 dropPos = palletCalc.GetDropPosition(0);
        Vector3 pickPos = pickPoint.position;

        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = dropPos + Vector3.up * hoverHeight;

        // ========================================================
        // 步骤 1: 去抓取点
        // ========================================================
        Debug.Log("1. 去抓取点");
        // 如果机械臂已经在附近，这一步可以注释掉
        // MoveRobotTo(pickPos); 
        // yield return new WaitForSeconds(2.5f);

        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // ========================================================
        // 步骤 2: 抬起
        // ========================================================
        Debug.Log("2. 抬起到安全高度");
        MoveRobotTo(pickHover);
        yield return new WaitForSeconds(3.0f);

        // ========================================================
        // 步骤 3: 飞向托盘上方
        // ========================================================
        Debug.Log("3. 飞向托盘上方");
        MoveRobotTo(dropHover);
        yield return new WaitForSeconds(4.0f);

        // ========================================================
        // 步骤 4: 下降
        // ========================================================
        Debug.Log("4. 下降放置");
        MoveRobotTo(dropPos);
        yield return new WaitForSeconds(2.0f);

        // ========================================================
        // 步骤 5: 放下
        // ========================================================
        Debug.Log("5. 放下");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // ========================================================
        // 步骤 6: 离开
        // ========================================================
        Debug.Log("6. 离开");
        MoveRobotTo(dropHover);
        yield return new WaitForSeconds(2.0f);

        Debug.Log("结束！");
    }

    void MoveRobotTo(Vector3 targetPos)
    {
        solver.Solve(targetPos, j1Base, j2Shoulder);

        var joints = robotController.joints;
        if (joints.Length >= 6)
        {
            if (joints[0].joint != null) joints[0].targetAngle = solver.outAngles[0];
            if (joints[1].joint != null) joints[1].targetAngle = solver.outAngles[1];
            if (joints[2].joint != null) joints[2].targetAngle = solver.outAngles[2];
            // J4 Fixed
            if (joints[4].joint != null) joints[4].targetAngle = solver.outAngles[4];

            if (joints[5].joint != null)
            {
                float targetJ1 = solver.outAngles[0];
                float targetJ6 = (boxPlaceAngle - targetJ1) + autoJ6Offset;
                while (targetJ6 > 180) targetJ6 -= 360;
                while (targetJ6 < -180) targetJ6 += 360;
                joints[5].targetAngle = targetJ6;
            }
        }
    }
}