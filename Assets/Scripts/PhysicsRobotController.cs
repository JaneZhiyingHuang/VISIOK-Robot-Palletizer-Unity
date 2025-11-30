using UnityEngine;

[ExecuteAlways]
public class PhysicsRobotController : MonoBehaviour
{
    [System.Serializable]
    public class JointControl
    {
        public HingeJoint joint; // 允许这里为空 (None)

        [Header("初始姿态")]
        public Vector3 initialEuler;

        [Header("控制面板")]
        [Range(-180, 180)]
        public float startAngle = 0f;
        [Range(-180, 180)]
        public float targetAngle = 0f;
    }

    // 依然保持 6 个元素，方便对号入座
    public JointControl[] joints;
    public GripperController gripper;

    // ... (RecordZeroPose 等代码保持不变) ...

    void Update()
    {
        // 编辑模式预览
        if (!Application.isPlaying)
        {
            foreach (var j in joints)
            {
                // 【关键修改】如果没拖关节，直接跳过，不要报错
                if (j.joint == null) continue;

                // ... (原有的旋转预览逻辑) ...
                Quaternion zeroRot = Quaternion.Euler(j.initialEuler);
                Quaternion moveRot = Quaternion.AngleAxis(j.targetAngle, j.joint.axis);
                j.joint.transform.localRotation = zeroRot * moveRot;
                j.startAngle = j.targetAngle;
            }
        }

        // 运行模式按键
        if (Application.isPlaying && gripper != null)
        {
            if (Input.GetKeyDown(KeyCode.G)) gripper.PickUp();
            if (Input.GetKeyDown(KeyCode.R)) gripper.Release();
        }
    }

    void FixedUpdate()
    {
        if (Application.isPlaying)
        {
            foreach (var j in joints)
            {
                // 【关键修改】物理驱动前也检查一下是否为空
                if (j.joint == null) continue;

                ApplyPhysics(j, j.targetAngle);
            }
        }
    }

    void ApplyPhysics(JointControl j, float angle)
    {
        // 再加一层保险
        if (j.joint == null) return;

        JointSpring spring = j.joint.spring;
        spring.targetPosition = angle;
        j.joint.spring = spring;
    }

    // ... (Start 和 Awake 函数里也要记得加 if (j.joint == null) continue;) ...
}