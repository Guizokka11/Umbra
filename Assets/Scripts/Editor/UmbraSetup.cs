#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Menu "Umbra" no editor. Monta a cena e cria as peças prontas dos puzzles,
/// já com as referências ligadas. Fica numa pasta Editor: não entra no build.
/// </summary>
public static class UmbraSetup
{
    const string CanvasName = "UI_Umbra";
    const string CluesFolder = "Assets/Dados/Pistas";

    // =====================================================================
    // 1. CONFIGURAR CENA
    // =====================================================================

    [MenuItem("Umbra/1. Configurar cena", priority = 0)]
    public static void ConfigureScene()
    {
        bool tmpReady = false;
        try { tmpReady = TMP_Settings.defaultFontAsset != null; } catch { tmpReady = false; }
        if (!tmpReady)
        {
            EditorUtility.DisplayDialog("Umbra",
                "Antes, importe os recursos do TextMeshPro:\n" +
                "Window > TextMeshPro > Import TMP Essential Resources.\n\n" +
                "Depois rode Umbra > 1. Configurar cena de novo.", "OK");
            return;
        }

        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Umbra",
                "Não achei a Luma. Coloque a tag \"Player\" no objeto Player (o que tem o PlayerMovement).", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Umbra: configurar cena");
        int group = Undo.GetCurrentGroup();

        // --- Player ---------------------------------------------------------
        if (!player.CompareTag("Player")) { Undo.RecordObject(player, "tag"); player.tag = "Player"; }
        var state = GetOrAdd<PlayerState>(player);
        var fear = GetOrAdd<FearSystem>(player);
        var interactor = GetOrAdd<PlayerInteractor>(player);

        // --- GameManager ----------------------------------------------------
        var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null)
        {
            var go = new GameObject("GameManager");
            Undo.RegisterCreatedObjectUndo(go, "GameManager");
            gm = go.AddComponent<GameManager>();
        }
        gm.player = state;
        if (gm.startPoint == null)
        {
            var sp = new GameObject("StartPoint");
            Undo.RegisterCreatedObjectUndo(sp, "StartPoint");
            sp.transform.SetParent(gm.transform, false);
            sp.transform.position = player.transform.position;
            gm.startPoint = sp.transform;
        }
        EditorUtility.SetDirty(gm);

        // --- UI -------------------------------------------------------------
        var canvas = BuildCanvas();
        var promptT = FindDeep(canvas.transform, "Prompt");
        if (promptT != null) interactor.promptLabel = promptT.GetComponent<TMP_Text>();
        EditorUtility.SetDirty(interactor);

        // --- Volume / Vignette ---------------------------------------------
        var volume = FindGlobalVolume();
        if (volume != null && volume.sharedProfile != null)
        {
            if (!volume.sharedProfile.TryGet(out Vignette vig))
            {
                vig = volume.sharedProfile.Add<Vignette>(true);
                if (AssetDatabase.Contains(volume.sharedProfile))
                {
                    vig.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(vig, volume.sharedProfile);
                }
            }
            vig.active = true;
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.2f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.4f;
            EditorUtility.SetDirty(volume.sharedProfile);
            fear.volume = volume;
            EditorUtility.SetDirty(fear);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(player.scene);

        Debug.Log("[Umbra] Cena configurada. Rode Umbra > Verificar cena para conferir.");
        if (volume == null)
            Debug.LogWarning("[Umbra] Nenhum Global Volume achado: a vinheta do medo ficou desligada. " +
                             "Crie um (GameObject > Volume > Global Volume) e rode de novo.");
        Selection.activeGameObject = player;
    }

