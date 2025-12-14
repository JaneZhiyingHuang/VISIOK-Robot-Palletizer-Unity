using UnityEngine;
using System.Collections;

public class PointCalibrator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Requires Solver reference to get J6 position")]
    public GeometricSolver solver;

    [Tooltip("The empty object (Green Sphere) to be moved/calibrated")]
    public Transform pickPoint;

    [Header("Settings")]
    [Tooltip("Time required to wait for robot arm physics to settle")]
    public float waitTime = 2.0f;

    void Start()
    {
        StartCoroutine(CalibrateRoutine());
    }

    IEnumerator CalibrateRoutine()
    {
        Debug.Log($"<color=magenta>[Calibrator] Waiting {waitTime} seconds for robot arm physics to settle...</color>");

        // 1. Wait for physics engine to settle (Robot arm will droop slightly due to gravity)
        yield return new WaitForSeconds(waitTime);

        // Check references
        if (solver != null && solver.j6Hand != null && pickPoint != null)
        {
            // 2. Get real world position of J6 (Flange/Wrist)
            Vector3 j6RealPos = solver.j6Hand.position;

            Debug.Log($"[Calibration Data] PickPoint Old Pos: {pickPoint.position}");
            Debug.Log($"[Calibration Data] J6 Real Pos: {j6RealPos}");

            // 3. Force align PickPoint X and Z to J6
            // Keep Y axis at PickPoint's original height (Conveyor height)
            Vector3 newPos = new Vector3(j6RealPos.x, pickPoint.position.y, j6RealPos.z);

            // Apply position
            pickPoint.position = newPos;

            Debug.Log($"<color=green>[Calibration Complete] PickPoint aligned directly below J6! New Pos: {pickPoint.position}</color>");

            // =========================================================
            // 4. Notify box to recalculate path
            // =========================================================
            // Since box might have read old coordinates initially, must notify it to recalculate now that coordinates changed
            BoxMover activeBox = FindObjectOfType<BoxMover>();
            if (activeBox != null)
            {
                activeBox.RecalculateDestination();
            }
        }
        else
        {
            Debug.LogError("❌ Calibration Failed: Solver, J6 or PickPoint reference missing!");
        }
    }
}