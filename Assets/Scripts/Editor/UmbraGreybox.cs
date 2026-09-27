#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Ferramentas de greybox: salas prontas, blocos, "assentar no chão",
/// conversão de Plane em chão sólido e verificação de física.
/// </summary>
public static class UmbraGreybox
{
    const string MatFolder = "Assets/Dados/Greybox";

    // ------------------------------------------------------------ materiais

    public static Material GreyMaterial(string name, Color color)
    {
        EnsureFolder(MatFolder);
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material Structure => GreyMaterial("Greybox_Estrutura", new Color(0.55f, 0.55f, 0.58f));
    static Material Detail    => GreyMaterial("Greybox_Detalhe",   new Color(0.35f, 0.36f, 0.4f));

    // ------------------------------------------------------------ blocos

    public static GameObject Block(string name, Vector3 size, Transform parent, Vector3 localCenter, Material mat, bool visible = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.enabled = visible;
        go.isStatic = true;
        return go;
    }

    static GameObject SpawnBlock(string name, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.transform.position = SnapToGrid(ScenePivot()) + Vector3.up * (size.y * 0.5f + 5f);
        Undo.RegisterCreatedObjectUndo(go, "Umbra: " + name);
        DropToGround(go);
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        return go;
    }

    [MenuItem("Umbra/Greybox/Bloco 1 x 1 x 1", priority = 40)]
    static void MenuBlock() => SpawnBlock("Bloco", Vector3.one, Structure);

    [MenuItem("Umbra/Greybox/Plataforma 3 x 0,5 x 2", priority = 41)]
    static void MenuPlatform() => SpawnBlock("Plataforma", new Vector3(3f, 0.5f, 2f), Structure);

    [MenuItem("Umbra/Greybox/Parede 4 x 4 x 0,3", priority = 42)]
    static void MenuWall() => SpawnBlock("Parede", new Vector3(4f, 4f, 0.3f), Structure);

    [MenuItem("Umbra/Greybox/Mesa 2 x 0,9 x 1", priority = 43)]
    static void MenuTable() => SpawnBlock("Mesa", new Vector3(2f, 0.9f, 1f), Detail);

    [MenuItem("Umbra/Greybox/Cama 2,2 x 0,6 x 1", priority = 44)]
    static void MenuBed() => SpawnBlock("Cama", new Vector3(2.2f, 0.6f, 1f), Detail);

    [MenuItem("Umbra/Greybox/Escada (5 degraus)", priority = 45)]
    static void MenuStairs()
    {
        var root = new GameObject("Escada");
        root.transform.position = SnapToGrid(ScenePivot());
        Undo.RegisterCreatedObjectUndo(root, "Umbra: Escada");
        const int steps = 5;
        const float stepH = 0.3f, stepD = 0.5f, width = 1.5f;
        for (int i = 0; i < steps; i++)
        {
            float h = stepH * (i + 1);
            Block("Degrau " + (i + 1), new Vector3(stepD, h, width), root.transform,
                  new Vector3(i * stepD, h * 0.5f, 0f), Structure);
        }
        DropToGround(root);
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    // ------------------------------------------------------------ sala

    public class RoomSettings
    {
        public string name = "Sala";
        public float width = 12f, depth = 4f, height = 5f, thickness = 0.5f;
        public bool floor = true, back = true, left = true, right = true, ceiling = false, frontInvisible = true;
        public bool visibleWalls = true;
        public bool camera = false;
        public float cameraFov = 30f;
    }

    public static GameObject BuildRoom(RoomSettings s, Vector3 floorCenter)
    {
        var root = new GameObject(s.name);
        root.transform.position = floorCenter;
        Undo.RegisterCreatedObjectUndo(root, "Umbra: sala");

        var geo = new GameObject("Geometria").transform;
        geo.SetParent(root.transform, false);

        float W = s.width, D = s.depth, H = s.height, T = s.thickness;
        if (s.floor)   Block("Chao", new Vector3(W + 2 * T, T, D + 2 * T), geo, new Vector3(0, -T / 2, 0), Structure, true);
        if (s.back)    Block("Parede_Fundo", new Vector3(W + 2 * T, H, T), geo, new Vector3(0, H / 2, D / 2 + T / 2), Structure, s.visibleWalls);
        if (s.left)    Block("Parede_Esquerda", new Vector3(T, H, D), geo, new Vector3(-W / 2 - T / 2, H / 2, 0), Structure, s.visibleWalls);
        if (s.right)   Block("Parede_Direita", new Vector3(T, H, D), geo, new Vector3(W / 2 + T / 2, H / 2, 0), Structure, s.visibleWalls);
        if (s.ceiling) Block("Teto", new Vector3(W + 2 * T, T, D + 2 * T), geo, new Vector3(0, H + T / 2, 0), Structure, s.visibleWalls);
        if (s.frontInvisible)
            Block("Parede_Frente_Invisivel", new Vector3(W + 2 * T, H, T), geo, new Vector3(0, H / 2, -D / 2 - T / 2), Structure, false);

        var art = new GameObject("Arte (coloque o PSB aqui)");
        art.transform.SetParent(root.transform, false);

        if (s.camera) BuildRoomCamera(root.transform, s);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        return root;
    }

    static void BuildRoomCamera(Transform room, RoomSettings s)
    {
        // Distância para a largura da sala caber na tela 16:9.
        float halfV = s.cameraFov * 0.5f * Mathf.Deg2Rad;
        float tanH = Mathf.Tan(halfV) * (16f / 9f);
        float dist = (s.width * 0.5f) / Mathf.Max(tanH, 0.01f);

        var camGo = new GameObject("Camera_" + s.name);
        camGo.transform.SetParent(room, false);
        camGo.transform.localPosition = new Vector3(0f, s.height * 0.5f, -s.depth * 0.5f - dist);
        camGo.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        var cam = camGo.AddComponent<CinemachineCamera>();
        var lens = cam.Lens;
        lens.FieldOfView = s.cameraFov;
        cam.Lens = lens;
        cam.Priority = 0;

        var trig = new GameObject("Trigger_Camera");
        trig.transform.SetParent(room, false);
        trig.transform.localPosition = new Vector3(0f, s.height * 0.5f, 0f);
        var box = trig.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(s.width, s.height, s.depth + 1f);
        var zone = trig.AddComponent<CameraZone>();
        zone.zoneCamera = cam;
    }

    // ------------------------------------------------------------ assentar no chão

    [MenuItem("Umbra/Greybox/Assentar no chão _END", priority = 60)]
    public static void DropSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            Undo.RecordObject(go.transform, "Assentar no chão");
            if (!DropToGround(go)) Debug.LogWarning("[Umbra] Nada embaixo de " + go.name + " para apoiar.");
        }
    }

