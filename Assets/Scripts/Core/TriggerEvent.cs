using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gatilho genérico: quando a Luma entra/sai da área, dispara eventos.
/// Serve para roteiro sem programar: começar perseguição, travar a profundidade
/// na varanda, iniciar o confronto final, apagar luzes, tocar um som.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerEvent : MonoBehaviour
{
    public bool onlyOnce = true;
    [Tooltip("Segundos de espera entre a Luma entrar e o evento disparar.")]
    public float delay = 0f;

    public UnityEvent onEnter;
    public UnityEvent onExit;

    bool fired;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || (onlyOnce && fired)) return;
        fired = true;
        if (delay > 0f) Invoke(nameof(FireEnter), delay);
        else FireEnter();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) onExit.Invoke();
    }

    void FireEnter() => onEnter.Invoke();

    public void ResetTrigger() => fired = false;

    void OnDrawGizmos()
    {
        var c = GetComponent<BoxCollider>();
        if (c == null) return;
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(c.center, c.size);
    }
}
