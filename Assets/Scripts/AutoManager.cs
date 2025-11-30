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
    public Transform pickPoint;  // 箱子当前的位置 (用来算抬起高度)

    void Start()
    {
        StartCoroutine(RunOneBox());
    }

    IEnumerator RunOneBox()
    {
        Debug.Log("任务开始：等待 2 秒让物理稳定...");
        yield return new WaitForSeconds(2f);

        // --- 1. 原地抓取 ---
        Debug.Log("1. 原地抓取");
        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // --- 2. 垂直抬起 ---
        Debug.Log("2. 抬起");
        // 算出当前位置上方 0.5 米的坐标
        Vector3 liftPos = pickPoint.position + Vector3.up * 0.5f;
        MoveRobot(liftPos);
        yield return new WaitForSeconds(3f); // 物理运动需要时间

        // --- 3. 计算并移动到托盘上方 ---
        Debug.Log("3. 移动到托盘上方");
        // 获取第 0 个格子的坐标
        Vector3 dropPos = palletCalc.GetDropPosition(0);
        // 加上悬停高度
        Vector3 dropHover = dropPos + Vector3.up * 0.4f;

        MoveRobot(dropHover);
        yield return new WaitForSeconds(4f); // 距离可能比较远，多给点时间

        // --- 4. 下降 ---
        Debug.Log("4. 下降");
        MoveRobot(dropPos);
        yield return new WaitForSeconds(2f);

        // --- 5. 放下 ---
        Debug.Log("5. 放下");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // --- 6. 抬起离开 ---
        Debug.Log("6. 离开");
        MoveRobot(dropHover);
        yield return new WaitForSeconds(2f);

        Debug.Log("测试结束！");
    }

    // 核心：把坐标转成角度，发给控制器
    void MoveRobot(Vector3 targetPos)
    {
        // 1. 算角度 (数学解算器不受影响，它只管算数)
        solver.Solve(targetPos, j1Base, j2Shoulder);

        // 2. 赋值给 PhysicsRobotController
        var joints = robotController.joints;

        // 确保数组长度够
        if (joints.Length >= 6)
        {
            // 给 J1, J2, J3 赋值 (需判空，或者确认拖了)
            if (joints[0].joint != null) joints[0].targetAngle = solver.outAngles[0];
            if (joints[1].joint != null) joints[1].targetAngle = solver.outAngles[1];
            if (joints[2].joint != null) joints[2].targetAngle = solver.outAngles[2];

            // J4 (Fixed) - 【关键】即使为空，跳过即可，不报错
            // joints[3].targetAngle = 0; 

            // J5 (水平)
            if (joints[4].joint != null) joints[4].targetAngle = solver.outAngles[4];

            // J6 (吸盘)
            if (joints[5].joint != null) joints[5].targetAngle = 0;
        }
    }
}