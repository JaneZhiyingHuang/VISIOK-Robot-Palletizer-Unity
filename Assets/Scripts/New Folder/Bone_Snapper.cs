using UnityEngine;

public class BoneSnapper : MonoBehaviour
{
    public Transform target; // 你的 IK_Target (红点)

    [Header("设置")]
    public bool snapPosition = true; // 锁定位置
    public bool snapRotation = true; // 锁定旋转 (解决翻转的关键!)

    // LateUpdate 是在所有 IK 和动画算完之后才运行的
    // 所以它是无敌的，没人能覆盖它
    void LateUpdate()
    {
        if (target == null) return;

        // 1. 强行瞬移位置
        if (snapPosition)
        {
            transform.position = target.position;
        }

        // 2. 强行扭转角度 (这一步能彻底治好 J5 上下翻转的病)
        if (snapRotation)
        {
            transform.rotation = target.rotation;
        }
    }
}