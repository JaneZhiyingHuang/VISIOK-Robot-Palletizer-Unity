using UnityEngine;

public class BoxMover : MonoBehaviour
{
    [Header("必须配置")]
    public Transform targetPoint;
    public Transform boxCenterReference;

    [Header("设置")]
    public float moveSpeed = 0.5f;

    public bool IsArrived { get; private set; } = false;

    private Vector3 finalDestination;
    private bool isMoving = false;

    void Start()
    {
        // 刚开始先算一次 (可能会算到旧的坐标，没关系，后面会被校准器修正)
        CalculatePath();
    }

    // ========================================================
    // 【供 PointCalibrator 调用】
    // ========================================================
    public void RecalculateDestination()
    {
        Debug.Log($"[BoxMover] 收到校准信号，正在更新终点...");
        CalculatePath();
    }

    void CalculatePath()
    {
        if (targetPoint == null || boxCenterReference == null) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 offset = boxCenterReference.position - transform.position;

        // 此时读取的 targetPoint.position 已经是校准器修改过的新坐标了
        float targetX = targetPoint.position.x - offset.x;
        float targetZ = targetPoint.position.z - offset.z;
        float fixedY = transform.position.y;

        finalDestination = new Vector3(targetX, fixedY, targetZ);

        isMoving = true;
        IsArrived = false;
        this.enabled = true;
    }

    void Update()
    {
        if (!isMoving) return;

        float step = moveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, finalDestination, step);

        if (Vector3.Distance(transform.position, finalDestination) < 0.001f)
        {
            transform.position = finalDestination;
            isMoving = false;
            IsArrived = true;
            Debug.Log("✅ 箱子中心已对齐，停止。");
            this.enabled = false;
        }
    }

    // OnDrawGizmos...
}