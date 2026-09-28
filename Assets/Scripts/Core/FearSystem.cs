using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Medo da Luma (0 a 1). Sobe no escuro e perto de criaturas, desce na luz
/// e quando ela abraça o urso. Quanto maior o medo, mais fácil é ser vista.
/// Sem barra na tela: o medo é mostrado com vinheta, respiração e batimentos.
/// Colocar no objeto Player.
/// </summary>
[RequireComponent(typeof(PlayerState))]
public class FearSystem : MonoBehaviour
{
    public static FearSystem Instance { get; private set; }

    [Range(0f, 1f)] public float fear = 0f;

    [Header("Ganho / perda por segundo")]
    public float darkRise     = 0.03f;
    public float lightFall    = 0.08f;
    public float threatRise   = 0.25f;   // criatura próxima (escala com a distância)
    public float chaseRise    = 0.20f;   // extra enquanto é perseguida
    public float hugFall      = 0.35f;
    public float threatRadius = 7f;

    [Header("Urso de pelúcia (âncora emocional)")]
    public KeyCode hugKey = KeyCode.F;
    [Tooltip("Luma só consegue abraçar o urso parada e no chão.")]
    public bool requireStanding = true;

    [Header("Feedback (opcional)")]
    [Tooltip("Global Volume com override de Vignette.")]
    public Volume volume;
    public float vignetteCalm  = 0.2f;
    public float vignettePanic = 0.55f;
    public AudioSource heartbeat;     // loop; volume e pitch sobem com o medo
    public AudioSource breathing;     // loop

    [Header("Eventos")]
    public UnityEvent onPanicStart;   // medo passou de 0.85
    public UnityEvent onPanicEnd;

    /// <summary>Multiplica a distância de visão das criaturas.</summary>
    public float DetectionMultiplier => Mathf.Lerp(0.8f, 1.6f, fear);
    public bool  InPanic { get; private set; }

    PlayerState state;
    Vignette vignette;
    float vinhetaExtra;                 // pulso de susto (some sozinho)
    float medoMinimo, medoMinimoAte;    // depois de um susto: medo alto por alguns segundos

    /// <summary>Está no "depois" de um susto (medo segurado alto, respiração acelerada).</summary>
    public bool Assustada => Time.time < medoMinimoAte;

    void Awake()
    {
        Instance = this;
        state = GetComponent<PlayerState>();
    }

    void Update()
    {
        if (state.isDead) return;

        HandleHug();

        float dt = Time.deltaTime;
        float delta = 0f;

        if (state.IsInLight) delta -= lightFall;
        else                 delta += darkRise;

        float threat = 0f; bool chased = false;
        foreach (var c in CreatureAI.Active)
        {
            float d = Vector3.Distance(c.transform.position, transform.position);
            if (d < threatRadius) threat = Mathf.Max(threat, 1f - d / threatRadius);
            if (c.State == CreatureState.Chase) chased = true;
        }
        delta += threat * threatRise;
        if (chased) delta += chaseRise;

        if (state.isHuggingBear) delta -= hugFall;

        fear = Mathf.Clamp01(fear + delta * dt);
        if (Time.time < medoMinimoAte) fear = Mathf.Max(fear, medoMinimo);

        if (!InPanic && fear > 0.85f) { InPanic = true;  onPanicStart.Invoke(); }
        if (InPanic  && fear < 0.60f) { InPanic = false; onPanicEnd.Invoke(); }

        UpdateFeedback();
    }

    void HandleHug()
    {
        var mv = state.Movement;
        bool wants = Input.GetKey(hugKey) && state.IsFree && !state.isGrabbing;
        if (requireStanding) wants &= state.Controller == null || state.Controller.isGrounded;

        if (wants && !state.isHuggingBear)
        {
            state.isHuggingBear = true;
            mv.canMove = false;
        }
        else if (!wants && state.isHuggingBear)
        {
            state.isHuggingBear = false;
            mv.canMove = true;
        }
    }

    void UpdateFeedback()
    {
        if (vignette == null && volume != null && volume.profile != null) volume.profile.TryGet(out vignette);
        if (vignette != null)
            vignette.intensity.Override(Mathf.Clamp01(Mathf.Lerp(vignetteCalm, vignettePanic, fear) + vinhetaExtra));
        vinhetaExtra = Mathf.MoveTowards(vinhetaExtra, 0f, Time.deltaTime * 0.6f);

        if (heartbeat != null)
        {
            heartbeat.volume = Mathf.InverseLerp(0.3f, 1f, fear);
            heartbeat.pitch  = Mathf.Lerp(0.9f, 1.4f, fear);
        }
        if (breathing != null)
        {
            breathing.volume = Mathf.Lerp(0.1f, 0.8f, fear);
            breathing.pitch = Mathf.Lerp(0.95f, 1.3f, fear) + (Assustada ? 0.15f : 0f);   // acelera com o medo
        }
    }

    /// <summary>Susto: a vinheta fecha de repente e volta devagar.</summary>
    public void PulsoDeVinheta(float quanto) => vinhetaExtra = Mathf.Max(vinhetaExtra, quanto);

    /// <summary>Depois de um susto: o medo fica pelo menos em "minimo" por alguns segundos (respiração acelerada).</summary>
    public void SegurarMedo(float minimo, float segundos)
    {
        medoMinimo = Mathf.Clamp01(minimo);
        medoMinimoAte = Mathf.Max(medoMinimoAte, Time.time + segundos);
        fear = Mathf.Max(fear, medoMinimo);
    }

    public void AddFear(float amount) => fear = Mathf.Clamp01(fear + amount);
    public void ResetFear() { fear = 0f; InPanic = false; medoMinimoAte = 0f; vinhetaExtra = 0f; }
}
