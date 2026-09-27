using UnityEngine;

/// <summary>
/// Uma roda do SymbolLock. Cada interação avança para o próximo símbolo
/// e troca o sprite mostrado.
/// </summary>
public class SymbolDial : Interactable
{
    public Sprite[] symbols;
    public SpriteRenderer display;
    public int startIndex = 0;
    public AudioSource clickSfx;

    [HideInInspector] public SymbolLock owner;

    public int Index { get; private set; }

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Girar";
        if (display == null) display = GetComponentInChildren<SpriteRenderer>();
        Index = startIndex;
        Refresh();
    }

    public override void Interact(PlayerInteractor who)
    {
        int n = symbols != null && symbols.Length > 0 ? symbols.Length : 1;
        Index = (Index + 1) % n;
        Refresh();
        if (clickSfx != null) clickSfx.Play();
        if (owner != null) owner.Check();
    }

    void Refresh()
    {
        if (display != null && symbols != null && symbols.Length > 0)
            display.sprite = symbols[Index];
    }
}