    /// <summary>Move o objeto para baixo/cima até a base encostar no colisor sólido embaixo dele.</summary>
    public static bool DropToGround(GameObject go)
    {
        if (!TryGetBounds(go, out Bounds b)) return false;
        Physics.SyncTransforms();

        var own = new HashSet<Collider>(go.GetComponentsInChildren<Collider>());
        Vector3 origin = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (own.Contains(h.collider)) continue;
            if (h.point.y > b.max.y) continue;
            if (h.point.y > best) best = h.point.y;
        }
        if (float.IsNegativeInfinity(best)) return false;

        go.transform.position += Vector3.up * (best - b.min.y + 0.001f);
        return true;
    }

    static bool TryGetBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool has = false;
        foreach (var c in go.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!has) { b = c.bounds; has = true; } else b.Encapsulate(c.bounds);
        }
        if (has) return true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return has;
    }

    // ------------------------------------------------------------ corrigir Plane

    [MenuItem("Umbra/Greybox/Converter Plane em chão sólido", priority = 61)]
    static void FixPlanes()
    {
        int fixedCount = 0;
        foreach (var go in Selection.gameObjects)
        {
            var mf = go.GetComponent<MeshFilter>();
            bool isPlane = mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Plane";
            Undo.RegisterFullObjectHierarchyUndo(go, "Chão sólido");

            var sc = go.transform.localScale;
            if (Mathf.Approximately(sc.y, 0f)) go.transform.localScale = new Vector3(sc.x, 1f, sc.z);

            foreach (var mc in go.GetComponents<MeshCollider>()) Undo.DestroyObjectImmediate(mc);
            foreach (var bc in go.GetComponents<BoxCollider>()) Undo.DestroyObjectImmediate(bc);

            var box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = false;
            float thick = 0.5f / Mathf.Max(go.transform.lossyScale.y, 0.0001f);
            if (isPlane)
            {
                box.size = new Vector3(10f, thick, 10f);   // o Plane da Unity mede 10 x 10
                box.center = new Vector3(0f, -thick / 2f, 0f);
            }
            else
            {
                var size = box.size;
                if (size.y < thick) { box.size = new Vector3(size.x, thick, size.z); box.center = new Vector3(0f, -thick / 2f, 0f); }
            }
            go.isStatic = true;
            fixedCount++;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }
        Debug.Log("[Umbra] " + fixedCount + " chão(s) convertido(s) em colisor sólido.");
    }

    // ------------------------------------------------------------ verificar física

    [MenuItem("Umbra/Verificar física", priority = 101)]
    static void CheckPhysics()
    {
        var problems = new List<string>();
        foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (c is CharacterController) continue;
            var ls = c.transform.lossyScale;
            if (Mathf.Abs(ls.x) < 0.0001f || Mathf.Abs(ls.y) < 0.0001f || Mathf.Abs(ls.z) < 0.0001f)
                problems.Add(c.name + ": escala zero em algum eixo (colisor sem espessura).");
            if (c is BoxCollider bc && !bc.isTrigger && (bc.size.x <= 0f || bc.size.y <= 0f || bc.size.z <= 0f))
                problems.Add(c.name + ": BoxCollider com tamanho zero.");
            if (c is MeshCollider mc && !mc.convex && c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                problems.Add(c.name + ": MeshCollider não convexo num Rigidbody (não funciona). Use Box ou marque Convex.");
            string n = c.name.ToLowerInvariant();
            if ((n.Contains("plane") || n.Contains("chao") || n.Contains("floor")) && c.isTrigger)
            {
                bool hasSolid = false;
                foreach (var other in c.GetComponents<Collider>()) if (!other.isTrigger && !(other is MeshCollider)) hasSolid = true;
                if (!hasSolid) problems.Add(c.name + ": parece um chão, mas o colisor é Trigger (não é sólido).");
            }
        }
        foreach (var p in Object.FindObjectsByType<Pushable>(FindObjectsSortMode.None))
        {
            bool solid = false;
            foreach (var c in p.GetComponentsInChildren<Collider>()) if (!c.isTrigger) solid = true;
            if (!solid) problems.Add(p.name + ": caixa sem colisor sólido.");
            var rb = p.GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic) problems.Add(p.name + ": Rigidbody com Is Kinematic marcado (não se move).");
        }

        if (problems.Count == 0) EditorUtility.DisplayDialog("Umbra", "Física OK.", "OK");
        else
        {
            foreach (var p in problems) Debug.LogWarning("[Umbra] " + p);
            EditorUtility.DisplayDialog("Umbra", problems.Count + " problema(s) de física:\n\n- " +
                string.Join("\n- ", problems.GetRange(0, Mathf.Min(15, problems.Count))) +
                (problems.Count > 15 ? "\n(veja o Console para o resto)" : ""), "OK");
        }
    }

    // ------------------------------------------------------------ utilitários

    public static Vector3 ScenePivot()
    {
        var sv = SceneView.lastActiveSceneView;
        return sv != null ? sv.pivot : Vector3.zero;
    }

    public static Vector3 SnapToGrid(Vector3 p, float step = 0.5f)
    {
        return new Vector3(Mathf.Round(p.x / step) * step, Mathf.Round(p.y / step) * step, Mathf.Round(p.z / step) * step);
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}

