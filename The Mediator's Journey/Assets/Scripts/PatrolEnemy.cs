using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FlyingEnemy : MonoBehaviour, IEnemy
{
    private static readonly int IsHit = Animator.StringToHash("isHit");
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

    [Header("Local Pathfinding (candidate sampling)")]
    [Tooltip("Maximum radius around the goal to sample alternative reachable points.")]
    public float candidateRadius = 2.0f;

    [Tooltip("Number of radial rings to sample (1 = only radius step, higher = wider search).")]
    public int candidateRings = 3;

    [Tooltip("Number of samples per ring (angular steps).")]
    public int candidatePerRing = 8;

    [Tooltip("Extra upward bias when choosing a candidate goal to escape under platforms.")]
    public float candidateUpBias = 0.5f;

    [Header("Avoidance (fallback steering)")]
    public float avoidRayDistance = 1.0f;

    public int avoidRayCount = 5;
    public float avoidRayAngle = 60f; // total spread in degrees
    public float avoidStrength = 2.0f;
    public bool ascendWhenBlocked = true;
    public float ascendSpeedBias = 0.6f;

    [Header("Visuals")] public float walkThreshold = 0.1f; // speed above which we consider the enemy walking

    [Header("Attack system")]
    [SerializeField] private float maxHealth = 15f;
    private float currentHealth;

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
    
    private EnemyFireballAttack fireballAttack;
    
    private void Start()
    {
        currentHealth = maxHealth;
        fireballAttack = GetComponent<EnemyFireballAttack>();
    }

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
            // Try to attack
            if (fireballAttack.TryAttack())
            {
                animator.SetTrigger("isAttacking");
            }
                
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

            // pick a reachable goal using local pathfinder
            Vector2 goal = GetReachableGoal(rawGoal);

            Vector2 toGoal = goal - rb.position;
            if (toGoal.magnitude <= pointThreshold)
            {
                // reached last known pos — if player not currently seen, stop chasing
                if (!playerInSight)
                {
                    desired = Vector2.zero;
                }
                else
                {
                    desired = toGoal.normalized * (moveSpeed * chaseSpeedMultiplier);
                }
            }
            else
            {
                desired = toGoal.normalized * (moveSpeed * chaseSpeedMultiplier);
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
                // try to get reachable patrol goal too
                Vector2 reachablePatrolGoal = GetReachableGoal(goal);
                Vector2 toGoal = reachablePatrolGoal - rb.position;
                if (toGoal.magnitude <= pointThreshold)
                    currentPointIndex = 1 - currentPointIndex; // toggle 0 <-> 1
                desired = toGoal.normalized * moveSpeed;
            }
        }
        else
        {
            desired = Vector2.zero; // hover
        }

        // Apply local avoidance fallback before smoothing
        if (desired != Vector2.zero)
        {
            desired = AvoidObstacles(desired);
        }

        // Smooth velocity and apply
        velocity = Vector2.MoveTowards(velocity, desired, accel * Time.fixedDeltaTime);
        rb.velocity = velocity;

        // --- Visual updates: flip sprite and set isWalking ---
        if (sr != null)
        {
            float vx = rb.velocity.x;
            if (Mathf.Abs(vx) > 0.001f)
                sr.flipX = vx > 0f; // face right when flipX = false
        }

        if (animator != null)
        {
            bool isWalking = Mathf.Abs(rb.velocity.x) > walkThreshold || Mathf.Abs(rb.velocity.y) > walkThreshold;
            animator.SetBool("isWalking", isWalking);
        }
    }
    
    #region attack system

    public bool TakeDamage(float amount)
    {
        animator.SetTrigger(IsHit);
        currentHealth -= amount;
        Debug.Log($"Patrol enemy took damage: {amount}, current health: {currentHealth}");
        if (currentHealth <= 0f)
        {
            Die();
            return true;
        }

        return false;
    }
    
    private void Die()
    {
        // Handle enemy death (e.g., play animation, drop loot, etc.)
        // TODO show death animation
        Destroy(gameObject);
    }

    #endregion attack system

    #region navigation
    // Local pathfinder: sample candidate goals around rawGoal and pick first reachable candidate.
    // If direct line to rawGoal is free, returns rawGoal.
    Vector2 GetReachableGoal(Vector2 rawGoal)
    {
        Vector2 from = rb.position;
        Vector2 dirToRaw = rawGoal - from;
        float distToRaw = dirToRaw.magnitude;
        if (distToRaw <= 0.01f) return rawGoal;

        // if direct path is free, return it
        RaycastHit2D directHit = Physics2D.Raycast(from, dirToRaw.normalized, distToRaw, obstacleMask);
        if (directHit.collider == null) return rawGoal;

        // sample rings of candidates around the rawGoal
        for (int ring = 1; ring <= candidateRings; ring++)
        {
            float radius = candidateRadius * (ring / (float)candidateRings);
            for (int i = 0; i < candidatePerRing; i++)
            {
                float angle = (360f / candidatePerRing) * i;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

                // apply slight upward bias to prefer escaping downwards/ upwards as configured
                offset += Vector2.up * candidateUpBias * (ring / (float)candidateRings);

                Vector2 candidate = rawGoal + offset;
                Vector2 toCand = candidate - from;
                float candDist = toCand.magnitude;
                if (candDist <= 0.01f) continue;

                // test line of sight to candidate
                RaycastHit2D hit = Physics2D.Raycast(from, toCand.normalized, candDist, obstacleMask);
                if (hit.collider == null)
                {
                    // found a reachable candidate
                    return candidate;
                }
            }
        }

        // no reachable candidate found; fallback to a small upward-adjusted rawGoal so enemy can try to climb out
        return rawGoal + Vector2.up * candidateUpBias;
    }

    // Simple multi-ray avoidance for flyers (fallback smoothing)
    Vector2 AvoidObstacles(Vector2 desired)
    {
        if (desired == Vector2.zero) return desired;

        Vector2 pos = transform.position;
        Vector2 forward = desired.normalized;
        float halfSpread = avoidRayAngle * 0.5f;
        Vector2 avoid = Vector2.zero;
        bool anyHit = false;

        for (int i = 0; i < Mathf.Max(1, avoidRayCount); i++)
        {
            float t = (avoidRayCount == 1) ? 0.5f : (float)i / (avoidRayCount - 1);
            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            float rad = angle * Mathf.Deg2Rad;

            // rotate forward by angle
            Vector2 dir = new Vector2(
                forward.x * Mathf.Cos(rad) - forward.y * Mathf.Sin(rad),
                forward.x * Mathf.Sin(rad) + forward.y * Mathf.Cos(rad)
            ).normalized;

            RaycastHit2D hit = Physics2D.Raycast(pos, dir, avoidRayDistance, obstacleMask);

#if UNITY_EDITOR
            Debug.DrawRay(pos, dir * avoidRayDistance, hit.collider ? Color.red : Color.green);
#endif

            if (hit.collider != null)
            {
                anyHit = true;
                avoid += hit.normal;
            }
        }

        if (!anyHit) return desired;

        // average normals and blend
        avoid /= Mathf.Max(1, avoidRayCount);
        Vector2 blended = (forward + avoid.normalized * avoidStrength).normalized;

        // optional upward bias so flyers try to ascend out of tight horizontal gaps
        if (ascendWhenBlocked)
            blended.y += ascendSpeedBias;

        // preserve desired speed
        return blended.normalized * desired.magnitude;
    }
    #endregion navigation

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