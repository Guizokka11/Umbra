using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Arco 1 (nictofobia): ficar no escuro é perigoso. Fora de qualquer LightZone, depois de alguns segundos:
///  sussurros, vinheta fechando, véu de escuridão mais fechado, mãos de sombra saindo das bordas da tela
///  e medo subindo mais rápido. Medo no máximo no escuro = PÂNICO: a Luma anda devagar (não corre) e faz barulho,
///  o que atrai as criaturas da cena e chama a Inspetora da ronda, se ela estiver no andar.
/// Voltar para a luz alivia devagar. Escondida, lendo ou abraçando o urso, o escuro não avança.
/// Tudo por cima da imagem (UI, vinheta, véu) e som: não mexe na pintura.
/// Nasce sozinho; configuração em Assets/Dados/Resources/MedoDoEscuro.asset; por cena: EscuroNaCena.
/// </summary>
public class MedoDoEscuro : MonoBehaviour
{
    public static MedoDoEscuro Instance { get; private set; }

    /// <summary>0 a 1: quanto o escuro pesa agora.</summary>
    public float Intensidade { get; private set; }
    public bool EmPanico { get; private set; }
    public bool Ativo { get; private set; }
    /// <summary>Quanto o véu de escuridão fecha a mais agora (DarknessOverlay).</summary>
    public float VeuExtra => Ativo && cfg != null ? Intensidade * cfg.veuExtra : 0f;

    MedoDoEscuroConfig global, cfg;
    float noEscuro, proxSussurro, proxBarulho, lightFallOriginal = -1f;
    bool mudouVelocidade;
    FearSystem medoDaCena;
    AudioSource loop, soltos, choro;
    RectTransform tela;
    readonly List<Mao> maos = new List<Mao>();
    static Sprite maoProvisoria;

    class Mao
    {
        public RectTransform rt;
        public Image img;
        public Vector2 borda;        // posição na borda (0..1 da tela)
        public Vector2 paraDentro;   // direção para o centro
        public float semente;
    }