    static Canvas BuildCanvas()
    {
        var existing = GameObject.Find(CanvasName);
        if (existing != null) return existing.GetComponent<Canvas>();

        var root = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(root, "UI");
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Texto de interação (ex.: "Esconder")
        var prompt = MakeText(root.transform, "Prompt", "Interagir", 34, TextAlignmentOptions.Center);
        SetRect(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(600, 60));
        prompt.gameObject.SetActive(false);

        // Painel de pistas
        var panel = MakeImage(root.transform, "CluePanel", new Color(0.05f, 0.04f, 0.06f, 0.92f));
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 720));
        var title = MakeText(panel.transform, "Titulo", "Título", 44, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1000, 70));
        var img = MakeImage(panel.transform, "Imagem", Color.white);
        SetRect(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(420, 300));
        img.preserveAspect = true;
        var body = MakeText(panel.transform, "Texto", "Texto da pista", 30, TextAlignmentOptions.TopLeft);
        SetRect(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(980, 260));
        var sun = MakeImage(panel.transform, "Sol", new Color(1f, 0.82f, 0.35f, 1f));
        SetRect(sun.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60, 60), new Vector2(48, 48));
        var hint = MakeText(panel.transform, "Fechar", "E / Esc para fechar", 22, TextAlignmentOptions.Center);
        SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 25), new Vector2(500, 36));
        hint.color = new Color(1f, 1f, 1f, 0.5f);

        var clueUI = root.AddComponent<ClueUI>();
        clueUI.root = panel.gameObject;
        clueUI.titleText = title;
        clueUI.bodyText = body;
        clueUI.image = img;
        clueUI.sunIcon = sun.gameObject;
        panel.gameObject.SetActive(false);

        // Overlay das memórias
        var mem = MakeImage(root.transform, "MemoryOverlay", new Color(0.9f, 0.88f, 0.85f, 0.15f));
        Stretch(mem.rectTransform);
        var memGroup = mem.gameObject.AddComponent<CanvasGroup>();
        memGroup.alpha = 0f;
        memGroup.blocksRaycasts = false;
        var memImg = MakeImage(mem.transform, "MemoryImage", Color.white);
        SetRect(memImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1200, 600));
        memImg.preserveAspect = true;
        var memText = MakeText(mem.transform, "MemoryText", "", 36, TextAlignmentOptions.Center);
        SetRect(memText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1400, 120));
        memText.fontStyle = FontStyles.Italic;
        mem.gameObject.SetActive(false);

        // Fade (por último = desenhado por cima de tudo)
        var fader = MakeImage(root.transform, "Fader", Color.black);
        Stretch(fader.rectTransform);
        fader.raycastTarget = false;
        var faderGroup = fader.gameObject.AddComponent<CanvasGroup>();
        faderGroup.alpha = 0f;           // transparente no editor; o ScreenFader escurece ao dar Play
        faderGroup.blocksRaycasts = false;
        fader.gameObject.AddComponent<ScreenFader>();

        return canvas;
    }

    // =====================================================================
    // 2. CRIAR PEÇAS
    // =====================================================================

    [MenuItem("Umbra/Criar/Zona de luz (lâmpada)", priority = 20)]
    static void CreateLightZone()
    {
        var go = NewObject("ZonaDeLuz");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 4f, 3f);
        box.center = new Vector3(0f, 2f, 0f);

        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 3.5f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 5f;
        light.intensity = 3f;
        light.color = new Color(1f, 0.85f, 0.75f);

        var zone = go.AddComponent<LightZone>();
        zone.linkedLight = light;
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Caixa empurrável", priority = 21)]
    static void CreatePushable()
    {
        var go = Primitive(PrimitiveType.Cube, "CaixaEmpurravel", new Vector3(0.8f, 0.8f, 0.8f));
        go.transform.position += Vector3.up * 3f;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 20f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        go.AddComponent<Pushable>();
        if (!UmbraGreybox.DropToGround(go))
            Debug.LogWarning("[Umbra] Não achei chão sólido embaixo da caixa. Rode Umbra > Verificar física.");
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Esconderijo (armário)", priority = 22)]
    static void CreateHidingSpot()
    {
        var go = NewObject("Esconderijo");
        var visual = Primitive(PrimitiveType.Cube, "Visual (troque pela arte)", new Vector3(1.2f, 2f, 0.6f));
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0.4f);

        var hide = new GameObject("HidePoint").transform;
        hide.SetParent(go.transform, false);
        hide.localPosition = new Vector3(0f, 0.1f, 0.4f);
        var exit = new GameObject("ExitPoint").transform;
        exit.SetParent(go.transform, false);
        exit.localPosition = new Vector3(0f, 0.1f, -0.6f);

        var spot = go.AddComponent<HidingSpot>();
        spot.hidePoint = hide;
        spot.exitPoint = exit;
        spot.interactionPoint = exit;
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Pista (com asset ClueData)", priority = 23)]
    static void CreateClue()
    {
        EnsureFolder(CluesFolder);
        var data = ScriptableObject.CreateInstance<ClueData>();
        string path = AssetDatabase.GenerateUniqueAssetPath(CluesFolder + "/Pista_.asset");
        AssetDatabase.CreateAsset(data, path);
        data.id = System.IO.Path.GetFileNameWithoutExtension(path);
        data.title = "Nova pista";
        data.text = "Escreva aqui o texto da pista.";
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        var go = Primitive(PrimitiveType.Quad, "Pista", new Vector3(0.35f, 0.35f, 1f));
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position += Vector3.up * 0.8f;
        var clue = go.AddComponent<Clue>();
        clue.data = data;
        Finish(go);
        EditorGUIUtility.PingObject(data);
    }

    [MenuItem("Umbra/Criar/Checkpoint", priority = 24)]
    static void CreateCheckpoint()
    {
        var go = NewObject("Checkpoint");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 3f, 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
        go.AddComponent<Checkpoint>();
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Saída de área (troca de cena)", priority = 25)]
    static void CreateLevelExit()
    {
        var go = NewObject("SaidaDeArea");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 3f, 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
        go.AddComponent<LevelExit>();
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Criatura com patrulha", priority = 26)]
    static void CreateCreature()
    {
        var go = Primitive(PrimitiveType.Capsule, "Criatura", new Vector3(0.9f, 1.1f, 0.9f));
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position += Vector3.up * 1.1f;

        var eyes = new GameObject("Olhos").transform;
        eyes.SetParent(go.transform, false);
        eyes.localPosition = new Vector3(0f, 0.6f, 0f);

        var parent = new GameObject("Criatura_Waypoints").transform;
        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Waypoints");
        parent.position = go.transform.position - Vector3.up * 1.1f;
        var a = new GameObject("A").transform; a.SetParent(parent, false); a.localPosition = new Vector3(-3f, 0f, 0f);
        var b = new GameObject("B").transform; b.SetParent(parent, false); b.localPosition = new Vector3(3f, 0f, 0f);

        var ai = go.AddComponent<CreatureAI>();
        ai.eyes = eyes;
        ai.waypoints = new[] { a, b };
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Memória (fragmento)", priority = 27)]
    static void CreateMemory()
    {
        var go = NewObject("Memoria");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 3f, 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
        var mem = go.AddComponent<MemoryTrigger>();
        mem.frames = new[] { new MemoryTrigger.Frame { line = "...", duration = 2.5f } };

        var canvas = GameObject.Find(CanvasName);
        if (canvas != null)
        {
            var overlay = FindDeep(canvas.transform, "MemoryOverlay");
            if (overlay != null)
            {
                mem.overlay = overlay.GetComponent<CanvasGroup>();
                mem.frameImage = FindDeep(overlay, "MemoryImage").GetComponent<Image>();
                mem.frameText = FindDeep(overlay, "MemoryText").GetComponent<TMP_Text>();
            }
        }
        else Debug.LogWarning("[Umbra] Rode Umbra > 1. Configurar cena antes, para a memória achar o overlay.");
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Interruptor ou alavanca", priority = 28)]
    static void CreateSwitch()
    {
        var go = Primitive(PrimitiveType.Cube, "Interruptor", new Vector3(0.2f, 0.3f, 0.1f));
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position += Vector3.up * 1.2f;
        var si = go.AddComponent<SimpleInteractable>();
        si.prompt = "Usar";
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Placa de pressão (contrapeso)", priority = 29)]
    static void CreatePlate()
    {
        var go = Primitive(PrimitiveType.Cube, "PlacaDePressao", new Vector3(1.5f, 0.1f, 1.5f));
        var col = go.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 10f, 1f);
        col.center = new Vector3(0f, 5f, 0f);
        go.AddComponent<PressurePlate>();
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Emissor de ruído (distração)", priority = 30)]
    static void CreateNoise()
    {
        var go = NewObject("Ruido");
        go.AddComponent<NoiseEmitter>();
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Tábua que range", priority = 31)]
    static void CreateCreakyBoard()
    {
        var go = NewObject("TabuaQueRange");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.8f, 1f, 1.5f);
        box.center = new Vector3(0f, 0.5f, 0f);
        var n = go.AddComponent<NoiseEmitter>();
        n.emitOnPlayerEnter = true;
        n.onlyWhenRunning = true;
        n.radius = 7f;
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Cadeado de 3 símbolos", priority = 32)]
    static void CreateSymbolLock()
    {
        var go = NewObject("CadeadoDeSimbolos");
        var lk = go.AddComponent<SymbolLock>();
        var dials = new List<SymbolDial>();
        for (int i = 0; i < 3; i++)
        {
            var d = Primitive(PrimitiveType.Quad, "Roda " + (i + 1), new Vector3(0.25f, 0.25f, 1f));
            Object.DestroyImmediate(d.GetComponent<Collider>());
            Object.DestroyImmediate(d.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(d.GetComponent<MeshFilter>());
            d.AddComponent<SpriteRenderer>();
            d.transform.SetParent(go.transform, false);
            d.transform.localPosition = new Vector3(-0.35f + i * 0.35f, 1f, 0f);
            var dial = d.AddComponent<SymbolDial>();
            dial.extraRange = -0.6f; // cada roda só responde de perto
            dials.Add(dial);
        }
        lk.dials = dials.ToArray();
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Gatilho (evento ao entrar)", priority = 34)]
    static void CreateTriggerEvent()
    {
        var go = NewObject("Gatilho");
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 3f, 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
        go.AddComponent<TriggerEvent>();
        Finish(go);
    }

    [MenuItem("Umbra/Criar/Porta final (confronto)", priority = 33)]
    static void CreateFinalDoor()
    {
        var go = NewObject("PortaFinal");
        var door = Primitive(PrimitiveType.Cube, "Porta", new Vector3(1.4f, 2.6f, 0.15f));
        door.transform.SetParent(go.transform, false);
        door.transform.localPosition = new Vector3(0.9f, 1.3f, 0f);
        var fd = go.AddComponent<FinalDoor>();
        fd.door = door.transform;
        fd.openLocalPos = door.transform.localPosition;
        fd.closedLocalPos = new Vector3(0f, 1.3f, 0f);
        Finish(go);
    }

    // =====================================================================
    // 3. VERIFICAR
    // =====================================================================

    [MenuItem("Umbra/Verificar cena", priority = 100)]
    public static void Validate()
    {
        var problems = new List<string>();
        var player = FindPlayer();
        if (player == null) problems.Add("Nenhum objeto com a tag Player.");
        else
        {
            if (player.GetComponent<CharacterController>() == null) problems.Add("Player sem CharacterController.");
            if (player.GetComponent<PlayerMovement>() == null) problems.Add("Player sem PlayerMovement.");
            if (player.GetComponent<PlayerState>() == null) problems.Add("Player sem PlayerState.");
            if (player.GetComponent<FearSystem>() == null) problems.Add("Player sem FearSystem.");
            var pi = player.GetComponent<PlayerInteractor>();
            if (pi == null) problems.Add("Player sem PlayerInteractor.");
            else if (pi.promptLabel == null) problems.Add("PlayerInteractor sem texto de prompt.");
            if (player.GetComponentInChildren<Animator>() == null) problems.Add("Player sem Animator no filho (sprite).");
        }
        if (Object.FindAnyObjectByType<GameManager>() == null) problems.Add("Sem GameManager.");
        if (Object.FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Include) == null) problems.Add("Sem ScreenFader.");
        if (Object.FindAnyObjectByType<ClueUI>(FindObjectsInactive.Include) == null) problems.Add("Sem ClueUI (painel de pistas).");
        if (FindGlobalVolume() == null) problems.Add("Sem Global Volume (a vinheta do medo não aparece).");

        foreach (var c in Object.FindObjectsByType<Clue>(FindObjectsSortMode.None))
            if (c.data == null) problems.Add("Pista sem ClueData: " + c.name);
        foreach (var h in Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
            if (h.hidePoint == null) problems.Add("Esconderijo sem HidePoint: " + h.name);
        foreach (var ai in Object.FindObjectsByType<CreatureAI>(FindObjectsSortMode.None))
            if ((ai.waypoints == null || ai.waypoints.Length == 0) && !ai.startIdle)
                problems.Add("Criatura sem waypoints (vai ficar parada): " + ai.name);
        foreach (var m in Object.FindObjectsByType<MemoryTrigger>(FindObjectsSortMode.None))
            if (m.overlay == null) problems.Add("Memória sem overlay: " + m.name);
        foreach (var ex in Object.FindObjectsByType<LevelExit>(FindObjectsSortMode.None))
            if (string.IsNullOrEmpty(ex.nextScene)) problems.Add("Saída de área sem nome de cena: " + ex.name);

        if (problems.Count == 0)
        {
            Debug.Log("[Umbra] Cena OK: nada faltando.");
            EditorUtility.DisplayDialog("Umbra", "Cena OK: nada faltando.", "OK");
        }
        else
        {
            foreach (var p in problems) Debug.LogWarning("[Umbra] " + p);
            EditorUtility.DisplayDialog("Umbra", problems.Count + " problema(s):\n\n- " + string.Join("\n- ", problems), "OK");
        }
    }

    // =====================================================================
    // Utilitários
    // =====================================================================

    static GameObject FindPlayer()
    {
        GameObject p = null;
        try { p = GameObject.FindWithTag("Player"); } catch { }
        if (p != null) return p;
        var mv = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        return mv != null ? mv.gameObject : null;
    }

    static Volume FindGlobalVolume()
    {
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v.isGlobal) return v;
        return null;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    static Vector3 SpawnPosition()
    {
        var sv = SceneView.lastActiveSceneView;
        Vector3 p = sv != null ? sv.pivot : Vector3.zero;
        var player = FindPlayer();
        if (player != null) p.z = player.transform.position.z; // mesmo plano da Luma
        p.y = player != null ? player.transform.position.y - 0.05f : 0f;
        return p;
    }

    static GameObject NewObject(string name)
    {
        var go = new GameObject(name);
        go.transform.position = SpawnPosition();
        return go;
    }

    static GameObject Primitive(PrimitiveType type, string name, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = SpawnPosition();
        go.transform.localScale = scale;
        return go;
    }

    static void Finish(GameObject go)
    {
        Undo.RegisterCreatedObjectUndo(go, "Umbra: criar " + go.name);
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var r = FindDeep(child, name);
            if (r != null) return r;
        }
        return null;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = new Color(0.92f, 0.9f, 0.88f);
        t.raycastTarget = false;
        return t;
    }

    static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
