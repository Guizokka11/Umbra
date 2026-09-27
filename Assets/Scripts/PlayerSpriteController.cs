using UnityEngine;

public class PlayerSpriteController : MonoBehaviour
{
    // Nomes dos estados no Animator da Luma. Para trocar o placeholder pela arte, basta
    // substituir os quadros do clipe (Assets/Animations/Placeholders) — o nome do estado continua o mesmo.
    [Header("Estados de ação (Animator)")]
    public string estadoSubir     = "Climb";   // ClimbAssist
    public string estadoEmpurrar  = "Push";    // Pushable, andando na direção do objeto
    public string estadoPuxar     = "Pull";    // Pushable, andando para longe do objeto
    public string estadoSegurar   = "Grab";    // Pushable, parada segurando (se não existir, usa Empurrar congelado)
    public string estadoEscondida = "Hide";    // HidingSpot (só aparece quando o esconderijo não some com o sprite)
    public string estadoPega      = "Caught";  // criatura pegou (GameManager.PlayerCaught)
    public string estadoAbraco    = "Hug";     // urso de pelúcia
    public string estadoEscada    = "Stairs";  // andando numa escada 3D (subindo ou descendo)

    private PlayerMovement movement;
    private Animator       animator;
    private SpriteRenderer sr;
    private PlayerState    state;
    private ClimbAssist    climb;
    private int            estadoInicial;   // estado padrão do Animator; volta para ele quando o pedido não existe

    void Start()
    {
        // PlayerMovement está no mesmo objeto (Player)
        movement = GetComponent<PlayerMovement>();
        state    = GetComponent<PlayerState>();
        climb    = GetComponent<ClimbAssist>();

        // Animator e SpriteRenderer estão no filho (SpriteObject)
        animator = GetComponentInChildren<Animator>();
        sr       = GetComponentInChildren<SpriteRenderer>();

        if (movement == null) Debug.LogError("PlayerMovement não achado no Player!");
        if (animator == null) Debug.LogError("Animator não achado no SpriteObject!");
        if (sr == null)       Debug.LogError("SpriteRenderer não achado no SpriteObject!");

        if (animator != null) estadoInicial = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
    }

    void Update()
    {
        if (movement == null || animator == null || sr == null) return;

        // Em 2.5D lateral não existe mais direção por ângulo — só espelha o sprite.
        sr.flipX = !movement.facingRight;

        bool congelar = false;   // segurando parada sem estado "Grab": mantém o quadro do empurrar
        string stateName;
        if (state != null && state.isDead)                    stateName = estadoPega;
        else if (state != null && state.isHidden)             stateName = estadoEscondida;
        else if (climb != null && climb.IsClimbing)           stateName = estadoSubir;
        else if (state != null && state.isHuggingBear)        stateName = estadoAbraco;
        else if (state != null && state.isGrabbing)
        {
            if (!movement.isMoving)
            {
                stateName = estadoSegurar;
                if (!HasState(stateName)) { stateName = estadoEmpurrar; congelar = true; }
            }
            else stateName = AndandoParaOObjeto() ? estadoEmpurrar : estadoPuxar;
        }
        else if (state != null && state.isOnStairs && movement.isMoving && !movement.isJumping) stateName = estadoEscada;
        else if (movement.isJumping)                          stateName = "Jump";
        else if (movement.isRunning && movement.isMoving)     stateName = "Run";
        else if (movement.isMoving)                           stateName = "Walk";
        else                                                  stateName = "Idle";

        animator.speed = congelar ? 0f : 1f;

        // Só troca se o estado existir no Animator (evita avisos enquanto as animações não estão prontas).
        // Se não existir, volta ao estado padrão — senão ficaria preso em Push/Caught depois de usá-los.
        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash)) hash = estadoInicial;
        if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != hash) animator.Play(hash, 0, 0f);
    }

    /// <summary>Empurrar = anda na direção do objeto segurado; puxar = anda para longe dele.</summary>
    bool AndandoParaOObjeto()
    {
        if (state.heldObject == null) return true;
        Vector3 paraObjeto = state.heldObject.position - transform.position;
        Vector3 anda = movement.moveDirection;
        paraObjeto.y = anda.y = 0f;
        return Vector3.Dot(paraObjeto, anda) >= 0f;
    }

    // Permite adicionar animações novas sem quebrar se ainda não existirem.
    bool HasState(string name) => animator.HasState(0, Animator.StringToHash(name));
}
