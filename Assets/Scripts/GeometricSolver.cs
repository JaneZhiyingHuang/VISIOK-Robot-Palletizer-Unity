using UnityEngine;

public class GeometricSolver : MonoBehaviour
{
    [Header("机械臂尺寸 (已填入你之前的数据)")]
    public float arm1Length = 0.638f;
    public float arm2Length = 1.313f;

    [Header("角度修正 (如果反了改这里)")]
    public float j1Offset = 0f;
    public float j2Offset = 0f;
    public float j3Offset = 0f;

    // 计算结果
    [HideInInspector] public float[] outAngles = new float[6];

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
        outAngles[2] = -(180 - angleJ3Internal) + j3Offset;

        // 4. J2 角度
        float angleToTarget = Mathf.Atan2(yDist, xDist) * Mathf.Rad2Deg;
        float cosTriangle = (arm1Length * arm1Length + c * c - arm2Length * arm2Length) / (2 * arm1Length * c);
        float angleTriangle = Mathf.Acos(Mathf.Clamp(cosTriangle, -1f, 1f)) * Mathf.Rad2Deg;
        outAngles[1] = -(angleToTarget + angleTriangle) + j2Offset;

        // 5. J5 自动水平
        outAngles[4] = -(outAngles[1] + outAngles[2]);

        // 其他归零
        outAngles[3] = 0;
        outAngles[5] = 0;
    }
}