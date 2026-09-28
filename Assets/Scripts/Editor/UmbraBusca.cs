using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Umbra > Terror > Preparar busca do banheiro (03_Banheiro)
/// Transforma o "Encontro_Banheiro" numa busca: a Inspetora entra farejando e checa os chuveiros e as cabines
/// um por um (BuscaDaCriatura). Cria só o objeto "Busca (chuveiros e cabines)" com seus pontos; não mexe no
/// cenário nem nas colisões das divisórias. Com Undo; pode rodar de novo sem duplicar.
/// </summary>
public static class UmbraBusca
{
    const string NomeBusca = "Busca (chuveiros e cabines)";

    [MenuItem("Umbra/Terror/Preparar busca do banheiro (03_Banheiro)", priority = 120)]
    static void Preparar()
    {
        var cena = EditorSceneManager.GetActiveScene();
        ScriptedEncounter enc = null;
        foreach (var e in Object.FindObjectsByType<ScriptedEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.name == "Encontro_Banheiro") enc = e;
        if (enc == null)
        {
            EditorUtility.DisplayDialog("Umbra — busca", "Não achei o \"Encontro_Banheiro\" na cena " + cena.name + ".\nAbra a 03_Banheiro.", "OK");
            return;
        }

        var rel = new List<string>();
        Undo.SetCurrentGroupName("Busca do banheiro");
        int grupo = Undo.GetCurrentGroup();

        var busca = enc.GetComponentInChildren<BuscaDaCriatura>(true);
        if (busca == null)
        {
            var go = new GameObject(NomeBusca);
            Undo.RegisterCreatedObjectUndo(go, "Busca do banheiro");
            go.transform.SetParent(enc.transform, false);
            busca = go.AddComponent<BuscaDaCriatura>();
            rel.Add("+ \"" + NomeBusca + "\" (BuscaDaCriatura)");
        }
        Undo.RecordObject(busca, "Busca do banheiro");
        Undo.RecordObject(enc, "Busca do banheiro");
        enc.busca = busca;
        busca.criatura = enc.creature;

        Transform Marca(string nome) { var g = GameObject.Find(nome); return g != null ? g.transform : null; }
        if (busca.entrada == null) busca.entrada = Marca("WP_porta");
        if (busca.saida == null) busca.saida = Marca("WP_saida");

        if (busca.pontos == null || busca.pontos.Length == 0)
        {
            var wpChuveiros = Marca("WP_chuveiros");
            var wpCabines = Marca("WP_cabines");
            HidingSpot cortina = null, cabine = null;
            foreach (var h in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = h.name.ToLowerInvariant();
                if (n.Contains("chuveiro") || n.Contains("cortina")) cortina = h;
                else if (n.Contains("cabine")) cabine = h;
            }
            var lista = new List<BuscaDaCriatura.Ponto>();
            // Mais perto da porta primeiro: chuveiro com cortina, o outro chuveiro, a cabine 1, a outra cabine.
            if (cortina != null && wpChuveiros != null) lista.Add(Ponto(busca.transform, "Chuveiro 2 (cortina)", new Vector3(cortina.transform.position.x, wpChuveiros.position.y, wpChuveiros.position.z), cortina));
            if (wpChuveiros != null) lista.Add(Ponto(busca.transform, "Chuveiro 1", wpChuveiros.position, null));
            if (cabine != null && wpCabines != null) lista.Add(Ponto(busca.transform, "Cabine 1", new Vector3(cabine.transform.position.x, wpCabines.position.y, wpCabines.position.z), cabine));
            if (wpCabines != null) lista.Add(Ponto(busca.transform, "Cabine 2", wpCabines.position, null));
            busca.pontos = lista.ToArray();
            foreach (var p in lista)
                rel.Add("+ lugar \"" + p.nome + "\"" + (p.esconderijo != null ? " (esconderijo: " + p.esconderijo.name + ")" : "") +
                        (p.balancar != null ? " — balança: \"" + p.balancar.name + "\"" : " — nada para balançar (escolha um sprite em Balancar)"));
        }
        else rel.Add("= a busca já tinha " + busca.pontos.Length + " lugares (não mexi)");

        EditorUtility.SetDirty(busca);
        EditorUtility.SetDirty(enc);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cena);
        rel.Insert(0, "= \"Encontro_Banheiro\" agora faz a busca no lugar da patrulha");
        string texto = string.Join("\n", rel) + "\n\nConfira os pontos (esferas amarelas/laranja) no Scene View.\nSalve a cena (Ctrl+S).";
        Debug.Log("[Umbra] Busca: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — busca", texto, "OK");
    }

    [MenuItem("Umbra/Terror/Testar busca do banheiro: começar agora (Play)", priority = 121)]
    static void Testar()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Umbra — busca", "Use no Play, na 03_Banheiro.", "OK"); return; }
        foreach (var e in Object.FindObjectsByType<ScriptedEncounter>(FindObjectsSortMode.None))
        {
            if (e.name != "Encontro_Banheiro") continue;
            GameFlags.Set(e.doneFlag, false);          // o encontro só acontece uma vez: libera para o teste
            if (e.busca != null) e.busca.mostrarNoConsole = true;
            e.Begin();
            Debug.Log("[Umbra] Busca (teste): encontro do banheiro começou. A Inspetora entra em " + e.arriveDelay + " s.");
            return;
        }
        Debug.LogWarning("[Umbra] Busca (teste): não achei o Encontro_Banheiro (abra a 03_Banheiro).");
    }

    [MenuItem("Umbra/Terror/Testar busca do banheiro: estado no Console (Play)", priority = 122)]
    static void Estado()
    {
        if (!Application.isPlaying) return;
        foreach (var e in Object.FindObjectsByType<ScriptedEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var c = e.creature;
            string luzes = "";
            if (e.lightsOff != null) foreach (var l in e.lightsOff) if (l != null) luzes += l.name + "=" + (l.isOn ? "acesa" : "apagada") + " ";
            var st = PlayerState.Instance;
            Debug.Log("[Umbra] Busca (estado): " + e.name + " rodando=" + e.Rodando + " busca=" + (e.busca != null ? (e.busca.Rodando ? "rodando" : e.busca.Terminou ? "terminou" : "parada") : "-") +
                      " | criatura " + (c != null ? c.name + " ativa=" + c.isActiveAndEnabled + " estado=" + c.State + " pos=" + c.transform.position.ToString("F2") : "-") +
                      " | " + luzes + "| Luma " + (st != null ? st.transform.position.ToString("F2") + " escondida=" + st.isHidden + " luz=" + st.IsInLight : "-"));
        }
    }

    static BuscaDaCriatura.Ponto Ponto(Transform pai, string nome, Vector3 pos, HidingSpot esconderijo)
    {
        var go = new GameObject("Busca: " + nome);
        Undo.RegisterCreatedObjectUndo(go, nome);
        go.transform.SetParent(pai, false);
        go.transform.position = pos;
        return new BuscaDaCriatura.Ponto { nome = nome, onde = go.transform, esconderijo = esconderijo, balancar = Balanca(pos) };
    }

    /// <summary>Sprite do cenário alto (cortina, porta, divisória) na frente/atrás do ponto: o menor que cobre o X do ponto.</summary>
    static Transform Balanca(Vector3 pos)
    {
        SpriteRenderer melhor = null;
        float area = float.MaxValue;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sr.sprite == null || sr.GetComponentInParent<PlayerState>() != null || sr.GetComponentInParent<CreatureAI>() != null) continue;
            string n = sr.name.ToLowerInvariant();
            if (n.Contains("privada") || n.StartsWith("pia") || n.Contains(" pia") || n.Contains("espelho")) continue;   // não balançam
            var b = sr.bounds;
            if (pos.x < b.min.x || pos.x > b.max.x || b.size.y < 1.2f || b.size.x > 5f || b.size.x < 0.25f) continue;
            float a = b.size.x * b.size.y;
            if (n.Contains("divisoria") || n.Contains("cortina") || n.Contains("porta")) a *= 0.25f;       // preferidos
            if (a < area) { area = a; melhor = sr; }
        }
        return melhor != null ? melhor.transform : null;
    }
}
