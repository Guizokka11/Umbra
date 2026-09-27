using System.Collections;
using UnityEngine;

/// <summary>
/// Fim da demonstração: quando todas as pistas pedidas forem lidas,
/// a Luma pensa em voz alta, a tela escurece e volta ao menu.
/// </summary>
public class DemoEnd : MonoBehaviour
{
    public string[] requiredClues;
    [TextArea] public string[] lines;
    public string endText = "FIM DA DEMONSTRAÇÃO — continua no Arco 2";
    public string doneFlag = "demo_fim";

    bool running;

    void OnEnable()  { ClueJournal.OnClueFound += Found; }
    void OnDisable() { ClueJournal.OnClueFound -= Found; }

    void Found(ClueData c) { if (AllFound() && !running) StartCoroutine(Run()); }

    bool AllFound()
    {
        if (requiredClues == null) return true;
        foreach (var id in requiredClues) if (!ClueJournal.Has(id)) return false;
        return true;
    }

    IEnumerator Run()
    {
        running = true;
        var st = PlayerState.Instance;
        while (st != null && st.isReading) yield return null;   // espera fechar a última ficha
        yield return new WaitForSeconds(0.6f);
        if (st != null) st.Movement.canMove = false;
        if (lines != null)
            foreach (var l in lines) { Hud.Subtitle(l, 3f); yield return new WaitForSeconds(3.6f); }
        GameFlags.Set(doneFlag);
        if (ScreenFader.Instance != null) yield return ScreenFader.Instance.FadeOut(2f);
        Hud.Toast(endText, 4f);
        yield return new WaitForSeconds(4.5f);
        SaveGame.ToMenu();
    }
}
