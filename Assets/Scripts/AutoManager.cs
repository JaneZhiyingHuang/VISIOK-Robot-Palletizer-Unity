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
    public float startDelay = 5.0f;

    private float[] initialJointAngles;
    private Vector3 currentTargetPos;

    private bool _isPaused = false;

    void Start()
    {
        RecordInitialAngles();
    }

    public void BeginWork()
    {
        StartCoroutine(StartDelayedJob());
    }

    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        if (_isPaused) Debug.Log("⏸️ 机械臂已暂停");
        else Debug.Log("▶️ 机械臂继续运行");
    }

    // ========================================================
    // 延迟启动协程
    // ========================================================
    IEnumerator StartDelayedJob()
    {
        Debug.Log($"⏳ [系统预热] 正在等待 {startDelay} 秒，让第1个箱子就位...");

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

    // ========================================================
    // 【修改执行整个托盘的任务 (支持多层自动变角度)
    // ========================================================
    IEnumerator RunFullPalletJob()
    {
        // 1. 获取所有层、所有箱子的坐标列表
        List<Vector3> allPoints = palletCalc.CalculateAllPoints();
        int totalCount = allPoints.Count;

        Debug.Log($"托盘规划了 {totalCount} 个箱子位。");

        // 我们需要知道每个箱子的高度，以便反算它属于第几层
        // (注意：这里假设所有箱子高度一致，如果不一致需要改逻辑)
        float singleBoxHeight = palletCalc.rawDimensions.y;

        // 托盘底部的 Y 坐标 (世界坐标)
        float palletBaseY = palletCalc.palletStartCorner.position.y;

        for (int i = 0; i < totalCount; i++)
        {
            // ==============================
            // 【核心暂停逻辑】卡在这里死循环，直到 _isPaused 变为 false
            // ==============================
            while (_isPaused)
            {
                yield return null; // 等待下一帧，什么都不做
            }
            // ==============================

            Debug.Log($"<color=orange>>> 处理第 {i + 1} / {totalCount} 个箱子 <<</color>");
            currentTargetPos = allPoints[i];

            // 2. 动态计算当前这个箱子需要的旋转角度

            // A. 算出当前箱子相对于托盘底部的相对高度
            float relativeY = currentTargetPos.y - palletBaseY;

            // B. 算出这是第几层 (0, 1, 2...)
            // 比如相对高度 0.14(半高) -> 0.14/0.28 = 0.5 -> floor = 0层
            // 比如相对高度 0.42(一层半) -> 0.42/0.28 = 1.5 -> floor = 1层
            int currentLayerIndex = Mathf.FloorToInt(relativeY / singleBoxHeight);

            // C. 问 PalletCalculator 这一层应该转多少度
            float dynamicAngle = palletCalc.GetRotationForLayer(currentLayerIndex);

            // 3. 把算出来的 dynamicAngle 传给单次任务
            yield return StartCoroutine(RunSingleBoxSequence(currentTargetPos, dynamicAngle));

            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("<color=cyan>=== 任务结束 ===</color>");
    }

    // ========================================================
    // 单次流程：接收目标位置作为参数
    // ========================================================
    IEnumerator RunSingleBoxSequence(Vector3 targetPos, float rotationY)
    {
        // 【新增】进入流程前先检查一下暂停
        while (_isPaused) yield return null;

        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = targetPos + Vector3.up * hoverHeight;

        // Step 1: Pick (抓取)
        Debug.Log($"步骤 1: 抓取");
        gripper.PickUp();
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(0.5f));

        // Step 2: Lift (抬起)
        Debug.Log($"步骤 2: 抬起");
        MoveRobotTo(pickHover, 0f, "Step 2");
        // 等待抬起动作完成 (稍微多给一点时间确保完全离开底座)
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(1f));

        // 这一步只是打印 Log，不需要等待，所以这里不加暂停检查也可以
        LogCurrentJointAngles("抬起后状态");

        // ========================================================
        // 通知 Feeder 补货
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
        // 注意：这里已经使用了传入的 rotationY
        Debug.Log($"步骤 3: 移动至托盘上方 (角度: {rotationY})");
        MoveRobotTo(dropHover, rotationY, "Step 3");
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(1f));

        // Step 4: Down (下降)
        Debug.Log($"步骤 4: 下降");
        MoveRobotTo(targetPos, rotationY, "Step 4");
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(0.5f));
        LogCurrentJointAngles("放置点状态");

        // Step 5: Release (放下)
        Debug.Log("步骤 5: 放下");
        gripper.Release();
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(0.5f));

        // Step 6: Retract (撤回)
        MoveRobotTo(dropHover, rotationY, "Step 6");
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(1f));

        // Step 7: 归位 (Return Home)
        Debug.Log("步骤 7: 归位");
        MoveRobotHome();
        // 【修改】换成带暂停的等待
        yield return StartCoroutine(WaitForSecondsOrPause(2.0f));
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
    // 【新增】带暂停功能的等待协程
    // ========================================================
    IEnumerator WaitForSecondsOrPause(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            // 如果暂停了，就卡死在这里，不再增加 timer
            while (_isPaused)
            {
                yield return null; // 等待下一帧
            }

            // 没暂停，计时器才走
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ========================================================
    // Gizmos (显示当前动态目标)
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