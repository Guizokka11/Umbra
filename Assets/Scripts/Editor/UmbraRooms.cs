#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
/// Monta os cômodos a partir dos PSDs, já com puzzles, criaturas, portas, pistas e dicas.
/// Menu: Umbra > Montar cena > ...   (ou "TODAS as cenas").
///
/// CENÁRIO 3D (v3): a pintura de fundo (parede, chão, cortinas...) é "assada" numa textura e
/// PROJETADA sobre um chão deitado e paredes em pé de verdade. Da câmera fica idêntico à pintura,
/// mas é 3D: o chão tem profundidade, as paredes laterais pintadas viram paredes com colisão
/// (a Luma não "sobe" nelas) e, quando a câmera anda, tudo se move junto com paralaxe correta.
/// Móveis (camas, mesa, baús) ficam em pé no chão, cada um na sua profundidade; os encostados
/// no fundo ganham uma CAIXA TRANSPARENTE (colisão) da frente até a parede: a Luma sobe neles
/// e nunca fica atrás deles.
///
/// Posições em FRAÇÕES da pintura: x 0 = borda esquerda, 1 = direita; y 0 = topo, 1 = base.
/// </summary>
public static class UmbraRooms
{
    const string Cen = "Assets/Sprites/Cenarios/";
    const string Criaturas = "Assets/Sprites/Criaturas/";
    const string Out3D = "Assets/Dados/Cenario3D/";
    const float Fov = 24f;
    const float View = 0.46f;            // metade da altura da pintura que a câmera mostra (corta 4% em cima/embaixo)

    // =====================================================================
    // Especificação de um cômodo
    // =====================================================================

    public class Spec
    {
        public string scene, psd, artName;
        public float scale = 1f;
        public float roomDepth = 1.6f;
        public string[] hide = new string[0];
        public Dictionary<string, float> opacity = new Dictionary<string, float>();
        public Color ambient = new Color(0.14f, 0.12f, 0.17f);
        public Color fog = new Color(0.05f, 0.04f, 0.07f);
        public Color? filter;
        public float exposure = 0f;
        public float vignette = 0.25f;
        public float defaultSpawnX = 0.08f;
        public int copies = 1;

        [Tooltip("Linha (fração do topo) onde o chão encontra a parede do fundo.")]
        public float wallBase = 0.8f;
        [Tooltip("Trechos com a parede do fundo em outra linha: (x0, x1, linha do chão daquela parede).")]
        public List<Vector3> backSegments = new List<Vector3>();
        [Tooltip("Paredes laterais pintadas em perspectiva: (x no fundo, y no fundo, x na frente, y na frente) da linha onde a parede toca o chão.")]
        public List<Vector4> sideWalls = new List<Vector4>();
        [Tooltip("Profundidade do chão pintado (m), da parede do fundo até a borda de baixo da pintura. <= 0: 65% da altura da pintura.")]
        public float floorDepth = -1f;
        [Tooltip("Até onde a Luma chega perto da câmera (fração do topo).")]
        public float frontLimit = 0.95f;
        public Dictionary<string, LayerCfg> layers = new Dictionary<string, LayerCfg>();
        [Tooltip("Quanto o escuro cobre a cena longe das luzes (0 = nada, 1 = preto).")]
        public float darkness = 0.65f;
        [Tooltip("Faixa (x0, x1) de parede lisa repetida além da pintura e nas laterais dos degraus de parede.")]
        public Vector2 wallPatch;
        [Tooltip("Camadas cortadas em pedaços: camada → pedaços.")]
        public Dictionary<string, SplitPart[]> splits = new Dictionary<string, SplitPart[]>();
        [Tooltip("Camadas sem configuração que descem abaixo da linha do chão viram objetos em pé.")]
        public bool autoStand = true;
    }

    /// <summary>
    /// Auto: decide pelo nome/posição. Back: faz parte da pintura de fundo (projetada no 3D).
    /// Stand: objeto em pé no chão. On: em cima de outro objeto (vaso na mesa).
    /// Effect: luz/brilho (plano solto). Overlay: sombra geral (vai para o fundo).
    /// Flat: sprite solto no plano da parede (ex.: a Inspetora pintada, que vira criatura). Hide: some.
    /// </summary>
    public enum L { Auto, Back, Stand, On, Effect, Overlay, Flat, Hide }

    public class LayerCfg
    {
        public L role = L.Auto;
        public string on;
        public bool solid;              // ganha caixa de colisão (dá para subir)
        public bool back;               // encostado no fundo: caixa vai até a parede e a Luma fica sempre na frente
        public float climb = 1f;        // altura da caixa em fração da camada (beliche ~0,38)
        public float thick = 0.8f;      // profundidade da caixa quando NÃO vai até a parede
        public bool bunk;               // beliche: caixa na cama de baixo e na cama de cima
        public bool toWall;             // caixa da frente até a parede (divisória de cabine)
        public Rect rect;
        public bool hasRect;
        public LayerCfg Rect(float x0, float y0, float x1, float y1) { rect = new Rect(x0, y0, x1 - x0, y1 - y0); hasRect = true; return this; }
    }
    /// <summary>Móvel encostado no fundo: caixa transparente da frente até a parede; a Luma sobe e nunca passa por trás.</summary>
    static LayerCfg Solid(float climb = 1f) => new LayerCfg { role = L.Stand, solid = true, back = true, climb = climb };
    /// <summary>Móvel solto no meio do cômodo: caixa com profundidade própria; dá para passar por trás.</summary>
    static LayerCfg SolidFree(float climb = 1f, float thick = 0.8f) => new LayerCfg { role = L.Stand, solid = true, climb = climb, thick = thick };
    static LayerCfg On(string parent) => new LayerCfg { role = L.On, on = parent };
    static LayerCfg As(L role) => new LayerCfg { role = role };
    /// <summary>Em pé, encostado no fundo, sem caixa (divisórias, portas pintadas que descem até o chão).</summary>
    static LayerCfg Back() => new LayerCfg { role = L.Stand, back = true };
    /// <summary>Beliche encostado no fundo: caixa na cama de baixo e outra na cama de cima.</summary>
    static LayerCfg Bunk() => new LayerCfg { role = L.Stand, solid = true, back = true, bunk = true };
    /// <summary>Divisória de cabine/chuveiro: em pé, cobre a Luma quando ela entra, parede sólida até o fundo.</summary>
    static LayerCfg Divider() => new LayerCfg { role = L.Stand, solid = true, toWall = true };
    /// <summary>Solto em pé (cortina, lixeira): cobre a Luma quando ela passa atrás, sem caixa.</summary>
    static LayerCfg Loose() => new LayerCfg { role = L.Stand, solid = false, thick = 0f };
    /// <summary>Continua na pintura da parede, mas ganha uma caixa pequena saindo da parede (pias).</summary>
    static LayerCfg WallBox() => new LayerCfg { role = L.Back, solid = true };

    /// <summary>
    /// Corta uma camada em pedaços (corte c0..c1 da pintura), cada um com seu papel (ex.: privada ≠ divisória).
    /// x0..x1, y0..y1 = parte desenhada do pedaço (medida da caixa de colisão e da profundidade).
    /// </summary>
    public class SplitPart { public string name; public float c0, c1, x0, x1, y0, y1; public LayerCfg cfg; }
    static SplitPart Part(string name, float c0, float c1, float x0, float x1, float y0, float y1, LayerCfg cfg) =>
        new SplitPart { name = name, c0 = c0, c1 = c1, x0 = x0, x1 = x1, y0 = y0, y1 = y1, cfg = cfg };

    // =====================================================================
    // Contexto: geometria da pintura em 3D
    // =====================================================================

    public class Ctx
    {
        public Spec spec;
        public CenarioPSD3D cfg;                   // medidas do cômodo (na arte; editáveis na janela)
        public Transform gen;                      // tudo que o cenário 3D gerou (remontar apaga e refaz)
        public Dictionary<SpriteRenderer, Rect> opaque = new Dictionary<SpriteRenderer, Rect>();
        public float shiftY;
        public string Name => cfg != null && !string.IsNullOrEmpty(cfg.nome) ? cfg.nome : (spec != null ? spec.scene : "cena");
        /// <summary>Quanto a câmera anda (menos de 1: o que está perto dela não mostra além da pintura).</summary>
        public float Travel => Mathf.Lerp(1f, (D0 + FloorAt(0.5f, 1f).z) / D0, 0.6f);
        public Scene scene;
        public GameObject room, art, luma;
        public Camera cam;
        public Bounds canvas;                      // pintura no plano z = 0 (linha do chão do fundo em y = 0)
        public float D0 = 10f;                     // distância câmera → plano da pintura
        public float cy = 2f;                      // altura da câmera (= linha do horizonte da pintura)
        public float vh = 0.3f;                    // horizonte em fração do topo (pode ser < 0)
        public float frontZ = -3f;                 // limite da frente onde a Luma anda
        public bool painted = true;
        public Dictionary<string, float> layerZ = new Dictionary<string, float>();
        public HashSet<string> backLayers = new HashSet<string>();
        public List<SpriteRenderer> backSprites = new List<SpriteRenderer>();
        /// <summary>Móveis do fundo com caixa: z da parte de trás da caixa (Luma atrás disso = móvel na frente dela).</summary>
        public Dictionary<SpriteRenderer, float> behindZ = new Dictionary<SpriteRenderer, float>();
        public Dictionary<string, float> behindZByName = new Dictionary<string, float>();

        public float X(float f) => canvas.min.x + f * canvas.size.x;
        public float Y(float v) => canvas.max.y - v * canvas.size.y;

        /// <summary>Linha (fração do topo) onde parede e chão se encontram na coluna u da pintura.</summary>
        public float FloorTop(float u)
        {
            float cu = Mathf.Clamp01(u);
            float v = cfg.linhaDoChao;
            foreach (var seg in cfg.trechosDeParede) if (cu >= seg.x && cu <= seg.y) v = seg.z;
            foreach (var w in cfg.paredesLaterais)
            {
                if (Mathf.Abs(w.z - w.x) < 1e-4f) continue;
                bool left = w.z < w.x;                    // parede da esquerda: a frente fica mais à esquerda
                if (left ? u > w.x : u < w.x) continue;
                float vl = w.y + (u - w.x) / (w.z - w.x) * (w.w - w.y);
                v = Mathf.Max(v, vl);
            }
            return Mathf.Clamp(v, vh + 0.03f, 1f);
        }

        /// <summary>Ponto do chão 3D que aparece no ponto (u, v) da pintura.</summary>
        public Vector3 FloorAt(float u, float v)
        {
            float yv = Mathf.Min(Y(v), cy - 0.01f);
            float t = cy / (cy - yv);
            return new Vector3(t * X(u), 0f, -D0 + t * D0);
        }
        public Vector3 WallBase(float u) => FloorAt(u, FloorTop(u));
        public float WallZ(float u) => WallBase(u).z;
        /// <summary>Chão logo na frente da parede na coluna u (inset em fração da pintura).</summary>
        public Vector3 WallFoot(float u, float inset = 0.04f) => FloorAt(u, Mathf.Min(FloorTop(u) + inset, 1f));
        /// <summary>Profundidade na faixa andável: z -0,8 = frente, 0 = meio, 0,9 = encostado na parede.</summary>
        public float ZAt(float fx, float z)
        {
            float zb = WallZ(fx) - 0.35f, zf = frontZ + 0.3f;
            if (zb < zf + 0.1f) zb = zf + 0.1f;
            return Mathf.LerpUnclamped(zf, zb, (z + 0.8f) / 1.7f);
        }
        /// <summary>Ponto em profundidade z que aparece na coluna fx da pintura.</summary>
        public Vector3 AtDepth(float fx, float y, float z) => new Vector3(X(fx) * (D0 + z) / D0, y, z);
        public Vector3 Floor(float fx, float z = 0f) => AtDepth(fx, 0f, ZAt(fx, z));
        public Vector3 P(float fx, float y, float z) => AtDepth(fx, y, ZAt(fx, z));
        public float Z(float z) => ZAt(0.5f, z);
        /// <summary>Altura real (m) de um ponto pintado na linha v, se estiver na profundidade z.</summary>
        public float PaintY(float v, float z) => cy + (D0 + z) / D0 * (Y(v) - cy);
        /// <summary>Ponto pintado (u, v) colado na parede (um pouco à frente dela).</summary>
        public Vector3 OnWall(float u, float v, float outFromWall = 0.08f)
        {
            float z = WallZ(u) - outFromWall;
            return new Vector3(X(u) * (D0 + z) / D0, PaintY(v, z), z);
        }
        public float LayerZ(string layer, float fallback = -1f) => layerZ.TryGetValue(layer, out var z) ? z : fallback;
    }

    // =====================================================================
    // Menu
    // =====================================================================

    [MenuItem("Umbra/Montar cena/TODAS as cenas (jogo completo)", priority = 1)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildMenu();
        BuildPesadelo();
        BuildDormitorio1();
        BuildCorredor1();
        BuildBanheiro();
        BuildBiblioteca();
        BuildLazyRoom();
        BuildDormitorio2();
        BuildLivingRoom();
        BuildCorredor2();
        BuildAndar3();
        BuildEscritorio();
        OrderBuildScenes();
        EditorSceneManager.OpenScene("Assets/Scenes/00_Menu.unity");
        Debug.Log("[Umbra] Todas as cenas montadas. Abra 00_Menu e dê Play.");
    }

