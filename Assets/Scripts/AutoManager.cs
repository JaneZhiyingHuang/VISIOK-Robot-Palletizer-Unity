using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AutoManager : MonoBehaviour
{
    [Header("脚本引用")]
    public PhysicsRobotController robotController;
    public GripperController gripper;
    public GeometricSolver solver;
    public PalletCalculator palletCalc;
    public BoxFeeder boxFeeder;

    [Header("关键物体")]
    public Transform pickPoint;
    public Transform j1Base;

    [Header("参数调试")]
    public float hoverHeight = 0.4f;
    public float boxPlaceAngle = 0f;

    [Header("启动设置")]
    [Tooltip("游戏开始后，等待几秒再开始第一次抓取？(给第一个箱子留出生成和移动的时间)")]
    public float startDelay = 5.0f; // 建议设为 4~6秒

    private float[] initialJointAngles;
    private Vector3 currentTargetPos;

    void Start()
    {
        RecordInitialAngles();

        // 【改动】不直接启动，而是通过协程延迟启动
        StartCoroutine(StartDelayedJob());
    }

    // ========================================================
    // 【新增】延迟启动协程
    // ========================================================
    IEnumerator StartDelayedJob()
    {
        Debug.Log($"⏳ [系统预热] 正在等待 {startDelay} 秒，让第1个箱子就位...");

        // 这里就是你想要的“隔一段时间再开始”
        yield return new WaitForSeconds(startDelay);

        Debug.Log("🔥 [系统启动] 开始执行抓取任务！");
        StartCoroutine(RunFullPalletJob());
    }

    void RecordInitialAngles()
    {
        if (robotController == null || robotController.joints == null) return;
        int count = robotController.joints.Length;
        initialJointAngles = new float[count];
        for (int i = 0; i < count; i++)
        {
            var j = robotController.joints[i];
            if (j.joint != null) initialJointAngles[i] = GetJointRawAngle(j.joint);
        }
    }

    IEnumerator RunFullPalletJob()
    {
        List<Vector3> allPoints = palletCalc.CalculateAllPoints();
        int totalCount = allPoints.Count;

        // 【新增】直接问 Calculator：这一批箱子要旋转多少度？
        float currentJobAngle = palletCalc.GetCurrentRotationY();

        Debug.Log($"托盘规划了 {totalCount} 个箱子位。");

        for (int i = 0; i < totalCount; i++)
        {
            Debug.Log($"<color=orange>>> 处理第 {i + 1} / {totalCount} 个箱子 <<</color>");
            currentTargetPos = allPoints[i];
            // 【修改】把角度 currentJobAngle 传进去
            yield return StartCoroutine(RunSingleBoxSequence(currentTargetPos, currentJobAngle));
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("<color=cyan>=== 任务结束 ===</color>");
    }
    // ========================================================
    // 【改动】单次流程：接收目标位置作为参数
    // ========================================================
    IEnumerator RunSingleBoxSequence(Vector3 targetPos, float rotationY)
    {
        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = targetPos + Vector3.up * hoverHeight;

        //// Step0: 移动到抓取点，但不触发 J6 转动
        //Debug.Log("步骤 0: 移动到抓取点 (保持gripper角度)");
        //MoveRobotTo(pickPos, 0f, "Step 0");
        //yield return new WaitForSeconds(1.0f);

        // Step 1: Pick (抓取)
        Debug.Log($"步骤 1: 抓取");
        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // Step 2: Lift (抬起)
        Debug.Log($"步骤 2: 抬起");
        MoveRobotTo(pickHover, 0f, "Step 2");
        // 等待抬起动作完成 (稍微多给一点时间确保完全离开底座)
        yield return new WaitForSeconds(1.5f);
        LogCurrentJointAngles("抬起后状态");

        // ========================================================
        // 【关键逻辑】通知 Feeder 补货
        // 此时箱子已经腾空，PickPoint 空出来了
        // ========================================================
        if (boxFeeder != null)
        {
            Debug.Log("🔔 [AutoManager] 通知生成下一个箱子...");
            boxFeeder.TrySpawnNext();
        }
        else
        {
            Debug.LogWarning("⚠️ 未绑定 BoxFeeder，无法自动补货！");
        }

        // Step 3: Fly (移动到托盘上方)
        Debug.Log($"步骤 3: 移动至托盘上方 (角度: {rotationY})");
        // 这里把原来的 boxPlaceAngle 换成 rotationY
        MoveRobotTo(dropHover, rotationY, "Step 3");
        yield return new WaitForSeconds(1.5f);

        // Step 4: Down (下降)
        Debug.Log($"步骤 4: 下降");
        MoveRobotTo(targetPos, rotationY, "Step 4");
        yield return new WaitForSeconds(1.0f);
        LogCurrentJointAngles("放置点状态");

        // Step 5: Release (放下)
        Debug.Log("步骤 5: 放下");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // Step 6: Retract (撤回)
        // 撤回时最好保持同样的角度，防止碰到旁边的箱子
        MoveRobotTo(dropHover, rotationY, "Step 6");
        yield return new WaitForSeconds(1.5f);

        // Step 7: 归位 (Return Home)
        Debug.Log("步骤 7: 归位");
        MoveRobotHome();
        // 归位时间可以稍微久一点，确保机器人回到初始姿态，准备好去抓刚生成的那个新箱子
        yield return new WaitForSeconds(2.5f);
        LogCurrentJointAngles("归位后状态");
    }

    // --------------------------------------------------------
    // IK 与 移动逻辑 (保持不变)
    // --------------------------------------------------------
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



    void MoveRobotHome()
    {
        for (int i = 0; i < robotController.joints.Length; i++)
        {
            robotController.joints[i].targetAngle = robotController.joints[i].startAngle;
        }
    }

    // --------------------------------------------------------
    // Log 辅助 
    // --------------------------------------------------------
    void LogCurrentJointAngles(string context)
    {
        Debug.Log($"\n—— {context} ——");

        if (solver != null && solver.gripperTip != null)
        {
            Debug.Log($"<color=green>【末端坐标】World: {solver.gripperTip.position.ToString("F4")}</color>");
        }

        for (int i = 0; i < robotController.joints.Length; i++)
        {
            if (i >= 4) continue;

            var jointControl = robotController.joints[i];
            if (jointControl.joint == null) continue;

            float currentRaw = GetJointRawAngle(jointControl.joint);
            float initRaw = initialJointAngles[i];
            float actualRelative = Mathf.DeltaAngle(initRaw, currentRaw);
            float target = jointControl.targetAngle;
            float error = Mathf.Abs(actualRelative - target);

            string color = error > 5f ? "red" : "white";
            Debug.Log($"<color={color}>J{i + 1}: 目标={target:F1}° / 实际={actualRelative:F1}° (误差:{error:F1})</color>");
        }
    }

    float GetJointRawAngle(HingeJoint joint)
    {
        Vector3 axis = joint.axis;
        Vector3 euler = joint.transform.localEulerAngles;
        if (Mathf.Abs(axis.x) > 0.5f) return euler.x;
        if (Mathf.Abs(axis.y) > 0.5f) return euler.y;
        return euler.z;
    }

    // ========================================================
    // Gizmos (更新：显示当前动态目标)
    // ========================================================
    void OnDrawGizmos()
    {
        if (pickPoint == null) return;

        // 抓取点
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(pickPoint.position, 0.05f);

        // 抓取悬停点
        Vector3 pickH = pickPoint.position + Vector3.up * hoverHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pickPoint.position, pickH);

        // 动态显示当前要去的目标点
        if (Application.isPlaying && currentTargetPos != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(currentTargetPos, 0.05f);

            Vector3 dropH = currentTargetPos + Vector3.up * hoverHeight;
            Gizmos.color = new Color(1, 0.5f, 0); // 橙色
            Gizmos.DrawSphere(dropH, 0.05f);
            Gizmos.DrawLine(dropH, currentTargetPos);

            // 画一条线示意飞行轨迹
            Gizmos.color = Color.white;
            Gizmos.DrawLine(pickH, dropH);
        }
    }
}