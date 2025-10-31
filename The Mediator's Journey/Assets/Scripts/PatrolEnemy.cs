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

    [Header("Chase Tuning")] [Tooltip("Extra range added to chaseRange when the enemy 'remembers' the player.")]
    public float chaseRangeBonus = 3f;

    [Tooltip("How long (seconds) the enemy will remember the player's last seen position.")]
    public float memoryTime = 2f;

    [Tooltip("Speed multiplier applied while actively chasing.")]
    public float chaseSpeedMultiplier = 1.5f;

    [Tooltip("Minimum vertical separation (units) the enemy will keep from the player.")]
    public float preferredVerticalSeparation = 1.0f;

    [Header("Vision")] [Tooltip("Layers that block the enemy's vision (e.g., Walls, Obstacles).")]
    public LayerMask obstacleMask;

    [Header("Visuals")] public float walkThreshold = 0.1f; // speed above which we consider the enemy walking

    Rigidbody2D rb;
    Vector2 velocity;
    int currentPointIndex = 0;

    // visual components
    SpriteRenderer sr;
    Animator animator;

    // memory / tracking
    Vector2 lastKnownPlayerPos;
    float lastSeenTime = -999f;
    bool isRemembering = false; // true while within memoryTime

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

        // Detection uses extended range so enemy can initially spot player beyond base range
        float detectionRange = chaseRange + chaseRangeBonus;

        // Check if player is within sight (distance <= detectionRange) and not occluded
        bool playerInSight = false;
        if (target != null)
        {
            float distToPlayer = Vector2.Distance(transform.position, target.position);
            if (distToPlayer <= detectionRange)
            {
                // cast from enemy to player, checking for obstacles
                Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
                RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, distToPlayer, obstacleMask);
                if (hit.collider == null)
                {
                    playerInSight = true;
                    lastKnownPlayerPos = target.position;
                    lastSeenTime = Time.time;
                }
            }
        }

        // Update remembering state
        isRemembering = (Time.time - lastSeenTime) <= memoryTime;

        // If memory expired and player isn't in sight, clear last known pos so behavior reverts
        if (!isRemembering && !playerInSight)
        {
            lastKnownPlayerPos = Vector2.zero;
            lastSeenTime = -999f;
        }

        // Decide behavior: chase (if seen or recently seen) or patrol/hover
        if (playerInSight || isRemembering)
        {
            // chase using last known position
            Vector2 rawGoal = lastKnownPlayerPos;

            // enforce vertical separation: keep at least preferredVerticalSeparation units from player Y
            float dy = rawGoal.y - transform.position.y;
            if (Mathf.Abs(dy) < preferredVerticalSeparation)
            {
                float direction = Mathf.Sign(dy);
                if (direction == 0f) direction = (transform.position.y <= rawGoal.y) ? -1f : 1f;
                rawGoal.y = rawGoal.y + direction * preferredVerticalSeparation;
            }

            Vector2 toGoal = rawGoal - rb.position;
            if (toGoal.magnitude <= pointThreshold)
            {
                // reached last known pos — if player not currently seen, stop chasing
                if (!playerInSight)
                {
                    desired = Vector2.zero;
                }
                else
                {
                    desired = toGoal.normalized * moveSpeed * chaseSpeedMultiplier;
                }
            }
            else
            {
                desired = toGoal.normalized * moveSpeed * chaseSpeedMultiplier;
            }
        }
        else if (patrol && (pointA != null || pointB != null))
        {
            // two-point patrol as before
            Transform[] pts = new Transform[] { pointA, pointB };
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

        // Smooth velocity and apply
        velocity = Vector2.MoveTowards(velocity, desired, accel * Time.fixedDeltaTime);
        rb.velocity = velocity;

        // --- Visual updates: flip sprite and set isWalking ---
        if (sr != null)
        {
            float vx = rb.velocity.x;
            if (Mathf.Abs(vx) > 0.001f)
                sr.flipX = vx < 0f; // face right when flipX = false
        }

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

        // Draw effective chase range only while remembering or during detection in play mode
        Gizmos.color = Color.red;
        float effectiveRangeToDraw = chaseRange;
        // if in play mode use runtime remembering flag; otherwise show extended range for clarity
#if UNITY_EDITOR
        if (Application.isPlaying)
            effectiveRangeToDraw = chaseRange + (isRemembering ? chaseRangeBonus : 0f);
        else
            effectiveRangeToDraw = chaseRange + chaseRangeBonus;
#else
        effectiveRangeToDraw = chaseRange + (isRemembering ? chaseRangeBonus : 0f);
#endif
        Gizmos.DrawWireSphere(transform.position, effectiveRangeToDraw);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
    }
}