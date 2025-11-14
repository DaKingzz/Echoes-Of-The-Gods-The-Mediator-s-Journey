using System;
using UnityEngine;

/// <summary>
/// PatrolEnemy
/// Abstract base class providing shared perception, patrol/chase decision logic,
/// local candidate sampling pathfinder, avoidance fallback, velocity smoothing,
/// debug gizmos, and basic health/damage API for enemy subclasses.
/// Subclasses must implement the movement model by overriding ComputeMovementVelocity,
/// and may override ApplyAvoidanceIfNeeded for specialized steering.
/// This variant includes robust sprite flipping logic:
/// - Uses SpriteRenderer.flipX (safer than localScale inversion)
/// - Exposes inspector option DefaultSpriteFacing to indicate whether the art faces Left or Right by default
/// - Remembers last facing direction so idle sprites keep their last orientation
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class PatrolEnemy : MonoBehaviour, IEnemy
{
    #region Movement Shared Configuration

    [Header("Movement")]
    [Tooltip(
        "Base movement speed in world units per second. Subclasses use this to compute their movement velocities.")]
    [SerializeField]
    protected float movementSpeed = 3f;

    [Tooltip("Acceleration used to smooth velocity changes (world units/second^2).")] [SerializeField]
    protected float acceleration = 10f;

    [Header("Patrol")] [Tooltip("Enable two-point patrol between PatrolPointA and PatrolPointB.")] [SerializeField]
    protected bool enablePatrol = true;

    [Tooltip("First patrol point (assign in Inspector).")] [SerializeField]
    protected Transform patrolPointA;

    [Tooltip("Second patrol point (assign in Inspector).")] [SerializeField]
    protected Transform patrolPointB;

    [Tooltip("Distance threshold to consider a patrol point reached.")] [SerializeField]
    protected float patrolPointThreshold = 0.1f;

    #endregion

    #region Perception and Chase Configuration

    [Header("Perception and Chase")]
    [Tooltip("Transform representing the target (usually the player).")]
    [SerializeField]
    protected Transform target;

    [Tooltip("Base chase radius at which the enemy decides to actively pursue.")] [SerializeField]
    protected float chaseRadius = 5f;

    [Tooltip("Extra vision bonus used for initial detection to allow earlier spotting.")] [SerializeField]
    protected float extendedVisionBonus = 3f;

    [Tooltip("How long (seconds) the enemy remembers the player's last seen position.")] [SerializeField]
    protected float memoryDuration = 2f;

    [Tooltip("Multiplier applied to movement speed while actively chasing (subclasses may use this).")] [SerializeField]
    protected float chaseSpeedMultiplier = 1.5f;

    [Tooltip("Preferred minimum vertical separation from the player's Y position.")] [SerializeField]
    protected float preferredVerticalSeparation = 1.0f;

    [Tooltip("Layer mask of obstacles that block vision and path visibility.")] [SerializeField]
    protected LayerMask obstacleMask;

    #endregion

    #region Local Pathfinding Configuration

    [Header("Local Pathfinding")]
    [Tooltip("Maximum radius around the goal to sample alternative reachable points.")]
    [SerializeField]
    protected float candidateRadius = 2.0f;

    [Tooltip("Number of radial rings to sample (1 = only one radius step).")] [SerializeField]
    protected int candidateRings = 3;

    [Tooltip("Number of samples per ring (angular steps).")] [SerializeField]
    protected int candidatePerRing = 8;

    [Tooltip(
        "Upward bias added to candidate positions to help escape under platforms. Subclasses (walkers) can keep this small or ignore vertical bias).")]
    [SerializeField]
    protected float candidateUpBias = 0.5f;

    #endregion

    #region Avoidance Configuration

    [Header("Avoidance")] [Tooltip("Distance for avoidance rays.")] [SerializeField]
    protected float avoidanceRayDistance = 1.0f;

    [Tooltip("Number of rays used for avoidance.")] [SerializeField]
    protected int avoidanceRayCount = 5;

    [Tooltip("Total spread angle in degrees used when sampling avoidance rays.")] [SerializeField]
    protected float avoidanceRaySpreadDegrees = 60f;

    [Tooltip("Strength used to blend avoidance normal into the forward direction.")] [SerializeField]
    protected float avoidanceStrength = 2.0f;

    [Tooltip("If true, the avoidance fallback will apply an upward bias (useful for flying enemies).")] [SerializeField]
    protected bool allowAscendWhenBlocked = true;

    [Tooltip("Upward bias applied by avoidance for flying enemies.")] [SerializeField]
    protected float ascendSpeedBias = 0.6f;

    #endregion

    #region Combat and Health

    [Header("Combat")] [Tooltip("Maximum health for this enemy.")] [SerializeField]
    protected float maximumHealth = 15f;

    #endregion

    #region Sprite Facing Configuration

    public enum DefaultSpriteFacing
    {
        Left,
        Right
    }

    [Header("Sprite Facing")]
    [Tooltip(
        "Choose whether the source sprite art faces Left or Right by default. Used to compute SpriteRenderer.flipX correctly.")]
    [SerializeField]
    protected DefaultSpriteFacing defaultSpriteFacing = DefaultSpriteFacing.Left;

    [Tooltip(
        "If true, the enemy will keep its last facing direction while idle (when horizontal velocity is nearly zero).")]
    [SerializeField]
    protected bool rememberLastFacingWhileIdle = true;

    [Tooltip("Threshold of horizontal speed below which the sprite is considered idle for facing purposes.")]
    [SerializeField]
    protected float idleFacingSpeedThreshold = 0.05f;

    #endregion

    #region Debug Gizmos

    [Header("Debug Gizmos")]
    [Tooltip("Enable editor gizmos to visualize patrol points, vision ranges, candidate samples, and avoidance rays.")]
    [SerializeField]
    protected bool showDebugGizmos = true;

    [Tooltip("Maximum number of candidate sample points to draw for debug to avoid clutter.")] [SerializeField]
    protected int debugMaxCandidatePointsToDraw = 64;

    #endregion

    #region Runtime State (cached)

    protected Rigidbody2D rigidbody2D;
    protected Vector2 smoothedVelocity;
    protected int currentPatrolIndex = 0;

    // Perception memory
    protected Vector2 lastKnownTargetPosition = Vector2.zero;
    protected float lastSeenTimestamp = -Mathf.Infinity;

    protected float currentHealth;

    // Animator and visuals (optional)
    protected Animator animator;
    protected SpriteRenderer spriteRenderer;

    // Cached animator parameter hashes to avoid string lookups
    protected static readonly int animatorHashIsHit = Animator.StringToHash("isHit");
    protected static readonly int animatorHashIsAttacking = Animator.StringToHash("isAttacking");
    protected static readonly int animatorHashIsWalking = Animator.StringToHash("isWalking");

    // Facing state remembered for idle facing
    protected bool lastFacingRight = true;

    #endregion

    #region Unity lifecycle

    protected virtual void Awake()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Ensure rotation is frozen for consistency
        if (rigidbody2D != null)
            rigidbody2D.constraints |= RigidbodyConstraints2D.FreezeRotation;

        // initialize lastFacingRight from default sprite facing (so first frame is deterministic)
        lastFacingRight = (defaultSpriteFacing == DefaultSpriteFacing.Left) ? false : true;

        // Allow subclass to perform additional awake initialization
        OnAwakeCustomInit();
    }

    protected virtual void Start()
    {
        currentHealth = maximumHealth;

        // sanitize sampling parameters
        candidateRings = Mathf.Max(1, candidateRings);
        candidatePerRing = Mathf.Max(1, candidatePerRing);
        avoidanceRayCount = Mathf.Max(1, avoidanceRayCount);
        debugMaxCandidatePointsToDraw = Mathf.Max(1, debugMaxCandidatePointsToDraw);
    }

    protected virtual void FixedUpdate()
    {
        // Perception and memory using fixed-time because we run inside FixedUpdate
        UpdatePerception();

        // Decide high-level desired velocity (not yet movement-model applied)
        Vector2 desiredHighLevel = DecideHighLevelDesiredVelocity();

        // Allow subclass to convert high-level desired into a movement-model-appropriate desired vector
        Vector2 desiredMovement = ComputeMovementVelocity(desiredHighLevel);

        // Apply avoidance fallback if needed (default is provided; subclasses may override)
        Vector2 desiredAfterAvoidance = ApplyAvoidanceIfNeeded(desiredMovement);

        // Smooth and apply final velocity
        SmoothAndApplyVelocity(desiredAfterAvoidance);

        // Update visuals and animator parameters, including robust flip logic
        UpdateVisualsAndAnimator();
    }

    #endregion

    #region Perception and memory

    /// <summary>
    /// Update vision detection and memory state. Uses Time.fixedTime.
    /// Records lastKnownTargetPosition when target is seen.
    /// Implements full-forget behavior when memory expires.
    /// </summary>
    protected virtual void UpdatePerception()
    {
        bool targetInSight = false;

        if (target != null)
        {
            float visionRange = chaseRadius + extendedVisionBonus;
            Vector2 delta = (Vector2)target.position - (Vector2)transform.position;
            float sqrDist = delta.sqrMagnitude;

            if (sqrDist <= visionRange * visionRange)
            {
                Vector2 direction = delta.normalized;
                float distance = Mathf.Sqrt(sqrDist);

                // Raycast to target to check occlusion. Assumes obstacleMask configured to include things that block sight.
                RaycastHit2D hit = Physics2D.Raycast(rigidbody2D.position, direction, distance, obstacleMask);
                if (hit.collider == null)
                {
                    targetInSight = true;
                    lastKnownTargetPosition = target.position;
                    lastSeenTimestamp = Time.fixedTime;
                }
            }
        }

        bool isRemembering = (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;

        if (!isRemembering && !targetInSight)
        {
            // Full forget behavior
            lastKnownTargetPosition = Vector2.zero;
            lastSeenTimestamp = -Mathf.Infinity;
        }
    }

    #endregion

    #region Decision: patrol or chase

    /// <summary>
    /// Decide the high-level desired velocity vector based on patrol/chase logic.
    /// This uses lastKnownTargetPosition and memory window to determine chasing state.
    /// It returns a raw desired vector (magnitude indicates desired speed).
    /// </summary>
    protected virtual Vector2 DecideHighLevelDesiredVelocity()
    {
        // Determine whether we should chase: we have a last known position and still remember it
        bool shouldChase = lastKnownTargetPosition != Vector2.zero &&
                           (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;

        if (shouldChase)
        {
            // Chase behavior: use last known position as goal
            Vector2 rawGoal = lastKnownTargetPosition;

            // enforce preferred vertical separation from the player's Y
            float dy = rawGoal.y - transform.position.y;
            if (Mathf.Abs(dy) < preferredVerticalSeparation)
            {
                float direction = Mathf.Sign(dy);
                if (direction == 0f)
                    direction = (transform.position.y <= rawGoal.y) ? -1f : 1f;
                rawGoal.y = rawGoal.y + direction * preferredVerticalSeparation;
            }

            Vector2 goal = GetReachableGoal(rawGoal);
            Vector2 toGoal = goal - rigidbody2D.position;
            if (toGoal.sqrMagnitude <= patrolPointThreshold * patrolPointThreshold)
            {
                // Reached last-known position; if target not seen right now, stop chasing
                bool currentlySeen = (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;
                if (!currentlySeen)
                    return Vector2.zero;
            }

            // desired speed when chasing uses chaseSpeedMultiplier
            return toGoal.normalized * (movementSpeed * chaseSpeedMultiplier);
        }

        // Patrol behavior (two-point) if enabled
        if (enablePatrol && patrolPointA != null && patrolPointB != null)
        {
            Transform left = patrolPointA;
            Transform right = patrolPointB;

            Vector2 goal = (currentPatrolIndex == 0) ? (Vector2)left.position : (Vector2)right.position;
            Vector2 reachable = GetReachableGoal(goal);
            Vector2 toGoal = reachable - rigidbody2D.position;

            if (toGoal.sqrMagnitude <= patrolPointThreshold * patrolPointThreshold)
            {
                // toggle patrol index when reaching point
                currentPatrolIndex = 1 - currentPatrolIndex;
            }

            return toGoal.normalized * movementSpeed;
        }

        // Idle / hover
        return Vector2.zero;
    }

    #endregion

    #region Local pathfinding (candidate sampling)

    /// <summary>
    /// Returns a reachable point near rawGoal. If direct path is free, returns rawGoal.
    /// Otherwise samples ring candidates around rawGoal and returns first reachable candidate.
    /// Falls back to rawGoal nudged upward slightly.
    /// </summary>
    protected Vector2 GetReachableGoal(Vector2 rawGoal)
    {
        Vector2 from = rigidbody2D.position;
        Vector2 dirToRaw = rawGoal - from;
        float distToRaw = dirToRaw.magnitude;
        if (distToRaw <= 0.01f) return rawGoal;

        // Direct path free?
        RaycastHit2D directHit = Physics2D.Raycast(from, dirToRaw.normalized, distToRaw, obstacleMask);
        if (directHit.collider == null) return rawGoal;

        // Sample candidate rings (small radius first)
        for (int ring = 1; ring <= candidateRings; ring++)
        {
            float radius = candidateRadius * (ring / (float)candidateRings);
            for (int i = 0; i < candidatePerRing; i++)
            {
                float angleDegrees = (360f / candidatePerRing) * i;
                float angleRad = angleDegrees * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;

                // upward bias scaled with ring index
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

        // fallback: nudge upward a little to help flyers escape
        return rawGoal + Vector2.up * candidateUpBias;
    }

    #endregion

    #region Avoidance fallback (default implementation)

    /// <summary>
    /// Basic multi-ray avoidance fallback. Subclasses can override for specialized steering.
    /// </summary>
    protected virtual Vector2 ApplyAvoidanceIfNeeded(Vector2 desired)
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

            // rotate forward by angle
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

        // Upward bias for flying enemies; grounded enemies should clamp vertical component
        if (allowAscendWhenBlocked && CanAscendWhenAvoiding())
        {
            blended.y += ascendSpeedBias;
        }
        else
        {
            // small clamp to avoid walkers trying to climb obstacles
            blended.y = Mathf.Clamp(blended.y, -0.2f, 0.2f);
        }

        return blended.normalized * desired.magnitude;
    }

    /// <summary>
    /// Hook for subclasses to indicate whether they allow ascend bias in avoidance.
    /// Flying enemies should return true; walking enemies should return false.
    /// </summary>
    protected abstract bool CanAscendWhenAvoiding();

    #endregion

    #region Movement model (abstract)

    /// <summary>
    /// Subclasses implement this to convert the high-level desired vector into a movement-model-appropriate desired vector.
    /// Examples:
    /// - FlyingEnemy: return desired as-is (2D velocity).
    /// - WalkingEnemy: control only X component and preserve rigidbody2D.velocity.y.
    /// </summary>
    protected abstract Vector2 ComputeMovementVelocity(Vector2 desiredHighLevel);

    #endregion

    #region Smoothing and apply

    /// <summary>
    /// Smooths velocity changes toward the desired vector and writes to Rigidbody2D.velocity.
    /// </summary>
    protected void SmoothAndApplyVelocity(Vector2 desired)
    {
        smoothedVelocity = Vector2.MoveTowards(smoothedVelocity, desired, acceleration * Time.fixedDeltaTime);
        if (rigidbody2D != null)
            rigidbody2D.velocity = smoothedVelocity;
    }

    #endregion

    #region Visuals and Animator (includes robust flip logic)

    /// <summary>
    /// Default visual and animator updates. Handles sprite flipping using SpriteRenderer.flipX.
    /// flipX is set so the final visible direction matches movement direction, taking into account
    /// the default sprite facing (Left or Right). Idle facing is optionally remembered.
    /// </summary>
    protected virtual void UpdateVisualsAndAnimator()
    {
        // Animator walking flag
        if (animator != null)
        {
            bool isWalking = Mathf.Abs(rigidbody2D.velocity.x) > 0.1f || Mathf.Abs(rigidbody2D.velocity.y) > 0.1f;
            animator.SetBool(animatorHashIsWalking, isWalking);
        }

        // Sprite flipping logic (safe; uses flipX)
        if (spriteRenderer != null)
        {
            float vx = rigidbody2D.velocity.x;
            bool hasMeaningfulHorizontalMovement = Mathf.Abs(vx) > idleFacingSpeedThreshold;
            bool desiredFacingRight;

            if (hasMeaningfulHorizontalMovement)
            {
                // Determine desired facing from current horizontal velocity
                desiredFacingRight = vx > 0f;
                // Update remembered facing
                lastFacingRight = desiredFacingRight;
            }
            else
            {
                // Idle: either keep last facing or derive from lastFacingRight default initialized in Awake
                desiredFacingRight = rememberLastFacingWhileIdle
                    ? lastFacingRight
                    : (defaultSpriteFacing == DefaultSpriteFacing.Left ? false : true);
            }

            // spriteRenderer.flipX should be true when the final visible direction is opposite the source art.
            // If the source art faces Left by default:
            //   - to show right-facing, flipX must be true
            //   - to show left-facing, flipX must be false
            // The following boolean expression captures that mapping:
            bool artFacesLeft = (defaultSpriteFacing == DefaultSpriteFacing.Left);
            spriteRenderer.flipX = (desiredFacingRight == artFacesLeft);
        }
    }

    #endregion

    #region Damage and health (basic)

    /// <summary>
    /// Apply damage to this enemy. Returns true if the enemy died as a result.
    /// Subclasses can override to add effects (animations, loot, etc.).
    /// </summary>
    public virtual bool TakeDamage(float amount)
    {
        if (animator != null) animator.SetTrigger(animatorHashIsHit);

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);

        if (currentHealth <= 0f)
        {
            Die();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Default death handling: destroy the GameObject. Subclasses can override to play death animations first.
    /// </summary>
    protected virtual void Die()
    {
        Destroy(gameObject);
    }

    #endregion

    #region Debug Gizmos

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Patrol points and connecting line
        Gizmos.color = Color.cyan;
        if (patrolPointA != null) Gizmos.DrawWireSphere(patrolPointA.position, 0.25f);
        if (patrolPointB != null) Gizmos.DrawWireSphere(patrolPointB.position, 0.25f);
        if (patrolPointA != null && patrolPointB != null) Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);

        // Chase ranges: base (yellow) and extended (red)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRadius);

        Gizmos.color = Color.red;
        float effectiveVision = chaseRadius + extendedVisionBonus;
        Gizmos.DrawWireSphere(transform.position, effectiveVision);

        // Last known target position and candidate samples
        if (lastKnownTargetPosition != Vector2.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(lastKnownTargetPosition, 0.06f);
            Gizmos.DrawLine(transform.position, lastKnownTargetPosition);

            Gizmos.color = Color.green;
            int drawn = 0;
            for (int ring = 1; ring <= Mathf.Max(1, candidateRings); ring++)
            {
                float radius = candidateRadius * (ring / (float)Mathf.Max(1, candidateRings));
                for (int i = 0; i < Mathf.Max(1, candidatePerRing); i++)
                {
                    if (drawn >= debugMaxCandidatePointsToDraw) break;
                    float angleDegrees = (360f / Mathf.Max(1, candidatePerRing)) * i;
                    float angleRad = angleDegrees * Mathf.Deg2Rad;
                    Vector2 offset = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * radius;
                    offset += Vector2.up * candidateUpBias * (ring / (float)Mathf.Max(1, candidateRings));
                    Vector2 candidate = lastKnownTargetPosition + offset;
                    Gizmos.DrawWireSphere(candidate, 0.05f);
                    Gizmos.DrawLine(lastKnownTargetPosition, candidate);
                    drawn++;
                }

                if (drawn >= debugMaxCandidatePointsToDraw) break;
            }
        }

        // Draw avoidance rays preview using smoothedVelocity as forward
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
                Gizmos.DrawLine(transform.position, (Vector2)transform.position + dir * avoidanceRayDistance);
            }
        }
    }
#endif

    #endregion

    #region Subclass hook

    /// <summary>
    /// Hook for subclasses to perform additional Awake initialization.
    /// </summary>
    protected virtual void OnAwakeCustomInit()
    {
    }

    #endregion
}