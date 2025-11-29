using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;       // Horizontal speed
    public float acceleration = 25f;    // How fast you accelerate to target
    public float deceleration = 20f;    // How fast you slow down

    [Header("Gravity")]
    public float gravity = -42f;        // Strong downward pull

    [Header("Relativistic Limits")]
    public bool enforceSpeedLimit = true; // Enforce max speed = C

    private CharacterController cc;
    private Transform cam;
    private Vector3 velocity;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        cam = Camera.main.transform;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float c = (TimeDilationSystem.Instance != null)
            ? TimeDilationSystem.Instance.C
            : 100f;

        // --- GROUND CHECK ---
        bool grounded = IsGrounded();

        // --- HORIZONTAL MOVEMENT ---
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 forward = cam.forward;
        Vector3 right = cam.right;

        forward.y = 0f; forward.Normalize();
        right.y = 0f; right.Normalize();

        Vector3 targetVelocity = (forward * input.y + right * input.x).normalized * moveSpeed;

        // Smooth accel/decel
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);

        bool hasInput = targetVelocity.magnitude > 0.1f;
        Vector3 newHorizontalVel = hasInput
            ? Vector3.MoveTowards(horizontalVel, targetVelocity, acceleration * dt)
            : Vector3.MoveTowards(horizontalVel, Vector3.zero, deceleration * dt);

        velocity.x = newHorizontalVel.x;
        velocity.z = newHorizontalVel.z;

        // --- GRAVITY ---
        if (grounded && velocity.y < 0f && Mathf.Abs(velocity.y) < 0.05f)
            velocity.y = 0f; 
        else
            velocity.y += gravity * dt;

        // --- SPEED LIMIT ---
        if (enforceSpeedLimit)
        {
            float speed = velocity.magnitude;
            if (speed > c)
                velocity = velocity.normalized * (c * 0.999f);
        }

        // --- MOVE PLAYER ---
        cc.Move(velocity * dt);
    }

    // Ground detection
    bool IsGrounded()
    {
        float radius = cc.radius * 0.9f;
        float offset = 0.05f;
        Vector3 pos = transform.position + Vector3.down * (cc.height / 2f - radius + offset);
        return Physics.CheckSphere(pos, radius, ~0, QueryTriggerInteraction.Ignore);
    }

    // Debug gizmo
    void OnDrawGizmosSelected()
    {
        if (!cc) cc = GetComponent<CharacterController>();
        Gizmos.color = Color.yellow;
        float radius = cc.radius * 0.9f;
        float offset = 0.05f;
        Vector3 pos = transform.position + Vector3.down * (cc.height / 2f - radius + offset);
        Gizmos.DrawWireSphere(pos, radius);
    }
}
