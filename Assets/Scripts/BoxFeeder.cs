using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoxFeeder : MonoBehaviour
{
    [Header("Multi-size Prefab Settings")]
    public GameObject prefabL;
    public GameObject prefabM;
    public GameObject prefabS;

    private GameObject _activePrefab;

    [Header("Core References")]
    public Transform spawnPoint;
    public PalletCalculator palletCalculator;

    [Header("Scene Reference Auto-Fix")]
    public Transform pickupPointReference;

    [Header("Position and Rotation Correction")]
    public Vector3 spawnRotationEuler = Vector3.zero;
    public Vector3 positionOffset = Vector3.zero;

    [Header("Status (Read Only)")]
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
        // 1. Switch Prefab
        if (type == "L") _activePrefab = prefabL;
        else if (type == "M") _activePrefab = prefabM;
        else if (type == "S") _activePrefab = prefabS;

        if (_activePrefab == null) return;

        Debug.Log($"[BoxFeeder] Switched Box Type to: {type}");

        // A. Assign current Prefab to Pallet's boxReference
        palletCalculator.boxReference = _activePrefab.transform;

        // B. Call Pallet's existing auto-detect function
        palletCalculator.AutoDetectBoxSize();

        // 2. Reset counter
        currentSpawnedCount = 0;

        // 3. Refresh capacity
        RefreshCapacity();
    }

    public void RefreshCapacity()
    {
        // Pallet recalculates points
        List<Vector3> points = palletCalculator.CalculateAllPoints();
        totalCapacity = points.Count;
        Debug.Log($"[BoxFeeder] Capacity updated to: {totalCapacity}");
    }

    public void TrySpawnNext()
    {
        // If boxes are full
        if (currentSpawnedCount >= totalCapacity)
        {
            // Tell UIManager the job is finished
            if (uiManager != null) uiManager.NotifyJobFinished();
            return;
        }
        SpawnBox();
    }

    void SpawnBox()
    {
        if (_activePrefab == null) return;
        if (currentSpawnedCount >= totalCapacity) return;

        // 1. Initial Spawn
        Vector3 initialPos = spawnPoint.position + positionOffset;
        Quaternion finalRot = Quaternion.Euler(spawnRotationEuler);

        GameObject newBox = Instantiate(_activePrefab, initialPos, finalRot);
        newBox.name = $"Box_{currentSpawnedCount + 1}";


        // 2. Get BoxMover script
        var boxScript = newBox.GetComponent<BoxMover>();

        if (boxScript != null)
        {
            boxScript.targetPoint = this.pickupPointReference;

            // Position correction logic
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