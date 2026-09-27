using UnityEngine;

/// <summary>
/// Conteúdo de uma pista (bilhete, desenho, registro, objeto).
/// Criar em: botão direito no Project > Create > Umbra > Pista.
///
/// VERDADEIRA = deixada pela Amelie (tem o pequeno sol desenhado).
/// FALSA      = forjada pelo Diretor (imita a letra da Amelie, sem o sol).
/// Ver GDD, seção "PISTAS VERDADEIRAS E PISTAS FALSAS".
/// </summary>
[CreateAssetMenu(menuName = "Umbra/Pista", fileName = "Pista_")]
public class ClueData : ScriptableObject
{
    public string id;
    public string title;
    [TextArea(3, 12)] public string text;
    public Sprite image;

    public enum Origin { Amelie, Falsa, Orfanato }
    [Tooltip("Amelie = verdadeira (mostra o sol). Falsa = forjada pelo Diretor. Orfanato = fichas e registros.")]
    public Origin origin = Origin.Amelie;

    [Tooltip("Arco em que aparece (1, 2 ou 3). Só para organização.")]
    [Range(0, 3)] public int arc = 1;

    public bool ShowsSun => origin == Origin.Amelie;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
}
