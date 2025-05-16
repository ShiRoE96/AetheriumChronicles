using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private PlayerInputActions playerInputActions;
    private Vector2 moveInput;
    private Animator anim;
    private bool isFacingRight = true;

    [Header("Jump Settings")]
    public float jumpForce = 7f;
    public float sprintJumpForce = 10f;
    private bool isJumping;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    [Header("Coyote Time Settings")]
    public float coyoteTimeDuration = 0.1f;
    private float coyoteTimeCounter;

    [Header("Jump Buffering Settings")]
    public float jumpBufferDuration = 0.1f;
    private float jumpBufferCounter;

    [Header("Sprint Settings")]
    public float sprintSpeed = 8f;
    private bool sprintKeyHeld = false;

    [Header("Air Control Settings")]
    public float airControlMultiplier = 0.8f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        playerInputActions = new PlayerInputActions();

        playerInputActions.PlayerControls.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        playerInputActions.PlayerControls.Move.canceled += ctx => moveInput = Vector2.zero;

        playerInputActions.PlayerControls.Jump.performed += JumpInputPerformed;
        playerInputActions.PlayerControls.Jump.canceled += JumpInputCanceled;

        playerInputActions.PlayerControls.Sprint.performed += ctx => sprintKeyHeld = true;
        playerInputActions.PlayerControls.Sprint.canceled += ctx => sprintKeyHeld = false;

        playerInputActions.PlayerControls.Attack.performed += AttackInputPerformed; // Nueva suscripci�n
    }

    private void JumpInputPerformed(InputAction.CallbackContext context)
    {
        jumpBufferCounter = jumpBufferDuration;
    }

    private void JumpInputCanceled(InputAction.CallbackContext context)
    {
        if (isJumping)
        {
            if (rb.linearVelocity.y > 0)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
            }
            isJumping = false;
        }
    }

    private void AttackInputPerformed(InputAction.CallbackContext context) // Nuevo m�todo
    {
        if (anim != null)
        {
            anim.SetTrigger("Attack");
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

        // Coyote Time Logic
        if (isGrounded)
            coyoteTimeCounter = coyoteTimeDuration;
        else
            coyoteTimeCounter -= Time.fixedDeltaTime;

        // Jump Buffer Logic
        if (jumpBufferCounter > 0f)
            jumpBufferCounter -= Time.fixedDeltaTime;

        // Reset isJumping if no longer ascending
        if (rb.linearVelocity.y <= 0)
            isJumping = false;

        // Actual Jump Execution
        if (jumpBufferCounter > 0f && (isGrounded || coyoteTimeCounter > 0f))
        {
            isJumping = true;
            bool isAttemptingSprintWhileMoving = sprintKeyHeld && (Mathf.Abs(moveInput.x) > 0.01f);
            float currentAppliedJumpForce = isAttemptingSprintWhileMoving ? sprintJumpForce : jumpForce;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentAppliedJumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;
            if (anim != null)
            {
                // Comenta la siguiente l�nea si no tienes un par�metro Trigger "Jump" en tu Animator para la animaci�n de salto.
                // anim.SetTrigger("Jump"); 
            }
        }

        // Determine movement states for animation
        bool isCurrentlyMovingHorizontally = Mathf.Abs(moveInput.x) > 0.01f;
        bool isCurrentlySprinting = sprintKeyHeld && isCurrentlyMovingHorizontally;
        bool isCurrentlyWalking = isCurrentlyMovingHorizontally && !isCurrentlySprinting;

        // Animation parameters
        if (anim != null)
        {
            anim.SetBool("IsWalking", isCurrentlyWalking);
            anim.SetBool("IsSprinting", isCurrentlySprinting);
            // Comenta la siguiente l�nea si no tienes un par�metro Bool "IsGrounded" en tu Animator.
            // anim.SetBool("IsGrounded", isGrounded);
        }

        // Sprite flipping
        if (moveInput.x > 0.01f && !isFacingRight) Flip();
        else if (moveInput.x < -0.01f && isFacingRight) Flip();

        // Movement
        if (rb != null)
        {
            float currentBaseSpeed = isCurrentlySprinting ? sprintSpeed : moveSpeed;
            float actualHorizontalSpeed = currentBaseSpeed;

            if (!isGrounded)
            {
                actualHorizontalSpeed *= airControlMultiplier;
            }

            rb.linearVelocity = new Vector2(moveInput.x * actualHorizontalSpeed, rb.linearVelocity.y);
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