using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puzzle sob pressão (reutilizável). Ao começar: aviso (luzes piscando, som de início) e passos que se aproximam.
///  - Tarefa com duração (ex.: a caixa de luz demora para ligar): conclui sozinho depois de "duracao" segundos;
///  - Ficar no lugar: se a Luma se afastar mais que "raioParaFicar", falha (ex.: o fusível cai) e dá para recomeçar;
///  - Tempo limite: se não terminar em "tempoLimite" s, chama a criatura / a ronda e dispara onTempoEsgotado.
/// Nunca impossível: na primeira tentativa o tempo limite ganha folga.
/// Uso: ItemLock.pressao (usar o item começa o puzzle e só resolve no fim), ou Comecar()/Concluir() por evento.
/// </summary>
public class PuzzleSobPressao : MonoBehaviour
{
    [Header("Tarefa")]
    [Tooltip("Segundos até a tarefa se completar sozinha (0 = alguém chama Concluir()).")]
    public float duracao = 6f;
    [Tooltip("A Luma precisa ficar perto de onde começou (m). 0 = pode andar.")]
    public float raioParaFicar = 1.3f;
    [TextArea] [Tooltip("Mensagem se ela sair do lugar (vazio = nenhuma).")]
    public string mensagemAoFalhar = "Soltou... tem que segurar até o fim.";

    [Header("Tempo limite")]
    [Tooltip("Se não terminar em tantos segundos, a pressão \"chega\" (0 = sem limite).")]
    public float tempoLimite = 0f;
    [Tooltip("Primeira tentativa: o tempo limite é multiplicado por isto (tempo de sobra).")]
    public float folgaNaPrimeiraTentativa = 1.6f;
    [Tooltip("Esgotou o tempo: esta criatura começa a perseguir (opcional).")]
    public CreatureAI criatura;
    [Tooltip("Esgotou o tempo: esta massa começa a perseguir (opcional).")]
    public InspetoraMass massa;
    [Tooltip("Esgotou o tempo: a Inspetora da ronda vem para este cômodo (se estiver no andar).")]
    public bool chamarARonda = true;

    [Header("Aviso")]
    [Tooltip("Luzes que piscam enquanto dura (cada vez mais) e voltam ao que eram no fim.")]
    public LightZone[] luzesQuePiscam;
    [Tooltip("As luzes piscam acendendo (ex.: lâmpada tentando ligar) em vez de apagando.")]
    public bool piscarAcendendo = true;
    public AudioClip somAoComecar;
    [Tooltip("Passos se aproximando: um sorteado a cada passo, cada vez mais alto e rápido.")]
    public AudioClip[] passos;
    public AudioClip somAoFalhar;
    public AudioClip somAoConcluir;

    [Header("Eventos")]
    public UnityEvent onComecar;
    public UnityEvent onConcluir;
    [Tooltip("A Luma saiu do lugar (ou Falhar() foi chamado).")]
    public UnityEvent onFalhar;
    public UnityEvent onTempoEsgotado;

    public bool Rodando => co != null;
    /// <summary>Tentativas nesta visita (a primeira ganha folga).</summary>
    public int Tentativas { get; private set; }

    Coroutine co;
    Action aoConcluir, aoFalhar;
    AudioSource fonte;
    readonly Dictionary<LightZone, bool> luzesAntes = new Dictionary<LightZone, bool>();

    void Awake()
    {
        var go = new GameObject("Som do puzzle");
        go.transform.SetParent(transform, false);
        fonte = go.AddComponent<AudioSource>();
        fonte.playOnAwake = false;
        fonte.spatialBlend = 0f;
    }

    void Start()
    {
        if (AudioManager.Instance != null) fonte.outputAudioMixerGroup = AudioManager.Instance.Grupo(AudioManager.GrupoEfeitos);
        if (GameManager.Instance != null) GameManager.Instance.onRespawn.AddListener(() => { if (Rodando) Falhar(); });
    }

    /// <summary>Começa (por evento). Para callbacks em código use Comecar(aoConcluir, aoFalhar).</summary>
    public void Comecar() => Comecar(null, null);

    public void Comecar(Action concluir, Action falhar)
    {
        if (Rodando) return;
        aoConcluir = concluir; aoFalhar = falhar;
        Tentativas++;
        co = StartCoroutine(Rodar());
    }

