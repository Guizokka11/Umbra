using UnityEngine;

/// <summary>
/// Papel de uma camada do PSD no cenário 3D.
/// Editado pela janela "Umbra > Cenário > Montar cenário do PSD" (e preenchido sozinho pelos montadores de cena).
/// Guarda também o estado original da camada, para o cenário poder ser remontado quantas vezes for preciso.
/// </summary>
[DisallowMultipleComponent]
public class PsdLayerDepth : MonoBehaviour
{
    public enum Role
    {
        Fundo,          // papel, parede, janela: vai para a pintura 3D (parede)
        DetalheParede,  // cortina, quadro, porta pintada: vai para a pintura 3D (parede)
        MovelFundo,     // móvel encostado no fundo (camas do fundo): a Luma sobe e fica SEMPRE na frente
        Movel,          // móvel solto no meio do cômodo: dá para passar na frente e atrás
        Frente,         // primeiro plano, cobre a Luma (camas da frente)
        Efeito,         // luz, brilho (plano solto, não vira pintura)
        Chao,           // chão, tapete, rachadura: vai para a pintura 3D (chão)
        Ignorar,        // some (ex.: "mc", a Luma pintada)
        PlanoParede,    // sprite solto colado na parede (ex.: a Inspetora pintada, que vira criatura)
    }

    public Role role = Role.Movel;
    [Tooltip("Tem caixa de colisão (dá para esbarrar e subir em cima).")]
    public bool solid;
    [Tooltip("Altura da caixa em fração da altura do móvel (beliche ~0,2 = colchão de baixo).")]
    [Range(0.05f, 1f)] public float climb = 1f;
    [Tooltip("Fica na mesma profundidade desta outra camada (ex.: vaso em cima da mesa).")]
    public string onLayer;
    [Tooltip("Beliche: uma caixa para a cama de baixo e outra para a cama de cima.")]
    public bool beliche;
    [Tooltip("Beliche: altura da caixa da cama de baixo (fração da altura do móvel, a partir do chão; ~0,2 = beira do colchão).")]
    [Range(0.05f, 1f)] public float camaDeBaixo = 0.2f;
    [Tooltip("Beliche: faixa da caixa da cama de cima (fração da altura do móvel: de baixo do estrado até a beira do colchão).")]
    public Vector2 camaDeCima = new Vector2(0.6f, 0.72f);
    [Tooltip("A caixa vai da frente do móvel até a parede do fundo (divisórias de cabine: não dá para passar por trás).")]
    public bool ateParede;
    [Tooltip("Usar o retângulo abaixo (frações da pintura) em vez da parte desenhada medida.")]
    public bool usarRetangulo;
    public Rect retangulo;

    // legado (versão plana antiga)
    [HideInInspector] public float depth = 0f;
    [HideInInspector] public bool customDepth = false;

    [HideInInspector] public bool hasOriginal;
    [HideInInspector] public Vector3 originalLocalPosition;
    [HideInInspector] public Quaternion originalLocalRotation;
    [HideInInspector] public Vector3 originalLocalScale;
    [HideInInspector] public bool hasOrder;
    [HideInInspector] public int originalSortingOrder;
    [HideInInspector] public Material originalMaterial;
    [HideInInspector] public Color originalColor = Color.white;
    [HideInInspector] public bool originalActive = true;
    [Tooltip("Nome da camada no PSD (continua valendo se você renomear o objeto na cena).")]
    public string nomeNoPsd;

    public void StoreOriginal()
    {
        if (string.IsNullOrEmpty(nomeNoPsd)) nomeNoPsd = gameObject.name.Trim();
        if (hasOriginal) return;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        originalActive = gameObject.activeSelf;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            originalSortingOrder = sr.sortingOrder; hasOrder = true;
            originalMaterial = sr.sharedMaterial;
            originalColor = sr.color;
        }
        hasOriginal = true;
    }

    public void RestoreOriginal()
    {
        if (!hasOriginal) return;
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
        gameObject.SetActive(originalActive);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (hasOrder) sr.sortingOrder = originalSortingOrder;
            if (originalMaterial != null) sr.sharedMaterial = originalMaterial;
            sr.color = originalColor;
        }
    }
}
