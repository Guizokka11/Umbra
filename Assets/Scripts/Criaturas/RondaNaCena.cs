using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ronda da Inspetora NESTA cena (um por cena do 2º andar). Lê o estado global do RondaDoAndar e:
///  - ela vem para cá: aviso de 5–8 s (passos se aproximando, porta rangendo, luz tremendo), a sombra dela
///    passa pela parede do fundo e ela entra pela porta do cômodo de onde veio; depois faz a ronda (CreatureAI);
///  - ela vai embora: anda até a porta do próximo cômodo e some;
///  - ela está num cômodo vizinho: só se ouve pela porta daquele vizinho (passos, galhos), mais alto perto da porta;
///  - passa perto de um esconderijo: pode parar na frente e farejar (não acha a Luma escondida).
/// Pega a Luma: captura com consequência (SequenciaDeCaptura) e, na volta ao checkpoint, ela vai para outro cômodo.
/// Preparado pelo menu "Umbra > Terror > Preparar ronda na cena aberta".
/// </summary>
public class RondaNaCena : MonoBehaviour
{
    public static RondaNaCena Atual { get; private set; }

    [Tooltip("Desliga a ronda só nesta cena (a Inspetora da cena, se houver, fica como estava).")]
    public bool desligadaNestaCena;
    [Tooltip("A Inspetora desta cena (CreatureAI). Fica desligada enquanto ela não está aqui.")]
    public CreatureAI inspetora;
    [Tooltip("Pontos da ronda dela aqui dentro.")]
    public Transform[] pontosDeRonda;
    [Tooltip("Portas por onde ela entra/sai (vazio = as PortaDaRonda filhas deste objeto).")]
    public PortaDaRonda[] portas;
    [Tooltip("A Luma chegou num cômodo onde ela está: segundos até o aviso começar.")]
    public float esperaAoChegar = 3f;

    enum Fase { Fora, Aviso, Dentro, Saindo }
    Fase fase = Fase.Fora;

    RondaDoAndar ronda;
    RondaDoAndarConfig cfg;
    string cena, vindaDe;
    bool iniciado, chegouAgora = true, controlava;
    Coroutine co;
    float avisoInicio, avisoFim, proxPasso, proxRangidoDePorta;
    PortaDaRonda portaDoAviso;
    AudioSource somGalhos, somPassos, somPorta, somFaro;
    SpriteRenderer sombra;
    readonly List<HidingSpot> esconderijos = new List<HidingSpot>();
    readonly Dictionary<HidingSpot, float> farejadoEm = new Dictionary<HidingSpot, float>();
    readonly Dictionary<LightZone, bool> luzesAntes = new Dictionary<LightZone, bool>();

    // ------------------------------------------------------------------ início

    void Awake()
    {
        Atual = this;
        cena = gameObject.scene.name;
        if (portas == null || portas.Length == 0) portas = GetComponentsInChildren<PortaDaRonda>(true);
        somGalhos = Fonte("Som: galhos (vizinho)", true);
        somPassos = Fonte("Som: passos (vizinho)", false);
        somPorta = Fonte("Som: porta", false);
    }

    void OnDestroy() { if (Atual == this) Atual = null; }

