using UnityEngine;

/// <summary>
/// Móvel do fundo (cama, mesa) desenhado sem profundidade: normalmente fica atrás da Luma
/// (ela sobe e anda em cima dele sem ser cortada). Quando a Luma passa POR TRÁS dele
/// (z maior que a parte de trás da caixa), ele passa a ser desenhado na frente dela.
/// Colocado pelo montador de cenário (Umbra > Cenário > Montar cenário do PSD).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class OrdemAtras : MonoBehaviour
{
    [Tooltip("z da parte de trás do móvel: a Luma com z maior que isto está atrás dele.")]
    public float zAtras;
    [Tooltip("Ordem quando a Luma está na frente ou em cima (atrás dela; a Luma usa 100).")]
    public int ordemAtras = -100;
    [Tooltip("Ordem quando a Luma está atrás (na frente dela; criaturas usam 150+).")]
    public int ordemFrente = 110;

    SpriteRenderer sr;
    Transform luma;

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void LateUpdate()
    {
        if (luma == null)
        {
            if (PlayerState.Instance != null) luma = PlayerState.Instance.transform;
            else { var p = GameObject.FindWithTag("Player"); if (p != null) luma = p.transform; }
            if (luma == null) return;
        }
        int o = luma.position.z > zAtras ? ordemFrente : ordemAtras;
        if (sr.sortingOrder != o) sr.sortingOrder = o;
    }
}
