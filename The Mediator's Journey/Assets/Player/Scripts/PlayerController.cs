using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
public class PlayerController : MonoBehaviour, IPlayer
{
    // Get Respawn Point Later
    public Transform playerRespawn;
    
    private SpriteRenderer _spriteRenderer;

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
    
    [Header("Death Settings")]
    [Tooltip("Audio clip to play when player dies")]
    [SerializeField] private AudioSource deathSoundSource;

    [Tooltip("Time it takes for health to drain to 0")]
    [SerializeField] private float healthDrainDuration = 0.5f;

    [Tooltip("Time it takes for health to refill after respawn")]
    [SerializeField] private float healthRefillDuration = 1f;

    [Tooltip("Delay before respawning after death")]
    [SerializeField] private float respawnDelay = 1f;
    private bool _isDead = false;
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
    private Collider2D damageArea;

    [Tooltip("Damage applied to enemies hit by the attack.")] [SerializeField]
    private float attackDamage = 2f;

    [Tooltip("LayerMask used to filter enemies for hit detection.")] [SerializeField]
    private LayerMask enemyLayerMask = default;

    [Tooltip("If true, only first target per sweep is damaged.")] [SerializeField]
    private bool stopAfterFirstHit = false;

    [Header("Attack Cooldown")] [Tooltip("Minimum time in seconds between player attacks.")] [SerializeField]
    private float attackCooldown = 0.35f;

    private readonly HashSet<Collider2D> hitsThisSweep = new HashSet<Collider2D>();
    private readonly int animatorHashAttack = Animator.StringToHash("isAttacking");
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

    private Vector2 movementInput = Vector2.zero;
    private bool runInputHeld;
    private bool jumpInputHeld;
    private bool jumpPressedThisFrame;

    #endregion

    #region Runtime State

    private bool isFacingRight = true;
    private bool isGrounded;
    private float timeWhenJumpStarted = -Mathf.Infinity;
    private bool wasWalkingPlaying = false;
    private bool isInJumpPhase = false;

    // last non-zero horizontal input sign while airborne: -1 = left, 0 = none, +1 = right
    private int lastAirMovementSign = 0;

    // whether an airborne-forward state is currently active (true means in-air pose is left/right)
    private bool airborneForwardActive = false;

    // once true (set when airborneForwardActive is exited by releasing input), player cannot re-enter airborneForward until landing
    private bool preventReenterAirForwardUntilLand = false;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        if (GetComponents<AudioSource>().Length == 0)
            gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        collider2D = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (playerRespawn != null)
        {
            Debug.Log("Found TargetObject Transform: " + playerRespawn.position);
        }
        else
        {
            Debug.LogWarning("TargetObject not found!");
        }


