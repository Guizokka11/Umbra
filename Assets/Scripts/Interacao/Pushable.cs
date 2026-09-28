using UnityEngine;

/// <summary>
/// Caixa, cadeira, baú... que a Luma pode segurar e arrastar (empurrar/puxar).
/// Requer Rigidbody (não-kinematic) e Collider sólido.
/// Enquanto segura: Luma anda mais devagar, não pula e não vira o sprite.
/// Se o objeto travar em uma parede e ela se afastar demais, solta sozinha.
/// Pesado: começa devagar e embala (toques curtos = devagar). Arrastar rápido faz barulho (NoiseEmitter),
/// proporcional à velocidade; barulho alto repetido chama a Inspetora da ronda, se ela estiver no andar.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pushable : Interactable
{
    [Header("Arrastar")]
    [Range(0.1f, 1f)] public float dragSpeedMultiplier = 0.45f;
    [Tooltip("Solta se a distância até a Luma aumentar mais que isso (objeto travou).")]
    public float maxGapIncrease = 0.4f;
    [Tooltip("Mantém o objeto no mesmo Z (corredores 2.5D).")]
    public bool lockZ = true;
    [Tooltip("Objeto pesado demais: não pode ser arrastado, só usado como plataforma.")]
    public bool locked = false;

    [Header("Empurrão ao encostar (sem segurar)")]
    public bool  allowFreePush = false;
    public float freePushSpeed = 1.2f;

    [Header("Som (opcional)")]
    public AudioSource dragLoop;

    [Header("Peso (embalo)")]
    [Tooltip("Velocidade ao começar a arrastar (fração da velocidade de arrasto). Toques curtos = devagar e quase sem barulho.")]
    [Range(0.1f, 1f)] public float velocidadeInicial = 0.3f;
    [Tooltip("Segundos arrastando sem parar até chegar na velocidade de arrasto.")]
    public float tempoParaEmbalar = 1.2f;

    [Header("Barulho ao arrastar")]
    public bool fazBarulho = true;
    [Tooltip("Abaixo desta fração da velocidade máxima de arrasto, não faz barulho.")]
    [Range(0f, 1f)] public float velocidadeSilenciosa = 0.5f;
    [Tooltip("Raio do barulho (m): logo acima do silencioso e na velocidade máxima.")]
    public Vector2 raioDoBarulho = new Vector2(2f, 9f);
    public float intervaloDoBarulho = 0.6f;
    [Tooltip("Barulho deste raio para cima, duas vezes seguidas, chama a Inspetora da ronda (se estiver no andar).")]
    public float raioQueChamaARonda = 7f;

    Rigidbody rb;
    Collider[] myCols;
    PlayerInteractor holder;
    Vector3 offset;
    float startGap;
    NoiseEmitter ruido;
    float embalo, proxRuido;
    int barulhosAltos;

    /// <summary>Raio do barulho de arrasto agora (0 = silencioso). Para depurar.</summary>
    public float BarulhoAtual { get; private set; }
    /// <summary>Raio e hora (Time.time) do último barulho feito. Para depurar (F12).</summary>
    public float UltimoBarulho { get; private set; }
    public float QuandoUltimoBarulho { get; private set; } = -99f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCols = GetComponentsInChildren<Collider>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // não atravessa chão fino
        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | (lockZ ? RigidbodyConstraints.FreezePositionZ : RigidbodyConstraints.None);
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Segurar";
        ruido = GetComponent<NoiseEmitter>();
        if (ruido == null) ruido = gameObject.AddComponent<NoiseEmitter>();
        ruido.cooldown = 0f;                         // o intervalo é controlado aqui
    }

    void Update()
    {
        if (holder == null) return;
        // Embalo: parado zera; empurrando sem parar vai até a velocidade de arrasto.
        var mv = holder.State.Movement;
        embalo = mv.isMoving ? Mathf.MoveTowards(embalo, 1f, Time.deltaTime / Mathf.Max(0.05f, tempoParaEmbalar)) : 0f;
        mv.speedMultiplier = dragSpeedMultiplier * Mathf.Lerp(velocidadeInicial, 1f, embalo);
    }

    public override bool CanInteract(PlayerInteractor who)
    {
        if (!base.CanInteract(who) || locked) return false;
        // Em cima da caixa não dá para segurá-la (deixa o trinco/objeto alto ser o alvo).
        var col = GetComponent<Collider>();
        if (col != null && who.transform.position.y > col.bounds.max.y - 0.15f) return false;
        return true;
    }

    public override void Interact(PlayerInteractor who)
    {
        if (holder == null) Grab(who);
        else Release();
    }

    void Grab(PlayerInteractor who)
    {
        holder = who;
        var st = who.State;
        st.isGrabbing = true;
        st.heldObject = transform;
        embalo = 0f;
        st.Movement.speedMultiplier = dragSpeedMultiplier * velocidadeInicial;
        st.Movement.canJump = false;
        st.Movement.lockFacing = true;

        offset = transform.position - who.transform.position;
        offset.y = 0f;
        startGap = offset.magnitude;

        var cc = st.Controller;
        foreach (var c in myCols) Physics.IgnoreCollision(cc, c, true);

        who.Lock(this);
        if (dragLoop != null) dragLoop.Play();
    }

    public void Release()
    {
        if (holder == null) return;
        var st = holder.State;
        st.isGrabbing = false;
        st.heldObject = null;
        st.Movement.speedMultiplier = 1f;
        st.Movement.canJump = true;
        st.Movement.lockFacing = false;

        var cc = st.Controller;
        foreach (var c in myCols) Physics.IgnoreCollision(cc, c, false);

        holder.Unlock(this);
        holder = null;
        embalo = 0f;
        BarulhoAtual = 0f;
        if (dragLoop != null) dragLoop.Stop();
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    public override void ForceRelease(PlayerInteractor who) => Release();

    void FixedUpdate()
    {
        if (holder == null) return;

        Vector3 target = holder.transform.position + offset;
        Vector3 delta  = target - rb.position;
        delta.y = 0f;
        if (lockZ) delta.z = 0f;

        Vector3 v = delta / Time.fixedDeltaTime;
        v = Vector3.ClampMagnitude(v, 8f);
        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);

        Vector3 gap = rb.position - holder.transform.position; gap.y = 0f;
        if (gap.magnitude > startGap + maxGapIncrease) Release();

        if (dragLoop != null) dragLoop.volume = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude);
        Barulho(new Vector2(v.x, v.z).magnitude);
    }

    void Barulho(float velocidadeDoObjeto)
    {
        // Pela velocidade com que a Luma arrasta (o corpo físico dá picos quando atrasa); parado/travado não conta.
        var mv = holder.State.Movement;
        float r = mv.isMoving && velocidadeDoObjeto > 0.05f ? mv.speedMultiplier / Mathf.Max(0.01f, dragSpeedMultiplier) : 0f;
        if (!fazBarulho || r <= velocidadeSilenciosa) { BarulhoAtual = 0f; barulhosAltos = 0; return; }
        if (Time.time < proxRuido) return;
        proxRuido = Time.time + intervaloDoBarulho;
        float raio = Mathf.Lerp(raioDoBarulho.x, raioDoBarulho.y, Mathf.InverseLerp(velocidadeSilenciosa, 1f, r));
        BarulhoAtual = raio;
        UltimoBarulho = raio;
        QuandoUltimoBarulho = Time.time;
        ruido.radius = raio;
        ruido.Emit();
        barulhosAltos = raio >= raioQueChamaARonda ? barulhosAltos + 1 : 0;
        if (barulhosAltos >= 2 && RondaDoAndar.Instance != null)
            RondaDoAndar.Instance.ChamarPara(gameObject.scene.name);
    }
}
