using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Transform groundCheck;
    public LayerMask groundLayer;
    private Rigidbody2D rigidBody2D;
    private Animator animator;

    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float jumpPower = 10f;

    private float horizontalInput;
    private bool isFacingRight = true;
    private bool jumpRequested = false;


    // Start is called before the first frame update
    void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (horizontalInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < 0 && isFacingRight)
        {
            Flip();
        }

        if (horizontalInput != 0)
        {
            animator.SetBool("isWalking", true);
        }
        else
        {
            animator.SetBool("isWalking", false);
        }
    }
    
    void FixedUpdate()
    {
        Vector2 currentVelocity = rigidBody2D.velocity;

        float timeScale = TimeController.Instance.TimeScale;
        currentVelocity.x = horizontalInput * movementSpeed * timeScale;

        float fixedStep = Time.fixedDeltaTime;
        currentVelocity.y += GravityController.Instance.Gravity * fixedStep;

        if (jumpRequested && IsGrounded()) {
            currentVelocity.y = jumpPower * timeScale;
            jumpRequested = false;
        }

        rigidBody2D.velocity = currentVelocity;
    }


    public void Move(InputAction.CallbackContext context)
    {
        horizontalInput = context.ReadValue<Vector2>().x;

    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && IsGrounded())
        {
            jumpRequested = true;
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
