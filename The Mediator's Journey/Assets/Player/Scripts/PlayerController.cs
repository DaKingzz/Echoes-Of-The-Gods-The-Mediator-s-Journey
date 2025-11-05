using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 8f;
    [SerializeField] private float runMultiplier = 1.5f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float fallGravityMultiplier = 2f;
    [SerializeField] private float jumpReleaseGravityMultiplier = 3f;

    [Header("Jump Tuning")]
    [SerializeField] private float minimumHopForce = 4f;
    [SerializeField] private float minimumPressTime = 0.08f;

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

    private void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        collider2D = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();

        rigidBody2D.gravityScale = gravityScale;
    }

    private void Update()
    {
        animator.SetBool("isWalking", Mathf.Abs(movementInput.x) > 0.01f && isGrounded);

        if (!isGrounded)
        {
            if (rigidBody2D.velocity.y > 0f)
            {
                if (Mathf.Abs(movementInput.x) > 0.01f)
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
            else
            {
                animator.SetBool("isJumpingForward", false);
                animator.SetBool("isJumpingRight", false);
            }
        }
        else
        {
            animator.SetBool("isJumpingForward", false);
            animator.SetBool("isJumpingRight", false);
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

        // Jump
        if (jumpPressed && isGrounded)
        {
            rigidBody2D.velocity = new Vector2(rigidBody2D.velocity.x, jumpForce);
            jumpStartTime = Time.time;
        }

        // Apply extra gravity when falling
        if (rigidBody2D.velocity.y < 0f)
        {
            rigidBody2D.velocity += Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
        // Apply stronger gravity if jump was released early while ascending
        else if (rigidBody2D.velocity.y > 0f && !jumpHeld)
        {
            float elapsed = Time.time - jumpStartTime;

            if (elapsed >= minimumPressTime)
            {
                rigidBody2D.velocity += Vector2.up * Physics2D.gravity.y * (jumpReleaseGravityMultiplier - 1f) * Time.fixedDeltaTime;
            }
            else
            {
                if (rigidBody2D.velocity.y > minimumHopForce)
                {
                    rigidBody2D.velocity = new Vector2(rigidBody2D.velocity.x, minimumHopForce);
                }
            }
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
}