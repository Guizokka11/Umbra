using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Busca: a criatura (CreatureAI) entra farejando e checa uma lista de lugares, um por um. Em cada um para
/// 2–3 s, "abre" (som de porta ou cortina; a cortina/porta balança) e segue. No esconderijo onde a Luma está,
/// para na frente, respira fundo... e vai embora sem achá-la. Se a Luma sair do esconderijo durante a busca
/// (ou não estiver escondida quando ela terminar de farejar a entrada), é pega.
/// Reutilizável: pontos a checar, tempo em cada e eventos. O ScriptedEncounter usa a busca se tiver uma.
/// </summary>
public class BuscaDaCriatura : MonoBehaviour
{
    [System.Serializable]
    public class Ponto
    {
        public string nome = "Lugar";
        [Tooltip("Onde ela para (no chão, na frente do lugar).")]
        public Transform onde;
        [Tooltip("Esconderijo deste lugar (opcional). Com a Luma dentro, ela não abre: respira fundo e vai embora.")]
        public HidingSpot esconderijo;
        [Tooltip("Cortina/porta que balança quando ela \"abre\" (opcional).")]
        public Transform balancar;
        [Tooltip("Som de abrir (porta da cabine, cortina correndo). Vazio = sem som.")]
        public AudioClip somAoAbrir;
    }

    [Tooltip("A criatura que busca. O ScriptedEncounter preenche com a criatura dele.")]
    public CreatureAI criatura;
    [Tooltip("Onde ela entra e fareja antes de começar.")]
    public Transform entrada;
    [Tooltip("Por onde ela vai embora no fim.")]
    public Transform saida;
    public Ponto[] pontos;

    [Header("Tempos")]
    [Tooltip("Farejando na entrada (a Luma ainda pode terminar de se esconder).")]
    public float farejarNaEntrada = 2.5f;
    [Tooltip("Segundos parada em cada lugar (mínimo, máximo).")]
    public Vector2 tempoEmCada = new Vector2(2f, 3f);
    [Tooltip("Parada na frente do esconderijo da Luma, respirando fundo.")]
    public float tempoNaFrenteDaLuma = 4f;
    [Tooltip("Medo que sobe na Luma quando ela para na frente do esconderijo.")]
    public float medoNaFrente = 0.35f;

    [Header("Sons (vazios: ver Assets/Audio/LEIAME.md)")]
    public AudioClip farejar;
    public AudioClip respirarFundo;

    [Header("Regras")]
    [Tooltip("Luma fora do esconderijo durante a busca = a criatura a vê e persegue (pega).")]
    public bool pegaQuemSai = true;
    [Tooltip("Descoberta na busca: por estes segundos a criatura não perde a Luma de vista (vai direto nela).")]
    public float segundosSemPerder = 6f;

    [Tooltip("Escreve cada passo da busca no Console (para testar).")]
    public bool mostrarNoConsole;

    [Header("Eventos")]
    public UnityEvent onComecar;
    public UnityEvent onChecarLugar;
    [Tooltip("Parou na frente do esconderijo onde a Luma está.")]
    public UnityEvent onNaFrenteDaLuma;
    public UnityEvent onPegou;
    public UnityEvent onTerminar;

    /// <summary>A busca acabou (ela foi embora ou a Luma foi descoberta).</summary>
    public bool Terminou { get; private set; }
    public bool Rodando => co != null;

    Coroutine co;
    AudioSource fonte;

    void Awake()
    {
        var go = new GameObject("Som da busca");
        go.transform.SetParent(transform, false);
        fonte = go.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.spatialBlend = 0f;
    }

    void Start()
    {
        if (AudioManager.Instance != null) fonte.outputAudioMixerGroup = AudioManager.Instance.Grupo(AudioManager.GrupoCriaturas);
    }

    public void Iniciar(CreatureAI quem = null)
    {
        if (quem != null) criatura = quem;
        Parar();
        Terminou = false;
        if (criatura == null) { Terminou = true; return; }
        co = StartCoroutine(Buscar());
    }

    public void Parar()
    {
        if (co != null) StopCoroutine(co);
        co = null;
    }

    // ------------------------------------------------------------------ busca

