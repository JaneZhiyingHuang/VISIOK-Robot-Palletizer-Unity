using UnityEngine;

// [ExecuteAlways] Allows the script to execute Update even when the game is not running
[ExecuteAlways]
public class PhysicsRobotController : MonoBehaviour
{
    [System.Serializable]
    public class JointControl
    {
        // public string name;
        public HingeJoint joint;

        [Header("Initial Pose (Do not edit manually, right-click script to save)")]
        public Vector3 initialEuler; // Records the original rotation value of the joint

        [Header("Control Panel")]
        [Range(-180, 180)]
        public float startAngle = 0f; // The angle to rotate to automatically on startup

        [Range(-180, 180)]
        public float targetAngle = 0f; // Real-time control angle
    }

    public JointControl[] joints;
    public GripperController gripper;

    // ---------------------------------------------------------
    // Right-click script -> Select "Record Zero Pose"
    // Must do this step first to tell the script "what looks like 0 degrees"
    // ---------------------------------------------------------
    [ContextMenu("1. Record Zero Pose")]
    public void RecordZeroPose()
    {
        foreach (var j in joints)
        {
            if (j.joint != null)
            {
                // Record current rotation as the "baseline"
                j.initialEuler = j.joint.transform.localEulerAngles;
                j.targetAngle = 0f;
                j.startAngle = 0f;
            }
        }
        Debug.Log("✅ Zero pose recorded! Now sliding the slider rotates based on this pose.");
    }

    // ---------------------------------------------------------
    // Reset function: If messed up, click this to return to zero pose
    // ---------------------------------------------------------
    [ContextMenu("2. Reset to Zero")]
    public void ResetToZero()
    {
        foreach (var j in joints)
        {
            if (j.joint != null)
            {
                j.joint.transform.localEulerAngles = j.initialEuler;
                j.targetAngle = 0f;
            }
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            foreach (var j in joints)
            {
                if (j.joint == null) continue;
                // Game starts, apply StartAngle, forceSnap=true means forced positioning
                ApplyPhysics(j, j.startAngle, true);
                j.targetAngle = j.startAngle; // Sync UI
            }
        }
    }

    void FixedUpdate()
    {
        if (Application.isPlaying)
        {
            foreach (var j in joints)
            {
                if (j.joint != null) ApplyPhysics(j, j.targetAngle, false);
            }
        }
    }

    void Update()
    {
        // ==================================================
        // 1. Edit Mode Logic (Preview)
        // ==================================================
        if (!Application.isPlaying)
        {
            foreach (var j in joints)
            {
                if (j.joint != null)
                {
                    // 1. Get initial baseline rotation
                    Quaternion zeroRot = Quaternion.Euler(j.initialEuler);

                    // 2. Get rotation axis (Prevent axis from being 0 causing no movement)
                    Vector3 axis = j.joint.axis.normalized;
                    if (axis == Vector3.zero) axis = Vector3.right; // Default to X axis to prevent errors

                    // 3. Calculate incremental rotation
                    Quaternion moveRot = Quaternion.AngleAxis(j.targetAngle, axis);

                    // 4. Blend together (Baseline * Increment)
                    j.joint.transform.localRotation = zeroRot * moveRot;

                    j.startAngle = j.targetAngle; // Sync save
                }
            }
        }

        // ==================================================
        // 2. Play Mode Logic (Keys)
        // ==================================================
        if (Application.isPlaying && gripper != null)
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                Debug.Log("Command: Pick Up");
                gripper.PickUp();
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("Command: Release");
                gripper.Release();
            }
        }
    }

    // ========================================================
    // Intelligent Shortest Path Application + Speed Limit
    // ========================================================
    void ApplyPhysics(JointControl j, float targetAngle, bool forceSnap)
    {
        if (j.joint == null) return;

        JointSpring spring = j.joint.spring;

        if (forceSnap)
        {
            spring.targetPosition = targetAngle;
        }
        else
        {
            float currentSpringTarget = spring.targetPosition;

            // 1. Calculate "Shortest Path" difference (-180 to 180)
            float delta = Mathf.DeltaAngle(currentSpringTarget, targetAngle);

            // 2. Speed limit (Prevent violent rushing)
            float maxStep = 10f;
            delta = Mathf.Clamp(delta, -maxStep, maxStep);

            // 3. Accumulate difference on top of old value
            spring.targetPosition = currentSpringTarget + delta;
        }

        j.joint.spring = spring;
    }

    void OnDrawGizmos()
    {
        if (joints == null) return;
        foreach (var j in joints)
        {
            if (j.joint != null)
            {
                // 1. Draw joint position (Blue sphere)
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(j.joint.transform.position, 0.05f);

                // 2. Draw joint rotation axis (Long yellow line) -- This is the "pivot" it rotates around
                Gizmos.color = Color.yellow;
                // Get axis in world space
                Vector3 worldAxis = j.joint.transform.TransformDirection(j.joint.axis);
                Gizmos.DrawRay(j.joint.transform.position, worldAxis * 2f);
            }
        }
    }
}