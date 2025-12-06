using UnityEngine;

// [ExecuteAlways] 允许脚本在不运行游戏时也能执行 Update
[ExecuteAlways]
public class PhysicsRobotController : MonoBehaviour
{
    [System.Serializable]
    public class JointControl
    {
        // public string name;
        public HingeJoint joint;

        [Header("初始姿态 (不要手动改，右键脚本保存)")]
        public Vector3 initialEuler; // 记录关节原本的旋转值

        [Header("控制面板")]
        [Range(-180, 180)]
        public float startAngle = 0f; // 开机时自动转到的角度

        [Range(-180, 180)]
        public float targetAngle = 0f; // 实时控制的角度
    }

    public JointControl[] joints;
    public GripperController gripper;

    // ---------------------------------------------------------
    // 【关键功能】右键脚本 -> 选择 "Record Zero Pose"
    // 必须先做这一步，告诉脚本“什么样子才算 0 度”
    // ---------------------------------------------------------
    [ContextMenu("1. 记录当前姿态为零位 (Record Zero Pose)")]
    public void RecordZeroPose()
    {
        foreach (var j in joints)
        {
            if (j.joint != null)
            {
                // 把现在的旋转记录下来，作为“基准”
                j.initialEuler = j.joint.transform.localEulerAngles;
                j.targetAngle = 0f; // 重置控制滑块
                j.startAngle = 0f;
            }
        }
        Debug.Log("✅ 已记录零位！现在拖动滑块是基于这个姿态旋转。");
    }

    // ---------------------------------------------------------
    // 还原功能：如果拖乱了，点这个回到零位
    // ---------------------------------------------------------
    [ContextMenu("2. 还原到零位 (Reset to Zero)")]
    public void ResetToZero()
    {
        foreach (var j in joints)
        {
            if (j.joint != null)
            {
                j.joint.transform.localEulerAngles = j.initialEuler;
                j.targetAngle = 0f;
            }
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            foreach (var j in joints)
            {
                if (j.joint == null) continue;
                // 游戏开始，应用 StartAngle，forceSnap=true 表示强制归位
                ApplyPhysics(j, j.startAngle, true);
                j.targetAngle = j.startAngle; // 同步UI
            }
        }
    }

    void FixedUpdate()
    {
        if (Application.isPlaying)
        {
            foreach (var j in joints)
            {
                if (j.joint != null) ApplyPhysics(j, j.targetAngle, false);
            }
        }
    }

    void Update()
    {
        // ==================================================
        // 1. 编辑模式逻辑 (预览)
        // ==================================================
        if (!Application.isPlaying)
        {
            foreach (var j in joints)
            {
                if (j.joint != null)
                {
                    // 1. 获取初始基准旋转
                    Quaternion zeroRot = Quaternion.Euler(j.initialEuler);

                    // 2. 获取旋转轴 (防止轴是0导致不动)
                    Vector3 axis = j.joint.axis.normalized;
                    if (axis == Vector3.zero) axis = Vector3.right; // 默认给个X轴防止报错

                    // 3. 计算增量旋转
                    Quaternion moveRot = Quaternion.AngleAxis(j.targetAngle, axis);

                    // 4. 混合在一起 (基准 * 增量)
                    j.joint.transform.localRotation = zeroRot * moveRot;

                    j.startAngle = j.targetAngle; // 同步保存
                }
            }
        }

        // ==================================================
        // 2. 运行模式逻辑 (按键)
        // ==================================================
        if (Application.isPlaying && gripper != null)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                Debug.Log("指令：抓取");
                gripper.PickUp();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("指令：释放");
                gripper.Release();
            }
        }
    }

    // ========================================================
    // 【核心修复】智能最短路径应用 + 速度限制
    // ========================================================
    void ApplyPhysics(JointControl j, float targetAngle, bool forceSnap)
    {
        if (j.joint == null) return;

        JointSpring spring = j.joint.spring;

        if (forceSnap)
        {
            spring.targetPosition = targetAngle;
        }
        else
        {
            float currentSpringTarget = spring.targetPosition;

            // 1. 计算 "最短路径" 差值 (-180 到 180)
            float delta = Mathf.DeltaAngle(currentSpringTarget, targetAngle);

            // 2. 速度限制 (防止暴冲)
            float maxStep = 10f;
            delta = Mathf.Clamp(delta, -maxStep, maxStep);

            // 3. 在旧值的基础上累加差值
            spring.targetPosition = currentSpringTarget + delta;
        }

        j.joint.spring = spring;
    }
}