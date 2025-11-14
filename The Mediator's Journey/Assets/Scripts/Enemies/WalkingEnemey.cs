using UnityEngine;

/// <summary>
/// WalkingEnemy
/// Two-point walker that follows the player when in range, otherwise patrols between A and B.
/// - Chase takes precedence over patrol (uses PatrolEnemy perception & memory).
/// - Optional pause at patrol points (pauseEnabled / pauseTime).
/// - Preserves Rigidbody2D vertical velocity; controls horizontal only.
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
        // Ensure reasonable gravity for walkers
        if (rigidbody2D != null && Mathf.Approximately(rigidbody2D.gravityScale, 0f))
            rigidbody2D.gravityScale = 1f;

        // Walkers should not ascend when avoiding
        allowAscendWhenBlocked = false;
    }

    protected override Vector2 DecideHighLevelDesiredVelocity()
    {
        // --- CHASE logic: priority over patrol ---
        // If the player is currently visible or remembered (within memoryDuration), chase.
        bool currentlySeen = (Time.fixedTime - lastSeenTimestamp) <= memoryDuration &&
                             lastKnownTargetPosition != Vector2.zero;
        if (currentlySeen)
        {
            // If target Transform is assigned and visible, prefer its current position; otherwise use lastKnownTargetPosition
            Vector2 goal = (target != null)
                ? (Vector2)target.position
                : lastKnownTargetPosition;

            // If we have a lastKnownTargetPosition but the base perception might have updated it,
            // prefer lastKnownTargetPosition if the raycast check in UpdatePerception determined visibility.
            // Use GetReachableGoal to get a reachable target near the player
            Vector2 reachable = GetReachableGoal(goal);
            Vector2 toGoal = reachable - rigidbody2D.position;
            if (toGoal.sqrMagnitude <= 0.0001f)
                return Vector2.zero;

            // Use chase speed (movementSpeed * chaseSpeedMultiplier from base) and apply walking-specific multiplier
            float baseChaseSpeed = movementSpeed * chaseSpeedMultiplier;
            float finalSpeed = baseChaseSpeed * walkingChaseMultiplier;

            if (animator != null) animator.SetBool(animatorHashIsWalking, true);

            return toGoal.normalized * finalSpeed;
        }

        // If currently paused (patrol) check whether we should end the pause
        if (isPaused)
        {
            if (!pauseEnabled || pauseTime <= 0f || Time.fixedTime - pauseStartTime >= pauseTime)
            {
                // Commit the next patrol index and resume moving
                if (nextPatrolIndex >= 0)
                {
                    currentPatrolIndex = nextPatrolIndex;
                    nextPatrolIndex = -1;
                }

                isPaused = false;
            }
            else
            {
                // Still pausing: remain idle
                if (animator != null) animator.SetBool(animatorHashIsWalking, false);
                return Vector2.zero;
            }
        }

        // --- PATROL logic ---
        if (!enablePatrol || patrolPointA == null || patrolPointB == null)
            return Vector2.zero;

        // Determine current patrol target
        Transform currentTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
        Vector2 pointPos = (Vector2)currentTarget.position;
        Vector2 toPoint = pointPos - rigidbody2D.position;
        float threshSqr = patrolPointThreshold * patrolPointThreshold;

        // If within threshold, either start a pause (if enabled) or toggle immediately
        if (toPoint.sqrMagnitude <= threshSqr)
        {
            if (pauseEnabled && pauseTime > 0f)
            {
                // Start pause and store where we'll go next after the pause
                isPaused = true;
                pauseStartTime = Time.fixedTime;
                nextPatrolIndex = 1 - currentPatrolIndex;
                if (animator != null) animator.SetBool(animatorHashIsWalking, false);
                return Vector2.zero;
            }
            else
            {
                // Immediate toggle and continue to other point
                currentPatrolIndex = 1 - currentPatrolIndex;
                currentTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
                pointPos = (Vector2)currentTarget.position;
            }
        }

        // Move toward reachable goal for the selected current target
        Vector2 reachablePoint = GetReachableGoal(pointPos);
        Vector2 toReachable = reachablePoint - rigidbody2D.position;
        if (toReachable.sqrMagnitude <= 0.0001f)
        {
            if (animator != null) animator.SetBool(animatorHashIsWalking, false);
            return Vector2.zero;
        }

        if (animator != null) animator.SetBool(animatorHashIsWalking, true);
        return toReachable.normalized * movementSpeed;
    }

    protected override Vector2 ComputeMovementVelocity(Vector2 desiredHighLevel)
    {
        // Preserve vertical physics velocity, control horizontal only
        float desiredX = desiredHighLevel.x;
        return new Vector2(desiredX, rigidbody2D.velocity.y);
    }

    protected override Vector2 ApplyAvoidanceIfNeeded(Vector2 desired)
    {
        Vector2 blended = base.ApplyAvoidanceIfNeeded(desired);
        // Preserve physics vertical velocity for walkers
        blended.y = rigidbody2D.velocity.y;
        if (float.IsNaN(blended.x) || float.IsInfinity(blended.x)) blended.x = desired.x;
        return blended;
    }

    protected override bool CanAscendWhenAvoiding() => false;

    public override bool TakeDamage(float amount)
    {
        if (animator != null) animator.SetTrigger(animatorHashIsHit);
        return base.TakeDamage(amount);
    }
}