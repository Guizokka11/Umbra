using UnityEngine;

/// <summary>
/// Ações para ligar em eventos pelo Inspector (UnityEvent) sem programar:
/// SetFlag("x"), ClearFlag("x"), Toast("texto"), Subtitle("texto").
/// Também pode ligar/desligar flags ao carregar a cena.
/// </summary>
public class FlagActions : MonoBehaviour
{
    public string[] setOnStart;
    public string[] clearOnStart;

    void Start()
    {
        if (setOnStart != null) foreach (var f in setOnStart) GameFlags.Set(f);
        if (clearOnStart != null) foreach (var f in clearOnStart) GameFlags.Set(f, false);
    }

    public void SetFlag(string flag)   => GameFlags.Set(flag);
    public void ClearFlag(string flag) => GameFlags.Set(flag, false);
    public void Toast(string text)     => Hud.Toast(text);
    public void Subtitle(string text)  => Hud.Subtitle(text, 3.5f);
    public void AddFear(float amount)  { if (FearSystem.Instance != null) FearSystem.Instance.AddFear(amount); }
}
