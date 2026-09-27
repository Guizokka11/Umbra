using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// "Faça N coisas para resolver." Cada ação chama Add(), ou AddFrom(objeto) para contar
/// cada objeto só uma vez (ex.: apertar o mesmo chuveiro duas vezes não conta dobrado).
/// Ex.: Banheiro — ligar os 2 chuveiros embaça o espelho por 12 segundos.
///   required = 2, activeTime = 12, onSolved: ativa o vapor e a pista;
///   onExpired: desativa o vapor e a pista, e os chuveiros podem ser ligados de novo.
/// </summary>
public class PuzzleCounter : MonoBehaviour
{
    public int required = 2;
    [Tooltip("0 = fica resolvido para sempre. >0 = segundos até expirar e reiniciar.")]
    public float activeTime = 0f;

    public UnityEvent onSolved;
    public UnityEvent onExpired;

    public bool Solved { get; private set; }

    int count;
    float solvedAt;
    readonly HashSet<GameObject> sources = new HashSet<GameObject>();

    public void AddFrom(GameObject source)
    {
        if (Solved || source == null || !sources.Add(source)) return;
        Add();
    }

    public void Add()
    {
        if (Solved) return;
        count++;
        if (count >= required) Solve();
    }

    public void Remove()
    {
        if (Solved) return;
        count = Mathf.Max(0, count - 1);
    }

    void Solve()
    {
        Solved = true;
        solvedAt = Time.time;
        onSolved.Invoke();
    }

    void Update()
    {
        if (Solved && activeTime > 0f && Time.time - solvedAt >= activeTime)
        {
            Solved = false;
            count = 0;
            sources.Clear();
            onExpired.Invoke();
        }
    }

    public void ResetCounter() { Solved = false; count = 0; sources.Clear(); }
}
