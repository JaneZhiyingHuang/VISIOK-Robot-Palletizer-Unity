using UnityEngine;
using System.Collections.Generic;

public class PalletCalculator : MonoBehaviour
{
    // ==========================================
    // Define Orientation Enum
    // ==========================================
    public enum BoxOrientation
    {
        [InspectorName("Rotate 90°")] Align_X,
        [InspectorName("Default")] Align_Z_Rotated
    }

    [Header("Core References")]
    [Tooltip("Bottom-left corner of the pallet (Starting point for arrangement)")]
    public Transform palletStartCorner;

    [Header("Pallet Settings")]
    [Tooltip("Effective pallet area size (x=Width, y=Depth/Z)")]
    public Vector2 palletSize = new Vector2(1.2f, 1.0f);

    [Tooltip("Gap between boxes")]
    public float gap = 0.01f;

    [Header("Stacking Height Settings (New Logic)")]
    [Tooltip("Maximum allowed stacking height (Meters)")]
    public float safeHeight = 2.0f;

    [Tooltip("Desired number of layers (Will not exceed max safe layers)")]
    public int targetLayers = 1;

    [Header("Calculation Preview (Read Only)")]
    [Tooltip("Maximum allowed layers calculated from safe height")]
    [SerializeField] private int _calculatedMaxLayers;
    [SerializeField] private int _capacityPerLayerX;
    [SerializeField] private int _capacityPerLayerZ;
    [SerializeField] private int _totalBoxes;

    [Header("Box Settings")]
    [Tooltip("Drag a box from the scene here")]
    public Transform boxReference;

