using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Umbra > Terror > Preparar fusível sob pressão (09_LazyRoom)
/// A caixa de luz da sala de descanso passa a ser um PuzzleSobPressao: colocar o fusível começa ~6 s de espera
/// (a lâmpada pisca, passos se aproximam); se a Luma sair do lugar, o fusível cai e ela recomeça.
/// Só adiciona o objeto "Fusível sob pressão" e liga na caixa (Undo; pode rodar de novo).
/// </summary>
public static class UmbraPressao
{
    [MenuItem("Umbra/Terror/Preparar fusível sob pressão (09_LazyRoom)", priority = 140)]
    static void Preparar()
    {
        var cena = EditorSceneManager.GetActiveScene();
        ItemLock caixa = null;
        foreach (var l in Object.FindObjectsByType<ItemLock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.name == "Caixa de luz (fusível)") caixa = l;
        if (caixa == null)
        {
            EditorUtility.DisplayDialog("Umbra — pressão", "Não achei a \"Caixa de luz (fusível)\" na cena " + cena.name + ".\nAbra a 09_LazyRoom.", "OK");
            return;
        }
        Undo.SetCurrentGroupName("Fusível sob pressão");
        int grupo = Undo.GetCurrentGroup();
        string rel;
        var p = caixa.GetComponentInChildren<PuzzleSobPressao>(true);
        if (p == null)
        {
            var go = new GameObject("Fusível sob pressão");
            Undo.RegisterCreatedObjectUndo(go, "Fusível sob pressão");
            go.transform.SetParent(caixa.transform, false);
            p = go.AddComponent<PuzzleSobPressao>();
            p.duracao = 6f;
            p.raioParaFicar = 1.3f;
            p.tempoLimite = 0f;
            p.chamarARonda = false;
            p.piscarAcendendo = true;
            p.mensagemAoFalhar = "O fusível escorregou e caiu... Encaixe de novo e fique parada até a luz firmar.";
            var luz = GameObject.Find("Luz_Lampada");
            if (luz != null && luz.GetComponent<LightZone>() != null) p.luzesQuePiscam = new[] { luz.GetComponent<LightZone>() };
            rel = "+ \"Fusível sob pressão\" (6 s, ficar a 1,3 m; a lâmpada \"Luz_Lampada\" pisca)";
        }
        else rel = "= já existia \"" + p.name + "\" (não mexi nos valores)";

        Undo.RecordObject(caixa, "Fusível sob pressão");
        caixa.pressao = p;
        if (string.IsNullOrEmpty(caixa.mensagemAoComecar))
            caixa.mensagemAoComecar = "Encaixou o fusível... a luz está tentando ligar. Não saia daí.";
        EditorUtility.SetDirty(caixa);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cena);
        string texto = rel + "\n= a caixa de luz agora usa a pressão (o fusível só é gasto quando a luz firmar)\n\nSalve a cena (Ctrl+S).";
        Debug.Log("[Umbra] Pressão: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — pressão", texto, "OK");
    }

    [MenuItem("Umbra/Terror/Testar fusível: dar o fusível e desfazer a caixa (antes do Play)", priority = 141)]
    static void DarFusivel()
    {
        GameFlags.Set("item_fusivel");
        GameFlags.Set("lazy_luz", false);
        GameFlags.Set("lazy_lampada_quebrada", false);
        GameFlags.Set("fuga_inspetora", false);
        Debug.Log("[Umbra] Pressão (teste): a Luma tem o fusível e a caixa de luz da sala de descanso voltou a estar sem fusível. Dê Play na 09_LazyRoom.");
        EditorUtility.DisplayDialog("Umbra — pressão", "Pronto: a Luma tem o fusível e a caixa está sem fusível.\nDê Play na 09_LazyRoom.", "OK");
    }
}
