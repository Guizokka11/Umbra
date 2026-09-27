using UnityEngine;

/// <summary>
/// Liga/desliga objetos conforme um GameFlag. Mantém o mundo coerente quando
/// a Luma volta a um cômodo (fusível já instalado, grade já aberta,
/// Inspetora acordada). Não coloque este componente num objeto das listas.
/// </summary>
[DefaultExecutionOrder(-40)]
public class FlagSwitch : MonoBehaviour
{
    public string flag;
    public GameObject[] activeWhenSet;
    public GameObject[] activeWhenNotSet;

    void Awake()  { Apply(); GameFlags.OnChanged += Changed; }
    void OnDestroy() { GameFlags.OnChanged -= Changed; }

    void Changed(string f, bool on) { if (f == flag) Apply(); }

    public void Apply()
    {
        bool on = GameFlags.Has(flag);
        if (activeWhenSet != null) foreach (var g in activeWhenSet) if (g != null) g.SetActive(on);
        if (activeWhenNotSet != null) foreach (var g in activeWhenNotSet) if (g != null) g.SetActive(!on);
    }
}
