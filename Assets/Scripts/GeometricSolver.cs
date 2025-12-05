using UnityEngine;



public class GeometricSolver : MonoBehaviour

{

    [Header("机械臂尺寸 (数学参数)")]

    public float arm1Length = 1.05f;

    public float arm2Length = 1.77433f;



    [Header("角度修正")]

    public float j1Offset = 0f;

    public float j2Offset = 0f;

    public float j3Offset = 0f;



    // 计算结果

    [HideInInspector] public float[] outAngles = new float[6];



    // ========================================================

    // 【新增】调试用变量 (只为了画图，不影响计算)

    // ========================================================

    [Header("调试可视化 (拖入骨骼显示长度)")]

    public Transform debug_J2_Shoulder; // 拖入 J2

    public Transform debug_J3_Elbow;    // 拖入 J3

    public Transform debug_GripperTip;  // 拖入 Gripper_Sensor (吸盘底)



    public void Solve(Vector3 targetPos, Transform j1Base, Transform j2Shoulder)

    {

        // 1. J1 水平旋转

        Vector3 localTarget = j1Base.InverseTransformPoint(targetPos);

        float angleJ1 = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        outAngles[0] = angleJ1 + j1Offset;



        // 2. 三角形计算

        Vector3 targetFromJ2 = j2Shoulder.InverseTransformPoint(targetPos);

        float xDist = Mathf.Sqrt(targetFromJ2.z * targetFromJ2.z + targetFromJ2.x * targetFromJ2.x);

        float yDist = targetFromJ2.y;



        float c = Mathf.Sqrt(xDist * xDist + yDist * yDist);

        c = Mathf.Clamp(c, 0.01f, arm1Length + arm2Length - 0.01f);



        // 3. J3 角度

        float cosJ3 = (arm1Length * arm1Length + arm2Length * arm2Length - c * c) / (2 * arm1Length * arm2Length);

        float angleJ3Internal = Mathf.Acos(Mathf.Clamp(cosJ3, -1f, 1f)) * Mathf.Rad2Deg;



        // 尝试修正：去掉负号或调整 Offset

        outAngles[2] = -(180 - angleJ3Internal) + j3Offset;



        // 4. J2 角度

        float angleToTarget = Mathf.Atan2(yDist, xDist) * Mathf.Rad2Deg;

        float cosTriangle = (arm1Length * arm1Length + c * c - arm2Length * arm2Length) / (2 * arm1Length * c);

        float angleTriangle = Mathf.Acos(Mathf.Clamp(cosTriangle, -1f, 1f)) * Mathf.Rad2Deg;



        // 尝试修正：去掉负号

        outAngles[1] = (angleToTarget + angleTriangle) + j2Offset;



        // 5. J5 自动水平

        outAngles[4] = -(outAngles[1] + outAngles[2]);



        // 其他归零

        outAngles[3] = 0;

        outAngles[5] = 0;

    }



    // ========================================================

    // 【新增】 Gizmos 画图函数

    // ========================================================

    void OnDrawGizmos()

    {

        // 如果没拖物体，就不画

        if (debug_J2_Shoulder == null) return;



        // --- 1. 画大臂 (Arm 1) ---

        Gizmos.color = Color.yellow;

        // 画出 J2 关节球

        Gizmos.DrawWireSphere(debug_J2_Shoulder.position, 0.1f);

        // 

        // 画出“数学公式认为的大臂长度” (空心圆球范围)

        Gizmos.DrawWireSphere(debug_J2_Shoulder.position, arm1Length);



        if (debug_J3_Elbow != null)

        {

            // 画实线：模型实际的骨骼连接

            Gizmos.DrawLine(debug_J2_Shoulder.position, debug_J3_Elbow.position);



            // --- 2. 画小臂 (Arm 2) ---

            Gizmos.color = Color.cyan;

            // 画出 J3 关节球

            Gizmos.DrawWireSphere(debug_J3_Elbow.position, 0.1f);

            // 以 J3 为中心，画出“数学公式认为的小臂长度”

            Gizmos.DrawWireSphere(debug_J3_Elbow.position, arm2Length);



            if (debug_GripperTip != null)

            {

                // 画实线：模型实际的小臂连接

                Gizmos.DrawLine(debug_J3_Elbow.position, debug_GripperTip.position);

            }

        }

    }

}

