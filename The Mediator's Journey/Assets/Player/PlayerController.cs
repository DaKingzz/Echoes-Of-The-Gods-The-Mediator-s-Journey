using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Transform groundCheck;
    public LayerMask groundLayer;
    private Rigidbody2D rigidBody2D;
    private Animator animator;

    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float jumpPower = 6f;

    private float horizontalInput;
    private bool isFacingRight = true;
    private bool jumpRequested = false;

    void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Handle facing flip based on input while on ground or in air movement direction
        if (horizontalInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < 0 && isFacingRight)
        {
            Flip();
        }

        // Walking animation
        animator.SetBool("isWalking", horizontalInput != 0 && IsGrounded());

        // Jump/fall animator control
        UpdateJumpAnimatorFlags();
    }

    void FixedUpdate()
    {
        Vector2 currentVelocity = rigidBody2D.velocity;

        float timeScale = TimeController.Instance.TimeScale;
        currentVelocity.x = horizontalInput * movementSpeed * timeScale;

        if (jumpRequested && IsGrounded())
        {
            // If starting jump from standstill, prefer forward jump sprite
            if (Mathf.Approximately(horizontalInput, 0f))
            {
                animator.SetBool("isJumpingForward", true);
                animator.SetBool("isJumpingRight", false);
            }
            else
            {
                animator.SetBool("isJumpingRight", true);
                animator.SetBool("isJumpingForward", false);
            }

            currentVelocity.y = jumpPower;
            jumpRequested = false;
        }

        rigidBody2D.velocity = currentVelocity;
    }

    public void Move(InputAction.CallbackContext context)
    {
        horizontalInput = context.ReadValue<Vector2>().x;

        // If in air and player moves, switch to side jump sprite and ensure orientation matches movement
        if (!IsGrounded())
        {
            if (!Mathf.Approximately(horizontalInput, 0f))
            {
                animator.SetBool("isJumpingRight", true);
                animator.SetBool("isJumpingForward", false);

                // Flip to match movement direction immediately
                if (horizontalInput > 0 && !isFacingRight) Flip();
                if (horizontalInput < 0 && isFacingRight) Flip();
            }
        }
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && IsGrounded())
        {
            jumpRequested = true;
        }
    }

    private void UpdateJumpAnimatorFlags()
    {
        bool grounded = IsGrounded();

        if (grounded)
        {
            // On ground, clear jump flags
            animator.SetBool("isJumpingForward", false);
            animator.SetBool("isJumpingRight", false);
            return;
        }

        // In air: decide which jump sprite to show
        if (!Mathf.Approximately(horizontalInput, 0f))
        {
            animator.SetBool("isJumpingRight", true);
            animator.SetBool("isJumpingForward", false);
        }
        else
        {
            animator.SetBool("isJumpingForward", true);
            animator.SetBool("isJumpingRight", false);
        }
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x *= -1;
        transform.localScale = localScale;
    }
}