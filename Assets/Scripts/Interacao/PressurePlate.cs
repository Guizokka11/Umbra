using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Placa de pressão / contrapeso. Conta quantos objetos Pushable (e, se quiser,
/// a Luma) estão dentro do trigger. Ao atingir "required", dispara onActivated.
/// Ex.: contrapeso do monta-cargas (2 sacos de farinha).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    public int  required = 1;
    public bool countPlayer = false;
    [Tooltip("Depois de ativada, fica ativada mesmo que tirem os objetos.")]
    public bool latch = true;

    public UnityEvent onActivated;
    public UnityEvent onDeactivated;

    public bool IsActive { get; private set; }
    public int  Count => inside.Count;

    readonly HashSet<GameObject> inside = new HashSet<GameObject>();

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        var go = Relevant(other);
        if (go != null && inside.Add(go)) Evaluate();
    }

    void OnTriggerExit(Collider other)
    {
        var go = Relevant(other);
        if (go != null && inside.Remove(go)) Evaluate();
    }

    GameObject Relevant(Collider other)
    {
        var p = other.GetComponentInParent<Pushable>();
        if (p != null) return p.gameObject;
        if (countPlayer && other.CompareTag("Player")) return other.gameObject;
        return null;
    }

    void Evaluate()
    {
        bool now = inside.Count >= required;
        if (now && !IsActive) { IsActive = true; onActivated.Invoke(); }
        else if (!now && IsActive && !latch) { IsActive = false; onDeactivated.Invoke(); }
    }
}
