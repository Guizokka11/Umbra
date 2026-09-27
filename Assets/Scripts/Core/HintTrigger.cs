using UnityEngine;

/// <summary>
/// Área que mostra uma dica/fala uma única vez (salvo em GameFlags).
/// Ex.: "Segure F para abraçar o urso", ou a voz "...Luma..." (asSubtitle).
/// </summary>
[RequireComponent(typeof(Collider))]
public class HintTrigger : MonoBehaviour
{
    [TextArea] public string text;
    public bool asSubtitle;
    public float seconds = 3.5f;
    [Tooltip("Flag para não repetir. Vazio = repete a cada vez que a cena carrega.")]
    public string onceFlag;
    [Tooltip("Só aparece se este flag existir (opcional).")]
    public string requiresFlag;

    bool fired;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (fired || !other.CompareTag("Player")) return;
        if (!string.IsNullOrEmpty(requiresFlag) && !GameFlags.Has(requiresFlag)) return;
        if (!string.IsNullOrEmpty(onceFlag) && GameFlags.Has(onceFlag)) return;
        fired = true;
        GameFlags.Set(onceFlag);
        if (asSubtitle) Hud.Subtitle(text, seconds);
        else Hud.Toast(text, seconds);
    }
}
