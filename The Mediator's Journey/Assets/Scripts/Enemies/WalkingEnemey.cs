using UnityEngine;

/// <summary>
/// WalkingEnemy
/// Minimal two-point walker with optional pause that reliably alternates A <-> B.
/// - Subclass owns patrol toggling (PatrolEnemy no longer toggles).
/// - If pauseEnabled: on arrival we idle for pauseTime, then switch to the other point and walk off.
/// - If pauseDisabled: toggle immediately on arrival and continue to the other point.
/// - Controls horizontal velocity only; preserves Rigidbody2D vertical velocity.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WalkingEnemy : PatrolEnemy
{
    [Header("Walking Enemy")] [Tooltip("Enable a short pause when reaching a patrol point.")] [SerializeField]
    private bool pauseEnabled = true;

    [Tooltip("Pause time in seconds when the enemy reaches a patrol point. Ignored if pauseEnabled is false.")]
    [SerializeField]
    private float pauseTime = 1f;

    [Tooltip("Extra multiplier applied to movementSpeed while chasing.")] [SerializeField]
    private float walkingChaseMultiplier = 1.0f;

    // Pause bookkeeping
    private bool isPaused = false;
    private float pauseStartTime = -Mathf.Infinity;
    private int nextPatrolIndex = -1; // index to switch to after pause

    protected override void OnAwakeCustomInit()
    {
        // Ensure reasonable gravity for walkers
        if (rigidbody2D != null && Mathf.Approximately(rigidbody2D.gravityScale, 0f))
            rigidbody2D.gravityScale = 1f;

        allowAscendWhenBlocked = false;
    }

    protected override Vector2 DecideHighLevelDesiredVelocity()
    {
        // If chasing, use base chase behavior and apply optional multiplier
        if (lastKnownTargetPosition != Vector2.zero && (Time.fixedTime - lastSeenTimestamp) <= memoryDuration)
        {
            Vector2 chase = base.DecideHighLevelDesiredVelocity();
            if (chase == Vector2.zero) return Vector2.zero;
            if (!Mathf.Approximately(walkingChaseMultiplier, 1f))
                chase = chase.normalized * (chase.magnitude * walkingChaseMultiplier);
            return chase;
        }

        // Otherwise simple two-point patrol with optional pause
        if (!enablePatrol || patrolPointA == null || patrolPointB == null)
            return Vector2.zero;

        // If currently paused, check whether we should end the pause
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

        // Determine current point and distance to it (use exact patrol point for arrival detection)
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
                // immediate toggle and continue to the other point
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