using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A INSPETORA (nictofobia): massa de galhos e cabelos que ocupa o escuro.
/// Anda só no eixo X, com a "frente" virada para a Luma. Não entra na luz.
///
/// Modos:
///  - Espreitar: fica no ninho; se a Luma chega perto no escuro, avança devagar.
///    Quando a frente está iluminada (LightZone acesa), recua.
///  - Perseguir: avança rápido na direção da Luma até pegá-la. Só a luz para.
///
/// Se a Luma fica "dentro" da massa (passou da frente) e não está na luz: captura.
/// Colocar no objeto raiz da criatura; as camadas pintadas ficam como filhas.
/// </summary>
public class InspetoraMass : MonoBehaviour
{
    public enum Mode { Espreitar, Perseguir, Dormir }

    public Mode mode = Mode.Espreitar;
    [Tooltip("-1 = avança para a esquerda (criatura à direita da Luma).")]
    public int direction = -1;
    [Tooltip("Ponto que marca a borda da frente da massa (filho).")]
    public Transform front;

    [Header("Movimento")]
    public float creepSpeed   = 0.9f;
    public float chaseSpeed   = 3.1f;
    public float retreatSpeed = 2.5f;
    [Tooltip("Distância (da frente até a Luma) em que ela começa a avançar no modo Espreitar.")]
    public float senseDistance = 4.5f;
    [Tooltip("Quanto a raiz pode sair do ninho no modo Espreitar.")]
    public float maxAdvance = 6f;
    [Tooltip("Quanto ela pode recuar além do ninho quando a luz a empurra.")]
    public float maxRetreat = 4f;
    [Tooltip("Espera antes de começar a perseguir (dá tempo de correr).")]
    public float chaseDelay = 0.8f;

    [Header("Captura")]
    public float catchPadding = 0.15f;

    [Header("Visual")]
    public Transform[] wobbleParts;
    public float wobbleAmount = 0.04f;

    public UnityEvent onStartChase;
    public UnityEvent onCatch;

    float homeX, startX;
    Mode startMode;
    float chaseTimer;
    Vector3[] baseScales;

    public float FrontX => front != null ? front.position.x : transform.position.x;

    void Awake()
    {
        homeX = startX = transform.position.x;
        startMode = mode;
        if (wobbleParts != null)
        {
            baseScales = new Vector3[wobbleParts.Length];
            for (int i = 0; i < wobbleParts.Length; i++) if (wobbleParts[i] != null) baseScales[i] = wobbleParts[i].localScale;
        }
    }

    void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.onRespawn.AddListener(ResetMass);
    }

    public void StartChase()
    {
        if (mode == Mode.Perseguir) return;
        mode = Mode.Perseguir;
        chaseTimer = 0f;
        onStartChase?.Invoke();
    }

    public void Sleep() => mode = Mode.Dormir;
    public void Lurk()  => mode = Mode.Espreitar;

    public void ResetMass()
    {
        var p = transform.position; p.x = startX; transform.position = p;
        mode = startMode;
        chaseTimer = 0f;
    }

    void Update()
    {
        Wobble();
        var st = PlayerState.Instance;
        if (st == null || st.isDead || mode == Mode.Dormir) return;
        // Trocando de cena / cutscene (e não abraçando o urso): não ataca.
        if (!st.Movement.canMove && !st.isHuggingBear) return;

        float px = st.transform.position.x;
        float gap = (px - FrontX) * direction;        // >0: Luma à frente da massa (a salvo)
        bool frontLit = LightZone.IsPointLit(new Vector3(FrontX + direction * 0.3f, 1f, st.transform.position.z));

        float move = 0f;                               // >0 = avançar
        if (mode == Mode.Perseguir)
        {
            chaseTimer += Time.deltaTime;
            if (chaseTimer >= chaseDelay) move = frontLit ? -retreatSpeed * 0.3f : chaseSpeed;
        }
        else
        {
            float advanced = (transform.position.x - homeX) * direction;
            if (frontLit) move = advanced > -maxRetreat ? -retreatSpeed : 0f;
            else if (gap < senseDistance && !st.IsInLight && advanced < maxAdvance) move = creepSpeed;
            else if (Mathf.Abs(advanced) > 0.05f) move = -Mathf.Sign(advanced) * creepSpeed * 0.6f;
        }

        transform.position += Vector3.right * direction * move * Time.deltaTime;

        // Medo sobe perto dela.
        if (FearSystem.Instance != null && gap < 5f)
            FearSystem.Instance.AddFear(Mathf.Clamp01(1f - gap / 5f) * 0.3f * Time.deltaTime);

        // Captura: a Luma está dentro da massa, fora da luz, e não escondida.
        if (gap < catchPadding && !st.IsInLight && !st.isHidden)
        {
            onCatch?.Invoke();
            if (GameManager.Instance != null) GameManager.Instance.PlayerCaught(null);
        }
    }

    void Wobble()
    {
        if (wobbleParts == null || baseScales == null) return;
        for (int i = 0; i < wobbleParts.Length; i++)
        {
            var t = wobbleParts[i];
            if (t == null) continue;
            float k = 1f + Mathf.Sin(Time.time * (1.3f + i * 0.37f) + i) * wobbleAmount;
            t.localScale = new Vector3(baseScales[i].x * k, baseScales[i].y * (2f - k), baseScales[i].z);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0f, 0.3f, 0.8f);
        Vector3 f = new Vector3(FrontX, 0f, transform.position.z);
        Gizmos.DrawLine(f, f + Vector3.up * 3f);
        Gizmos.DrawLine(f + Vector3.up * 1.5f, f + Vector3.up * 1.5f + Vector3.right * direction * senseDistance);
    }
}
