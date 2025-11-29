using UnityEngine;

[RequireComponent(typeof(Transform))]
public class ProjectileComponent : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Optional motion function (unit-space). If null, linear motion is used.")]
    public MotionFunctionSO motionFunction; // can be LinearMotionSO or other
    [Tooltip("If using a motionFunction, we rotate its local-space output by spawnRotation and scale by instanceSpeed.")]
    public float instanceSpeed = 20f; // units/sec scale applied to motionFunction output
    public Vector3 instanceDirection = Vector3.forward; // local direction when spawned (normalized)
    public bool enforceSpeedLimit = true;

    [Header("Collision")]
    public float radius = 0.1f; // used for conservative spherecast
    public LayerMask environmentMask = ~0; // layers considered environment (default everything)
    public LayerMask enemyMask = 0; // set to layer(s) your enemies use OR use tagging
    public string enemyTag = "Enemy"; // fallback: check tag

    [Header("Lifetime & Play Area")]
    public float maxLifetime = 8f; // seconds
    public bool usePlayArea = true;

    [Header("Transforms")]
    public Transform realTransform;   // authoritative position (collision)
    public Transform visualTransform; // what the player sees (delayed)
    [Header("Debug (optional)")]
    public GameObject ghostPrefab; // optional prefab that will be instantiated to show the real position

    private RealPositionGhost ghostInstance;

    // internal
    private float spawnTime;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation; // used to orient motionFunction output
    private Vector3 prevRealPosition;
    private float lifeTimer;
    
    [Header("Visual / Collision Options")]
    [Tooltip("If true, the projectile's visualTransform will show the authoritative (real) position instead of the light-delayed visible position.")]
    public bool bypassTimeDistortionForVisual = false;

    [Tooltip("If true, the projectile will attempt to detect hits using the enemy's visual transform (what the player sees) instead of the enemy's real position.")]
    public bool detectHitsAgainstVisuals = false;

    void Start()
    {
        if (realTransform == null) realTransform = this.transform;
        if (visualTransform == null) visualTransform = this.transform;

        spawnTime = Time.time;
        spawnPosition = realTransform.position;
        spawnRotation = realTransform.rotation;

        // normalize direction
        if (instanceDirection.sqrMagnitude < 0.001f) instanceDirection = Vector3.forward;
        instanceDirection = instanceDirection.normalized;

        prevRealPosition = spawnPosition;
        lifeTimer = 0f;

        // Optionally create a debug ghost that mirrors the authoritative (real) transform
        if (ghostPrefab != null)
        {
            GameObject debugManager = GameObject.Find("DebugManager");
            if (debugManager == null)
            {
                debugManager = new GameObject("DebugManager");
                debugManager.AddComponent<GhostToggleSystem>();
            }

            Transform ghostsParent = debugManager.transform.Find("Ghosts");
            if (ghostsParent == null)
            {
                GameObject gp = new GameObject("Ghosts");
                gp.transform.SetParent(debugManager.transform, false);
                ghostsParent = gp.transform;
            }

            GameObject g = Instantiate(ghostPrefab, realTransform.position, realTransform.rotation, ghostsParent);
            ghostInstance = g.GetComponent<RealPositionGhost>();
            if (ghostInstance == null)
                ghostInstance = g.AddComponent<RealPositionGhost>();

            ghostInstance.targetRealTransform = realTransform;
            ghostInstance.followRotation = true;
            g.name = gameObject.name + "_Ghost";
            g.SetActive(GhostToggleSystem.GhostsEnabled);
        }
    }

    void Update()
    {
        float tWorld = Time.time;
        float dt = Time.deltaTime;
        float c = (TimeDilationSystem.Instance != null) ? TimeDilationSystem.Instance.C : 100f;

        // --- Real position calculation (using spawn time)
        float localT = tWorld - spawnTime; // t measured since spawn
        Vector3 relativeReal;

        if (motionFunction != null)
        {
            // motionFunction returns positions in its local unit-space. We scale by instanceSpeed and rotate by spawnRotation.
            Vector3 func = motionFunction.EvaluatePosition(localT);
            // Treat func as "unit" path: rotate by spawnRotation and scale by instanceSpeed as magnitude scaling.
            Vector3 scaled = func * instanceSpeed;
            relativeReal = spawnRotation * scaled;
        }
        else
        {
            // default linear behavior: position = direction * speed * t
            relativeReal = instanceDirection * (instanceSpeed * localT);
        }

        Vector3 realWorldPos = spawnPosition + relativeReal;

        // --- clamp instantaneous speed to c (finite-difference approx)
        if (enforceSpeedLimit && dt > Mathf.Epsilon)
        {
            float prevLocalT = (tWorld - dt) - spawnTime;
            Vector3 prevRel;
            if (motionFunction != null)
            {
                Vector3 prevFunc = motionFunction.EvaluatePosition(prevLocalT);
                Vector3 prevScaled = prevFunc * instanceSpeed;
                prevRel = spawnRotation * prevScaled;
            }
            else
            {
                prevRel = instanceDirection * (instanceSpeed * prevLocalT);
            }

            Vector3 prevWorld = spawnPosition + prevRel;
            Vector3 intendedDelta = realWorldPos - prevWorld;
            float intendedSpeed = intendedDelta.magnitude / dt;
            if (intendedSpeed > c)
            {
                Vector3 clampedDelta = intendedDelta.normalized * c * dt;
                realWorldPos = prevWorld + clampedDelta;
            }
        }

        // --- Collision check along ray from prevRealPosition to realWorldPos (prevents tunneling)
        Vector3 moveDelta = realWorldPos - prevRealPosition;
        float moveDist = moveDelta.magnitude;
        if (moveDist > Mathf.Epsilon)
        {
            Vector3 dir = moveDelta / moveDist;

            // If configured to detect hits against visuals, do a proximity check against known visual transforms
            if (detectHitsAgainstVisuals)
            {
                // Iterate motion-driven objects (common case for enemies) and test distance to their visual transform.
                MotionFunctionComponent[] movers = FindObjectsOfType<MotionFunctionComponent>();
                foreach (var mover in movers)
                {
                    if (mover == null || mover.visualTransform == null) continue;

                    // compute closest point on projectile segment to the visual position
                    Vector3 visPos = mover.visualTransform.position;
                    Vector3 closest = ClosestPointOnSegment(prevRealPosition, realWorldPos, visPos);

                    // If the visual object has a collider, use it for precise testing
                    Collider visCol = mover.visualTransform.GetComponentInChildren<Collider>();
                    if (visCol != null)
                    {
                        Vector3 closestOnCollider = visCol.ClosestPoint(closest);
                        float dist = Vector3.Distance(closestOnCollider, closest);
                        if (dist <= radius)
                        {
                            // Prefer to apply a letter hit if this mover has an EnemyWord component
                            var enemyWord = mover.GetComponent<EnemyWord>();
                            if (enemyWord != null)
                                enemyWord.ApplyHit();
                            else
                                Destroy(mover.gameObject);

                            DestroyProjectile();
                            return;
                        }
                    }
                    else
                    {
                        // fallback spherical check: assume small visual radius
                        float fallbackRadius = 0.5f;
                        float dist = Vector3.Distance(closest, visPos);
                        if (dist <= radius + fallbackRadius)
                        {
                            var enemyWord = mover.GetComponent<EnemyWord>();
                            if (enemyWord != null)
                                enemyWord.ApplyHit();
                            else
                                Destroy(mover.gameObject);

                            DestroyProjectile();
                            return;
                        }
                    }
                }
            }

            // Default behaviour: spherecast against layers (environment/enemy masks)
            RaycastHit hit;
            float castLen = moveDist + Mathf.Max(0.01f, radius);

            bool enemyHit = false;
            if (enemyMask != 0)
            {
                if (Physics.SphereCast(prevRealPosition, radius, dir, out hit, castLen, enemyMask, QueryTriggerInteraction.Collide))
                    enemyHit = true;
            }
            else
            {
                if (Physics.SphereCast(prevRealPosition, radius, dir, out hit, castLen, environmentMask, QueryTriggerInteraction.Collide))
                    enemyHit = true;
            }

            if (enemyHit && hit.collider != null)
            {
                bool isEnemy = false;
                if (enemyMask != 0 && ((1 << hit.collider.gameObject.layer) & enemyMask) != 0)
                    isEnemy = true;

                if (!isEnemy && !string.IsNullOrEmpty(enemyTag) && hit.collider.CompareTag(enemyTag))
                    isEnemy = true;

                if (!isEnemy && hit.collider.GetComponent<MotionFunctionComponent>() != null)
                    isEnemy = true;

                if (isEnemy)
                {
                    // Prefer destroying the GameObject that owns a MotionFunctionComponent (common case)
                    // If the hit object belongs to an EnemyWord, apply a letter hit first
                    var enemyWord = hit.collider.GetComponentInParent<EnemyWord>();
                    if (enemyWord != null)
                    {
                        enemyWord.ApplyHit();
                    }
                    else
                    {
                        var moverComp = hit.collider.GetComponentInParent<MotionFunctionComponent>();
                        if (moverComp != null)
                        {
                            Destroy(moverComp.gameObject);
                        }
                        else
                        {
                            // If no MotionFunctionComponent, try to find a parent with the enemy tag
                            Transform parentWithTag = null;
                            if (!string.IsNullOrEmpty(enemyTag))
                            {
                                Transform p = hit.collider.transform;
                                while (p != null)
                                {
                                    if (p.CompareTag(enemyTag))
                                    {
                                        parentWithTag = p;
                                        break;
                                    }
                                    p = p.parent;
                                }
                            }

                            if (parentWithTag != null)
                                Destroy(parentWithTag.gameObject);
                            else
                                Destroy(hit.collider.gameObject);
                        }
                    }

                    DestroyProjectile();
                    return;
                }
            }

            RaycastHit envHit;
            if (Physics.SphereCast(prevRealPosition, radius, dir, out envHit, castLen, environmentMask, QueryTriggerInteraction.Ignore))
            {
                bool hitEnemyViaEnv = (!string.IsNullOrEmpty(enemyTag) && envHit.collider.CompareTag(enemyTag)) ||
                                      (enemyMask != 0 && ((1 << envHit.collider.gameObject.layer) & enemyMask) != 0) ||
                                      (envHit.collider.GetComponent<MotionFunctionComponent>() != null);

                if (!hitEnemyViaEnv)
                {
                    DestroyProjectile();
                    return;
                }
            }
        }

        // commit real position (authoritative)
        prevRealPosition = realWorldPos;
        realTransform.position = realWorldPos;

        // Keep projectile ghost active state in sync with global toggle (if present)
        if (ghostInstance != null && ghostInstance.gameObject.activeSelf != GhostToggleSystem.GhostsEnabled)
        {
            ghostInstance.gameObject.SetActive(GhostToggleSystem.GhostsEnabled);
            if (GhostToggleSystem.GhostsEnabled)
            {
                ghostInstance.transform.position = realTransform.position;
                ghostInstance.transform.rotation = realTransform.rotation;
            }
        }

        // --- Visible position (retarded time: t - d/c) shown to player
        Transform playerT = FindObjectOfType<PlayerController>()?.transform;
        if (playerT != null)
        {
            float distanceToPlayer = Vector3.Distance(playerT.position, realWorldPos);
            float tDelayed = tWorld - distanceToPlayer / c;
            if (! (motionFunction != null ? motionFunction.AllowNegativeTime : true) && tDelayed < 0f)
                tDelayed = 0f;
            tDelayed = Mathf.Clamp(tDelayed, 0f, tWorld);

            Vector3 visRel;
            if (motionFunction != null)
            {
                Vector3 func = motionFunction.EvaluatePosition(tDelayed - spawnTime);
                Vector3 scaled = func * instanceSpeed;
                visRel = spawnRotation * scaled;
            }
            else
            {
                float localVisT = tDelayed - spawnTime;
                localVisT = Mathf.Max(0f, localVisT);
                visRel = instanceDirection * (instanceSpeed * localVisT);
            }

            visualTransform.position = spawnPosition + visRel;
            visualTransform.rotation = spawnRotation * (motionFunction != null ? motionFunction.EvaluateRotation(tDelayed - spawnTime) : Quaternion.identity);
        }
        else
        {
            // No player found: just show real
            visualTransform.position = realWorldPos;
            visualTransform.rotation = realTransform.rotation;
        }

        // If configured to bypass time distortion for visuals, show the real (authoritative) position instead
        if (bypassTimeDistortionForVisual)
        {
            visualTransform.position = realWorldPos;
            visualTransform.rotation = realTransform.rotation;
        }

        // Lifetime & play area check
        lifeTimer += dt;
        if (lifeTimer > maxLifetime)
        {
            DestroyProjectile();
            return;
        }

        if (usePlayArea && PlayAreaManager.Instance != null && PlayAreaManager.Instance.IsOutside(realWorldPos))
        {
            DestroyProjectile();
            return;
        }
    }

    void DestroyProjectile()
    {
        // TODO: spawn VFX, SFX here if desired
        // Destroy any debug ghost we created to avoid dangling ghosts
        if (ghostInstance != null)
        {
            if (ghostInstance.gameObject != null)
                Destroy(ghostInstance.gameObject);
            ghostInstance = null;
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Safeguard: ensure ghost is removed if the projectile is destroyed by other means
        if (ghostInstance != null)
        {
            if (ghostInstance.gameObject != null)
                Destroy(ghostInstance.gameObject);
            ghostInstance = null;
        }
    }

    // Utility: closest point on a segment AB to point P
    Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float ab2 = Vector3.Dot(ab, ab);
        if (ab2 == 0f) return a;
        float t = Vector3.Dot(p - a, ab) / ab2;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    void OnDrawGizmosSelected()
    {
        if (realTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(realTransform.position, radius);
        }

        if (visualTransform != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(visualTransform.position, radius * 0.9f);
        }
    }
}
