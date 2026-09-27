#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Monta cenas completas do Umbra com um clique, a partir da arte do PSD.
/// Menu: Umbra > Montar cena > ...
/// Tudo é calculado pelas medidas da arte: nada precisa ser ajustado a olho.
/// </summary>
public static class UmbraSceneBuilder
{
    const string QuartoPath = "Assets/Sprites/Cenarios/quarto.psd";
    const string LumaSpritePath = "Assets/Sprites/Main Character/Luma_01.png";
    const string LumaControllerPath = "Assets/Animations/Player.controller";
    const string CutoutMatPath = "Assets/Dados/Materiais/SpriteRecorte.mat";

    const float LumaHeight = 1.0f;     // altura da Luma (pequena diante do cenário, como em Little Nightmares)
    const float RoomDepth = 1.6f;      // faixa da frente em que a Luma anda (ela também pode ir atrás das camas)
    const float CameraFov = 30f;

    // =====================================================================
    // DORMITÓRIO 1
    // =====================================================================

    [MenuItem("Umbra/Montar cena/Dormitório 1 (cena completa)", priority = 5)]
    public static void BuildDormitorio1()
    {
        // Agora montado com o sistema 2.5D do chão pintado (UmbraRooms).
        UmbraRooms.BuildDormitorio1();
    }

    // =====================================================================
    // Peças
    // =====================================================================

