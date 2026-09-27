using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 3.5f;
    public float runSpeed  = 6f;
    public float gravity   = -20f;
    public float jumpForce = 8f;

    [Header("Controle externo (usado por esconderijo, caixas, urso, pistas)")]
    [HideInInspector] public bool  canMove = true;
    [HideInInspector] public bool  canJump = true;
    [HideInInspector] public bool  lockFacing = false;
    [HideInInspector] public float speedMultiplier = 1f;

    [Header("Travamento de profundidade (opcional, para momentos cinemáticos)")]
    [Tooltip("Enquanto travado, o eixo Z ignora o input e desliza suavemente até depthLockZ.")]
    public bool  isDepthLocked = false;
    public float depthLockTransitionSpeed = 8f;

    [HideInInspector] public Vector3 moveDirection;
    [HideInInspector] public bool    isMoving;
    [HideInInspector] public bool    isRunning;
    [HideInInspector] public bool    isJumping;
    [HideInInspector] public bool    facingRight = true;

    private CharacterController cc;
    private float verticalVelocity;
    private float depthLockZ;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        depthLockZ = transform.position.z;
    }

    void Update()
    {
        float ix = Input.GetAxisRaw("Horizontal");
        float iz = Input.GetAxisRaw("Vertical");
        if (!canMove) { ix = 0f; iz = 0f; }
        isRunning = canMove && speedMultiplier >= 1f && Input.GetKey(KeyCode.LeftShift);

        Vector3 input = new Vector3(ix, 0f, iz);
        if (input.magnitude > 1f) input.Normalize();

        moveDirection = input;
        isMoving      = input.magnitude > 0.01f;

        // Flip do sprite baseado só no eixo X, mesmo se houver movimento em Z.
        if (lockFacing) { }
        else if (ix > 0.01f)  facingRight = true;
        else if (ix < -0.01f) facingRight = false;

        // Gravidade e pulo.
        if (cc.isGrounded)
        {
            verticalVelocity = -2f;
            isJumping = false;
            if (canMove && canJump && Input.GetButtonDown("Jump"))
            {
                verticalVelocity = jumpForce;
                isJumping = true;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        float speed = (isRunning ? runSpeed : walkSpeed) * speedMultiplier;
        Vector3 vel = input * speed;
        vel.y = verticalVelocity;

        // Se travado, ignora o input de Z e desliza até a profundidade alvo.
        if (isDepthLocked)
        {
            float currentZ = transform.position.z;
            float newZ     = Mathf.MoveTowards(currentZ, depthLockZ, depthLockTransitionSpeed * Time.deltaTime);
            vel.z = (newZ - currentZ) / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        cc.Move(vel * Time.deltaTime);
    }

    /// <summary>
    /// Trava o eixo Z numa profundidade fixa (para corredores/eventos cinemáticos).
    /// </summary>
    public void LockDepth(float z)
    {
        isDepthLocked = true;
        depthLockZ = z;
    }

    /// <summary>
    /// Libera o eixo Z para movimento normal (X/Z livres).
    /// </summary>
    public void UnlockDepth()
    {
        isDepthLocked = false;
    }

    /// <summary>
    /// Move a Luma instantaneamente (checkpoint, esconderijo, cutscene).
    /// </summary>
    public void Teleport(Vector3 position)
    {
        if (cc == null) cc = GetComponent<CharacterController>();
        Vector3 delta = position - transform.position;
        cc.enabled = false;
        transform.position = position;
        cc.enabled = true;
        verticalVelocity = 0f;
        // Avisa o Cinemachine para a câmera que segue pular junto, sem arrastar pela tela.
        CinemachineCore.OnTargetObjectWarped(transform, delta);
    }
}
