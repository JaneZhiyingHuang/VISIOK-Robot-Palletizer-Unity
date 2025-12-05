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
    public float boxPlaceAngle = 0f; // 箱子想要的世界朝向

    // J6 自动校准参数
    private float autoJ6Offset = 0f;

    void Start()
    {
        // 1. 记录 J6 初始偏差
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

        // --- 准备工作 ---
        Vector3 dropPos = palletCalc.GetDropPosition(0); // 托盘放置点
        Vector3 pickPos = pickPoint.position;            // 抓取点

        // 定义两个“空中安全点” (Hover Points)
        // 抓取点上方 0.4米
        Vector3 pickHover = pickPos + Vector3.up * 0.4f;
        // 放置点上方 0.4米
        Vector3 dropHover = dropPos + Vector3.up * 0.4f;


        // ========================================================
        // 步骤 1: 去抓取点 (直接到位)
        // ========================================================
        Debug.Log("1. 去抓取点");
        //MoveRobotTo(pickPos);
        //yield return new WaitForSeconds(2.5f); 

        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // ========================================================
        // 步骤 2: 抬起 (去 PickHover)
        // ========================================================
        Debug.Log("2. 抬起到安全高度");
        MoveRobotTo(pickHover);
        yield return new WaitForSeconds(2.0f);

        // ========================================================
        // 步骤 3: 飞向托盘上方 (去 DropHover)
        // ========================================================
        Debug.Log("3. 飞向托盘上方 (所有关节自动配合)");
        // 这一步 J1 转动的同时，J2/J3 也会调整伸缩
        // J6 会自动反向旋转保持箱子平齐
        MoveRobotTo(dropHover);
        yield return new WaitForSeconds(3.5f); // 距离远，多给点时间

        // ========================================================
        // 步骤 4: 下降 (去 DropPos)
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
        // 步骤 6: 离开 (回到 DropHover)
        // ========================================================
        Debug.Log("6. 离开");
        MoveRobotTo(dropHover);
        yield return new WaitForSeconds(2.0f);

        Debug.Log("结束！");
    }

    // ---------------------------------------------------------
    // 统一控制函数：输入坐标 -> 自动算出 J1-J6 怎么动
    // ---------------------------------------------------------
    void MoveRobotTo(Vector3 targetPos)
    {
        // 1. 调用数学家计算 J1-J5 的基础角度
        solver.Solve(targetPos, j1Base, j2Shoulder);

        var joints = robotController.joints;
        if (joints.Length >= 6)
        {
            // --- 应用 J1-J5 ---
            if (joints[0].joint != null) joints[0].targetAngle = solver.outAngles[0]; // J1
            if (joints[1].joint != null) joints[1].targetAngle = solver.outAngles[1]; // J2
            if (joints[2].joint != null) joints[2].targetAngle = solver.outAngles[2]; // J3
            // J4 Fixed
            if (joints[4].joint != null) joints[4].targetAngle = solver.outAngles[4]; // J5 (自动水平)

            // --- 应用 J6 (保留你想要的逻辑) ---
            if (joints[5].joint != null)
            {
                // 获取刚才算出来的 J1 目标角度
                float targetJ1 = solver.outAngles[0];

                // 公式：(目标箱子角度 - 底座角度) + 初始校准
                float targetJ6 = (boxPlaceAngle - targetJ1) + autoJ6Offset;

                // 规范化到 -180 ~ 180
                while (targetJ6 > 180) targetJ6 -= 360;
                while (targetJ6 < -180) targetJ6 += 360;

                joints[5].targetAngle = targetJ6;
            }
        }
    }
}