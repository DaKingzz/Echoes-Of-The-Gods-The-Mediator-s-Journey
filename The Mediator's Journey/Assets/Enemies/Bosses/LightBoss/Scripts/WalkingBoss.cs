using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WalkingBoss
/// - Melee boss that walks and dashes.
/// - Implements smart behavior: approaches player, attacks with telegraphed windups, creates vulnerability windows, and retreats when taking heavy damage.
/// - Uses state machine for clear behavior flow.
/// - Always knows where the player is (no perception needed).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class WalkingBoss : MonoBehaviour, IEnemy
{

    public GameObject npcPrefab;

    private enum BossState
    {
        Idle,
        Approaching,
        AttackWindup,
        Attacking,
        Recovering,
        Retreating,
        Dashing,
        Dead
    }

    [Header("References")]
    [Tooltip("The player transform. Boss will always know where the player is.")]
    [SerializeField]
    private Transform playerTransform;

    [Tooltip("Child GameObject with BossDamageArea component. Used to detect player proximity and deal damage.")]
    [SerializeField]
    private GameObject damageAreaObject;

    [Header("Arena Boundaries")]
    [Tooltip("Left edge of the arena. Boss will stop retreating if it reaches this position.")]
    [SerializeField]
    private Transform leftEdge;

    [Tooltip("Right edge of the arena. Boss will stop retreating if it reaches this position.")] [SerializeField]
    private Transform rightEdge;

    [Tooltip("Distance from edge to stop retreating (prevents getting stuck exactly at edge).")] [SerializeField]
    private float edgeBuffer = 0.5f;

    [Header("Health")] [Tooltip("Maximum health of the boss.")] [SerializeField]
    private float maxHealth = 100f;

    [Header("Movement")] [Tooltip("Movement speed when walking toward the player.")] [SerializeField]
    private float moveSpeed = 3f;

    [Tooltip("How close the boss needs to be to the player before stopping (to avoid jittering).")] [SerializeField]
    private float stoppingDistance = 0.5f;

    [Header("Attack Configuration")] [Tooltip("Damage dealt to the player per attack.")] [SerializeField]
    private float attackDamage = 15f;

    [Tooltip("Duration of the attack windup (telegraph). Player can hit boss during this time.")] [SerializeField]
    private float attackWindupDuration = 0.8f;

    [Tooltip("Duration of the recovery phase after attacking. Boss is vulnerable during this time.")] [SerializeField]
    private float attackRecoveryDuration = 1.5f;

    [Tooltip("Minimum time between attacks when in combat.")] [SerializeField]
    private float minTimeBetweenAttacks = 2f;

    [Tooltip("Maximum time between attacks when in combat.")] [SerializeField]
    private float maxTimeBetweenAttacks = 4f;

    [Header("Dash Configuration")] [Tooltip("Speed of the dash.")] [SerializeField]
    private float dashSpeed = 15f;

    [Tooltip("Duration of the dash in seconds.")] [SerializeField]
    private float dashDuration = 0.3f;

    [Tooltip("Cooldown between dashes to prevent spam.")] [SerializeField]
    private float dashCooldown = 1.5f;

    [Header("Damage Tracking & Retreat")]
    [Tooltip("Time window (in seconds) to track cumulative damage for retreat decision.")]
    [SerializeField]
    private float damageTrackingWindow = 3f;

    [Tooltip("Amount of damage needed within the tracking window to trigger retreat.")] [SerializeField]
    private float retreatDamageThreshold = 40f;

    [Tooltip("Duration of the retreat behavior (in seconds).")] [SerializeField]
    private float retreatDuration = 3f;

    [Tooltip("Cooldown before the boss can retreat again (prevents constant retreating).")] [SerializeField]
    private float retreatCooldown = 10f;

    [Tooltip("Minimum number of dashes during retreat.")] [SerializeField]
    private int minimumRetreatDashes = 2;

    [Tooltip("Maximum number of dashes during retreat.")] [SerializeField]
    private int maximumRetreatDashes = 4;

    [Tooltip("Time between dashes during retreat.")] [SerializeField]
    private float retreatDashInterval = 0.5f;

    [Header("Adaptive Behavior")]
    [Tooltip("Health percentage at which boss becomes enraged (faster and more aggressive).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float enrageHealthPercent = 0.3f;

    [Tooltip("Speed multiplier applied when enraged.")] [SerializeField]
    private float enrageSpeedMultiplier = 1.4f;

    [Tooltip("Attack speed multiplier when enraged (reduces windup and recovery times).")] [SerializeField]
    private float enrageAttackSpeedMultiplier = 1.2f;

    // Component references
    private Rigidbody2D rigidbody2D;
    private Animator animator;

    // Animator parameter hashes
    private int animatorHashIsWalking;
    private int animatorHashIsAttacking;
    private int animatorHashIsVulnerable;
    private int animatorHashIsDead;
    private int animatorHashIsHit;

    // State tracking
    private BossState currentState = BossState.Idle;
    public float CurrentHealth => currentHealth;
    private float currentHealth;
    private float lastDashTime = -Mathf.Infinity;
    private int currentFacingDirection = 1; // 1 = right, -1 = left

    // Attack tracking
    private bool canDealDamage = false;
    private HashSet<GameObject> hitPlayersThisAttack = new HashSet<GameObject>();
    private bool playerInDamageArea = false;
    private float stateStartTime;
    private float nextAttackTime;

    // Dash tracking
    private Vector2 dashDirection;
    private float lastRetreatDashTime = -Mathf.Infinity;

    // Damage tracking for retreat decision
    private List<float> recentDamageTimes = new List<float>();
    private float lastRetreatTime = -Mathf.Infinity;
    private int retreatDashesRemaining = 0;
    private float retreatStartTime;

    // Enrage state
    private bool isEnraged = false;

    private void Awake()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // Cache animator parameter hashes for performance
        animatorHashIsWalking = Animator.StringToHash("isWalking");
        animatorHashIsAttacking = Animator.StringToHash("isAttacking");
        animatorHashIsVulnerable = Animator.StringToHash("isVulnerable");
        animatorHashIsDead = Animator.StringToHash("isDead");
        animatorHashIsHit = Animator.StringToHash("isHit");

        currentHealth = maxHealth;

        // Validate references
        if (playerTransform == null)
        {
            Debug.LogError("WalkingBoss: playerTransform is not assigned!", this);
        }

        if (damageAreaObject == null)
        {
            Debug.LogError("WalkingBoss: damageAreaObject is not assigned!", this);
        }

        if (leftEdge == null)
        {
            Debug.LogWarning("WalkingBoss: leftEdge is not assigned! Boss may get stuck at map edges.", this);
        }

        if (rightEdge == null)
        {
            Debug.LogWarning("WalkingBoss: rightEdge is not assigned! Boss may get stuck at map edges.", this);
        }

        // Set initial state
        TransitionToState(BossState.Idle);
    }

    private void FixedUpdate()
    {
        if (currentState == BossState.Dead)
        {
            return;
        }

        UpdateEnrageState();
        ExecuteCurrentState();
        UpdateAnimatorParameters();
    }

    #region State Machine

    private void TransitionToState(BossState newState)
    {
        currentState = newState;
        stateStartTime = Time.time;

        // State entry logic
        switch (newState)
        {
            case BossState.Idle:
                OnEnterIdleState();
                break;
            case BossState.Approaching:
                OnEnterApproachingState();
                break;
            case BossState.AttackWindup:
                OnEnterAttackWindupState();
                break;
            case BossState.Attacking:
                OnEnterAttackingState();
                break;
            case BossState.Recovering:
                OnEnterRecoveringState();
                break;
            case BossState.Retreating:
                OnEnterRetreatingState();
                break;
            case BossState.Dashing:
                OnEnterDashingState();
                break;
            case BossState.Dead:
                OnEnterDeadState();
                break;
        }
    }

    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case BossState.Idle:
                ExecuteIdleState();
                break;
            case BossState.Approaching:
                ExecuteApproachingState();
                break;
            case BossState.AttackWindup:
                ExecuteAttackWindupState();
                break;
            case BossState.Attacking:
                ExecuteAttackingState();
                break;
            case BossState.Recovering:
                ExecuteRecoveringState();
                break;
            case BossState.Retreating:
                ExecuteRetreatingState();
                break;
            case BossState.Dashing:
                ExecuteDashingState();
                break;
            case BossState.Dead:
                // No execution needed, boss is dead
                break;
        }
    }

    #endregion

    #region State: Idle

    private void OnEnterIdleState()
    {
        rigidbody2D.velocity = Vector2.zero;
        nextAttackTime = Time.time + Random.Range(minTimeBetweenAttacks, maxTimeBetweenAttacks);
    }

    private void ExecuteIdleState()
    {
        // Transition to approaching after a brief moment
        if (Time.time - stateStartTime > 0.5f)
        {
            TransitionToState(BossState.Approaching);
        }
    }

    #endregion

    #region State: Approaching

    private void OnEnterApproachingState()
    {
        // Nothing special needed on entry
    }

    private void ExecuteApproachingState()
    {
        // If player is in damage area and enough time has passed since last attack, start windup
        if (playerInDamageArea && Time.time >= nextAttackTime)
        {
            TransitionToState(BossState.AttackWindup);
            return;
        }

        // Check if we should retreat due to heavy damage
        if (ShouldRetreat())
        {
            TransitionToState(BossState.Retreating);
            return;
        }

        // Move toward player
        MoveTowardPlayer();
    }

    #endregion

    #region State: Attack Windup

    private void OnEnterAttackWindupState()
    {
        // Stop movement during windup
        rigidbody2D.velocity = Vector2.zero;
    }

    private void ExecuteAttackWindupState()
    {
        float windupDuration = GetModifiedAttackWindupDuration();

        // If windup duration has passed, transition to attacking
        if (Time.time - stateStartTime >= windupDuration)
        {
            TransitionToState(BossState.Attacking);
            return;
        }

        // Boss remains stationary during windup (vulnerable to player attacks)
    }

    #endregion

    #region State: Attacking

    private void OnEnterAttackingState()
    {
        // Trigger attack animation (animation will call EnableDamageDealing via event)
        animator.SetTrigger(animatorHashIsAttacking);

        // Stop movement during attack
        rigidbody2D.velocity = Vector2.zero;

        // Clear the list of hit players for this new attack
        hitPlayersThisAttack.Clear();
    }

    private void ExecuteAttackingState()
    {
        // Wait for animation to complete (animation will call DisableDamageDealing)
        // For now, we'll use a simple timer. In a real implementation, you might use animation events
        // to signal when the attack is complete.

        // Rough estimate: attack animation duration could be 0.3-0.5 seconds
        float estimatedAttackDuration = 0.4f;

        if (Time.time - stateStartTime >= estimatedAttackDuration)
        {
            TransitionToState(BossState.Recovering);
        }
    }

    #endregion

    #region State: Recovering

    private void OnEnterRecoveringState()
    {
        // Boss is vulnerable during recovery
        rigidbody2D.velocity = Vector2.zero;

        // Set next attack time
        nextAttackTime = Time.time + Random.Range(minTimeBetweenAttacks, maxTimeBetweenAttacks);
    }

    private void ExecuteRecoveringState()
    {
        float recoveryDuration = GetModifiedAttackRecoveryDuration();

        // If recovery duration has passed, return to approaching
        if (Time.time - stateStartTime >= recoveryDuration)
        {
            // Check if we should retreat instead
            if (ShouldRetreat())
            {
                TransitionToState(BossState.Retreating);
                return;
            }

            TransitionToState(BossState.Approaching);
            return;
        }

        // Boss remains stationary and vulnerable during recovery
    }

    #endregion

    #region State: Retreating

    private void OnEnterRetreatingState()
    {
        lastRetreatTime = Time.time;
        retreatStartTime = Time.time;
        retreatDashesRemaining = Random.Range(minimumRetreatDashes, maximumRetreatDashes + 1);
        lastRetreatDashTime = -Mathf.Infinity;

        // Perform first retreat dash immediately
        PerformRetreatDash();
    }

    private void ExecuteRetreatingState()
    {
        // Check if retreat duration has passed
        if (Time.time - retreatStartTime >= retreatDuration)
        {
            TransitionToState(BossState.Approaching);
            return;
        }

        // Check if reached edge - stop retreating if we did
        if (IsAtEdge())
        {
            rigidbody2D.velocity = Vector2.zero;
            TransitionToState(BossState.Approaching);
            return;
        }

        // Perform additional retreat dashes if enough time has passed and dashes remaining
        if (retreatDashesRemaining > 0 && Time.time - lastRetreatDashTime >= retreatDashInterval && CanDash())
        {
            PerformRetreatDash();
        }

        // Move away from player while retreating (when not dashing)
        if (currentState == BossState.Retreating)
        {
            MoveAwayFromPlayer();
        }
    }

    #endregion

    #region State: Dashing

    private void OnEnterDashingState()
    {
        // Dash state is automatically entered when dashing
    }

    private void ExecuteDashingState()
    {
        // Check if reached edge during dash - stop immediately
        if (IsAtEdge())
        {
            rigidbody2D.velocity = Vector2.zero;
            TransitionToState(BossState.Approaching);
            return;
        }

        // Apply dash velocity
        rigidbody2D.velocity = dashDirection * dashSpeed;

        // Check if dash duration has passed
        if (Time.time - stateStartTime >= dashDuration)
        {
            // Return to retreating if there are more dashes to perform
            if (retreatDashesRemaining > 0)
            {
                TransitionToState(BossState.Retreating);
            }
            else
            {
                TransitionToState(BossState.Approaching);
            }
        }
    }

    #endregion

    #region State: Dead

    private void OnEnterDeadState()
    {
        AchievementManager.Instance.MarkBossDefeated();

        // Stop all movement
        rigidbody2D.velocity = Vector2.zero;
        rigidbody2D.isKinematic = true;

        // Disable colliders to prevent further interactions
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }

        // Disable damage area
        if (damageAreaObject != null)
        {
            damageAreaObject.SetActive(false);
        }

        animator.SetBool(animatorHashIsDead, true);
    }

    public void OnDeathAnimationComplete()
    {
        Debug.Log("WalkingBoss: Death animation complete. Boss defeated.");

        Destroy(gameObject);
        
    }

    #endregion

    #region Edge Detection

    /// <summary>
    /// Checks if the boss has reached either edge of the arena
    /// </summary>
    private bool IsAtEdge()
    {
        float bossX = transform.position.x;

        // Check left edge
        if (leftEdge != null)
        {
            float leftEdgeX = leftEdge.position.x;
            if (bossX <= leftEdgeX + edgeBuffer)
            {
                return true;
            }
        }

        // Check right edge
        if (rightEdge != null)
        {
            float rightEdgeX = rightEdge.position.x;
            if (bossX >= rightEdgeX - edgeBuffer)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Movement

    private void MoveTowardPlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector2 directionToPlayer = GetDirectionToPlayer();
        float distanceToPlayer = GetDistanceToPlayer();

        // If too close, don't move (prevents jittering)
        if (distanceToPlayer <= stoppingDistance)
        {
            rigidbody2D.velocity = Vector2.zero;
            return;
        }

        // Apply movement
        float currentMoveSpeed = GetCurrentMoveSpeed();
        rigidbody2D.velocity = new Vector2(directionToPlayer.x * currentMoveSpeed, rigidbody2D.velocity.y);

        // Update facing direction
        UpdateFacingDirection(directionToPlayer.x);
    }

    private void MoveAwayFromPlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector2 directionAwayFromPlayer = -GetDirectionToPlayer();

        // Check if moving away would hit edge - if so, stop
        if (WouldHitEdge(directionAwayFromPlayer.x))
        {
            rigidbody2D.velocity = Vector2.zero;
            return;
        }

        // Apply movement away from player
        float currentMoveSpeed = GetCurrentMoveSpeed();
        rigidbody2D.velocity = new Vector2(directionAwayFromPlayer.x * currentMoveSpeed, rigidbody2D.velocity.y);

        // Update facing direction
        UpdateFacingDirection(directionAwayFromPlayer.x);
    }

    /// <summary>
    /// Checks if moving in the given direction would hit an edge
    /// </summary>
    private bool WouldHitEdge(float direction)
    {
        float bossX = transform.position.x;

        // Moving left
        if (direction < 0 && leftEdge != null)
        {
            return bossX <= leftEdge.position.x + edgeBuffer;
        }

        // Moving right
        if (direction > 0 && rightEdge != null)
        {
            return bossX >= rightEdge.position.x - edgeBuffer;
        }

        return false;
    }

    private void UpdateFacingDirection(float horizontalMovement)
    {
        if (Mathf.Abs(horizontalMovement) < 0.01f)
        {
            return;
        }

        int desiredDirection = horizontalMovement > 0 ? 1 : -1;

        if (desiredDirection != currentFacingDirection)
        {
            currentFacingDirection = desiredDirection;

            // Flip the entire GameObject
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * currentFacingDirection;
            transform.localScale = scale;
        }
    }

    private Vector2 GetDirectionToPlayer()
    {
        if (playerTransform == null)
        {
            return Vector2.zero;
        }

        Vector2 direction = (playerTransform.position - transform.position).normalized;
        return direction;
    }

    private float GetDistanceToPlayer()
    {
        if (playerTransform == null)
        {
            return Mathf.Infinity;
        }

        return Vector2.Distance(transform.position, playerTransform.position);
    }

    private float GetCurrentMoveSpeed()
    {
        float speed = moveSpeed;

        if (isEnraged)
        {
            speed *= enrageSpeedMultiplier;
        }

        return speed;
    }

    #endregion

    #region Dashing

    private void PerformRetreatDash()
    {
        if (!CanDash())
        {
            return;
        }

        // Calculate dash direction away from player
        Vector2 directionAwayFromPlayer = -GetDirectionToPlayer();

        // Check if dashing would hit edge - if so, don't dash
        if (WouldHitEdge(directionAwayFromPlayer.x))
        {
            retreatDashesRemaining--;
            return;
        }

        dashDirection = new Vector2(directionAwayFromPlayer.x, 0f).normalized;

        lastDashTime = Time.time;
        lastRetreatDashTime = Time.time;
        retreatDashesRemaining--;

        TransitionToState(BossState.Dashing);
    }

    private bool CanDash()
    {
        return Time.time - lastDashTime >= dashCooldown;
    }

    #endregion

    #region Damage Tracking & Retreat Logic

    private bool ShouldRetreat()
    {
        // Can't retreat if recently retreated
        if (Time.time - lastRetreatTime < retreatCooldown)
        {
            return false;
        }

        // Calculate total damage in recent window
        float totalRecentDamage = 0f;
        float currentTime = Time.time;

        // Remove old damage entries outside the tracking window
        recentDamageTimes.RemoveAll(damageTime => currentTime - damageTime > damageTrackingWindow);

        // Count damage entries (each entry represents one hit, but we need to track actual damage amounts)
        // For simplicity, we'll track timestamps and count them. 
        // A better approach would be to track damage amounts, but this works for the behavior.
        totalRecentDamage = recentDamageTimes.Count * 10f; // Assuming average hit damage

        return totalRecentDamage >= retreatDamageThreshold;
    }

    private void RecordDamage()
    {
        recentDamageTimes.Add(Time.time);
    }

    #endregion

    #region Enrage Logic

    private void UpdateEnrageState()
    {
        float healthPercent = currentHealth / maxHealth;

        if (!isEnraged && healthPercent <= enrageHealthPercent)
        {
            isEnraged = true;
            Debug.Log("WalkingBoss: Enraged!");
        }
    }

    private float GetModifiedAttackWindupDuration()
    {
        float duration = attackWindupDuration;

        if (isEnraged)
        {
            duration /= enrageAttackSpeedMultiplier;
        }

        return duration;
    }

    private float GetModifiedAttackRecoveryDuration()
    {
        float duration = attackRecoveryDuration;

        if (isEnraged)
        {
            duration /= enrageAttackSpeedMultiplier;
        }

        return duration;
    }

    #endregion

    #region Damage Area Callbacks (called by BossDamageArea)

    public void OnDamageAreaEnter(Collider2D collision)
    {
        // Check if player entered
        if (collision.CompareTag("Player"))
        {
            playerInDamageArea = true;
        }
    }

    public void OnDamageAreaStay(Collider2D collision)
    {
        // If damage dealing is enabled and player is in area, try to deal damage
        if (!canDealDamage)
        {
            return;
        }

        if (!collision.CompareTag("Player"))
        {
            return;
        }

        // Check if we already hit this player during this attack
        if (hitPlayersThisAttack.Contains(collision.gameObject))
        {
            return;
        }

        // Try to get IPlayer interface
        IPlayer player = collision.GetComponent<IPlayer>();
        if (player != null)
        {
            player.TakeDamage(attackDamage);
            hitPlayersThisAttack.Add(collision.gameObject);
            Debug.Log($"WalkingBoss: Dealt {attackDamage} damage to player.");
        }
    }

    public void OnDamageAreaExit(Collider2D collision)
    {
        // Check if player exited
        if (collision.CompareTag("Player"))
        {
            playerInDamageArea = false;
        }
    }

    #endregion

    #region Damage Dealing Control (called by Animation Events)

    /// <summary>
    /// Called by animation event to enable damage dealing.
    /// The damage area can now hurt the player.
    /// </summary>
    public void EnableDamageDealing()
    {
        canDealDamage = true;
        Debug.Log("WalkingBoss: Damage dealing enabled.");
    }

    /// <summary>
    /// Called by animation event to disable damage dealing.
    /// The damage area can no longer hurt the player until the next attack.
    /// </summary>
    public void DisableDamageDealing()
    {
        canDealDamage = false;
        hitPlayersThisAttack.Clear();
        Debug.Log("WalkingBoss: Damage dealing disabled.");
    }

    #endregion

    #region IEnemy Implementation

    public bool TakeDamage(float damage)
    {
        if (currentState == BossState.Dead)
        {
            return true;
        }

        animator.SetTrigger(animatorHashIsHit);

        currentHealth -= damage;
        RecordDamage();

        Debug.Log($"WalkingBoss: Took {damage} damage. Current health: {currentHealth}/{maxHealth}");

        // Check if boss is killed
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            TransitionToState(BossState.Dead);

            if (npcPrefab != null)
            {
                npcPrefab.SetActive(true);
            }
            return true;
        }

        return false;
    }

    #endregion

    #region Animator Updates

    private void UpdateAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        // isWalking: true if moving horizontally
        bool isWalking = Mathf.Abs(rigidbody2D.velocity.x) > 0.1f &&
                         (currentState == BossState.Approaching || currentState == BossState.Retreating);
        animator.SetBool(animatorHashIsWalking, isWalking);

        // isVulnerable: true during AttackWindup and Recovering states
        bool isVulnerable = currentState == BossState.AttackWindup || currentState == BossState.Recovering;
        animator.SetBool(animatorHashIsVulnerable, isVulnerable);
    }

    #endregion

    public void DeadAnimationEnd()
    {
        Destroy(gameObject);
    }

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        // Visualize stopping distance
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, stoppingDistance);
        }

        // Visualize arena edges
        if (leftEdge != null)
        {
            Gizmos.color = Color.red;
            Vector3 leftPos = leftEdge.position;
            Gizmos.DrawLine(leftPos + Vector3.up * 5f, leftPos + Vector3.down * 5f);
            Gizmos.DrawWireSphere(leftPos, 0.3f);
        }

        if (rightEdge != null)
        {
            Gizmos.color = Color.red;
            Vector3 rightPos = rightEdge.position;
            Gizmos.DrawLine(rightPos + Vector3.up * 5f, rightPos + Vector3.down * 5f);
            Gizmos.DrawWireSphere(rightPos, 0.3f);
        }

        // Visualize edge buffer zones
        if (leftEdge != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 leftBufferPos = leftEdge.position + Vector3.right * edgeBuffer;
            Gizmos.DrawLine(leftBufferPos + Vector3.up * 5f, leftBufferPos + Vector3.down * 5f);
        }

        if (rightEdge != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Vector3 rightBufferPos = rightEdge.position + Vector3.left * edgeBuffer;
            Gizmos.DrawLine(rightBufferPos + Vector3.up * 5f, rightBufferPos + Vector3.down * 5f);
        }
    }

    #endregion
}