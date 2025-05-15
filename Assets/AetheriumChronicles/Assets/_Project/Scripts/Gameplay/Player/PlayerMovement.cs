using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private PlayerInputActions playerInputActions;
    private Vector2 moveInput;
    private Animator anim;
    // Se eliminó: private SpriteRenderer spriteRenderer;

    [Header("Jump Settings")]
    public float jumpForce = 7f;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private bool isGrounded;
    private bool isFacingRight = true; // Asumimos que el personaje empieza mirando a la derecha

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        // Se eliminó: spriteRenderer = GetComponent<SpriteRenderer>();

        playerInputActions = new PlayerInputActions();

        playerInputActions.PlayerControls.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerInputActions.PlayerControls.Move.canceled += ctx => moveInput = Vector2.zero;
        playerInputActions.PlayerControls.Jump.performed += JumpInputPerformed;
    }

    private void JumpInputPerformed(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            if (anim != null)
            {
                // Comenta esto si no tienes el parámetro "Jump" en el Animator
                // anim.SetTrigger("Jump"); 
            }
        }
    }

    void OnEnable()
    {
        playerInputActions?.PlayerControls.Enable();
    }

    void OnDisable()
    {
        playerInputActions?.PlayerControls.Disable();
    }

    void FixedUpdate()
    {
        // Ground check
        if (groundCheck != null)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        else
            isGrounded = false;

        // Animation parameter for IsRunning
        if (anim != null)
        {
            anim.SetBool("IsRunning", Mathf.Abs(moveInput.x) > 0.01f);
            // Comenta esto si no tienes el parámetro "IsGrounded" en el Animator
            // anim.SetBool("IsGrounded", isGrounded);
        }

        // Sprite flipping by changing localScale.x
        if (moveInput.x > 0.01f && !isFacingRight)
        {
            Flip();
        }
        else if (moveInput.x < -0.01f && isFacingRight)
        {
            Flip();
        }

        // Movement
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 currentScale = transform.localScale;
        currentScale.x *= -1;
        transform.localScale = currentScale;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}