    [Header("Dimensions and Orientation")]
    [Tooltip("Raw dimensions read from box")]
    public Vector3 rawDimensions = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("Select orientation for the [First Layer] on the pallet")]
    public BoxOrientation placementOrientation = BoxOrientation.Align_X;

    // ========================================================
    // 1. Get box dimensions for a specific state (Helper)
    // ========================================================
    private Vector3 GetBoxSize(bool isRotated)
    {
        // If rotated, swap X and Z
        if (isRotated)
        {
            return new Vector3(rawDimensions.z, rawDimensions.y, rawDimensions.x);
        }
        return rawDimensions;
    }

    // ========================================================
    // 2. Determine if a layer needs rotation (Core Interlocking Logic)
    // ========================================================
    public bool IsLayerRotated(int layerIndex)
    {
        // Determine base state of the first layer
        // Note: Per previous logic, Align_Z_Rotated is considered "0f" (No Rot), Align_X is "90f" (Rotated)
        // Here we unify logic:
        // Assume Align_Z_Rotated is "Mode A", Align_X is "Mode B"

        bool isBaseMode = (placementOrientation == BoxOrientation.Align_Z_Rotated);

        // If even layer (0, 2, 4...) -> Keep same state as first layer
        // If odd layer (1, 3, 5...) -> Invert state
        bool useBaseMode = (layerIndex % 2 == 0) ? isBaseMode : !isBaseMode;

        // If final decision is Align_Z_Rotated mode, in GetFinalBoxSize logic it is "rawDimensions.z, y, x" (swapped)
        // So IsRotated = True
        return useBaseMode;
    }

    // ========================================================
    // 3. Core Calculation Logic: Calculate all valid placement points (Supports multi-layer)
    // ========================================================

    public int GetMaxSafeLayers()
    {
        if (rawDimensions.y <= 0.001f) return 1;
        return Mathf.FloorToInt(safeHeight / rawDimensions.y);
    }

    public List<Vector3> CalculateAllPoints()
    {
        List<Vector3> points = new List<Vector3>();

        if (palletStartCorner == null) return points;
        if (rawDimensions.y <= 0.001f) return points; // Prevent infinite loop if height is 0

        // A. Calculate max allowed layers
        _calculatedMaxLayers = GetMaxSafeLayers();

        // B. Determine actual layers to spawn (Take Min)
        int actualLayers = Mathf.Min(targetLayers, _calculatedMaxLayers);
        if (actualLayers < 1) actualLayers = 1;

        // For counting total
        int totalCount = 0;

        // --- Outer Loop: Control Height (Y-axis) ---
        for (int layer = 0; layer < actualLayers; layer++)
        {
            // 1. Determine if this layer is rotated
            bool isRotated = IsLayerRotated(layer);
            Vector3 currentBoxSize = GetBoxSize(isRotated);

            // 2. Avoid invalid dimensions
            if (currentBoxSize.x <= 0.01f || currentBoxSize.z <= 0.01f) continue;

            // 3. Calculate step and capacity for this layer
            float stepX = currentBoxSize.x + gap;
            float stepZ = currentBoxSize.z + gap;
            int countX = Mathf.FloorToInt((palletSize.x + 0.001f) / stepX);
            int countZ = Mathf.FloorToInt((palletSize.y + 0.001f) / stepZ);

            // Just for Inspector preview of first layer data
            if (layer == 0)
            {
                _capacityPerLayerX = countX;
                _capacityPerLayerZ = countZ;
            }

            // --- Inner Loop: Control Plane (X/Z-axis) ---
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    // Calculate local position
                    float localX = (x * stepX) + (currentBoxSize.x / 2f);
                    float localZ = (z * stepZ) + (currentBoxSize.z / 2f);

                    // Y-axis height = (Layer * BoxHeight) + (Half BoxHeight)
                    float localY = (layer * rawDimensions.y) + (currentBoxSize.y / 2f);

                    Vector3 localPos = new Vector3(localX, localY, localZ);
                    Vector3 worldPos = palletStartCorner.TransformPoint(localPos);

                    points.Add(worldPos);
                    totalCount++;
                }
            }
        }

        _totalBoxes = totalCount;
        return points;
    }

    // ========================================================
    // 4. Public Interface for External Calls
    // ========================================================
    public Vector3 GetDropPosition(int index)
    {
        List<Vector3> allPoints = CalculateAllPoints();
        if (index >= 0 && index < allPoints.Count)
        {
            return allPoints[index];
        }
        return Vector3.zero;
    }

    public float GetCurrentRotationY()
    {
        return placementOrientation == BoxOrientation.Align_Z_Rotated ? 0f : 90f;
    }

    public float GetRotationForLayer(int layerIndex)
    {
        bool isRotated = IsLayerRotated(layerIndex);

        return isRotated ? 0f : 90f;
    }

    // ========================================================
    // 5. Auto Detect
    // ========================================================
    [ContextMenu("Auto Detect Box Size")]
    public void AutoDetectBoxSize()
    {
        if (boxReference == null)
        {
            Debug.LogError("Please assign 'Box Reference' first!");
            return;
        }

        Renderer ren = boxReference.GetComponent<Renderer>();
        if (ren != null)
        {
            rawDimensions = ren.bounds.size;
            //Debug.Log($" [Renderer] Read raw dimensions: {rawDimensions}");
            return;
        }

        Collider col = boxReference.GetComponent<Collider>();
        if (col != null)
        {
            rawDimensions = col.bounds.size;
            //Debug.Log($" [Collider] Read raw dimensions: {rawDimensions}");
            return;
        }
    }

    // ========================================================
    // 6. Visualization
    // ========================================================
    void OnDrawGizmos()
    {
        if (palletStartCorner == null) return;

        // A. Draw pallet bounds
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.matrix = palletStartCorner.localToWorldMatrix;
        Vector3 palletCenter = new Vector3(palletSize.x / 2f, 0, palletSize.y / 2f);
        Gizmos.DrawWireCube(palletCenter, new Vector3(palletSize.x, 0.05f, palletSize.y));

        // B. Draw all calculated boxes
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.identity;

        List<Vector3> points = CalculateAllPoints();

        float boxHeight = rawDimensions.y;
        if (boxHeight < 0.001f) return;

        foreach (var pos in points)
        {
            // Reverse calculate layer index (based on Y height)
            // World Y minus Pallet Y divided by Box Height
            float relativeY = pos.y - palletStartCorner.position.y;
            int layerIndex = Mathf.FloorToInt(relativeY / boxHeight);

            // Get dimensions for this layer
            bool isRotated = IsLayerRotated(layerIndex);
            Vector3 size = GetBoxSize(isRotated);

            // Draw
            float rotAngle = GetRotationForLayer(layerIndex);

            // Construct a Matrix at this position with corresponding rotation
            Quaternion rotation = palletStartCorner.rotation * Quaternion.Euler(0, rotAngle, 0);
            Gizmos.matrix = Matrix4x4.TRS(pos, rotation, Vector3.one);

            // Use swapped size, only follow pallet rotation
            Gizmos.matrix = Matrix4x4.TRS(pos, palletStartCorner.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}