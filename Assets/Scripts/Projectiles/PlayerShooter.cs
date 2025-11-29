using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [Header("Projectile")]
    public GameObject projectilePrefab; // prefab must include ProjectileComponent
    public float fireRate = 6f; // shots per second
    public float muzzleOffset = 0.6f;

    [Header("Default projectile settings")]
    public MotionFunctionSO defaultMotionFunction; // can be LinearMotionSO asset
    public float defaultSpeed = 25f;
    public float defaultRadius = 0.1f;

    private float fireCooldown = 0f;
    private Transform cam;

    void Start()
    {
        cam = Camera.main.transform;
    }

    void Update()
    {
        fireCooldown -= Time.deltaTime;
        if (Input.GetButton("Fire1") && fireCooldown <= 0f)
        {
            Fire();
            fireCooldown = 1f / fireRate;
        }
    }

    void Fire()
    {
        if (projectilePrefab == null) return;

        Vector3 spawnPos = transform.position + cam.forward * muzzleOffset;
        Quaternion spawnRot = Quaternion.LookRotation(cam.forward, Vector3.up);

        GameObject go = Instantiate(projectilePrefab, spawnPos, spawnRot);
        ProjectileComponent proj = go.GetComponent<ProjectileComponent>();
        if (proj == null)
        {
            Debug.LogError("Projectile prefab missing ProjectileComponent.", go);
            return;
        }

        // Initialize projectile
        proj.motionFunction = defaultMotionFunction;
        proj.instanceSpeed = defaultSpeed;
        proj.instanceDirection = cam.forward.normalized;
        proj.radius = defaultRadius;
        proj.realTransform.position = spawnPos;
        proj.visualTransform.position = spawnPos;
    }
}
