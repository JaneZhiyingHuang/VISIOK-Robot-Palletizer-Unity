using UnityEngine;

public class BoxMover : MonoBehaviour
{
    [Header("必须配置")]
    [Tooltip("箱子要去的目标点 (世界坐标)")]
    public Transform targetPoint;
    [Tooltip("箱子的几何中心 (用于对齐)")]
    public Transform boxCenterReference;

    [Header("设置")]
    public float moveSpeed = 0.5f;

    public bool IsArrived { get; private set; } = false;

    private Vector3 finalDestination;
    private bool isMoving = false;

    void Start()
    {
        // 刚开始先算一次
        CalculatePath();
    }

    // ========================================================
    // 【供 PointCalibrator 调用】
    // ========================================================
    public void RecalculateDestination()
    {
        CalculatePath();
    }

    void CalculatePath()
    {
        if (targetPoint == null || boxCenterReference == null) return;

        // 1. 强制关闭物理模拟，防止干扰移动
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // 2. 计算世界坐标系下的偏移量
        Vector3 worldOffset = boxCenterReference.position - transform.position;

        // 3. 计算轴心 (Pivot) 最终应该在的世界坐标
        float targetX = targetPoint.position.x - worldOffset.x;
        float targetZ = targetPoint.position.z - worldOffset.z;

        // 保持当前的 Y 轴高度 (世界坐标)
        float fixedY = transform.position.y;

        // 4. 组装最终的世界坐标终点
        finalDestination = new Vector3(targetX, fixedY, targetZ);

        isMoving = true;
        IsArrived = false;
        this.enabled = true;
    }

    void Update()
    {
        if (!isMoving) return;

        // 1. 步长计算
        float step = moveSpeed * Time.deltaTime;

        // 2. 使用 MoveTowards 在世界坐标系中移动
        transform.position = Vector3.MoveTowards(transform.position, finalDestination, step);

        // 3. 判断到达 (使用世界坐标距离)
        if (Vector3.Distance(transform.position, finalDestination) < 0.001f)
        {
            // 强制吸附，消除浮点误差
            transform.position = finalDestination;

            isMoving = false;
            IsArrived = true;

            // ===========================================================
            // 到位后恢复物理，让机械臂 Gripper 能感应到！
            // ===========================================================
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // 恢复物理感应
                rb.WakeUp();            // 强制唤醒，确保碰撞检测立即生效
                // 如果需要重力让它贴合地面，可以把下面这行取消注释
                // rb.useGravity = true; 
            }
            // ===========================================================

            Debug.Log("✅ 箱子中心已对齐，物理已恢复，停止。");
            this.enabled = false;
        }
    }

    // ========================================================
    // 添加可视化辅助线 (Gizmos)
    // ========================================================
    void OnDrawGizmos()
    {
        if (boxCenterReference != null && targetPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(boxCenterReference.position, 0.03f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(targetPoint.position, 0.03f);

            if (isMoving || Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Vector3 projectedCenterDest = new Vector3(targetPoint.position.x, boxCenterReference.position.y, targetPoint.position.z);
                Gizmos.DrawLine(boxCenterReference.position, projectedCenterDest);
            }
        }
    }
}