using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// CONFRONTO FINAL: fechar a passagem.
/// Segurar a tecla de interação empurra a porta — mas ela só avança enquanto
/// o medo está abaixo de "maxFearToPush". A Umbra investe de tempos em tempos,
/// aumentando o medo e empurrando a porta de volta. O jogador alterna entre
/// empurrar (E) e abraçar o urso (F) para se acalmar.
/// </summary>
public class FinalDoor : Interactable
{
    [Header("Porta")]
    public Transform door;               // objeto que gira/desliza
    public Vector3 openLocalPos;
    public Vector3 closedLocalPos;
    [Range(0f, 1f)] public float progress = 0f;
    public float pushSpeed = 0.08f;       // por segundo
    public float maxFearToPush = 0.5f;
    public KeyCode holdKey = KeyCode.E;

    [Header("Investidas da Umbra")]
    public Vector2 surgeInterval = new Vector2(4f, 7f);
    public float surgeFear = 0.35f;
    public float surgePushBack = 0.12f;
    public AudioSource surgeSfx;
    public AudioSource pushLoop;

    public UnityEvent onSurge;
    public UnityEvent onClosed;

    bool active, done;
    float nextSurge;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Segurar a porta";
    }

    /// <summary>Começa o confronto (chamar por evento/trigger).</summary>
    public void Begin()
    {
        active = true;
        nextSurge = Time.time + Random.Range(surgeInterval.x, surgeInterval.y);
    }

    public override void Interact(PlayerInteractor who)
    {
        if (!active && !done) Begin();
    }

    void Update()
    {
        if (!active || done) return;
        var st = PlayerState.Instance;
        var fear = FearSystem.Instance;
        if (st == null) return;

        Vector3 d = st.transform.position - Point; d.y = 0f;
        bool near = d.magnitude <= 1.5f + extraRange;
        bool pushing = near && Input.GetKey(holdKey) && !st.isHuggingBear;
        bool calm = fear == null || fear.fear <= maxFearToPush;

        if (pushing && calm) progress += pushSpeed * Time.deltaTime;
        if (pushLoop != null)
        {
            if (pushing && calm && !pushLoop.isPlaying) pushLoop.Play();
            else if (!(pushing && calm) && pushLoop.isPlaying) pushLoop.Stop();
        }

        if (Time.time >= nextSurge)
        {
            nextSurge = Time.time + Random.Range(surgeInterval.x, surgeInterval.y);
            progress -= surgePushBack;
            if (fear != null) fear.AddFear(surgeFear);
            if (surgeSfx != null) surgeSfx.Play();
            onSurge.Invoke();
        }

        progress = Mathf.Clamp01(progress);
        if (door != null) door.localPosition = Vector3.Lerp(openLocalPos, closedLocalPos, progress);

        if (progress >= 1f)
        {
            done = true;
            active = false;
            if (pushLoop != null) pushLoop.Stop();
            onClosed.Invoke();
        }
    }
}
