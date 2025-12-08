using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoxFeeder : MonoBehaviour
{
    [Header("核心设置")]
    public GameObject boxPrefab;
    public Transform spawnPoint;
    public PalletCalculator palletCalculator;

    [Header("场景引用自动修复")]
    public Transform pickupPointReference;

    [Header("位置与旋转修正")]
    public Vector3 spawnRotationEuler = Vector3.zero;
    public Vector3 positionOffset = Vector3.zero;

    [Header("状态 (只读)")]
    [SerializeField] private int totalCapacity = 0;
    [SerializeField] private int currentSpawnedCount = 0;
    [SerializeField] private GameObject currentActiveBox;

    void Start()
    {
        if (boxPrefab == null || spawnPoint == null || palletCalculator == null) return;
        if (pickupPointReference == null) pickupPointReference = spawnPoint;

        List<Vector3> points = palletCalculator.CalculateAllPoints();
        totalCapacity = points.Count;

        SpawnBox();
    }

    public void TrySpawnNext()
    {
        if (currentSpawnedCount >= totalCapacity) return;
        SpawnBox();
    }

    void SpawnBox()
    {
        if (currentSpawnedCount >= totalCapacity) return;

        // 1. 初始生成 (此时 Pivot 会对齐 SpawnPoint，但 Mesh 可能会偏)
        Vector3 initialPos = spawnPoint.position + positionOffset;
        Quaternion finalRot = Quaternion.Euler(spawnRotationEuler);

        GameObject newBox = Instantiate(boxPrefab, initialPos, finalRot);
        newBox.name = $"Box_{currentSpawnedCount + 1}";

        // 2. 获取 BoxMover 脚本
        var boxScript = newBox.GetComponent<BoxMover>();

        if (boxScript != null)
        {
            // 自动填入 TargetPoint
            boxScript.targetPoint = this.pickupPointReference;

            // ====================================================================
            // 【核心修复】利用 Box_Center 修正生成位置
            // ====================================================================

            // A. 确保 boxScript 里已经引用了 Box_Center
            // 如果 Prefab 里没拖，尝试自动找一下名字叫 "Box_Center" 或 "Center" 的子物体
            if (boxScript.boxCenterReference == null)
            {
                Transform foundCenter = newBox.transform.Find("Box_Center"); // 你的子物体名字
                if (foundCenter == null) foundCenter = newBox.transform.Find("Center");

                // 如果实在找不到，就只能用自己了（那样就没法修偏移了）
                boxScript.boxCenterReference = (foundCenter != null) ? foundCenter : newBox.transform;
            }

            // B. 计算偏移并修正
            // 现在的 Center 在哪？
            Vector3 currentCenterWorldPos = boxScript.boxCenterReference.position;

            // 我们希望 Center 在哪？(我们希望 Center 正好落在 spawnPoint 上)
            Vector3 desiredCenterPos = spawnPoint.position + positionOffset;

            // 差距是多少？
            Vector3 deviation = currentCenterWorldPos - desiredCenterPos;

            // 把箱子整体往回挪，抵消这个差距
            newBox.transform.position -= deviation;

            // ====================================================================

            // 3. 通知箱子锁定目标
            boxScript.RecalculateDestination();
        }

        currentActiveBox = newBox;
        currentSpawnedCount++;
    }

    void OnDrawGizmos()
    {
        if (spawnPoint == null) return;
        Gizmos.color = Color.cyan;
        Vector3 targetPos = spawnPoint.position + positionOffset;
        Gizmos.DrawWireSphere(targetPos, 0.05f);
    }
}