    IEnumerator Buscar()
    {
        onComecar?.Invoke();
        yield return null;          // criatura recém-ligada: o Start() dela roda neste quadro e definiria a patrulha por cima

        // Entra farejando.
        if (entrada != null) criatura.Posicionar(entrada.position);
        criatura.Farejar(criatura.transform.position, farejarNaEntrada);
        Tocar(farejar, 1f);
        Log("entrou farejando");
        float fim = Time.time + farejarNaEntrada;
        while (Time.time < fim) { if (Descobriu(false)) yield break; yield return null; }   // ainda dá tempo de se esconder

        // Checa cada lugar.
        if (pontos != null)
            foreach (var p in pontos)
            {
                if (p == null || p.onde == null) continue;
                float espera = Random.Range(tempoEmCada.x, Mathf.Max(tempoEmCada.x, tempoEmCada.y));
                bool aLumaEstaAqui = p.esconderijo != null && p.esconderijo.Occupied;
                criatura.Farejar(p.onde.position, aLumaEstaAqui ? tempoNaFrenteDaLuma + 3f : espera + 3f);

                // Vai até lá.
                float limite = Time.time + 12f;
                while (Distancia(p.onde.position) > 0.45f && Time.time < limite)
                {
                    if (Descobriu()) yield break;
                    yield return null;
                }

                aLumaEstaAqui = p.esconderijo != null && p.esconderijo.Occupied;
                if (aLumaEstaAqui)
                {
                    // Na frente de onde a Luma está: para, respira fundo... e vai embora.
                    Log("parou na frente de \"" + p.nome + "\" (a Luma está aí): respira fundo e vai embora");
                    onNaFrenteDaLuma?.Invoke();
                    Tocar(respirarFundo, 1f);
                    if (FearSystem.Instance != null) FearSystem.Instance.AddFear(medoNaFrente);
                    fim = Time.time + tempoNaFrenteDaLuma;
                    while (Time.time < fim) { if (Descobriu()) yield break; yield return null; }
                    break;
                }

                // "Abre": som e a cortina/porta balança; fica um pouco olhando.
                Log("abriu \"" + p.nome + "\"");
                onChecarLugar?.Invoke();
                Tocar(p.somAoAbrir, 1f);
                if (p.balancar != null) StartCoroutine(Balancar(p.balancar));
                fim = Time.time + espera;
                while (Time.time < fim) { if (Descobriu()) yield break; yield return null; }
            }

        // Vai embora.
        if (saida != null)
        {
            criatura.DefinirRonda(new[] { saida });
            float limite = Time.time + 15f;
            while (Distancia(saida.position) > 0.45f && Time.time < limite)
            {
                if (Descobriu()) yield break;
                yield return null;
            }
        }
        Terminou = true;
        co = null;
        Log("foi embora");
        onTerminar?.Invoke();
    }

    /// <summary>A Luma está fora do esconderijo (ou já está sendo perseguida): ela é vista e perseguida.</summary>
    bool Descobriu(bool contaExposta = true)
    {
        var st = PlayerState.Instance;
        if (criatura == null || !criatura.isActiveAndEnabled) { Terminou = true; co = null; return true; }
        bool persegue = criatura.State == CreatureState.Chase;
        bool exposta = contaExposta && pegaQuemSai && st != null && !st.isHidden && !st.isDead;
        if (!persegue && !exposta) return false;
        if (!persegue) criatura.StartChase();
        criatura.PerseguirSemPerder(segundosSemPerder);            // atrás de divisória ou não, ela vai direto na Luma
        Log(persegue ? "já perseguindo" : "a Luma está fora do esconderijo: persegue");
        onPegou?.Invoke();
        Terminou = true;
        co = null;
        return true;
    }

    float Distancia(Vector3 p)
    {
        Vector3 d = p - criatura.transform.position; d.y = 0f;
        return d.magnitude;
    }

    void Log(string msg) { if (mostrarNoConsole) Debug.Log("[Umbra] Busca: " + msg, this); }

    void Tocar(AudioClip c, float volume)
    {
        if (c != null && fonte != null) fonte.PlayOneShot(c, volume);
    }

    /// <summary>A cortina/porta balança como se alguém a puxasse, e para devagar.</summary>
    static IEnumerator Balancar(Transform t)
    {
        Quaternion r0 = t.localRotation;
        Vector3 s0 = t.localScale;
        const float dur = 1.2f;
        for (float k = 0f; k < dur; k += Time.deltaTime)
        {
            float a = Mathf.Sin(k * 22f) * (1f - k / dur);
            t.localRotation = r0 * Quaternion.Euler(0f, 0f, a * 2.5f);
            t.localScale = new Vector3(s0.x * (1f + a * 0.04f), s0.y, s0.z);
            yield return null;
        }
        t.localRotation = r0;
        t.localScale = s0;
    }

    void OnDrawGizmosSelected()
    {
        if (pontos == null) return;
        Vector3? antes = entrada != null ? entrada.position : (Vector3?)null;
        foreach (var p in pontos)
        {
            if (p == null || p.onde == null) continue;
            Gizmos.color = p.esconderijo != null ? new Color(1f, 0.4f, 0.2f) : new Color(1f, 0.85f, 0.3f);
            Gizmos.DrawWireSphere(p.onde.position, 0.3f);
            if (antes.HasValue) Gizmos.DrawLine(antes.Value, p.onde.position);
            antes = p.onde.position;
        }
        if (saida != null && antes.HasValue) { Gizmos.color = Color.gray; Gizmos.DrawLine(antes.Value, saida.position); }
    }
}
