#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Role = PsdLayerDepth.Role;

/// <summary>
/// Janela "Umbra > Cenário > Montar cenário do PSD".
/// Pega a arte do PSD que está na cena, deixa você dizer o que cada camada é
/// (fundo, detalhe de parede, chão, móvel do fundo, móvel solto, frente, efeito...)
/// e monta o cenário 3D: chão deitado + paredes em pé com a pintura, móveis em pé,
/// caixas transparentes para subir e colisões das paredes.
/// Pode aplicar quantas vezes quiser: ele sempre volta ao PSD original e refaz.
/// </summary>
public class UmbraCenarioPSD : EditorWindow
{
    GameObject root;
    Vector2 scroll;
    string filtro = "";
    bool mostrarEscondidas;

    static readonly string[] RoleNames =
    {
        "Fundo (parede)", "Detalhe de parede", "Móvel do fundo", "Móvel solto", "Frente",
        "Efeito (luz)", "Chão", "Ignorar (some)", "Solto na parede",
    };
    static readonly string[] RoleHelp =
    {
        "vai para a pintura 3D, na parede",
        "cortina, quadro, porta pintada: vai para a pintura 3D (com \"Subir\": caixinha saindo da parede, ex.: pia)",
        "encostado no fundo: a Luma sobe e fica sempre na frente dele",
        "no meio do cômodo: a Luma passa na frente e atrás (divisória de cabine: marque \"Até parede\")",
        "primeiro plano: cobre a Luma",
        "luz/brilho: fica solto, não vira pintura",
        "chão, tapete: vai para a pintura 3D, deitado",
        "a camada some",
        "sprite solto colado na parede (ex.: criatura pintada)",
    };

    [MenuItem("Umbra/Cenário/Montar cenário do PSD...", priority = 70)]
    public static void Open()
    {
        var w = GetWindow<UmbraCenarioPSD>(false, "Umbra: cenário PSD");
        w.minSize = new Vector2(640, 560);
        w.root = FindArt();
    }

    /// <summary>A arte do PSD da cena aberta (a que tem as medidas do cômodo, ou a seleção).</summary>
    static GameObject FindArt()
    {
        if (Selection.activeGameObject != null)
        {
            var t = Selection.activeGameObject.transform;
            while (t != null) { if (t.GetComponent<CenarioPSD3D>() != null) return t.gameObject; t = t.parent; }
        }
        var cfg = Object.FindAnyObjectByType<CenarioPSD3D>(FindObjectsInactive.Include);
        if (cfg != null) return cfg.gameObject;
        return Selection.activeGameObject;
    }

    void OnSelectionChange() { if (root == null) { root = FindArt(); Repaint(); } }

