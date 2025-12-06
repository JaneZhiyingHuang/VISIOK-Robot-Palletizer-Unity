//using UnityEngine;

//[ExecuteInEditMode] // 不用运行，挂上去就能看！
//public class TrajectoryDebugger : MonoBehaviour
//{
//    public GeometricSolver solver;
//    public Transform j1Base;
//    public Transform j2Shoulder;
//    public Transform pickPoint;

//    [Header("调试设置")]
//    public float liftHeight = 0.4f; // 抬起高度
//    [Range(2, 50)] public int resolution = 20; // 画多少帧？(密度)

//    // 轴向定义 (必须跟你的模型一致)
//    // 你的 J1 绕 Z 转，说明 Forward(Z) 是旋转轴，Up(Y) 是 0度方向
//    private Vector3 j1Axis = Vector3.forward;
//    private Vector3 j1ZeroDir = Vector3.up;

//    void OnDrawGizmos()
//    {
//        if (solver == null || j1Base == null || j2Shoulder == null || pickPoint == null) return;

//        // 1. 获取起点和终点
//        Vector3 startPos = pickPoint.position;
//        Vector3 endPos = startPos + Vector3.up * liftHeight;

//        // 循环画出这一段路程中的每一帧
//        for (int i = 0; i <= resolution; i++)
//        {
//            float t = (float)i / resolution;
//            Vector3 currentTarget = Vector3.Lerp(startPos, endPos, t);

//            // ========================================================
//            // 核心：调用数学家，算出这一帧的“虚拟角度”
//            // ========================================================
//            solver.Solve(currentTarget, j1Base, j2Shoulder);

//            // 获取解算出来的角度
//            float angleJ1 = solver.outAngles[0];
//            float angleJ2 = solver.outAngles[1];
//            // 注意：这里我们需要 J3 的绝对角度来画图，比较麻烦
//            // 所以我们直接用“数学三角形”来反推肘部位置，这样最准

//            DrawVirtualArm(currentTarget, angleJ1, angleJ2);
//        }
//    }

//    // 画出一个“虚拟火柴人”手臂
//    void DrawVirtualArm(Vector3 targetPos, float j1Angle, float j2Angle)
//    {
//        // 1. 确定 J1 旋转平面 (底座朝向)
//        // 既然 J1 绕 Z 转，我们先算出底座转了多少度后的 "前方"
//        // 这是一个纯数学推导，用来验证 Solver 的逻辑

//        // 计算 J2 (肩膀) 的位置
//        Vector3 p_Shoulder = j2Shoulder.position;

//        // --- 计算 J3 (肘部) 的虚拟位置 ---
//        // 我们利用 Solver 里的几何逻辑反推

//        // A. 算出目标相对于肩膀的向量
//        Vector3 toTarget = targetPos - p_Shoulder;

//        // B. 投影距离 (水平 & 垂直)
//        // 假设 J1 旋转轴是 Z，那么水平面是 XY
//        // 我们需要算出在这个旋转平面上的 2D 坐标

//        // 这种反推太依赖轴向，我们用更简单的方法：
//        // 直接利用 Solver 的臂长参数画圆！

//        float r1 = solver.arm1Length;
//        float r2 = solver.arm2Length;
//        float dist = toTarget.magnitude;

//        // 利用余弦定理算出内部结构
//        // 大臂与“肩膀-目标连线”的夹角 (Alpha)
//        float cosAlpha = (r1 * r1 + dist * dist - r2 * r2) / (2 * r1 * dist);
//        float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)); // 弧度

//        // 目标的仰角 (Beta)
//        // 我们需要在一个这就垂直于地面的平面内计算
//        // 简单来说：我们直接在 "J2 -> Target" 这根线的平面上画三角形

//        // 这是一个 3D 旋转问题：
//        // 1. 找到旋转轴：是 "J1轴" 和 "J2-Target" 构成的平面的法线
//        Vector3 crossAxis = Vector3.Cross(j1Base.forward, toTarget).normalized;

//        // 2. 旋转 J2-Target 向量 Alpha 度，得到大臂向量
//        Quaternion rot = Quaternion.AngleAxis(alpha * Mathf.Rad2Deg, crossAxis);
//        Vector3 arm1Vec = rot * toTarget.normalized * r1;

//        // 3. 算出肘部位置
//        Vector3 p_Elbow = p_Shoulder + arm1Vec;

//        // --- 开始画线 ---

//        // 颜色渐变：从绿(开始) 到 红(结束)
//        Gizmos.color = new Color(1, 1, 0, 0.3f); // 半透明黄色

//        // 画大臂 (肩 -> 肘)
//        Gizmos.DrawLine(p_Shoulder, p_Elbow);

//        // 画小臂 (肘 -> 手)
//        Gizmos.DrawLine(p_Elbow, targetPos);

//        // 画关节
//        Gizmos.DrawWireSphere(p_Elbow, 0.02f);
//    }
//}