using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Umbra > Terror > Medo do escuro: ...
///  - preparar na cena aberta: cria a configuração global (Assets/Dados/Resources/MedoDoEscuro.asset) se faltar e
///    põe na cena o objeto "Medo do escuro (cena)" (EscuroNaCena) para ligar/desligar/ajustar só nela. Com Undo.
///  - testar (Play): escuro no máximo / pânico agora.
/// </summary>
public static class UmbraEscuro
{
    const string ConfigPath = "Assets/Dados/Resources/MedoDoEscuro.asset";

    [MenuItem("Umbra/Terror/Medo do escuro: preparar na cena aberta", priority = 130)]
    static void Preparar()
    {
        string rel = "";
        var cfg = AssetDatabase.LoadAssetAtPath<MedoDoEscuroConfig>(ConfigPath);
        if (cfg == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Dados")) AssetDatabase.CreateFolder("Assets", "Dados");
            if (!AssetDatabase.IsValidFolder("Assets/Dados/Resources")) AssetDatabase.CreateFolder("Assets/Dados", "Resources");
            cfg = ScriptableObject.CreateInstance<MedoDoEscuroConfig>();
            AssetDatabase.CreateAsset(cfg, ConfigPath);
            AssetDatabase.SaveAssets();
            rel += "+ configuração " + ConfigPath + "\n";
        }

        var cena = EditorSceneManager.GetActiveScene();
        var ajuste = Object.FindAnyObjectByType<EscuroNaCena>(FindObjectsInactive.Include);
        if (ajuste == null)
        {
            var go = new GameObject("Medo do escuro (cena)");
            Undo.RegisterCreatedObjectUndo(go, "Medo do escuro");
            ajuste = go.AddComponent<EscuroNaCena>();
            bool naLista = System.Array.IndexOf(cfg.cenas, cena.name) >= 0;
            ajuste.desligado = !naLista;                   // começa como a lista global diz; mude aqui se quiser
            EditorSceneManager.MarkSceneDirty(cena);
            rel += "+ \"Medo do escuro (cena)\" — " + (naLista ? "LIGADO (cena do Arco 1)" : "DESLIGADO (cena fora da lista)") + "\n";
        }
        else rel += "= a cena já tinha \"" + ajuste.name + "\" (" + (ajuste.desligado ? "desligado" : "ligado") + ")\n";

        Selection.activeObject = ajuste.gameObject;
        string texto = "Cena " + cena.name + ":\n\n" + rel +
                       "\nPara desligar só nesta cena: marque \"Desligado\". Para ajustar tudo: " + ConfigPath + ".\nSalve a cena (Ctrl+S).";
        Debug.Log("[Umbra] Medo do escuro: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — medo do escuro", texto, "OK");
    }

    [MenuItem("Umbra/Terror/Testar medo do escuro: escuro no máximo (Play)", priority = 131)]
    static void TestarEscuro() => Testar(false);

    [MenuItem("Umbra/Terror/Testar medo do escuro: pânico agora (Play)", priority = 132)]
    static void TestarPanico() => Testar(true);

    static void Testar(bool panico)
    {
        var m = MedoDoEscuro.Instance;
        if (!Application.isPlaying || m == null) { EditorUtility.DisplayDialog("Umbra — medo do escuro", "Use no Play.", "OK"); return; }
        if (!m.Ativo) { Debug.LogWarning("[Umbra] Medo do escuro: está DESLIGADO nesta cena."); return; }
        m.TestarMaximo(panico);
        Debug.Log("[Umbra] Medo do escuro (teste): escuro no máximo" + (panico ? " + medo 100% (pânico se ela estiver fora da luz)" : "") +
                  ". Fique fora da luz para manter; entre na luz para ver o alívio lento.");
    }
}
