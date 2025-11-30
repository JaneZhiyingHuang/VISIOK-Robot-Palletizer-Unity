using UnityEngine;

// 这个脚本要放在 LateUpdate 里运行，覆盖掉所有动画
[ExecuteInEditMode] // 让你在编辑器里拖动红点也能看到效果
public class ElasticArmIK : MonoBehaviour
{
    [Header("核心目标")]
    public Transform target;    // 红点 (IK_Target)

    [Header("关节链条")]
    public Transform j1Base;    // 旋转底座 (J1)
    public Transform j2Shoulder; // 肩膀/大臂根部 (J2) - 这是一个要被“脱臼”的关节
    public Transform j3Elbow;   // 肘部 (J3)
    public Transform j6Hand;    // 吸盘 (J6)

    [Header("参数设置")]
    // 机械臂完全伸直的最大长度 (J2到J6的距离)
    // 这个值你需要根据你的模型实际情况调整！
    public float maxArmLength = 2.5f;

    [Header("肘部方向修正")]
    // 用来控制肘部是向上弯还是向下弯 (相当于 Hint)
    public Vector3 elbowHintOffset = new Vector3(0, 1, -1);


    void LateUpdate()
    {
        if (target == null || j1Base == null || j2Shoulder == null || j3Elbow == null || j6Hand == null)
            return;

        // ========================================================
        // 1. J1 旋转 (始终对准目标方向)
        // ========================================================
        Vector3 targetPosPlane = target.position;
        targetPosPlane.y = j1Base.position.y; // 抹平高度
        // 让 J1 的 Z 轴指向目标 (如果你的模型是别的轴，这里要改)
        j1Base.LookAt(targetPosPlane, Vector3.up);

        // ========================================================
        // 2. 计算距离和“脱臼”量
        // ========================================================
        // 计算从 J1 到 Target 的总距离
        float distanceToTarget = Vector3.Distance(j1Base.position, target.position);

        // 计算从 J1 指向 Target 的方向向量
        Vector3 directionToTarget = (target.position - j1Base.position).normalized;

        // 默认情况下，J2 应该在 J1 的位置 (没脱臼)
        Vector3 j2IdealPosition = j1Base.position;

        // 如果目标太远，超出了臂长
        if (distanceToTarget > maxArmLength)
        {
            // 计算超出了多少
            float excessDistance = distanceToTarget - maxArmLength;
            // 把 J2 沿着目标方向拉出去，填补这个空缺！
            j2IdealPosition = j1Base.position + directionToTarget * excessDistance;
        }

        // 应用 J2 的新位置 (可能会脱离 J1)
        j2Shoulder.position = j2IdealPosition;


        // ========================================================
        // 3. 简易 IK (让 J2-J3-J6 形成一个三角形去够目标)
        // ========================================================
        // 这一步是为了让手臂看起来是弯曲的，而不是僵硬的直线
        // 这是一个简化的两段式 IK 计算

        float upperArmLen = Vector3.Distance(j2Shoulder.position, j3Elbow.position);
        float forearmLen = Vector3.Distance(j3Elbow.position, j6Hand.position);

        // 使用余弦定理计算肘部角度 (这里省略复杂数学，用简易 LookAt 实现)

        // 让大臂 (J2) 先指向目标
        j2Shoulder.LookAt(target, Vector3.up);
        // 估算一个肘部位置 (向上/向后弯曲)
        Vector3 hintPosition = j2Shoulder.position + j2Shoulder.TransformDirection(elbowHintOffset);

        // 简单的两段 LookAt 模拟 IK 效果
        // 注意：这只是一个视觉近似，为了保证 J6 到位，我们需要最后一招强行吸附

        // ========================================================
        // 4. 【终极绝招】强行把 J6 按在 Target 上
        // ========================================================
        // 无论前面怎么算，最后一步必须强制对齐
        j6Hand.position = target.position;
        j6Hand.rotation = target.rotation;

        // 因为 J6 被强行移动了，J3 需要指向新的 J6 位置来保持视觉连贯
        j3Elbow.LookAt(j6Hand, Vector3.up);
    }
}