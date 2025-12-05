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

    public float liftAngleOffset = 40f;

    public float boxPlaceAngle = 0f; // 0=平行X轴, 90=平行Z轴



    void Start()

    {

        StartCoroutine(RunOneBox());

    }



    IEnumerator RunOneBox()

    {

        yield return new WaitForSeconds(1f);



        // 1. 原地抓取

        Debug.Log("1. 原地抓取");

        gripper.PickUp();

        yield return new WaitForSeconds(0.5f);



        // ========================================================

        // 2. J3 独立抬起 (J1/J2 不动)

        // ========================================================

        Debug.Log("2. J3 独立抬起");



        float currentJ2 = robotController.joints[1].targetAngle;

        float currentJ3 = robotController.joints[2].targetAngle;



        // 计算目标

        float targetJ3 = currentJ3 + liftAngleOffset;

        float targetJ5 = -(currentJ2 + targetJ3); // J5保持水平



        // 赋值

        if (robotController.joints[2].joint != null) robotController.joints[2].targetAngle = targetJ3;

        if (robotController.joints[4].joint != null) robotController.joints[4].targetAngle = targetJ5;



        yield return new WaitForSeconds(2.5f);



        // ========================================================

        // 3. J1 独立旋转 + J6 跟随对齐 (核心修改)

        // ========================================================

        Debug.Log("3. J1 旋转 (带 J6 对齐)");



        // A. 算出目标点 (只是为了算 J1 角度)

        Vector3 dropPos = palletCalc.GetDropPosition(0);



        // 此时高度不重要，只要 X 和 Z 对就行，随便给个高度让数学家算

        Vector3 rotateTarget = dropPos + Vector3.up * 0.5f;

        solver.Solve(rotateTarget, j1Base, j2Shoulder);



        // B. 【简化版 J6 逻辑】

        // 既然 J1 要动了，J6 必须马上反向动，抵消旋转

        float targetJ1 = solver.outAngles[0];

        float targetJ6 = boxPlaceAngle - targetJ1; // 简单的减法公式



        // 规范化角度 (-180 ~ 180)

        while (targetJ6 > 180) targetJ6 -= 360;

        while (targetJ6 < -180) targetJ6 += 360;



        // C. 赋值给电机 (只动 J1 和 J6，其他不动)

        if (robotController.joints[0].joint != null) robotController.joints[0].targetAngle = targetJ1;

        if (robotController.joints[5].joint != null) robotController.joints[5].targetAngle = targetJ6;



        yield return new WaitForSeconds(3.0f); // 等底座转完



        // ========================================================

        // 4. 伸出并下降 (手臂动作)

        // ========================================================

        Debug.Log("4. 伸出并下降");



        // J1 和 J6 已经到位了，现在让大臂小臂动起来

        MoveArmOnly(dropPos);



        yield return new WaitForSeconds(3.0f);



        // 5. 放下

        Debug.Log("5. 放下");

        gripper.Release();

        yield return new WaitForSeconds(0.5f);



        // 6. 离开

        Debug.Log("6. 离开");



        // 简单的反向抬起逻辑

        float dropJ2 = robotController.joints[1].targetAngle;

        float dropJ3 = robotController.joints[2].targetAngle;

        float leaveJ3 = dropJ3 + liftAngleOffset;

        float leaveJ5 = -(dropJ2 + leaveJ3);



        if (robotController.joints[2].joint != null) robotController.joints[2].targetAngle = leaveJ3;

        if (robotController.joints[4].joint != null) robotController.joints[4].targetAngle = leaveJ5;



        yield return new WaitForSeconds(2f);

        Debug.Log("结束！");

    }



    // 辅助函数：只动 J2, J3, J5 (手臂)，不动 J1 和 J6

    void MoveArmOnly(Vector3 targetPos)

    {

        solver.Solve(targetPos, j1Base, j2Shoulder);

        var joints = robotController.joints;



        if (joints.Length >= 6)

        {

            // J1 不动 (保持刚才的旋转)

            // if (joints[0].joint != null) joints[0].targetAngle = solver.outAngles[0];



            if (joints[1].joint != null) joints[1].targetAngle = solver.outAngles[1]; // J2

            if (joints[2].joint != null) joints[2].targetAngle = solver.outAngles[2]; // J3



            // J4 Fixed



            if (joints[4].joint != null) joints[4].targetAngle = solver.outAngles[4]; // J5



            // J6 不动 (保持刚才的对齐)

        }

    }

}