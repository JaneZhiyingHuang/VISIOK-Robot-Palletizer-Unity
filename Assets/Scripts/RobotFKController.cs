using UnityEngine;

public class RobotFKController : MonoBehaviour
{
    [System.Serializable]
    public class RobotJoint
    {
        public string name;        // 关节名字 (方便你看)
        public Transform bone;     // 拖入骨骼 J1, J2...
        public Vector3 rotationAxis = Vector3.forward; // 绕哪个轴转? (1,0,0)是X, (0,1,0)是Y, (0,0,1)是Z

        [Range(-180, 180)]
        public float targetAngle = 0f; // 目标角度 (滑块)
        public float currentAngle = 0f; // 内部记录当前角度
    }

    public float speed = 30f; // 转动速度 (度/秒)

    // 定义 6 个关节
    public RobotJoint[] joints;

    void Update()
    {
        foreach (var j in joints)
        {
            if (j.bone == null) continue;

            // 1. 平滑插值：让角度慢慢变到目标值
            j.currentAngle = Mathf.MoveTowards(j.currentAngle, j.targetAngle, speed * Time.deltaTime);

            // 2. 应用旋转
            // 核心逻辑：保持初始旋转，在此基础上叠加新的旋转
            // 注意：这里假设你的模型初始状态是归零的。如果不是，可能需要记录初始四元数。
            // 简单版：直接设置 localRotation
            j.bone.localRotation = Quaternion.Euler(j.rotationAxis * j.currentAngle);
        }
    }

    // 供外部调用：设置某个关节的角度
    public void SetJointAngle(int index, float angle)
    {
        if (index >= 0 && index < joints.Length)
        {
            joints[index].targetAngle = angle;
        }
    }
}