using System;
using System.Collections.Generic;
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

    public float MaximumHealth => maximumHealth;

    private float currentHealth;

    public float CurrentHealth
    {
        get => currentHealth;
        private set => currentHealth = Mathf.Max(0f, value);
    }

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

    #region Ground Check (OverlapCircle)

    [Header("Ground Check (OverlapCircle)")]
    [Tooltip("Transform used as the center of the ground check.")]
    [SerializeField]
    private Transform groundCheckPoint;

    [Tooltip("Radius of the ground check circle.")] [SerializeField]
    private float groundCheckRadius = 0.12f;

    [Tooltip("LayerMask used to detect ground.")] [SerializeField]
    private LayerMask groundLayerMask = ~0;

    [Header("Ground timing tweaks")]
    [Tooltip("Ignore ground checks for this many seconds immediately after jumping to avoid false landings.")]
    [SerializeField]
    private float jumpGroundIgnoreTime = 0.06f;

    #endregion

    #region Landing / Animation lock

    [Header("Jump animation lock")]
    [Tooltip(
        "Time (seconds) to wait before allowing hasLanded to fire after a jump start. Set this to your jump animation length.")]
    [SerializeField]
    private float jumpAnimationLockTime = 0.0f;

    // fixed-time until which land trigger is ignored
    private float landingIgnoreUntilFixed = -Mathf.Infinity;

    #endregion

    #region Audio

    [Header("Audio Sources")]
    [Tooltip("AudioSource used for one-shot SFX like jump. Prefer non-looping AudioSource.")]
    [SerializeField]
    private AudioSource sfxSource;

    [Tooltip(
        "AudioSource used for looped walking/footstep sound. Prefer looping AudioSource or leave empty to let script manage it.")]
    [SerializeField]
    private AudioSource footstepsSource;

    [Tooltip("Audio source for sword attack sounds")] [SerializeField]
    private AudioSource SwordAttackAudioSource;

    [Header("Audio Volumes (optional overrides)")] [Range(0f, 1f)] [SerializeField]
    private float jumpSoundVolume = 1f;

    [Range(0f, 1f)] [SerializeField] private float walkSoundVolume = 1f;

    [Header("Walking Sound Settings")]
    [Tooltip("Minimum horizontal input magnitude to consider the player 'walking'.")]
    [SerializeField]
    private float walkThreshold = 0.01f;

    #endregion

    #region Attack Configuration (immediate sweep on attack)

    [Header("Attack Settings")]
    [Tooltip("Child BoxCollider2D used only for defining sweep size/position. Is Trigger flag is ignored here.")]
    [SerializeField]
    private Collider2D damageArea; // optional: used to compute overlap box size/center; can be left null

    [Tooltip("Damage applied to enemies hit by the attack.")] [SerializeField]
    private float attackDamage = 2f;

    [Tooltip("LayerMask used to filter enemies for hit detection.")] [SerializeField]
    private LayerMask enemyLayerMask = default;

    [Tooltip("If true, only first target per sweep is damaged.")] [SerializeField]
    private bool stopAfterFirstHit = false;

    [Header("Attack Cooldown")] [Tooltip("Minimum time in seconds between player attacks.")] [SerializeField]
    private float attackCooldown = 0.35f;

    // temporary per-sweep set (prevents duplicate hits in same sweep)
    private readonly HashSet<Collider2D> hitsThisSweep = new HashSet<Collider2D>();

    // animator trigger hash for attack
    private readonly int animatorHashAttack = Animator.StringToHash("isAttacking");

    // last attack timestamp
    private float lastAttackTime = -Mathf.Infinity;

    #endregion

    #region Components and Animator Hashes

    private Rigidbody2D rigidBody2D;
    private Collider2D collider2D;
    private Animator animator;

    private readonly int animatorHashIsWalking = Animator.StringToHash("isWalking");
    private readonly int animatorHashIsJumpingForward = Animator.StringToHash("isJumpingForward");
    private readonly int animatorHashIsJumpingRight = Animator.StringToHash("isJumpingRight");
    private readonly int animatorHashIsJumping = Animator.StringToHash("isJumping");

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
    private float timeWhenJumpStarted = -Mathf.Infinity;

    // walking audio state
    private bool wasWalkingPlaying = false;

    // track whether we are in a jump phase (set when a jump starts, cleared when grounded after leaving)
    private bool isInJumpPhase = false;

    // last non-zero horizontal input sign while airborne: -1 = left, 0 = none, +1 = right
    private int lastAirMovementSign = 0;

    // whether an airborne-forward state is currently active (true means in-air pose is left/right)
    private bool airborneForwardActive = false;

    // once true (set when airborneForwardActive is exited by releasing input), player cannot re-enter airborneForwardActive until landing.
    // pressing the opposite direction still allowed and will set airborneForwardActive = true again.
    private bool preventReenterAirForwardUntilLand = false;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        // ensure at least one AudioSource exists so designers can wire two if desired
        if (GetComponents<AudioSource>().Length == 0)
            gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        collider2D = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        // Auto-resolve audio sources if the designer didn't assign them
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sfxSource == null)
        {
            if (sources.Length >= 1) sfxSource = sources[0];
            else sfxSource = gameObject.AddComponent<AudioSource>();
        }

        if (footstepsSource == null)
        {
            if (sources.Length >= 2) footstepsSource = sources[1];
            else if (sources.Length == 1) footstepsSource = gameObject.AddComponent<AudioSource>();
            else footstepsSource = gameObject.AddComponent<AudioSource>();
        }

        // sensible defaults (do not override designer choices unnecessarily)
        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        if (footstepsSource != null)
        {
            footstepsSource.playOnAwake = false;
            // Keep footstepsSource.loop as configured by designer; script will ensure loop when playing
        }

        rigidBody2D.gravityScale = baseGravityScale;
        currentHealth = Mathf.Clamp(maximumHealth, 0f, maximumHealth);

        // initialize lastFrameGrounded state to avoid false landing on start
        lastFrameGrounded = CheckGroundImmediate();
    }

    private void Update()
    {
        // Non-physics visual updates (animator)
        UpdateAnimatorVisuals();
    }

    private void FixedUpdate()
    {
        // Determine grounded state using overlap circle (with short ignore window after jumping)
        RefreshGroundedStateFromOverlap();

        // Compute desired velocity locally and assign once at the end
        Vector2 computedVelocity = rigidBody2D.velocity;

        // Horizontal movement (apply run multiplier if held)
        float horizontalSpeed = movementInput.x * (runInputHeld ? movementSpeed * runSpeedMultiplier : movementSpeed);
        computedVelocity.x = horizontalSpeed;

        // Jump start: consume the pressed flag and only allow jumps when grounded
        if (jumpPressedThisFrame && isGrounded)
        {
            computedVelocity.y = initialJumpVelocity;
            timeWhenJumpStarted = Time.fixedTime;
            isInJumpPhase = true;

            // when jump begins, initialize airborne direction fields from current input if any
            InitializeAirborneDirectionOnJump();

            // start landing ignore window (fixed-time)
            landingIgnoreUntilFixed = Time.fixedTime + jumpAnimationLockTime;

            // ensure previous-grounded false so transition requires real leave+return
            lastFrameGrounded = false;

            // play jump SFX using sfxSource: prefer PlayOneShot if clip assigned, otherwise Play()
            if (sfxSource != null)
            {
                if (sfxSource.clip != null)
                    sfxSource.PlayOneShot(sfxSource.clip, jumpSoundVolume);
                else
                    sfxSource.Play();
            }

            // consume the press
            jumpPressedThisFrame = false;
        }
        else
        {
            // ensure we don't keep stale pressed flag
            jumpPressedThisFrame = false;
        }

        // While airborne, handle input-driven direction changes using the air-direction rules
        if (isInJumpPhase)
        {
            HandleAirborneDirectionRules();
        }

        // Sustained jump while holding: variable jump height mechanic
        ApplySustainedJump(ref computedVelocity);

        // Extra gravity while falling to make descents snappier
        ApplyExtraFallGravity(ref computedVelocity);

        // Commit velocity exactly once
        rigidBody2D.velocity = computedVelocity;

        // Update walking sound (based on grounded and horizontal input)
        bool isWalkingNow = Mathf.Abs(movementInput.x) > walkThreshold && isGrounded;
        UpdateWalkingSound(isWalkingNow);
    }

    #endregion

    #region Ground Check (OverlapCircle) Implementation

    private bool CheckGroundImmediate()
    {
        if (groundCheckPoint == null)
        {
            Vector2 origin = (Vector2)transform.position;
            float checkDist = 0.1f;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDist, groundLayerMask);
            return hit.collider != null;
        }
        else
        {
            return Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayerMask) != null;
        }
    }

    private bool lastFrameGrounded = false;

    private void RefreshGroundedStateFromOverlap()
    {
        bool prevGrounded = lastFrameGrounded;

        if (groundCheckPoint == null)
        {
            Vector2 origin = (Vector2)transform.position;
            float checkDist = 0.1f;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, checkDist, groundLayerMask);
            isGrounded = hit.collider != null;
        }
        else
        {
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayerMask);
        }

        // If we just started a jump, ignore ground checks for a very short window to avoid false positives
        if (isInJumpPhase && (Time.fixedTime - timeWhenJumpStarted) <= jumpGroundIgnoreTime)
        {
            isGrounded = false;
        }

        // Landing detection: only fire when we were NOT grounded previously and are now grounded,
        // the landing-ignore timer (fixed) has expired, and we actually started a jump.
        if (isGrounded && !prevGrounded && isInJumpPhase)
        {
            if (Time.fixedTime >= landingIgnoreUntilFixed)
            {
                // actual landing: clear jump phase and fire animator
                isInJumpPhase = false;
                airborneForwardActive = false;
                preventReenterAirForwardUntilLand = false;
                lastAirMovementSign = 0;

                if (animator != null)
                {
                    animator.SetBool(animatorHashIsJumping, false);
                    animator.SetBool(animatorHashIsJumpingForward, false);
                    animator.SetBool(animatorHashIsJumpingRight, false);
                }
            }
            // else still locked by animation timer: do nothing now
        }

        // store for next frame
        lastFrameGrounded = isGrounded;
    }

    #endregion

    #region Airborne direction rules (core)

    // Called at the instant of jump to initialize airborne direction state from input (if any).
    private void InitializeAirborneDirectionOnJump()
    {
        float x = movementInput.x;
        if (Mathf.Abs(x) > 0.01f)
        {
            // player jumped while holding left/right -> enter airborne-forward state
            lastAirMovementSign = x > 0f ? 1 : -1;
            airborneForwardActive = true;
            preventReenterAirForwardUntilLand = false;
        }
        else
        {
            // jumped from standing/no-horiz input -> no airborne-forward active
            lastAirMovementSign = 0;
            airborneForwardActive = false;
            preventReenterAirForwardUntilLand = false;
        }

        // Set generic jump animator flag
        if (animator != null)
            animator.SetBool(animatorHashIsJumping, true);
    }

    // Called while isInJumpPhase each FixedUpdate to enforce the rules:
    // - pressing L/R while airborne sets or changes airborne-forward.
    // - releasing input while airborne exits airborne-forward and prevents reentry until landing.
    private void HandleAirborneDirectionRules()
    {
        float x = movementInput.x;
        int inputSign = 0;
        if (Mathf.Abs(x) > 0.01f) inputSign = x > 0f ? 1 : -1;

        if (inputSign != 0)
        {
            // player is pressing left or right while airborne
            if (!airborneForwardActive)
            {
                // allow entering forward state only if not prevented OR if changing direction relative to remembered sign
                if (!preventReenterAirForwardUntilLand)
                {
                    lastAirMovementSign = inputSign;
                    airborneForwardActive = true;
                    preventReenterAirForwardUntilLand = false;
                }
                else
                {
                    // prevented: only allow if it's an actual direction change relative to lastAirMovementSign
                    if (lastAirMovementSign != 0 && inputSign != lastAirMovementSign)
                    {
                        lastAirMovementSign = inputSign;
                        airborneForwardActive = true;
                        preventReenterAirForwardUntilLand = false;
                    }
                    // else ignore
                }
            }
            else
            {
                // active already: allow immediate change if opposite pressed
                if (inputSign != lastAirMovementSign)
                {
                    lastAirMovementSign = inputSign;
                    airborneForwardActive = true;
                    preventReenterAirForwardUntilLand = false;
                }
            }
        }
        else
        {
            // input released while airborne
            if (airborneForwardActive)
            {
                // exit forward state and prevent re-entry of same direction until land
                airborneForwardActive = false;
                preventReenterAirForwardUntilLand = true;
                // keep lastAirMovementSign so visuals can "stick"
            }
        }
    }

    #endregion

    #region Jump Helpers (variable jump implementation)

    /// <summary>
    /// Applies sustained upward acceleration for a limited duration while the jump input is held.
    /// </summary>
    private void ApplySustainedJump(ref Vector2 velocity)
    {
        // Must be holding jump and be airborne for sustained effect
        if (!jumpInputHeld) return;
        if (isGrounded) return;

        float timeSinceJumpStart = Time.fixedTime - timeWhenJumpStarted;
        if (timeSinceJumpStart <= sustainedJumpMaximumDuration)
        {
            float holdFactor = 1f - (timeSinceJumpStart / sustainedJumpMaximumDuration);
            float accelerationThisFrame = sustainedJumpAcceleration * holdFactor;

            // Apply acceleration as a velocity delta this frame
            velocity.y += accelerationThisFrame * Time.fixedDeltaTime;

            // Prevent runaway upward velocity from stacking too much
            float maximumAllowedUpwardVelocity =
                initialJumpVelocity + sustainedJumpAcceleration * sustainedJumpMaximumDuration * 0.9f;
            if (velocity.y > maximumAllowedUpwardVelocity)
                velocity.y = maximumAllowedUpwardVelocity;
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
    /// This version uses lastAirMovementSign as the source of the visual "stuck" direction while airborne.
    /// Releasing input does not change visuals; only landing clears the remembered direction.
    /// </summary>
    private void UpdateAnimatorVisuals()
    {
        if (animator == null) return;

        // walking only on ground
        animator.SetBool(animatorHashIsWalking, Mathf.Abs(movementInput.x) > 0.01f && isGrounded);

        // Airborne visuals: stick to lastAirMovementSign if present.
        if (isInJumpPhase)
        {
            if (lastAirMovementSign != 0)
            {
                // show horizontal-air pose matching the remembered direction
                animator.SetBool(animatorHashIsJumpingRight, true);
                animator.SetBool(animatorHashIsJumpingForward, false);
            }
            else
            {
                // no remembered horizontal direction: show forward (non-horizontal) jump pose
                animator.SetBool(animatorHashIsJumpingRight, false);
                animator.SetBool(animatorHashIsJumpingForward, true);
            }

            animator.SetBool(animatorHashIsJumping, true);
        }
        else
        {
            // on ground: clear airborne flags
            animator.SetBool(animatorHashIsJumpingRight, false);
            animator.SetBool(animatorHashIsJumpingForward, false);
            animator.SetBool(animatorHashIsJumping, false);
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

        // Immediate visual facing flip when pressing direction
        if (movementInput.x > 0f && !isFacingRight) FlipFacingDirection();
        else if (movementInput.x < 0f && isFacingRight) FlipFacingDirection();
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

    /// <summary>
    /// Attack Input callback. Performs the animator trigger and immediately performs a sweep to damage enemies.
    /// When attacking in-air we re-apply the remembered airborne direction so the attack doesn't force idle.
    /// </summary>
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || animator == null) return;

        // enforce cooldown
        if (Time.time - lastAttackTime < attackCooldown) return;

        // record attack time
        lastAttackTime = Time.time;

        // trigger animator and perform sweep / sound
        animator.SetTrigger(animatorHashAttack);

        // If attacking while airborne, re-apply the remembered visual direction so animation returns to it.
        if (isInJumpPhase && lastAirMovementSign != 0)
        {
            animator.SetBool(animatorHashIsJumpingRight, true);
            animator.SetBool(animatorHashIsJumpingForward, false);
        }

        if (SwordAttackAudioSource != null)
            SwordAttackAudioSource.Play();

        DoAttackSweep();
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

    #region Walking Sound Helper

    private void UpdateWalkingSound(bool shouldPlay)
    {
        if (footstepsSource == null) return;

        // keep volume in sync with inspector override
        footstepsSource.volume = walkSoundVolume;

        if (shouldPlay)
        {
            if (!wasWalkingPlaying)
            {
                footstepsSource.loop = true;
                footstepsSource.Play();
                wasWalkingPlaying = true;
            }
        }
        else
        {
            if (wasWalkingPlaying)
            {
                footstepsSource.Stop();
                wasWalkingPlaying = false;
            }
        }
    }

    #endregion

    #region Attack Sweep (immediate damage on input)

    /// <summary>
    /// Performs an instantaneous physics overlap (box) using the damageArea's transform/size if available,
    /// otherwise logs an error. Applies damage immediately to IEnemy targets found.
    /// </summary>
    private void DoAttackSweep()
    {
        hitsThisSweep.Clear();

        // Determine centre, size and angle for the overlap box
        Vector2 centre;
        Vector2 size;
        float angle = 0f;

        if (damageArea is BoxCollider2D box)
        {
            size = Vector2.Scale(box.size, box.transform.lossyScale);
            centre = (Vector2)box.transform.position + box.offset;
            angle = box.transform.eulerAngles.z;
        }
        else
        {
            Debug.LogError(
                "PlayerController.DoAttackSweep: damageArea is not assigned or not a BoxCollider2D. Using default small box in front of player.");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(centre, size, angle, enemyLayerMask);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hitsThisSweep.Contains(hit)) continue;
            hitsThisSweep.Add(hit);

            var damageable = hit.GetComponent<IEnemy>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage);
            }

            if (stopAfterFirstHit) break;
        }
    }

    #endregion

    #region Health and Damage

    /// <summary>
    /// Applies damage to the player. Clamps health and triggers death when health reaches zero.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, maximumHealth);

        OnTookDamage?.Invoke(damage);

        if (CurrentHealth <= 0f)
            HandleDeath();
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

        // Draw damageArea bounds if assigned and visible in editor
        if (damageArea is BoxCollider2D box)
        {
            Gizmos.color = Color.red;
            Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
            Vector3 pos = box.transform.position + (Vector3)box.offset;
            Gizmos.DrawWireCube(pos, new Vector3(size.x, size.y, 0f));
        }

        // draw ground check
        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
#endif

    #endregion
}