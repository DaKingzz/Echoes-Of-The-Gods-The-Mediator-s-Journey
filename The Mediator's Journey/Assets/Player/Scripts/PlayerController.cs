using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
public class PlayerController : MonoBehaviour, IPlayer
{
    #region Health

    [Header("Health Settings")]
    [Tooltip("Maximum player health. CurrentHealth will be clamped between 0 and this value.")]
    [SerializeField]
    private float maximumHealth = 10f;

    private float currentHealth;

    public event Action<float> OnTookDamage;
    public event Action OnDied;

    #endregion

    #region Movement Configuration

    [Header("Movement Settings")] [Tooltip("Horizontal movement speed in world units per second.")] [SerializeField]
    private float movementSpeed = 8f;

    [Tooltip("Multiplier applied to movementSpeed while the run input is held.")] [SerializeField]
    private float runSpeedMultiplier = 1.5f;

    #endregion

    #region Jump Configuration (variable jump: small and big jumps)

    [Header("Jump Settings")]
    [Tooltip(
        "Initial vertical velocity applied at the instant of jump (world units/second). This is the jump 'burst'.")]
    [SerializeField]
    private float initialJumpVelocity = 10f;

    [Tooltip("Total additional upward force applied while the jump input is held (world units/second^2).")]
    [SerializeField]
    private float sustainedJumpAcceleration = 50f;

    [Tooltip("Maximum duration in seconds during which sustained jump acceleration may be applied after jump start.")]
    [SerializeField]
    private float sustainedJumpMaximumDuration = 2f;

    #endregion

    #region Gravity Configuration

    [Header("Gravity Settings")] [Tooltip("Base gravity scale to apply to the Rigidbody2D at start.")] [SerializeField]
    private float baseGravityScale = 3f;

    [Tooltip("Multiplier applied to gravity when the player is falling (makes fall faster).")] [SerializeField]
    private float fallGravityMultiplier = 2f;

    #endregion

    #region Grounded-from-Velocity Configuration

    [Header("Grounded From Velocity")]
    [Tooltip(
        "If the absolute vertical velocity is less than or equal to this threshold, the player is considered grounded.")]
    [SerializeField]
    private float groundedVerticalVelocityThreshold = 0.05f;

    [Tooltip(
        "Minimum time in seconds the vertical velocity must remain below the threshold to be treated as grounded. Helps avoid flicker on very fast physics steps.")]
    [SerializeField]
    private float groundedStabilityTime = 0.02f;

    #endregion

    #region Components and Animator Hashes

    private Rigidbody2D rigidBody2D;
    private Collider2D collider2D;
    private Animator animator;

    private readonly int animatorHashIsWalking = Animator.StringToHash("isWalking");
    private readonly int animatorHashIsJumpingForward = Animator.StringToHash("isJumpingForward");
    private readonly int animatorHashIsJumpingRight = Animator.StringToHash("isJumpingRight");

    #endregion

    #region Input State

    // Movement input vector captured by Input System
    private Vector2 movementInput = Vector2.zero;

    // Run state captured by Input System (held or not)
    private bool runInputHeld;

    // Jump state captured by Input System:
    // - jumpInputHeld: true while button is held
    // - jumpPressedThisFrame: becomes true when performed and is consumed in FixedUpdate
    private bool jumpInputHeld;
    private bool jumpPressedThisFrame;

    #endregion

    #region Runtime State

