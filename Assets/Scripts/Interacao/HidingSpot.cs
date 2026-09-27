using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Esconderijo (armário, embaixo da cama, atrás de cortina...).
/// Interagir entra, interagir de novo sai. Escondida, a Luma não é vista
/// nem ouvida — MAS se uma criatura a viu entrando durante uma perseguição,
/// ela vai até o esconderijo e a puxa para fora (ver CreatureAI).
/// </summary>
public class HidingSpot : Interactable
{
    [Tooltip("Onde a Luma fica enquanto escondida.")]
    public Transform hidePoint;
    [Tooltip("Onde ela reaparece ao sair. Se vazio, volta para onde estava.")]
    public Transform exitPoint;
    [Tooltip("Esconde o sprite da Luma (armário). Desmarque para embaixo da cama/sombra.")]
    public bool hideSprite = true;
    [Tooltip("Fica mais escuro/tenso dentro: sobe o medo aos poucos.")]
    public float fearPerSecondInside = 0.01f;

    public UnityEvent onEnter;
    public UnityEvent onExit;

    public bool Occupied => occupant != null;

    PlayerInteractor occupant;
    Vector3 returnPos;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Esconder";
    }

    public override bool CanInteract(PlayerInteractor who)
        => base.CanInteract(who) && (occupant == null || occupant == who) && !who.State.isGrabbing;

    public override void Interact(PlayerInteractor who)
    {
        if (occupant == null) Enter(who);
        else Exit();
    }

    void Enter(PlayerInteractor who)
    {
        occupant = who;
        var st = who.State;
        returnPos = who.transform.position;

        st.Movement.isMoving = false;
        st.Movement.isRunning = false;
        if (hidePoint != null) st.Movement.Teleport(hidePoint.position);
        st.Movement.enabled = false;
        st.isHidden = true;

        if (hideSprite)
            foreach (var r in who.GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;

        who.Lock(this);
        onEnter.Invoke();
    }

    public void Exit()
    {
        if (occupant == null) return;
        var who = occupant;
        var st = who.State;

        st.Movement.enabled = true;
        st.Movement.Teleport(exitPoint != null ? exitPoint.position : returnPos);
        st.isHidden = false;
        foreach (var r in who.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = true;

        who.Unlock(this);
        occupant = null;
        onExit.Invoke();
    }

    public override void ForceRelease(PlayerInteractor who) => Exit();

    public string exitPrompt = "Sair";
    string basePrompt;

    void Update()
    {
        if (basePrompt == null) basePrompt = prompt;
        prompt = occupant != null ? exitPrompt : basePrompt;

        if (occupant != null && FearSystem.Instance != null)
            FearSystem.Instance.AddFear(fearPerSecondInside * Time.deltaTime);
    }
}
