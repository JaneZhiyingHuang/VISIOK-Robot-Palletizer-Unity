using UnityEngine;

public class BoxMover : MonoBehaviour
{
    [Header("Required Configuration")]
    [Tooltip("Target point for the box (World Coordinates)")]
    public Transform targetPoint;
    [Tooltip("Geometric center of the box (For alignment)")]
    public Transform boxCenterReference;

    [Header("Settings")]
    public float moveSpeed = 0.5f;

    public bool IsArrived { get; private set; } = false;

    private Vector3 finalDestination;
    private bool isMoving = false;

    void Start()
    {
        // Calculate once at start
        CalculatePath();
    }

    // ========================================================
    // [Called by PointCalibrator]
    // ========================================================
    public void RecalculateDestination()
    {
        CalculatePath();
    }

    void CalculatePath()
    {
        if (targetPoint == null || boxCenterReference == null) return;

        // 1. Force disable physics simulation to prevent interference with movement
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // 2. Calculate offset in world coordinates
        Vector3 worldOffset = boxCenterReference.position - transform.position;

        // 3. Calculate the final world coordinate where the Pivot should be
        float targetX = targetPoint.position.x - worldOffset.x;
        float targetZ = targetPoint.position.z - worldOffset.z;

        // Maintain current Y-axis height (World Coordinate)
        float fixedY = transform.position.y;

        // 4. Assemble final world destination
        finalDestination = new Vector3(targetX, fixedY, targetZ);

        isMoving = true;
        IsArrived = false;
        this.enabled = true;
    }

    void Update()
    {
        if (!isMoving) return;

        // 1. Calculate step
        float step = moveSpeed * Time.deltaTime;

        // 2. Use MoveTowards to move in world coordinates
        transform.position = Vector3.MoveTowards(transform.position, finalDestination, step);

        // 3. Check arrival (Using world coordinate distance)
        if (Vector3.Distance(transform.position, finalDestination) < 0.001f)
        {
            // Snap to position to eliminate floating point error
            transform.position = finalDestination;

            isMoving = false;
            IsArrived = true;

            // ===========================================================
            // Restore physics after arrival so the Robot Gripper can detect it!
            // ===========================================================
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // Restore physics sensing
                rb.WakeUp();            // Force wake up to ensure collision detection takes effect immediately
                // Uncomment line below if gravity is needed to settle on ground
                // rb.useGravity = true; 
            }
            // ===========================================================

            Debug.Log("✅ Box center aligned, physics restored, stopping.");
            this.enabled = false;
        }
    }

    // ========================================================
    // Add visualization gizmos
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