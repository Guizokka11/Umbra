using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Faz barulho num ponto. As criaturas que ouvirem vão investigar.
/// Usos:
///  - Distração: ligar Emit() no onInteract de um SimpleInteractable
///    (derrubar pilha de papéis, jogar um objeto, bater num cano).
///  - Tábua que range: marcar "emitOnPlayerEnter" e colocar um Collider trigger.
///    Andando devagar não faz barulho (se "onlyWhenRunning" estiver ligado).
/// </summary>
public class NoiseEmitter : MonoBehaviour
{
    public float radius = 8f;
    public bool emitOnPlayerEnter = false;
    [Tooltip("Tábua que range: só faz barulho se a Luma estiver correndo.")]
    public bool onlyWhenRunning = false;
    public float cooldown = 1f;
    public AudioSource sfx;

    public UnityEvent onEmit;

    float lastEmit = -99f;

    public void Emit()
    {
        if (Time.time - lastEmit < cooldown) return;
        lastEmit = Time.time;

        if (sfx != null) sfx.Play();
        foreach (var c in CreatureAI.All) c.HearNoiseAt(transform.position, radius);
        onEmit.Invoke();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!emitOnPlayerEnter || !other.CompareTag("Player")) return;
        var st = PlayerState.Instance;
        if (onlyWhenRunning && (st == null || !st.Movement.isRunning)) return;
        Emit();
    }

    void OnTriggerStay(Collider other)
    {
        // Tábua que range: se começar a correr já em cima dela.
        if (emitOnPlayerEnter && onlyWhenRunning && other.CompareTag("Player"))
        {
            var st = PlayerState.Instance;
            if (st != null && st.Movement.isRunning && st.Movement.isMoving) Emit();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
