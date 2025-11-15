using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour, IPlayer
{
    [Header("Health Settings")] [SerializeField]
    private float maxHealth = 10f;

    private float currentHealth;

    [Header("Movement Settings")] [SerializeField]
    private float movementSpeed = 8f;

    [SerializeField] private float runMultiplier = 1.5f;

    [Header("Jump Settings")] [SerializeField]
    private float initialJumpForce = 8f;

    [SerializeField] private float sustainedJumpForce = 5f;
    [SerializeField] private float sustainedJumpDuration = 0.25f;
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float fallGravityMultiplier = 2f;

    [Header("Audio Sources")]
    [Tooltip("AudioSource used for one-shot SFX like jump. Prefer non-looping AudioSource.")]
    [SerializeField]
    private AudioSource sfxSource;

    [Tooltip(
        "AudioSource used for looped walking/footstep sound. Prefer looping AudioSource or leave empty to let script manage it.")]
    [SerializeField]
    private AudioSource footstepsSource;

    [Header("Audio Volumes (optional overrides)")] [Range(0f, 1f)] [SerializeField]
    private float jumpSoundVolume = 1f;

    [Range(0f, 1f)] [SerializeField] private float walkSoundVolume = 1f;

    [Header("Walking Sound Settings")]
    [Tooltip("Minimum horizontal input magnitude to consider the player 'walking'.")]
    [SerializeField]
    private float walkThreshold = 0.01f;

    private Rigidbody2D rigidBody2D;
    private Collider2D collider2D;
    private Animator animator;

    private Vector2 movementInput;
    private bool jumpPressed;
    private bool jumpHeld;
    private bool runHeld;
    private bool isGrounded;
    private bool isFacingRight = true;

    private float jumpStartTime;
    private bool wasWalkingPlaying = false;

    private void Reset()
    {
        // Add at least one AudioSource so designers can wire two if they want
        if (GetComponents<AudioSource>().Length == 0)
            gameObject.AddComponent<AudioSource>();
    }

    private void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        collider2D = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        // If designer didn't assign sources, try to auto-resolve:
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

        // Configure defaults (don't force designers' choices but ensure sensible defaults)
        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        if (footstepsSource != null)
        {
            footstepsSource.playOnAwake = false;
            // keep loop as configured by designer; we will ensure it's looping when playing
        }

        rigidBody2D.gravityScale = gravityScale;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        bool isWalkingNow = Mathf.Abs(movementInput.x) > walkThreshold && isGrounded;

        if (animator != null)
            animator.SetBool("isWalking", isWalkingNow);

        UpdateWalkingSound(isWalkingNow);

        if (!isGrounded)
        {
            if (rigidBody2D.velocity.y > 0f)
            {
                if (Mathf.Abs(movementInput.x) > 0.01f)
                {
                    if (animator != null)
                    {
                        animator.SetBool("isJumpingRight", true);
                        animator.SetBool("isJumpingForward", false);
                    }
                }
                else
                {
                    if (animator != null)
                    {
                        animator.SetBool("isJumpingForward", true);
                        animator.SetBool("isJumpingRight", false);
                    }
                }
            }
            else
            {
                if (animator != null)
                {
                    animator.SetBool("isJumpingForward", false);
                    animator.SetBool("isJumpingRight", false);
                }
            }
        }
        else
        {
            if (animator != null)
            {
                animator.SetBool("isJumpingForward", false);
                animator.SetBool("isJumpingRight", false);
            }
        }
    }

    private void FixedUpdate()
    {
        // Ground check: any contact beneath counts
        isGrounded = false;
        ContactPoint2D[] contacts = new ContactPoint2D[4];
        int contactCount = rigidBody2D.GetContacts(contacts);
        for (int i = 0; i < contactCount; i++)
        {
            if (contacts[i].normal.y > 0.5f)
            {
                isGrounded = true;
                break;
            }
        }

        // Horizontal movement with sprint multiplier
        float currentSpeed = movementSpeed * (runHeld ? runMultiplier : 1f);
        rigidBody2D.velocity = new Vector2(movementInput.x * currentSpeed, rigidBody2D.velocity.y);

        // Flip sprite if needed
        if (movementInput.x > 0 && !isFacingRight) Flip();
        else if (movementInput.x < 0 && isFacingRight) Flip();

        // Jump start
        if (jumpPressed && isGrounded)
        {
            rigidBody2D.velocity = new Vector2(rigidBody2D.velocity.x, initialJumpForce);
            jumpStartTime = Time.time;

            // Play jump SFX using the assigned sfxSource:
            // Prefer PlayOneShot if a clip is assigned on the sfxSource (so it won't interrupt other one-shots),
            // otherwise call Play() which will play the assigned clip on that source.
            if (sfxSource != null)
            {
                if (sfxSource.clip != null)
                {
                    sfxSource.PlayOneShot(sfxSource.clip, jumpSoundVolume);
                }
                else
                {
                    sfxSource.Play();
                }
            }
        }

        // Sustained jump while holding
        if (jumpHeld && !isGrounded)
        {
            float elapsed = Time.time - jumpStartTime;
            if (elapsed < sustainedJumpDuration)
            {
                float holdFactor = 1f - (elapsed / sustainedJumpDuration);
                rigidBody2D.velocity += Vector2.up * (sustainedJumpForce * holdFactor * Time.fixedDeltaTime);
            }
        }

        // Extra gravity when falling
        if (rigidBody2D.velocity.y < 0f)
        {
            rigidBody2D.velocity +=
                Vector2.up * (Physics2D.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
        }

        jumpPressed = false;
    }

    // -------- Input System Callbacks --------
    public void OnMove(InputAction.CallbackContext context)
    {
        movementInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
            jumpHeld = true;
        }
        else if (context.canceled)
        {
            jumpHeld = false;
        }
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.performed) runHeld = true;
        else if (context.canceled) runHeld = false;
    }

    // -------- Flip method --------
    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1f;
        transform.localScale = localScale;
    }

    // -------- Walking sound helper --------
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

    public void TakeDamage(float damage)
    {
        Debug.Log($"Player took {damage} damage!");
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            Debug.Log("Player has died!");
            Destroy(gameObject);
        }
    }
}