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
    private float initialJumpForce = 8f; // burst at jump start

    [SerializeField] private float sustainedJumpForce = 5f; // extra force while holding
    [SerializeField] private float sustainedJumpDuration = 0.25f; // how long extra force can be applied
    [SerializeField] private float gravityScale = 3f;
    [SerializeField] private float fallGravityMultiplier = 2f;

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
        currentHealth = maxHealth;
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

        // Jump start
        if (jumpPressed && isGrounded)
        {
            rigidBody2D.velocity = new Vector2(rigidBody2D.velocity.x, initialJumpForce);
            jumpStartTime = Time.time;
        }

        // Sustained jump while holding
        if (jumpHeld && !isGrounded)
        {
            float elapsed = Time.time - jumpStartTime;
            if (elapsed < sustainedJumpDuration)
            {
                // Apply diminishing upward force
                float holdFactor = 1f - (elapsed / sustainedJumpDuration); // decreases from 1 to 0
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

    public void TakeDamage(float damage)
    {
        Debug.Log($"Player took {damage} damage!");
        // Implement health reduction and death logic here
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            Debug.Log("Player has died!");
            Destroy(gameObject);
        }
    }
}