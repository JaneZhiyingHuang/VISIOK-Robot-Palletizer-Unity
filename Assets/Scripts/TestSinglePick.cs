using UnityEngine;
using System.Collections;

public class TestSinglePick : MonoBehaviour
{
    [Header("组件")]
    public GripperController gripper;
    public Transform ikTarget;
    public Transform j1Base;          // 拖入 J1 [009s] 物体

    [Header("目标位置")]
    public Transform palletDropPoint;

    [Header("参数")]
    public float moveSpeed = 1.0f;
    public float rotateSpeed = 45.0f; // 旋转速度 (度/秒)
    public float liftHeight = 0.5f;

    void Start()
    {
        if (palletDropPoint != null && j1Base != null)
            StartCoroutine(PickAndPlaceSequence());
        else
            Debug.LogError("请在 Inspector 里拖入 Pallet Drop Point 和 J1 Base！");
    }

    IEnumerator PickAndPlaceSequence()
    {
        // 1. 抓取
        yield return new WaitForSeconds(0.5f);
        Debug.Log("1. 抓取");
        gripper.PickUp();
        yield return new WaitForSeconds(0.5f);

        // 2. 垂直抬起 (Lift)
        Debug.Log("2. 抬起");
        Vector3 liftPos = ikTarget.position + Vector3.up * liftHeight;
        yield return MoveTo(liftPos);

        // =======================================================
        // 3. 旋转对准 (Rotate) - 核心修改部分
        // =======================================================
        Debug.Log("3. 旋转 J1 对准托盘");

        // 计算目标向量
        Vector3 directionToPallet = palletDropPoint.position - j1Base.position;
        Vector3 directionCurrent = ikTarget.position - j1Base.position;

        // 【修改点1】计算角度时，也使用 J1 的 Z 轴 (forward) 作为参考轴
        // 这样算出来的角度才是绕 Z 轴旋转的角度
        float angleToRotate = Vector3.SignedAngle(directionCurrent, directionToPallet, j1Base.forward);

        // 执行画圆弧旋转
        yield return RotateAroundBase(angleToRotate);


        // =======================================================
        // 4. 水平伸缩 (Reach)
        // =======================================================
        Debug.Log("4. 伸出/缩回 到托盘上方");

        // 目标位置：托盘位置，但高度要保持在空中
        // 这里我们要重新基于目前的高度来组合坐标
        Vector3 hoverOverPallet = palletDropPoint.position;
        hoverOverPallet.y = ikTarget.position.y;

        //yield return MoveTo(hoverOverPallet);

        // 5. 下降 (Down)
        Debug.Log("5. 下降");
        //yield return MoveTo(palletDropPoint.position);

        // 6. 释放 (Release)
        Debug.Log("6. 释放");
        gripper.Release();
        yield return new WaitForSeconds(0.5f);

        // 7. 抬起离开
        //Debug.Log("7. 抬起离开");
        //yield return MoveTo(hoverOverPallet);
    }

    // ==========================================
    // 让 IK Target 绕着 J1 的 Z 轴画圆弧
    // ==========================================
    IEnumerator RotateAroundBase(float angleTotal)
    {
        float angleMoved = 0f;

        // 判断旋转方向 (正转还是反转)
        float direction = Mathf.Sign(angleTotal);
        float absAngle = Mathf.Abs(angleTotal);

        while (angleMoved < absAngle)
        {
            // 这一帧转多少度？
            float step = rotateSpeed * Time.deltaTime;

            // 防止转过头
            if (angleMoved + step > absAngle) step = absAngle - angleMoved;

            // 【修改点2 - 关键！】
            // 不要用 Vector3.up (那是世界Y轴)
            // 改用 j1Base.forward (这是 J1 的 Z 轴！)
            // 这样红点就会顺着 J1 的倾斜角度画圆，距离永远不变
            ikTarget.RotateAround(j1Base.position, j1Base.forward, step * direction);

            angleMoved += step;
            yield return null;
        }
    }

    // 直线移动
    IEnumerator MoveTo(Vector3 targetPos)
    {
        while (Vector3.Distance(ikTarget.position, targetPos) > 0.01f)
        {
            ikTarget.position = Vector3.MoveTowards(ikTarget.position, targetPos, moveSpeed * Time.deltaTime);
            yield return null;
        }
        ikTarget.position = targetPos;
    }
}