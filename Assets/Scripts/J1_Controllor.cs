using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class J1_Control : MonoBehaviour
{
    public Transform target; // 拖入 IK_Target

    [Header("调试修正")]
    [Range(-180, 180)]
    public float offsetAngle = 0f; // 拖动这个来校准方向！

    public bool lockRotation = false; // 测试用：勾选后停止旋转

    void LateUpdate()
    {
        if (target == null || lockRotation) return;

        // 1. 把目标点的世界坐标，转成 J1 的“本地坐标”
        // 这一步非常关键！它会自动处理父级底座的任何旋转
        Vector3 localTargetPos = transform.parent.InverseTransformPoint(target.position);

        // 2. 计算角度 (Atan2)
        // 因为我们绕 Z 轴转，所以我们在 XY 平面上看目标
        // Atan2(y, x) 返回的是弧度
        float angleRad = Mathf.Atan2(localTargetPos.y, localTargetPos.x);

        // 3. 转成度数
        float angleDeg = angleRad * Mathf.Rad2Deg;

        // 4. 应用旋转 (只改 Z 轴！)
        // 加上 offsetAngle，方便你在 Inspector 里微调
        Vector3 currentRot = transform.localEulerAngles;
        transform.localRotation = Quaternion.Euler(currentRot.x, currentRot.y, angleDeg + offsetAngle + 180);
    }
}