using UnityEngine;

public class GeometricSolver : MonoBehaviour
{
    [Header("机械臂尺寸")]
    public float arm1Length = 1.05f;
    public float arm2Length = 1.774f;

    [Header("角度修正")]
    public float j1Offset = 0f;
    public float j2Offset = 0f;
    public float j3Offset = 0f;

    [HideInInspector] public float[] outAngles = new float[6];

    public void Solve(Vector3 targetPos, Transform j1Base, Transform j2Shoulder)
    {
        // 1. J1
        Vector3 localTarget = j1Base.InverseTransformPoint(targetPos);
        float angleJ1 = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        outAngles[0] = angleJ1 + j1Offset;

        // 2. 三角形 J2/J3
        Vector3 targetFromJ2 = j2Shoulder.InverseTransformPoint(targetPos);
        float xDist = Mathf.Sqrt(targetFromJ2.z * targetFromJ2.z + targetFromJ2.x * targetFromJ2.x);
        float yDist = targetFromJ2.y;

        float c = Mathf.Sqrt(xDist * xDist + yDist * yDist);
        c = Mathf.Clamp(c, 0.01f, arm1Length + arm2Length - 0.01f);

        // J3
        float cosJ3 = (arm1Length * arm1Length + arm2Length * arm2Length - c * c) / (2 * arm1Length * arm2Length);
        float angleJ3Internal = Mathf.Acos(Mathf.Clamp(cosJ3, -1f, 1f)) * Mathf.Rad2Deg;
        // 注意：根据你的反馈，这里可能需要去掉负号，或者调整 Offset
        outAngles[2] = (180 - angleJ3Internal) + j3Offset;

        // J2
        float angleToTarget = Mathf.Atan2(yDist, xDist) * Mathf.Rad2Deg;
        float cosTriangle = (arm1Length * arm1Length + c * c - arm2Length * arm2Length) / (2 * arm1Length * c);
        float angleTriangle = Mathf.Acos(Mathf.Clamp(cosTriangle, -1f, 1f)) * Mathf.Rad2Deg;
        // 注意：根据你的反馈，这里可能需要去掉负号
        outAngles[1] = -(angleToTarget + angleTriangle) + j2Offset;

        // J5 (水平)
        outAngles[4] = -(outAngles[1] + outAngles[2]);

        outAngles[3] = 0;
        outAngles[5] = 0;
    }
}