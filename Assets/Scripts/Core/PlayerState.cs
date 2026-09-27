using UnityEngine;

/// <summary>
/// Estado central da Luma. Outros sistemas (criaturas, medo, esconderijos, UI)
/// consultam este componente em vez de conversar entre si diretamente.
/// Colocar no objeto Player (junto com PlayerMovement).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerState : MonoBehaviour
{
    public static PlayerState Instance { get; private set; }

    [Header("Ruído (raio em metros que as criaturas conseguem ouvir)")]
    public float walkNoise = 2f;
    public float runNoise  = 7f;
    public float jumpNoise = 4f;

    [Tooltip("Altura aproximada do peito da Luma, usada nos raycasts de visão das criaturas.")]
    public float chestHeight = 0.6f;

    [HideInInspector] public bool isHidden;
    [HideInInspector] public bool isHuggingBear;
    [HideInInspector] public bool isGrabbing;
    [HideInInspector] public bool isReading;   // lendo pista / vendo memória
    [HideInInspector] public bool isDead;      // pega por uma criatura (até o respawn)
    [HideInInspector] public Transform heldObject;   // Pushable que ela está segurando (empurrar/puxar)
    [HideInInspector] public bool isOnStairs;        // dentro da área de uma escada 3D (EscadaGuia)

    public PlayerMovement Movement { get; private set; }
    public CharacterController Controller { get; private set; }

    /// <summary>Está dentro de uma LightZone acesa?</summary>
    public bool IsInLight => LightZone.IsPointLit(ChestPosition);

    public Vector3 ChestPosition => transform.position + Vector3.up * chestHeight;

    /// <summary>Raio de ruído atual. 0 = silenciosa.</summary>
    public float NoiseRadius
    {
        get
        {
            if (isHidden || isHuggingBear || isReading || isDead || Movement == null) return 0f;
            if (Movement.isJumping) return jumpNoise;
            if (!Movement.isMoving) return 0f;
            return Movement.isRunning ? runNoise : walkNoise;
        }
    }

    /// <summary>Luma está livre para agir (não escondida, não lendo, não morta).</summary>
    public bool IsFree => !isHidden && !isReading && !isDead;

    void Awake()
    {
        Instance   = this;
        Movement   = GetComponent<PlayerMovement>();
        Controller = GetComponent<CharacterController>();
    }

    public void ResetState()
    {
        isHidden = isHuggingBear = isGrabbing = isReading = isDead = false;
        heldObject = null;
        isOnStairs = false;
        Movement.enabled = true;
        Movement.canMove = true;
        Movement.canJump = true;
        Movement.lockFacing = false;
        Movement.speedMultiplier = 1f;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = true;
    }
}
