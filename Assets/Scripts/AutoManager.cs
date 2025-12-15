using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AutoManager : MonoBehaviour
{
    [Header("Script References")]
    public PhysicsRobotController robotController;
    public GripperController gripper;
    public GeometricSolver solver;
    public PalletCalculator palletCalc;
    public BoxFeeder boxFeeder;

    [Header("Key Objects")]
    public Transform pickPoint;
    public Transform j1Base;

    [Header("Debug Parameters")]
    public float hoverHeight = 0.4f;

    [Header("Startup Settings")]
    [Tooltip("How many seconds to wait after game start before the first grab? (To allow time for the first box to spawn and move)")]
    public float startDelay = 5.0f;

    private float[] initialJointAngles;
    private Vector3 currentTargetPos;

    private bool _isPaused = false;

    void Start()
    {
        RecordInitialAngles();
    }

    public void BeginWork()
    {
        StartCoroutine(StartDelayedJob());
    }

    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        if (_isPaused) Debug.Log("Robot Arm Paused");
        else Debug.Log("Robot Arm Resumed");
    }

    // ========================================================
    // Delayed Start Coroutine
    // ========================================================
    IEnumerator StartDelayedJob()
    {
        Debug.Log($"[System Warmup] Waiting {startDelay} seconds for the 1st box to be in position...");

        yield return new WaitForSeconds(startDelay);

        Debug.Log("[System Start] Starting picking job!");
        StartCoroutine(RunFullPalletJob());
    }

    void RecordInitialAngles()
    {
        if (robotController == null || robotController.joints == null) return;
        int count = robotController.joints.Length;
        initialJointAngles = new float[count];
        for (int i = 0; i < count; i++)
        {
            var j = robotController.joints[i];
            if (j.joint != null) initialJointAngles[i] = GetJointRawAngle(j.joint);
        }
    }

    // ========================================================
    // Execute the full pallet job (Supports multi-layer automatic angle adjustment)
    // ========================================================
    IEnumerator RunFullPalletJob()
    {
        // 1. Get coordinate list for all layers and all boxes
        List<Vector3> allPoints = palletCalc.CalculateAllPoints();
        int totalCount = allPoints.Count;

        Debug.Log($"Pallet planned {totalCount} box positions.");

        // We need box height to calculate which layer it belongs to
        float singleBoxHeight = palletCalc.rawDimensions.y;

        // Pallet Base Y (World Coordinate)
        float palletBaseY = palletCalc.palletStartCorner.position.y;

        for (int i = 0; i < totalCount; i++)
        {
            // ==============================
            // [Core Pause Logic] Loop here until _isPaused is false
            // ==============================
            while (_isPaused)
            {
                yield return null; // Wait next frame, do nothing
            }
            // ==============================

            Debug.Log($">> Processing Box {i + 1} / {totalCount} <<");
            currentTargetPos = allPoints[i];

            // 2. Dynamically calculate required rotation angle for current box

            // A. Calculate relative height from base
            float relativeY = currentTargetPos.y - palletBaseY;

            // B. Calculate Layer Index (0, 1, 2...)
            // E.g. relative 0.14 (half) -> 0.14/0.28 = 0.5 -> floor = Layer 0
            // E.g. relative 0.42 (1.5 height) -> 0.42/0.28 = 1.5 -> floor = Layer 1
            int currentLayerIndex = Mathf.FloorToInt(relativeY / singleBoxHeight);

            // C. Ask PalletCalculator for rotation degrees
            float dynamicAngle = palletCalc.GetRotationForLayer(currentLayerIndex);

            // 3. Pass dynamicAngle to single task
            yield return StartCoroutine(RunSingleBoxSequence(currentTargetPos, dynamicAngle));

            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("=== Job Finished ===");
    }

    // ========================================================
    // Single Sequence: Receive target position as parameter
    // ========================================================
    IEnumerator RunSingleBoxSequence(Vector3 targetPos, float rotationY)
    {
        // Check pause before entering sequence
        while (_isPaused) yield return null;

        Vector3 pickPos = pickPoint.position;
        Vector3 pickHover = pickPos + Vector3.up * hoverHeight;
        Vector3 dropHover = targetPos + Vector3.up * hoverHeight;

        // Step 1: Pick
        //Debug.Log($"Step 1: Pick");
        gripper.PickUp();
        yield return StartCoroutine(WaitForSecondsOrPause(0.5f));

        // Step 2: Lift
        //Debug.Log($"Step 2: Lift");
        MoveRobotTo(pickHover, 0f, "Step 2");
        yield return StartCoroutine(WaitForSecondsOrPause(1f));
        //LogCurrentJointAngles("Status after Lift");

        // ========================================================
        // Notify Feeder to restock
        // ========================================================
        if (boxFeeder != null)
        {
            //Debug.Log("🔔 [AutoManager] Notify spawn next box...");
            boxFeeder.TrySpawnNext();
        }
        else
        {
            Debug.LogWarning("BoxFeeder not assigned, cannot auto-restock!");
        }

        // Step 3: Fly (Move to pallet)
        //Debug.Log($"Step 3: Move to pallet ");
        MoveRobotTo(dropHover, rotationY, "Step 3");
        yield return StartCoroutine(WaitForSecondsOrPause(1f));

        // Step 4: Down
        //Debug.Log($"Step 4: Down");
        MoveRobotTo(targetPos, rotationY, "Step 4");
        yield return StartCoroutine(WaitForSecondsOrPause(0.5f));
        //LogCurrentJointAngles("Status at Place Point");

        // Step 5: Release
        //Debug.Log("Step 5: Release");
        gripper.Release();
        yield return StartCoroutine(WaitForSecondsOrPause(0.5f));

        // Step 6: Retract
        MoveRobotTo(dropHover, rotationY, "Step 6");
        yield return StartCoroutine(WaitForSecondsOrPause(1f));

        // Step 7: Return Home
        //Debug.Log("Step 7: Return Home");
        MoveRobotHome();
        yield return StartCoroutine(WaitForSecondsOrPause(2.0f));
        //LogCurrentJointAngles("Status at Home");
    }

    // --------------------------------------------------------
    // IK and Movement Logic
    // --------------------------------------------------------
    void MoveRobotTo(Vector3 targetPos, float rotationY, string stepName)
    {
        if (!solver.Solve(targetPos, rotationY))
        {
            Debug.LogError($"[IK Failed] {stepName}");
            return;
        }
        for (int i = 0; i < 6; i++)
        {
            if (i < robotController.joints.Length)
                robotController.joints[i].targetAngle = solver.outAngles[i];
        }
    }

    void MoveRobotHome()
    {
        for (int i = 0; i < robotController.joints.Length; i++)
        {
            robotController.joints[i].targetAngle = robotController.joints[i].startAngle;
        }
    }

    // --------------------------------------------------------
    // Log Helpers
    // --------------------------------------------------------
    void LogCurrentJointAngles(string context)
    {
        Debug.Log($"\n—— {context} ——");

        if (solver != null && solver.gripperTip != null)
        {
            Debug.Log($"<color=green>[End Effector] World: {solver.gripperTip.position.ToString("F4")}</color>");
        }

        for (int i = 0; i < robotController.joints.Length; i++)
        {
            if (i >= 4) continue;

            var jointControl = robotController.joints[i];
            if (jointControl.joint == null) continue;

            float currentRaw = GetJointRawAngle(jointControl.joint);
            float initRaw = initialJointAngles[i];
            float actualRelative = Mathf.DeltaAngle(initRaw, currentRaw);
            float target = jointControl.targetAngle;
            float error = Mathf.Abs(actualRelative - target);

            string color = error > 5f ? "red" : "white";
            Debug.Log($"<color={color}>J{i + 1}: Target={target:F1}° / Actual={actualRelative:F1}° (Error:{error:F1})</color>");
        }
    }

    float GetJointRawAngle(HingeJoint joint)
    {
        Vector3 axis = joint.axis;
        Vector3 euler = joint.transform.localEulerAngles;
        if (Mathf.Abs(axis.x) > 0.5f) return euler.x;
        if (Mathf.Abs(axis.y) > 0.5f) return euler.y;
        return euler.z;
    }

    // ========================================================
    // Wait Coroutine with Pause
    // ========================================================
    IEnumerator WaitForSecondsOrPause(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            // If paused, stuck here, stop timer
            while (_isPaused)
            {
                yield return null; // Wait next frame
            }

            // Not paused, timer runs
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // ========================================================
    // Gizmos (Show current dynamic target)
    // ========================================================
    void OnDrawGizmos()
    {
        if (pickPoint == null) return;

        // Pick Point
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(pickPoint.position, 0.05f);

        // Pick Hover Point
        Vector3 pickH = pickPoint.position + Vector3.up * hoverHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pickPoint.position, pickH);

        // Dynamically show target point
        if (Application.isPlaying && currentTargetPos != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(currentTargetPos, 0.05f);

            Vector3 dropH = currentTargetPos + Vector3.up * hoverHeight;
            Gizmos.color = new Color(1, 0.5f, 0); // Orange
            Gizmos.DrawSphere(dropH, 0.05f);
            Gizmos.DrawLine(dropH, currentTargetPos);

            // Draw flight path line
            Gizmos.color = Color.white;
            Gizmos.DrawLine(pickH, dropH);
        }
    }
}