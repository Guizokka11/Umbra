using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Algo que precisa de um item (ou só de ser usado uma vez) e lembra que foi
/// resolvido: caixa de fusíveis, grade trancada, trinco alto, porta do escritório.
/// Sem "requiredItem" funciona como um interruptor de uso único que fica salvo.
/// </summary>
public class ItemLock : Interactable
{
    [Tooltip("Flag do item necessário (vazio = não precisa de item).")]
    public string requiredItem;
    public bool consumeItem = true;
    [Tooltip("Flag salvo quando resolvido, ex.: corredor2_fusivel")]
    public string doneFlag;

    public string promptWithItem = "Usar";
    [TextArea] public string lockedMessage = "Não abre.";
    [TextArea] public string doneMessage;

    public GameObject[] activateOnDone;
    public GameObject[] deactivateOnDone;
    public UnityEvent onDone;
    [Tooltip("Chamado também ao voltar para a cena já resolvida (sem mensagem).")]
    public UnityEvent onAlreadyDone;

    [Header("Sob pressão (opcional)")]
    [Tooltip("Usar o item começa este puzzle e só resolve quando ele terminar; se falhar, o item volta e dá para tentar de novo.")]
    public PuzzleSobPressao pressao;
    [TextArea] [Tooltip("Mensagem ao começar (ex.: \"Colocou o fusível. Não solte...\").")]
    public string mensagemAoComecar;

    string basePrompt;
    bool done;

    void Awake()
    {
        basePrompt = string.IsNullOrEmpty(prompt) || prompt == "Interagir" ? "Examinar" : prompt;
    }

    void Start()
    {
        if (GameFlags.Has(doneFlag))
        {
            done = true;
            Apply();
            onAlreadyDone?.Invoke();
        }
    }

    bool HasItem => string.IsNullOrEmpty(requiredItem) || GameFlags.Has(requiredItem);

    public override bool CanInteract(PlayerInteractor who) => base.CanInteract(who) && !done && (pressao == null || !pressao.Rodando);

    void Update()
    {
        prompt = HasItem && !string.IsNullOrEmpty(requiredItem) ? promptWithItem : basePrompt;
    }

    public override void Interact(PlayerInteractor who)
    {
        if (done) return;
        if (!HasItem)
        {
            Hud.Toast(lockedMessage);
            return;
        }
        if (pressao != null)
        {
            // Sob pressão: só resolve no fim; se falhar, nada foi gasto.
            if (pressao.Rodando) return;
            if (!string.IsNullOrEmpty(mensagemAoComecar)) Hud.Toast(mensagemAoComecar);
            pressao.Comecar(Resolver, null);
            return;
        }
        Resolver();
    }

    void Resolver()
    {
        if (done) return;
        done = true;
        if (!string.IsNullOrEmpty(requiredItem) && consumeItem)
        {
            GameFlags.Set(requiredItem, false);
            GameFlags.Set(requiredItem + "_usado");
        }
        GameFlags.Set(doneFlag);
        Apply();
        if (!string.IsNullOrEmpty(doneMessage)) Hud.Toast(doneMessage);
        onDone?.Invoke();
    }

    void Apply()
    {
        if (activateOnDone != null) foreach (var g in activateOnDone) if (g != null) g.SetActive(true);
        if (deactivateOnDone != null) foreach (var g in deactivateOnDone) if (g != null) g.SetActive(false);
    }
}
