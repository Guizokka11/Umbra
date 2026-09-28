using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Interface que existe em todas as cenas, criada sozinha ao dar Play:
///  - Hud.Toast("Pegou: fusível")      mensagem curta embaixo
///  - Hud.Subtitle("...Luma...")       fala/voz em itálico
///  - Esc: menu de pausa (Continuar / Opções / Controles / Voltar ao checkpoint / Menu / Sair)
///  - Dicas de controle na primeira vez que cada ação aparece (DicasDeControle)
/// Não precisa colocar nada na cena.
/// </summary>
public class Hud : MonoBehaviour
{
    public static Hud Instance { get; private set; }

    TMP_Text toast, subtitle;
    CanvasGroup toastGroup, subtitleGroup;
    GameObject pausePanel, paginaPrincipal, paginaOpcoes, paginaControles;
    TMP_Text textoControles, textoDicas;
    Coroutine toastCo, subCo;
    bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("Umbra_HUD");
        DontDestroyOnLoad(go);
        go.AddComponent<Hud>();
    }

    public static void Toast(string msg, float seconds = 2.8f)
    {
        if (Instance == null) Boot();
        if (Instance != null) Instance.ShowToast(msg, seconds);
    }

    public static void Subtitle(string msg, float seconds = 3f)
    {
        if (Instance == null) Boot();
        if (Instance != null) Instance.ShowSubtitle(msg, seconds);
    }

    public static bool IsPaused => Instance != null && Instance.paused;

    // ------------------------------------------------------------------ build

    void Awake()
    {
        Instance = this;
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        toast = Text(transform, "Toast", 32, new Vector2(0.5f, 0f), new Vector2(0, 170), new Vector2(1200, 60));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;

        subtitle = Text(transform, "Legenda", 38, new Vector2(0.5f, 0f), new Vector2(0, 260), new Vector2(1500, 80));
        subtitle.fontStyle = FontStyles.Italic;
        subtitle.color = new Color(0.85f, 0.8f, 0.9f);
        subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;

        BuildPause();
        gameObject.AddComponent<DicasDeControle>().Montar(transform);
        pausePanel.transform.SetAsLastSibling();   // a pausa cobre as dicas
        SceneManager.sceneLoaded += (s, m) => { SetPaused(false); EnsureEventSystem(); };
        EnsureEventSystem();
    }

    void BuildPause()
    {
        pausePanel = new GameObject("Pausa", typeof(RectTransform), typeof(Image));
        pausePanel.transform.SetParent(transform, false);
        Esticar((RectTransform)pausePanel.transform);
        pausePanel.GetComponent<Image>().color = new Color(0.02f, 0.015f, 0.03f, 0.82f);

        // Página principal
        paginaPrincipal = Pagina("Principal");
        Text(paginaPrincipal.transform, "Titulo", 64, new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(800, 100)).text = "Pausa";
        Button(paginaPrincipal.transform, "Continuar", new Vector2(0, 170), () => SetPaused(false));
        Button(paginaPrincipal.transform, "Opções", new Vector2(0, 90), () => MostrarPagina(paginaOpcoes));
        Button(paginaPrincipal.transform, "Controles", new Vector2(0, 10), () => MostrarPagina(paginaControles));
        Button(paginaPrincipal.transform, "Voltar ao último checkpoint", new Vector2(0, -70), () =>
        {
            SetPaused(false);
            if (GameManager.Instance != null) GameManager.Instance.RespawnNow();
        });
        Button(paginaPrincipal.transform, "Menu principal", new Vector2(0, -150), () => { SetPaused(false); SaveGame.ToMenu(); });
        Button(paginaPrincipal.transform, "Sair do jogo", new Vector2(0, -230), () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        // Opções: volumes (geral + grupos do AudioMixer), brilho, dicas
        paginaOpcoes = Pagina("Opcoes");
        Text(paginaOpcoes.transform, "Titulo", 64, new Vector2(0.5f, 0.5f), new Vector2(0, 390), new Vector2(800, 100)).text = "Opções";
        Slider(paginaOpcoes.transform, "Volume geral", new Vector2(0, 290), 0f, 10f, Mathf.Round(Opcoes.VolumeGeral * 10f),
               v => Opcoes.VolumeGeral = v / 10f, v => Mathf.RoundToInt(v * 10f) + "%");
        float y = 220f;
        foreach (var grupo in AudioManager.Grupos)
        {
            string g = grupo;
            Slider(paginaOpcoes.transform, g, new Vector2(0, y), 0f, 10f, Mathf.Round(Opcoes.VolumeDoGrupo(g) * 10f),
                   v => Opcoes.DefinirVolumeDoGrupo(g, v / 10f), v => Mathf.RoundToInt(v * 10f) + "%");
            y -= 70f;
        }
        Slider(paginaOpcoes.transform, "Brilho", new Vector2(0, y - 20f), -10f, 10f, Mathf.Round(Opcoes.Brilho * 10f),
               v => Opcoes.Brilho = v / 10f, v => v == 0f ? "padrão" : (v > 0f ? "+" : "") + Mathf.RoundToInt(v));
        textoDicas = Button(paginaOpcoes.transform, "Dicas de controle", new Vector2(0, y - 115f), () =>
        {
            Opcoes.DicasDeControle = !Opcoes.DicasDeControle;
            AtualizarTextoDicas();
        });
        AtualizarTextoDicas();
        var nota = Text(paginaOpcoes.transform, "Nota", 24, new Vector2(0.5f, 0.5f), new Vector2(0, y - 180f), new Vector2(1000, 40));
        nota.color = new Color(1f, 1f, 1f, 0.45f);
        nota.text = "Brilho ideal: a Luma sempre visível, mas o escuro ainda escuro.";
        Button(paginaOpcoes.transform, "Voltar", new Vector2(0, y - 265f), () => MostrarPagina(paginaPrincipal));

        // Controles
        paginaControles = Pagina("Controles");
        Text(paginaControles.transform, "Titulo", 64, new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(800, 100)).text = "Controles";
        textoControles = Text(paginaControles.transform, "Lista", 32, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1300, 440));
        textoControles.lineSpacing = 12f;
        Button(paginaControles.transform, "Voltar", new Vector2(0, -260), () => MostrarPagina(paginaPrincipal));

        MostrarPagina(paginaPrincipal);
        pausePanel.SetActive(false);
    }

    GameObject Pagina(string nome)
    {
        var go = new GameObject("Pagina_" + nome, typeof(RectTransform));
        go.transform.SetParent(pausePanel.transform, false);
        Esticar((RectTransform)go.transform);
        return go;
    }

    void MostrarPagina(GameObject pagina)
    {
        foreach (var p in new[] { paginaPrincipal, paginaOpcoes, paginaControles })
            if (p != null) p.SetActive(p == pagina);
        if (pagina == paginaControles) textoControles.text = ListaDeControles();
        var first = pagina != null ? pagina.GetComponentInChildren<Selectable>() : null;
        if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
    }

    void AtualizarTextoDicas() => textoDicas.text = "Dicas de controle: " + (Opcoes.DicasDeControle ? "ligadas" : "desligadas");

    /// <summary>Teclas atuais (lê E e F dos componentes da Luma, caso tenham sido trocadas).</summary>
    static string ListaDeControles()
    {
        string e = "E", f = "F";
        var st = PlayerState.Instance;
        var pi = st != null ? st.GetComponent<PlayerInteractor>() : null;
        if (pi != null) e = pi.interactKey.ToString();
        if (FearSystem.Instance != null) f = FearSystem.Instance.hugKey.ToString();
        string K(string k) => "<b><color=#F2E6CC>" + k + "</color></b>";
        return K("A / D") + " ou setas   andar\n" +
               K("W / S") + "   ir para o fundo / para a frente  ·  subir e descer escadas\n" +
               K("Shift") + "   correr (faz barulho)\n" +
               K("Espaço") + "   pular  ·  encostada em algo baixo: subir\n" +
               K(e) + "   interagir: portas, pegar, ler, esconder, segurar e soltar móveis\n" +
               "segurando um móvel: " + K("A / D / W / S") + "   empurrar e puxar\n" +
               K(f) + " (segurar)   abraçar o urso: acalma, mas ela fica parada\n" +
               K("Esc") + "   pausa";
    }

    static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Atalhos de teste: F9 = próxima cena do build, F8 = anterior, F10 = pausa (igual ao Esc).
        if (Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.F8))
        {
            int i = SceneManager.GetActiveScene().buildIndex + (Input.GetKeyDown(KeyCode.F9) ? 1 : -1);
            if (i >= 0 && i < SceneManager.sceneCountInBuildSettings) { SetPaused(false); SceneManager.LoadScene(i); }
        }
        // F12: foto da tela do jogo (Docs/_previews/captura.png).
        if (Input.GetKeyDown(KeyCode.F12))
        {
            System.IO.Directory.CreateDirectory("Docs/_previews");
            ScreenCapture.CaptureScreenshot("Docs/_previews/captura.png");
            var ps = PlayerState.Instance;
            if (ps != null)
            {
                var p = ps.transform.position;
                string under = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.collider.name + " @" + hit.point.ToString("F2") : "nada";
                string perto = "";
                foreach (var col in Physics.OverlapSphere(p + Vector3.up * 0.5f, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                    perto += "\n  " + col.name + " " + col.bounds.center.ToString("F2") + " " + col.bounds.size.ToString("F2");
                var pi = ps.GetComponent<PlayerInteractor>();
                string alvo = pi != null && pi.Current != null ? pi.Current.name : "-";
                System.IO.File.WriteAllText("Docs/_previews/captura.txt", "Luma " + p.ToString("F2") + "  embaixo: " + under
                    + "  segurando: " + ps.isGrabbing + "  alvo: " + alvo + perto);
            }
        }
#endif
        bool esc = Input.GetKeyDown(KeyCode.Escape);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        esc |= Input.GetKeyDown(KeyCode.F10);   // no editor o Esc às vezes só solta o mouse do Game view
#endif
        if (!esc) return;
        if (SceneManager.GetActiveScene().name == SaveGame.MenuScene) return;
        var st = PlayerState.Instance;
        if (!paused && st != null && st.isReading) return; // Esc fecha a pista, não pausa
        if (paused && paginaPrincipal != null && !paginaPrincipal.activeSelf) { MostrarPagina(paginaPrincipal); return; }
        SetPaused(!paused);
    }

    public void SetPaused(bool p)
    {
        paused = p;
        if (pausePanel != null) pausePanel.SetActive(p);
        Time.timeScale = p ? 0f : 1f;
        AudioListener.pause = p;
        if (p && pausePanel != null) MostrarPagina(paginaPrincipal);
    }

    void ShowToast(string msg, float seconds)
    {
        if (toastCo != null) StopCoroutine(toastCo);
        toast.text = msg;
        toastCo = StartCoroutine(Flash(toastGroup, seconds));
    }

    void ShowSubtitle(string msg, float seconds)
    {
        if (subCo != null) StopCoroutine(subCo);
        subtitle.text = msg;
        subCo = StartCoroutine(Flash(subtitleGroup, seconds));
    }

    static IEnumerator Flash(CanvasGroup g, float seconds)
    {
        for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime) { g.alpha = t / 0.25f; yield return null; }
        g.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime) { g.alpha = 1f - t / 0.6f; yield return null; }
        g.alpha = 0f;
    }

    // ------------------------------------------------------------------ util

    static TMP_Text Text(Transform parent, string name, float size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.93f, 0.9f, 0.86f);
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    static TMP_Text Button(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Botao_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(560, 64);
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.06f);
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.06f);
        cb.highlightedColor = new Color(1f, 0.85f, 0.6f, 0.25f);
        cb.selectedColor = new Color(1f, 0.85f, 0.6f, 0.25f);
        cb.pressedColor = new Color(1f, 0.85f, 0.6f, 0.4f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        var t = Text(go.transform, "Texto", 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 64));
        t.text = label;
        return t;
    }

    /// <summary>Linha "Nome  [=====o----]  valor" com um Slider do UGUI (setas esquerda/direita também mudam).</summary>
    static Slider Slider(Transform parent, string label, Vector2 pos, float min, float max, float value,
                         System.Action<float> onChange, System.Func<float, string> format)
    {
        var row = new GameObject("Linha_" + label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = (RectTransform)row.transform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = pos;
        rrt.sizeDelta = new Vector2(900, 64);

        var name = Text(row.transform, "Nome", 32, new Vector2(0f, 0.5f), new Vector2(110, 0), new Vector2(220, 64));
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.text = label;
        var val = Text(row.transform, "Valor", 28, new Vector2(1f, 0.5f), new Vector2(-70, 0), new Vector2(140, 64));
        val.alignment = TextAlignmentOptions.MidlineRight;

        var sgo = new GameObject("Slider", typeof(RectTransform));
        sgo.transform.SetParent(row.transform, false);
        var srt = (RectTransform)sgo.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(20, 0);
        srt.sizeDelta = new Vector2(460, 28);

        var bg = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(sgo.transform, false);
        var bgrt = (RectTransform)bg.transform;
        bgrt.anchorMin = new Vector2(0f, 0.35f); bgrt.anchorMax = new Vector2(1f, 0.65f); bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);

        var fillArea = new GameObject("Area", typeof(RectTransform));
        fillArea.transform.SetParent(sgo.transform, false);
        var fart = (RectTransform)fillArea.transform;
        fart.anchorMin = new Vector2(0f, 0.35f); fart.anchorMax = new Vector2(1f, 0.65f); fart.offsetMin = fart.offsetMax = Vector2.zero;
        var fill = new GameObject("Preenchido", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        var frt = (RectTransform)fill.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f); frt.offsetMin = frt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(1f, 0.85f, 0.6f, 0.55f);

        var handleArea = new GameObject("AreaDaAlca", typeof(RectTransform));
        handleArea.transform.SetParent(sgo.transform, false);
        var hart = (RectTransform)handleArea.transform;
        hart.anchorMin = Vector2.zero; hart.anchorMax = Vector2.one; hart.offsetMin = new Vector2(12, 0); hart.offsetMax = new Vector2(-12, 0);
        var handle = new GameObject("Alca", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        var hrt = (RectTransform)handle.transform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(0f, 1f); hrt.sizeDelta = new Vector2(24, 0);
        var himg = handle.GetComponent<Image>();

        var s = sgo.AddComponent<Slider>();
        s.fillRect = frt;
        s.handleRect = hrt;
        s.targetGraphic = himg;
        s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        s.wholeNumbers = true;                           // passos inteiros: as setas andam 1 e o 0 do brilho é exato
        s.minValue = min; s.maxValue = max;
        var cb = s.colors;
        cb.normalColor = new Color(0.95f, 0.92f, 0.86f);
        cb.highlightedColor = new Color(1f, 0.85f, 0.6f);
        cb.selectedColor = new Color(1f, 0.85f, 0.6f);
        s.colors = cb;
        s.value = value;
        val.text = format(value);
        s.onValueChanged.AddListener(v => { onChange(v); val.text = format(v); });
        return s;
    }
}
