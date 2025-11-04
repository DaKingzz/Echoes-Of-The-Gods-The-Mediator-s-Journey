using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private ScriptableStats stats;

    private Rigidbody2D rigidBody2D;
    private CapsuleCollider2D capsuleCollider2D;
    private Animator animator;

    private FrameInput frameInput;
    private Vector2 frameVelocity;

    private bool cachedQueryStartInColliders;
    private float time;

    // Grounding state
    private float frameLeftGrounded = float.MinValue;
    private bool grounded;

    // Jump state
    private bool jumpToConsume;
    private bool bufferedJumpUsable;
    private bool endedJumpEarly;
    private bool coyoteUsable;
    private float timeJumpWasPressed;

    // Facing direction
    private bool isFacingRight = true;

    [SerializeField] private GameObject weapon;
    private Animator weaponAnimator;

    private void Awake()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        capsuleCollider2D = GetComponent<CapsuleCollider2D>();
        animator = GetComponent<Animator>();
        weaponAnimator = weapon.GetComponent<Animator>();

        cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
    }

    private void Update()
    {
        time += Time.deltaTime;

        // Handle facing flip
        if (frameInput.Move.x > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (frameInput.Move.x < 0 && isFacingRight)
        {
            Flip();
        }

        // Walking animation
        animator.SetBool("isWalking", frameInput.Move.x != 0 && grounded);

        // Jump/fall animator control
        UpdateJumpAnimatorFlags();
    }

    private void FixedUpdate()
    {
        CheckCollisions();

        HandleJump();
        HandleDirection();
        HandleGravity();

        ApplyMovement();
    }

    #region Collisions

    private void CheckCollisions()
    {
        Physics2D.queriesStartInColliders = false;

        // Ground and Ceiling
        bool groundHit = Physics2D.CapsuleCast(
            capsuleCollider2D.bounds.center,
            capsuleCollider2D.size,
            capsuleCollider2D.direction,
            0,
            Vector2.down,
            stats.GrounderDistance,
            ~stats.PlayerLayer
        );

        bool ceilingHit = Physics2D.CapsuleCast(
            capsuleCollider2D.bounds.center,
            capsuleCollider2D.size,
            capsuleCollider2D.direction,
            0,
            Vector2.up,
            stats.GrounderDistance,
            ~stats.PlayerLayer
        );

        // Hit a Ceiling
        if (ceilingHit) frameVelocity.y = Mathf.Min(0, frameVelocity.y);

        // Landed on the Ground
        if (!grounded && groundHit)
        {
            grounded = true;
            coyoteUsable = true;
            bufferedJumpUsable = true;
            endedJumpEarly = false;
        }
        // Left the Ground
        else if (grounded && !groundHit)
        {
            grounded = false;
            frameLeftGrounded = time;
        }

        Physics2D.queriesStartInColliders = cachedQueryStartInColliders;
    }

    #endregion

    #region Jumping

    private bool HasBufferedJump => bufferedJumpUsable && time < timeJumpWasPressed + stats.JumpBuffer;
    private bool CanUseCoyote => coyoteUsable && !grounded && time < frameLeftGrounded + stats.CoyoteTime;

    private void HandleJump()
    {
        if (!endedJumpEarly && !grounded && !frameInput.JumpHeld && rigidBody2D.velocity.y > 0)
            endedJumpEarly = true;

        if (!jumpToConsume && !HasBufferedJump) return;

        if (grounded || CanUseCoyote) ExecuteJump();

        jumpToConsume = false;
    }

    private void ExecuteJump()
    {
        endedJumpEarly = false;
        timeJumpWasPressed = 0;
        bufferedJumpUsable = false;
        coyoteUsable = false;
        frameVelocity.y = stats.JumpPower;
    }

    #endregion

    #region Horizontal

    private void HandleDirection()
    {
        float timeScale = TimeController.Instance.TimeScale;

        if (frameInput.Move.x == 0)
        {
            float deceleration = grounded ? stats.GroundDeceleration : stats.AirDeceleration;
            frameVelocity.x = Mathf.MoveTowards(frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
        }
        else
        {
            frameVelocity.x = Mathf.MoveTowards(
                frameVelocity.x,
                frameInput.Move.x * stats.MaxSpeed * timeScale,
                stats.Acceleration * Time.fixedDeltaTime
            );
        }
    }

    #endregion

    #region attacking
    public void Attack()
    {
        weaponAnimator.SetTrigger("swordAttack");
    }
    #endregion

    #region Gravity

    private void HandleGravity()
    {
        if (grounded && frameVelocity.y <= 0f)
        {
            frameVelocity.y = stats.GroundingForce;
        }
        else
        {
            float inAirGravity = stats.FallAcceleration;
            if (endedJumpEarly && frameVelocity.y > 0)
                inAirGravity *= stats.JumpEndEarlyGravityModifier;

            frameVelocity.y =
                Mathf.MoveTowards(frameVelocity.y, -stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
        }
    }

    #endregion

    private void ApplyMovement() => rigidBody2D.velocity = frameVelocity;

    #region Input System

    public void Move(InputAction.CallbackContext context)
    {
        Vector2 inputVector = context.ReadValue<Vector2>();
        frameInput.Move = new Vector2(inputVector.x, inputVector.y);

        // If in air and player moves, switch to side jump sprite and ensure orientation matches movement
        if (!grounded)
        {
            if (!Mathf.Approximately(frameInput.Move.x, 0f))
            {
                animator.SetBool("isJumpingRight", true);
                animator.SetBool("isJumpingForward", false);

                if (frameInput.Move.x > 0 && !isFacingRight) Flip();
                if (frameInput.Move.x < 0 && isFacingRight) Flip();
            }
        }
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpToConsume = true;
            timeJumpWasPressed = time;
            frameInput.JumpDown = true;
            frameInput.JumpHeld = true;
        }
        else if (context.canceled)
        {
            frameInput.JumpHeld = false;
        }
    }

    public void Run(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            stats.MaxSpeed *= stats.SprintMultiplier;
        }
        else if (context.canceled)
        {
            stats.MaxSpeed /= stats.SprintMultiplier;
        }
    }

    #endregion

    #region Animations

    private void UpdateJumpAnimatorFlags()
    {
        bool pendingJump = jumpToConsume || HasBufferedJump;

        // Reset all jump flags by default
        animator.SetBool("isJumpingForward", false);
        animator.SetBool("isJumpingRight", false);

        if (!grounded)
        {
            if (rigidBody2D.velocity.y > 0f || pendingJump)
            {
                // Ascending: show jump animations
                if (!Mathf.Approximately(frameInput.Move.x, 0f))
                {
                    animator.SetBool("isJumpingRight", true);
                    animator.SetBool("isJumpingForward", false);
                }
                else
                {
                    animator.SetBool("isJumpingForward", true);
                    animator.SetBool("isJumpingRight", false);
                }

                // While jumping, walking flag off
                animator.SetBool("isWalking", false);
            }
            else
            {
                // Falling: use walking pose if moving sideways, idle if not
                if (!Mathf.Approximately(frameInput.Move.x, 0f))
                {
                    animator.SetBool("isWalking", true);
                    animator.speed = 0f; // freeze on first frame of walk
                }
                else
                {
                    animator.SetBool("isWalking", false);
                    animator.speed = 1f; // reset speed for idle
                }
            }
        }
        else
        {
            // Grounded: normal walking/idle
            animator.speed = 1f;
            animator.SetBool("isWalking", !Mathf.Approximately(frameInput.Move.x, 0f));
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }

    #endregion
}

public struct FrameInput
{
    public bool JumpDown;
    public bool JumpHeld;
    public Vector2 Move;
}