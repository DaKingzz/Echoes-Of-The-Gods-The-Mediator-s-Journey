using UnityEngine;

/// <summary>
/// FlyingEnemy
/// - Full 2D mover that chases player when seen/remembered (delegates chase goal to base).
/// - Implements its own A <-> B patrol toggling (base no longer toggles).
/// - Optional pause at patrol points (pauseEnabled / pauseTime).
/// - Uses animator triggers for events and sets PatrolEnemy.forceIdle while paused so parent drives isWalking.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingEnemy : PatrolEnemy
{
    [Header("Flying Enemy")]
    [Tooltip("Optional attack component used by this flying enemy (e.g., fireball shooter).")]
    [SerializeField]
    private EnemyFireballAttack fireballAttack;

    [Tooltip(
        "Optional multiplier to further increase speed while chasing (additional to chaseSpeedMultiplier in base).")]
    [SerializeField]
    private float extraChaseSpeedMultiplier = 1.0f;

    [Tooltip("Enable a short pause when reaching a patrol point.")] [SerializeField]
    private bool pauseEnabled = false;

    [Tooltip("Pause time in seconds when the enemy reaches a patrol point. Ignored if pauseEnabled is false.")]
    [SerializeField]
    private float pauseTime = 0.5f;

    // simple patrol pause state
    private bool isPaused = false;
    private float pauseStartTime = -Mathf.Infinity;
    private int nextPatrolIndex = -1;

    // cached rigidbody (convenience)
    private Rigidbody2D rb2d;

    protected override void OnAwakeCustomInit()
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        if (fireballAttack == null)
            fireballAttack = GetComponent<EnemyFireballAttack>();

        // Flying enemies allow ascending in avoidance
        allowAscendWhenBlocked = true;
    }

    /// <summary>
    /// Decide high-level desired velocity:
    /// - If chasing (player seen/remembered) use base chase logic and apply extra multiplier.
    /// - Otherwise run simple two-point patrol with optional pause; toggle currentPatrolIndex here.
    /// </summary>
    protected override Vector2 DecideHighLevelDesiredVelocity()
    {
        // Clear any animator override by default; children set forceIdle when they want to force idle.
        forceIdle = false;
        animatorSpeedOverride = float.NaN;

        // --- CHASE priority ---
        bool isChasing = lastKnownTargetPosition != Vector2.zero &&
                         (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;
        if (isChasing)
        {
            // Use base to compute chase vector toward the remembered/seen position
            Vector2 chase = base.DecideHighLevelDesiredVelocity();
            if (chase != Vector2.zero && !Mathf.Approximately(extraChaseSpeedMultiplier, 1f))
                chase = chase.normalized * (chase.magnitude * extraChaseSpeedMultiplier);

            // Attempt attack (children trigger animator event)
            if (fireballAttack != null && fireballAttack.TryAttack())
            {
                if (animator != null) animator.SetTrigger(animatorHashIsAttacking);
            }

            return chase;
        }

        // --- PATROL fallback (this subclass manages toggling) ---
        if (!enablePatrol || patrolPointA == null || patrolPointB == null)
            return Vector2.zero;

        // If currently paused, check whether to finish the pause
        if (isPaused)
        {
            if (!pauseEnabled || pauseTime <= 0f || Time.fixedTime - pauseStartTime >= pauseTime)
            {
                // commit next index and resume moving
                if (nextPatrolIndex >= 0)
                {
                    currentPatrolIndex = nextPatrolIndex;
                    nextPatrolIndex = -1;
                }

                isPaused = false;
                forceIdle = false;
            }
            else
            {
                // still pausing: instruct parent to set isWalking = false and do nothing else
                forceIdle = true;
                return Vector2.zero;
            }
        }

        // Compute vector toward current patrol point
        Transform currentTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
        Vector2 pointPos = (Vector2)currentTarget.position;
        Vector2 toPoint = pointPos - rigidbody2D.position;
        float threshSqr = patrolPointThreshold * patrolPointThreshold;

        // If within threshold, either start pause or toggle immediately
        if (toPoint.sqrMagnitude <= threshSqr)
        {
            if (pauseEnabled && pauseTime > 0f)
            {
                isPaused = true;
                pauseStartTime = Time.fixedTime;
                nextPatrolIndex = 1 - currentPatrolIndex;
                forceIdle = true; // parent will set isWalking = false
                return Vector2.zero;
            }
            else
            {
                // immediate toggle and continue toward the other point this frame
                currentPatrolIndex = 1 - currentPatrolIndex;
                currentTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
                pointPos = (Vector2)currentTarget.position;
            }
        }

        // Move toward reachable goal for the selected current target
        Vector2 reachable = GetReachableGoal(pointPos);
        Vector2 toReachable = reachable - rigidbody2D.position;
        if (toReachable.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        // Optionally inform animator speed override (useful if you want parent to consider it)
        animatorSpeedOverride = toReachable.magnitude;

        return toReachable.normalized * movementSpeed;
    }

    protected override Vector2 ComputeMovementVelocity(Vector2 desiredHighLevel)
    {
        // Flying uses full 2D velocity
        return desiredHighLevel;
    }

    protected override Vector2 ApplyAvoidanceIfNeeded(Vector2 desired)
    {
        return base.ApplyAvoidanceIfNeeded(desired);
    }

    protected override bool CanAscendWhenAvoiding() => true;

    public override bool TakeDamage(float amount)
    {
        if (animator != null) animator.SetTrigger(animatorHashIsHit);
        return base.TakeDamage(amount);
    }
}