using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mostra a tecla de cada ação na PRIMEIRA vez que ela aparece (andar, correr, pular, subir, segurar,
/// esconder, abraçar o urso, escada...). Cada dica aparece uma vez por jogo (salvo em GameFlags como
/// "ctrl_...", então um Novo Jogo mostra de novo). Canto inferior esquerdo, uma de cada vez.
/// Criado pelo Hud; desligável em Pausa > Opções.
/// </summary>
public class DicasDeControle : MonoBehaviour
{
    const float Duracao = 5f, MinParaSumir = 1.2f;

    class Dica
    {
        public string id;            // flag "ctrl_" + id
        public string[] teclas;      // texto de cada tecla desenhada
        public KeyCode[] somem;      // apertar qualquer uma delas (depois de MinParaSumir) esconde a dica
        public string texto;
    }

    readonly Queue<Dica> fila = new Queue<Dica>();
    readonly HashSet<string> naFila = new HashSet<string>();
    Dica atual;
    float mostrando, andando, jogando, proxChecagem;

    CanvasGroup grupo;
    RectTransform linha;
    TMP_Text rotulo;
    readonly List<GameObject> caixas = new List<GameObject>();

    // ------------------------------------------------------------------ montagem

    public void Montar(Transform canvas)
    {
        var go = new GameObject("Dica de controle", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        linha = (RectTransform)go.transform;
        linha.anchorMin = linha.anchorMax = linha.pivot = new Vector2(0f, 0f);
        linha.anchoredPosition = new Vector2(70f, 70f);
        linha.sizeDelta = new Vector2(1100f, 64f);
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        grupo = go.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;

        var lg = new GameObject("Texto", typeof(RectTransform));
        lg.transform.SetParent(linha, false);
        rotulo = lg.AddComponent<TextMeshProUGUI>();
        rotulo.fontSize = 30;
        rotulo.color = new Color(0.93f, 0.9f, 0.86f);
        rotulo.alignment = TextAlignmentOptions.MidlineLeft;
        rotulo.textWrappingMode = TextWrappingModes.NoWrap;
        rotulo.raycastTarget = false;
        ((RectTransform)lg.transform).sizeDelta = new Vector2(700f, 56f);
        SceneManager.sceneLoaded += (s, m) => { fila.Clear(); naFila.Clear(); Esconder(); };
    }

    GameObject Caixa(string tecla)
    {
        var go = new GameObject("Tecla " + tecla, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(linha, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.93f, 0.9f, 0.86f, 0.14f);
        img.raycastTarget = false;
        go.AddComponent<Outline>().effectColor = new Color(0.93f, 0.9f, 0.86f, 0.7f);
        var tg = new GameObject("Letra", typeof(RectTransform));
        tg.transform.SetParent(go.transform, false);
        var t = tg.AddComponent<TextMeshProUGUI>();
        t.text = tecla;
        t.fontSize = 28;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.97f, 0.94f, 0.88f);
        t.raycastTarget = false;
        var trt = (RectTransform)tg.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        float w = Mathf.Max(56f, 22f + tecla.Length * 17f);
        ((RectTransform)go.transform).sizeDelta = new Vector2(w, 56f);
        return go;
    }

    // ------------------------------------------------------------------ pedidos

    /// <summary>Pede uma dica (ignorada se já foi vista, já está na fila ou as dicas estão desligadas).</summary>
    void Pedir(string id, string texto, string[] teclas, params KeyCode[] somem)
    {
        if (!Opcoes.DicasDeControle || GameFlags.Has("ctrl_" + id) || naFila.Contains(id) || (atual != null && atual.id == id)) return;
        naFila.Add(id);
        fila.Enqueue(new Dica { id = id, texto = texto, teclas = teclas, somem = somem });
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        var st = PlayerState.Instance;
        bool podeMostrar = st != null && !Hud.IsPaused && !st.isReading && !st.isDead && Opcoes.DicasDeControle;
        if (!podeMostrar)
        {
            if (atual != null && (!Opcoes.DicasDeControle || st == null)) Esconder();
            else if (atual != null && !Hud.IsPaused) grupo.alpha = 0f;   // lendo pista / pega: some até voltar
            return;
        }

        Observar(st);

        if (atual == null && fila.Count > 0)
        {
            atual = fila.Dequeue();
            naFila.Remove(atual.id);
            if (GameFlags.Has("ctrl_" + atual.id)) { atual = null; return; }
            Mostrar(atual);
        }
        if (atual == null) return;

        mostrando += Time.deltaTime;
        grupo.alpha = Mathf.Min(1f, mostrando / 0.3f) * Mathf.Clamp01((Duracao - mostrando) / 0.6f);
        bool fez = false;
        if (mostrando > MinParaSumir)
            foreach (var k in atual.somem) if (Input.GetKey(k)) fez = true;
        if (fez && mostrando < Duracao - 0.6f) mostrando = Duracao - 0.6f;   // fez a ação: some logo
        if (mostrando >= Duracao) Esconder();
    }

    void Mostrar(Dica d)
    {
        GameFlags.Set("ctrl_" + d.id);
        foreach (var c in caixas) Destroy(c);
        caixas.Clear();
        foreach (var t in d.teclas) caixas.Add(Caixa(t));
        rotulo.transform.SetAsLastSibling();
        rotulo.text = d.texto;
        mostrando = 0f;
        grupo.alpha = 0f;
    }

    void Esconder()
    {
        atual = null;
        mostrando = 0f;
        if (grupo != null) grupo.alpha = 0f;
    }

    /// <summary>Vê o que a Luma pode fazer agora e pede as dicas das ações que acabaram de aparecer.</summary>
    void Observar(PlayerState st)
    {
        var mv = st.Movement;
        jogando += Time.deltaTime;
        if (mv != null && mv.isMoving) andando += Time.deltaTime;

        // Sempre disponíveis: vão aparecendo no começo, uma depois da outra.
        if (mv != null && mv.canMove && st.IsFree)
        {
            Pedir("andar", "andar", new[] { "A", "D" }, KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow);
            if (andando > 1.5f)
                Pedir("fundo", "ir para o fundo / para a frente", new[] { "W", "S" }, KeyCode.W, KeyCode.S, KeyCode.UpArrow, KeyCode.DownArrow);
            if (andando > 5f)
                Pedir("correr", "correr (faz barulho)", new[] { "Shift" }, KeyCode.LeftShift, KeyCode.RightShift);
            if (andando > 9f)
                Pedir("pular", "pular", new[] { "Espaço" }, KeyCode.Space);
            if (jogando > 25f)
                Pedir("pausa", "pausa e opções", new[] { "Esc" }, KeyCode.Escape);
        }

        // Situações: checadas algumas vezes por segundo.
        if (Time.time < proxChecagem) return;
        proxChecagem = Time.time + 0.2f;

        var pi = st.GetComponent<PlayerInteractor>();
        string e = pi != null ? Nome(pi.interactKey) : "E";
        KeyCode ek = pi != null ? pi.interactKey : KeyCode.E;

        if (st.isGrabbing)
            Pedir("empurrar", "empurrar e puxar (toques curtos = sem barulho)  ·  " + e + " de novo: soltar", new[] { "A", "D", "W", "S" }, ek);
        else if (st.isHidden)
            Pedir("sair_esconderijo", "sair do esconderijo (espere ela ir embora)", new[] { e }, ek);
        else if (pi != null && pi.Current != null && st.IsFree)
        {
            // Cada tipo de interação é uma ação diferente (porta, pegar, segurar, esconder, ler...).
            var it = pi.Current;
            Pedir("interagir_" + it.GetType().Name, it.prompt.ToLowerInvariant(), new[] { e }, ek);
        }

        var climb = st.GetComponent<ClimbAssist>();
        if (climb != null && st.IsFree && !st.isGrabbing && climb.TemOndeSubir())
            Pedir("subir", "subir (encostada)", new[] { "Espaço" }, KeyCode.Space);

        if (st.isOnStairs && st.IsFree)
            Pedir("escada", "subir e descer a escada", new[] { "W", "S" }, KeyCode.W, KeyCode.S);

        var fear = FearSystem.Instance;
        if (fear != null && fear.fear > 0.35f && st.IsFree && !st.isGrabbing)
            Pedir("urso", "segure: abraçar o urso (acalma, mas ela fica parada)", new[] { Nome(fear.hugKey) }, fear.hugKey);
    }

    static string Nome(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Space: return "Espaço";
            case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
            case KeyCode.Escape: return "Esc";
            case KeyCode.Return: return "Enter";
            default: return k.ToString();
        }
    }
}
