using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoxFeeder : MonoBehaviour
{
    [Header("多尺寸预制体设置")]
    public GameObject prefabL;
    public GameObject prefabM;
    public GameObject prefabS;

    // 当前正在使用的预制体
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

    void Start()
    {
        if (spawnPoint == null || palletCalculator == null) return;
        if (pickupPointReference == null) pickupPointReference = spawnPoint;

        // 默认选中 L (或者你在 Inspector 里手动指定的逻辑)
        // 注意：这里最好也调用一次 SwitchBoxType 来确保 Pallet 数据同步
        SwitchBoxType("L");
    }

    public void StartSpawning()
    {
        SpawnBox();
    }

    // ========================================================
    // 供 UI 调用的切换方法 (修改版)
    // ========================================================
    public void SwitchBoxType(string type)
    {
        // 1. 切换 Prefab
        if (type == "L") _activePrefab = prefabL;
        else if (type == "M") _activePrefab = prefabM;
        else if (type == "S") _activePrefab = prefabS;

        if (_activePrefab == null) return;

        Debug.Log($"[BoxFeeder] 切换箱型为: {type}");

        // ========================================================
        // 【核心修改】直接利用 PalletCalculator 现有的逻辑
        // ========================================================

        // A. 把当前的 Prefab 赋值给 Pallet 的 boxReference
        // (PalletCalculator 原本是拖场景物体的，但拖 Prefab transform 也可以读到 Collider/Renderer)
        palletCalculator.boxReference = _activePrefab.transform;

        // B. 调用 Pallet 现有的自动读取函数
        // 这就像你在 Inspector 上点了一下 "Auto Detect"
        palletCalculator.AutoDetectBoxSize();

        // ========================================================

        // 2. 重置计数
        currentSpawnedCount = 0;

        // 3. 刷新容量 (此时 Pallet 里的 rawDimensions 已经是新的了)
        RefreshCapacity();
    }

    public void RefreshCapacity()
    {
        // Pallet 重新计算点位
        List<Vector3> points = palletCalculator.CalculateAllPoints();
        totalCapacity = points.Count;
        Debug.Log($"[BoxFeeder] 容量已更新为: {totalCapacity}");
    }

    public UIManager uiManager;

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