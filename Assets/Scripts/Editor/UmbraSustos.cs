using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Comandos dos sustos (ScareFlash), só na cena aberta, com Undo:
///  - Umbra > Terror > Tirar legendas dos sustos da cena aberta: limpa o campo subtitle (sustos não mostram legenda)
///    e revisa as regras (um susto grande por cena, onceFlag).
///  - Umbra > Terror > Preparar susto do espelho (03_Banheiro): transforma o "Susto_Espelho" no susto do reflexo.
/// Podem rodar de novo sem duplicar nada.
/// </summary>
public static class UmbraSustos
{
    const string MaterialReflexo = "Assets/Dados/Materiais/SpriteReflexo.mat";

    [MenuItem("Umbra/Terror/Tirar legendas dos sustos da cena aberta", priority = 110)]
    static void TirarLegendas()
    {
        var cena = EditorSceneManager.GetActiveScene();
        var sustos = Object.FindObjectsByType<ScareFlash>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var rel = new List<string>();
        Undo.SetCurrentGroupName("Tirar legendas dos sustos");
        int grupo = Undo.GetCurrentGroup();
        int grandes = 0;
        foreach (var s in sustos)
        {
            if (!string.IsNullOrEmpty(s.subtitle))
            {
                Undo.RecordObject(s, "Tirar legenda");
                rel.Add("- legenda tirada de \"" + s.name + "\": " + s.subtitle.Replace("\n", " "));
                s.subtitle = "";
                EditorUtility.SetDirty(s);
            }
            if (string.IsNullOrEmpty(s.onceFlag))
                rel.Add("· \"" + s.name + "\" sem onceFlag: vai usar o automático \"susto:" + cena.name + "/" + s.name + "\"");
            if (s.grande && s.tipo != ScareFlash.Tipo.Falso) grandes++;
        }
        if (grandes > 1)
            rel.Add("! " + grandes + " sustos GRANDES nesta cena: só o primeiro vai acontecer em cada visita. Marque os outros como falsos ou desmarque \"Grande\".");
        Undo.CollapseUndoOperations(grupo);
        if (sustos.Length > 0) EditorSceneManager.MarkSceneDirty(cena);

        string texto = sustos.Length == 0 ? "Nenhum susto (ScareFlash) na cena " + cena.name + "."
                     : "Cena " + cena.name + ": " + sustos.Length + " susto(s).\n\n" +
                       (rel.Count > 0 ? string.Join("\n", rel) : "Nenhuma legenda para tirar; regras em ordem.") + "\n\nSalve a cena (Ctrl+S).";
        Debug.Log("[Umbra] Sustos: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — sustos", texto, "OK");
    }