    void OnGUI()
    {
        EditorGUILayout.LabelField("1. Cenário da cena atual", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        root = (GameObject)EditorGUILayout.ObjectField("Arte do PSD", root, typeof(GameObject), true);
        if (GUILayout.Button("Achar na cena", GUILayout.Width(110))) root = FindArt();
        EditorGUILayout.EndHorizontal();
        if (root == null)
        {
            EditorGUILayout.HelpBox("Abra uma cena montada (Umbra > Montar cena) ou selecione a arte do PSD na Hierarchy " +
                                    "(o objeto \"... (arte)\" dentro de \"Arte (coloque o PSB aqui)\").", MessageType.Info);
            return;
        }

        var cfg = root.GetComponent<CenarioPSD3D>();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Medidas do cômodo (frações da pintura: 0 = topo/esquerda, 1 = base/direita)", EditorStyles.boldLabel);
        if (cfg == null)
        {
            if (GUILayout.Button("Criar medidas do cômodo nesta arte")) { cfg = Undo.AddComponent<CenarioPSD3D>(root); cfg.nome = root.scene.name; }
        }
        else
        {
            var so = new SerializedObject(cfg);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("linhaDoChao"), new GUIContent("Linha do chão", "Onde o chão encontra a parede do fundo."));
            EditorGUILayout.PropertyField(so.FindProperty("profundidadeDoChao"), new GUIContent("Profundidade do chão (m)", "0 = automático."));
            EditorGUILayout.PropertyField(so.FindProperty("limiteDaFrente"), new GUIContent("Limite da frente", "Até onde a Luma chega perto da câmera."));
            EditorGUILayout.PropertyField(so.FindProperty("trechosDeParede"), new GUIContent("Trechos de parede (x0, x1, linha)"), true);
            EditorGUILayout.PropertyField(so.FindProperty("paredesLaterais"), new GUIContent("Paredes laterais (x fundo, y fundo, x frente, y frente)"), true);
            EditorGUILayout.PropertyField(so.FindProperty("amostraParede"), new GUIContent("Faixa de parede lisa (x0, x1)",
                "Pedaço da pintura com parede e chão lisos, repetido além das bordas e nas laterais dos degraus de parede."));
            EditorGUILayout.PropertyField(so.FindProperty("vaosNaParede"), new GUIContent("Vãos na parede (x0, x1, z) m",
                "Aberturas nas colisões da parede do fundo (ex.: entrada da escada 3D)."), true);
            EditorGUILayout.PropertyField(so.FindProperty("buracosNaPintura"), new GUIContent("Buracos na pintura (x0, x1, z) m",
                "Trechos da parede pintada que somem para mostrar o que foi modelado atrás (ex.: vão da escada 3D)."), true);
            EditorGUILayout.PropertyField(so.FindProperty("detectarMoveis"), new GUIContent("Detectar móveis sozinho"));
            so.ApplyModifiedProperties();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. O que cada camada é", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Detectar pelo nome (só as sem papel)")) Detect(false);
        if (GUILayout.Button("Detectar TODAS de novo")) { if (EditorUtility.DisplayDialog("Umbra", "Trocar o papel de todas as camadas pelo palpite automático?", "Sim", "Não")) Detect(true); }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        filtro = EditorGUILayout.TextField("Filtrar", filtro);
        mostrarEscondidas = EditorGUILayout.ToggleLeft("mostrar escondidas no PSD", mostrarEscondidas, GUILayout.Width(190));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Camada", EditorStyles.miniBoldLabel, GUILayout.Width(150));
        GUILayout.Label("Papel", EditorStyles.miniBoldLabel, GUILayout.Width(130));
        GUILayout.Label("Subir", EditorStyles.miniBoldLabel, GUILayout.Width(40));
        GUILayout.Label("Altura", EditorStyles.miniBoldLabel, GUILayout.Width(50));
        GUILayout.Label(new GUIContent("Beliche", "Caixa na cama de baixo e outra na cama de cima."), EditorStyles.miniBoldLabel, GUILayout.Width(48));
        GUILayout.Label(new GUIContent("Até parede", "Divisória: caixa da frente até a parede do fundo (a Luma entra no vão e fica atrás dela)."), EditorStyles.miniBoldLabel, GUILayout.Width(62));
        GUILayout.Label("Em cima de", EditorStyles.miniBoldLabel);
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(220));
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var info = sr.GetComponent<PsdLayerDepth>();
            bool escondida = info != null ? (info.hasOriginal && !info.originalActive) : !sr.gameObject.activeSelf;
            if (escondida && !mostrarEscondidas) continue;
            if (!string.IsNullOrEmpty(filtro) && !sr.name.ToLowerInvariant().Contains(filtro.ToLowerInvariant())) continue;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(sr.name + (escondida ? " (esc.)" : ""), EditorStyles.label, GUILayout.Width(150)))
            { Selection.activeGameObject = sr.gameObject; EditorGUIUtility.PingObject(sr.gameObject); }
            if (info == null)
            {
                GUILayout.Label("(sem papel: clique em Detectar)");
                EditorGUILayout.EndHorizontal();
                continue;
            }
            EditorGUI.BeginChangeCheck();
            var role = (Role)EditorGUILayout.Popup((int)info.role, RoleNames, GUILayout.Width(130));
            bool standing = role == Role.MovelFundo || role == Role.Movel || role == Role.Frente;
            bool solid = info.solid;
            float climb = info.climb;
            bool beliche = info.beliche, ateParede = info.ateParede;
            using (new EditorGUI.DisabledScope(!standing && role != Role.DetalheParede))
                solid = EditorGUILayout.Toggle(info.solid, GUILayout.Width(40));
            using (new EditorGUI.DisabledScope(!standing || !solid))
            {
                using (new EditorGUI.DisabledScope(info.beliche || info.ateParede))
                    climb = EditorGUILayout.FloatField(info.climb, GUILayout.Width(50));
                beliche = EditorGUILayout.Toggle(info.beliche, GUILayout.Width(48));
                ateParede = EditorGUILayout.Toggle(info.ateParede, GUILayout.Width(62));
            }
            string on = EditorGUILayout.TextField(info.onLayer ?? "");
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(info, "Papel da camada");
                info.role = role; info.solid = solid; info.climb = Mathf.Clamp(climb, 0.05f, 1f);
                if (beliche != info.beliche) { info.beliche = beliche; if (beliche) info.ateParede = false; }
                else if (ateParede != info.ateParede) { info.ateParede = ateParede; if (ateParede) info.beliche = false; }
                info.onLayer = on.Trim();
                EditorUtility.SetDirty(info);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<PsdLayerDepth>() : null;
        EditorGUILayout.HelpBox(sel != null
            ? sel.name + ": " + RoleHelp[(int)sel.role]
            : "Móvel do fundo = camas encostadas na parede (a Luma sobe e nunca fica atrás). " +
              "\"Subir\" cria a caixa transparente; \"Altura\" = até onde ela sobe (mesa 1). " +
              "\"Beliche\" = uma caixa na cama de baixo e outra na de cima (ajuste fino no Inspector da camada). " +
              "\"Até parede\" = divisória de cabine: parede sólida até o fundo, a Luma entra no vão e fica atrás dela. " +
              "Detalhe de parede com \"Subir\" = caixinha saindo da parede (pia). " +
              "\"Em cima de\" = nome de outra camada (vaso em cima da mesa).", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. Aplicar", EditorStyles.boldLabel);
        if (GUILayout.Button("Montar cenário 3D", GUILayout.Height(34))) Aplicar();
        if (GUILayout.Button("Voltar tudo ao PSD original (plano)")) Restore();
        EditorGUILayout.HelpBox("Depois de montar, salve a cena (Ctrl+S). Portas, pistas e dicas não mudam de lugar: " +
                                "se mexer muito na linha do chão, remonte a cena pelo menu Umbra > Montar cena.", MessageType.None);
    }

    void Detect(bool all)
    {
        if (root == null) return;
        var c = UmbraRooms.Remontar(root);            // mede tudo (e já monta)
        if (c == null) return;
        if (all)
        {
            foreach (var info in root.GetComponentsInChildren<PsdLayerDepth>(true))
            {
                Undo.RecordObject(info, "Detectar papel");
                UmbraRooms.Guess(info, c, c.opaque.TryGetValue(info.GetComponent<SpriteRenderer>(), out var r) ? r : new Rect(0, 0, 1, 1));
            }
            UmbraRooms.Remontar(root);
        }
    }

    void Aplicar()
    {
        if (root == null) return;
        Undo.RegisterFullObjectHierarchyUndo(root, "Montar cenário 3D");
        UmbraRooms.Remontar(root);
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    void Restore()
    {
        if (root == null) return;
        foreach (var info in root.GetComponentsInChildren<PsdLayerDepth>(true))
        {
            Undo.RecordObject(info.transform, "Voltar PSD");
            info.RestoreOriginal();
        }
        var p = root.transform.parent;
        var room = p != null && p.parent != null ? p.parent : p;
        var gen = room != null ? room.Find("(gerado) Cenário 3D") : null;
        if (gen != null) Undo.DestroyObjectImmediate(gen.gameObject);
        EditorSceneManager.MarkSceneDirty(root.scene);
    }
}
#endif
