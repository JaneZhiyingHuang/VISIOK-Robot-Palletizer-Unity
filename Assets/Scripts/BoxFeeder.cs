using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoxFeeder : MonoBehaviour
{
    [Header("多尺寸预制体设置")]
    public GameObject prefabL;
    public GameObject prefabM;
    public GameObject prefabS;

    private GameObject _activePrefab;

    [Header("核心引用")]
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

    public UIManager uiManager;

    void Start()
    {
        if (spawnPoint == null || palletCalculator == null) return;
        if (pickupPointReference == null) pickupPointReference = spawnPoint;

        SwitchBoxType("L");
    }

    public void StartSpawning()
    {
        SpawnBox();
    }


    public void SwitchBoxType(string type)
    {
        // 1. 切换 Prefab
        if (type == "L") _activePrefab = prefabL;
        else if (type == "M") _activePrefab = prefabM;
        else if (type == "S") _activePrefab = prefabS;

        if (_activePrefab == null) return;

        Debug.Log($"[BoxFeeder] 切换箱型为: {type}");

        // A. 把当前的 Prefab 赋值给 Pallet 的 boxReference
        palletCalculator.boxReference = _activePrefab.transform;

        // B. 调用 Pallet 现有的自动读取函数
        palletCalculator.AutoDetectBoxSize();

        // 2. 重置计数
        currentSpawnedCount = 0;

        // 3. 刷新容量
        RefreshCapacity();
    }

    public void RefreshCapacity()
    {
        // Pallet 重新计算点位
        List<Vector3> points = palletCalculator.CalculateAllPoints();
        totalCapacity = points.Count;
        Debug.Log($"[BoxFeeder] 容量已更新为: {totalCapacity}");
    }

    public void TrySpawnNext()
    {
        // 如果箱子已经满了
        if (currentSpawnedCount >= totalCapacity)
        {
            // 告诉 UIManager 任务完成了
            if (uiManager != null) uiManager.NotifyJobFinished();
            return;
        }
        SpawnBox();
    }

    void SpawnBox()
    {
        if (_activePrefab == null) return;
        if (currentSpawnedCount >= totalCapacity) return;

        // 1. 初始生成
        Vector3 initialPos = spawnPoint.position + positionOffset;
        Quaternion finalRot = Quaternion.Euler(spawnRotationEuler);

        GameObject newBox = Instantiate(_activePrefab, initialPos, finalRot);
        newBox.name = $"Box_{currentSpawnedCount + 1}";


        // 2. 获取 BoxMover 脚本
        var boxScript = newBox.GetComponent<BoxMover>();

        if (boxScript != null)
        {
            boxScript.targetPoint = this.pickupPointReference;

            // 位置修正逻辑
            if (boxScript.boxCenterReference == null)
            {
                Transform foundCenter = newBox.transform.Find("Box_Center");
                if (foundCenter == null) foundCenter = newBox.transform.Find("Center");
                boxScript.boxCenterReference = (foundCenter != null) ? foundCenter : newBox.transform;
            }

            Vector3 currentCenterWorldPos = boxScript.boxCenterReference.position;
            Vector3 desiredCenterPos = spawnPoint.position + positionOffset;
            Vector3 deviation = currentCenterWorldPos - desiredCenterPos;
            newBox.transform.position -= deviation;

            boxScript.RecalculateDestination();
        }

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