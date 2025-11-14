using System;
using UnityEngine;

/// <summary>
/// Refactored enemy controller usable for both flying and grounded enemies.
/// - Clear names, tooltips, headers, regions
/// - Single responsibility sections and small helper methods
/// - Time.fixedTime for physics/perception logic
/// - Safe null checks, cached animator hashes
/// - AvoidObstacles fixes (guard zero normals, average only hits)
/// - Optional sprite default-facing setting using SpriteRenderer.flipX
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PatrolEnemy : MonoBehaviour, IEnemy
{
    #region Movement

    [Header("Movement")] [Tooltip("Base movement speed in world units/second.")] [SerializeField]
    private float movementSpeed = 3f;

    [Tooltip("Acceleration used to smooth velocity changes (world units/second^2).")] [SerializeField]
    private float acceleration = 10f;

    [Tooltip("Enable two-point patrol between PointA and PointB.")] [SerializeField]
    private bool enablePatrol = true;

    [Tooltip("First patrol point (assign in Inspector).")] [SerializeField]
    private Transform patrolPointA;

    [Tooltip("Second patrol point (assign in Inspector).")] [SerializeField]
    private Transform patrolPointB;

    [Tooltip("Distance threshold to consider a patrol point reached.")] [SerializeField]
    private float patrolPointThreshold = 0.1f;

    #endregion

    #region Perception and Chase

    [Header("Perception")] [Tooltip("Transform representing the target (typically the player).")] [SerializeField]
    private Transform target;

    [Tooltip("Base chase radius at which the enemy decides to engage.")] [SerializeField]
    private float chaseRadius = 5f;

    [Tooltip("Extra range used only for initial sighting checks (enemy can spot player slightly earlier).")]
    [SerializeField]
    private float extendedVisionBonus = 3f;

    [Tooltip("How long (seconds) the enemy remembers the player's last seen position.")] [SerializeField]
    private float memoryDuration = 2f;

    [Tooltip("Multiplier applied to movementSpeed while actively chasing.")] [SerializeField]
    private float chaseSpeedMultiplier = 1.5f;

    [Tooltip("Preferred minimum vertical separation from the player's Y position.")] [SerializeField]
    private float preferredVerticalSeparation = 1.0f;

    [Tooltip("LayerMask of obstacles that block vision and raycasts.")] [SerializeField]
    private LayerMask obstacleMask;

    #endregion

    #region Local Pathfinding (candidate sampling)

    [Header("Local Pathfinding")]
    [Tooltip("Maximum radius around the goal to sample alternative reachable points.")]
    [SerializeField]
    private float candidateRadius = 2.0f;

    [Tooltip("Number of radial rings to sample (1 = only radius step, higher = wider search).")] [SerializeField]
    private int candidateRings = 3;

    [Tooltip("Number of samples per ring (angular steps).")] [SerializeField]
    private int candidatePerRing = 8;

    [Tooltip("Extra upward bias when choosing a candidate goal to escape under platforms.")] [SerializeField]
    private float candidateUpBias = 0.5f;

    #endregion

    #region Avoidance and Steering

    [Header("Avoidance and Steering")] [Tooltip("Distance for avoidance rays.")] [SerializeField]
    private float avoidanceRayDistance = 1.0f;

    [Tooltip("Number of rays used for avoidance.")] [SerializeField]
    private int avoidanceRayCount = 5;

    [Tooltip("Total spread angle (degrees) used when sampling avoidance rays.")] [SerializeField]
    private float avoidanceRaySpreadDegrees = 60f;

    [Tooltip("Strength used to blend avoidance normal into forward direction.")] [SerializeField]
    private float avoidanceStrength = 2.0f;

    [Tooltip("Allow ascend bias when blocked (useful for flying enemies).")] [SerializeField]
    private bool ascendWhenBlocked = true;

    [Tooltip("Upward bias added when blocked (only used for flying enemies).")] [SerializeField]
    private float ascendSpeedBias = 0.6f;

    #endregion

    #region Combat & Visuals

    [Header("Combat and Visuals")] [Tooltip("Health for this enemy.")] [SerializeField]
    private float maximumHealth = 15f;

    [Tooltip("Speed threshold above which the enemy is considered walking (used by animator).")] [SerializeField]
    private float walkingThreshold = 0.1f;

    [Tooltip("If the sprite art faces left by default, enable this to correct flip behavior.")] [SerializeField]
    private bool spriteFacesLeftByDefault = true;

    #endregion

    #region Runtime fields (cached components, state)

    private Rigidbody2D rigidbody2D;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private EnemyFireballAttack fireballAttack;

    private Vector2 smoothedVelocity;
    private int currentPatrolIndex;

    // perception memory
    private Vector2 lastKnownTargetPosition = Vector2.zero;
    private float lastSeenTimestamp = -Mathf.Infinity;
    private bool isWithinMemoryWindow = false;

    private float currentHealth;

    // cached animator hashes
    private static readonly int animatorHashIsHit = Animator.StringToHash("isHit");
    private static readonly int animatorHashIsAttacking = Animator.StringToHash("isAttacking");
    private static readonly int animatorHashIsWalking = Animator.StringToHash("isWalking");

    // derived mode: flying or grounded
    private bool isFlyingMode;

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        fireballAttack = GetComponent<EnemyFireballAttack>();

        // Infer flying mode from gravityScale (zero => flying)
        isFlyingMode = Mathf.Approximately(rigidbody2D.gravityScale, 0f);

        // Ensure rotation is frozen (enemies should not rotate by physics)
        rigidbody2D.constraints |= RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        currentHealth = maximumHealth;
        // sanitize candidate sampling params
        candidateRings = Mathf.Max(1, candidateRings);
        candidatePerRing = Mathf.Max(1, candidatePerRing);
        avoidanceRayCount = Mathf.Max(1, avoidanceRayCount);
    }

    private void FixedUpdate()
    {
        UpdatePerception();

        Vector2 desired = DecideBehaviorDesiredVelocity();

        desired = ApplyAvoidanceIfNeeded(desired);

        SmoothAndApplyVelocity(desired);

        UpdateVisualsAndAnimator();
    }

    #endregion

    #region Perception

    /// <summary>
    /// Update vision detection and memory state. Uses Time.fixedTime because detection runs in FixedUpdate.
    /// </summary>
    private void UpdatePerception()
    {
        bool targetInSight = false;

        if (target != null)
        {
            float visionRange = chaseRadius + extendedVisionBonus;
            float sqrDistanceToTarget = ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude;

            if (sqrDistanceToTarget <= visionRange * visionRange)
            {
                Vector2 directionToTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
                float distanceToTarget = Mathf.Sqrt(sqrDistanceToTarget);

                // Raycast from the enemy position toward the target. Query uses obstacleMask.
                // We assume the player's collider is excluded by layer mask configuration (per your note).
                RaycastHit2D hit = Physics2D.Raycast(rigidbody2D.position, directionToTarget, distanceToTarget,
                    obstacleMask);
                if (hit.collider == null)
                {
                    targetInSight = true;
                    lastKnownTargetPosition = target.position;
                    lastSeenTimestamp = Time.fixedTime;
                }
            }
        }

        isWithinMemoryWindow = (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;

        if (!isWithinMemoryWindow && !targetInSight)
        {
            // full forget behavior
            lastKnownTargetPosition = Vector2.zero;
            lastSeenTimestamp = -Mathf.Infinity;
        }
    }

    #endregion

    #region Behavior decision

    /// <summary>
    /// Returns the desired velocity vector (not yet smoothed) for the current frame.
    /// </summary>
    private Vector2 DecideBehaviorDesiredVelocity()
    {
        Vector2 desired = Vector2.zero;

        bool shouldChase = (lastKnownTargetPosition != Vector2.zero) && (isWithinMemoryWindow ||
                                                                         (target != null &&
                                                                          (target.position - transform.position)
                                                                          .sqrMagnitude <=
                                                                          (chaseRadius * chaseRadius)));

        if (shouldChase)
        {
            // Try attack (guarded)
            if (fireballAttack != null && fireballAttack.TryAttack())
            {
                if (animator != null) animator.SetTrigger(animatorHashIsAttacking);
            }

            Vector2 rawGoal = lastKnownTargetPosition;

            // Enforce vertical separation preference
            float dy = rawGoal.y - transform.position.y;
            if (Mathf.Abs(dy) < preferredVerticalSeparation)
            {
                float direction = Mathf.Sign(dy);
                if (direction == 0f) direction = (transform.position.y <= rawGoal.y) ? -1f : 1f;
                rawGoal.y = rawGoal.y + direction * preferredVerticalSeparation;
            }

            Vector2 goal = GetReachableGoal(rawGoal);

            Vector2 toGoal = goal - rigidbody2D.position;
            float distSqr = toGoal.sqrMagnitude;

            if (distSqr <= patrolPointThreshold * patrolPointThreshold)
            {
                // Reached last known pos — if target not currently seen, stop chasing
                if (!isWithinMemoryWindow && (target == null ||
                                              (target.position - transform.position).sqrMagnitude >
                                              chaseRadius * chaseRadius))
                {
                    desired = Vector2.zero;
                }
                else
                {
                    desired = toGoal.normalized * (movementSpeed * chaseSpeedMultiplier);
                }
            }
            else
            {
                desired = toGoal.normalized * (movementSpeed * chaseSpeedMultiplier);
            }
        }
        else if (enablePatrol && (patrolPointA != null && patrolPointB != null))
        {
            // Two-point patrol without allocations
            Transform[] pts = new Transform[2] { patrolPointA, patrolPointB };
            Vector2 goal = pts[currentPatrolIndex].position;
            Vector2 reachable = GetReachableGoal(goal);
            Vector2 toGoal = reachable - rigidbody2D.position;
            if (toGoal.sqrMagnitude <= patrolPointThreshold * patrolPointThreshold)
            {
                currentPatrolIndex = 1 - currentPatrolIndex;
            }

            desired = toGoal.normalized * movementSpeed;
        }
        else
        {
            desired = Vector2.zero; // hover or idle
        }

        return desired;
    }

    #endregion

    #region Local Pathfinding

    /// <summary>
    /// Returns a reachable goal near rawGoal. If direct line is free returns rawGoal.
    /// Otherwise samples candidate points around rawGoal and returns first reachable candidate.
    /// Falls back to small upward bias if nothing found.
    /// </summary>
    private Vector2 GetReachableGoal(Vector2 rawGoal)
    {
        Vector2 from = rigidbody2D.position;
        Vector2 dirToRaw = rawGoal - from;
        float distToRaw = dirToRaw.magnitude;
        if (distToRaw <= 0.01f) return rawGoal;

        // Direct path free?
        RaycastHit2D directHit = Physics2D.Raycast(from, dirToRaw.normalized, distToRaw, obstacleMask);
        if (directHit.collider == null) return rawGoal;

        // Sample rings outward (radius increases with ring index).
        for (int ring = 1; ring <= candidateRings; ring++)
        {
            float radius = candidateRadius * (ring / (float)candidateRings); // smaller radius first
            for (int i = 0; i < candidatePerRing; i++)
            {
                float angleDegrees = (360f / candidatePerRing) * i;
                float angleRad = angleDegrees * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;

                // upward bias scaled by ring
                offset += Vector2.up * candidateUpBias * (ring / (float)candidateRings);

                Vector2 candidate = rawGoal + offset;
                Vector2 toCandidate = candidate - from;
                float candDist = toCandidate.magnitude;
                if (candDist <= 0.01f) continue;

                RaycastHit2D hit = Physics2D.Raycast(from, toCandidate.normalized, candDist, obstacleMask);
                if (hit.collider == null)
                {
                    return candidate;
                }
            }
        }

        // No candidate found: fallback upward nudge (helps flyers escape)
        return rawGoal + Vector2.up * candidateUpBias;
    }

    #endregion

    #region Avoidance

    /// <summary>
    /// Blend fallback avoidance into the desired direction if obstacles detected.
    /// Fixed issues:
    /// - average normals only over hits
    /// - guard against zero normals before normalizing
    /// - respect grounded enemies by limiting upward bias
    /// </summary>
    private Vector2 ApplyAvoidanceIfNeeded(Vector2 desired)
    {
        if (desired == Vector2.zero) return desired;

        Vector2 position = rigidbody2D.position;
        Vector2 forward = desired.normalized;

        float halfSpread = avoidanceRaySpreadDegrees * 0.5f;
        Vector2 accumulatedNormal = Vector2.zero;
        int hitCount = 0;

        int rayCount = Mathf.Max(1, avoidanceRayCount);

        for (int i = 0; i < rayCount; i++)
        {
            float t = (rayCount == 1) ? 0.5f : (float)i / (rayCount - 1);
            float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
            float rad = angle * Mathf.Deg2Rad;

            // rotate forward vector by angle
            Vector2 dir = new Vector2(
                forward.x * Mathf.Cos(rad) - forward.y * Mathf.Sin(rad),
                forward.x * Mathf.Sin(rad) + forward.y * Mathf.Cos(rad)
            ).normalized;

            RaycastHit2D hit = Physics2D.Raycast(position, dir, avoidanceRayDistance, obstacleMask);

#if UNITY_EDITOR
            Debug.DrawRay(position, dir * avoidanceRayDistance, hit.collider ? Color.red : Color.green);
#endif

            if (hit.collider != null)
            {
                accumulatedNormal += hit.normal;
                hitCount++;
            }
        }

        if (hitCount == 0) return desired;

        Vector2 averageNormal = accumulatedNormal / hitCount;
        if (averageNormal == Vector2.zero) return desired;

        Vector2 blended = (forward + averageNormal.normalized * avoidanceStrength).normalized;

        if (ascendWhenBlocked && isFlyingMode)
        {
            blended.y += ascendSpeedBias;
        }
        else
        {
            // If grounded enemy, reduce upward bias so they don't attempt to climb obstacles
            blended.y = Mathf.Clamp(blended.y, -0.2f, 0.2f);
        }

        return blended.normalized * desired.magnitude;
    }

    #endregion

    #region Velocity smoothing and application

    private void SmoothAndApplyVelocity(Vector2 desired)
    {
        smoothedVelocity = Vector2.MoveTowards(smoothedVelocity, desired, acceleration * Time.fixedDeltaTime);
        rigidbody2D.velocity = smoothedVelocity;
    }

    #endregion

    #region Visuals and animator

    private void UpdateVisualsAndAnimator()
    {
        // Sprite facing: respect sprite default facing (left or right)
        if (spriteRenderer != null)
        {
            float vx = rigidbody2D.velocity.x;
            if (Mathf.Abs(vx) > 0.001f)
            {
                // If sprite art faces left by default, flipX should be false when moving left.
                // desiredFacingRight = vx > 0
                bool desiredFacingRight = vx > 0f;
                spriteRenderer.flipX = spriteFacesLeftByDefault ? desiredFacingRight : !desiredFacingRight;
            }
        }

        if (animator != null)
        {
            bool isWalking = Mathf.Abs(rigidbody2D.velocity.x) > walkingThreshold ||
                             Mathf.Abs(rigidbody2D.velocity.y) > walkingThreshold;
            animator.SetBool(animatorHashIsWalking, isWalking);
        }
    }

    #endregion

    #region Damage & death

    public bool TakeDamage(float amount)
    {
        if (animator != null) animator.SetTrigger(animatorHashIsHit);

        currentHealth -= amount;
        Debug.Log($"PatrolEnemy took damage: {amount}, current health: {currentHealth}");

        if (currentHealth <= 0f)
        {
            Die();
            return true;
        }

        return false;
    }

    private void Die()
    {
        // Hook point: play death VFX / animation before destroy if desired
        Destroy(gameObject);
    }

    #endregion

    #region Gizmos and debug notes

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (patrolPointA != null) Gizmos.DrawWireSphere(patrolPointA.position, 0.25f);
        if (patrolPointB != null) Gizmos.DrawWireSphere(patrolPointB.position, 0.25f);
        if (patrolPointA != null && patrolPointB != null) Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);

        Gizmos.color = Color.red;
        float effectiveVision = chaseRadius + extendedVisionBonus;
        Gizmos.DrawWireSphere(transform.position, effectiveVision);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);
    }
