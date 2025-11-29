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

    // internal
    private float spawnTime;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation; // used to orient motionFunction output
    private Vector3 prevRealPosition;
    private float lifeTimer;

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

            // SphereCast to detect environment or enemies
            RaycastHit hit;
            float castLen = moveDist + Mathf.Max(0.01f, radius);

            // Check enemies first (layer mask or tag)
            bool enemyHit = false;
            if (enemyMask != 0)
            {
                if (Physics.SphereCast(prevRealPosition, radius, dir, out hit, castLen, enemyMask, QueryTriggerInteraction.Collide))
                    enemyHit = true;
            }
            else
            {
                // fallback: any hit — we'll check tag/component
                if (Physics.SphereCast(prevRealPosition, radius, dir, out hit, castLen, environmentMask, QueryTriggerInteraction.Collide))
                    enemyHit = true;
            }

            if (enemyHit && hit.collider != null)
            {
                // Determine if the collider is an enemy by tag or component
                bool isEnemy = false;
                if (enemyMask != 0 && ((1 << hit.collider.gameObject.layer) & enemyMask) != 0)
                    isEnemy = true;

                if (!isEnemy && !string.IsNullOrEmpty(enemyTag) && hit.collider.CompareTag(enemyTag))
                    isEnemy = true;

                // If not recognized as enemy but has MotionFunctionComponent, treat as target by default
                if (!isEnemy && hit.collider.GetComponent<MotionFunctionComponent>() != null)
                    isEnemy = true;

                if (isEnemy)
                {
                    // Destroy target (simple behavior). Replace with health/damage if you have an Enemy component.
                    Destroy(hit.collider.gameObject);
                    DestroyProjectile();
                    return;
                }
            }

            // Environment hit (non-enemy)
            RaycastHit envHit;
            if (Physics.SphereCast(prevRealPosition, radius, dir, out envHit, castLen, environmentMask, QueryTriggerInteraction.Ignore))
            {
                // If this collided with something that is NOT an enemy, treat as environment hit
                bool hitEnemyViaEnv = (!string.IsNullOrEmpty(enemyTag) && envHit.collider.CompareTag(enemyTag)) ||
                                      (enemyMask != 0 && ((1 << envHit.collider.gameObject.layer) & enemyMask) != 0) ||
                                      (envHit.collider.GetComponent<MotionFunctionComponent>() != null);

                if (!hitEnemyViaEnv)
                {
                    // Hit environment: destroy projectile (could spawn effects here)
                    DestroyProjectile();
                    return;
                }
                // else it was an enemy but was handled above
            }
        }

        // commit real position (authoritative)
        prevRealPosition = realWorldPos;
        realTransform.position = realWorldPos;

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
        Destroy(gameObject);
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