    IEnumerator Rodar()
    {
        onComecar?.Invoke();
        Tocar(somAoComecar, 1f);
        var st = PlayerState.Instance;
        Vector3 inicio = st != null ? st.transform.position : transform.position;
        GuardarLuzes();

        float limite = tempoLimite > 0f ? tempoLimite * (Tentativas == 1 ? Mathf.Max(1f, folgaNaPrimeiraTentativa) : 1f) : 0f;
        float t0 = Time.time, proxPasso = Time.time + 0.6f, proxPisca = Time.time;
        bool esgotou = false;
        while (true)
        {
            float t = Time.time - t0;
            float k = duracao > 0f ? Mathf.Clamp01(t / duracao) : (limite > 0f ? Mathf.Clamp01(t / limite) : 0.5f);

            // Saiu do lugar: falha.
            if (raioParaFicar > 0f && st != null)
            {
                Vector3 d = st.transform.position - inicio; d.y = 0f;
                if (d.magnitude > raioParaFicar || st.isDead) { Falhar(); yield break; }
            }
            // Tarefa pronta.
            if (duracao > 0f && t >= duracao) { Concluir(); yield break; }
            // Tempo limite.
            if (!esgotou && limite > 0f && t >= limite) { esgotou = true; Esgotou(); }

            // Luzes piscando cada vez mais.
            if (Time.time >= proxPisca)
            {
                proxPisca = Time.time + UnityEngine.Random.Range(0.08f, Mathf.Lerp(0.6f, 0.15f, k));
                foreach (var kv in luzesAntes)
                    if (kv.Key != null) kv.Key.SetOn(UnityEngine.Random.value < (piscarAcendendo ? Mathf.Lerp(0.15f, 0.6f, k) : Mathf.Lerp(0.85f, 0.4f, k)));
            }
            // Passos chegando: mais alto e mais rápido.
            if (passos != null && passos.Length > 0 && Time.time >= proxPasso)
            {
                proxPasso = Time.time + Mathf.Lerp(0.9f, 0.45f, k);
                var c = passos[UnityEngine.Random.Range(0, passos.Length)];
                if (c != null) Tocar(c, Mathf.Lerp(0.2f, 1f, k));
            }
            yield return null;
        }
    }

    /// <summary>Terminou bem (sozinho pela duração, ou chamado por outro script).</summary>
    public void Concluir()
    {
        if (co != null) StopCoroutine(co);
        co = null;
        RestaurarLuzes();
        Tocar(somAoConcluir, 1f);
        var cb = aoConcluir; aoConcluir = aoFalhar = null;
        cb?.Invoke();
        onConcluir?.Invoke();
    }

    /// <summary>Falhou (saiu do lugar, foi pega): dá para recomeçar.</summary>
    public void Falhar()
    {
        if (co != null) StopCoroutine(co);
        co = null;
        RestaurarLuzes();
        Tocar(somAoFalhar, 1f);
        if (!string.IsNullOrEmpty(mensagemAoFalhar)) Hud.Toast(mensagemAoFalhar);
        var cb = aoFalhar; aoConcluir = aoFalhar = null;
        cb?.Invoke();
        onFalhar?.Invoke();
    }

    void Esgotou()
    {
        if (criatura != null) { criatura.gameObject.SetActive(true); criatura.StartChase(); }
        if (massa != null) massa.StartChase();
        if (chamarARonda && RondaDoAndar.Instance != null) RondaDoAndar.Instance.ChamarPara(gameObject.scene.name, 0f);
        onTempoEsgotado?.Invoke();
    }

    void GuardarLuzes()
    {
        luzesAntes.Clear();
        if (luzesQuePiscam != null) foreach (var l in luzesQuePiscam) if (l != null && !luzesAntes.ContainsKey(l)) luzesAntes[l] = l.isOn;
    }

    void RestaurarLuzes()
    {
        foreach (var kv in luzesAntes) if (kv.Key != null) kv.Key.SetOn(kv.Value);
        luzesAntes.Clear();
    }

    void Tocar(AudioClip c, float volume)
    {
        if (c != null && fonte != null) fonte.PlayOneShot(c, volume);
    }
}
