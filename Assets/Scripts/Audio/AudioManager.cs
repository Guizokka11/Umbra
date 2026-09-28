using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Som do jogo, um só para o jogo todo (persiste entre cenas). Nasce sozinho ao dar Play a partir de
/// Assets/Audio/Resources/AudioManager.prefab: é nesse prefab que a equipe de som coloca os sons.
///
///  - Ambiente: toca o AmbienteDoComodo da cena (loop grave + rangidos aleatórios), com transição entre cenas.
///  - Passos da Luma por superfície (madeira, azulejo), pela distância andada.
///  - Stingers: TocarStinger() para sustos curtos (ScareFlash já chama).
///  - Música de perseguição: entra com fade quando uma criatura começa a perseguir e sai quando todas
///    desistem (CreatureAI e InspetoraMass avisam: PerseguicaoComecou / PerseguicaoAcabou).
///  - Medo: batimento e respiração em loop; o FearSystem sobe o volume com o medo.
///  - Silencio(segundos): o ambiente quase some antes de um susto.
/// Grupos do AudioMixer: Ambiente, Efeitos, Música, Criaturas (volumes em Pausa > Opções).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const string GrupoAmbiente = "Ambiente", GrupoEfeitos = "Efeitos", GrupoMusica = "Música", GrupoCriaturas = "Criaturas";
    public static readonly string[] Grupos = { GrupoAmbiente, GrupoEfeitos, GrupoMusica, GrupoCriaturas };
    /// <summary>Nome do parâmetro exposto no mixer para o volume do grupo (em dB).</summary>
    public static string ParametroDoGrupo(string grupo) => "Vol_" + grupo.Replace("ú", "u");

    [Tooltip("Assets/Audio/UmbraMixer.mixer (criado pelo menu Umbra > Terror > Colocar sons na cena aberta).")]
    public AudioMixer mixer;

    [System.Serializable]
    public class PassosDaSuperficie
    {
        public Superficie superficie;
        [Tooltip("Passos andando: um é sorteado a cada passo.")]
        public AudioClip[] passos;
        [Tooltip("Passos correndo (vazio = usa os de andar, mais alto).")]
        public AudioClip[] passosCorrendo;
        [Range(0f, 1f)] public float volume = 0.45f;
    }

    [Header("Passos da Luma")]
    public PassosDaSuperficie[] passos =
    {
        new PassosDaSuperficie { superficie = Superficie.Madeira },
        new PassosDaSuperficie { superficie = Superficie.Azulejo },
    };
    [Tooltip("Metros entre um passo e outro, andando e correndo.")]
    public float passoAndando = 0.42f, passoCorrendo = 0.6f;
    [Tooltip("Som ao cair no chão depois de um pulo (opcional).")]
    public AudioClip aterrissar;

    [Header("Stingers (sustos curtos)")]
    [Tooltip("Sorteados quando quem chama não passa um som próprio (ex.: ScareFlash sem stinger).")]
    public AudioClip[] stingers;
    [Range(0f, 1f)] public float volumeStinger = 0.9f;
    [Tooltip("Tocado quando uma criatura começa a perseguir (opcional).")]
    public AudioClip stingerInicioPerseguicao;
    [Tooltip("A Luma puxando o ar logo depois de um susto (sorteado).")]
    public AudioClip[] arfar;

    [Header("Perseguição")]
    public AudioClip musicaDePerseguicao;
    [Range(0f, 1f)] public float volumeMusica = 0.8f;
    public float fadeEntrada = 0.6f, fadeSaida = 3f;
    [Tooltip("Espera depois que a última criatura desiste antes de a música começar a sumir.")]
    public float segurarDepoisDePerder = 1.5f;
    [Tooltip("Quanto o ambiente abaixa durante a perseguição (1 = nada).")]
    [Range(0f, 1f)] public float ambienteNaPerseguicao = 0.4f;

    [Header("Medo (o FearSystem controla o volume)")]
    public AudioClip batimento;
    public AudioClip respiracao;

    [Header("Silêncio antes do susto")]
    [Range(0f, 1f)] public float nivelDoSilencio = 0.04f;

    // ------------------------------------------------------------------ estado

    public AmbienteDoComodo Ambiente { get; private set; }
    public bool Perseguindo => perseguidores.Count > 0;

    AudioMixerGroup gAmbiente, gEfeitos, gMusica, gCriaturas;
    AudioSource loopA, loopB, rangido, passo, stinger, musica, somBatimento, somRespiracao;
    AudioSource loopAtual;
    float proxRangido, distanciaPasso, ultimoPerseguidor = -99f, silencioMult = 1f, fadeAmbiente = 1f;
    bool estavaNoChao = true;
    Vector3 ultimaPos;
    Coroutine silencioCo;
    readonly HashSet<Object> perseguidores = new HashSet<Object>();

    // ------------------------------------------------------------------ início

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<AudioManager>("AudioManager");
        var am = prefab != null ? Instantiate(prefab) : new GameObject().AddComponent<AudioManager>();
        am.name = "Umbra_Audio";
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        gAmbiente = Grupo(GrupoAmbiente); gEfeitos = Grupo(GrupoEfeitos);
        gMusica = Grupo(GrupoMusica); gCriaturas = Grupo(GrupoCriaturas);

        loopA = Fonte("Ambiente A", gAmbiente, true);
        loopB = Fonte("Ambiente B", gAmbiente, true);
        rangido = Fonte("Rangidos", gAmbiente, false);
        passo = Fonte("Passos", gEfeitos, false);
        stinger = Fonte("Stinger", gEfeitos, false);
        musica = Fonte("Música de perseguição", gMusica, true);
        somBatimento = Fonte("Batimento", gEfeitos, true);
        somRespiracao = Fonte("Respiração", gEfeitos, true);
        somBatimento.volume = somRespiracao.volume = 0f;

        SceneManager.sceneLoaded += AoCarregarCena;
        Opcoes.AplicarTudo();
        AoCarregarCena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    // O mixer às vezes ignora SetFloat feito no Awake: reaplica aqui.
    void Start() => AplicarVolumes();

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= AoCarregarCena;
        Instance = null;
    }

    void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        perseguidores.Clear();
        ultimoPerseguidor = -99f;          // cena nova: a música de perseguição some sem esperar
        silencioMult = 1f;
        if (silencioCo != null) { StopCoroutine(silencioCo); silencioCo = null; }
        var amb = FindAnyObjectByType<AmbienteDoComodo>();
        if (amb != null) DefinirAmbiente(amb); else SairDoAmbiente(Ambiente);
        LigarMedo(FearSystem.Instance);
        var st = PlayerState.Instance;
        if (st != null) ultimaPos = st.transform.position;
        distanciaPasso = 0f;
    }

    /// <summary>Grupo do mixer pelo nome (nulo se não houver mixer): para outros scripts rotearem seus AudioSources.</summary>
    public AudioMixerGroup Grupo(string nome)
    {
        if (mixer == null) return null;
        var g = mixer.FindMatchingGroups(nome);
        return g != null && g.Length > 0 ? g[0] : null;
    }

    AudioSource Fonte(string nome, AudioMixerGroup grupo, bool loop)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        s.outputAudioMixerGroup = grupo;
        return s;
    }

    // ------------------------------------------------------------------ volumes (Opções)

    /// <summary>Aplica no mixer os volumes dos grupos salvos em Opções.</summary>
    public void AplicarVolumes()
    {
        if (mixer == null) return;
        foreach (var g in Grupos)
        {
            float v = Opcoes.VolumeDoGrupo(g);
            mixer.SetFloat(ParametroDoGrupo(g), v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f);
        }
    }

    // ------------------------------------------------------------------ ambiente

    public void DefinirAmbiente(AmbienteDoComodo amb)
    {
        if (amb == null || amb == Ambiente) return;
        Ambiente = amb;
        proxRangido = Time.time + Random.Range(amb.intervaloRangidos.x, amb.intervaloRangidos.y);
        if (loopAtual != null && loopAtual.clip == amb.loopGrave && loopAtual.isPlaying) return;   // mesmo loop: continua sem cortar
        var novo = loopAtual == loopA ? loopB : loopA;
        novo.clip = amb.loopGrave;
        novo.volume = 0f;
        if (novo.clip != null) novo.Play();
        StartCoroutine(Cruzar(loopAtual, novo, 1.5f));
        loopAtual = novo;
    }

    public void SairDoAmbiente(AmbienteDoComodo amb)
    {
        if (amb == null || amb != Ambiente) return;
        Ambiente = null;
        if (loopAtual != null) StartCoroutine(Cruzar(loopAtual, null, 1.5f));
        loopAtual = null;
    }

    IEnumerator Cruzar(AudioSource sai, AudioSource entra, float tempo)
    {
        fadeAmbiente = 0f;
        float inicioSai = sai != null ? sai.volume : 0f;
        for (float t = 0; t < tempo; t += Time.unscaledDeltaTime)
        {
            float k = t / tempo;
            if (sai != null) sai.volume = inicioSai * (1f - k);
            fadeAmbiente = k;
            yield return null;
        }
        fadeAmbiente = 1f;
        if (sai != null && sai != loopAtual) sai.Stop();
    }

    /// <summary>Ambiente quase some por alguns segundos (e sem rangidos), depois volta devagar.</summary>
    public void Silencio(float segundos)
    {
        if (silencioCo != null) StopCoroutine(silencioCo);
        silencioCo = StartCoroutine(SilencioRotina(Mathf.Max(0f, segundos)));
    }

    IEnumerator SilencioRotina(float segundos)
    {
        float inicio = silencioMult;
        for (float t = 0; t < 0.4f; t += Time.deltaTime) { silencioMult = Mathf.Lerp(inicio, nivelDoSilencio, t / 0.4f); yield return null; }
        silencioMult = nivelDoSilencio;
        yield return new WaitForSeconds(segundos);
        for (float t = 0; t < 2f; t += Time.deltaTime) { silencioMult = Mathf.Lerp(nivelDoSilencio, 1f, t / 2f); yield return null; }
        silencioMult = 1f;
        silencioCo = null;
    }

    // ------------------------------------------------------------------ stingers

    /// <summary>
    /// Toca um susto curto. Sem som = sorteia um dos stingers gerais. silenciar > 0: o ambiente some junto.
    /// volume: multiplica o volume dos stingers (1 = normal; sustos grandes usam mais).
    /// </summary>
    public void TocarStinger(AudioClip som = null, float silenciar = 0f, float volume = 1f)
    {
        if (som == null && stingers != null && stingers.Length > 0) som = stingers[Random.Range(0, stingers.Length)];
        if (silenciar > 0f) Silencio(silenciar);
        if (som != null) stinger.PlayOneShot(som, volumeStinger * volume);
    }

    /// <summary>A Luma puxa o ar (depois de um susto).</summary>
    public void Arfar()
    {
        if (arfar == null || arfar.Length == 0) return;
        var c = arfar[Random.Range(0, arfar.Length)];
        if (c != null) passo.PlayOneShot(c, 0.9f);
    }

    // ------------------------------------------------------------------ perseguição

    public void PerseguicaoComecou(Object quem)
    {
        if (quem == null) return;
        bool primeira = perseguidores.Count == 0;
        perseguidores.Add(quem);
        if (primeira && stingerInicioPerseguicao != null) stinger.PlayOneShot(stingerInicioPerseguicao, volumeStinger);
    }

    public void PerseguicaoAcabou(Object quem)
    {
        if (quem != null && perseguidores.Remove(quem) && perseguidores.Count == 0) ultimoPerseguidor = Time.time;
    }

    // ------------------------------------------------------------------ medo

    /// <summary>Dá ao FearSystem as fontes de batimento e respiração (se a cena não tiver as próprias).</summary>
    public void LigarMedo(FearSystem medo)
    {
        if (medo == null) return;
        if (medo.heartbeat == null) medo.heartbeat = somBatimento;
        if (medo.breathing == null) medo.breathing = somRespiracao;
        Tocar(somBatimento, batimento);
        Tocar(somRespiracao, respiracao);
    }

    static void Tocar(AudioSource s, AudioClip c)
    {
        if (s.clip != c) { s.clip = c; s.Stop(); }
        if (c != null && !s.isPlaying) s.Play();
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        // Limpa criaturas destruídas/desligadas sem aviso.
        if (perseguidores.RemoveWhere(o => o == null || (o is Behaviour b && !b.isActiveAndEnabled)) > 0 && perseguidores.Count == 0)
            ultimoPerseguidor = Time.time;

        AtualizarMusica();
        AtualizarAmbiente();
        AtualizarPassos();
    }

    void AtualizarMusica()
    {
        var clip = Ambiente != null && Ambiente.musicaDePerseguicao != null ? Ambiente.musicaDePerseguicao : musicaDePerseguicao;
        bool tocar = Perseguindo || Time.time - ultimoPerseguidor < segurarDepoisDePerder;
        if (tocar && clip != null && (musica.clip != clip || !musica.isPlaying))
        {
            musica.clip = clip;
            musica.volume = 0f;
            musica.Play();
        }
        float alvo = tocar ? volumeMusica : 0f;
        float vel = volumeMusica / Mathf.Max(0.05f, tocar ? fadeEntrada : fadeSaida);
        musica.volume = Mathf.MoveTowards(musica.volume, alvo, vel * Time.unscaledDeltaTime);
        if (!tocar && musica.isPlaying && musica.volume <= 0.001f) musica.Stop();
    }

    void AtualizarAmbiente()
    {
        float perseguicao = Mathf.Lerp(1f, ambienteNaPerseguicao, volumeMusica > 0f ? musica.volume / volumeMusica : 0f);
        if (loopAtual != null && Ambiente != null)
            loopAtual.volume = Ambiente.volumeLoop * fadeAmbiente * silencioMult * perseguicao;

        if (Ambiente == null || Ambiente.rangidos == null || Ambiente.rangidos.Length == 0) return;
        if (Time.time < proxRangido) return;
        proxRangido = Time.time + Random.Range(Ambiente.intervaloRangidos.x, Mathf.Max(Ambiente.intervaloRangidos.x, Ambiente.intervaloRangidos.y));
        if (silencioMult < 0.5f) return;                     // no silêncio antes do susto, nada range
        var c = Ambiente.rangidos[Random.Range(0, Ambiente.rangidos.Length)];
        if (c == null) return;
        rangido.panStereo = Random.Range(-Ambiente.espalhamento, Ambiente.espalhamento);
        rangido.pitch = Random.Range(0.9f, 1.08f);
        rangido.PlayOneShot(c, Random.Range(Ambiente.volumeRangidos.x, Ambiente.volumeRangidos.y) * perseguicao);
    }

    void AtualizarPassos()
    {
        var st = PlayerState.Instance;
        if (st == null || st.Movement == null) return;
        Vector3 pos = st.transform.position;
        Vector3 d = pos - ultimaPos; d.y = 0f;
        ultimaPos = pos;
        var cc = st.Controller;
        bool noChao = cc == null || cc.isGrounded;

        // Aterrissou depois de um pulo/queda.
        if (noChao && !estavaNoChao && aterrissar != null) passo.PlayOneShot(aterrissar, VolumePassos(SuperficieAtual()));
        estavaNoChao = noChao;

        if (!noChao || st.isHidden || !st.Movement.isMoving || d.magnitude > 1.5f) { if (d.magnitude > 1.5f) distanciaPasso = 0f; return; }
        distanciaPasso += d.magnitude;
        bool correndo = st.Movement.isRunning;
        if (distanciaPasso < (correndo ? passoCorrendo : passoAndando)) return;
        distanciaPasso = 0f;

        var sup = SuperficieAtual();
        var cfg = Passos(sup);
        if (cfg == null) return;
        var lista = correndo && cfg.passosCorrendo != null && cfg.passosCorrendo.Length > 0 ? cfg.passosCorrendo : cfg.passos;
        if (lista == null || lista.Length == 0) return;
        var c = lista[Random.Range(0, lista.Length)];
        if (c == null) return;
        passo.pitch = Random.Range(0.92f, 1.08f);
        float vol = cfg.volume * (correndo ? 1.35f : 1f) * (st.isGrabbing ? 0.7f : 1f);
        passo.PlayOneShot(c, Mathf.Clamp01(vol));
    }

    float VolumePassos(Superficie s) { var p = Passos(s); return p != null ? p.volume : 0.4f; }

    PassosDaSuperficie Passos(Superficie s)
    {
        if (passos == null) return null;
        foreach (var p in passos) if (p != null && p.superficie == s) return p;
        return passos.Length > 0 ? passos[0] : null;
    }

    /// <summary>Superfície sob a Luma: área-gatilho SuperficieDoChao > colisor com SuperficieDoChao > padrão da cena.</summary>
    Superficie SuperficieAtual()
    {
        if (SuperficieDoChao.AreaAtual != null) return SuperficieDoChao.AreaAtual.superficie;
        var st = PlayerState.Instance;
        if (st != null && Physics.Raycast(st.transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore))
        {
            var sc = hit.collider.GetComponentInParent<SuperficieDoChao>();
            if (sc != null) return sc.superficie;
        }
        return Ambiente != null ? Ambiente.superficiePadrao : Superficie.Madeira;
    }
}
