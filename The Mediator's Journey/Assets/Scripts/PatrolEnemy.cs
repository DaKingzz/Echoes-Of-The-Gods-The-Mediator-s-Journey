using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlyingEnemy : MonoBehaviour
{
    [Header("Movement")] public float moveSpeed = 3f;
    public float accel = 10f;
    public bool patrol = true;
    public Transform pointA; // assign in Inspector
    public Transform pointB; // assign in Inspector
    public float pointThreshold = 0.1f;

    [Header("Chase")] public Transform target;
    public float chaseRange = 5f;

    [Header("Visuals")] public float walkThreshold = 0.1f; // speed above which we consider the enemy walking

    Rigidbody2D rb;
    Vector2 velocity;
    int currentPointIndex = 0;

    // visual components
    SpriteRenderer sr;
    Animator animator;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        sr = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        Vector2 desired = Vector2.zero;

        if (target != null && Vector2.Distance(transform.position, target.position) <= chaseRange)
        {
            desired = ((Vector2)target.position - rb.position).normalized * moveSpeed;
        }
        else if (patrol && (pointA != null || pointB != null))
        {
            // pick active goal transform
            Transform[] pts = new Transform[] { pointA, pointB };
            // if either is null, hover at current position
            if (pts[0] == null || pts[1] == null)
            {
                desired = Vector2.zero;
            }
            else
            {
                Vector2 goal = pts[currentPointIndex].position;
                Vector2 toGoal = goal - rb.position;
                if (toGoal.magnitude <= pointThreshold)
                    currentPointIndex = 1 - currentPointIndex; // toggle 0 <-> 1
                desired = toGoal.normalized * moveSpeed;
            }
        }
        else
        {
            desired = Vector2.zero; // hover
        }

        velocity = Vector2.MoveTowards(velocity, desired, accel * Time.fixedDeltaTime);
        rb.velocity = velocity;

        // --- Visual updates: flip sprite and set isWalking ---
        // Flip horizontally based on horizontal velocity (only if sr exists)
        if (sr != null)
        {
            if (Mathf.Abs(rb.velocity.x) < 0.001f)
            {
                // facing right when flipX = false, facing left when flipX = true
                sr.flipX = rb.velocity.x > 0f;
            }
        }

        // Set Animator parameter "isWalking" if Animator exists
        if (animator != null)
        {
            bool isWalking = Mathf.Abs(rb.velocity.x) > walkThreshold || Mathf.Abs(rb.velocity.y) > walkThreshold;
            animator.SetBool("isWalking", isWalking);
        }
    }

    // Draw simple gizmos for the two patrol points and connecting line
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (pointA != null) Gizmos.DrawWireSphere(pointA.position, 0.5f);
        if (pointB != null) Gizmos.DrawWireSphere(pointB.position, 0.5f);
        if (pointA != null && pointB != null) Gizmos.DrawLine(pointA.position, pointB.position);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
    }
}