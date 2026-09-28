using UnityEngine;

/// <summary>
/// Som de uma criatura (CreatureAI ou InspetoraMass), no grupo "Criaturas" do mixer:
///  - loop de presença 3D (respiração, galhos arrastando) que fica mais alto quando ela se move;
///  - um som quando ela começa a perseguir (grito, estalo de galhos).
/// Colocado em cada criatura pelo menu "Umbra > Terror > Colocar sons na cena aberta". Sons vazios: a equipe preenche.
/// </summary>
public class SomDaCriatura : MonoBehaviour
{
    [Tooltip("Loop 3D enquanto a criatura existe (respiração, galhos).")]
    public AudioClip loopPresenca;
    [Range(0f, 1f)] public float volumeParada = 0.25f;
    [Range(0f, 1f)] public float volumeAndando = 0.7f;
    [Tooltip("Tocado uma vez quando ela começa a perseguir.")]
    public AudioClip aoPerseguir;
    [Range(0f, 1f)] public float volumeAoPerseguir = 1f;
    [Tooltip("Distância (m) em que o loop some totalmente.")]
    public float alcance = 14f;

    AudioSource loop, grito;
    Vector3 ultimaPos;
    float mexendo;
    bool perseguia;
    CreatureAI ia;
    InspetoraMass massa;

    void Awake()
    {
        ia = GetComponent<CreatureAI>();
        massa = GetComponent<InspetoraMass>();
        loop = Fonte("Som: presença", true);
        grito = Fonte("Som: perseguir", false);
        ultimaPos = transform.position;
    }

    void Start()
    {
        var g = AudioManager.Instance != null ? AudioManager.Instance.Grupo(AudioManager.GrupoCriaturas) : null;
        loop.outputAudioMixerGroup = grito.outputAudioMixerGroup = g;
        if (loopPresenca != null) { loop.clip = loopPresenca; loop.volume = 0f; loop.Play(); }
    }

    AudioSource Fonte(string nome, bool emLoop)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = emLoop;
        s.spatialBlend = 1f;                                  // 3D: vem de onde ela está
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = alcance;
        s.dopplerLevel = 0f;
        return s;
    }

    void Update()
    {
        float vel = (transform.position - ultimaPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        ultimaPos = transform.position;
        mexendo = Mathf.MoveTowards(mexendo, vel > 0.2f ? 1f : 0f, Time.deltaTime * 2f);
        if (loop.clip != null) loop.volume = Mathf.Lerp(volumeParada, volumeAndando, mexendo);

        bool persegue = (ia != null && ia.State == CreatureState.Chase) || (massa != null && massa.EstaPerseguindo);
        if (persegue && !perseguia && aoPerseguir != null) grito.PlayOneShot(aoPerseguir, volumeAoPerseguir);
        perseguia = persegue;
    }
}
