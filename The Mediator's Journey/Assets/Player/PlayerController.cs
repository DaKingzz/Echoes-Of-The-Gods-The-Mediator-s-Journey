using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public Transform groundCheck;
    public LayerMask groundLayer;
    private Rigidbody2D  rigidBody2D;

    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float jumpHeight = 5f;
    // This value would later be changed by the gravity god mood swings
    [SerializeField] private float gravityValue = -9.8f;

    private float horizontalInput;
    private bool isFacingRight = true;


    // Start is called before the first frame update
    void Start()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
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
    }
    
    void FixedUpdate()
    {
        rigidBody2D.velocity = new Vector2(horizontalInput * movementSpeed, rigidBody2D.velocity.y);
    }


    public void Move(InputAction.CallbackContext context)
    {
        horizontalInput = context.ReadValue<Vector2>().x;

    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && IsGrounded())
        {
            rigidBody2D.velocity = new Vector2(rigidBody2D.velocity.x, jumpHeight);
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
