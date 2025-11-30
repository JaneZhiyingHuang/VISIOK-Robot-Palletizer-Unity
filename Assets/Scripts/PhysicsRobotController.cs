using UnityEngine;

public class PhysicsRobotController : MonoBehaviour
{
    [System.Serializable]
    public class JointControl
    {
        public string name;
        public HingeJoint joint; // 拖入挂了 HingeJoint 的关节
        [Range(-180, 180)]
        public float targetAngle = 0f; // 拖动这个来控制！
    }

    public JointControl[] joints; // 数组大小设为 6

    void FixedUpdate() // 物理操作必须在 FixedUpdate
    {
        foreach (var j in joints)
        {
            if (j.joint == null) continue;

            // 获取当前的 Spring 设置
            JointSpring spring = j.joint.spring;

            // 修改目标角度
            spring.targetPosition = j.targetAngle;

            // 应用回去 (这一步必须做，否则不生效)
            j.joint.spring = spring;
        }
    }
}