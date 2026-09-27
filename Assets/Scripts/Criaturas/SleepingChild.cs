using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Criança dormindo no dormitório. Se a Luma faz barulho perto (corre, pula),
/// ela se mexe e resmunga — e o Diretor, que ouve tudo, vem ver.
/// Primeiro aviso: só se mexe. Segundo: acorda de vez e chama o Diretor.
/// </summary>
public class SleepingChild : MonoBehaviour
{
    public float wakeRadius = 1.6f;
    [Tooltip("Raio do barulho que ela faz ao acordar (chama criaturas).")]
    public float alertRadius = 30f;
    public Transform visual;
    public float cooldown = 4f;

    public UnityEvent onStir;
    public UnityEvent onWake;

    int strikes;
    float lastTime = -99f;
    Vector3 baseScale;

    void Awake() { if (visual != null) baseScale = visual.localScale; }

    void Update()
    {
        if (visual != null)
        {
            float k = 1f + Mathf.Sin(Time.time * 1.1f + transform.position.x) * 0.02f; // respiração
            visual.localScale = new Vector3(baseScale.x, baseScale.y * k, baseScale.z);
        }

        var st = PlayerState.Instance;
        if (st == null || Time.time - lastTime < cooldown) return;
        float noise = st.NoiseRadius;
        if (noise <= 0f) return;
        float d = Vector3.Distance(st.transform.position, transform.position);
        // Andar devagar ao lado não acorda; correr/pular, sim.
        if (d > wakeRadius || noise <= st.walkNoise) return;

        lastTime = Time.time;
        strikes++;
        if (strikes == 1)
        {
            Hud.Subtitle("(uma criança se mexe na cama...)", 2f);
            onStir?.Invoke();
        }
        else
        {
            Hud.Subtitle("— Quem tá aí?!", 2f);
            foreach (var c in CreatureAI.All) c.HearNoiseAt(transform.position, alertRadius);
            onWake?.Invoke();
            strikes = 0;
        }
    }
}