    private bool isFacingRight = true;
    private bool isGrounded;
    private float timeWhenVerticalBelowThreshold = -Mathf.Infinity;
    private float timeWhenJumpStarted = -Mathf.Infinity;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        collider2D = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        rigidBody2D.gravityScale = baseGravityScale;
        currentHealth = Mathf.Clamp(maximumHealth, 0f, maximumHealth);
    }

    private void Update()
    {
        // Non-physics visual updates (animator)
        UpdateAnimatorVisuals();
    }

    private void FixedUpdate()
    {
        // Determine grounded state using vertical velocity
        RefreshGroundedStateFromVelocity();

        // Compute desired velocity locally and assign once at the end
        Vector2 computedVelocity = rigidBody2D.velocity;

        // Horizontal movement (apply run multiplier if held)
        float horizontalSpeed = movementInput.x * (runInputHeld ? movementSpeed * runSpeedMultiplier : movementSpeed);
        computedVelocity.x = horizontalSpeed;

        // Jump start: consume the pressed flag and only allow jumps when considered grounded (no coyote time)
        if (jumpPressedThisFrame && isGrounded)
        {
            computedVelocity.y = initialJumpVelocity;
            timeWhenJumpStarted = Time.fixedTime;
            // Mark jump pressed consumed so it doesn't trigger again
            jumpPressedThisFrame = false;
        }
        else
        {
            // Ensure we do not keep a stale pressed flag if not consumed this frame
            jumpPressedThisFrame = false;
        }

        // Sustained jump while holding: variable jump height mechanic
        ApplySustainedJump(ref computedVelocity);

        // Extra gravity while falling to make descents snappier
        ApplyExtraFallGravity(ref computedVelocity);

        // Commit velocity exactly once
        rigidBody2D.velocity = computedVelocity;
    }

    #endregion

    #region Grounded-from-Velocity Logic

    /// <summary>
    /// Uses the Rigidbody2D vertical velocity to determine if the player is grounded.
    /// The player is treated as grounded when the absolute vertical velocity stays below a small threshold
    /// for groundedStabilityTime seconds to avoid flicker from physics steps.
    /// </summary>
    private void RefreshGroundedStateFromVelocity()
    {
        float absoluteVerticalSpeed = Mathf.Abs(rigidBody2D.velocity.y);

        if (absoluteVerticalSpeed <= groundedVerticalVelocityThreshold)
        {
            // record when vertical velocity dropped below threshold
            if (timeWhenVerticalBelowThreshold == -Mathf.Infinity)
            {
                timeWhenVerticalBelowThreshold = Time.fixedTime;
            }

            // require stability time to consider grounded to prevent flicker
            if (Time.fixedTime - timeWhenVerticalBelowThreshold >= groundedStabilityTime)
            {
                isGrounded = true;
            }
            else
            {
                isGrounded = false;
            }
        }
        else
        {
            // reset timer when vertical speed goes above threshold
            timeWhenVerticalBelowThreshold = -Mathf.Infinity;
            isGrounded = false;
        }
    }

    #endregion

    #region Jump Helpers (variable jump implementation)

    /// <summary>
    /// Applies sustained upward acceleration for a limited duration while the jump input is held.
    /// This is the variable jump mechanic: a quick tap produces a small jump (only the initial burst),
    /// holding the button applies extra upward acceleration for up to sustainedJumpMaximumDuration to create a taller jump.
    /// </summary>
    private void ApplySustainedJump(ref Vector2 velocity)
    {
        // Must be holding jump and be airborne for sustained effect
        if (!jumpInputHeld) return;
        if (isGrounded) return;

        float timeSinceJumpStart = Time.fixedTime - timeWhenJumpStarted;
        if (timeSinceJumpStart <= sustainedJumpMaximumDuration)
        {
            // Diminishing hold factor (1 -> 0 across sustain duration) to make hold feel natural
            float holdFactor = 1f - (timeSinceJumpStart / sustainedJumpMaximumDuration);
            float accelerationThisFrame = sustainedJumpAcceleration * holdFactor;

            // Apply acceleration as a velocity delta this frame
            velocity.y += accelerationThisFrame * Time.fixedDeltaTime;

            // Prevent runaway upward velocity from stacking too much
            float maximumAllowedUpwardVelocity =
                initialJumpVelocity + sustainedJumpAcceleration * sustainedJumpMaximumDuration * 0.9f;
            if (velocity.y > maximumAllowedUpwardVelocity)
            {
                velocity.y = maximumAllowedUpwardVelocity;
            }
        }
    }

    #endregion

    #region Gravity Helpers

    /// <summary>
    /// Applies extra gravity while falling to make descent snappier.
    /// </summary>
    private void ApplyExtraFallGravity(ref Vector2 velocity)
    {
        if (velocity.y < 0f)
        {
            float extraGravityThisFrame = Physics2D.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
            velocity.y += extraGravityThisFrame;
        }
    }

    #endregion

    #region Animator Management

    /// <summary>
    /// Updates animator parameters used purely for visuals.
    /// </summary>
    private void UpdateAnimatorVisuals()
    {
        if (animator == null) return;

        animator.SetBool(animatorHashIsWalking, Mathf.Abs(movementInput.x) > 0.01f && isGrounded);

        if (!isGrounded)
        {
            if (rigidBody2D.velocity.y > 0f)
            {
                bool movingHorizontally = Mathf.Abs(movementInput.x) > 0.01f;
                animator.SetBool(animatorHashIsJumpingRight, movingHorizontally);
                animator.SetBool(animatorHashIsJumpingForward, !movingHorizontally);
            }
            else
            {
                animator.SetBool(animatorHashIsJumpingRight, false);
                animator.SetBool(animatorHashIsJumpingForward, false);
            }
        }
        else
        {
            animator.SetBool(animatorHashIsJumpingRight, false);
            animator.SetBool(animatorHashIsJumpingForward, false);
        }
    }

    #endregion

    #region Input Callbacks

    /// <summary>
    /// Movement Input System callback. Captures movement vector and flips facing when direction changes.
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        movementInput = context.ReadValue<Vector2>();

        // Flip visual facing when horizontal direction changes
        if (movementInput.x > 0f && !isFacingRight)
        {
            FlipFacingDirection();
        }
        else if (movementInput.x < 0f && isFacingRight)
        {
            FlipFacingDirection();
        }
    }

    /// <summary>
    /// Jump Input System callback.
    /// - performed: registers a press (consumed in FixedUpdate) and marks held
    /// - canceled: marks release so sustained jump stops immediately
    /// </summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // record press for consumption at the next FixedUpdate (physics step)
            jumpPressedThisFrame = true;
            jumpInputHeld = true;
        }
        else if (context.canceled)
        {
            jumpInputHeld = false;
            // When released, we stop sustained jump naturally because jumpInputHeld is false
        }
    }

    /// <summary>
    /// Run Input System callback. Performed sets held to true; canceled sets held to false.
    /// </summary>
    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.performed) runInputHeld = true;
        else if (context.canceled) runInputHeld = false;
    }

    #endregion

    #region Facing / Visual Flip

    /// <summary>
    /// Flip the player's visual facing direction by negating the localScale.x.
    /// If negative scales cause issues with child transforms or physics, consider switching to SpriteRenderer.flipX.
    /// </summary>
    private void FlipFacingDirection()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    #endregion

    #region Health and Damage

    /// <summary>
    /// Applies damage to the player. Clamps health and triggers death when health reaches zero.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);

        OnTookDamage?.Invoke(damage);

        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    /// <summary>
    /// Handles death: fires death event then destroys the GameObject.
    /// </summary>
    private void HandleDeath()
    {
        OnDied?.Invoke();
        Destroy(gameObject);
    }

    #endregion

    #region Editor Debugging

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw a small line indicating the grounded vertical velocity threshold (visual aid)
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position;
        Gizmos.DrawLine(origin + Vector3.left * 0.5f, origin + Vector3.right * 0.5f);
    }
#endif

    #endregion
}