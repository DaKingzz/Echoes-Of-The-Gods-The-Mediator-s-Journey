using UnityEngine;

/// <summary>
/// FlyingEnemy
/// Concrete enemy that moves freely in 2D (X and Y).
/// - Uses PatrolEnemy for perception, high-level decision, pathfinding and smoothing.
/// - Enforces flying-specific physics (zero gravity) in Awake.
/// - Allows ascend bias during avoidance and uses full 2D movement model.
/// - Triggers attack via EnemyFireballAttack when available.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingEnemy : PatrolEnemy
{
    #region Flying-specific Configuration

    [Header("Flying Enemy")]
    [Tooltip("Optional attack component used by this flying enemy (e.g., fireball shooter).")]
    [SerializeField]
    private EnemyFireballAttack fireballAttack;

    [Tooltip(
        "Optional multiplier to further increase speed while chasing (additional to chaseSpeedMultiplier in base).")]
    [SerializeField]
    private float extraChaseSpeedMultiplier = 1.0f;

    #endregion

    #region Cached runtime state

    // cache of components resolved in Awake
    private Rigidbody2D rb2d;

    #endregion

    #region Awake / initialization

    protected override void OnAwakeCustomInit()
    {
        rb2d = GetComponent<Rigidbody2D>();

        // Ensure flying physics: zero gravity and no rotation
        if (rb2d != null)
        {
            rb2d.gravityScale = 0f;
            rb2d.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        // Try to auto-find an attack component if not explicitly assigned in inspector
        if (fireballAttack == null)
        {
            fireballAttack = GetComponent<EnemyFireballAttack>();
        }

        // Ensure base allows ascend when blocked for flyers
        allowAscendWhenBlocked = true;
    }

    #endregion

    #region Per-frame / decision overrides

    /// <summary>
    /// Decide desired velocity and trigger attack when appropriate.
    /// We call the base high-level decision to compute chase/patrol desired speed,
    /// then attempt attack if we have an attack component and a remembered/seen target.
    /// </summary>
    protected override Vector2 DecideHighLevelDesiredVelocity()
    {
        // Let base determine the high-level desired movement (handles chase/patrol)
        Vector2 desired = base.DecideHighLevelDesiredVelocity();

        // If we have a known target position and an attack component, attempt an attack
        // Attack logic is left to the fireballAttack.TryAttack implementation (range/cooldown aware)
        if (lastKnownTargetPosition != Vector2.zero && fireballAttack != null)
        {
            if (fireballAttack.TryAttack())
            {
                if (animator != null)
                {
                    animator.SetTrigger(animatorHashIsAttacking);
                }
            }
        }

        // Apply any subclass-specific speed multipliers when chasing (desired magnitude already includes chaseSpeedMultiplier)
        // We scale the magnitude if extraChaseSpeedMultiplier differs from 1
        if (desired != Vector2.zero && extraChaseSpeedMultiplier != 1f)
        {
            desired = desired.normalized * (desired.magnitude * extraChaseSpeedMultiplier);
        }

        return desired;
    }

    #endregion

    #region Movement model and avoidance

    /// <summary>
    /// Flying enemies use the desired high-level movement directly as a full 2D velocity.
    /// This means the movement model controls both X and Y components.
    /// </summary>
    protected override Vector2 ComputeMovementVelocity(Vector2 desiredHighLevel)
    {
        return desiredHighLevel;
    }

    /// <summary>
    /// Flying enemies allow ascend bias during avoidance; the base implementation already
    /// supports ascend when CanAscendWhenAvoiding returns true (and allowAscendWhenBlocked is true).
    /// We still call base for ray-based avoidance; override kept for clarity and potential future extension.
    /// </summary>
    protected override Vector2 ApplyAvoidanceIfNeeded(Vector2 desired)
    {
        // Use base avoidance which blends normals and applies ascend bias when allowed
        return base.ApplyAvoidanceIfNeeded(desired);
    }

    /// <summary>
    /// Flying enemies can ascend when avoiding obstacles.
    /// </summary>
    protected override bool CanAscendWhenAvoiding()
    {
        return true;
    }

    #endregion

    #region Damage & death

    /// <summary>
    /// Trigger hit animation then forward to base damage handling.
    /// Returns true when the enemy died as a result of the damage.
    /// </summary>
    public override bool TakeDamage(float amount)
    {
        if (animator != null)
        {
            animator.SetTrigger(animatorHashIsHit);
        }

        return base.TakeDamage(amount);
    }

    #endregion
}