    [MenuItem("Umbra/Montar cena/00 Menu", priority = 3)]
    public static void BuildMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = Fov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.1f, 0.08f, 0.12f);
        UmbraSceneBuilder.BuildVolume("Menu", new Color(0.8f, 0.72f, 0.85f), -0.6f, 0.45f);

        var psd = AssetDatabase.LoadAssetAtPath<GameObject>(Cen + "quarto.psd");
        if (psd != null)
        {
            var art = (GameObject)PrefabUtility.InstantiatePrefab(psd);
            art.name = "Fundo (quarto)";
            var b = UmbraSceneBuilder.CanvasBounds(art);
            art.transform.position -= new Vector3(b.center.x, b.center.y, 0f);
            float dist = (b.size.y * 0.5f) / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            camGo.transform.position = new Vector3(0f, 0f, -dist);
        }
        var menu = new GameObject("Menu").AddComponent<MainMenu>();
        UmbraGreybox.EnsureFolder("Assets/Dados/Menu");
        const string textosPath = "Assets/Dados/Menu/TextosDoMenu.asset";
        var textos = AssetDatabase.LoadAssetAtPath<MenuTexts>(textosPath);
        if (textos == null)
        {
            textos = ScriptableObject.CreateInstance<MenuTexts>();
            textos.creditos = "Ana Clara de Meneses Canela · Guilherme Machado Freire · João Gabriel Inouye de Souza · S. Mayumi Sakuma · Vinicius L. Souza";
            AssetDatabase.CreateAsset(textos, textosPath);
            AssetDatabase.SaveAssets();
        }
        menu.textos = textos;
        Save(scene, "00_Menu");
    }

    // =====================================================================
    // 00 PESADELO  —  o corredor distorcido, Amelie sendo levada
    // =====================================================================

    [MenuItem("Umbra/Montar cena/00 Pesadelo (prólogo)", priority = 4)]
    public static void BuildPesadelo()
    {
        var s = CorredorSpec("00_Pesadelo");
        s.filter = new Color(0.95f, 0.55f, 0.6f);
        s.exposure = -0.4f;
        s.vignette = 0.45f;
        s.fog = new Color(0.12f, 0.02f, 0.04f);
        s.ambient = new Color(0.2f, 0.08f, 0.1f);
        s.defaultSpawnX = 0.2f;
        var c = Begin(s);
        if (c == null) return;

        var amelie = new GameObject("Amelie (pesadelo)");
        amelie.transform.position = c.Floor(0.45f, 0.2f);
        var asp = SpriteChild(amelie.transform, "Sprite", LoadSprite(Cen + "3° andar.psd", "Camada 15") ?? GenSprite("Amelie_sombra.png"), 1.45f, 0f);
        asp.color = new Color(0.8f, 0.8f, 0.9f, 0.9f);
        var alta1 = SpriteChild(amelie.transform, "Figura alta 1", GenSprite("Diretor.png"), 3.2f, -0.9f);
        var alta2 = SpriteChild(amelie.transform, "Figura alta 2", GenSprite("Inspetora.png"), 3.4f, 0.9f);
        alta1.color = alta2.color = new Color(0.05f, 0.03f, 0.05f, 0.85f);
        alta1.transform.localPosition += new Vector3(0, 0, 0.3f);
        alta2.transform.localPosition += new Vector3(0, 0, 0.3f);

        var lights = new List<LightZone>();
        for (int i = 0; i < 6; i++)
        {
            float fx = 0.15f + i * 0.15f;
            lights.Add(UmbraSceneBuilder.BuildLightZone(c.Floor(fx, 0.2f), new Vector3(2.5f, 4f, 3f),
                null, "Luz_" + i, new Color(1f, 0.45f, 0.45f), 1.4f));
        }

        var nc = amelie.AddComponent<NightmareCorridor>();
        nc.ameliesprite = asp;
        nc.keepDistance = 5f;
        nc.duration = 11f;
        nc.corridorLights = lights.ToArray();
        var exit = amelie.AddComponent<BoxCollider>();
        exit.isTrigger = true; exit.enabled = false;
        var le = amelie.AddComponent<LevelExit>();
        le.loadOnEnter = false;
        le.nextScene = "01_Dormitorio1";
        le.fadeTime = 0.1f;
        nc.onWakeUp = new UnityEvent();
        UnityEventTools.AddPersistentListener(nc.onWakeUp, le.LoadNow);

        Hint("Hint_Amelie", c.Floor(0.22f), new Vector3(3f, 3f, 6f), "Amelie?!  ...Amelie, espera!", true, null);
        Hint("Hint_Correr", c.Floor(0.3f), new Vector3(2f, 3f, 6f), "Shift: correr", false, "dica_correr");
        Finish(c);
    }

    // =====================================================================
    // 01 DORMITÓRIO 1  —  o despertar: baú, trinco alto, a cama vazia
    // =====================================================================

    public static void BuildDormitorio1()
    {
        var s = new Spec
        {
            scene = "01_Dormitorio1", psd = Cen + "quarto.psd", artName = "quarto (arte)",
            scale = 1f, wallBase = 0.785f, defaultSpawnX = 0.42f, darkness = 0.62f, floorDepth = 3.6f,
            hide = new[] { "mc" }, wallPatch = new Vector2(0.19f, 0.22f),
            opacity = new Dictionary<string, float> { { "luz", 0.48f }, { "sombra geral", 0.35f } },
            ambient = new Color(0.16f, 0.13f, 0.18f),
            layers = new Dictionary<string, LayerCfg>
            {
                // beliches encostados no fundo: uma caixa na cama de baixo e outra na de cima (a Luma sobe na de cima)
                { "cama 1", Bunk() }, { "cama 2", Bunk() }, { "cama 3", Bunk() },
                { "cama 4", Bunk() }, { "cama 5", Bunk() }, { "cama 6", Bunk() },
                { "cama 7", Bunk() }, { "cama tras 1", Bunk() }, { "cama tras 2", Bunk() },
                { "cobertor", On("cama 4") },
                { "cama frente 1", As(L.Stand) }, { "cama frente 2", As(L.Stand) },   // camas em primeiro plano
                { "Camada 6", As(L.Stand) }, { "Camada 6 Copiar", As(L.Stand) },   // estantes: viram objetos de empurrar
                { "Camada 5", As(L.Back) },       // fio da lâmpada
                { "luz", As(L.Effect) }, { "sombra geral", As(L.Overlay) },
            },
        };
        var c = Begin(s);
        if (c == null) return;

        // A Luma acorda na frente da cama dela (beliche com o cobertor).
        var start = c.Floor(0.42f, -0.2f) + Vector3.up * 0.05f;
        c.luma.transform.position = start;

        const float door = 0.711f;
        Spawn("do_corredor", c.Floor(door - 0.02f, 0.3f), true);

        var luz = UmbraSceneBuilder.FindChild(c.art.transform, "luz");
        UmbraSceneBuilder.BuildLightZone(c.Floor(0.44f, 0f), new Vector3(4.2f, 4f, -c.frontZ + 1f),
                                         luz != null ? luz.gameObject : null);

        // Trinco alto: as estantes pintadas entre as camas são o objeto de empurrar.
        // Puxe (S) para sair do lado das camas, empurre até a porta e para o fundo (W), suba e abra.
        var estante2 = MakePushable(c, "Camada 6 Copiar", "Estante 2");
        var estante1 = MakePushable(c, "Camada 6", "Estante 1");
        var bau = estante2 != null ? estante2 : (estante1 != null ? estante1 : UmbraSceneBuilder.BuildChest(c.Floor(0.64f, -0.75f)));
        var bp = bau.GetComponent<Pushable>();
        if (bp != null) bp.lockZ = false;
        var saida = UmbraSceneBuilder.BuildExit(c.WallFoot(door, 0.03f), "02_Corredor1");
        saida.GetComponent<BoxCollider>().size = new Vector3(1.0f, 2.5f, 0.8f);
        saida.GetComponent<LevelExit>().spawnId = "do_dormitorio1";
        saida.SetActive(false);
        var lk = new GameObject("Trinco (alto)").AddComponent<ItemLock>();
        float zDoor = c.WallZ(door) - 0.45f;
        lk.transform.position = c.AtDepth(door - 0.025f, 1.45f, zDoor);
        lk.prompt = "Abrir o trinco";
        lk.doneFlag = "dorm1_trinco";
        lk.doneMessage = "O trinco cede com um estalo. A porta se abre.";
        lk.maxVerticalDistance = 1.0f;     // do chão não alcança; em cima da estante, sim
        lk.extraRange = 0.4f;
        lk.activateOnDone = new[] { saida };

        // Pista P01 na cama vazia da Amelie (beliche ao lado).
        float z5 = c.LayerZ("cama 5", -1.5f);
        UmbraSceneBuilder.BuildClue("P01_Desenho", "Um desenho debaixo do travesseiro",
            "Duas meninas de mãos dadas, desenhadas com giz de cera.\nNo canto do papel, um pequeno sol.",
            ClueData.Origin.Amelie, c.AtDepth(0.556f, 0.45f, z5 - 0.1f), 1, "Olhar embaixo do travesseiro");

        Hint("Dica_Controles", start, new Vector3(2.5f, 3f, 4f),
            "A/D: andar  ·  W/S: fundo e frente  ·  E: interagir  ·  Espaço: pular / subir  ·  Esc: pausa", false, "dica_controles");
        Hint("Dica_CamaVazia", c.AtDepth(0.556f, 0f, z5 - 0.4f), new Vector3(1.4f, 3f, 2f),
            "(A cama da Amelie está vazia. Arrumada. Como se ninguém tivesse dormido ali.)", true, "d1_cama_vazia");
        Hint("Dica_Bau", bau.transform.position, new Vector3(2.2f, 3f, 2.5f),
            "E perto da estante: segurar. S para puxar, A/D e W para empurrar. E de novo: soltar.", false, "dica_bau");
        Hint("Dica_Trinco", c.Floor(door, 0.5f), new Vector3(1.6f, 3f, 2f),
            "O trinco está alto demais... Traga uma estante até a porta e aperte Espaço encostada nela para subir.", false, "dica_trinco");
        Hint("Dica_Camas", c.Floor(0.30f, 0.2f), new Vector3(3f, 3f, 3f),
            "Dá para subir na cama de cima do beliche: encoste, segure W e aperte Espaço.", false, "dica_camas");
        Finish(c);
    }

    // =====================================================================
    // 02 CORREDOR 1  —  o corredor do 2º andar (centro do Arco 1)
    //   esquerda: dormitórios 1 e 2 (portas na parede lateral) · nicho: mesa e biblioteca
    //   direita: banheiros, escadas (bloqueadas), porta da sala de descanso / sala de estar
    // =====================================================================

    const float DoorDorm1 = 0.052f, DoorDorm2 = 0.102f, DoorBib = 0.40f, DoorBan = 0.655f, DoorBan2 = 0.736f,
                Stairs = 0.895f, DoorLazy = 0.982f;

    static Spec CorredorSpec(string scene) => new Spec
    {
        scene = scene, psd = Cen + "corredor.psd", artName = "corredor (arte)",
        scale = 1.75f, exposure = 0.3f, darkness = 0.72f,
        wallBase = 0.69f, autoStand = false, floorDepth = 3.2f, frontLimit = 0.96f,
        wallPatch = new Vector2(0.44f, 0.47f),                                           // parede lisa do nicho
        backSegments = new List<Vector3> { new Vector3(0.48f, 1.0f, 0.862f) },          // parede das portas, mais perto
        sideWalls = new List<Vector4> { new Vector4(0.134f, 0.69f, 0.0f, 1.03f) },      // parede lateral dos dormitórios
        opacity = new Dictionary<string, float> { { "Camada 11", 0.62f }, { "Camada 11 Copiar", 0.69f } },
        layers = new Dictionary<string, LayerCfg>
        {
            { "Camada 7", Solid(0.88f) },   // mesa do nicho: dá para subir (Espaço)
            { "Camada 9", On("Camada 7") }, { "Camada 9 Copiar", On("Camada 7") }, { "Camada 10", On("Camada 7") },
        },
    };

    [MenuItem("Umbra/Montar cena/02 Corredor 1", priority = 6)]
    public static void BuildCorredor1()
    {
        var c = Begin(CorredorSpec("02_Corredor1"));
        if (c == null) return;

        // Entradas
        Spawn("do_dormitorio1", c.WallFoot(DoorDorm1, 0.05f) + Vector3.right * 0.3f, false);
        Spawn("do_dormitorio2", c.WallFoot(DoorDorm2, 0.06f) + Vector3.right * 0.3f, false);
        Spawn("da_biblioteca", c.WallFoot(DoorBib, 0.05f), false);
        Spawn("do_banheiro", c.WallFoot(DoorBan, 0.04f), false);
        Spawn("da_escada", c.WallFoot(Stairs, 0.04f), true);
        Spawn("do_lazy", c.WallFoot(DoorLazy - 0.02f, 0.06f), true);

        // Portas da esquerda (parede lateral): dormitório 1 (a da frente) e dormitório 2 (a do fundo).
        Door("Porta_Dormitorio1", c.WallFoot(DoorDorm1, 0.04f), "01_Dormitorio1", "do_corredor", "Entrar (dormitório 1)");
        var d2 = Door("Porta_Dormitorio2", c.WallFoot(DoorDorm2, 0.04f), "06_Dormitorio2", "do_corredor", "Entrar (dormitório 2)");
        d2.requiredFlag = "item_chave_dormitorio2";
        d2.lockedMessage = "Dormitório 2. Trancado. Do outro lado, várias respirações devagar... todas no mesmo ritmo.";

        // Biblioteca: ainda sem arte. Porta provisória no fundo do nicho.
        var bb = c.WallBase(DoorBib);
        var portaBib = Block("Porta da biblioteca (provisória)", new Vector3(bb.x, 1.1f, bb.z - 0.04f), new Vector3(1.1f, 2.2f, 0.06f),
                             new Color(0.16f, 0.1f, 0.07f));
        Object.DestroyImmediate(portaBib.GetComponent<Collider>());
        Sign("BIBLIOTECA", new Vector3(bb.x, 2.45f, bb.z - 0.08f), 2.2f, new Color(0.9f, 0.8f, 0.6f, 0.5f));
        var bib = Door("Porta_Biblioteca", c.WallFoot(DoorBib, 0.03f), "08_Biblioteca", "do_corredor", "Entrar na biblioteca");
        bib.requiredFlag = "item_chave_biblioteca";
        bib.lockedMessage = "BIBLIOTECA. Trancada. A fechadura é grande, de latão... a chave deve estar em algum lugar deste andar.";

        // Banheiros (lado a lado)
        Door("Porta_Banheiro", c.WallFoot(DoorBan, 0.03f), "03_Banheiro", "do_corredor", "Entrar no banheiro");
        var trancada = Door("Porta_Banheiro2", c.WallFoot(DoorBan2, 0.03f), "", "", "Abrir (banheiro 2)");
        trancada.requiredFlag = "nunca";
        trancada.lockedMessage = "O outro banheiro. Trancado por fora... e alguém respira devagar do outro lado.";

        // Direita: sala de descanso (e, depois dela, a sala de estar).
        Door("Porta_SalaDescanso", c.WallFoot(DoorLazy, 0.05f), "09_LazyRoom", "do_corredor1", "Entrar (sala de descanso)");

        // Escada 3D: a Luma sobe andando (W) até o patamar e vai para o 3º andar; voltando de lá, desce andando (S).
        // Descida para o térreo: porta embaixo do lance de subida pintado, degraus 3D descendo (W desce, S sobe).
        BuildEscada3D(c, new EscadaCfg(), false);
        BuildEscadaDescer3D(c, new EscadaDescerCfg(), false);

        // Luzes: calmas perto da mesa e das portas. O resto é escuro.
        var luzMesa = UmbraSceneBuilder.BuildLightZone(c.Floor(0.27f, 0.3f), new Vector3(3.2f, 4f, 3.4f), null, "Luz_Mesa",
                                                       new Color(1f, 0.8f, 0.6f), 1.3f, 2.6f, 4f);
        var luzPortas = UmbraSceneBuilder.BuildLightZone(c.Floor(0.70f, 0.3f), new Vector3(3.6f, 4f, 2.4f), null, "Luz_Portas",
                                                         new Color(1f, 0.8f, 0.7f), 1.2f, 2.8f, 4f);
        luzMesa.linkedVisuals = new[] { Cone("Facho_Mesa", c.Floor(0.27f, 0.7f), 2.9f, 3.0f) };
        luzPortas.linkedVisuals = new[] { Cone("Facho_Portas", c.Floor(0.70f, 0.7f), 2.9f, 3.4f) };
        luzPortas.visualsWhenOff = new[] { Darkness(c, 0.55f) };

        // Mesa: esconderijo embaixo + pista da Amelie na caixinha.
        float zMesa = c.LayerZ("Camada 7", c.WallZ(0.23f) - 0.4f);
        Hide("Embaixo da mesa", c.AtDepth(0.23f, 0f, zMesa - 0.25f), true);
        UmbraSceneBuilder.BuildClue("P02_Caixinha", "Uma caixinha de madeira sobre a mesa",
            "Dentro, um papel dobrado em quatro:\n\n\"Se as luzes apagarem, fique perto delas.\nEla não entra onde tem luz.\"\n\nNo canto, um pequeno sol.",
            ClueData.Origin.Amelie, c.AtDepth(0.225f, c.PaintY(0.56f, zMesa), zMesa + 0.1f), 1, "Abrir a caixinha");

        // Primeira aparição (de longe): as luzes piscam e galhos passam pela escada.
        var galho = InspetoraSprite("Galhos na escada", 2.6f);
        galho.transform.position = c.OnWall(0.95f, 0.45f, 0.3f);
        var scare = TriggerBox<ScareFlash>("Susto_Escada", c.Floor(0.66f), new Vector3(1f, 3f, 6f));
        scare.target = galho;
        scare.seconds = 1.4f;
        scare.from = Marker("Galho_A", c.OnWall(0.97f, 0.45f, 0.3f));
        scare.to = Marker("Galho_B", c.OnWall(0.84f, 0.35f, 0.3f));
        scare.flickerLights = new[] { luzPortas, luzMesa };
        scare.subtitle = "(alguma coisa passou pela escada...)";
        scare.onceFlag = "c1_susto";

        // FUGA: voltando da sala de descanso com a chave, a Inspetora vem atrás.
        var fuga = new GameObject("Fuga (só com o flag fuga_inspetora)");
        var massa = BuildMass("Inspetora (perseguindo)", c.Floor(1.0f, 0.2f) + Vector3.right * 0.8f, 3.6f, fuga.transform);
        massa.mode = InspetoraMass.Mode.Perseguir;
        massa.chaseDelay = 1.3f;
        massa.chaseSpeed = 2.3f;
        Hint("Hint_Fuga", c.Floor(0.95f), new Vector3(1.5f, 3f, 6f), "Ela está vindo! O dormitório 2... a chave!", true, null)
            .requiresFlag = "fuga_inspetora";
        var fs = new GameObject("FlagSwitch_Fuga").AddComponent<FlagSwitch>();
        fs.flag = "fuga_inspetora";
        fs.activeWhenSet = new[] { fuga };

        Hint("Dica_Luz", c.Floor(0.20f), new Vector3(1.5f, 3f, 6f),
             "No escuro o medo sobe. Fique perto da luz, ou segure F para abraçar o urso.", false, "dica_luz");
        Hint("Dica_Biblioteca", c.Floor(DoorBib, 0.3f), new Vector3(1.4f, 3f, 3f),
             "(A biblioteca... Amelie adorava ler lá escondida.)", true, "c1_bib");
        Finish(c);
    }

    // =====================================================================
    // 03 BANHEIRO  —  a chave da biblioteca e a primeira visita da Inspetora
    // =====================================================================

    [MenuItem("Umbra/Montar cena/03 Banheiro", priority = 7)]
    public static void BuildBanheiro()
    {
        var s = new Spec
        {
            scene = "03_Banheiro", psd = Cen + "banheiro.psd", artName = "banheiro (arte)",
            scale = 1.0f, hide = new[] { "MC" }, wallBase = 0.81f, autoStand = false, darkness = 0.78f, floorDepth = 2.6f,
            wallPatch = new Vector2(0.41f, 0.44f),
            opacity = new Dictionary<string, float> { { "Camada 3", 0.75f }, { "Camada 19", 0.17f } },
            // Divisórias dos chuveiros e das cabines ficam em pé, na frente da parede, com parede sólida até o fundo:
            // a Luma entra na cabine/chuveiro pelo vão entre elas e fica ATRÁS da divisória desenhada.
            // As camadas com duas divisórias (e a privada) são cortadas em pedaços.
            splits = new Dictionary<string, SplitPart[]>
            {
                { "Camada 5", new[]
                    {
                        Part("Divisoria cabine A", 0.70f, 0.78f, 0.7138f, 0.766f, 0.272f, 0.875f, Divider()),
                        Part("Privada", 0.78f, 0.835f, 0.7957f, 0.8214f, 0.602f, 0.85f, SolidFree(1f, 0.4f)),
                        Part("Divisoria cabine B", 0.835f, 0.95f, 0.8499f, 0.9347f, 0.278f, 0.881f, Divider()),
                    } },
                { "Camada 5 Copiar 2", new[]
                    {
                        Part("Divisoria chuveiro A", 0.14f, 0.32f, 0.1524f, 0.275f, 0.27f, 0.872f, Divider()),
                        Part("Divisoria chuveiro B", 0.32f, 0.42f, 0.3612f, 0.4068f, 0.271f, 0.874f, Divider()),
                    } },
            },
            layers = new Dictionary<string, LayerCfg>
            {
                { "Camada 5 Copiar 4", Divider() },                 // divisória do chuveiro da ponta
                { "Camada 5 Copiar", SolidFree(1f, 0.4f) },          // privada da cabine 2 (atrás da divisória C)
                { "Camada 8", Loose() },                             // divisória C (inclinada): a Luma passa por trás dela
                { "Camada 15", Loose() },                            // cortina do chuveiro (a Luma se esconde atrás)
                { "Camada 9", Back() },                              // lixeira
                { "Camada 1", Back() },                              // porta do corredor
                { "Camada 7", WallBox() }, { "Camada 7 Copiar", WallBox() }, { "Camada 7 Copiar 2", WallBox() },   // pias
            },
            ambient = new Color(0.2f, 0.2f, 0.22f), defaultSpawnX = 0.475f,
        };
        var c = Begin(s);
        if (c == null) return;
        var divC = UmbraSceneBuilder.FindChild(c.art.transform, "camada 8");
        if (divC != null)
        {
            divC.name = "Divisoria cabine C";       // nome da equipe (o PsdLayerDepth guarda "Camada 8")
            var dsr = divC.GetComponent<SpriteRenderer>();
            if (dsr != null) CropToMainShape(dsr, s.scene);   // tira as pinceladas soltas em volta da divisória
        }

        Spawn("do_corredor", c.WallFoot(0.475f, 0.05f), false);
        Door("Porta_Corredor", c.WallFoot(0.475f, 0.03f), "02_Corredor1", "do_banheiro", "Voltar ao corredor");

        var luz = UmbraSceneBuilder.BuildLightZone(c.Floor(0.5f, 0.2f), new Vector3(c.canvas.size.x, 4f, 4f), null,
                                                   "Luz_Banheiro", new Color(0.85f, 0.9f, 1f), 1.1f, 3f, 14f);
        luz.visualsWhenOff = new[] { Darkness(c, 0.72f) };

        Hide("Atrás da cortina do chuveiro", c.WallFoot(0.31f, 0.03f), true);
        // Dentro da cabine 1: entre as divisórias, na frente da privada.
        float zCab = c.LayerZ("Divisoria cabine A", c.WallZ(0.8f) - 1f);
        float zPriv = c.LayerZ("Privada", zCab + 0.4f);
        Hide("Dentro da cabine", c.AtDepth(0.808f, 0f, Mathf.Min(zCab + 0.2f, zPriv - 0.25f)), true);

        var reflexo = InspetoraSprite("Reflexo no espelho", 1.2f);
        reflexo.transform.position = c.OnWall(0.64f, 0.60f, 0.05f);
        foreach (var sr in reflexo.GetComponentsInChildren<SpriteRenderer>()) sr.color = new Color(1, 1, 1, 0.7f);
        var sc = TriggerBox<ScareFlash>("Susto_Espelho", c.Floor(0.60f), new Vector3(0.8f, 3f, 6f));
        sc.target = reflexo; sc.seconds = 0.6f; sc.flickerLights = new[] { luz }; sc.onceFlag = "banheiro_espelho";

        UmbraSceneBuilder.BuildClue("P03_Espelho", "Letras no vapor do espelho",
            "Alguém escreveu com o dedo, de dentro para fora, como se soubesse que outra pessoa ia ler depois:\n\n\"NÃO BEBA O LEITE.\"\n\nEmbaixo, quase apagado, um pequeno sol.",
            ClueData.Origin.Amelie, c.P(0.55f, 0.9f, 0.8f), 1, "Olhar o espelho");

        // A chave da biblioteca, escondida na caixa da descarga -> a Inspetora entra.
        var chave = new GameObject("Chave da biblioteca (descarga)").AddComponent<ItemPickup>();
        chave.transform.position = c.AtDepth(0.808f, 0.75f, zPriv + 0.15f);    // na caixa da descarga
        chave.itemFlag = "item_chave_biblioteca";
        chave.displayName = "a chave da biblioteca";
        chave.prompt = "Abrir a caixa da descarga";
        chave.message = "Uma chave grande, de latão, embrulhada num pano. Na etiqueta: BIBLIOTECA.";
        chave.extraRange = 0f;      // só de dentro da cabine
        Glow(chave.transform);

        var insp = BuildWalker("Inspetora", GenSprite("Inspetora.png"), 2.3f, c.Floor(0.475f, 0.7f));
        insp.patrolSpeed = 1.2f; insp.chaseSpeed = 3.4f; insp.viewDistance = 5f; insp.viewAngle = 110f;
        insp.avoidsLight = true; insp.waitAtPoint = 1.2f;
        insp.waypoints = new[]
        {
            Marker("WP_porta", c.Floor(0.475f, 0.3f)), Marker("WP_chuveiros", c.Floor(0.12f, 0.1f)),
            Marker("WP_pias", c.Floor(0.62f, 0.1f)), Marker("WP_cabines", c.Floor(0.92f, 0.1f)),
            Marker("WP_saida", c.Floor(0.475f, 0.6f)),
        };
        insp.gameObject.SetActive(false);

        var enc = new GameObject("Encontro_Banheiro").AddComponent<ScriptedEncounter>();
        enc.creature = insp;
        enc.lightsOff = new[] { luz };
        enc.arriveDelay = 2.6f;
        enc.duration = 13f;
        enc.doneFlag = "banheiro_encontro";
        enc.respawnPoint = Marker("Respawn_Encontro", c.Floor(0.36f));
        enc.startSubtitle = "(a luz apaga... alguma coisa se arrasta pelo corredor)";
        var fa = enc.gameObject.AddComponent<FlagActions>();
        enc.onBegin = new UnityEvent();
        UnityEventTools.AddStringPersistentListener(enc.onBegin, fa.Toast, "Esconda-se! Cortina do chuveiro ou a cabine (E).");
        enc.onEnd = new UnityEvent();
        UnityEventTools.AddStringPersistentListener(enc.onEnd, fa.Subtitle, "(...foi embora. A chave... a biblioteca, no corredor!)");
        chave.onPickup = new UnityEvent();
        UnityEventTools.AddPersistentListener(chave.onPickup, enc.Begin);

        Finish(c);
    }

    // =====================================================================
    // 08 BIBLIOTECA  —  (arte pendente) o fusível no alto do armário
    // =====================================================================

    [MenuItem("Umbra/Montar cena/08 Biblioteca (provisória)", priority = 8)]
    public static void BuildBiblioteca()
    {
        const float W = 14f;
        var c = BeginGrey("08_Biblioteca", W, 3f, new Color(0.13f, 0.11f, 0.1f), 0.72f, "BIBLIOTECA\n(arte pendente)");

        Spawn("do_corredor", new Vector3(-W / 2 + 1.2f, 0.05f, -0.3f), false);
        Door("Porta_Corredor", new Vector3(-W / 2 + 0.6f, 0f, 0.4f), "02_Corredor1", "da_biblioteca", "Voltar ao corredor");

        var madeira = new Color(0.2f, 0.13f, 0.09f);
        foreach (float x in new[] { -4.6f, -2.6f, -0.6f, 5.6f })
            Block("Estante", new Vector3(x, 1.5f, 1.15f), new Vector3(1.7f, 3f, 0.5f), madeira);
        var armario = Block("Armario alto", new Vector3(3.6f, 0.73f, 0.95f), new Vector3(1.1f, 1.46f, 0.9f), madeira);
        Block("Mesa de leitura", new Vector3(1.0f, 0.38f, 0.1f), new Vector3(1.6f, 0.76f, 0.8f), new Color(0.24f, 0.16f, 0.1f));

        UmbraSceneBuilder.BuildChest(new Vector3(-1.4f, 0f, -0.2f));

        var fus = new GameObject("Fusivel (em cima do armário)").AddComponent<ItemPickup>();
        fus.transform.position = new Vector3(3.6f, 1.6f, 0.9f);
        fus.itemFlag = "item_fusivel";
        fus.displayName = "um fusível";
        fus.prompt = "Pegar o fusível";
        fus.message = "Um fusível, escondido lá no alto... Amelie sempre escondia as coisas no alto.";
        fus.maxVerticalDistance = 1.0f;       // do chão não alcança
        Glow(fus.transform);

        UmbraSceneBuilder.BuildLightZone(new Vector3(-4.8f, 0f, 0f), new Vector3(3.4f, 4f, 3f), null, "Luz_Entrada",
                                         new Color(1f, 0.8f, 0.55f), 1.2f, 2.8f, 4.5f);
        UmbraSceneBuilder.BuildLightZone(new Vector3(1.0f, 0f, 0f), new Vector3(2.4f, 4f, 3f), null, "Luz_Leitura",
                                         new Color(1f, 0.85f, 0.6f), 0.9f, 2.2f, 3f);

        UmbraSceneBuilder.BuildClue("P07_Livro", "Um livro de contos aberto na mesa",
            "\"A menina e o sol\". Nas margens, a letra da Amelie:\n\n\"A mulher dos galhos só anda no escuro.\nNa sala de descanso tem uma caixa de luz sem fusível.\nEscondi o fusível no alto, onde ela não olha.\"\n\nE um pequeno sol.",
            ClueData.Origin.Amelie, new Vector3(1.0f, 0.85f, 0.1f), 1, "Ler o livro");

        var livro = Block("Livro que cai", new Vector3(5.6f, 2.9f, 0.85f), new Vector3(0.3f, 0.4f, 0.1f), new Color(0.35f, 0.1f, 0.1f));
        Object.DestroyImmediate(livro.GetComponent<Collider>());
        var sc = TriggerBox<ScareFlash>("Susto_Livro", new Vector3(2.2f, 0f, 0f), new Vector3(1f, 3f, 4f));
        sc.target = livro; sc.seconds = 0.5f;
        sc.from = Marker("Livro_A", new Vector3(5.6f, 2.9f, 0.85f));
        sc.to = Marker("Livro_B", new Vector3(5.2f, 0.1f, 0.6f));
        sc.subtitle = "(um livro cai sozinho, lá no fundo...)";
        sc.onceFlag = "bib_livro";

        Hint("Dica_Armario", new Vector3(3.2f, 0f, 0f), new Vector3(1.8f, 3f, 3f),
             "Alto demais... empurre o baú até o armário e suba (Espaço).", false, "dica_armario");
        Finish(c);
    }

    // =====================================================================
    // 09 SALA DE DESCANSO (Lazy Room)  —  (arte pendente) o ninho da Inspetora
    // =====================================================================

    [MenuItem("Umbra/Montar cena/09 Sala de descanso (provisória)", priority = 9)]
    public static void BuildLazyRoom()
    {
        const float W = 16f;
        var c = BeginGrey("09_LazyRoom", W, 3f, new Color(0.1f, 0.09f, 0.12f), 0.8f, "SALA DE DESCANSO\n(arte pendente)");

        Spawn("do_corredor1", new Vector3(-W / 2 + 1.2f, 0.05f, -0.3f), false);
        Spawn("da_living", new Vector3(-2.2f, 0.05f, 0.3f), false);
        Door("Porta_Corredor", new Vector3(-W / 2 + 0.6f, 0f, 0.4f), "02_Corredor1", "do_lazy", "Voltar ao corredor");

        var tecido = new Color(0.22f, 0.12f, 0.14f);
        Block("Sofa", new Vector3(-4.4f, 0.3f, 0.9f), new Vector3(2.4f, 0.6f, 0.9f), tecido);
        Block("Poltrona", new Vector3(3.2f, 0.35f, 1.0f), new Vector3(1.0f, 0.7f, 0.8f), tecido);
        Block("Mesinha", new Vector3(0.5f, 0.35f, 0.2f), new Vector3(1.6f, 0.7f, 0.8f), new Color(0.2f, 0.14f, 0.1f));

        // Porta para a sala de estar (no fundo): trancada.
        var pv = Block("Porta da sala de estar (provisória)", new Vector3(-2.2f, 1.1f, 1.45f), new Vector3(1.1f, 2.2f, 0.06f), new Color(0.16f, 0.1f, 0.07f));
        Object.DestroyImmediate(pv.GetComponent<Collider>());
        Sign("SALA DE ESTAR", new Vector3(-2.2f, 2.45f, 1.4f), 2f, new Color(0.9f, 0.8f, 0.6f, 0.45f));
        var living = Door("Porta_SalaEstar", new Vector3(-2.2f, 0f, 0.9f), "10_LivingRoom", "da_lazy", "Entrar (sala de estar)");
        living.requiredFlag = "item_chave_sala_estar";
        living.lockedMessage = "SALA DE ESTAR. Trancada. Quem teria essa chave... alguém que dorme perto das crianças?";

        // A Inspetora: massa de galhos vinda do escuro, à direita.
        var mass = BuildMass("Inspetora (ninho)", new Vector3(W / 2 - 1.2f, 0f, 0.6f), 3.6f, null);
        mass.direction = -1;
        mass.senseDistance = 3.4f;
        mass.maxAdvance = 4f;
        mass.creepSpeed = 0.7f;
        mass.chaseSpeed = 3.0f;
        mass.chaseDelay = 0.5f;

        // Lâmpada sobre a mesinha: apagada até colocar o fusível.
        var luz = UmbraSceneBuilder.BuildLightZone(new Vector3(0.5f, 0f, 0f), new Vector3(4.4f, 4f, 3f), null, "Luz_Lampada",
                                                   new Color(1f, 0.85f, 0.7f), 2.4f, 2.8f, 6f);
        luz.isOn = false;
        var cone = Cone("Facho da lâmpada", new Vector3(0.5f, 0f, 0.3f), 3.0f, 3.6f);
        luz.linkedVisuals = new[] { cone };
        cone.SetActive(false);

        var caixaVis = Block("Caixa de luz", new Vector3(-6.2f, 1.2f, 1.45f), new Vector3(0.45f, 0.6f, 0.08f), new Color(0.45f, 0.45f, 0.42f));
        Object.DestroyImmediate(caixaVis.GetComponent<Collider>());
        var caixa = new GameObject("Caixa de luz (fusível)").AddComponent<ItemLock>();
        caixa.transform.position = new Vector3(-6.2f, 1.0f, 1.0f);
        caixa.prompt = "Abrir a caixa de luz";
        caixa.requiredItem = "item_fusivel";
        caixa.promptWithItem = "Colocar o fusível";
        caixa.doneFlag = "lazy_luz";
        caixa.lockedMessage = "Uma caixa de luz. Falta um fusível.";
        caixa.doneMessage = "A lâmpada acende. A coisa no escuro recua.";
        caixa.onDone = new UnityEvent();
        UnityEventTools.AddBoolPersistentListener(caixa.onDone, luz.SetOn, true);
        caixa.onAlreadyDone = new UnityEvent();
        UnityEventTools.AddBoolPersistentListener(caixa.onAlreadyDone, luz.SetOn, true);

        // Chave do dormitório 2 na tigela: só dá para ver com a luz. Pegar = a lâmpada estoura.
        var tigelaEscura = new GameObject("Tigela (escuro)").AddComponent<ItemLock>();
        tigelaEscura.transform.position = new Vector3(0.5f, 0.75f, 0.2f);
        tigelaEscura.prompt = "Olhar a tigela";
        tigelaEscura.requiredItem = "nunca";
        tigelaEscura.lockedMessage = "Escuro demais. Algo brilha dentro da tigela... e algo se mexe no fundo da sala.";
        var chave = new GameObject("Chave do dormitório 2 (tigela)").AddComponent<ItemPickup>();
        chave.transform.position = new Vector3(0.5f, 0.75f, 0.2f);
        chave.itemFlag = "item_chave_dormitorio2";
        chave.displayName = "a chave do dormitório 2";
        chave.prompt = "Pegar a chave";
        chave.message = "Uma chave com um número pintado: 2. O dormitório 2!";
        Glow(chave.transform);
        var fsLuz = new GameObject("FlagSwitch_Luz").AddComponent<FlagSwitch>();
        fsLuz.flag = "lazy_luz";
        fsLuz.activeWhenSet = new[] { chave.gameObject };
        fsLuz.activeWhenNotSet = new[] { tigelaEscura.gameObject };

        var acoes = new GameObject("Acoes_Chave").AddComponent<FlagActions>();
        chave.onPickup = new UnityEvent();
        UnityEventTools.AddBoolPersistentListener(chave.onPickup, luz.SetOn, false);
        UnityEventTools.AddStringPersistentListener(chave.onPickup, acoes.SetFlag, "fuga_inspetora");
        UnityEventTools.AddStringPersistentListener(chave.onPickup, acoes.SetFlag, "lazy_lampada_quebrada");
        UnityEventTools.AddStringPersistentListener(chave.onPickup, acoes.Subtitle, "(a lâmpada estoura!)  CORRA!");
        UnityEventTools.AddPersistentListener(chave.onPickup, mass.StartChase);

        var quebrada = new GameObject("FlagSwitch_LampadaQuebrada").AddComponent<FlagSwitch>();
        quebrada.flag = "lazy_lampada_quebrada";
        quebrada.activeWhenNotSet = new[] { luz.gameObject, caixa.gameObject };

        UmbraSceneBuilder.BuildLightZone(new Vector3(-W / 2 + 1.5f, 0f, 0f), new Vector3(2.4f, 4f, 3f), null, "Luz_Porta",
                                         new Color(1f, 0.8f, 0.6f), 0.9f, 2.6f, 3.5f);
        Hint("Hint_Respira", new Vector3(-3f, 0f, 0f), new Vector3(1.5f, 3f, 4f),
             "(tem alguma coisa ali no escuro... respirando)", true, "lazy_respira");
        Finish(c);
    }

    // =====================================================================
    // 06 DORMITÓRIO 2  —  silêncio: a Inspetora faz a ronda e ouve tudo
    // =====================================================================

    [MenuItem("Umbra/Montar cena/06 Dormitório 2", priority = 10)]
    public static void BuildDormitorio2()
    {
        var s = new Spec
        {
            scene = "06_Dormitorio2", psd = Cen + "quarto 2.psd", artName = "quarto 2 (arte)",
            scale = 1.12f, defaultSpawnX = 0.255f, floorDepth = 4f,
            wallBase = 0.78f, darkness = 0.7f, wallPatch = new Vector2(0.45f, 0.50f),
            layers = new Dictionary<string, LayerCfg>
            {
                // beliches: caixa na cama de baixo e na de cima
                { "Camada 4", Bunk() }, { "Camada 4 Copiar", Bunk() },
                { "Camada 4 Copiar 3", Bunk() }, { "Camada 4 Copiar 5", Bunk() },
                { "Camada 4 Copiar 6", Bunk() }, { "Camada 4 Copiar 8", Bunk() },
                { "Camada 4 Copiar 9", Bunk() }, { "Camada 6", Solid(1f) },
                { "Camada 5 Copiar", As(L.Effect) },
            },
            opacity = new Dictionary<string, float>
            {
                { "Camada 3 Copiar", 0.44f }, { "Camada 5 Copiar", 0.41f }, { "Camada 7", 0.14f }, { "Camada 20", 0.12f },
                { "Camada 3", 0f }, { "Camada 12", 0f },
            },
            hide = new[] { "Camada 4 Copiar 11" },
            ambient = new Color(0.14f, 0.1f, 0.12f), exposure = 0.5f,
        };
        var c = Begin(s);
        if (c == null) return;

        var fl = new GameObject("Flags_Dormitorio2").AddComponent<FlagActions>();
        fl.clearOnStart = new[] { "fuga_inspetora" };          // entrou e trancou a porta: a fuga acabou

        Spawn("do_corredor", c.WallFoot(0.255f, 0.05f), false);
        Door("Porta_Corredor", c.WallFoot(0.255f, 0.03f), "02_Corredor1", "do_dormitorio2", "Sair");

        UmbraSceneBuilder.BuildLightZone(c.Floor(0.60f, 0.3f), new Vector3(2.6f, 4f, 3f),
            UmbraSceneBuilder.FindChild(c.art.transform, "camada 5 copiar")?.gameObject, "Luz_Lampada",
            new Color(1f, 0.75f, 0.8f), 1.6f, 2.8f, 5f);

        foreach (float fx in new[] { 0.09f, 0.40f, 0.70f, 0.83f, 0.93f })
        {
            var k = new GameObject("Crianca dormindo").AddComponent<SleepingChild>();
            k.transform.position = c.Floor(fx, 0.6f);
        }
        Hide("Embaixo da cama A", c.Floor(0.42f, 0.5f), true);
        Hide("Embaixo da cama B", c.Floor(0.83f, 0.5f), true);

        Plank(c, 0.53f);
        Plank(c, 0.87f);

        var ins = BuildWalker("Inspetora (ronda)", GenSprite("Inspetora.png"), 2.4f, c.Floor(0.92f, 0.3f));
        ins.patrolSpeed = 0.8f; ins.chaseSpeed = 2.6f; ins.investigateSpeed = 1.6f;
        ins.viewDistance = 2.4f; ins.viewAngle = 90f; ins.hearingMultiplier = 1.3f;
        ins.avoidsLight = true; ins.waitAtPoint = 3f; ins.catchDistance = 0.8f; ins.searchTime = 5f;
        ins.waypoints = new[] { Marker("WP_1", c.Floor(0.92f, 0.3f)), Marker("WP_2", c.Floor(0.36f, 0.3f)) };

        var chave = new GameObject("Chave da sala de estar").AddComponent<ItemPickup>();
        chave.transform.position = c.P(0.965f, 0.9f, 0.2f);
        chave.itemFlag = "item_chave_sala_estar";
        chave.displayName = "a chave da sala de estar";
        chave.prompt = "Pegar a chave na cabeceira";
        chave.message = "Uma chave com uma etiqueta: SALA DE ESTAR.";
        Glow(chave.transform);

        UmbraSceneBuilder.BuildClue("P04_NomesRiscados", "Riscos na cabeceira",
            "Na madeira, nomes de crianças, cada um com um X.\nNo fim da lista, com a letra da Amelie:\n\n\"Se eu sumir, NÃO me procure lá embaixo.\nFique na luz.\"\n\nE um pequeno sol.",
            ClueData.Origin.Amelie, c.P(0.45f, 0.4f, 0.2f), 2, "Olhar a cabeceira");

        Hint("Hint_Silencio", c.Floor(0.30f), new Vector3(1.2f, 3f, 6f),
             "Shh... todas dormindo. Não corra. Ela ouve tudo.", true, "d2_silencio");
        Finish(c);
    }

    // =====================================================================
    // 10 SALA DE ESTAR (Living Room)  —  (arte pendente) fim do Arco 1: a voz e o bilhete
    // =====================================================================

    [MenuItem("Umbra/Montar cena/10 Sala de estar (provisória)", priority = 11)]
    public static void BuildLivingRoom()
    {
        const float W = 15f;
        var c = BeginGrey("10_LivingRoom", W, 3f, new Color(0.14f, 0.1f, 0.1f), 0.6f, "SALA DE ESTAR\n(arte pendente)");

        var fl = new GameObject("Flags_SalaEstar").AddComponent<FlagActions>();
        fl.clearOnStart = new[] { "fuga_inspetora" };
        fl.setOnStart = new[] { "arco1_fim" };

        Spawn("da_lazy", new Vector3(-W / 2 + 1.2f, 0.05f, -0.3f), false);
        Door("Porta_SalaDescanso", new Vector3(-W / 2 + 0.6f, 0f, 0.4f), "09_LazyRoom", "da_living", "Voltar (sala de descanso)");

        var madeira = new Color(0.2f, 0.13f, 0.09f);
        Block("Lareira", new Vector3(2.5f, 0.9f, 1.15f), new Vector3(2.2f, 1.8f, 0.5f), new Color(0.15f, 0.12f, 0.12f));
        Block("Poltrona A", new Vector3(0.4f, 0.4f, 0.4f), new Vector3(1f, 0.8f, 0.8f), new Color(0.25f, 0.12f, 0.12f));
        Block("Poltrona B", new Vector3(4.6f, 0.4f, 0.4f), new Vector3(1f, 0.8f, 0.8f), new Color(0.25f, 0.12f, 0.12f));
        Block("Relogio de pe", new Vector3(-3.2f, 1.2f, 1.2f), new Vector3(0.6f, 2.4f, 0.4f), madeira);
        var janela = Block("Janela", new Vector3(-0.8f, 2.2f, 1.45f), new Vector3(1.6f, 1.6f, 0.04f), new Color(0.15f, 0.18f, 0.28f));
        Object.DestroyImmediate(janela.GetComponent<Collider>());

        UmbraSceneBuilder.BuildLightZone(new Vector3(2.5f, 0f, 0f), new Vector3(4f, 4f, 3f), null, "Luz_Lareira",
                                         new Color(1f, 0.6f, 0.35f), 1.8f, 1.2f, 6f);

        Hint("Voz", new Vector3(-1.5f, 0f, 0f), new Vector3(1.2f, 3f, 4f), "...Luma...  estou aqui embaixo...", true, "voz_sala");
        UmbraSceneBuilder.BuildClue("F01_Bilhete", "Um bilhete em cima da lareira",
            "A letra da Amelie. Tem certeza que é a letra dela.\n\n\"Luma, estou te esperando lá embaixo.\nNão conte para ninguém. Venha sozinha.\n— A.\"",
            ClueData.Origin.Falsa, new Vector3(2.5f, 1.2f, 0.8f), 1, "Pegar o bilhete");

        var fim = new GameObject("Fim da demo (Arco 1)").AddComponent<DemoEnd>();
        fim.requiredClues = new[] { "F01_Bilhete" };
        fim.lines = new[]
        {
            "Amelie está lá embaixo. Tenho certeza.",
            "Mas a escada está trancada... preciso achar outro caminho.",
        };
        fim.endText = "FIM DA DEMONSTRAÇÃO — Arco 1 (2º andar)";
        Finish(c);
    }

    // =====================================================================
    // 04 CORREDOR 2  —  térreo (fora da demo até a escada funcionar)
    // =====================================================================

    [MenuItem("Umbra/Montar cena/04 Corredor 2 (térreo)", priority = 20)]
    public static void BuildCorredor2()
    {
        var s = new Spec
        {
            scene = "04_Corredor2", psd = Cen + "corredor 2.psd", artName = "corredor 2 (arte)",
            scale = 1.12f, defaultSpawnX = 0.06f,
            opacity = new Dictionary<string, float> { { "Camada 4", 0.62f } },
            wallBase = 0.73f, autoStand = false, darkness = 0.8f, wallPatch = new Vector2(0.56f, 0.62f),
            layers = new Dictionary<string, LayerCfg>
            {
                { "Camada 6", As(L.Flat) }, { "Camada 7", As(L.Flat) }, { "Camada 8", As(L.Flat) },   // a massa (vira criatura)
                { "Camada 4", Solid(1f).Rect(0.18f, 0.61f, 0.39f, 0.74f) },                          // mesa
                { "Camada 13", On("Camada 4") }, { "Camada 14", On("Camada 4") },
            },
            ambient = new Color(0.1f, 0.09f, 0.12f), vignette = 0.3f, exposure = 0.35f,
        };
        var c = Begin(s);
        if (c == null) return;

        Spawn("da_escada", c.Floor(0.06f), false);
        Door("Escada_Subir", c.Floor(0.02f, 0.3f), "02_Corredor1", "da_escada_de_baixo", "Subir a escada");

        var root = new GameObject("Inspetora (ninho)");
        var parts = new List<Transform>();
        foreach (var n in new[] { "camada 7", "camada 6", "camada 8" })
        {
            var t = UmbraSceneBuilder.FindChild(c.art.transform, n);
            if (t != null) parts.Add(t);
        }
        float zm = c.WallZ(0.6f) - 0.1f;
        root.transform.position = c.AtDepth(0.47f, 0f, zm);
        foreach (var t in parts) t.SetParent(root.transform, true);
        var mass = root.AddComponent<InspetoraMass>();
        mass.front = Marker("Frente", root.transform.position).transform;
        mass.front.SetParent(root.transform, true);
        mass.direction = -1;
        mass.senseDistance = 3.2f;
        mass.maxAdvance = 3.5f;
        mass.creepSpeed = 0.7f;
        mass.chaseSpeed = 3.0f;
        mass.chaseDelay = 0.5f;
        mass.wobbleParts = parts.ToArray();

        var luz = UmbraSceneBuilder.BuildLightZone(c.Floor(0.47f, 0.3f), new Vector3(4.2f, 4f, 3f), null, "Luz_Lampada",
                                                   new Color(1f, 0.85f, 0.7f), 2.4f, 3f, 6f);
        luz.isOn = false;
        var cone = Cone("Facho da lâmpada", c.Floor(0.47f, 0.6f), 3.2f, 3.6f);
        luz.linkedVisuals = new[] { cone };
        cone.SetActive(false);

        var caixa = new GameObject("Caixa de luz").AddComponent<ItemLock>();
        caixa.transform.position = c.WallFoot(0.115f, 0.03f) + Vector3.up;
        caixa.prompt = "Abrir a caixa de luz";
        caixa.requiredItem = "item_fusivel_terreo";
        caixa.promptWithItem = "Colocar o fusível";
        caixa.doneFlag = "corredor2_luz";
        caixa.lockedMessage = "Uma caixa de luz. Falta um fusível.";
        caixa.doneMessage = "A lâmpada acende. A coisa no escuro recua.";
        caixa.onDone = new UnityEvent();
        UnityEventTools.AddBoolPersistentListener(caixa.onDone, luz.SetOn, true);

        Hint("Hint_Respira", c.Floor(0.2f), new Vector3(1.5f, 3f, 6f),
             "(tem alguma coisa ali no escuro... respirando)", true, "c2_respira");
        Finish(c);
    }

    // =====================================================================
    // 05 TERCEIRO ANDAR  —  sala do Diretor, quarto do Diretor, armazém, varanda (fora da demo)
    // =====================================================================

    [MenuItem("Umbra/Montar cena/05 Terceiro andar", priority = 21)]
    public static void BuildAndar3()
    {
        var s = new Spec
        {
            scene = "05_Andar3", psd = Cen + "3° andar.psd", artName = "3º andar (arte)",
            scale = 1.12f, defaultSpawnX = 0.05f,
            wallBase = 0.768f, autoStand = false, darkness = 0.62f, wallPatch = new Vector2(0.10f, 0.25f),
            backSegments = new List<Vector3> { new Vector3(0f, 0.443f, 0.80f) },      // bloco de parede da sala do Diretor
            ambient = new Color(0.14f, 0.1f, 0.14f),
        };
        var c = Begin(s);
        if (c == null) return;

        Spawn("das_escadas", c.Floor(0.05f), false);
        Spawn("do_escritorio", c.WallFoot(0.33f, 0.04f), false);
        Door("Escada_Descer", c.Floor(0.02f, 0.3f), "02_Corredor1", "da_escada", "Descer a escada");
        var esc = Door("Porta_Escritorio", c.WallFoot(0.33f, 0.03f), "07_Escritorio", "da_porta", "Abrir a porta (sala do Diretor)");
        esc.requiredFlag = "item_chave_escritorio";
        esc.lockedMessage = "DIREÇÃO. Trancada. Pela fresta, o som de uma caneta que não para de escrever.";
        var quarto = Door("Porta_QuartoDiretor", c.WallFoot(0.98f, 0.05f), "", "", "Abrir (quarto do Diretor)");
        quarto.requiredFlag = "nunca";
        quarto.lockedMessage = "O quarto do Diretor. Trancado. (arte pendente: quarto do Diretor, armazém e varanda)";

        var luzPorta = UmbraSceneBuilder.BuildLightZone(c.Floor(0.33f, 0.3f), new Vector3(2.4f, 4f, 2.4f), null, "Luz_Porta",
                                         new Color(1f, 0.75f, 0.7f), 1f, 2.8f, 4f);
        luzPorta.linkedVisuals = new[] { Cone("Facho_Porta", c.Floor(0.33f, 0.7f), 2.9f, 2.6f) };

        Hint("Voz", c.Floor(0.55f), new Vector3(1.2f, 3f, 6f), "...Luma...  estou aqui embaixo...", true, "voz_andar3");
        Finish(c);
    }

    // =====================================================================
    // 07 ESCRITÓRIO (sala do Diretor)  —  greybox: as fichas
    // =====================================================================

    [MenuItem("Umbra/Montar cena/07 Escritório (provisório)", priority = 22)]
    public static void BuildEscritorio()
    {
        const float W = 13f;
        var c = BeginGrey("07_Escritorio", W, 2.4f, new Color(0.12f, 0.1f, 0.1f), 0.5f, "SALA DO DIRETOR\n(arte pendente)");

        Block("Mesa do Diretor", new Vector3(1.5f, 0.45f, 0.7f), new Vector3(2.6f, 0.9f, 1f), new Color(0.2f, 0.13f, 0.1f));
        for (int i = 0; i < 4; i++)
            Block("Arquivo " + i, new Vector3(-3f + i * 1.1f, 1.1f, 1.05f), new Vector3(0.9f, 2.2f, 0.5f), new Color(0.18f, 0.18f, 0.2f));
        Block("Pilha de fichas", new Vector3(2.3f, 1.05f, 0.6f), new Vector3(0.6f, 0.3f, 0.4f), new Color(0.75f, 0.72f, 0.65f));
        UmbraSceneBuilder.BuildLightZone(new Vector3(1.5f, 0f, 0.3f), new Vector3(3f, 4f, 2.4f), null, "Luminária",
                                         new Color(1f, 0.85f, 0.6f), 2f, 2.2f, 5f);

        Spawn("da_porta", new Vector3(-W / 2 + 1f, 0.05f, 0f), false);
        Door("Porta_Saida", new Vector3(-W / 2 + 0.5f, 0f, 0.5f), "05_Andar3", "do_escritorio", "Sair");

        UmbraSceneBuilder.BuildClue("P05_FichaLuma", "Ficha 0417 — LUMA",
            "Chegada: noite de chuva, com a irmã.\nObservação: \"muito assustada\".\n\nNo canto, um carimbo preto. Foi raspado... e carimbado de novo. Raspado. Carimbado. Várias vezes.",
            ClueData.Origin.Orfanato, new Vector3(1.0f, 1f, 0.5f), 2, "Ler a ficha");
        UmbraSceneBuilder.BuildClue("P06_FichaAmelie", "Ficha 0416 — AMELIE",
            "Situação: TRANSFERIDA.\n\nMas o carimbo preto está nela. Tinta nova, borrada, como se alguém tivesse carimbado com pressa... com a mão pequena.",
            ClueData.Origin.Orfanato, new Vector3(2.1f, 1f, 0.5f), 2, "Ler a ficha");

        var fim = new GameObject("Fim (Arco 2)").AddComponent<DemoEnd>();
        fim.requiredClues = new[] { "P05_FichaLuma", "P06_FichaAmelie" };
        fim.lines = new[] { "O carimbo... era para ser o meu nome.", "Amelie marcou o próprio nome no lugar do meu?" };
        Finish(c);
    }

    // =====================================================================
    // Base dos cômodos pintados: cenário 3D
    // =====================================================================

    static Ctx Begin(Spec s)
    {
        var psd = AssetDatabase.LoadAssetAtPath<GameObject>(s.psd);
        if (psd == null) { Debug.LogError("[Umbra] Não achei " + s.psd); return null; }

        var c = new Ctx { spec = s };
        c.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Ambient(s.ambient, s.fog);
        c.cam = MainCamera();
        UmbraSceneBuilder.BuildVolume(s.scene, s.filter, s.exposure, s.vignette);

        c.room = UmbraGreybox.BuildRoom(new UmbraGreybox.RoomSettings
        { name = s.scene, width = 15f, depth = s.roomDepth, height = 5f, camera = false }, Vector3.zero);
        var artParent = c.room.transform.Find("Arte (coloque o PSB aqui)");

        var art = new GameObject(s.artName);
        art.transform.SetParent(artParent != null ? artParent : c.room.transform, false);
        c.art = art;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(psd, art.transform);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = s.artName + " (camadas)";
        inst.transform.localScale = Vector3.one * s.scale;
        FixLayerPlacement(inst, s.psd);
        PrepareLayers(inst, s);

        // Medidas do cômodo: ficam na arte e podem ser ajustadas depois na janela
        // "Umbra > Cenário > Montar cenário do PSD".
        var cfg = art.AddComponent<CenarioPSD3D>();
        cfg.nome = s.scene;
        cfg.psd = s.psd;
        cfg.escala = s.scale;
        cfg.linhaDoChao = s.wallBase;
        cfg.profundidadeDoChao = s.floorDepth > 0f ? s.floorDepth : 0f;
        cfg.limiteDaFrente = s.frontLimit;
        cfg.trechosDeParede = new List<Vector3>(s.backSegments);
        cfg.paredesLaterais = new List<Vector4>(s.sideWalls);
        cfg.detectarMoveis = s.autoStand;
        cfg.amostraParede = s.wallPatch;
        c.cfg = cfg;

        // Camadas cortadas em pedaços (cada pedaço vira uma camada própria, com papel próprio).
        var canvas0 = ArtCanvas(art);
        foreach (var kv in s.splits)
        {
            var src = UmbraSceneBuilder.FindChild(inst.transform, kv.Key.ToLowerInvariant())?.GetComponent<SpriteRenderer>();
            if (src == null) { Debug.LogWarning("[Umbra] Camada para cortar não encontrada: " + kv.Key); continue; }
            foreach (var part in kv.Value)
            {
                var psr = SplitSprite(src, canvas0, part, s.scene);
                if (psr == null) continue;
                var pi = psr.gameObject.AddComponent<PsdLayerDepth>();
                ApplyCfg(pi, part.cfg);
                pi.usarRetangulo = true;
                pi.retangulo = Rect.MinMaxRect(part.x0, part.y0, part.x1, part.y1);
            }
            var srcInfo = src.GetComponent<PsdLayerDepth>() ?? src.gameObject.AddComponent<PsdLayerDepth>();
            srcInfo.role = PsdLayerDepth.Role.Ignorar;      // a camada inteira some; ficam os pedaços
        }

        // Papéis das camadas configuradas aqui; as outras são detectadas pelo nome/posição.
        foreach (var sr in inst.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!s.layers.TryGetValue(sr.gameObject.name.Trim(), out var lc) || lc.role == L.Auto) continue;
            var info = sr.GetComponent<PsdLayerDepth>();
            if (info == null) info = sr.gameObject.AddComponent<PsdLayerDepth>();
            ApplyCfg(info, lc);
        }

        Apply3D(c);

        var spawn = c.Floor(s.defaultSpawnX) + Vector3.up * 0.05f;
        c.luma = UmbraSceneBuilder.BuildLuma(spawn);
        UmbraSetup.ConfigureScene();
        RailCamera(c.cam, c.luma, c.canvas, c.canvas.size.y, c.cy, c.shiftY, c.Travel);
        var veu = c.cam.GetComponent<DarknessOverlay>();
        if (veu != null) veu.darkness = s.darkness;
        return c;
    }

    /// <summary>
    /// Transforma uma camada pintada (ex.: estante) num objeto que a Luma segura, puxa e empurra (e sobe em cima).
    /// O desenho vai junto com a caixa de colisão do tamanho da parte pintada.
    /// </summary>
    static GameObject MakePushable(Ctx c, string layer, string newName)
    {
        var t = UmbraSceneBuilder.FindChild(c.art.transform, layer.ToLowerInvariant());
        var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
        if (sr == null || !sr.gameObject.activeInHierarchy) { Debug.LogWarning("[Umbra] Camada para empurrar não encontrada: " + layer); return null; }
        Rect o = c.opaque.TryGetValue(sr, out var r) ? r : new Rect(0, 0, 1, 1);
        float z = c.LayerZ(layer, c.WallZ(o.center.x) - 0.5f);
        float k = (c.D0 + z) / c.D0;
        Vector3 cam = c.cam.transform.position;
        float X(float f) => cam.x + (c.X(f) - cam.x) * k;
        float Y(float f) => cam.y + (c.Y(f) - cam.y) * k;
        float x0 = X(o.xMin), x1 = X(o.xMax), top = Mathf.Max(0.3f, Y(o.yMin));
        const float depth = 0.5f;

        var go = new GameObject(newName + " (empurrável)");
        go.transform.position = new Vector3((x0 + x1) * 0.5f, 0.01f, z + depth * 0.5f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3((x1 - x0) * 0.75f, top, depth);          // um pouco menor: não nasce encostada nas camas
        box.center = new Vector3(0f, top * 0.5f, 0f);
        box.sharedMaterial = Deslizante();                                // escorrega nas camas e paredes em vez de travar
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 20f;
        rb.linearDamping = 2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var p = go.AddComponent<Pushable>();
        p.lockZ = false;
        p.maxGapIncrease = 0.7f;
        p.extraRange = 0.35f;
        p.prompt = "Segurar a estante";

        // O desenho sai da arte do PSD (a janela "Montar cenário" não mexe mais nele) e vai com a caixa.
        var info = sr.GetComponent<PsdLayerDepth>();
        if (info != null) Object.DestroyImmediate(info);
        sr.transform.SetParent(go.transform, true);
        sr.gameObject.name = newName;
        return go;
    }

    /// <summary>Material físico sem atrito (objetos de empurrar deslizam encostados nas camas/paredes).</summary>
    static PhysicsMaterial Deslizante()
    {
        const string path = "Assets/Dados/Materiais/Deslizante.physicMaterial";
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m != null) return m;
        UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
        m = new PhysicsMaterial("Deslizante")
        {
            dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
        };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void ApplyCfg(PsdLayerDepth info, LayerCfg lc)
    {
        info.role = ToRole(lc);
        info.solid = lc.solid;
        info.climb = lc.climb;
        info.onLayer = lc.on;
        info.beliche = lc.bunk;
        info.ateParede = lc.toWall;
    }

    /// <summary>Um pedaço (x0..x1 da pintura) de uma camada, como sprite próprio salvo em Assets/Dados/Cenario3D/Partes.</summary>
    [MenuItem("Umbra/Cenário/Tirar sujeira do sprite selecionado", priority = 71)]
    static void LimparSpriteSelecionado()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) continue;
            if (CropToMainShape(sr, go.scene.name)) n++;
        }
        if (n == 0) EditorUtility.DisplayDialog("Umbra", "Selecione na Hierarchy o objeto com o sprite (ex.: \"Divisoria cabine C\").", "OK");
        else if (Selection.activeGameObject != null) EditorSceneManager.MarkSceneDirty(Selection.activeGameObject.scene);
    }

    /// <summary>
    /// Recorta o sprite no maior pedaço desenhado (tira pinceladas soltas em volta dele), sem mudar o lugar na tela:
    /// o pivô continua no mesmo pixel da textura. O sprite novo fica em Assets/Dados/Cenario3D/Partes (o PSD não muda).
    /// </summary>
    public static bool CropToMainShape(SpriteRenderer sr, string scene, float alphaMin = 0.4f, int margin = 2)
    {
        var sp = sr.sprite;
        if (sp == null || sp.texture == null) return false;
        var tex = sp.texture;
        Rect r = sp.rect;
        int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        if (w < 2 || h < 2) return false;

        // Lê os pixels mesmo com a textura sem "Read/Write" (cópia pela placa de vídeo).
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        read.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
        read.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = read.GetPixels32();
        Object.DestroyImmediate(read);

        // Maior pedaço ligado (alfa acima do limite).
        byte lim = (byte)Mathf.Clamp(Mathf.RoundToInt(alphaMin * 255f), 1, 255);
        var lab = new int[w * h];
        var stack = new Stack<int>();
        int best = 0, bestCount = 0, next = 0;
        int bx0 = 0, by0 = 0, bx1 = 0, by1 = 0;
        for (int i = 0; i < px.Length; i++)
        {
            if (lab[i] != 0 || px[i].a < lim) continue;
            next++;
            int count = 0, x0 = w, y0 = h, x1 = -1, y1 = -1;
            lab[i] = next; stack.Push(i);
            while (stack.Count > 0)
            {
                int j = stack.Pop(); count++;
                int x = j % w, y = j / w;
                if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                if (x > 0 && lab[j - 1] == 0 && px[j - 1].a >= lim) { lab[j - 1] = next; stack.Push(j - 1); }
                if (x < w - 1 && lab[j + 1] == 0 && px[j + 1].a >= lim) { lab[j + 1] = next; stack.Push(j + 1); }
                if (y > 0 && lab[j - w] == 0 && px[j - w].a >= lim) { lab[j - w] = next; stack.Push(j - w); }
                if (y < h - 1 && lab[j + w] == 0 && px[j + w].a >= lim) { lab[j + w] = next; stack.Push(j + w); }
            }
            if (count > bestCount) { bestCount = count; best = next; bx0 = x0; by0 = y0; bx1 = x1; by1 = y1; }
        }
        if (best == 0) { Debug.LogWarning("[Umbra] " + sr.name + ": sprite vazio, nada para recortar."); return false; }
        bx0 = Mathf.Max(0, bx0 - margin); by0 = Mathf.Max(0, by0 - margin);
        bx1 = Mathf.Min(w - 1, bx1 + margin); by1 = Mathf.Min(h - 1, by1 + margin);
        int nw = bx1 - bx0 + 1, nh = by1 - by0 + 1;
        if (nw >= w && nh >= h) { Debug.Log("[Umbra] " + sr.name + ": nada solto em volta do desenho."); return false; }

        var sub = new Rect(r.x + bx0, r.y + by0, nw, nh);
        var pivot = new Vector2((sp.pivot.x - bx0) / nw, (sp.pivot.y - by0) / nh);   // mesmo pixel da textura
        var clean = Sprite.Create(tex, sub, pivot, sp.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        clean.name = sr.gameObject.name + " (limpo)";
        UmbraGreybox.EnsureFolder(Out3D + "Partes");
        string path = Out3D + "Partes/" + scene + "_" + sr.gameObject.name.Trim().Replace(' ', '_') + "_limpo.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clean, path);
        Undo.RecordObject(sr, "Tirar sujeira do sprite");
        sr.sprite = clean;
        EditorUtility.SetDirty(sr);
        Debug.Log("[Umbra] " + sr.name + ": sprite recortado de " + w + "x" + h + " para " + nw + "x" + nh +
                  " px (tirou " + (next - 1) + " pedaço(s) soltos). Salvo em " + path);
        return true;
    }

    static SpriteRenderer SplitSprite(SpriteRenderer src, Bounds canvas, SplitPart part, string scene)
    {
        var sp = src.sprite;
        if (sp == null) return null;
        var t = src.transform;
        float ppu = sp.pixelsPerUnit;
        Vector3 wMin = t.TransformPoint(new Vector3(-sp.pivot.x / ppu, -sp.pivot.y / ppu, 0f));
        Vector3 wMax = t.TransformPoint(new Vector3((sp.rect.width - sp.pivot.x) / ppu, (sp.rect.height - sp.pivot.y) / ppu, 0f));
        float W = wMax.x - wMin.x;
        if (W <= 1e-4f) return null;
        float u0 = Mathf.Clamp01((canvas.min.x + part.c0 * canvas.size.x - wMin.x) / W);
        float u1 = Mathf.Clamp01((canvas.min.x + part.c1 * canvas.size.x - wMin.x) / W);
        float px0 = Mathf.Floor(u0 * sp.rect.width), px1 = Mathf.Ceil(u1 * sp.rect.width);
        if (px1 - px0 < 1f) return null;
        var sub = new Rect(sp.rect.x + px0, sp.rect.y, px1 - px0, sp.rect.height);
        var piece = Sprite.Create(sp.texture, sub, Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        piece.name = part.name;
        UmbraGreybox.EnsureFolder(Out3D + "Partes");
        string path = Out3D + "Partes/" + scene + "_" + part.name.Replace(' ', '_') + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(piece, path);

        var go = new GameObject(part.name);
        go.transform.SetParent(t.parent, false);
        go.transform.localRotation = t.localRotation;
        go.transform.localScale = t.localScale;
        go.transform.position = new Vector3(wMin.x + px0 / sp.rect.width * W, wMin.y, t.position.z);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = piece;
        sr.color = src.color;
        sr.sortingOrder = src.sortingOrder;
        sr.sharedMaterial = src.sharedMaterial;
        return sr;
    }

    static PsdLayerDepth.Role ToRole(LayerCfg lc)
    {
        switch (lc.role)
        {
            case L.Back: return lc.solid ? PsdLayerDepth.Role.DetalheParede : PsdLayerDepth.Role.Fundo;
            case L.Stand: return lc.back ? PsdLayerDepth.Role.MovelFundo
                               : (lc.solid || lc.thick <= 0f ? PsdLayerDepth.Role.Movel : PsdLayerDepth.Role.Frente);
            case L.On: return PsdLayerDepth.Role.Movel;
            case L.Effect: return PsdLayerDepth.Role.Efeito;
            case L.Flat: return PsdLayerDepth.Role.PlanoParede;
            case L.Hide: return PsdLayerDepth.Role.Ignorar;
            default: return PsdLayerDepth.Role.Fundo;
        }
    }

    const string GenName = "(gerado) Cenário 3D";

    /// <summary>
    /// Remonta o cenário 3D de uma arte que já está na cena (usado pela janela "Montar cenário do PSD").
    /// Volta as camadas ao estado original e aplica de novo com os papéis e medidas atuais.
    /// </summary>
    public static Ctx Remontar(GameObject art)
    {
        var c = new Ctx { art = art, spec = new Spec { scene = art.scene.name } };
        c.cam = Camera.main;
        if (c.cam == null) { EditorUtility.DisplayDialog("Umbra", "A cena precisa de uma Main Camera.", "OK"); return null; }
        c.cfg = art.GetComponent<CenarioPSD3D>();
        if (c.cfg == null) { c.cfg = Undo.AddComponent<CenarioPSD3D>(art); c.cfg.nome = art.scene.name; }
        var p = art.transform.parent;
        c.room = p != null && p.parent != null ? p.parent.gameObject : (p != null ? p.gameObject : art);
        Apply3D(c);

        // Câmera em trilho da cena (se existir) acompanha as novas medidas.
        var rail = Object.FindAnyObjectByType<CameraRail>();
        if (rail != null)
        {
            var vcam = rail.GetComponent<CinemachineCamera>();
            ConfigureRail(rail, vcam, c.canvas, c.canvas.size.y, c.cy, c.shiftY, c.Travel);
        }
        EditorSceneManager.MarkSceneDirty(art.scene);
        return c;
    }

    /// <summary>O cenário 3D: medidas, camadas, pintura projetada, colisões e caixas dos móveis.</summary>
    static void Apply3D(Ctx c)
    {
        var art = c.art;
        var cfg = c.cfg;
        var old = c.room.transform.Find(GenName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        c.gen = new GameObject(GenName).transform;
        c.gen.SetParent(c.room.transform, false);
        var geo = c.room.transform.Find("Geometria");
        if (geo != null) for (int i = geo.childCount - 1; i >= 0; i--) Object.DestroyImmediate(geo.GetChild(i).gameObject);

        // Camadas no estado original (remontar não acumula deslocamentos).
        var srs = art.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            var info = sr.GetComponent<PsdLayerDepth>();
            if (info != null) { info.StoreOriginal(); info.RestoreOriginal(); }
            var old3 = sr.GetComponent<OrdemAtras>();
            if (old3 != null) Object.DestroyImmediate(old3);
        }

        // A linha chão/parede vai para y = 0 e o meio da pintura para x = 0.
        c.canvas = ArtCanvas(art);
        float H = c.canvas.size.y;
        float wallY = c.canvas.max.y - cfg.linhaDoChao * H;
        Vector3 shift = new Vector3(-c.canvas.center.x, -wallY, -c.canvas.center.z);
        art.transform.position += shift;
        c.canvas.center += shift;

        // Câmera: a pintura enche a tela; o horizonte sai da profundidade do chão.
        float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        c.D0 = H * View / tanV;
        float F = cfg.profundidadeDoChao > 0f ? cfg.profundidadeDoChao : 0.65f * H;
        float t1 = Mathf.Clamp(1f - F / c.D0, 0.3f, 0.95f);
        c.vh = (cfg.linhaDoChao - t1) / (1f - t1);
        c.cy = c.Y(c.vh);
        c.frontZ = c.FloorAt(0.5f, cfg.limiteDaFrente).z;
        c.shiftY = (c.canvas.center.y - c.cy) / (2f * c.D0 * tanV);
        c.cam.transform.SetPositionAndRotation(new Vector3(0f, c.cy, -c.D0), Quaternion.identity);
        ApplyLensShift(c.cam, c.shiftY);

        // Medidas reais (parte opaca) de cada camada + papel das camadas ainda sem papel.
        var opaco = LoadOpaque(cfg.psd);
        foreach (var sr in srs)
        {
            c.opaque[sr] = OpaqueRect(c, sr, opaco);
            if (sr.GetComponent<PsdLayerDepth>() != null) continue;
            var info = sr.gameObject.AddComponent<PsdLayerDepth>();
            Guess(info, c, c.opaque[sr]);
            info.StoreOriginal();
        }

        var bake = new List<SpriteRenderer>();
        PlaceLayers(c, srs, bake);
        var tex = BakeBackdrop(c, bake);
        foreach (var sr in bake) sr.gameObject.SetActive(false);
        BuildBackdrop(c, tex);
        RoomColliders(c);

        // Móveis do fundo: na mesma ordem das camadas do PSD (a pintura já diz o que fica na frente).
        int Ord(SpriteRenderer r) { var i = r.GetComponent<PsdLayerDepth>(); return i != null && i.hasOrder ? i.originalSortingOrder : r.sortingOrder; }
        c.backSprites.Sort((a, b) => Ord(a) != Ord(b) ? Ord(a).CompareTo(Ord(b)) : b.transform.position.z.CompareTo(a.transform.position.z));
        for (int i = 0; i < c.backSprites.Count; i++)
        {
            var bs = c.backSprites[i];
            bs.sortingOrder = -200 + i;
            // Móvel do fundo com caixa (camas, mesa): se a Luma passa por trás dele, ele é desenhado na frente dela.
            if (c.behindZ.TryGetValue(bs, out var zb))
            {
                var oa = bs.gameObject.AddComponent<OrdemAtras>();
                oa.zAtras = zb + 0.05f;
                oa.ordemAtras = -200 + i;
                oa.ordemFrente = Mathf.Min(101 + i, 149);
            }
        }
        Debug.Log("[Umbra] Cenário 3D montado: " + bake.Count + " camadas na pintura, " + c.layerZ.Count + " objetos em pé.");
    }

    /// <summary>Retângulo da pintura (fica certo mesmo com camadas escondidas ou maiores que o documento).</summary>
    static Bounds ArtCanvas(GameObject art)
    {
        foreach (var sr in art.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || sr.gameObject.name.Trim().ToLowerInvariant() != "papel") continue;
            var t = sr.transform;
            Vector3 center = t.TransformPoint(sr.sprite.bounds.center);
            Vector3 size = Vector3.Scale(sr.sprite.bounds.size, t.lossyScale);
            return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0f));
        }
        // Sem "Papel" (ex.: banheiro): junta as camadas do PSD, como o UmbraSceneBuilder.CanvasBounds,
        // mas SEM os pedaços cortados pelo montador (um pedaço chamado "... parede ..." encolhia a pintura).
        Bounds b = default;
        bool has = false;
        for (int pass = 0; pass < 2 && !has; pass++)
            foreach (var sr in art.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var pl = sr.GetComponent<PsdLayerDepth>();
                if (pl != null && pl.usarRetangulo) continue;
                string n = sr.name.ToLowerInvariant();
                if (pass == 0 && !(n.Contains("papel") || n.Contains("parede") || n.Contains("lado de fora"))) continue;
                if (!has) { b = sr.bounds; has = true; } else b.Encapsulate(sr.bounds);
            }
        return has ? b : new Bounds(art.transform.position, Vector3.zero);
    }

    // ------------------------------------------------------------ medidas reais das camadas

    class OpaqueEntry { public float[] box, op; }

    /// <summary>Assets/Dados/PSD/&lt;psd&gt;.opaco.txt: caixa da camada e da parte opaca (pixels), gerado dos PSDs.</summary>
    static Dictionary<string, List<OpaqueEntry>> LoadOpaque(string psdPath)
    {
        var map = new Dictionary<string, List<OpaqueEntry>>();
        if (string.IsNullOrEmpty(psdPath)) return map;
        string file = "Assets/Dados/PSD/" + System.IO.Path.GetFileNameWithoutExtension(psdPath) + ".opaco.txt";
        if (!System.IO.File.Exists(file)) return map;
        var lines = System.IO.File.ReadAllLines(file);
        var head = lines[0].Split(' ');
        float W = float.Parse(head[0]), H = float.Parse(head[1]);
        map[""] = new List<OpaqueEntry> { new OpaqueEntry { box = new[] { W, H } } };
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split('\t');
            if (f.Length < 9) continue;
            var v = new float[8];
            for (int k = 0; k < 8; k++) v[k] = float.Parse(f[k + 1]);
            if (!map.TryGetValue(f[0].Trim(), out var list)) map[f[0].Trim()] = list = new List<OpaqueEntry>();
            list.Add(new OpaqueEntry { box = new[] { v[0], v[1], v[2], v[3] }, op = new[] { v[4], v[5], v[6], v[7] } });
        }
        return map;
    }

    /// <summary>Parte opaca da camada, em frações da pintura (x0, y0 = topo, x1, y1 = base).</summary>
    static Rect OpaqueRect(Ctx c, SpriteRenderer sr, Dictionary<string, List<OpaqueEntry>> map)
    {
        Bounds b = sr.sprite != null
            ? new Bounds(sr.transform.TransformPoint(sr.sprite.bounds.center), Vector3.Scale(sr.sprite.bounds.size, sr.transform.lossyScale))
            : sr.bounds;
        b.size = new Vector3(Mathf.Abs(b.size.x), Mathf.Abs(b.size.y), 0f);
        float Fx(float wx) => (wx - c.canvas.min.x) / c.canvas.size.x;
        float Fy(float wy) => (c.canvas.max.y - wy) / c.canvas.size.y;
        Rect whole = Rect.MinMaxRect(Fx(b.min.x), Fy(b.max.y), Fx(b.max.x), Fy(b.min.y));

        var pl = sr.GetComponent<PsdLayerDepth>();
        if (pl != null && pl.usarRetangulo && pl.retangulo.width > 0f) return pl.retangulo;
        string key = pl != null && !string.IsNullOrEmpty(pl.nomeNoPsd) ? pl.nomeNoPsd : sr.gameObject.name.Trim();
        if (!map.TryGetValue(key, out var list) || !map.TryGetValue("", out var hd) || b.size.x < 1e-4f) return whole;
        float W = hd[0].box[0], H = hd[0].box[1];
        // Três jeitos do PSD Importer trazer a camada: inteira, cortada no documento, ou só a parte desenhada.
        float[] Box(OpaqueEntry e, int mode)
        {
            if (mode == 2) return new[] { Mathf.Max(0, e.op[0]), Mathf.Max(0, e.op[1]), Mathf.Min(W, e.op[2]), Mathf.Min(H, e.op[3]) };
            if (mode == 1) return new[] { Mathf.Max(0, e.box[0]), Mathf.Max(0, e.box[1]), Mathf.Min(W, e.box[2]), Mathf.Min(H, e.box[3]) };
            return e.box;
        }
        OpaqueEntry best = null; float bestD = float.MaxValue; int bestMode = 0;
        foreach (var e in list)
            for (int mode = 0; mode < 3; mode++)
            {
                var bb = Box(e, mode);
                float x0 = bb[0], y0 = bb[1], x1 = bb[2], y1 = bb[3];
                if (x1 - x0 < 1f || y1 - y0 < 1f) continue;
                // tamanho e posição parecidos com o sprite da cena
                float d = Mathf.Abs((x1 - x0) / W - whole.width) + Mathf.Abs((y1 - y0) / H - whole.height)
                        + 0.5f * (Mathf.Abs((x0 + x1) * 0.5f / W - whole.center.x) + Mathf.Abs((y0 + y1) * 0.5f / H - whole.center.y));
                if (d < bestD - 1e-5f) { bestD = d; best = e; bestMode = mode; }
            }
        if (best == null) return whole;
        var bbb = Box(best, bestMode);
        float bx0 = bbb[0], by0 = bbb[1], bx1 = bbb[2], by1 = bbb[3];
        float U(float px) => whole.xMin + (px - bx0) / (bx1 - bx0) * whole.width;
        float V(float py) => whole.yMin + (py - by0) / (by1 - by0) * whole.height;
        return Rect.MinMaxRect(U(Mathf.Max(best.op[0], bx0)), V(Mathf.Max(best.op[1], by0)),
                               U(Mathf.Min(best.op[2], bx1)), V(Mathf.Min(best.op[3], by1)));
    }

    // ------------------------------------------------------------ papel pelo nome

    static string Normalize(string s)
    {
        var d = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var ch in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>Palavra inteira (aceita plural): "cama" acha "cama tras 1", mas NÃO "Camada 4".</summary>
    static bool Has(string n, params string[] keys)
    {
        var parts = n.Split(' ', '_', '-', '.', '(', ')');
        foreach (var k in keys)
        {
            if (k.Contains(" ")) { if (n.Contains(k)) return true; continue; }
            foreach (var part in parts) if (part == k || part == k + "s" || part == k + "es") return true;
        }
        return false;
    }

    /// <summary>Palpite do papel de uma camada (a janela deixa trocar).</summary>
    public static void Guess(PsdLayerDepth info, Ctx c, Rect o)
    {
        string n = Normalize(info.gameObject.name);
        info.solid = false; info.climb = 1f; info.onLayer = null; info.beliche = false; info.ateParede = false;
        var R = PsdLayerDepth.Role.Fundo;
        if (Has(n, "mc", "luma", "personagem")) R = PsdLayerDepth.Role.Ignorar;
        else if (Has(n, "luz", "brilho")) R = PsdLayerDepth.Role.Efeito;
        else if (Has(n, "sombra", "nevoa", "fog")) R = PsdLayerDepth.Role.Fundo;
        else if (Has(n, "chao", "piso", "tapete", "floor")) R = PsdLayerDepth.Role.Chao;
        else if (Has(n, "papel", "fundo", "lado de fora", "ceu", "parede", "wall")) R = PsdLayerDepth.Role.Fundo;
        else if (Has(n, "cortina", "quadro", "porta", "janela", "espelho", "prateleira", "relogio", "foto")) R = PsdLayerDepth.Role.DetalheParede;
        else if (Has(n, "frente")) R = PsdLayerDepth.Role.Frente;
        else if (Has(n, "beliche"))
        {
            R = PsdLayerDepth.Role.MovelFundo; info.solid = true; info.beliche = true;
        }
        else if (Has(n, "cama", "mesa", "bau", "caixa", "armario", "cadeira", "tras"))
        {
            R = PsdLayerDepth.Role.MovelFundo; info.solid = true; info.climb = Has(n, "cama") ? 0.2f : 1f;
        }
        else if (Has(n, "divisoria", "cabine"))
        {
            R = PsdLayerDepth.Role.Movel; info.solid = true; info.ateParede = true;
        }
        else if (Has(n, "pia"))
        {
            R = PsdLayerDepth.Role.DetalheParede; info.solid = true;
        }
        else if (c.cfg.detectarMoveis && o.yMax > c.FloorTop(o.center.x) + 0.03f && o.width < 0.6f) R = PsdLayerDepth.Role.Movel;
        if (!info.gameObject.activeSelf && R != PsdLayerDepth.Role.Ignorar) { /* escondida no PSD: continua escondida */ }
        info.role = R;
    }

    // ------------------------------------------------------------ objetos em pé

    static void PlaceLayers(Ctx c, SpriteRenderer[] srs, List<SpriteRenderer> bake)
    {
        var cutout = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dados/Materiais/SpriteRecorte.mat");
        var fundoMat = SpriteFundoMaterial();
        Vector3 cam = c.cam.transform.position;
        int effectOrder = 200;

        foreach (var pass in new[] { 0, 1 })
        foreach (var sr in srs)
        {
            var info = sr.GetComponent<PsdLayerDepth>();
            if (info == null || !sr.gameObject.activeSelf) continue;
            bool on = !string.IsNullOrEmpty(info.onLayer);
            if (on != (pass == 1)) continue;
            string n = sr.gameObject.name.Trim();
            var R = info.role;
            if (R == PsdLayerDepth.Role.Ignorar) { sr.gameObject.SetActive(false); continue; }
            if (!on && (R == PsdLayerDepth.Role.Fundo || R == PsdLayerDepth.Role.DetalheParede || R == PsdLayerDepth.Role.Chao))
            {
                bake.Add(sr);
                if (info.solid && c.opaque.TryGetValue(sr, out var ow)) WallBoxFor(sr, c, ow);
                continue;
            }

            Rect o = c.opaque.TryGetValue(sr, out var r) ? r : new Rect(0, 0, 1, 1);
            float cu = Mathf.Clamp01(o.center.x);
            bool back = R == PsdLayerDepth.Role.MovelFundo || R == PsdLayerDepth.Role.PlanoParede;
            float z;
            if (on)
            {
                z = c.LayerZ(info.onLayer.Trim(), c.WallZ(cu) - 0.3f) - 0.01f;
                back |= c.backLayers.Contains(info.onLayer.Trim());
            }
            else if (R == PsdLayerDepth.Role.Efeito) z = c.WallZ(cu) - 0.05f;
            else if (R == PsdLayerDepth.Role.PlanoParede) z = c.WallZ(cu) - 0.02f;
            else z = Mathf.Clamp(c.FloorAt(cu, o.yMax).z, -0.8f * c.D0, c.WallZ(cu) - 0.03f);
            c.layerZ[n] = z;
            if (back) c.backLayers.Add(n);

            // Empurra ao longo do raio da câmera: mesma posição e tamanho na tela.
            float k = (c.D0 + z) / c.D0;
            var t = sr.transform;
            t.position = cam + (t.position - cam) * k;
            t.localScale *= k;

            if (R == PsdLayerDepth.Role.Efeito)
            {
                sr.sortingOrder = effectOrder++;
                if (sr.color.a > 0.99f) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.5f);
            }
            else if (back && fundoMat != null) { sr.sharedMaterial = fundoMat; c.backSprites.Add(sr); }
            else if (cutout != null && sr.color.a > 0.98f) { sr.sharedMaterial = cutout; sr.sortingOrder = 0; }

            if (info.solid) SolidFor(sr, info, c, z, k, cam, o, back);
            // Em cima de um móvel que troca de ordem (cobertor na cama): troca junto.
            if (on && back && c.behindZByName.TryGetValue(info.onLayer.Trim(), out var zbOn)) c.behindZ[sr] = zbOn;
        }
    }

    /// <summary>
    /// Caixa transparente (só colisão) do móvel, do tamanho da parte desenhada dele.
    /// Rasa (a Luma pisa logo atrás da frente pintada). Móvel do fundo: sem bloqueio atrás; se a Luma passa por trás,
    /// o componente OrdemAtras desenha o móvel na frente dela.
    /// </summary>
    static void SolidFor(SpriteRenderer sr, PsdLayerDepth info, Ctx c, float z, float k, Vector3 cam, Rect o, bool back)
    {
        float X(float f) => cam.x + (c.X(f) - cam.x) * k;
        float Y(float f) => cam.y + (c.Y(f) - cam.y) * k;
        float x0 = X(o.xMin), x1 = X(o.xMax), y1 = Y(o.yMin), y0 = Y(o.yMax);
        float w = Mathf.Max(0.2f, (x1 - x0) * 0.92f);
        float top = Mathf.Max(0.2f, (y1 - Mathf.Max(y0, 0f)) * info.climb);
        float cu = Mathf.Clamp01(o.center.x);
        float wall = c.WallZ(cu) + 0.3f;
        float full = Mathf.Max(0.2f, y1 - Mathf.Max(y0, 0f));
        if (info.ateParede && !back)
        {
            // Divisória: parede sólida da frente pintada até a parede do fundo.
            var dv = new GameObject("Divisoria_" + sr.gameObject.name + " (transparente)");
            dv.transform.SetParent(c.gen, true);
            float d = Mathf.Max(0.3f, wall - z);
            dv.transform.position = new Vector3((x0 + x1) * 0.5f, full * 0.5f, z + d * 0.5f);
            dv.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.2f, (x1 - x0) * 0.96f), full, d);
            return;
        }
        float depth = back ? Mathf.Clamp(wall - z, 0.5f, 0.6f) : 0.8f;
        float zTop = z + depth;
        var go = new GameObject("Caixa_" + sr.gameObject.name + " (transparente)");
        go.transform.SetParent(c.gen, true);
        if (info.beliche)
        {
            // Beliche: caixa da cama de baixo (do chão até o colchão) e caixa da cama de cima (estrado + colchão).
            float low = full * info.camaDeBaixo;
            float upB = full * info.camaDeCima.x, upT = full * info.camaDeCima.y;
            go.name = "Caixa_" + sr.gameObject.name + " (cama de baixo)";
            go.transform.position = new Vector3((x0 + x1) * 0.5f, low * 0.5f, (z + zTop) * 0.5f);
            go.AddComponent<BoxCollider>().size = new Vector3(w, low, depth);
            var up = new GameObject("Caixa_" + sr.gameObject.name + " (cama de cima)");
            up.transform.SetParent(go.transform, true);
            up.transform.position = new Vector3((x0 + x1) * 0.5f, (upB + upT) * 0.5f, (z + zTop) * 0.5f);
            up.AddComponent<BoxCollider>().size = new Vector3(w, Mathf.Max(0.05f, upT - upB), depth);
        }
        else
        {
            go.transform.position = new Vector3((x0 + x1) * 0.5f, top * 0.5f, (z + zTop) * 0.5f);
            go.AddComponent<BoxCollider>().size = new Vector3(w, top, depth);
        }
        // Sem bloqueio atrás: a Luma pode passar por trás do móvel; aí ele é desenhado na frente dela (OrdemAtras).
        if (back) { c.behindZ[sr] = zTop; c.behindZByName[sr.gameObject.name.Trim()] = zTop; }
    }

    /// <summary>Caixa pequena saindo da parede no lugar de algo pintado nela (pia, prateleira).</summary>
    static void WallBoxFor(SpriteRenderer sr, Ctx c, Rect o)
    {
        float cu = Mathf.Clamp01(o.center.x);
        float zw = c.WallZ(cu);
        float k = (c.D0 + zw) / c.D0;
        Vector3 cam = c.cam.transform.position;
        float X(float f) => cam.x + (c.X(f) - cam.x) * k;
        float Y(float f) => cam.y + (c.Y(f) - cam.y) * k;
        float x0 = X(o.xMin), x1 = X(o.xMax), yTop = Y(o.yMin), yBot = Mathf.Max(0f, Y(o.yMax));
        const float depth = 0.45f;
        var go = new GameObject("Caixa_" + sr.gameObject.name + " (na parede)");
        go.transform.SetParent(c.gen, true);
        go.transform.position = new Vector3((x0 + x1) * 0.5f, (yTop + yBot) * 0.5f, zw - depth * 0.5f);
        go.AddComponent<BoxCollider>().size = new Vector3(Mathf.Max(0.1f, x1 - x0), Mathf.Max(0.05f, yTop - yBot), depth);
    }

    // ------------------------------------------------------------ pintura 3D

    /// <summary>Renderiza só as camadas de fundo (planas) numa textura do tamanho da pintura.</summary>
    static Texture2D BakeBackdrop(Ctx c, List<SpriteRenderer> bake)
    {
        int pxH = Mathf.RoundToInt(c.canvas.size.y / Mathf.Max(c.cfg.escala, 0.01f) * 200f);
        int pxW = Mathf.RoundToInt(c.canvas.size.x / Mathf.Max(c.cfg.escala, 0.01f) * 200f);
        float lim = 8192f / Mathf.Max(pxW, pxH);
        if (lim < 1f) { pxW = Mathf.RoundToInt(pxW * lim); pxH = Mathf.RoundToInt(pxH * lim); }

        var keep = new HashSet<SpriteRenderer>(bake);
        var turnedOff = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r.enabled && !(r is SpriteRenderer sr && keep.Contains(sr))) { r.enabled = false; turnedOff.Add(r); }
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        bool camActive = c.cam.gameObject.activeSelf;
        c.cam.gameObject.SetActive(false);

        var go = new GameObject("_assar_fundo");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = c.canvas.size.y * 0.5f;
        go.transform.position = new Vector3(c.canvas.center.x, c.canvas.center.y, -30f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        var rt = new RenderTexture(pxW, pxH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        rt.Create();
        cam.targetTexture = rt;
        cam.aspect = (float)pxW / pxH;

        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
        else cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(pxW, pxH, TextureFormat.RGB24, false, false);
        tex.ReadPixels(new Rect(0, 0, pxW, pxH), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        Object.DestroyImmediate(go);
        rt.Release();
        Object.DestroyImmediate(rt);

        c.cam.gameObject.SetActive(camActive);
        RenderSettings.fog = fog;
        foreach (var r in turnedOff) if (r != null) r.enabled = true;

        UmbraGreybox.EnsureFolder("Assets/Dados/Cenario3D");
        string path = Out3D + c.Name + "_fundo.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 8192;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.alphaSource = TextureImporterAlphaSource.None;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Colunas onde a parede muda (degraus) e dobras (onde a parede lateral encontra o fundo).</summary>
    static void Breaks(CenarioPSD3D cfg, List<float> steps, List<float> kinks)
    {
        foreach (var seg in cfg.trechosDeParede)
        {
            if (seg.x > 0.001f && seg.x < 0.999f) steps.Add(seg.x);
            if (seg.y > 0.001f && seg.y < 0.999f) steps.Add(seg.y);
        }
        foreach (var w in cfg.paredesLaterais) { kinks.Add(w.x); kinks.Add(w.z); }
    }

    /// <summary>Fração da textura usada na lateral dos degraus de parede (1 = densidade de frente).</summary>
    const float LateralEscorco = 0.3f;

    /// <summary>Chão deitado + paredes em pé, com a pintura projetada da posição da câmera.</summary>
    static void BuildBackdrop(Ctx c, Texture2D tex)
    {
        const float M = 0.35f;
        const float E = 1e-4f;
        int n = Mathf.Clamp(Mathf.RoundToInt(c.canvas.size.x / c.canvas.size.y * 60f), 120, 420);
        var us = new List<float>();
        int m = Mathf.RoundToInt(M * n);
        for (int i = -m; i <= n + m; i++) us.Add((float)i / n);
        var steps = new List<float>(); var kinks = new List<float>();
        Breaks(c.cfg, steps, kinks);
        foreach (var b in steps) { us.Add(b - E); us.Add(b + E); }
        foreach (var b in kinks) us.Add(b);
        // Buracos na pintura (ex.: vão da escada 3D): colunas exatamente nas bordas do buraco.
        var holes = c.cfg.buracosNaPintura;
        foreach (var h in holes)
        {
            float k = (c.D0 + h.z) / c.D0;
            us.Add((h.x / k - c.canvas.min.x) / c.canvas.size.x);
            us.Add((h.y / k - c.canvas.min.x) / c.canvas.size.x);
        }
        var openings = c.cfg.aberturasNaPintura;                // só até uma altura (ex.: porta da escada de descida)
        foreach (var h in openings)
        {
            float k = (c.D0 + h.z) / c.D0;
            us.Add((h.x / k - c.canvas.min.x) / c.canvas.size.x);
            us.Add((h.y / k - c.canvas.min.x) / c.canvas.size.x);
        }
        us.Sort();
        var cols = new List<float>();
        foreach (var u in us) if (cols.Count == 0 || u - cols[cols.Count - 1] > E * 0.5f) cols.Add(u);
        // Coluna da grade caindo exatamente em cima de um degrau (ex.: 0.48 = 108/225 no Corredor 1): tira,
        // senão o par (b - E, b + E) deixa de ser vizinho, IsStepGap não pula o vão e a grade liga as duas
        // paredes com uma coluna de pixels esticada (listras) na frente da lateral de verdade.
        foreach (var b in steps) cols.RemoveAll(u => Mathf.Abs(u - b) < E * 0.5f);

        // Faixa de parede lisa (repetida espelhada) para o que fica além da pintura e para as laterais dos degraus.
        var patch = c.cfg.amostraParede;
        bool hasPatch = patch.y > patch.x + 0.002f;
        float pu0 = Mathf.Clamp01(patch.x), pu1 = Mathf.Clamp01(patch.y), pw = Mathf.Max(pu1 - pu0, 0.002f);
        float pvt = hasPatch ? c.FloorTop((pu0 + pu1) * 0.5f) : 1f;
        Vector2 PatchUV(float d, float v, float vt)
        {
            float uu = pu0 + Mathf.PingPong(Mathf.Abs(d), pw);
            float vv = v <= vt ? v * (pvt / Mathf.Max(vt, 1e-3f))
                               : pvt + (v - vt) / Mathf.Max(1f - vt, 1e-3f) * (1f - pvt);
            return new Vector2(uu, 1f - Mathf.Clamp01(vv));
        }
        Vector2 UV(float u, float v, float vt)
        {
            if (!hasPatch || (u >= 0f && u <= 1f)) return new Vector2(u, 1f - v);
            return PatchUV(u < 0f ? -u : u - 1f, v, vt);
        }

        const int NW = 24, NF = 40;
        int R = NW + NF + 1;
        var verts = new List<Vector3>(cols.Count * R);
        var uvs = new List<Vector2>(cols.Count * R);
        var colBase = new List<Vector3>(cols.Count);
        foreach (var u in cols)
        {
            float vt = c.FloorTop(u);
            var B = c.FloorAt(u, vt);
            colBase.Add(B);
            float tB = (B.z + c.D0) / c.D0;
            for (int r = 0; r < R; r++)
            {
                float v;
                Vector3 p;
                if (r <= NW)
                {
                    v = vt * r / NW;
                    p = new Vector3(B.x, c.cy + tB * (c.Y(v) - c.cy), B.z);
                }
                else
                {
                    v = vt + (1f - vt) * (r - NW) / NF;
                    p = c.FloorAt(u, v);
                }
                verts.Add(p);
                uvs.Add(UV(u, v, vt));
            }
        }
        bool IsStepGap(float a, float b)
        {
            foreach (var st in steps) if (Mathf.Abs(a - (st - E)) < E * 0.3f && Mathf.Abs(b - (st + E)) < E * 0.3f) return true;
            return false;
        }
        var tris = new List<int>();
        for (int i = 0; i < cols.Count - 1; i++)
        {
            // Degrau de parede: a lateral é feita abaixo, com textura de parede. O chão continua (sem fresta).
            bool stepGap = IsStepGap(cols[i], cols[i + 1]);
            bool hole = stepGap;
            foreach (var h in holes)
                if (Mathf.Abs(colBase[i].z - h.z) < 0.01f && Mathf.Abs(colBase[i + 1].z - h.z) < 0.01f &&
                    colBase[i].x >= h.x - 1e-3f && colBase[i + 1].x <= h.y + 1e-3f) hole = true;
            float openTop = float.NegativeInfinity;
            foreach (var h in openings)
                if (Mathf.Abs(colBase[i].z - h.z) < 0.01f && Mathf.Abs(colBase[i + 1].z - h.z) < 0.01f &&
                    colBase[i].x >= h.x - 1e-3f && colBase[i + 1].x <= h.y + 1e-3f) openTop = Mathf.Max(openTop, h.w);
            for (int j = 0; j < R - 1; j++)
            {
                if (hole && j < NW) continue;                     // parede com buraco ou degrau: só o chão continua
                if (j < NW && verts[i * R + j].y <= openTop + 1e-3f) continue;   // abertura: some a parede até o topo dela
                int a = i * R + j, b = (i + 1) * R + j;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
            }
        }

        // Lateral de cada degrau de parede (ex.: o bloco das portas do Corredor 1 que avança sobre o nicho):
        // parede de verdade com a textura da parede vizinha, em vez de uma coluna de pixels esticada.
        foreach (var st in steps)
        {
            float uL = st - E, uR = st + E;
            var A = c.WallBase(uL); var Bp = c.WallBase(uR);
            bool rightNear = Bp.z < A.z;
            var near = rightNear ? Bp : A; var deep = rightNear ? A : Bp;
            float uNear = rightNear ? uR : uL;
            float L = Vector2.Distance(new Vector2(near.x, near.z), new Vector2(deep.x, deep.z));
            if (L < 0.05f) continue;
            const int NS = 8, NH = 24;
            int start = verts.Count;
            for (int i = 0; i <= NS; i++)
            {
                float t = (float)i / NS;
                var P = Vector3.Lerp(near, deep, t);
                float k = (c.D0 + P.z) / c.D0;
                float top = c.cy + k * (c.Y(0f) - c.cy);
                // A câmera vê esta lateral de viés (encurtada ~3x): amostrar a parede na densidade "de frente"
                // deixava o grão espremido na horizontal e virava listras. LateralEscorco compensa isso.
                float du = t * L * LateralEscorco / (c.canvas.size.x * k);
                float vFloor = (c.canvas.max.y - (c.cy - c.cy / k)) / c.canvas.size.y;
                for (int r = 0; r <= NH; r++)
                {
                    float y = top * r / NH;
                    verts.Add(new Vector3(P.x, y, P.z));
                    float v = (c.canvas.max.y - (c.cy + (y - c.cy) / k)) / c.canvas.size.y;
                    if (hasPatch) uvs.Add(PatchUV(du, Mathf.Clamp01(v), Mathf.Clamp01(vFloor)));
                    else uvs.Add(new Vector2(Mathf.Clamp01(uNear + (rightNear ? du : -du)), 1f - Mathf.Clamp01(v)));
                }
            }
            for (int i = 0; i < NS; i++)
                for (int j = 0; j < NH; j++)
                {
                    int a = start + i * (NH + 1) + j, b = start + (i + 1) * (NH + 1) + j;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
                }
        }
        var mesh = new Mesh { name = c.Name + "_sala", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        string meshPath = Out3D + c.Name + "_sala.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        string matPath = Out3D + c.Name + "_fundo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var sh = Shader.Find("Umbra/Fundo Projetado") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = sh;
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        EditorUtility.SetDirty(mat);

        var go = new GameObject("Pintura projetada (chão + paredes)");
        go.transform.SetParent(c.gen, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>Chão, paredes (fundo e laterais pintadas), bordas e frente: a Luma só anda no chão pintado.</summary>
    static void RoomColliders(Ctx c)
    {
        var geo = new GameObject("Colisões (paredes e chão)").transform;
        geo.SetParent(c.gen, false);
        Vector3 inside = c.FloorAt(0.5f, 0.97f);
        int count = 0;

        void Box(string name, Vector3 center, Vector3 size, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(geo, false);
            t.SetPositionAndRotation(center, rot);
            t.gameObject.AddComponent<BoxCollider>().size = size;
        }
        void Seg(string name, Vector3 a, Vector3 b, bool centered)
        {
            Vector3 d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.05f) return;
            Vector3 dir = d / len;
            Vector3 nrm = new Vector3(-dir.z, 0f, dir.x);
            Vector3 mid = (a + b) * 0.5f; mid.y = 0f;
            if (Vector3.Dot(nrm, inside - mid) > 0f) nrm = -nrm;
            float th = centered ? 0.3f : 0.6f;
            Vector3 pos = mid + (centered ? Vector3.zero : nrm * th * 0.5f) + Vector3.up * 3f;
            Box(name + "_" + (count++), pos, new Vector3(len + 0.3f, 6f, th), Quaternion.LookRotation(nrm, Vector3.up));
        }

        float W = c.canvas.size.x;
        Box("Chao", new Vector3(0f, -0.25f, -c.D0 * 0.4f), new Vector3(W * 2f, 0.5f, c.D0 * 1.2f), Quaternion.identity);

        var steps = new List<float>(); var kinks = new List<float>();
        Breaks(c.cfg, steps, kinks);
        var cuts = new List<float> { 0.003f, 0.997f };
        foreach (var k in kinks) if (k > 0.003f && k < 0.997f) cuts.Add(k);
        foreach (var st in steps) cuts.Add(st);
        cuts.Sort();
        const float E = 1e-3f;
        for (int i = 0; i < cuts.Count - 1; i++)
        {
            float a = cuts[i] + (steps.Contains(cuts[i]) ? E : 0f);
            float b = cuts[i + 1] - (steps.Contains(cuts[i + 1]) ? E : 0f);
            if (b - a < E) continue;
            Seg("Parede", c.WallBase(a), c.WallBase(b), false);
        }
        foreach (var st in steps) Seg("Parede_Quina", c.WallBase(st - E), c.WallBase(st + E), true);

        Seg("Borda_Esquerda", c.WallBase(0.003f), c.FloorAt(0.003f, 1.02f), false);
        Seg("Borda_Direita", c.WallBase(0.997f), c.FloorAt(0.997f, 1.02f), false);
        Box("Frente_Invisivel", new Vector3(0f, 3f, c.frontZ - 0.25f), new Vector3(W * 2f, 6f, 0.5f), Quaternion.identity);
        foreach (var vao in c.cfg.vaosNaParede) CutOpening(geo, vao.x, vao.y, vao.z, false);
        foreach (var b in c.cfg.buracosNoChao) CutFloor(geo, b.x, b.y, b.z, b.w, false);
        Physics.SyncTransforms();
    }

    // =====================================================================
    // Escada 3D (Corredor 1): vão modelado atrás da parede, com a pintura projetada
    // =====================================================================

    /// <summary>Onde está o lance de escada na pintura e para onde ele leva.</summary>
    public class EscadaCfg
    {
        public float u0 = 0.870f, u1 = 0.938f;      // degrau de baixo: borda esquerda e direita (fração da pintura)
        public float ut0 = 0.885f, ut1 = 0.936f;    // degrau de cima
        public float vTop = 0.37f;                  // linha do degrau de cima
        public float angle = 35f;                   // inclinação da escada de verdade (graus)
        public float landing = 1.4f;                // fundo do patamar (m)
        public int steps = 9;                       // degraus pintados
        public string nextScene = "05_Andar3", nextSpawn = "das_escadas", arriveSpawn = "da_escada";
    }

    const string EscadaRoot = "Escada 3D";

    [MenuItem("Umbra/Cenário/Escada 3D do Corredor 1 (só a escada)", priority = 72)]
    static void MenuEscadaCorredor()
    {
        var cfg = Object.FindAnyObjectByType<CenarioPSD3D>(FindObjectsInactive.Include);
        if (cfg == null || !cfg.gameObject.scene.name.StartsWith("02_Corredor1"))
        {
            EditorUtility.DisplayDialog("Umbra", "Abra a cena 02_Corredor1 primeiro.", "OK");
            return;
        }
        var c = MeasureOnly(cfg);
        if (c == null) return;
        BuildEscada3D(c, new EscadaCfg(), true);
        EditorSceneManager.MarkSceneDirty(cfg.gameObject.scene);
    }

    /// <summary>Medidas da pintura/câmera de uma cena já montada, sem mexer em nada.</summary>
    static Ctx MeasureOnly(CenarioPSD3D cfg)
    {
        var art = cfg.gameObject;
        var c = new Ctx { cfg = cfg, art = art, spec = new Spec { scene = art.scene.name } };
        var p = art.transform.parent;
        c.room = p != null && p.parent != null ? p.parent.gameObject : (p != null ? p.gameObject : art);
        var g = c.room.transform.Find(GenName);
        c.gen = g;
        c.cam = Camera.main;
        c.canvas = ArtCanvas(art);
        float H = c.canvas.size.y;
        float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        c.D0 = H * View / tanV;
        float F = cfg.profundidadeDoChao > 0f ? cfg.profundidadeDoChao : 0.65f * H;
        float t1 = Mathf.Clamp(1f - F / c.D0, 0.3f, 0.95f);
        c.vh = (cfg.linhaDoChao - t1) / (1f - t1);
        c.cy = c.Y(c.vh);
        c.frontZ = c.FloorAt(0.5f, cfg.limiteDaFrente).z;
        float wallY = c.canvas.max.y - cfg.linhaDoChao * H;
        if (Mathf.Abs(wallY) > 0.05f || Mathf.Abs(c.canvas.center.x) > 0.05f)
            Debug.LogWarning("[Umbra] A arte não parece montada em 3D (linha do chão em y = " + wallY + "). Monte o cenário antes.");
        return c;
    }

    /// <summary>
    /// Escada como vão 3D de verdade (igual ao nicho da mesa): a parede pintada ganha um buraco e atrás dele ficam
    /// degraus, patamar e paredes modelados, com a pintura projetada neles. Vista da câmera parada no fim do trilho
    /// fica igual à pintura; com a câmera andando, os degraus mostram profundidade.
    /// A Luma sobe/desce andando (W/S) numa rampa invisível nos degraus; no patamar vai para a próxima cena.
    /// Só mexe nos objetos da escada, no buraco da pintura e no vão da colisão da parede.
    /// </summary>
    static void BuildEscada3D(Ctx c, EscadaCfg e, bool undo)
    {
        float uMid = (e.u0 + e.u1) * 0.5f;
        var rail = Object.FindAnyObjectByType<CameraRail>();
        float camX = rail != null ? (c.X(uMid) > 0f ? rail.maxX : rail.minX) : 0f;
        float vBot = c.FloorTop(uMid);
        float zw = c.WallZ(uMid);
        float kw = (c.D0 + zw) / c.D0;
        var C = new Vector3(camX, c.cy, -c.D0);                 // câmera quando a Luma está na escada
        float F(float z) => (c.D0 + z) / (c.D0 + zw);

        // Pintura da sala (malha + material) e as colunas da parede no z da escada.
        MeshFilter roomMF = null;
        if (c.gen != null)
            foreach (var mf in c.gen.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.StartsWith("Pintura projetada")) roomMF = mf;
        float hx0 = c.X(e.u0) * kw, hx1 = c.X(e.u1) * kw;     // bordas do buraco na parede
        if (roomMF != null && roomMF.sharedMesh != null)
        {
            var xs = new List<float>();
            foreach (var p in roomMF.sharedMesh.vertices)
                if (Mathf.Abs(p.z - zw) < 1e-3f && p.y > 0.05f) xs.Add(p.x);
            float Snap(float x) { float best = x, bestD = float.MaxValue; foreach (var q in xs) { float dq = Mathf.Abs(q - x); if (dq < bestD) { bestD = dq; best = q; } } return best; }
            if (xs.Count > 0) { hx0 = Snap(hx0); hx1 = Snap(hx1); }
        }
        float XL(float z) => C.x + (hx0 - C.x) * F(z);           // lados do vão: raios da câmera pelas bordas do buraco
        float XR(float z) => C.x + (hx1 - C.x) * F(z);

        // Tamanho da escada de verdade: o patamar aparece na linha do degrau de cima da pintura.
        float tanA = Mathf.Tan(e.angle * Mathf.Deg2Rad);
        float hwTop = c.cy + (c.Y(e.vTop) - c.cy) * kw;
        float L = hwTop / (tanA + (c.cy - hwTop) / (c.D0 + zw));
        float zt = zw + L, Ht = tanA * L;
        int N = Mathf.Max(2, e.steps);
        float run = L / N, rise = Ht / N;
        float zb = zt + e.landing;
        float wallTopW = c.cy + (c.Y(0f) - c.cy) * kw;          // topo da pintura no plano da parede
        float YTop(float z) => c.cy + (wallTopW - c.cy) * F(z) + 0.5f;

        // Remove a escada antiga (grade, trinco, porta desligada, escada 3D anterior).
        foreach (var n in new[] { EscadaRoot, "Grade da escada (provisória)", "Escada (bloqueada)", "Escada_Subir (desligada)" })
            foreach (var go in FindAllByName(c.art.scene, n))
                if (undo) Undo.DestroyObjectImmediate(go); else Object.DestroyImmediate(go);
        var root = new GameObject(EscadaRoot);
        SceneManager.MoveGameObjectToScene(root, c.art.scene);
        if (undo) Undo.RegisterCreatedObjectUndo(root, "Escada 3D");

        // ---------------------------------------------------------------- buraco na pintura da parede
        if (undo) Undo.RecordObject(c.cfg, "Escada 3D");
        c.cfg.buracosNaPintura.RemoveAll(v => Mathf.Abs(v.z - zw) < 0.3f && v.x < hx1 && v.y > hx0);
        c.cfg.buracosNaPintura.Add(new Vector3(hx0, hx1, zw));
        if (roomMF != null && roomMF.sharedMesh != null)
        {
            var mesh = roomMF.sharedMesh;
            if (undo) Undo.RecordObject(mesh, "Escada 3D");
            var vs = mesh.vertices;
            var tri = mesh.triangles;
            var keep = new List<int>(tri.Length);
            bool InHole(Vector3 p) => Mathf.Abs(p.z - zw) < 1e-3f && p.y > -1e-3f && p.x >= hx0 - 1e-3f && p.x <= hx1 + 1e-3f;
            for (int i = 0; i < tri.Length; i += 3)
            {
                if (InHole(vs[tri[i]]) && InHole(vs[tri[i + 1]]) && InHole(vs[tri[i + 2]])) continue;
                keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
            }
            if (keep.Count != tri.Length)
            {
                mesh.SetTriangles(keep, 0);
                EditorUtility.SetDirty(mesh);
            }
        }

        // ---------------------------------------------------------------- degraus, patamar e paredes (visual)
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        // A pintura que a câmera da escada vê atrás de cada ponto (raio até a parede → pintura).
        Vector2 ProjUV(Vector3 P)
        {
            Vector3 W = C + (P - C) * ((c.D0 + zw) / (c.D0 + P.z));
            float cx = W.x / kw, cyv = c.cy + (W.y - c.cy) / kw;
            return new Vector2((cx - c.canvas.min.x) / c.canvas.size.x, (cyv - c.canvas.min.y) / c.canvas.size.y);
        }
        // Parede lisa (a mesma faixa usada nas laterais dos degraus do nicho).
        var patch = c.cfg.amostraParede;
        bool hasPatch = patch.y > patch.x + 0.002f;
        float pu0 = Mathf.Clamp01(patch.x), pw = Mathf.Max(Mathf.Clamp01(patch.y) - pu0, 0.002f);
        float pvt = hasPatch ? c.FloorTop((patch.x + patch.y) * 0.5f) : 1f;
        Vector2 PatchUV(float along, Vector3 P)
        {
            float k = (c.D0 + P.z) / c.D0;
            float du = along / (c.canvas.size.x * k);
            float v = Mathf.Clamp01((c.canvas.max.y - (c.cy + (P.y - c.cy) / k)) / c.canvas.size.y);
            float vt = Mathf.Clamp01((c.canvas.max.y - (c.cy - c.cy / k)) / c.canvas.size.y);
            float uu = pu0 + Mathf.PingPong(Mathf.Abs(du), pw);
            float vv = v <= vt ? v * (pvt / Mathf.Max(vt, 1e-3f)) : pvt + (v - vt) / Mathf.Max(1f - vt, 1e-3f) * (1f - pvt);
            return new Vector2(uu, 1f - Mathf.Clamp01(vv));
        }
        void Grid(System.Func<float, float, Vector3> P, int nu, int nv, System.Func<float, float, Vector3, Vector2> uv)
        {
            int start = verts.Count;
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float s = (float)i / nu, t = (float)j / nv;
                    var p = P(s, t);
                    verts.Add(p); uvs.Add(uv(s, t, p));
                }
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    int a = start + i * (nv + 1) + j, b = start + (i + 1) * (nv + 1) + j;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
                }
        }
        System.Func<float, float, Vector3, Vector2> Proj = (s, t, p) => ProjUV(p);
        // Desenhado de trás para a frente (os degraus não escrevem profundidade): paredes, patamar, degraus de cima para baixo.
        foreach (int side in new[] { -1, 1 })
        {
            System.Func<float, float> X = side < 0 ? (System.Func<float, float>)XL : XR;
            float x0w = X(zw);
            Grid((s, t) => { float z = Mathf.Lerp(zw, zb, s); return new Vector3(X(z), Mathf.Lerp(0f, YTop(z), t), z); }, 12, 16,
                 (s, t, p) => hasPatch ? PatchUV(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(x0w, zw)), p) : ProjUV(p));
        }
        Grid((s, t) => new Vector3(Mathf.Lerp(XL(zb), XR(zb), s), Mathf.Lerp(Ht, YTop(zb), t), zb), 8, 16, Proj);   // fundo
        Grid((s, t) => { float z = Mathf.Lerp(zt, zb, t); return new Vector3(Mathf.Lerp(XL(z), XR(z), s), Ht, z); }, 8, 6, Proj); // patamar
        for (int i = N - 1; i >= 0; i--)
        {
            float z0 = zw + i * run, z1 = zw + (i + 1) * run, y0 = i * rise, y1 = (i + 1) * rise;
            Grid((s, t) => { float z = Mathf.Lerp(z0, z1, t); return new Vector3(Mathf.Lerp(XL(z), XR(z), s), y1, z); }, 8, 2, Proj);        // piso
            Grid((s, t) => new Vector3(Mathf.Lerp(XL(z0), XR(z0), s), Mathf.Lerp(y0, y1, t), z0), 8, 2, Proj);                          // espelho
        }
        var stairMesh = new Mesh { name = c.Name + "_escada", indexFormat = IndexFormat.UInt32 };
        stairMesh.SetVertices(verts); stairMesh.SetUVs(0, uvs); stairMesh.SetTriangles(tris, 0);
        stairMesh.RecalculateBounds(); stairMesh.RecalculateNormals();
        string meshPath = Out3D + c.Name + "_escada.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(stairMesh, meshPath);
        var vis = new GameObject("Pintura da escada (degraus 3D)");
        vis.transform.SetParent(root.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = stairMesh;
        var vmr = vis.AddComponent<MeshRenderer>();
        vmr.shadowCastingMode = ShadowCastingMode.Off;
        vmr.receiveShadows = false;
        if (roomMF != null)
        {
            var roomMat = roomMF.GetComponent<MeshRenderer>().sharedMaterial;
            string matPath = Out3D + c.Name + "_escada.mat";
            var sm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (sm == null) { sm = new Material(roomMat); AssetDatabase.CreateAsset(sm, matPath); }
            sm.CopyPropertiesFromMaterial(roomMat);
            sm.shader = roomMat.shader;
            if (sm.HasProperty("_ZWrite")) sm.SetFloat("_ZWrite", 0f);     // degraus não cortam os pés da Luma
            sm.renderQueue = roomMat.shader.renderQueue + 1;               // depois da sala: a parede na frente cobre os degraus
            EditorUtility.SetDirty(sm);
            vmr.sharedMaterial = sm;
        }

        // ---------------------------------------------------------------- colisões (invisíveis)
        BoxCollider Box(string name, Vector3 pos, Quaternion rot, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return bc;
        }
        float XC(float z) => (XL(z) + XR(z)) * 0.5f;
        // Rampa pelo meio dos degraus: no meio de cada piso a Luma fica exatamente na altura dele.
        float zIn = zw - run * 0.5f, zUp = zt - run * 0.5f;
        var A0 = new Vector3(XC(zIn - 0.6f), -0.6f * tanA, zIn - 0.6f);
        var A1 = new Vector3(XC(zUp), Ht, zUp);
        Vector3 d = A1 - A0;
        var slope = Quaternion.LookRotation(d.normalized, Vector3.up);
        var fwdH = new Vector3(d.x, 0f, d.z).normalized;
        var yaw = Quaternion.LookRotation(fwdH, Vector3.up);
        float width = XR(zw) - XL(zw);
        const float thick = 1.2f;
        Box("Rampa (degraus)", (A0 + A1) * 0.5f - (slope * Vector3.up) * thick * 0.5f, slope, new Vector3(width, thick, d.magnitude));
        float landLen = zb - zUp;
        Box("Patamar", new Vector3(XC((zUp + zb) * 0.5f), Ht - 0.5f, (zUp + zb) * 0.5f), yaw, new Vector3(width + 0.8f, 1f, landLen / Mathf.Max(0.2f, fwdH.z)));
        float hWall = YTop(zb) + 1f;
        foreach (int side in new[] { -1, 1 })
        {
            System.Func<float, float> X = side < 0 ? (System.Func<float, float>)XL : XR;
            var p0 = new Vector3(X(zw), 0f, zw); var p1 = new Vector3(X(zb), 0f, zb);
            var dir = (p1 - p0).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (p0 + p1) * 0.5f + (rot * Vector3.right) * (side * 0.15f); mid.y = hWall * 0.5f;
            Box(side < 0 ? "Parede da escada (esquerda)" : "Parede da escada (direita)", mid, rot, new Vector3(0.3f, hWall, (p1 - p0).magnitude));
        }
        Box("Parede do patamar", new Vector3(XC(zb), hWall * 0.5f, zb + 0.15f), Quaternion.identity, new Vector3(XR(zb) - XL(zb) + 0.9f, hWall, 0.3f));

        // W/S seguem a escada (ela vai um pouco para a direita conforme sobe).
        var guia = Box("Guia da escada (W/S seguem os degraus)", new Vector3(XC((zw + zb) * 0.5f), Ht * 0.5f + 1.2f, (zw + zb) * 0.5f),
                       Quaternion.identity, new Vector3(XR(zb) - XL(zw) + 0.4f, Ht + 2.4f, zb - zw));
        guia.isTrigger = true;
        var eg = guia.gameObject.AddComponent<EscadaGuia>();
        eg.inclinacaoX = (XC(zt) - XC(zw)) / Mathf.Max(0.1f, zt - zw);

        // Saída no patamar (3º andar).
        var exitPos = new Vector3(XC(zt + e.landing * 0.7f), Ht, zt + e.landing * 0.7f);
        var exitGo = UmbraSceneBuilder.BuildExit(exitPos, e.nextScene);
        exitGo.name = "Saida_Escada (" + e.nextScene + ")";
        exitGo.transform.SetParent(root.transform, true);
        var eb = exitGo.GetComponent<BoxCollider>();
        eb.size = new Vector3(XR(zt) - XL(zt), 2.5f, e.landing * 0.5f);
        eb.center = new Vector3(0f, 1.25f, 0f);
        exitGo.GetComponent<LevelExit>().spawnId = e.nextSpawn;

        // Quem volta do 3º andar aparece no alto da escada e desce andando.
        float za = zw + L * 0.8f;
        var arrive = new Vector3(XC(za), tanA * (za - zw) + rise * 0.5f + 0.05f, za);
        var sps = FindAllByName(c.art.scene, "Spawn_" + e.arriveSpawn);
        if (sps.Count > 0)
        {
            if (undo) Undo.RecordObject(sps[0].transform, "Escada 3D");
            sps[0].transform.position = arrive;
        }
        else Spawn(e.arriveSpawn, arrive - Vector3.up * 0.05f, true).transform.SetParent(root.transform, true);

        // Vão na colisão da parede do corredor (as paredes da escada fecham os lados).
        float g0 = hx0 - 0.3f, g1 = hx1 + 0.3f;
        c.cfg.vaosNaParede.RemoveAll(v => Mathf.Abs(v.z - zw) < 0.3f && v.x < g1 && v.y > g0);
        c.cfg.vaosNaParede.Add(new Vector3(g0, g1, zw));
        EditorUtility.SetDirty(c.cfg);
        if (c.gen != null) CutOpening(c.gen, g0, g1, zw, undo);

        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        Debug.Log("[Umbra] Escada 3D: vão de x " + hx0.ToString("F2") + " a " + hx1.ToString("F2") + " na parede (z " + zw.ToString("F2") +
                  "), " + N + " degraus de " + (rise * 100f).ToString("F0") + " cm, " + L.ToString("F2") + " m de fundo, " + Ht.ToString("F2") +
                  " m de altura; câmera da escada em x " + camX.ToString("F2") + ".");
    }

    // =====================================================================
    // Escada de descida (Corredor 1): porta na parede, embaixo do lance de subida pintado, e degraus 3D
    // descendo para o térreo. A pintura não tem essa escada: degraus e paredes usam amostras da própria pintura.
    // =====================================================================

    /// <summary>Onde fica a porta da descida na pintura, o tamanho dos degraus e para onde ela leva.</summary>
    public class EscadaDescerCfg
    {
        public float u0 = 0.800f, u1 = 0.852f;      // bordas da porta (fração da pintura), à esquerda da escada de subida
        public float vTop = 0.40f;                  // topo da porta (fração do topo da pintura), abaixo do lance pintado
        public float angle = 35f;                   // inclinação (graus), igual à da subida
        public float rise = 0.18f;                  // altura de cada degrau (m)
        public int steps = 10;
        public float topLanding = 0.35f;            // piso no nível do corredor logo depois da porta (m)
        public float bottomLanding = 1.4f;          // patamar lá embaixo (m)
        // Textura dos degraus: faixa dos degraus pintados da escada de subida (longe do galho pintado na borda).
        public float su0 = 0.895f, su1 = 0.930f, sv0 = 0.37f, sv1 = 0.862f;
        public int paintedSteps = 9;
        public Color tint = new Color(0.8f, 0.78f, 0.85f);   // um pouco mais escuro que o corredor: o poço da escada
        // Câmera do poço: um pouco abaixo do topo da porta e recuada no corredor, olhando a Luma descer.
        public float camBelowTop = 0.25f, camBack = 1.6f, camFov = 50f;
        public float zoneStart = 0.2f;              // a câmera do poço entra quando ela passa da soleira (m)
        public string nextScene = "04_Corredor2", nextSpawn = "da_escada", arriveSpawn = "da_escada_de_baixo";
    }

    const string DescidaRoot = "Escada 3D (descida)";

    [MenuItem("Umbra/Cenário/Escada de descida do Corredor 1 (só a descida)", priority = 73)]
    static void MenuEscadaDescerCorredor()
    {
        var cfg = Object.FindAnyObjectByType<CenarioPSD3D>(FindObjectsInactive.Include);
        if (cfg == null || !cfg.gameObject.scene.name.StartsWith("02_Corredor1"))
        {
            EditorUtility.DisplayDialog("Umbra", "Abra a cena 02_Corredor1 primeiro.", "OK");
            return;
        }
        var c = MeasureOnly(cfg);
        if (c == null) return;
        BuildEscadaDescer3D(c, new EscadaDescerCfg(), true);
        EditorSceneManager.MarkSceneDirty(cfg.gameObject.scene);
    }

    /// <summary>
    /// Escada que desce, num vão atrás de uma porta aberta na parede pintada (como a subida, mas indo para baixo do chão).
    /// A Luma entra andando (W), desce pela rampa invisível e some aos poucos atrás da borda do chão do corredor;
    /// lá embaixo vai para o térreo. Quem volta do térreo aparece nos degraus de baixo e sobe andando (S).
    /// Só mexe nos objetos da descida, na abertura da pintura e nos vãos das colisões da parede e do chão.
    /// </summary>
    static void BuildEscadaDescer3D(Ctx c, EscadaDescerCfg e, bool undo)
    {
        float uMid = (e.u0 + e.u1) * 0.5f;
        var rail = Object.FindAnyObjectByType<CameraRail>();
        float camX = rail != null ? (c.X(uMid) > 0f ? rail.maxX : rail.minX) : 0f;
        float zw = c.WallZ(uMid);
        float kw = (c.D0 + zw) / c.D0;
        var C = new Vector3(camX, c.cy, -c.D0);                 // câmera quando a Luma está na escada
        float F(float z) => (c.D0 + z) / (c.D0 + zw);

        // Porta na parede pintada: bordas e topo encaixados nas colunas/linhas da malha da pintura.
        MeshFilter roomMF = null;
        if (c.gen != null)
            foreach (var mf in c.gen.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.StartsWith("Pintura projetada")) roomMF = mf;
        float hx0 = c.X(e.u0) * kw, hx1 = c.X(e.u1) * kw;
        float hTop = c.cy + (c.Y(e.vTop) - c.cy) * kw;
        if (roomMF != null && roomMF.sharedMesh != null)
        {
            var xs = new List<float>(); var ys = new List<float>();
            foreach (var p in roomMF.sharedMesh.vertices)
                if (Mathf.Abs(p.z - zw) < 1e-3f && p.y > 0.05f) { xs.Add(p.x); ys.Add(p.y); }
            float Snap(float x) { float best = x, bestD = float.MaxValue; foreach (var q in xs) { float dq = Mathf.Abs(q - x); if (dq < bestD) { bestD = dq; best = q; } } return best; }
            if (xs.Count > 0) { hx0 = Snap(hx0); hx1 = Snap(hx1); }
            float row = float.NegativeInfinity;
            foreach (var y in ys) if (y <= hTop + 1e-3f && y > row) row = y;
            if (row > 0.5f) hTop = row;
        }
        float XL(float z) => C.x + (hx0 - C.x) * F(z);           // lados do poço: raios da câmera pelas bordas da porta
        float XR(float z) => C.x + (hx1 - C.x) * F(z);
        float XC(float z) => (XL(z) + XR(z)) * 0.5f;

        // Degraus de verdade.
        float tanA = Mathf.Tan(e.angle * Mathf.Deg2Rad);
        int N = Mathf.Max(2, e.steps);
        float rise = e.rise, run = rise / tanA, Hd = N * rise;
        float zs = zw + e.topLanding, zEnd = zs + N * run, zb = zEnd + e.bottomLanding;
        float yBot = -Hd;

        // Remove a descida anterior (e a porta desligada que ficava no lugar dela).
        foreach (var n in new[] { DescidaRoot, "Escada_Descer (desligada)" })
            foreach (var go in FindAllByName(c.art.scene, n))
                if (undo) Undo.DestroyObjectImmediate(go); else Object.DestroyImmediate(go);
        var root = new GameObject(DescidaRoot);
        SceneManager.MoveGameObjectToScene(root, c.art.scene);
        if (undo) Undo.RegisterCreatedObjectUndo(root, "Escada de descida");

        // ---------------------------------------------------------------- porta na pintura da parede
        if (undo) Undo.RecordObject(c.cfg, "Escada de descida");
        c.cfg.aberturasNaPintura.RemoveAll(v => Mathf.Abs(v.z - zw) < 0.3f && v.x < hx1 && v.y > hx0);
        c.cfg.aberturasNaPintura.Add(new Vector4(hx0, hx1, zw, hTop));
        if (roomMF != null && roomMF.sharedMesh != null)
        {
            var mesh = roomMF.sharedMesh;
            if (undo) Undo.RecordObject(mesh, "Escada de descida");
            var vs = mesh.vertices;
            var tri = mesh.triangles;
            var keep = new List<int>(tri.Length);
            bool InDoor(Vector3 p) => Mathf.Abs(p.z - zw) < 1e-3f && p.y > -1e-3f && p.y <= hTop + 1e-3f &&
                                      p.x >= hx0 - 1e-3f && p.x <= hx1 + 1e-3f;
            for (int i = 0; i < tri.Length; i += 3)
            {
                if (InDoor(vs[tri[i]]) && InDoor(vs[tri[i + 1]]) && InDoor(vs[tri[i + 2]])) continue;
                keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]);
            }
            if (keep.Count != tri.Length)
            {
                mesh.SetTriangles(keep, 0);
                EditorUtility.SetDirty(mesh);
            }
        }

        // ---------------------------------------------------------------- degraus, patamares, paredes e teto (visual)
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        void Grid(System.Func<float, float, Vector3> P, int nu, int nv, System.Func<float, float, Vector3, Vector2> uv)
        {
            int start = verts.Count;
            for (int i = 0; i <= nu; i++)
                for (int j = 0; j <= nv; j++)
                {
                    float s = (float)i / nu, t = (float)j / nv;
                    var p = P(s, t);
                    verts.Add(p); uvs.Add(uv(s, t, p));
                }
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    int a = start + i * (nv + 1) + j, b = start + (i + 1) * (nv + 1) + j;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
                }
        }
        // Parede e chão lisos: a mesma faixa da pintura usada nas laterais dos degraus do nicho, repetida espelhada.
        var patch = c.cfg.amostraParede;
        float pu0 = Mathf.Clamp01(patch.x), pw = Mathf.Max(Mathf.Clamp01(patch.y) - pu0, 0.002f);
        float pvt = patch.y > patch.x + 0.002f ? c.FloorTop((patch.x + patch.y) * 0.5f) : 0.8f;
        float mU = 1f / c.canvas.size.x, mV = 1f / c.canvas.size.y;     // fração da pintura por metro
        Vector2 WallUV(float along, float down)
        {
            float uu = pu0 + Mathf.PingPong(Mathf.Abs(along) * mU, pw);
            float vv = 0.08f + Mathf.PingPong(Mathf.Abs(down) * mV, Mathf.Max(0.05f, pvt - 0.16f));
            return new Vector2(uu, 1f - vv);
        }
        Vector2 FloorUV(float x, float z)
        {
            float uu = pu0 + Mathf.PingPong(Mathf.Abs(x) * mU, pw);
            float vv = pvt + 0.03f + Mathf.PingPong(Mathf.Abs(z) * mV, Mathf.Max(0.02f, 1f - pvt - 0.06f));
            return new Vector2(uu, 1f - vv);
        }
        // Degraus: cada um pega um degrau pintado da escada de subida (metade de cima = piso, de baixo = espelho).
        float sh = (e.sv1 - e.sv0) / Mathf.Max(1, e.paintedSteps);
        Vector2 StepUV(int i, float s, float t, bool tread)
        {
            int k = 2 + i % Mathf.Max(1, e.paintedSteps - 4);
            float b0 = e.sv0 + k * sh;
            float vv = tread ? Mathf.Lerp(b0, b0 + sh * 0.5f, t) : Mathf.Lerp(b0 + sh * 0.5f, b0 + sh, t);
            return new Vector2(Mathf.Lerp(e.su0, e.su1, s), 1f - vv);
        }

        // Desenhado de trás para a frente (nada aqui escreve profundidade): fundo, paredes, teto, patamar de baixo,
        // degraus do mais fundo para o mais perto e, por último, o piso logo depois da porta.
        Grid((s, t) => new Vector3(Mathf.Lerp(XL(zb), XR(zb), s), Mathf.Lerp(yBot, hTop, t), zb), 8, 16,
             (s, t, p) => WallUV(p.x - XL(zb), hTop - p.y));
        foreach (int side in new[] { -1, 1 })
        {
            System.Func<float, float> X = side < 0 ? (System.Func<float, float>)XL : XR;
            float x0w = X(zw);
            Grid((s, t) => { float z = Mathf.Lerp(zw, zb, s); return new Vector3(X(z), Mathf.Lerp(yBot, hTop, t), z); }, 12, 16,
                 (s, t, p) => WallUV(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(x0w, zw)), hTop - p.y));
        }
        Grid((s, t) => { float z = Mathf.Lerp(zw, zb, t); return new Vector3(Mathf.Lerp(XL(z), XR(z), s), hTop, z); }, 8, 12,
             (s, t, p) => WallUV(p.x - XL(p.z), p.z - zw));                                                                 // teto
        Grid((s, t) => { float z = Mathf.Lerp(zEnd, zb, t); return new Vector3(Mathf.Lerp(XL(z), XR(z), s), yBot, z); }, 8, 6,
             (s, t, p) => FloorUV(p.x, p.z));                                                                               // patamar de baixo
        for (int i = N - 1; i >= 0; i--)
        {
            int k = i;
            float z0 = zs + i * run, z1 = zs + (i + 1) * run, yT = -(i + 1) * rise, yR = -i * rise;
            Grid((s, t) => { float z = Mathf.Lerp(z0, z1, t); return new Vector3(Mathf.Lerp(XL(z), XR(z), s), yT, z); }, 8, 2,
                 (s, t, p) => StepUV(k, s, t, true));                                                                       // piso
            Grid((s, t) => new Vector3(Mathf.Lerp(XL(z0), XR(z0), s), Mathf.Lerp(yT, yR, t), z0), 8, 2,
                 (s, t, p) => StepUV(k, s, t, false));                                                                      // espelho
        }
        Grid((s, t) => { float z = Mathf.Lerp(zw, zs, t); return new Vector3(Mathf.Lerp(XL(z), XR(z), s), 0f, z); }, 8, 2,
             (s, t, p) => FloorUV(p.x, p.z));                                                                               // piso da porta

        var stairMesh = new Mesh { name = c.Name + "_escada_descer", indexFormat = IndexFormat.UInt32 };
        stairMesh.SetVertices(verts); stairMesh.SetUVs(0, uvs); stairMesh.SetTriangles(tris, 0);
        stairMesh.RecalculateBounds(); stairMesh.RecalculateNormals();
        string meshPath = Out3D + c.Name + "_escada_descer.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(stairMesh, meshPath);
        var vis = new GameObject("Pintura da descida (degraus 3D)");
        vis.transform.SetParent(root.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = stairMesh;
        var vmr = vis.AddComponent<MeshRenderer>();
        vmr.shadowCastingMode = ShadowCastingMode.Off;
        vmr.receiveShadows = false;
        if (roomMF != null)
        {
            var roomMat = roomMF.GetComponent<MeshRenderer>().sharedMaterial;
            string matPath = Out3D + c.Name + "_escada_descer.mat";
            var sm = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (sm == null) { sm = new Material(roomMat); AssetDatabase.CreateAsset(sm, matPath); }
            sm.CopyPropertiesFromMaterial(roomMat);
            sm.shader = roomMat.shader;
            if (sm.HasProperty("_ZWrite")) sm.SetFloat("_ZWrite", 0f);     // degraus não cortam os pés da Luma
            if (sm.HasProperty("_Color")) sm.SetColor("_Color", e.tint);
            sm.renderQueue = roomMat.shader.renderQueue + 1;               // depois da sala: o chão do corredor cobre o poço
            EditorUtility.SetDirty(sm);
            vmr.sharedMaterial = sm;
        }

        // ---------------------------------------------------------------- colisões (invisíveis)
        BoxCollider Box(string name, Vector3 pos, Quaternion rot, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return bc;
        }
        // Rampa pelo meio dos degraus: começa no chão (meio degrau antes do primeiro) e passa pelo meio de cada piso.
        float zStart = zs - run * 0.5f, zLast = zEnd - run * 0.5f;
        var A0 = new Vector3(XC(zStart), 0f, zStart);
        var A1 = new Vector3(XC(zLast), -Hd, zLast);
        Vector3 d = A1 - A0;
        var slope = Quaternion.LookRotation(d.normalized, Vector3.up);
        var fwdH = new Vector3(d.x, 0f, d.z).normalized;
        var yaw = Quaternion.LookRotation(fwdH, Vector3.up);
        float width = XR(zw) - XL(zw);
        const float thick = 1.2f;
        Box("Rampa (degraus)", (A0 + A1) * 0.5f - (slope * Vector3.up) * thick * 0.5f, slope, new Vector3(width + 0.4f, thick, d.magnitude));
        float landLen = zb - zLast;
        Box("Patamar de baixo", new Vector3(XC((zLast + zb) * 0.5f), yBot - 0.5f, (zLast + zb) * 0.5f), yaw,
            new Vector3(width + 0.8f, 1f, landLen / Mathf.Max(0.2f, fwdH.z)));
        float wallLo = yBot - 1f, wallHi = hTop + 1f;
        foreach (int side in new[] { -1, 1 })
        {
            System.Func<float, float> X = side < 0 ? (System.Func<float, float>)XL : XR;
            var p0 = new Vector3(X(zw), 0f, zw); var p1 = new Vector3(X(zb), 0f, zb);
            var dir = (p1 - p0).normalized;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (p0 + p1) * 0.5f + (rot * Vector3.right) * (side * 0.15f); mid.y = (wallLo + wallHi) * 0.5f;
            Box(side < 0 ? "Parede da descida (esquerda)" : "Parede da descida (direita)", mid, rot,
                new Vector3(0.3f, wallHi - wallLo, (p1 - p0).magnitude));
        }
        Box("Parede do fundo da descida", new Vector3(XC(zb), (wallLo + wallHi) * 0.5f, zb + 0.15f), Quaternion.identity,
            new Vector3(XR(zb) - XL(zb) + 0.9f, wallHi - wallLo, 0.3f));
        // Teto (e parede acima da porta): sem ele, um pulo na porta levaria a Luma para dentro da parede.
        Box("Teto da descida", new Vector3(XC((zw + zb) * 0.5f), hTop + 1.5f, (zw - 0.05f + zb) * 0.5f), Quaternion.identity,
            new Vector3(Mathf.Max(XR(zw), XR(zb)) - Mathf.Min(XL(zw), XL(zb)) + 0.9f, 3f, zb - zw + 0.05f));

        // W/S seguem a escada (as paredes acompanham os raios da câmera, então ela vai um pouco de lado).
        var guia = Box("Guia da descida (W/S seguem os degraus)", new Vector3(XC((zw + zb) * 0.5f), (yBot + hTop) * 0.5f, (zw + zb) * 0.5f),
                       Quaternion.identity, new Vector3(Mathf.Abs(XC(zb) - XC(zw)) + width + 0.4f, hTop - yBot + 0.5f, zb - zw));
        guia.isTrigger = true;
        var eg = guia.gameObject.AddComponent<EscadaGuia>();
        eg.inclinacaoX = (XC(zEnd) - XC(zw)) / Mathf.Max(0.1f, zEnd - zw);

        // Câmera da descida: a do trilho fica alta demais (vista de lá, descer 1,8 m quase não muda a altura na tela
        // e os degraus ficam atrás da soleira). Dentro do poço, uma câmera acima da porta olha a Luma descendo os degraus.
        var camGo = new GameObject("CM_Descida");
        camGo.transform.SetParent(root.transform, false);
        var camPos = new Vector3(XC(zw), hTop - e.camBelowTop, zw - e.camBack);
        var lookAt = new Vector3(XC((zs + zEnd) * 0.5f), -Hd * 0.5f, (zs + zEnd) * 0.5f);
        camGo.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(lookAt - camPos, Vector3.up));
        var cm = camGo.AddComponent<CinemachineCamera>();
        var lens = cm.Lens;
        lens.FieldOfView = e.camFov;
        cm.Lens = lens;
        cm.Priority = 0;
        var luma = GameObject.FindWithTag("Player");
        if (luma != null)
        {
            cm.LookAt = luma.transform;
            var rc = camGo.AddComponent<CinemachineRotationComposer>();
            rc.TargetOffset = new Vector3(0f, 0.6f, 0f);
            rc.Damping = new Vector2(0.8f, 0.8f);
        }
        var zoneGo = new GameObject("Zona_CM_Descida");
        zoneGo.transform.SetParent(root.transform, false);
        zoneGo.transform.position = new Vector3(XC((zw + zb) * 0.5f), (yBot + hTop) * 0.5f, (zw + e.zoneStart + zb) * 0.5f);
        var zb2 = zoneGo.AddComponent<BoxCollider>();
        zb2.isTrigger = true;
        zb2.size = new Vector3(Mathf.Abs(XC(zb) - XC(zw)) + width + 0.4f, hTop - yBot + 1f, zb - zw - e.zoneStart);
        var zone = zoneGo.AddComponent<CameraZone>();
        zone.zoneCamera = cm;
        zone.priority = 5;                                   // vence a zona do trilho (que cobre o corredor todo)
        zone.cut = false;
        zone.blendTime = 0.9f;

        // Saída lá embaixo (térreo).
        float zx = zEnd + e.bottomLanding * 0.3f;
        var exitGo = UmbraSceneBuilder.BuildExit(new Vector3(XC(zx), yBot, zx), e.nextScene);
        exitGo.name = "Saida_Descida (" + e.nextScene + ")";
        exitGo.transform.SetParent(root.transform, true);
        var eb = exitGo.GetComponent<BoxCollider>();
        eb.size = new Vector3(width + 0.4f, 2.5f, e.bottomLanding * 0.6f);
        eb.center = new Vector3(0f, 1.25f, 0f);
        exitGo.GetComponent<LevelExit>().spawnId = e.nextSpawn;

        // Quem volta do térreo aparece nos degraus de baixo e sobe andando (S).
        float za = zs + (N - 2.5f) * run;
        var arrive = new Vector3(XC(za), -(N - 2) * rise + 0.05f, za);
        var sps = FindAllByName(c.art.scene, "Spawn_" + e.arriveSpawn);
        if (sps.Count > 0)
        {
            if (undo) Undo.RecordObject(sps[0].transform, "Escada de descida");
            sps[0].transform.position = arrive;
        }
        else
        {
            var sp = Spawn(e.arriveSpawn, arrive - Vector3.up * 0.05f, false);
            sp.transform.SetParent(root.transform, true);
        }

        // Vãos nas colisões: parede do corredor (as paredes do poço fecham os lados) e chão sobre o poço.
        float g0 = hx0 - 0.3f, g1 = hx1 + 0.3f;
        c.cfg.vaosNaParede.RemoveAll(v => Mathf.Abs(v.z - zw) < 0.3f && Mathf.Abs(v.x - g0) < 0.05f && Mathf.Abs(v.y - g1) < 0.05f);
        c.cfg.vaosNaParede.Add(new Vector3(g0, g1, zw));
        float f0 = Mathf.Min(XL(zStart), XL(zb)), f1 = Mathf.Max(XR(zStart), XR(zb));
        c.cfg.buracosNoChao.RemoveAll(v => v.x < f1 && v.y > f0 && v.z < zb + 0.3f && v.w > zStart);
        c.cfg.buracosNoChao.Add(new Vector4(f0, f1, zStart, zb + 0.3f));
        EditorUtility.SetDirty(c.cfg);
        if (c.gen != null)
        {
            CutOpening(c.gen, g0, g1, zw, undo);
            CutFloor(c.gen, f0, f1, zStart, zb + 0.3f, undo);
        }

        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        // A Luma (~1 m) some atrás da borda do chão quando a cabeça fica abaixo do raio da câmera pela soleira da porta.
        float slopeRay = c.cy / Mathf.Max(0.1f, c.D0 + zw);
        float hidden = tanA > slopeRay ? 1.05f / (tanA - slopeRay) : float.PositiveInfinity;
        Debug.Log("[Umbra] Escada de descida: porta de x " + hx0.ToString("F2") + " a " + hx1.ToString("F2") + " (z " + zw.ToString("F2") +
                  "), topo " + hTop.ToString("F2") + " m; " + N + " degraus de " + (rise * 100f).ToString("F0") + " cm, " +
                  (zEnd - zs).ToString("F2") + " m de fundo, desce " + Hd.ToString("F2") + " m. Luma some a " + hidden.ToString("F2") +
                  " m da porta; saída a " + (zx - zw).ToString("F2") + " m. Câmera da escada em x " + camX.ToString("F2") + ".");
    }

    /// <summary>Todos os objetos com esse nome na cena (inclusive desligados).</summary>
    static List<GameObject> FindAllByName(Scene scene, string name)
    {
        var list = new List<GameObject>();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) list.Add(t.gameObject);
        return list;
    }

    /// <summary>Abre um vão (x0..x1) nas colisões retas da parede que ficam no z da parede.</summary>
    static void CutOpening(Transform under, float x0, float x1, float zWall, bool undo)
    {
        if (under == null) return;
        foreach (var bc in under.GetComponentsInChildren<BoxCollider>(true))
        {
            if (bc == null || bc.isTrigger || !bc.name.StartsWith("Parede") || bc.name.StartsWith("Parede_Quina")) continue;
            while (bc.name.Contains(" (depois do vão) (depois do vão)"))
                bc.name = bc.name.Replace(" (depois do vão) (depois do vão)", " (depois do vão)");
            var t = bc.transform;
            if (Quaternion.Angle(t.rotation, Quaternion.identity) > 1f) continue;
            Vector3 ctr = t.TransformPoint(bc.center);
            Vector3 sz = Vector3.Scale(bc.size, t.lossyScale);
            float minx = ctr.x - sz.x * 0.5f, maxx = ctr.x + sz.x * 0.5f;
            float minz = ctr.z - sz.z * 0.5f, maxz = ctr.z + sz.z * 0.5f;
            if (zWall < minz - 0.15f || zWall > maxz + 0.15f) continue;
            if (x1 <= minx + 0.01f || x0 >= maxx - 0.01f) continue;      // já está fora do vão
            bool hasLeft = x0 - minx > 0.05f, hasRight = maxx - x1 > 0.05f;
            void Fit(BoxCollider b, float lo, float hi)
            {
                var tr = b.transform;
                Vector3 cw = tr.TransformPoint(b.center);
                tr.position += Vector3.right * ((lo + hi) * 0.5f - cw.x);
                var s2 = b.size; s2.x = (hi - lo) / Mathf.Max(1e-4f, tr.lossyScale.x); b.size = s2;
            }
            if (hasRight)
            {
                var r = Object.Instantiate(bc.gameObject, t.parent);
                r.name = bc.name.EndsWith(" (depois do vão)") ? bc.name : bc.name + " (depois do vão)";
                if (undo) Undo.RegisterCreatedObjectUndo(r, "Vão na parede");
                Fit(r.GetComponent<BoxCollider>(), x1, maxx);
            }
            if (hasLeft)
            {
                if (undo) { Undo.RecordObject(t, "Vão na parede"); Undo.RecordObject(bc, "Vão na parede"); }
                Fit(bc, minx, x0);
            }
            else if (undo) Undo.DestroyObjectImmediate(bc.gameObject);
            else Object.DestroyImmediate(bc.gameObject);
        }
    }

    /// <summary>Abre um buraco (x0..x1, z0..z1) nas colisões retas do chão ("Chao"), repartindo a caixa em volta dele.</summary>
    static void CutFloor(Transform under, float x0, float x1, float z0, float z1, bool undo)
    {
        if (under == null) return;
        foreach (var bc in under.GetComponentsInChildren<BoxCollider>(true))
        {
            if (bc == null || bc.isTrigger || !bc.name.StartsWith("Chao")) continue;
            var t = bc.transform;
            if (Quaternion.Angle(t.rotation, Quaternion.identity) > 1f) continue;
            Vector3 ctr = t.TransformPoint(bc.center);
            Vector3 sz = Vector3.Scale(bc.size, t.lossyScale);
            float minx = ctr.x - sz.x * 0.5f, maxx = ctr.x + sz.x * 0.5f;
            float minz = ctr.z - sz.z * 0.5f, maxz = ctr.z + sz.z * 0.5f;
            if (x1 <= minx + 0.01f || x0 >= maxx - 0.01f || z1 <= minz + 0.01f || z0 >= maxz - 0.01f) continue;
            // Pedaços em volta do buraco: esquerda e direita inteiras em z; frente e fundo só na largura do buraco.
            var pecas = new List<(float, float, float, float)>
            {
                (minx, x0, minz, maxz), (x1, maxx, minz, maxz),
                (Mathf.Max(minx, x0), Mathf.Min(maxx, x1), minz, z0), (Mathf.Max(minx, x0), Mathf.Min(maxx, x1), z1, maxz),
            };
            foreach (var (a0, a1, b0, b1) in pecas)
            {
                if (a1 - a0 < 0.05f || b1 - b0 < 0.05f) continue;
                var go = Object.Instantiate(bc.gameObject, t.parent);
                go.name = "Chao (em volta do buraco)";
                if (undo) Undo.RegisterCreatedObjectUndo(go, "Buraco no chão");
                var nb = go.GetComponent<BoxCollider>();
                go.transform.position += new Vector3((a0 + a1) * 0.5f - ctr.x, 0f, (b0 + b1) * 0.5f - ctr.z);
                var s2 = nb.size;
                s2.x = (a1 - a0) / Mathf.Max(1e-4f, t.lossyScale.x);
                s2.z = (b1 - b0) / Mathf.Max(1e-4f, t.lossyScale.z);
                nb.size = s2;
            }
            if (undo) Undo.DestroyObjectImmediate(bc.gameObject); else Object.DestroyImmediate(bc.gameObject);
        }
    }

    static void ApplyLensShift(Camera cam, float shiftY)
    {
        cam.usePhysicalProperties = true;
        cam.sensorSize = new Vector2(36f, 20.25f);
        cam.gateFit = Camera.GateFitMode.Vertical;
        cam.fieldOfView = Fov;
        cam.lensShift = new Vector2(0f, shiftY);
    }

    /// <summary>Material dos móveis encostados no fundo (desenhados antes da Luma, sem profundidade).</summary>
    static Material SpriteFundoMaterial()
    {
        const string path = "Assets/Dados/Materiais/SpriteFundo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var sh = Shader.Find("Umbra/Sprite Fundo");
        if (sh == null) { Debug.LogWarning("[Umbra] Shader 'Umbra/Sprite Fundo' não encontrado."); return null; }
        UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
        mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>
    /// O PSD Importer posiciona errado camadas que passam da borda do documento.
    /// Corrige usando Assets/Dados/PSD/&lt;nome&gt;.layers.txt  (nome, x0, y0, x1, y1 em pixels).
    /// </summary>
    static void FixLayerPlacement(GameObject inst, string psdPath)
    {
        string file = "Assets/Dados/PSD/" + System.IO.Path.GetFileNameWithoutExtension(psdPath) + ".layers.txt";
        if (!System.IO.File.Exists(file)) return;
        var lines = System.IO.File.ReadAllLines(file);
        var head = lines[0].Split(' ');
        float W = float.Parse(head[0]), H = float.Parse(head[1]);
        var canvas = UmbraSceneBuilder.CanvasBounds(inst);
        var boxes = new Dictionary<string, float[]>();
        var dup = new HashSet<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split('\t');
            if (f.Length < 5) continue;
            string key = f[0].Trim();
            if (boxes.ContainsKey(key)) { dup.Add(key); continue; }
            boxes[key] = new[] { float.Parse(f[1]), float.Parse(f[2]), float.Parse(f[3]), float.Parse(f[4]) };
        }
        int fixedCount = 0;
        var opaco = LoadOpaque(psdPath);
        foreach (var sr in inst.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null || dup.Contains(sr.gameObject.name.Trim()) || !boxes.TryGetValue(sr.gameObject.name.Trim(), out var b)) continue;
            if (b[2] - b[0] < 1f) continue;
            Bounds actual = sr.bounds;
            Rect full = new Rect(b[0], b[1], b[2] - b[0], b[3] - b[1]);
            Rect clip = Rect.MinMaxRect(Mathf.Max(0, b[0]), Mathf.Max(0, b[1]), Mathf.Min(W, b[2]), Mathf.Min(H, b[3]));
            Rect best = full;
            float ax = actual.size.x / canvas.size.x * W, ay = actual.size.y / canvas.size.y * H;
            float Diff(Rect r) => Mathf.Abs(r.width - ax) + Mathf.Abs(r.height - ay);
            if (Diff(clip) < Diff(best)) best = clip;
            // O PSD Importer às vezes corta a camada na parte desenhada (sem a sobra transparente):
            // aí o lugar certo é o da parte opaca.
            if (opaco.TryGetValue(sr.gameObject.name.Trim(), out var ol) && ol.Count == 1)
            {
                var op = ol[0].op;
                Rect opaque = Rect.MinMaxRect(Mathf.Max(0, op[0]), Mathf.Max(0, op[1]), Mathf.Min(W, op[2]), Mathf.Min(H, op[3]));
                if (Diff(opaque) < Diff(best)) best = opaque;
            }
            Vector2 want = new Vector2(canvas.min.x + best.center.x / W * canvas.size.x,
                                       canvas.max.y - best.center.y / H * canvas.size.y);
            Vector2 delta = want - (Vector2)actual.center;
            if (delta.magnitude < canvas.size.y * 0.004f) continue;
            sr.transform.position += new Vector3(delta.x, delta.y, 0f);
            fixedCount++;
        }
        if (fixedCount > 0) Debug.Log("[Umbra] " + fixedCount + " camada(s) do " + psdPath + " reposicionadas (passavam da borda do PSD).");
    }

    static void PrepareLayers(GameObject inst, Spec s)
    {
        foreach (var sr in inst.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string n = sr.gameObject.name.Trim();
            if (s.opacity.TryGetValue(n, out float a))
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, a);
                if (a <= 0.01f) sr.gameObject.SetActive(false);
            }
            foreach (var h in s.hide) if (h == n) sr.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------ cômodos provisórios (sem arte)

    static Ctx BeginGrey(string sceneName, float W, float D, Color ambient, float darkness, string placa)
    {
        var c = new Ctx { spec = new Spec { scene = sceneName }, painted = false };
        c.scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Ambient(ambient, new Color(0.04f, 0.03f, 0.04f));
        c.cam = MainCamera();
        UmbraSceneBuilder.BuildVolume(sceneName, null, 0f, 0.35f);
        c.room = UmbraGreybox.BuildRoom(new UmbraGreybox.RoomSettings { name = sceneName, width = W, depth = D, height = 5f }, Vector3.zero);
        foreach (var mr in c.room.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = mr.name.ToLowerInvariant();
            var col = n.Contains("chao") ? new Color(0.13f, 0.1f, 0.08f) : new Color(0.17f, 0.15f, 0.19f);
            mr.sharedMaterial = UmbraGreybox.GreyMaterial(n.Contains("chao") ? "Greybox_ChaoEscuro" : "Greybox_ParedeEscura", col);
        }
        c.canvas = new Bounds(new Vector3(0f, 2.5f, 0f), new Vector3(W, 5f, 0f));
        c.luma = UmbraSceneBuilder.BuildLuma(new Vector3(-W / 2 + 1.2f, 0.05f, -0.3f));
        UmbraSetup.ConfigureScene();
        RailCamera(c.cam, c.luma, new Bounds(new Vector3(0, 2f, 0), new Vector3(W, 5.2f, 1f)));
        var veu = c.cam.GetComponent<DarknessOverlay>();
        if (veu != null) veu.darkness = darkness;
        Sign(placa, new Vector3(0f, 3.4f, D * 0.5f - 0.05f), 5f, new Color(1f, 1f, 1f, 0.22f));
        return c;
    }

    /// <summary>Texto no cenário (placa). Útil para marcar o que ainda não tem arte.</summary>
    static void Sign(string text, Vector3 pos, float width, Color color)
    {
        var go = new GameObject("Placa: " + text.Split('\n')[0]);
        go.transform.position = pos;
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = 3f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.rectTransform.sizeDelta = new Vector2(width, 1.6f);
    }

    // ------------------------------------------------------------ câmera

    static void RailCamera(Camera cam, GameObject luma, Bounds canvas, float viewHeight = -1f, float camY = float.NaN, float shiftY = 0f, float travel = 1f)
    {
        if (viewHeight <= 0f) viewHeight = canvas.size.y;
        if (float.IsNaN(camY)) camY = canvas.center.y;
        float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        float dist = (viewHeight * View) / tanV;
        float halfW = dist * tanV * (16f / 9f);
        cam.transform.SetPositionAndRotation(new Vector3(canvas.center.x, camY, -dist), Quaternion.identity);
        ApplyLensShift(cam, shiftY);

        var follow = UmbraCameras.SetupSilent();
        if (follow != null)
        {
            var pc = follow.GetComponent<CinemachinePositionComposer>();
            if (pc != null) { pc.CameraDistance = 9f; pc.TargetOffset = new Vector3(0f, 0.9f, 0f); }
        }
        var rail = UmbraSceneBuilder.BuildFixedCamera("CM_Trilho", cam.transform.position, cam.transform.rotation,
            new Vector3(canvas.center.x, canvas.center.y, 0f), new Vector3(canvas.size.x + 6f, canvas.size.y + 8f, 60f), Fov);
        var r = rail.gameObject.AddComponent<CameraRail>();
        r.target = luma.transform;
        ConfigureRail(r, rail, canvas, viewHeight, camY, shiftY, travel);
    }

    /// <summary>Posição, lente (lens shift) e limites da câmera em trilho.</summary>
    static void ConfigureRail(CameraRail r, CinemachineCamera rail, Bounds canvas, float viewHeight, float camY, float shiftY, float travel)
    {
        float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
        float dist = (viewHeight * View) / tanV;
        float halfW = dist * tanV * (16f / 9f);
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(new Vector3(canvas.center.x, camY, -dist), Quaternion.identity);
            ApplyLensShift(cam, shiftY);
        }
        r.transform.SetPositionAndRotation(new Vector3(canvas.center.x, camY, -dist), Quaternion.identity);
        if (rail != null)
        {
            var lens = rail.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Physical;
            lens.PhysicalProperties.SensorSize = new Vector2(36f, 20.25f);
            lens.PhysicalProperties.GateFit = Camera.GateFitMode.Vertical;
            lens.PhysicalProperties.LensShift = new Vector2(0f, shiftY);
            lens.FieldOfView = Fov;
            rail.Lens = lens;
            EditorUtility.SetDirty(rail);
        }
        r.rotateInstead = false;         // cenário 3D de verdade: a câmera anda (paralaxe)
        r.wallDistance = dist;
        r.canvasMinX = canvas.min.x;
        r.canvasMaxX = canvas.max.x;
        r.verticalFov = Fov;
        r.followSpeed = 3f;
        // travel < 1: a câmera anda um pouco menos, para o que está perto dela não mostrar além da pintura.
        r.minX = Mathf.Min(canvas.center.x, canvas.center.x + (canvas.min.x + halfW - canvas.center.x) * travel);
        r.maxX = Mathf.Max(canvas.center.x, canvas.center.x + (canvas.max.x - halfW - canvas.center.x) * travel);
        r.Snap();
        EditorUtility.SetDirty(r);
    }

    static void Finish(Ctx c)
    {
        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm != null && gm.startPoint != null && c.luma != null) gm.startPoint.position = c.luma.transform.position;
        Save(c.scene, c.spec.scene);
        Selection.activeGameObject = c.luma;
    }

    static void Save(Scene scene, string name)
    {
        UmbraGreybox.EnsureFolder("Assets/Scenes");
        string path = "Assets/Scenes/" + name + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        UmbraSceneBuilder.AddToBuild(path);
        Debug.Log("[Umbra] " + name + " montada e salva.");
    }

    [MenuItem("Umbra/Montar cena/Organizar a lista de cenas do build", priority = 40)]
    public static void OrderBuildScenes()
    {
        // Ordem do jogo (2º andar = Arco 1). Depois: cenas fora da demo (escada ainda bloqueada).
        string[] order = { "00_Menu", "00_Pesadelo", "01_Dormitorio1", "02_Corredor1", "03_Banheiro", "08_Biblioteca",
                           "09_LazyRoom", "06_Dormitorio2", "10_LivingRoom", "04_Corredor2", "05_Andar3", "07_Escritorio" };
        var list = new List<EditorBuildSettingsScene>();
        foreach (var n in order)
        {
            string p = "Assets/Scenes/" + n + ".unity";
            if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
        }
        foreach (var s in EditorBuildSettings.scenes)
            if (!list.Exists(x => x.path == s.path) && !s.path.Contains("SampleScene")) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }

    // =====================================================================
    // Peças
    // =====================================================================

    static void Ambient(Color amb, Color fog)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = amb;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fog;
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 45f;
    }

    static Camera MainCamera()
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = Fov;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        camGo.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camGo.AddComponent<CinemachineBrain>();

        var veu = camGo.AddComponent<DarknessOverlay>();
        var sh = Shader.Find("Umbra/Escuridao");
        if (sh != null)
        {
            UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
            const string path = "Assets/Dados/Materiais/Escuridao_Veu.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
            veu.material = mat;
        }
        veu.darkness = 0.55f;
        return cam;
    }

    internal static SpawnPoint Spawn(string id, Vector3 pos, bool faceLeft)
    {
        var go = new GameObject("Spawn_" + id);
        go.transform.position = pos + Vector3.up * 0.05f;
        var sp = go.AddComponent<SpawnPoint>();
        sp.id = id;
        sp.faceLeft = faceLeft;
        return sp;
    }

    internal static HintTrigger Hint(string name, Vector3 floorPos, Vector3 size, string text, bool subtitle, string onceFlag)
    {
        var go = new GameObject(name);
        go.transform.position = floorPos;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        var h = go.AddComponent<HintTrigger>();
        h.text = text;
        h.asSubtitle = subtitle;
        h.onceFlag = onceFlag;
        return h;
    }

    static DoorExit Door(string name, Vector3 pos, string scene, string spawn, string prompt)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.4f, 0.4f, 0.4f);
        box.center = new Vector3(0f, 2.5f, 0f);          // fora do caminho: a porta abre só com E
        var le = go.AddComponent<LevelExit>();
        le.loadOnEnter = false;
        le.nextScene = scene;
        le.spawnId = spawn;
        le.fadeTime = 0.8f;
        var d = go.AddComponent<DoorExit>();
        d.nextScene = scene;
        d.spawnId = spawn;
        d.prompt = prompt;
        d.extraRange = 0.3f;
        return d;
    }

    static HidingSpot Hide(string name, Vector3 pos, bool hideSprite)
    {
        var go = new GameObject("Esconderijo: " + name);
        go.transform.position = pos;
        var h = go.AddComponent<HidingSpot>();
        h.prompt = "Esconder";
        h.hideSprite = hideSprite;
        var hp = new GameObject("HidePoint").transform;
        hp.SetParent(go.transform, false);
        hp.localPosition = new Vector3(0f, 0.05f, 0f);
        h.hidePoint = hp;
        return h;
    }

    static T TriggerBox<T>(string name, Vector3 floorPos, Vector3 size) where T : Component
    {
        var go = new GameObject(name);
        go.transform.position = floorPos;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        return go.AddComponent<T>();
    }

    static Transform Marker(string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.position = pos;
        return t;
    }

    static GameObject Block(string name, Vector3 center, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = UmbraGreybox.GreyMaterial("Greybox_" + name.Split(' ')[0], color);
        return go;
    }

    static void Glow(Transform parent)
    {
        var g = new GameObject("Brilho");
        g.transform.SetParent(parent, false);
        GlowSprite(g.transform);
    }

    internal static void GlowSprite(Transform parent)
    {
        var sp = LoadGen("Assets/Sprites/UI/Brilho.png");
        if (sp == null) return;
        var go = new GameObject("Brilho_Sprite");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = 250;
        float size = 0.55f;
        go.transform.localScale = Vector3.one * (size / Mathf.Max(sp.bounds.size.x, 0.01f));
        go.AddComponent<GlowPulse>();
    }

    /// <summary>Tábua solta: faz barulho quando pisada (é preciso pular).</summary>
    static void Plank(Ctx c, float fx)
    {
        float zf = c.frontZ, zb = c.WallZ(fx) - 0.1f, zm = (zf + zb) * 0.5f, depth = zb - zf;
        float x = c.AtDepth(fx, 0f, zm).x;
        var go = Block("Tabua solta", new Vector3(x, 0.01f, zm), new Vector3(0.5f, 0.02f, depth), new Color(0.32f, 0.24f, 0.2f));
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var trig = new GameObject("Ruido_Tabua");
        trig.transform.SetParent(go.transform, true);
        trig.transform.position = new Vector3(x, 0.3f, zm);
        var box = trig.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.5f, 0.5f, depth);
        var ne = trig.AddComponent<NoiseEmitter>();
        ne.radius = 12f;
        ne.emitOnPlayerEnter = true;
        var fa = trig.AddComponent<FlagActions>();
        ne.onEmit = new UnityEvent();
        UnityEventTools.AddStringPersistentListener(ne.onEmit, fa.Subtitle, "(CREC! a tábua range)");
    }

    /// <summary>Véu escuro sobre o cenário: aparece quando a luz apaga.</summary>
    static GameObject Darkness(Ctx c, float alpha)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Escuridao (luz apagada)";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        float z = c.painted ? c.frontZ + 0.2f : -1.5f;
        float k = c.painted ? (c.D0 + z) / c.D0 : 1f;
        go.transform.position = new Vector3(c.canvas.center.x, c.canvas.center.y * k, z);
        go.transform.localScale = new Vector3(c.canvas.size.x * 2f, c.canvas.size.y * 2f, 1f);
        UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
        string path = "Assets/Dados/Materiais/Escuridao.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
        }
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        var mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
        var cols = new Color[mesh.vertexCount];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color(0f, 0f, 0.01f, alpha);
        mesh.colors = cols;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.SetActive(false);
        return go;
    }

    static GameObject Cone(string name, Vector3 floorPos, float top, float width)
    {
        var go = new GameObject(name);
        go.transform.position = floorPos;
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var m = new Mesh();
        m.vertices = new[] { new Vector3(-0.15f, top, 0), new Vector3(0.15f, top, 0), new Vector3(width * 0.5f, 0, 0), new Vector3(-width * 0.5f, 0, 0) };
        m.colors = new[] { new Color(1, 0.9f, 0.75f, 0.13f), new Color(1, 0.9f, 0.75f, 0.13f), new Color(1, 0.9f, 0.75f, 0f), new Color(1, 0.9f, 0.75f, 0f) };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        mf.sharedMesh = m;
        UmbraGreybox.EnsureFolder("Assets/Dados/Materiais");
        string path = "Assets/Dados/Materiais/Facho.mat";
        var sh = Shader.Find("Umbra/Brilho Aditivo") ?? Shader.Find("Sprites/Default");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = sh;                                   // facho de luz aditivo: clareia, nunca vira um triângulo branco
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(1f, 0.85f, 0.65f, 1f));
        if (mat.HasProperty("_Intensity")) mat.SetFloat("_Intensity", 0.9f);
        EditorUtility.SetDirty(mat);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    // ------------------------------------------------------------ criaturas

    static CreatureAI BuildWalker(string name, Sprite sprite, float height, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        SpriteChild(go.transform, "Sprite", sprite, height, 0f).gameObject.AddComponent<FaceCamera>();
        var eyes = new GameObject("Olhos").transform;
        eyes.SetParent(go.transform, false);
        eyes.localPosition = new Vector3(0f, height * 0.75f, 0f);
        var ai = go.AddComponent<CreatureAI>();
        ai.eyes = eyes;
        ai.obstacleMask = ~0;
        ai.onStartChase = new UnityEvent();
        ai.onLosePlayer = new UnityEvent();
        ai.onCatch = new UnityEvent();
        return ai;
    }

    static InspetoraMass BuildMass(string name, Vector3 frontPos, float height, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = frontPos;
        var parts = new List<Transform>();
        var sp = GenSprite("Inspetora.png");
        for (int i = 0; i < 3; i++)
        {
            var sr = SpriteChild(go.transform, "Massa " + i, sp, height * (1f - i * 0.12f), 0f);
            sr.transform.localPosition = new Vector3(sr.bounds.size.x * 0.4f + i * 1.3f, 0f, i * 0.02f);
            parts.Add(sr.transform);
        }
        var m = go.AddComponent<InspetoraMass>();
        m.front = go.transform;
        m.wobbleParts = parts.ToArray();
        return m;
    }

    static GameObject InspetoraSprite(string name, float height)
    {
        var go = new GameObject(name);
        var sp = LoadSprite(Cen + "corredor 2.psd", "Camada 8") ?? GenSprite("Inspetora.png");
        var sr = SpriteChild(go.transform, "Sprite", sp, height, 0f);
        sr.transform.localPosition = Vector3.zero;
        return go;
    }

    /// <summary>Sprite de personagem/criatura: desenhado depois da Luma (na frente dos móveis do fundo).</summary>
    static SpriteRenderer SpriteChild(Transform parent, string name, Sprite sprite, float height, float x)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 150;
        if (sprite != null)
        {
            float s = height / Mathf.Max(sprite.bounds.size.y, 0.01f);
            go.transform.localScale = Vector3.one * s;
            go.transform.localPosition = new Vector3(x, -sprite.bounds.min.y * s, 0f);
        }
        return sr;
    }

    static Sprite LoadSprite(string assetPath, string spriteName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
            if (o is Sprite sp && sp.name == spriteName) return sp;
        return null;
    }

    static Sprite GenSprite(string file) => LoadGen(Criaturas + file);

    static Sprite LoadGen(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[Umbra] Sprite provisório não encontrado: " + path); return null; }
        if (ti.textureType != TextureImporterType.Sprite || Mathf.Abs(ti.spritePixelsPerUnit - 200f) > 0.1f)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 200f;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
