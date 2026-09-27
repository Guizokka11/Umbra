using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cadeado de símbolos (ex.: caixinha de música da Amelie: lua, chave, estrela).
/// Cada roda é um SymbolDial filho. Interagir numa roda troca o símbolo.
/// Quando todas mostram o símbolo certo, dispara onUnlocked uma vez.
/// </summary>
public class SymbolLock : MonoBehaviour
{
    public SymbolDial[] dials;
    [Tooltip("Índice do símbolo correto em cada roda, na mesma ordem de 'dials'.")]
    public int[] solution = { 0, 1, 2 };

    public UnityEvent onUnlocked;
    public AudioSource unlockSfx;

    public bool Unlocked { get; private set; }

    void Awake()
    {
        if (dials == null || dials.Length == 0) dials = GetComponentsInChildren<SymbolDial>();
        foreach (var d in dials) d.owner = this;
    }

    public void Check()
    {
        if (Unlocked || dials == null) return;
        for (int i = 0; i < dials.Length; i++)
            if (i >= solution.Length || dials[i].Index != solution[i]) return;

        Unlocked = true;
        foreach (var d in dials) d.enabled = false; // não dá mais para girar
        if (unlockSfx != null) unlockSfx.Play();
        onUnlocked.Invoke();
    }
}