    [MenuItem("Umbra/Terror/Preparar susto do espelho (03_Banheiro)", priority = 111)]
    static void PrepararEspelho()
    {
        var cena = EditorSceneManager.GetActiveScene();
        ScareFlash s = null;
        foreach (var x in Object.FindObjectsByType<ScareFlash>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (x.name == "Susto_Espelho") s = x;
        if (s == null)
        {
            EditorUtility.DisplayDialog("Umbra — espelho", "Não achei o objeto \"Susto_Espelho\" na cena " + cena.name + ".\nAbra a 03_Banheiro.", "OK");
            return;
        }
        var rel = new List<string>();
        var mat = MaterialDoReflexo(rel);

        Undo.SetCurrentGroupName("Susto do espelho");
        int grupo = Undo.GetCurrentGroup();
        Undo.RecordObject(s, "Susto do espelho");
        s.tipo = ScareFlash.Tipo.Espelho;
        s.grande = true;
        s.materialDoReflexo = mat;
        s.subtitle = "";
        if (s.seconds <= 0f || s.seconds > 1.2f) s.seconds = 0.7f;
        s.calma = new Vector2(0.8f, 1.6f);
        if (string.IsNullOrEmpty(s.onceFlag)) s.onceFlag = "banheiro_espelho";

        // Vidro: começa onde já estava o reflexo (ajuste o tamanho no Inspector / a caixa azul no Scene View).
        if (s.espelhoCentro == null)
        {
            var go = new GameObject("Espelho (vidro)");
            Undo.RegisterCreatedObjectUndo(go, "Vidro do espelho");
            go.transform.SetParent(s.transform, false);
            Vector3 p = s.target != null ? s.target.transform.position : s.transform.position + Vector3.up * 1.5f;
            go.transform.position = p;
            s.espelhoCentro = go.transform;
            rel.Add("+ \"Espelho (vidro)\" onde estava o reflexo (confira o retângulo azul no Scene View)");
        }

        // Encaixa o vidro no espelho pintado: a camada do PSD (sprite) mais estreita que contém o ponto do reflexo.
        var pintado = EspelhoPintado(s.espelhoCentro.position, s);
        if (pintado != null)
        {
            var b = pintado.bounds;
            Undo.RecordObject(s.espelhoCentro, "Vidro do espelho");
            // A camada inclui a moldura/sombra acima do vidro: a base é a do vidro, a altura é ~70% da camada.
            s.espelhoCentro.position = new Vector3(b.center.x, b.center.y - b.size.y * 0.1f, s.espelhoCentro.position.z);
            s.espelhoTamanho = new Vector2(b.size.x * 0.94f, b.size.y * 0.7f);
            rel.Add("= vidro encaixado no espelho pintado (\"" + pintado.name + "\"): " + s.espelhoTamanho.x.ToString("F2") + " × " + s.espelhoTamanho.y.ToString("F2") + " m");
        }
        else rel.Add("! não achei o espelho pintado: ajuste Espelho Centro / Espelho Tamanho à mão");

        // A área cobre a chegada ao espelho: a calma começa antes, o susto é na frente do vidro.
        var box = s.GetComponent<BoxCollider>();
        if (box != null)
        {
            float largura = s.espelhoTamanho.x + 5f;
            if (box.size.x < largura - 0.01f)
            {
                Undo.RecordObject(box, "Área do espelho");
                var sz = box.size; sz.x = largura; box.size = sz;
                // centraliza a área no vidro, em X
                var c = box.center;
                c.x = s.transform.InverseTransformPoint(s.espelhoCentro.position).x;
                box.center = c;
                rel.Add("+ área do susto alargada para " + largura.ToString("F1") + " m (cobre a chegada ao espelho)");
            }
        }
        EditorUtility.SetDirty(s);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cena);
        rel.Insert(0, "= \"Susto_Espelho\" agora é do tipo Espelho (reflexo da Luma + Inspetora atrás dela)");
        string texto = string.Join("\n", rel) + "\n\nSalve a cena (Ctrl+S).";
        Debug.Log("[Umbra] Espelho: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — espelho", texto, "OK");
    }

    /// <summary>Sprite do cenário perto do ponto (X, Y) com cara de espelho: a faixa mais comprida (largura/altura).</summary>
    static SpriteRenderer EspelhoPintado(Vector3 ponto, ScareFlash s)
    {
        SpriteRenderer melhor = null;
        float area = float.MaxValue;
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sr.sprite == null || (s.target != null && sr.transform.IsChildOf(s.target.transform))) continue;
            if (sr.GetComponentInParent<PlayerState>() != null || sr.GetComponentInParent<CreatureAI>() != null) continue;
            var b = sr.bounds;
            if (ponto.x < b.min.x || ponto.x > b.max.x || ponto.y < b.min.y - 0.6f || ponto.y > b.max.y + 0.6f) continue;
            if (b.size.x < 0.8f || b.size.x > 6f || b.size.y > b.size.x) continue;
            // O espelho é uma faixa bem mais larga que alta (as pias, logo abaixo, são quase quadradas).
            float a = -b.size.x / Mathf.Max(0.05f, b.size.y);
            if (a < area) { area = a; melhor = sr; }
        }
        return melhor;
    }

    [MenuItem("Umbra/Terror/Testar sustos: rearmar os sustos da cena (Play)", priority = 112)]
    static void Rearmar()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Umbra — sustos", "Use no Play.", "OK"); return; }
        int n = ScareFlash.RearmarDaCena();
        Debug.Log("[Umbra] Sustos (teste): " + n + " susto(s) da cena rearmados (flags apagados, regra de calma zerada).");
    }

    static Material MaterialDoReflexo(List<string> rel)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialReflexo);
        if (mat != null) return mat;
        var sh = Shader.Find("Umbra/Sprite Reflexo");
        if (sh == null) { rel.Add("! shader Umbra/Sprite Reflexo não encontrado (Assets/Shaders/SpriteReflexo.shader)"); return null; }
        mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, MaterialReflexo);
        AssetDatabase.SaveAssets();
        rel.Add("+ material " + MaterialReflexo);
        return mat;
    }
}