    AudioSource Fonte(string nome, bool loop)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;           // 2D: o volume vem da distância da Luma até a porta (calculado aqui)
        return s;
    }

    IEnumerator Start()
    {
        ronda = RondaDoAndar.Instance;
        if (ronda == null) yield break;
        cfg = ronda.Config;
        ronda.OnTroca += AoTrocar;
        if (GameManager.Instance != null) GameManager.Instance.onRespawn.AddListener(AoRespawn);

        var grupo = AudioManager.Instance != null ? AudioManager.Instance.Grupo(AudioManager.GrupoCriaturas) : null;
        somGalhos.outputAudioMixerGroup = somPassos.outputAudioMixerGroup = somPorta.outputAudioMixerGroup = grupo;
        somGalhos.clip = cfg.galhosRaspando;
        esconderijos.AddRange(FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (inspetora != null)
        {
            var f = new GameObject("Som: farejar");
            f.transform.SetParent(inspetora.transform, false);
            somFaro = f.AddComponent<AudioSource>();
            somFaro.playOnAwake = false;
            somFaro.spatialBlend = 1f;
            somFaro.maxDistance = 12f;
            somFaro.rolloffMode = AudioRolloffMode.Linear;
            somFaro.outputAudioMixerGroup = grupo;
        }

        yield return null;              // depois do Start dos outros (o ScriptedEncounter desliga a criatura dele no Start)
        iniciado = true;
    }

    void OnDisable()
    {
        if (ronda != null) ronda.OnTroca -= AoTrocar;
        RestaurarLuzes();
    }

    void AoTrocar(string de, string para)
    {
        if (para == cena) vindaDe = de;
    }

    void AoRespawn()
    {
        // Pegou a Luma: na volta ao checkpoint ela já foi para outro cômodo (não pega de novo na hora).
        if (!Controla || ronda.ComodoAtual != cena) return;
        Esconder();
        ronda.MoverParaVizinho();
        chegouAgora = true;
    }

    // ------------------------------------------------------------------ estado

    bool Controla => !desligadaNestaCena && ronda != null && ronda.Ativa && inspetora != null && ronda.FazParte(cena);

    /// <summary>Está perseguindo, farejando ou capturando: não pode sumir agora.</summary>
    bool Ocupada
    {
        get
        {
            if (inspetora == null || !inspetora.isActiveAndEnabled) return false;
            var seq = inspetora.GetComponent<SequenciaDeCaptura>();
            var st = PlayerState.Instance;
            return inspetora.State == CreatureState.Chase || inspetora.Farejando || (seq != null && seq.Rodando) || (st != null && st.isDead);
        }
    }

    /// <summary>O RondaDoAndar pergunta antes de trocar de cômodo.</summary>
    public bool PodeSair() => fase != Fase.Dentro || !Ocupada;

    // ------------------------------------------------------------------ loop

    void Update()
    {
        if (!iniciado || ronda == null) return;
        bool ctrl = Controla;
        if (!ctrl)
        {
            // Pausada (ex.: fuga) ou desligada: some, se não estiver no meio de algo.
            if (controlava && fase != Fase.Fora && !Ocupada) Esconder();
            controlava = false;
            AbaixarSomDoVizinho();
            return;
        }
        if (!controlava)
        {
            // Acabou de passar a controlar (início da cena ou fim da pausa): a Inspetora da cena some até a ronda trazê-la.
            if (fase == Fase.Fora && inspetora.gameObject.activeSelf && inspetora.State != CreatureState.Chase)
                inspetora.gameObject.SetActive(false);
            controlava = true;
        }

        string onde = ronda.ComodoAtual;
        if (onde == cena)
        {
            if (fase == Fase.Fora) Trocar(Entrar(PortaPara(vindaDe), chegouAgora ? esperaAoChegar : 0f));
        }
        else
        {
            if (fase == Fase.Aviso) Esconder();
            if (fase == Fase.Dentro && !Ocupada) Trocar(Sair(PortaPara(onde)));
        }
        chegouAgora = false;

        SomDoVizinhoOuAviso(onde);
        if (fase == Fase.Dentro) Farejar();
    }

    void Trocar(IEnumerator rotina)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(rotina);
    }

    PortaDaRonda PortaPara(string vizinho)
    {
        if (portas == null || portas.Length == 0) return null;
        foreach (var p in portas) if (p != null && p.comodoVizinho == vizinho) return p;
        return portas[Random.Range(0, portas.Length)];
    }

    // ------------------------------------------------------------------ entrar / sair

    IEnumerator Entrar(PortaDaRonda porta, float espera)
    {
        fase = Fase.Aviso;
        portaDoAviso = porta;
        if (espera > 0f) { avisoInicio = avisoFim = float.MaxValue; yield return new WaitForSeconds(espera); }

        float aviso = Random.Range(cfg.tempoDeAviso.x, Mathf.Max(cfg.tempoDeAviso.x, cfg.tempoDeAviso.y));
        ronda.FicarPeloMenos(aviso + cfg.intervaloDeTroca.x);
        avisoInicio = Time.time;
        avisoFim = Time.time + aviso;
        var tremer = StartCoroutine(TremerLuzes(porta, aviso));
        bool sombraFoi = false, portaFoi = false;
        while (Time.time < avisoFim)
        {
            float falta = avisoFim - Time.time;
            if (!sombraFoi && falta <= cfg.sombraAntes) { sombraFoi = true; StartCoroutine(Sombra(porta)); }
            if (!portaFoi && falta <= cfg.portaAntes) { portaFoi = true; TocarPorta(porta, 1f); }
            yield return null;
        }
        StopCoroutine(tremer);
        RestaurarLuzes();

        // Aparece na porta e começa a ronda.
        Vector3 pos = porta != null ? porta.transform.position
                    : pontosDeRonda != null && pontosDeRonda.Length > 0 && pontosDeRonda[0] != null ? pontosDeRonda[0].position
                    : inspetora.transform.position;
        inspetora.gameObject.SetActive(true);
        inspetora.Posicionar(pos);
        inspetora.DefinirRonda(PontosValidos());
        fase = Fase.Dentro;
        co = null;
    }

    IEnumerator Sair(PortaDaRonda porta)
    {
        fase = Fase.Saindo;
        if (porta != null)
        {
            inspetora.DefinirRonda(new[] { porta.transform });
            float limite = Time.time + 15f;
            while (Time.time < limite)
            {
                if (Ocupada) { fase = Fase.Dentro; inspetora.DefinirRonda(PontosValidos()); co = null; yield break; }   // viu a Luma no caminho
                Vector3 d = porta.transform.position - inspetora.transform.position; d.y = 0f;
                if (d.magnitude < 0.35f) break;
                yield return null;
            }
            TocarPorta(porta, 0.7f);
        }
        inspetora.gameObject.SetActive(false);
        fase = Fase.Fora;
        co = null;
    }

    void Esconder()
    {
        if (co != null) { StopCoroutine(co); co = null; }
        RestaurarLuzes();
        if (sombra != null) sombra.enabled = false;
        if (inspetora != null) inspetora.gameObject.SetActive(false);
        fase = Fase.Fora;
    }

    Transform[] PontosValidos()
    {
        var l = new List<Transform>();
        if (pontosDeRonda != null) foreach (var p in pontosDeRonda) if (p != null) l.Add(p);
        return l.ToArray();
    }

    // ------------------------------------------------------------------ aviso: luz, porta, sombra

    IEnumerator TremerLuzes(PortaDaRonda porta, float tempo)
    {
        var luzes = new List<LightZone>();
        if (porta != null && porta.luzesQueTremem != null && porta.luzesQueTremem.Length > 0) luzes.AddRange(porta.luzesQueTremem);
        else if (porta != null)
            foreach (var z in LightZone.All)
                if (z != null && Vector3.Distance(z.transform.position, porta.transform.position) < 7f) luzes.Add(z);
        foreach (var l in luzes) if (l != null && !luzesAntes.ContainsKey(l)) luzesAntes[l] = l.isOn;

        float fim = Time.time + tempo;
        while (Time.time < fim)
        {
            // Treme mais conforme ela chega perto.
            float k = 1f - (fim - Time.time) / tempo;
            foreach (var l in luzes)
                if (l != null && luzesAntes.TryGetValue(l, out bool era) && era && Random.value < Mathf.Lerp(0.03f, 0.25f, k)) l.Toggle();
            yield return new WaitForSeconds(Random.Range(0.05f, 0.18f));
        }
    }

    void RestaurarLuzes()
    {
        foreach (var kv in luzesAntes) if (kv.Key != null) kv.Key.SetOn(kv.Value);
        luzesAntes.Clear();
    }

    void TocarPorta(PortaDaRonda porta, float volume)
    {
        if (cfg.portaRangendo == null) return;
        somPorta.panStereo = porta != null ? Pan(porta.transform.position) : 0f;
        somPorta.PlayOneShot(cfg.portaRangendo, volume);
    }

    IEnumerator Sombra(PortaDaRonda porta)
    {
        var src = inspetora != null ? (inspetora.sprite != null ? inspetora.sprite : inspetora.GetComponentInChildren<SpriteRenderer>(true)) : null;
        if (porta == null || porta.sombraInicio == null || porta.sombraFim == null || src == null) yield break;
        if (sombra == null)
        {
            var go = new GameObject("Sombra da Inspetora (parede)");
            go.transform.SetParent(transform, false);
            sombra = go.AddComponent<SpriteRenderer>();
            sombra.sortingOrder = -20;                   // atrás de todos os sprites (é só uma sombra na parede)
        }
        sombra.sprite = src.sprite;
        sombra.enabled = true;
        Vector3 escala = src.transform.lossyScale;
        sombra.transform.localScale = new Vector3(escala.x * 1.3f, escala.y * 1.9f, 1f);   // esticada, como sombra de luz baixa
        float subir = src.sprite != null ? -src.sprite.bounds.min.y * escala.y * 1.9f : 0f;
        Vector3 a = porta.sombraInicio.position, b = porta.sombraFim.position;
        sombra.flipX = b.x < a.x;
        float dur = Mathf.Max(0.3f, cfg.duracaoDaSombra);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            sombra.transform.position = Vector3.Lerp(a, b, k) + Vector3.up * subir;
            sombra.color = new Color(0f, 0f, 0f, 0.55f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        sombra.enabled = false;
    }

    // ------------------------------------------------------------------ som pela parede

    void SomDoVizinhoOuAviso(string onde)
    {
        var st = PlayerState.Instance;
        if (st == null) { AbaixarSomDoVizinho(); return; }
        PortaDaRonda porta = null;
        float alvo = 0f, cadencia = 0.8f;

        if (fase == Fase.Aviso && Time.time >= avisoInicio && avisoFim > avisoInicio)
        {
            // Chegando: cada vez mais alto e mais rápido, mesmo com a Luma longe da porta.
            porta = portaDoAviso;
            float k = Mathf.Clamp01((Time.time - avisoInicio) / (avisoFim - avisoInicio));
            alvo = Mathf.Lerp(cfg.volumeNoVizinho, 1f, k) * Mathf.Max(0.5f, Proximidade(porta));
            cadencia = Mathf.Lerp(0.8f, 0.5f, k);
        }
        else if (fase == Fase.Fora && onde != null && onde != cena && ronda.SaoVizinhos(cena, onde))
        {
            // No vizinho: anda de um lado para o outro lá dentro (às vezes perto da porta, às vezes longe).
            porta = PortaPara(onde);
            if (porta != null && porta.comodoVizinho != onde) porta = null;   // sem porta para esse vizinho aqui: não se ouve
            float vagando = Mathf.PerlinNoise(Time.time * 0.12f, 3.7f);
            alvo = cfg.volumeNoVizinho * Proximidade(porta) * Mathf.Lerp(0.2f, 1f, vagando);
            cadencia = vagando > 0.5f ? 0.75f : 99f;                          // só se ouvem passos quando ela anda
        }

        if (porta == null) { AbaixarSomDoVizinho(); return; }
        float pan = Pan(porta.transform.position);
        if (somGalhos.clip != null)
        {
            if (!somGalhos.isPlaying) { somGalhos.volume = 0f; somGalhos.Play(); }
            somGalhos.volume = Mathf.MoveTowards(somGalhos.volume, alvo, Time.deltaTime * 0.5f);
            somGalhos.panStereo = pan;
        }
        if (cfg.passos != null && cfg.passos.Length > 0 && Time.time >= proxPasso && alvo > 0.02f)
        {
            proxPasso = Time.time + cadencia * Random.Range(0.85f, 1.15f);
            var c = cfg.passos[Random.Range(0, cfg.passos.Length)];
            if (c != null) { somPassos.panStereo = pan; somPassos.pitch = Random.Range(0.9f, 1.05f); somPassos.PlayOneShot(c, alvo); }
        }
    }

    void AbaixarSomDoVizinho()
    {
        if (!somGalhos.isPlaying) return;
        somGalhos.volume = Mathf.MoveTowards(somGalhos.volume, 0f, Time.deltaTime * 0.8f);
        if (somGalhos.volume <= 0.001f) somGalhos.Stop();
    }

    /// <summary>1 com a Luma na porta, 0 a "alcanceDoSomVizinho" metros.</summary>
    float Proximidade(PortaDaRonda porta)
    {
        var st = PlayerState.Instance;
        if (porta == null || st == null) return 0f;
        float d = Vector3.Distance(st.transform.position, porta.transform.position);
        return 1f - Mathf.Clamp01(d / Mathf.Max(1f, cfg.alcanceDoSomVizinho));
    }

    static float Pan(Vector3 p)
    {
        var st = PlayerState.Instance;
        return st == null ? 0f : Mathf.Clamp((p.x - st.transform.position.x) / 7f, -0.85f, 0.85f);
    }

    // ------------------------------------------------------------------ farejar

    void Farejar()
    {
        if (inspetora == null || !inspetora.isActiveAndEnabled || inspetora.State != CreatureState.Patrol) return;
        foreach (var h in esconderijos)
        {
            if (h == null || !h.isActiveAndEnabled) continue;
            if (farejadoEm.TryGetValue(h, out float quando) && Time.time - quando < 20f) continue;
            Vector3 d = h.transform.position - inspetora.transform.position; d.y = 0f;
            float raio = h.Occupied ? cfg.raioDoFaro : 1.5f;
            if (d.magnitude > raio) continue;
            farejadoEm[h] = Time.time;                                        // decide uma vez por passagem
            if (!h.Occupied && Random.value > cfg.chanceEsconderijoVazio) continue;

            Vector3 frente = h.transform.position + Vector3.back * 0.5f;      // na frente do esconderijo (lado da câmera)
            frente.y = inspetora.transform.position.y;
            inspetora.Farejar(frente, Random.Range(cfg.tempoFarejando.x, Mathf.Max(cfg.tempoFarejando.x, cfg.tempoFarejando.y)));
            if (somFaro != null && cfg.farejar != null) somFaro.PlayOneShot(cfg.farejar);
            if (h.Occupied && FearSystem.Instance != null) FearSystem.Instance.AddFear(cfg.medoAoFarejar);
            return;
        }
    }
}
