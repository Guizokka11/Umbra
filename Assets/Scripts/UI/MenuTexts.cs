using UnityEngine;

/// <summary>
/// Textos da tela inicial. Para mudar: clique em Assets/Dados/Menu/TextosDoMenu
/// e edite no Inspector (título, frase, botões, controles). Não precisa programar.
/// </summary>
[CreateAssetMenu(menuName = "Umbra/Textos do menu", fileName = "TextosDoMenu")]
public class MenuTexts : ScriptableObject
{
    public string titulo = "UMBRA";
    [TextArea] public string subtitulo = "um jogo sobre o medo de ficar sozinha";
    public string botaoNovoJogo = "Novo jogo";
    public string botaoContinuar = "Continuar";
    public string botaoSair = "Sair";
    [TextArea(2, 4)] public string controles =
        "A/D: andar   W/S: fundo/frente   Shift: correr   Espaço: pular / subir   E: interagir   F: abraçar o urso   Esc: pausa\n" +
        "A luz é segura. O escuro não é.";
    [TextArea] public string creditos = "";
    public Color corTitulo = new Color(0.9f, 0.86f, 0.9f);
}