/// <summary>Janela "Umbra > Greybox > Criar sala...".</summary>
public class UmbraRoomWindow : EditorWindow
{
    UmbraGreybox.RoomSettings s = new UmbraGreybox.RoomSettings();

    [MenuItem("Umbra/Greybox/Criar sala...", priority = 39)]
    static void Open()
    {
        var w = GetWindow<UmbraRoomWindow>(true, "Umbra: criar sala");
        w.minSize = new Vector2(320, 380);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Sala (medidas em metros / unidades)", EditorStyles.boldLabel);
        s.name = EditorGUILayout.TextField("Nome", s.name);
        s.width = EditorGUILayout.FloatField("Largura (X)", s.width);
        s.depth = EditorGUILayout.FloatField("Profundidade (Z)", s.depth);
        s.height = EditorGUILayout.FloatField("Altura (Y)", s.height);
        s.thickness = EditorGUILayout.FloatField("Espessura", s.thickness);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Partes", EditorStyles.boldLabel);
        s.floor = EditorGUILayout.Toggle("Chão", s.floor);
        s.back = EditorGUILayout.Toggle("Parede do fundo", s.back);
        s.left = EditorGUILayout.Toggle("Parede esquerda", s.left);
        s.right = EditorGUILayout.Toggle("Parede direita", s.right);
        s.ceiling = EditorGUILayout.Toggle("Teto", s.ceiling);
        s.frontInvisible = EditorGUILayout.Toggle("Parede da frente invisível", s.frontInvisible);
        s.visibleWalls = EditorGUILayout.Toggle("Paredes visíveis (greybox)", s.visibleWalls);

        EditorGUILayout.Space();
        s.camera = EditorGUILayout.Toggle("Câmera fixa da sala (CameraZone)", s.camera);
        if (s.camera) s.cameraFov = EditorGUILayout.Slider("FOV da câmera", s.cameraFov, 15f, 60f);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("A sala é criada com o centro do chão no ponto central da Scene View, " +
                                "alinhado à grade de 0,5. O chão fica em Y = 0 da sala; a Luma anda entre " +
                                "Z = -profundidade/2 e +profundidade/2.", MessageType.Info);
        if (GUILayout.Button("Criar sala", GUILayout.Height(32)))
            UmbraGreybox.BuildRoom(s, UmbraGreybox.SnapToGrid(UmbraGreybox.ScenePivot()));
    }
}
#endif