    internal static void BuildVolume(string name, Color? filter = null, float exposure = 0f, float vignette = 0.25f)
    {
        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;

        UmbraGreybox.EnsureFolder("Assets/Dados/Perfis");
        string path = "Assets/Dados/Perfis/" + name + "_Volume.asset";
        AssetDatabase.DeleteAsset(path); // recria limpo a cada montagem
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(vignette);
        vig.smoothness.Override(0.45f);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.6f);
        bloom.threshold.Override(0.85f);

        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-18f);
        color.contrast.Override(8f);
        if (filter.HasValue) color.colorFilter.Override(filter.Value);
        if (exposure != 0f) color.postExposure.Override(exposure);

        var grain = profile.Add<FilmGrain>(true);
        grain.intensity.Override(0.25f);

        var chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.05f);
        var lensD = profile.Add<LensDistortion>(true);
        lensD.intensity.Override(0f);

        // Os efeitos precisam ser salvos DENTRO do asset do perfil, senão somem ao recarregar.
        foreach (var c in profile.components)
        {
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
        }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        vol.sharedProfile = profile;
        volGo.AddComponent<FearFX>();     // medo: perde cor, treme, pulsa
    }

    internal static GameObject BuildLuma(Vector3 position)
    {
        var go = new GameObject("Luma");
        go.tag = "Player";
        go.transform.position = position;

        var cc = go.AddComponent<CharacterController>();
        cc.height = LumaHeight;
        cc.radius = 0.2f;
        cc.center = new Vector3(0f, LumaHeight * 0.5f, 0f);
        cc.stepOffset = 0.3f;

        var mv = go.AddComponent<PlayerMovement>();
        mv.walkSpeed = 1.9f;
        mv.runSpeed = 3.7f;
        mv.jumpForce = 5.5f;      // pulo baixo de criança; para subir em caixas existe a escalada (ClimbAssist)
        mv.gravity = -20f;

        var st = go.AddComponent<PlayerState>();
        st.chestHeight = LumaHeight * 0.5f;

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(go.transform, false);
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        LumaTextureQuality();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LumaSpritePath);
        sr.sprite = sprite;
        var mat = PersonagemMaterial();
        if (mat != null) sr.sharedMaterial = mat;
        sr.sortingOrder = 100;          // depois dos móveis do fundo: a Luma sempre aparece na frente deles
        var anim = spriteGo.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LumaControllerPath);

        if (sprite != null)
        {
            float s = LumaHeight / Mathf.Max(sprite.bounds.size.y, 0.01f);
            spriteGo.transform.localScale = Vector3.one * s;
            spriteGo.transform.localPosition = new Vector3(0f, -sprite.bounds.min.y * s, 0f);
        }

        go.AddComponent<PlayerSpriteController>();
        go.AddComponent<ClimbAssist>();
        spriteGo.AddComponent<FaceCamera>();   // a câmera gira para seguir: o sprite acompanha
        return go;
    }

    /// <summary>
    /// Material da Luma: recorte com profundidade (os móveis da frente a cobrem certinho),
    /// mas na fila transparente, para ser desenhada DEPOIS dos móveis encostados no fundo.
    /// </summary>
    internal static Material PersonagemMaterial()
    {
        const string path = "Assets/Dados/Materiais/SpriteRecorte_Personagem.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var sh = Shader.Find("Umbra/Sprite Recorte");
            if (sh == null) return AssetDatabase.LoadAssetAtPath<Material>(CutoutMatPath);
            UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// Luma menor sem perder qualidade: sem compressão, com mipmaps e filtro trilinear
    /// (a imagem reduzida fica lisa, sem serrilhado nem borrão).
    /// </summary>
    static void LumaTextureQuality()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites/Main Character" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null || ti.textureType != TextureImporterType.Sprite) continue;
            bool changed = ti.textureCompression != TextureImporterCompression.Uncompressed || !ti.mipmapEnabled
                           || ti.filterMode != FilterMode.Trilinear || ti.anisoLevel < 4;
            if (!changed) continue;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = true;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.alphaIsTransparency = true;
            var std = ti.GetPlatformTextureSettings("Standalone");
            if (std.overridden) { std.textureCompression = TextureImporterCompression.Uncompressed; ti.SetPlatformTextureSettings(std); }
            ti.SaveAndReimport();
        }
    }

    /// <summary>Halo de luz aditivo em volta de uma lâmpada (some quando a luz apaga).</summary>
    internal static GameObject Halo(Transform parent, Color color, float size)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Brilho.png");
        var sh = Shader.Find("Umbra/Brilho Aditivo");
        var go = new GameObject("Halo");
        go.transform.SetParent(parent, false);
        if (sp == null || sh == null) return go;
        UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
        const string path = "Assets/Dados/Materiais/Brilho_Aditivo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sharedMaterial = mat;
        sr.color = new Color(color.r, color.g, color.b, 0.55f);
        go.transform.localScale = Vector3.one * (size / Mathf.Max(sp.bounds.size.x, 0.01f));
        go.AddComponent<FaceCamera>();
        return go;
    }

    internal static CinemachineCamera BuildFixedCamera(string name, Vector3 pos, Quaternion rot, Vector3 zoneCenter, Vector3 zoneSize, float fov = CameraFov)
    {
        var rig = GameObject.Find("Cameras");
        var camGo = new GameObject(name);
        if (rig != null) camGo.transform.SetParent(rig.transform, false);
        camGo.transform.SetPositionAndRotation(pos, rot);
        var vcam = camGo.AddComponent<CinemachineCamera>();
        var lens = vcam.Lens;
        lens.FieldOfView = fov;
        lens.FarClipPlane = 200f;
        vcam.Lens = lens;
        vcam.Priority = 0;

        var zoneGo = new GameObject("Zona_" + name);
        if (rig != null) zoneGo.transform.SetParent(rig.transform, false);
        zoneGo.transform.position = zoneCenter;
        var box = zoneGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = zoneSize;
        var zone = zoneGo.AddComponent<CameraZone>();
        zone.zoneCamera = vcam;
        zone.cut = true; // cena começa já enquadrada
        return vcam;
    }

    internal static LightZone BuildLightZone(Vector3 floorCenter, Vector3 size, GameObject visual,
                                             string name = "ZonaDeLuz_Lampada", Color? color = null,
                                             float intensity = 2f, float lightHeight = 3.4f, float range = 5f)
    {
        var go = new GameObject(name);
        go.transform.position = floorCenter;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = new Vector3(0f, size.y * 0.5f, 0f);

        var lightGo = new GameObject("Luz");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, lightHeight, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = range;
        light.intensity = intensity;
        light.color = color ?? new Color(1f, 0.78f, 0.82f);

        var zone = go.AddComponent<LightZone>();
        zone.linkedLight = light;
        var halo = Halo(lightGo.transform, color ?? new Color(1f, 0.78f, 0.82f), 1.6f);
        zone.linkedVisuals = visual != null ? new[] { visual, halo } : new[] { halo };
        return zone;
    }

    internal static GameObject BuildChest(Vector3 floorPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Bau (empurrável)";
        go.transform.localScale = new Vector3(0.8f, 0.7f, 0.6f);
        go.transform.position = floorPos + Vector3.up * 0.35f;
        go.GetComponent<MeshRenderer>().sharedMaterial =
            UmbraGreybox.GreyMaterial("Greybox_Madeira", new Color(0.22f, 0.16f, 0.13f));
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 20f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var p = go.AddComponent<Pushable>();
        p.lockZ = true;
        p.extraRange = 0.3f;
        return go;
    }

    internal static GameObject BuildExit(Vector3 floorPos, string nextScene)
    {
        var go = new GameObject("Saida_Porta");
        go.transform.position = floorPos;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.2f, 2.5f, 1.2f);
        box.center = new Vector3(0f, 1.25f, 0f);
        var exit = go.AddComponent<LevelExit>();
        exit.nextScene = nextScene;
        return go;
    }

    static void BuildLatch(Vector3 pos, GameObject exitToEnable)
    {
        var go = new GameObject("Trinco (alto)");
        go.transform.position = pos;
        var lk = go.AddComponent<ItemLock>();
        lk.prompt = "Abrir o trinco";
        lk.doneFlag = "dorm1_trinco";
        lk.doneMessage = "O trinco cede com um estalo.";
        lk.maxVerticalDistance = 1.0f;   // do chão (1,65) não alcança; em cima do baú (0,95) sim
        lk.extraRange = 0.4f;
        lk.activateOnDone = new[] { exitToEnable };
    }

    internal static Clue BuildClue(string id, string title, string text, ClueData.Origin origin, Vector3 pos,
                                   int arc = 1, string prompt = null)
    {
        UmbraGreybox.EnsureFolder("Assets/Dados/Pistas");
        string path = "Assets/Dados/Pistas/" + id + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<ClueData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ClueData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.id = id;
        data.title = title;
        data.text = text;
        data.origin = origin;
        data.arc = arc;
        EditorUtility.SetDirty(data);

        var go = new GameObject("Pista_" + id);
        go.transform.position = pos;
        var clue = go.AddComponent<Clue>();
        clue.data = data;
        if (!string.IsNullOrEmpty(prompt)) clue.prompt = prompt;

        // Brilho discreto para o jogador notar.
        var glow = new GameObject("Brilho");
        glow.transform.SetParent(go.transform, false);
        var l = glow.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 0.9f;
        l.intensity = 0.8f;
        l.color = new Color(1f, 0.85f, 0.55f);
        UmbraRooms.GlowSprite(glow.transform);
        clue.highlight = glow;
        return clue;
    }

    // =====================================================================
    // Utilitários
    // =====================================================================

    internal static Transform FindChild(Transform root, string exactName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Trim().ToLowerInvariant() == exactName) return t;
        return null;
    }

    /// <summary>Área da pintura inteira: usa as camadas de fundo (papel/parede), que cobrem o canvas.</summary>
    internal static Bounds CanvasBounds(GameObject art)
    {
        Bounds b = new Bounds(art.transform.position, Vector3.zero);
        bool has = false;
        foreach (var sr in art.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string n = sr.name.ToLowerInvariant();
            if (!(n.Contains("papel") || n.Contains("parede") || n.Contains("lado de fora"))) continue;
            if (!has) { b = sr.bounds; has = true; } else b.Encapsulate(sr.bounds);
        }
        if (!has)
            foreach (var sr in art.GetComponentsInChildren<SpriteRenderer>(true))
            { if (!has) { b = sr.bounds; has = true; } else b.Encapsulate(sr.bounds); }
        return b;
    }

    /// <summary>Altura (mundo) dos pés dos móveis pintados: mediana da base das camadas de camas/móveis.</summary>
    static float WalkLine(GameObject art, Bounds canvas)
    {
        var bottoms = new System.Collections.Generic.List<float>();
        foreach (var sr in art.GetComponentsInChildren<SpriteRenderer>())
        {
            string n = sr.name.ToLowerInvariant();
            if (!n.Contains("cama") || n.Contains("frente") || n.Contains("tras")) continue;
            bottoms.Add(sr.bounds.min.y);
        }
        if (bottoms.Count == 0) return canvas.min.y + canvas.size.y * 0.12f;
        bottoms.Sort();
        return bottoms[bottoms.Count / 2];
    }

    internal static void AddToBuild(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
#endif