#endif

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw patrol points and connection
        Gizmos.color = Color.cyan;
        if (patrolPointA != null) Gizmos.DrawWireSphere(patrolPointA.position, 0.25f);
        if (patrolPointB != null) Gizmos.DrawWireSphere(patrolPointB.position, 0.25f);
        if (patrolPointA != null && patrolPointB != null) Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);

        // Draw chase ranges: base (yellow) and extended vision (red)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);

        Gizmos.color = Color.red;
        float effectiveVisionRange = chaseRadius + extendedVisionBonus;
        // While playing, indicate whether memory is active by tinting slightly
        if (Application.isPlaying)
        {
            effectiveVisionRange = chaseRadius + (isWithinMemoryWindow ? extendedVisionBonus : 0f);
        }

        Gizmos.DrawWireSphere(transform.position, effectiveVisionRange);

        // Draw last known target position
        Gizmos.color = Color.magenta;
        if (lastKnownTargetPosition != Vector2.zero)
        {
            Gizmos.DrawSphere(lastKnownTargetPosition, 0.075f);
            Gizmos.DrawLine(transform.position, lastKnownTargetPosition);
        }

        // Draw candidate samples around last known target (useful to debug GetReachableGoal)
        if (lastKnownTargetPosition != Vector2.zero)
        {
            Gizmos.color = Color.green;
            int drawn = 0;
            for (int ring = 1; ring <= Mathf.Max(1, candidateRings); ring++)
            {
                float radius = candidateRadius * (ring / (float)Mathf.Max(1, candidateRings));
                for (int i = 0; i < Mathf.Max(1, candidatePerRing); i++)
                {
                    float angleDegrees = (360f / Mathf.Max(1, candidatePerRing)) * i;
                    float angleRad = angleDegrees * Mathf.Deg2Rad;
                    Vector2 offset = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;
                    offset += Vector2.up * candidateUpBias * (ring / (float)Mathf.Max(1, candidateRings));
                    Vector2 candidate = lastKnownTargetPosition + offset;
                    Gizmos.DrawWireSphere(candidate, 0.05f);
                    // draw a thin line from target to candidate
                    Gizmos.DrawLine(lastKnownTargetPosition, candidate);
                    drawn++;
                }
            }
        }

        // Draw avoidance rays preview (spread around forward). This uses current smoothedVelocity as 'forward' direction.
        if (smoothedVelocity != Vector2.zero)
        {
            Gizmos.color = Color.blue;
            Vector2 forward = smoothedVelocity.normalized;
            float halfSpread = avoidanceRaySpreadDegrees * 0.5f;
            int rayCount = Mathf.Max(1, avoidanceRayCount);
            for (int i = 0; i < rayCount; i++)
            {
                float t = (rayCount == 1) ? 0.5f : (float)i / (rayCount - 1);
                float angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(
                    forward.x * Mathf.Cos(rad) - forward.y * Mathf.Sin(rad),
                    forward.x * Mathf.Sin(rad) + forward.y * Mathf.Cos(rad)
                ).normalized;
                // Draw line indicating the ray
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + dir * avoidanceRayDistance);
            }
        }
    }
#endif

    #endregion
}