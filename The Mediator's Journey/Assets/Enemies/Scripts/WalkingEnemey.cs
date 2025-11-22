using UnityEngine;

/// <summary>
/// WalkingEnemy
/// - Ground-based walker that patrols between patrolPointA and patrolPointB and chases the player when seen/remembered.
/// - Uses PatrolEnemy for perception, pathfinding and smoothing.
/// - Subclass owns patrol toggling (base no longer toggles).
/// - Optional pause at patrol points (pauseEnabled / pauseTime). While paused this class sets PatrolEnemy.forceIdle so the parent writes isWalking=false.
/// - Preserves Rigidbody2D vertical velocity and controls horizontal velocity only.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WalkingEnemy : PatrolEnemy
{
    [Header("Walking Enemy")] [Tooltip("Enable a short pause when reaching a patrol point.")] [SerializeField]
    private bool pauseEnabled = true;

    [Tooltip("Pause time in seconds when the enemy reaches a patrol point. Ignored if pauseEnabled is false.")]
    [SerializeField]
    private float pauseTime = 0.6f;

    [Tooltip("Extra multiplier applied to movementSpeed while chasing.")] [SerializeField]
    private float walkingChaseMultiplier = 1.2f;

    // Pause bookkeeping
    private bool isPaused = false;
    private float pauseStartTime = -Mathf.Infinity;
    private int nextPatrolIndex = -1; // index to switch to after pause

    protected override void OnAwakeCustomInit()
    {
        // Ensure reasonable gravity for walkers if designer left it at zero
        if (rigidbody2D != null && Mathf.Approximately(rigidbody2D.gravityScale, 0f))
            rigidbody2D.gravityScale = 1f;

        // Walkers should not ascend when avoiding
        allowAscendWhenBlocked = false;

        // Ensure any animator override signals are clear at start
        forceIdle = false;
        animatorSpeedOverride = float.NaN;
    }

    protected override Vector2 DecideHighLevelDesiredVelocity()
    {
        // Reset continuous animator override signals each decision; set them explicitly when needed below.
        forceIdle = false;
        animatorSpeedOverride = float.NaN;

        // --- CHASE priority ---
        bool isChasing = lastKnownTargetPosition != Vector2.zero &&
                         (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;
        if (isChasing)
        {
            // Use base chase computation (it returns a vector toward the remembered/seen goal)
            Vector2 chase = base.DecideHighLevelDesiredVelocity();
            if (chase == Vector2.zero) return Vector2.zero;
            if (!Mathf.Approximately(walkingChaseMultiplier, 1f))
                chase = chase.normalized * (chase.magnitude * walkingChaseMultiplier);

            // Let parent animate based on velocity; no need to forceIdle here.
            return chase;
        }

        // --- PATROL fallback (subclass toggles index) ---
        if (!enablePatrol || patrolPointA == null || patrolPointB == null)
            return Vector2.zero;

        // If currently paused, check whether the pause finished
        if (isPaused)
        {
            if (!pauseEnabled || pauseTime <= 0f || Time.fixedTime - pauseStartTime >= pauseTime)
            {
                // Commit the next index and resume moving
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
                // Still pausing: instruct parent to show idle
                forceIdle = true;
                return Vector2.zero;
            }
        }

        // Determine current patrol target and distance (use exact patrol point for arrival detection)
        Transform currentTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
        Vector2 pointPos = (Vector2)currentTarget.position;
        Vector2 toPoint = pointPos - rigidbody2D.position;
        float threshSqr = patrolPointThreshold * patrolPointThreshold;

        // If within threshold, either start pause or toggle immediately
        if (toPoint.sqrMagnitude <= threshSqr)
        {
            if (pauseEnabled && pauseTime > 0f)
            {
                // Start pause and remember where to go next after pause
                isPaused = true;
                pauseStartTime = Time.fixedTime;
                nextPatrolIndex = 1 - currentPatrolIndex;
                forceIdle = true; // parent will set isWalking = false
                return Vector2.zero;
            }
            else
            {
                // Immediate toggle and continue toward the other point this frame
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
            // Nothing to do; ensure idle animation via parent
            forceIdle = true;
            return Vector2.zero;
        }

        // Optionally provide a speed hint to the animator (parent may use animatorSpeedOverride)
        animatorSpeedOverride = toReachable.magnitude;

        // Movement along horizontal only will be applied in ComputeMovementVelocity
        return toReachable.normalized * movementSpeed;
    }

    protected override Vector2 ComputeMovementVelocity(Vector2 desiredHighLevel)
    {
        // Preserve vertical physics Y, control horizontal only
        float desiredX = desiredHighLevel.x;
        return new Vector2(desiredX, rigidbody2D.velocity.y);
    }

    protected override Vector2 ApplyAvoidanceIfNeeded(Vector2 desired)
    {
        // Use base avoidance but preserve vertical physics and clamp vertical influence for walkers
        Vector2 blended = base.ApplyAvoidanceIfNeeded(desired);
        blended.y = rigidbody2D.velocity.y;
        if (float.IsNaN(blended.x) || float.IsInfinity(blended.x))
            blended.x = desired.x;
        return blended;
    }

    protected override bool CanAscendWhenAvoiding() => false;

    public override bool TakeDamage(float amount)
    {
        if (animator != null) animator.SetTrigger(animatorHashIsHit);
        return base.TakeDamage(amount);
    }
}