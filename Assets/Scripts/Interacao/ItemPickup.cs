using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto que a Luma pega e guarda (fusível, chave). Vira um GameFlag
/// ("item_fusivel"). Some do cenário depois de pego, inclusive ao voltar à cena.
/// </summary>
public class ItemPickup : Interactable
{
    [Tooltip("Flag do item, ex.: item_fusivel")]
    public string itemFlag = "item_";
    public string displayName = "item";
    [Tooltip("Texto extra ao pegar (opcional).")]
    public string message;
    [Tooltip("Brilho/objeto que some junto.")]
    public GameObject visual;

    public UnityEvent onPickup;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Pegar";
    }

    void Start()
    {
        // Já foi pego (ou usado) numa visita anterior.
        if (GameFlags.Has(itemFlag) || GameFlags.Has(itemFlag + "_usado")) Hide();
    }

    public override void Interact(PlayerInteractor who)
    {
        GameFlags.Set(itemFlag);
        Hud.Toast(string.IsNullOrEmpty(message) ? "Luma pegou: " + displayName : message);
        Hide();
        onPickup?.Invoke();
    }

    void Hide()
    {
        if (visual != null) visual.SetActive(false);
        gameObject.SetActive(false);
    }
}
