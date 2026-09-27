using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escalada automática (como em Little Nightmares): encostada num objeto baixo
/// (baú, caixa, mesa, cama de cima do beliche), apertar Pular faz a Luma subir nele em vez de pular no lugar.
/// Colocar no objeto da Luma.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ClimbAssist : MonoBehaviour
{
    [Tooltip("Altura máxima que ela consegue escalar.")]
    public float maxHeight = 1.15f;
    public float minHeight = 0.35f;
    [Tooltip("Distância à frente em que procura algo para subir.")]
    public float reach = 0.45f;
    public float duration = 0.4f;

    CharacterController cc;
    PlayerMovement mv;
    PlayerState st;
    bool climbing;

    public bool IsClimbing => climbing;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        mv = GetComponent<PlayerMovement>();
        st = GetComponent<PlayerState>();
    }

    void Update()
    {
        if (climbing || mv == null || !mv.canMove) return;
        if (st != null && (!st.IsFree || st.isGrabbing)) return;
        if (!Input.GetButtonDown("Jump")) return;

        Vector3 dir = mv.facingRight ? Vector3.right : Vector3.left;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(v) > 0.1f && Mathf.Abs(v) >= Mathf.Abs(h)) dir = v > 0 ? Vector3.forward : Vector3.back;   // subir numa cama à frente/atrás
        else if (Mathf.Abs(h) > 0.1f) dir = h > 0 ? Vector3.right : Vector3.left;

        if (Procurar(dir, out Vector3 alvo)) StartCoroutine(Climb(alvo));
    }

    /// <summary>Tem algo escalável logo à frente (para o lado que ela olha)? Usado pelas dicas de controle.</summary>
    public bool TemOndeSubir()
    {
        if (climbing || mv == null || !mv.canMove || cc == null || !cc.enabled) return false;
        if (st != null && (!st.IsFree || st.isGrabbing)) return false;
        return Procurar(mv.facingRight ? Vector3.right : Vector3.left, out _);
    }

    /// <summary>Acha onde a Luma pararia em cima de algo escalável na direção dir.</summary>
    bool Procurar(Vector3 dir, out Vector3 alvo)
    {
        alvo = default;
        Vector3 feet = transform.position;
        float r = cc.radius;
        // Procura à frente tudo que tem o topo na altura escalável (caixa larga em Z: pega objetos um pouco
        // atrás ou à frente; alta: pega a cama de cima do beliche, que não encosta no chão).
        float hy = (maxHeight + 0.05f) * 0.5f;
        Vector3 center = feet + dir * (r + reach * 0.5f) + Vector3.up * (0.05f + hy);
        Vector3 half = Mathf.Abs(dir.z) > 0.5f ? new Vector3(0.9f, hy, reach * 0.5f) : new Vector3(reach * 0.5f, hy, 0.9f);
        var hits = Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        var cands = new List<Collider>();
        foreach (var col in hits)
        {
            if (col.transform.root == transform.root) continue;
            float ht = col.bounds.max.y - feet.y;
            if (ht < minHeight || ht > maxHeight) continue;
            if (col.bounds.size.x > 6f) continue;                  // paredes/chão: não
            // Subindo para o fundo/frente: só o que está bem na frente dela (não a estante ao lado).
            if (Mathf.Abs(dir.z) > 0.5f && (feet.x < col.bounds.min.x - r || feet.x > col.bounds.max.x + r)) continue;
            cands.Add(col);
        }
        // Do mais baixo para o mais alto: sobe no primeiro que tem espaço em cima
        // (beliche: a cama de baixo não tem espaço, a de cima tem).
        cands.Sort((x, y) => x.bounds.max.y.CompareTo(y.bounds.max.y));
        foreach (var best in cands)
        {
            Bounds bb = best.bounds;
            float mx = Mathf.Min(0.15f, bb.extents.x), mz = Mathf.Min(r + 0.02f, bb.extents.z);
            Vector3 target = new Vector3(
                Mathf.Clamp(feet.x + dir.x * (r + 0.25f), bb.min.x + mx, bb.max.x - mx),
                bb.max.y + 0.03f,
                Mathf.Clamp(feet.z + dir.z * (r + 0.25f), bb.min.z + mz, bb.max.z - mz));
            if (!Room(target, best, r)) continue;
            alvo = target;
            return true;
        }
        return false;
    }

    /// <summary>Espaço para a Luma em pé no ponto (nada além do próprio apoio no caminho).</summary>
    bool Room(Vector3 target, Collider support, float r)
    {
        Vector3 a = target + Vector3.up * (r + 0.05f);
        Vector3 b = target + Vector3.up * Mathf.Max(cc.height - r, r + 0.06f);
        foreach (var col in Physics.OverlapCapsule(a, b, r * 0.8f, ~0, QueryTriggerInteraction.Ignore))
            if (col.transform.root != transform.root && col != support) return false;
        return true;
    }

    IEnumerator Climb(Vector3 target)
    {
        climbing = true;
        mv.canMove = false;
        cc.enabled = false;
        Vector3 start = transform.position;
        Vector3 up = new Vector3(start.x, target.y + 0.1f, Mathf.Lerp(start.z, target.z, 0.5f));
        float halfT = duration * 0.5f;
        for (float t = 0; t < halfT; t += Time.deltaTime) { transform.position = Vector3.Lerp(start, up, t / halfT); yield return null; }
        for (float t = 0; t < halfT; t += Time.deltaTime) { transform.position = Vector3.Lerp(up, target, t / halfT); yield return null; }
        cc.enabled = true;
        mv.Teleport(target);        // zera a velocidade vertical do pulo
        mv.canMove = true;
        climbing = false;
    }
}
