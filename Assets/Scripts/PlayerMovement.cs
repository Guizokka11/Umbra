using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 3.5f;
    public float runSpeed  = 6f;
    public float gravity   = -20f;
    public float jumpForce = 8f;

    [HideInInspector] public Vector3 moveDirection;
    [HideInInspector] public bool    isMoving;
    [HideInInspector] public bool    isRunning;
    [HideInInspector] public bool    isJumping;

    private CharacterController cc;
    private float verticalVelocity;

    void Start() => cc = GetComponent<CharacterController>();

    void Update()
    {
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        Vector3 input = new Vector3(ix, 0f, iz).normalized;
        moveDirection = input;
        isMoving      = input.magnitude > 0.01f;

        if (cc.isGrounded)
        {
            verticalVelocity = -2f;
            isJumping = false;
            if (Input.GetButtonDown("Jump"))
            {
                verticalVelocity = jumpForce;
                isJumping = true;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        float speed = isRunning ? runSpeed : walkSpeed;
        Vector3 vel = input * speed;
        vel.y = verticalVelocity;
        cc.Move(vel * Time.deltaTime);
    }
}