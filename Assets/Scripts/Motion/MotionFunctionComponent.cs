using UnityEngine;

[RequireComponent(typeof(Transform))]
public class MotionFunctionComponent : MonoBehaviour
{
    [Header("Debug (optional)")]
    public GameObject ghostPrefab;
    private RealPositionGhost ghostInstance;

    [Header("Motion")]
    public MotionFunctionSO motionFunction;
    public Transform realTransform;   // used for physics / AI
    public Transform visualTransform; // what the player sees
    public bool enforceSpeedLimit = true;

    private Vector3 motionCenter;     // objects start here
    private float motionStartTime;

    const float EPS = 1e-6f;

    void Start()
    {
        if (realTransform == null) realTransform = this.transform;
        if (visualTransform == null) visualTransform = this.transform;

        if (motionFunction == null)
        {
            Debug.LogError("Assign a MotionFunctionSO to MotionFunctionComponent.", this);
            return;
        }

        // The object's starting position defines its motion center.
        motionCenter = realTransform.position;
        // record the time the motion started so we evaluate functions using local time
        motionStartTime = Time.time;

        // Create a ghost if a prefab exists
        if (ghostPrefab != null)
        {
            // Create/ensure a DebugManager/Ghosts parent in the scene for cleanliness
            GameObject debugManager = GameObject.Find("DebugManager");
            if (debugManager == null)
            {
                debugManager = new GameObject("DebugManager");
                debugManager.AddComponent<GhostToggleSystem>(); // optional: create a toggle if none exists
            }

            Transform ghostsParent = debugManager.transform.Find("Ghosts");
            if (ghostsParent == null)
            {
                GameObject gp = new GameObject("Ghosts");
                gp.transform.SetParent(debugManager.transform, false);
                ghostsParent = gp.transform;
            }

            GameObject g = Instantiate(ghostPrefab, motionCenter, realTransform.rotation, ghostsParent);
            // Ensure there's a RealPositionGhost component
            ghostInstance = g.GetComponent<RealPositionGhost>();
            if (ghostInstance == null)
                ghostInstance = g.AddComponent<RealPositionGhost>();

            // Point ghost at the real transform so it can follow when enabled
            ghostInstance.targetRealTransform = realTransform;
            ghostInstance.followRotation = true;
            g.name = gameObject.name + "_Ghost";

            // Set initial position immediately (important if ghosts start disabled)
            g.transform.position = realTransform.position;
            g.transform.rotation = realTransform.rotation;

            // Optionally disable the ghost GameObject initially if ghosts are disabled
            g.SetActive(GhostToggleSystem.GhostsEnabled);
        }
    }

    void Update()
    {
        if (motionFunction == null) return;
        if (realTransform == null || visualTransform == null) return;
        float tWorld = Time.time;
        float dt = Time.deltaTime;
        float c = (TimeDilationSystem.Instance != null)
            ? TimeDilationSystem.Instance.C
            : 10f;

        // local time since motion started
        float localT = tWorld - motionStartTime;

        // ------- REAL POSITION (true physics location) -------
        Vector3 relativePosNow = motionFunction.EvaluatePosition(localT);

        // Clamp instantaneous speed to c
        if (enforceSpeedLimit && dt > EPS)
        {
            float tPrev = tWorld - dt;
            float localPrev = tPrev - motionStartTime;
            Vector3 prevRelative = motionFunction.EvaluatePosition(localPrev);
            Vector3 desired = relativePosNow - prevRelative;
            float desiredSpeed = desired.magnitude / dt;

            if (desiredSpeed > c)
            {
                Vector3 clamped = desired.normalized * c * dt;
                relativePosNow = prevRelative + clamped;
            }
        }

        Vector3 realWorldPos = motionCenter + relativePosNow;

        // If we're outside the play area, wrap the object to the opposite side by
        // shifting the motion center. This keeps the motion function's local-space
        // behavior consistent while teleporting the object across the border.
        if (PlayAreaManager.Instance != null && PlayAreaManager.Instance.IsOutside(realWorldPos))
        {
            Vector3 wrapped = PlayAreaManager.Instance.WrapPosition(realWorldPos);
            Vector3 delta = wrapped - realWorldPos;
            motionCenter += delta; // shift center so subsequent relative positions map to wrapped world pos
            realWorldPos = wrapped;
        }

        // Ensure the real transform still exists (it may have been destroyed externally)
        if (realTransform != null)
        {
            realTransform.position = realWorldPos;
            realTransform.rotation = motionFunction.EvaluateRotation(localT);
        }

        // If a ghost exists, keep its active state in sync with the global toggle
        if (ghostInstance != null)
        {
            // If the target real transform has been destroyed, destroy the ghost to avoid dangling references
            if (ghostInstance.targetRealTransform == null)
            {
                Destroy(ghostInstance.gameObject);
                ghostInstance = null;
            }
            else if (ghostInstance.gameObject.activeSelf != GhostToggleSystem.GhostsEnabled)
            {
                ghostInstance.gameObject.SetActive(GhostToggleSystem.GhostsEnabled);
                // if enabling, snap immediately to the real position to avoid origin-pop
                if (GhostToggleSystem.GhostsEnabled && realTransform != null)
                {
                    ghostInstance.transform.position = realTransform.position;
                    ghostInstance.transform.rotation = realTransform.rotation;
                }
            }
        }

        // ------- VISIBLE POSITION (retarded time: t - d/c) -------
        var player = FindObjectOfType<PlayerController>()?.transform;
        if (player == null)
        {
            // If no player yet, use real position
            visualTransform.position = realWorldPos;
            visualTransform.rotation = realTransform.rotation;
            return;
        }

        float distance = Vector3.Distance(player.position, realWorldPos);
        float tDelayed = tWorld - distance / c;

        // Prevent time from going before the motion started
        // convert delayed world-time to local motion time
        float localDelayed = tDelayed - motionStartTime;

        if (!motionFunction.AllowNegativeTime && localDelayed < 0f)
            localDelayed = 0f;

        localDelayed = Mathf.Clamp(localDelayed, 0f, localT);

        Vector3 relativeVisible = motionFunction.EvaluatePosition(localDelayed);
        Vector3 visibleWorldPos = motionCenter + relativeVisible;

        visualTransform.position = visibleWorldPos;
        visualTransform.rotation = motionFunction.EvaluateRotation(localDelayed);
    }

    void OnDestroy()
    {
        if (ghostInstance != null)
        {
            // Ghosts are parented under DebugManager; destroy them when the host is destroyed to avoid using destroyed transforms
            if (ghostInstance.gameObject != null)
                Destroy(ghostInstance.gameObject);
            ghostInstance = null;
        }
    }

    // Visualize real → visible displacement
    void OnDrawGizmosSelected()
    {
        if (realTransform == null || visualTransform == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(realTransform.position, visualTransform.position);
        Gizmos.DrawSphere(visualTransform.position, 0.05f);
    }
}