    // ------------------------------------------------------------------ início

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("Umbra_MedoDoEscuro");
        DontDestroyOnLoad(go);
        go.AddComponent<MedoDoEscuro>();
    }

    void Awake()
    {
        Instance = this;
        global = Resources.Load<MedoDoEscuroConfig>("MedoDoEscuro");
        if (global == null) global = ScriptableObject.CreateInstance<MedoDoEscuroConfig>();
        loop = Fonte("Sussurros (loop)", true);
        soltos = Fonte("Sussurros (soltos)", false);
        choro = Fonte("Choro (pânico)", false);
        MontarTela();
        SceneManager.sceneLoaded += (s, m) => AoCarregar();
        AoCarregar();
    }

    AudioSource Fonte(string nome, bool emLoop)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = emLoop;
        s.spatialBlend = 0f;
        return s;
    }

    void AoCarregar()
    {
        RestaurarVelocidade();
        RestaurarQuedaDoMedo();
        Intensidade = 0f; noEscuro = 0f; EmPanico = false;

        var cena = SceneManager.GetActiveScene().name;
        var ajuste = FindAnyObjectByType<EscuroNaCena>();
        cfg = ajuste != null && ajuste.configuracao != null ? ajuste.configuracao : global;
        Ativo = ajuste != null ? !ajuste.desligado : System.Array.IndexOf(global.cenas, cena) >= 0;

        var grupo = AudioManager.Instance != null ? AudioManager.Instance.Grupo(AudioManager.GrupoEfeitos) : null;
        loop.outputAudioMixerGroup = soltos.outputAudioMixerGroup = choro.outputAudioMixerGroup = grupo;
        loop.clip = cfg.sussurrosLoop;
        loop.volume = 0f;
        if (Ativo && loop.clip != null) loop.Play(); else loop.Stop();
        CriarMaos();
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        var st = PlayerState.Instance;
        var medo = FearSystem.Instance;
        if (!Ativo || st == null || medo == null || st.isDead)
        {
            Intensidade = Mathf.MoveTowards(Intensidade, 0f, Time.deltaTime * 2f);
            SairDoPanico();
            Aplicar(medo);
            return;
        }
        if (medo != medoDaCena) { RestaurarQuedaDoMedo(); medoDaCena = medo; }
        if (cfg.quedaDoMedoNaLuz > 0f)
        {
            if (lightFallOriginal < 0f) lightFallOriginal = medo.lightFall;
            medo.lightFall = cfg.quedaDoMedoNaLuz;                       // alívio lento na luz
        }

        bool naLuz = st.IsInLight;
        bool segurado = st.isHidden || st.isReading || st.isHuggingBear;   // o escuro não avança

        if (naLuz)
        {
            noEscuro = 0f;
            Intensidade = Mathf.MoveTowards(Intensidade, 0f, cfg.alivioPorSegundo * Time.deltaTime);
        }
        else if (!segurado)
        {
            noEscuro += Time.deltaTime;
            if (noEscuro > cfg.atraso)
                Intensidade = Mathf.MoveTowards(Intensidade, 1f, Time.deltaTime / Mathf.Max(0.5f, cfg.tempoAteOMaximo));
            medo.AddFear(cfg.medoExtraPorSegundo * Intensidade * Time.deltaTime);
        }

        // Pânico: medo no máximo, no escuro, sem estar escondida/abraçando o urso.
        if (!EmPanico && !naLuz && !segurado && medo.fear >= cfg.medoDoPanico) EntrarNoPanico();
        else if (EmPanico && (naLuz || segurado || medo.fear < cfg.medoParaSairDoPanico)) SairDoPanico();
        if (EmPanico) Panico(st);

        Sussurros(naLuz);
        Aplicar(medo);
    }

    void Aplicar(FearSystem medo)
    {
        if (medo != null) medo.DefinirVinhetaExtra(Ativo ? Intensidade * cfg.vinhetaExtra : 0f);
        if (loop.clip != null) loop.volume = Intensidade * cfg.volumeDosSussurros;
        AnimarMaos();
    }

    void Sussurros(bool naLuz)
    {
        if (cfg.sussurrosSoltos == null || cfg.sussurrosSoltos.Length == 0 || Intensidade < 0.15f) return;
        if (Time.time < proxSussurro) return;
        proxSussurro = Time.time + Random.Range(cfg.intervaloDosSoltos.x, Mathf.Max(cfg.intervaloDosSoltos.x, cfg.intervaloDosSoltos.y)) / Mathf.Lerp(1f, 2f, Intensidade);
        var c = cfg.sussurrosSoltos[Random.Range(0, cfg.sussurrosSoltos.Length)];
        if (c == null) return;
        soltos.panStereo = Random.Range(-0.9f, 0.9f);
        soltos.PlayOneShot(c, cfg.volumeDosSussurros * Intensidade);
    }

    // ------------------------------------------------------------------ pânico

    void EntrarNoPanico()
    {
        EmPanico = true;
        proxBarulho = Time.time;             // o primeiro barulho é na hora
    }

    void Panico(PlayerState st)
    {
        // Anda devagar (e, com velocidade menor que 1, não corre). Segurando um móvel, o Pushable manda.
        var mv = st.Movement;
        if (mv != null && !st.isGrabbing && mv.speedMultiplier > cfg.velocidadeNoPanico)
        {
            mv.speedMultiplier = cfg.velocidadeNoPanico;
            mudouVelocidade = true;
        }

        if (Time.time < proxBarulho) return;
        proxBarulho = Time.time + cfg.intervaloDoBarulho;
        Vector3 p = st.transform.position;
        foreach (var c in CreatureAI.All) if (c != null) c.HearNoiseAt(p, cfg.raioDoBarulho);
        if (cfg.choro != null && cfg.choro.Length > 0)
        {
            var clip = cfg.choro[Random.Range(0, cfg.choro.Length)];
            if (clip != null) choro.PlayOneShot(clip, 0.9f);
        }

        // A Inspetora da ronda ouve do outro cômodo e vem (com o aviso de sempre).
        if (cfg.chamaARonda && RondaDoAndar.Instance != null)
            RondaDoAndar.Instance.ChamarPara(SceneManager.GetActiveScene().name, cfg.intervaloEntreChamados);
    }

    void SairDoPanico()
    {
        if (!EmPanico && !mudouVelocidade) return;
        EmPanico = false;
        RestaurarVelocidade();
    }

    void RestaurarVelocidade()
    {
        var st = PlayerState.Instance;
        if (mudouVelocidade && st != null && st.Movement != null && !st.isGrabbing && cfg != null &&
            Mathf.Approximately(st.Movement.speedMultiplier, cfg.velocidadeNoPanico))
            st.Movement.speedMultiplier = 1f;
        mudouVelocidade = false;
    }

    void RestaurarQuedaDoMedo()
    {
        if (medoDaCena != null && lightFallOriginal >= 0f) medoDaCena.lightFall = lightFallOriginal;
        lightFallOriginal = -1f;
        medoDaCena = null;
    }

    // ------------------------------------------------------------------ mãos de sombra (UI)

    void MontarTela()
    {
        var go = new GameObject("Maos de sombra", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;                        // abaixo do HUD (80): pausa e dicas ficam por cima
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        tela = (RectTransform)go.transform;
    }

    void CriarMaos()
    {
        foreach (var m in maos) if (m.rt != null) Destroy(m.rt.gameObject);
        maos.Clear();
        if (!Ativo || cfg.quantidadeDeMaos <= 0) return;
        // Espalhadas pelas bordas: laterais e embaixo (em cima o teto "pesa" menos).
        var bordas = new[]
        {
            new Vector2(0f, 0.25f), new Vector2(1f, 0.3f), new Vector2(0.2f, 0f), new Vector2(0.85f, 0f),
            new Vector2(0f, 0.7f), new Vector2(1f, 0.75f), new Vector2(0.5f, 0f), new Vector2(0.05f, 0f),
            new Vector2(0.95f, 0f), new Vector2(0.35f, 0f),
        };
        for (int i = 0; i < cfg.quantidadeDeMaos && i < bordas.Length; i++)
        {
            var go = new GameObject("Mao " + (i + 1), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(tela, false);
            var img = go.GetComponent<Image>();
            img.sprite = cfg.maos != null && cfg.maos.Length > 0 && cfg.maos[i % cfg.maos.Length] != null
                ? cfg.maos[i % cfg.maos.Length] : MaoProvisoria();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0.5f, 0.05f);         // pulso na borda, dedos para dentro
            Vector2 b = bordas[i];
            Vector2 dentro = b.x <= 0f ? Vector2.right : b.x >= 1f ? Vector2.left : Vector2.up;
            dentro = (dentro + new Vector2(0.5f - b.x, 0.5f - b.y) * 0.6f).normalized;
            maos.Add(new Mao { rt = rt, img = img, borda = b, paraDentro = dentro, semente = Random.value * 100f });
        }
    }

    void AnimarMaos()
    {
        if (maos.Count == 0 || tela == null) return;
        Vector2 tam = tela.rect.size;
        float lado = cfg.tamanhoDaMao * tam.y;
        for (int i = 0; i < maos.Count; i++)
        {
            var m = maos[i];
            // Cada mão entra no seu ritmo (uma de cada vez parece mais viva) e "tateia".
            float t = Time.time;
            float vez = Mathf.Clamp01(Intensidade * 1.6f - i * 0.12f);
            float rastejar = 0.75f + 0.25f * Mathf.PerlinNoise(m.semente, t * 0.35f);
            float entra = vez * rastejar * cfg.quantoEntram;
            Vector2 naBorda = new Vector2(m.borda.x * tam.x, m.borda.y * tam.y) - tam * 0.5f;
            m.rt.sizeDelta = new Vector2(lado * 0.75f, lado);
            m.rt.anchoredPosition = naBorda + m.paraDentro * (lado * (entra - 0.35f));
            float ang = Mathf.Atan2(m.paraDentro.y, m.paraDentro.x) * Mathf.Rad2Deg - 90f;
            ang += Mathf.Sin(t * (1.1f + i * 0.17f) + m.semente) * 7f;    // dedos tateando
            m.rt.localRotation = Quaternion.Euler(0f, 0f, ang);
            float pulso = 1f + Mathf.Sin(t * 2.3f + m.semente) * 0.04f;
            m.rt.localScale = new Vector3(pulso, 2f - pulso, 1f);
            m.img.color = new Color(0f, 0f, 0f, cfg.opacidadeDasMaos * Mathf.SmoothStep(0f, 1f, vez));
        }
    }

    /// <summary>Mão provisória gerada (palma + 5 dedos compridos e finos, traço contínuo e borda suave). Troque pelos desenhos na configuração.</summary>
    static Sprite MaoProvisoria()
    {
        if (maoProvisoria != null) return maoProvisoria;
        const int W = 192, H = 256, N = 12;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * H];
        // Dedos: (base x, ângulo, comprimento, grossura) — curvos e desiguais, como galhos.
        var dedos = new[] { (0.30f, -28f, 0.52f, 0.05f), (0.42f, -9f, 0.70f, 0.055f), (0.53f, 3f, 0.76f, 0.055f),
                            (0.63f, 14f, 0.66f, 0.05f), (0.72f, 38f, 0.40f, 0.045f) };
        // Pontos de cada dedo (com uma curva, como se estivessem se dobrando).
        var pts = new Vector3[dedos.Length, N + 1];              // x, y, raio
        for (int f = 0; f < dedos.Length; f++)
        {
            var (bx, ang, comp, gros) = dedos[f];
            float a = ang * Mathf.Deg2Rad, lado = ang >= 0f ? 1f : -1f;
            for (int k = 0; k <= N; k++)
            {
                float t = k / (float)N;
                float curva = Mathf.Sin(t * 2.4f) * 0.06f * lado;
                pts[f, k] = new Vector3(bx + Mathf.Sin(a) * comp * t + curva, 0.22f + Mathf.Cos(a) * comp * t, gros * (1f - t * 0.8f));
            }
        }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;
                float d = Elipse(u, v, 0.52f, 0.14f, 0.2f, 0.16f);              // palma/pulso
                for (int f = 0; f < dedos.Length; f++)
                    for (int k = 0; k < N; k++)
                    {
                        Vector3 p0 = pts[f, k], p1 = pts[f, k + 1];
                        // distância até o segmento p0-p1, com o raio variando ao longo dele
                        float sx = p1.x - p0.x, sy = p1.y - p0.y;
                        float h = Mathf.Clamp01(((u - p0.x) * sx + (v - p0.y) * sy) / Mathf.Max(1e-6f, sx * sx + sy * sy));
                        float dx = u - (p0.x + sx * h), dy = v - (p0.y + sy * h);
                        float r = Mathf.Lerp(p0.z, p1.z, h);
                        d = Mathf.Min(d, Mathf.Sqrt(dx * dx * 1.6f + dy * dy) / Mathf.Max(0.004f, r));
                    }
                float alfa = Mathf.Clamp01((1.15f - d) / 0.35f);
                px[y * W + x] = new Color32(0, 0, 0, (byte)(alfa * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        maoProvisoria = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.05f), 100f);
        maoProvisoria.name = "Mao de sombra (provisoria)";
        return maoProvisoria;
    }

    static float Elipse(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // ------------------------------------------------------------------ testes

    /// <summary>Testes: leva o escuro ao máximo agora (e o medo, se pedir pânico).</summary>
    public void TestarMaximo(bool panico)
    {
        noEscuro = cfg.atraso;
        Intensidade = 1f;
        if (panico && FearSystem.Instance != null) FearSystem.Instance.fear = 1f;
    }
}
