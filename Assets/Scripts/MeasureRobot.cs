using UnityEngine;

[ExecuteInEditMode] // 不用运行游戏，挂上去就能看！
public class MeasureRobot : MonoBehaviour
{
    public Transform j2_Shoulder; // 肩膀转轴
    public Transform j3_Elbow;    // 肘部转轴
    public Transform gripperTip;  // 吸盘最底端 (接触箱子的那个点)

    public bool clickToMeasure = false; // 勾选这个来测量

    void Update()
    {
        if (clickToMeasure)
        {
            clickToMeasure = false;
            Measure();
        }
    }

    void Measure()
    {
        if (j2_Shoulder == null || j3_Elbow == null || gripperTip == null)
        {
            Debug.LogError("请先把 J2, J3, 吸盘底端 拖进去！");
            return;
        }

        // 1. 计算 Arm 1 (大臂)
        float dist1 = Vector3.Distance(j2_Shoulder.position, j3_Elbow.position);

        // 2. 计算 Arm 2 (小臂 + 手 + 吸盘)
        float dist2 = Vector3.Distance(j3_Elbow.position, gripperTip.position);

        Debug.Log($"<color=green>=== 测量结果 ===</color>");
        Debug.Log($"Arm 1 Length (应填): {dist1}");
        Debug.Log($"Arm 2 Length (应填): {dist2}");
        Debug.Log($"----------------------");
    }
}