        // Auto-resolve audio sources if the designer didn't assign them
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sfxSource == null) sfxSource = sources.Length >= 1 ? sources[0] : gameObject.AddComponent<AudioSource>();
        if (footstepsSource == null)
            footstepsSource = sources.Length >= 2 ? sources[1] : gameObject.AddComponent<AudioSource>();

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        if (footstepsSource != null) footstepsSource.playOnAwake = false;

        rigidBody2D.gravityScale = baseGravityScale;
        currentHealth = Mathf.Clamp(maximumHealth, 0f, maximumHealth);
    }

    private void Update()
    {
        // Animator visuals synchronized from FixedUpdate; keep Update minimal.
        UpdateAnimatorVisuals();
    }

    private void FixedUpdate()
    {
        RefreshGroundedStateFromOverlap();

        Vector2 computedVelocity = rigidBody2D.velocity;
        float horizontalSpeed = movementInput.x * (runInputHeld ? movementSpeed * runSpeedMultiplier : movementSpeed);
        computedVelocity.x = horizontalSpeed;

        if (jumpPressedThisFrame && isGrounded)
        {
            computedVelocity.y = initialJumpVelocity;
            timeWhenJumpStarted = Time.fixedTime;
            isInJumpPhase = true;

            InitializeAirborneDirectionOnJump();

            if (sfxSource != null)
            {
                if (sfxSource.clip != null) sfxSource.PlayOneShot(sfxSource.clip, jumpSoundVolume);
                else sfxSource.Play();
            }

            jumpPressedThisFrame = false;
        }
        else
        {
            jumpPressedThisFrame = false;
        }

        if (isInJumpPhase) HandleAirborneDirectionRules();

        ApplySustainedJump(ref computedVelocity);
        ApplyExtraFallGravity(ref computedVelocity);

        rigidBody2D.velocity = computedVelocity;

        bool isWalkingNow = Mathf.Abs(movementInput.x) > walkThreshold && isGrounded;
        UpdateWalkingSound(isWalkingNow);

        // keep animator visuals up to date in physics step
        UpdateAnimatorVisuals();
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

    private void RefreshGroundedStateFromOverlap()
    {
        bool prevGrounded = isGrounded;

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

        if (isInJumpPhase && (Time.fixedTime - timeWhenJumpStarted) <= jumpGroundIgnoreTime)
        {
            isGrounded = false;
        }

        // Clear jump phase on landing; do NOT fire any landing trigger or timer logic
        if (isGrounded && !prevGrounded)
        {
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
    }

    #endregion

    #region Airborne direction rules (core)

    private void InitializeAirborneDirectionOnJump()
    {
        float x = movementInput.x;
        if (Mathf.Abs(x) > 0.01f)
        {
            lastAirMovementSign = x > 0f ? 1 : -1;
            airborneForwardActive = true;
            preventReenterAirForwardUntilLand = false;
        }
        else
        {
            lastAirMovementSign = 0;
            airborneForwardActive = false;
            preventReenterAirForwardUntilLand = false;
        }

        if (animator != null) animator.SetBool(animatorHashIsJumping, true);
    }

    private void HandleAirborneDirectionRules()
    {
        float x = movementInput.x;
        int inputSign = 0;
        if (Mathf.Abs(x) > 0.01f) inputSign = x > 0f ? 1 : -1;

        if (inputSign != 0)
        {
            if (!airborneForwardActive)
            {
                if (!preventReenterAirForwardUntilLand)
                {
                    lastAirMovementSign = inputSign;
                    airborneForwardActive = true;
                    preventReenterAirForwardUntilLand = false;
                }
                else
                {
                    if (lastAirMovementSign != 0 && inputSign != lastAirMovementSign)
                    {
                        lastAirMovementSign = inputSign;
                        airborneForwardActive = true;
                        preventReenterAirForwardUntilLand = false;
                    }
                }
            }
            else
            {
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
            if (airborneForwardActive)
            {
                airborneForwardActive = false;
                preventReenterAirForwardUntilLand = true;
            }
        }
    }

    #endregion

    #region Jump Helpers (variable jump implementation)

    private void ApplySustainedJump(ref Vector2 velocity)
    {
        if (!jumpInputHeld) return;
        if (isGrounded) return;

        float timeSinceJumpStart = Time.fixedTime - timeWhenJumpStarted;
        if (timeSinceJumpStart <= sustainedJumpMaximumDuration)
        {
            float holdFactor = 1f - (timeSinceJumpStart / sustainedJumpMaximumDuration);
            float accelerationThisFrame = sustainedJumpAcceleration * holdFactor;
            velocity.y += accelerationThisFrame * Time.fixedDeltaTime;

            float maximumAllowedUpwardVelocity =
                initialJumpVelocity + sustainedJumpAcceleration * sustainedJumpMaximumDuration * 0.9f;
            if (velocity.y > maximumAllowedUpwardVelocity) velocity.y = maximumAllowedUpwardVelocity;
        }
    }

    #endregion

    #region Gravity Helpers

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

    private void UpdateAnimatorVisuals()
    {
        if (animator == null) return;

        // walking only on ground (use velocity or input; input here keeps designer intent)
        animator.SetBool(animatorHashIsWalking, Mathf.Abs(movementInput.x) > 0.01f && isGrounded);

        if (isInJumpPhase)
        {
            if (lastAirMovementSign != 0)
            {
                animator.SetBool(animatorHashIsJumpingRight, true);
                animator.SetBool(animatorHashIsJumpingForward, false);
            }
            else
            {
                animator.SetBool(animatorHashIsJumpingRight, false);
                animator.SetBool(animatorHashIsJumpingForward, true);
            }

            animator.SetBool(animatorHashIsJumping, true);
        }
        else
        {
            animator.SetBool(animatorHashIsJumpingRight, false);
            animator.SetBool(animatorHashIsJumpingForward, false);
            animator.SetBool(animatorHashIsJumping, false);
        }
    }

    #endregion

    #region Input Callbacks

    public void OnMove(InputAction.CallbackContext context)
    {
        if (_isDead)
        {
            return;
        }
        
        movementInput = context.ReadValue<Vector2>();

        if (movementInput.x > 0f && !isFacingRight) FlipFacingDirection();
        else if (movementInput.x < 0f && isFacingRight) FlipFacingDirection();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (_isDead)
        {
            // Set inputs to zero
            jumpPressedThisFrame = false;
            return;
        }
        if (context.performed)
        {
            jumpPressedThisFrame = true;
            jumpInputHeld = true;
        }
        else if (context.canceled)
        {
            jumpInputHeld = false;
        }
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (_isDead) return;
        if (context.performed) runInputHeld = true;
        else if (context.canceled) runInputHeld = false;
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (_isDead) return;
        
        if (!context.performed || animator == null) return;

        if (Time.time - lastAttackTime < attackCooldown) return;

        lastAttackTime = Time.time;

        animator.SetTrigger(animatorHashAttack);

        if (isInJumpPhase && lastAirMovementSign != 0)
        {
            animator.SetBool(animatorHashIsJumpingRight, true);
            animator.SetBool(animatorHashIsJumpingForward, false);
        }

        if (SwordAttackAudioSource != null) SwordAttackAudioSource.Play();

        DoAttackSweep();
    }

    #endregion

    #region Facing / Visual Flip

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

    private void DoAttackSweep()
    {
        hitsThisSweep.Clear();

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
            Debug.LogError("PlayerController.DoAttackSweep: damageArea is not assigned or not a BoxCollider2D.");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(centre, size, angle, enemyLayerMask);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hitsThisSweep.Contains(hit)) continue;
            hitsThisSweep.Add(hit);

            var damageable = hit.GetComponent<IEnemy>();
            if (damageable != null) damageable.TakeDamage(attackDamage);

            if (stopAfterFirstHit) break;
        }
    }

    #endregion

    #region Health and Damage

    public void TakeDamage(float damage)
    {
        if (damage <= 0f) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0f, maximumHealth);

        if (currentHealth <= 0f)
            KillPlayer();
    }
    
    /// <summary>
    /// Kills the player, sets hp to 0
    /// </summary>
    public void KillPlayer()
    {
        if (_isDead) return;
    
        StartCoroutine(DeathSequence());
    }
    
    private IEnumerator DeathSequence()
    {
        _isDead = true;
        
        deathSoundSource.Play();
    
        rigidBody2D.velocity = Vector2.zero;
        _spriteRenderer.enabled = false;
        movementInput = Vector2.zero;
    
        // Gradually drain health to 0
        float startHealth = currentHealth;
        float elapsedTime = 0f;
    
        while (elapsedTime < healthDrainDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / healthDrainDuration;
            currentHealth = Mathf.Lerp(startHealth, 0f, t);
            yield return null;
        }
    
        currentHealth = 0f;
    
        // Wait before respawning
        yield return new WaitForSeconds(respawnDelay);
    
        // Respawn player
        if (playerRespawn != null)
        {
            transform.position = playerRespawn.position;
            rigidBody2D.velocity = Vector2.zero; // Stop any momentum
            movementInput = Vector2.zero;
        }
        
        _spriteRenderer.enabled = true;
    
        // Gradually refill health
        elapsedTime = 0f;
    
        while (elapsedTime < healthRefillDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / healthRefillDuration;
            currentHealth = Mathf.Lerp(0f, maximumHealth, t);
            yield return null;
        }
    
        currentHealth = maximumHealth;
        _isDead = false;
    }
    #endregion

    #region Editor Debugging

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position;
        Gizmos.DrawLine(origin + Vector3.left * 0.5f, origin + Vector3.right * 0.5f);

        if (damageArea is BoxCollider2D box)
        {
            Gizmos.color = Color.red;
            Vector2 size = Vector2.Scale(box.size, box.transform.lossyScale);
            Vector3 pos = box.transform.position + (Vector3)box.offset;
            Gizmos.DrawWireCube(pos, new Vector3(size.x, size.y, 0f));
        }

        if (groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
        }
    }
#endif

    #endregion
}