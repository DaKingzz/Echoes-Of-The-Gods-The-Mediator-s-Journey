using UnityEngine;

/// <summary>
/// FlyingEnemy
/// Moves freely in 2D and correctly alternates patrolPointA ↔ patrolPointB.
/// - Patrol toggling moved into this subclass (base no longer toggles).
/// - Supports optional pause at each patrol point and an exit margin to avoid immediate re-toggles.
/// - Still prefers chase behavior when a target is remembered/seen (delegates chase to base).
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

    [Tooltip("Pause time in seconds when the enemy reaches a patrol point. Set to 0 for no pause.")] [SerializeField]
    private float pauseTime = 0.0f;

    [Tooltip(
        "Small world-space margin (units) the enemy must exit beyond the patrolPointThreshold after leaving a point to allow next arrival.")]
    [SerializeField]
    private float exitDistanceMargin = 0.15f;

    // Patrol state machine
    private enum PatrolState
    {
        Moving,
        Paused
    }

    private PatrolState patrolState = PatrolState.Moving;
    private float stateStartTime = -Mathf.Infinity;

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

        allowAscendWhenBlocked = true;
    }

    /// <summary>
    /// Prefer chase when a remembered target exists; otherwise run patrol state machine
    /// that toggles currentPatrolIndex A↔B and optionally pauses at points.
    /// </summary>
    protected override Vector2 DecideHighLevelDesiredVelocity()
    {
        // If chasing (base can compute chase vector toward lastKnownTargetPosition), use it.
        bool isChasing = lastKnownTargetPosition != Vector2.zero &&
                         (Time.fixedTime - lastSeenTimestamp) <= memoryDuration;
        if (isChasing)
        {
            Vector2 chase = base.DecideHighLevelDesiredVelocity();
            if (chase != Vector2.zero && !Mathf.Approximately(extraChaseSpeedMultiplier, 1f))
                chase = chase.normalized * (chase.magnitude * extraChaseSpeedMultiplier);

            // Attempt attack when remembered target exists
            if (fireballAttack != null && fireballAttack.TryAttack() && animator != null)
                animator.SetTrigger(animatorHashIsAttacking);

            return chase;
        }

        // Not chasing: handle explicit patrol A <-> B toggling
        if (!enablePatrol || patrolPointA == null || patrolPointB == null)
            return Vector2.zero;

        Transform currentTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
        Vector2 targetPos = (Vector2)currentTarget.position;
        Vector2 reachable = GetReachableGoal(targetPos);
        Vector2 toTarget = reachable - rigidbody2D.position;

        float threshSqr = patrolPointThreshold * patrolPointThreshold;
        float exitSqr = (patrolPointThreshold + exitDistanceMargin) * (patrolPointThreshold + exitDistanceMargin);

        // MOVING state: move toward current target; on arrival switch to PAUSED (or toggle immediately if no pause)
        if (patrolState == PatrolState.Moving)
        {
            if (toTarget.sqrMagnitude > threshSqr)
            {
                if (animator != null) animator.SetBool(animatorHashIsWalking, true);
                return toTarget.normalized * movementSpeed;
            }

            // Arrived
            patrolState = PatrolState.Paused;
            stateStartTime = Time.fixedTime;
            if (animator != null) animator.SetBool(animatorHashIsWalking, false);

            if (pauseTime <= 0f)
            {
                // No pause requested: toggle immediately and start moving toward the other point
                TogglePatrolIndex();
                patrolState = PatrolState.Moving;
                stateStartTime = Time.fixedTime;
            }
            else
            {
                // Pausing now
                return Vector2.zero;
            }
        }

        // PAUSED state: wait until pauseTime elapsed AND the enemy has moved sufficiently away before allowing another toggle
        if (patrolState == PatrolState.Paused)
        {
            // If still within pause window, remain idle
            if (Time.fixedTime - stateStartTime < pauseTime)
                return Vector2.zero;

            // After pause elapsed: ensure we toggle to the other point only if we are still within arrival threshold.
            // This prevents double toggles if we already toggled immediately earlier.
            if (toTarget.sqrMagnitude <= threshSqr)
            {
                TogglePatrolIndex();
            }

            patrolState = PatrolState.Moving;
            if (animator != null) animator.SetBool(animatorHashIsWalking, true);
        }

        // Compute movement toward (possibly toggled) current target
        Transform nextTarget = (currentPatrolIndex == 0) ? patrolPointA : patrolPointB;
        if (nextTarget == null) return Vector2.zero;
        Vector2 nextReachable = GetReachableGoal((Vector2)nextTarget.position);
        Vector2 toNext = nextReachable - rigidbody2D.position;
        if (toNext.sqrMagnitude <= 0.0001f) return Vector2.zero;

        return toNext.normalized * movementSpeed;
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

    // Simple toggle helper
    private void TogglePatrolIndex()
    {
        currentPatrolIndex = 1 - currentPatrolIndex;
    }
}