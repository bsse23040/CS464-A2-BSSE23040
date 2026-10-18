using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves and jumps the player. The keys are read in Update, and the physics is done in FixedUpdate.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float speed = 5f;                 // metres per second

    [Header("Jump")]
    [SerializeField] float jumpHeight = 1.8f;          // metres
    [SerializeField] float fallMultiplier = 2.2f;      // extra gravity on the way down only
    [SerializeField] float coyoteTime = 0.1f;          // a press this long after leaving a ledge still jumps
    [SerializeField] float jumpBuffer = 0.12f;         // a press this long before landing jumps when we land

    [Header("Ground check")]
    [SerializeField] Transform feet;                   // the check sphere is centred here
    [SerializeField] float checkRadius = 0.4f;         // a bit smaller than the capsule, so a wall never counts as ground
    [SerializeField] LayerMask groundMask;             // only the Ground layer, never the player's own capsule

    Rigidbody rb;
    InputAction moveAction, jumpAction;
    Vector3 move;                                      // built in Update, used in FixedUpdate
    bool isGrounded, jumpQueued;
    float lastGroundedTime = -1f, lastPressedTime = -1f;   // both windows are timestamps, checked in Update

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // the project-wide actions asset (Assets/InputSystem_Actions.inputactions)
        moveAction = InputSystem.actions.FindAction("Move");
        jumpAction = InputSystem.actions.FindAction("Jump");
    }

    void Update()
    {
        // read the keys here: a key press only lasts one frame, and FixedUpdate does not run every frame
        Vector2 input = moveAction.ReadValue<Vector2>();
        move = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);   // diagonals are not faster

        bool jumpPressed = jumpAction.WasPressedThisFrame();

        // stamp the ground only when no jump is queued and we are not rising, or a quick second press would jump again
        if (isGrounded && !jumpQueued && rb.linearVelocity.y <= 0f) lastGroundedTime = Time.time;
        if (jumpPressed) lastPressedTime = Time.time;

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;    // coyote time: still counts as standing
        bool wantJump = Time.time - lastPressedTime <= jumpBuffer;    // jump buffer: the press is remembered
        if (canJump && wantJump && !jumpQueued)
        {
            jumpQueued = true;
            lastGroundedTime = -1f;          // the jump uses up both windows
            lastPressedTime = -1f;
        }
    }

    void FixedUpdate()
    {
        isGrounded = Physics.CheckSphere(feet.position, checkRadius, groundMask, QueryTriggerInteraction.Ignore);

        // a velocity is in metres per second: no deltaTime here, the physics step does that
        rb.linearVelocity = new Vector3(move.x * speed, rb.linearVelocity.y, move.z * speed);

        if (jumpQueued)
        {
            // rising speed v against gravity g stops at height h = v^2 / 2g, so v = sqrt(2 g h)
            float g = -Physics.gravity.y;
            float v0 = Mathf.Sqrt(2f * g * jumpHeight);

            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);   // so a jump taken on the way down is not short
            rb.AddForce(Vector3.up * rb.mass * v0, ForceMode.Impulse);                        // an impulse is divided by the mass
            jumpQueued = false;
        }

        // fall faster than we rise: extra gravity while falling, so the apex stays the same but the fall is shorter
        if (rb.linearVelocity.y < 0f)
            rb.AddForce(Physics.gravity * (fallMultiplier - 1f), ForceMode.Acceleration);
    }

    // select the Player to see the ground check: green when it touches ground, red when it does not
    void OnDrawGizmosSelected()
    {
        Vector3 centre = feet != null ? feet.position : transform.position;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(centre, checkRadius);
    }
}
