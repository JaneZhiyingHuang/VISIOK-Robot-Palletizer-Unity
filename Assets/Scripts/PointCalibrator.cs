using UnityEngine;
using System.Collections;

public class PointCalibrator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("需要引用 Solver 来获取 J6 的位置")]
    public GeometricSolver solver;

    [Tooltip("要被移动/校准的那个空物体 (绿球)")]
    public Transform pickPoint;

    [Header("设置")]
    [Tooltip("等待机械臂物理稳定需要的时间")]
    public float waitTime = 2.0f;

    void Start()
    {
        // 游戏一开始，自动启动校准
        StartCoroutine(CalibrateRoutine());
    }

    IEnumerator CalibrateRoutine()
    {
        Debug.Log($"<color=magenta>[校准器] 正在等待 {waitTime} 秒，让机械臂物理下垂...</color>");

        // 1. 等待物理引擎稳定 (机械臂会因为重力稍微往下掉一点)
        yield return new WaitForSeconds(waitTime);

        // 检查引用
        if (solver != null && solver.j6Hand != null && pickPoint != null)
        {
            // 2. 【核心修改】获取 J6 (法兰/手腕) 的真实世界坐标
            // 之前用的是 gripperTip，现在改用 j6Hand
            Vector3 j6RealPos = solver.j6Hand.position;

            Debug.Log($"[校准数据] PickPoint旧坐标: {pickPoint.position}");
            Debug.Log($"[校准数据] J6 真实坐标: {j6RealPos}");

            // 3. 【核心修正】
            // 将 PickPoint 的 X 和 Z 强行对齐到 J6
            // Y 轴保持 PickPoint 原来的高度 (传送带高度)
            Vector3 newPos = new Vector3(j6RealPos.x, pickPoint.position.y, j6RealPos.z);

            // 应用坐标
            pickPoint.position = newPos;

            Debug.Log($"<color=green>[校准完成] PickPoint 已对齐到 J6 正下方！新坐标: {pickPoint.position}</color>");

            // =========================================================
            // 4. 通知箱子重新计算路径
            // =========================================================
            // 因为箱子可能一开始读的是旧坐标，现在坐标变了，必须通知它重算
            BoxMover activeBox = FindObjectOfType<BoxMover>();
            if (activeBox != null)
            {
                activeBox.RecalculateDestination();
            }
        }
        else
        {
            Debug.LogError("❌ 校准失败：Solver, J6 或 PickPoint 引用缺失！");
        }
    }
}