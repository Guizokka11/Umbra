using UnityEngine;

/// <summary>
/// Liga, desliga ou ajusta o "escuro perigoso" (MedoDoEscuro) só nesta cena.
/// Sem este componente, vale a lista de cenas da configuração global.
/// </summary>
public class EscuroNaCena : MonoBehaviour
{
    [Tooltip("Desliga o escuro perigoso nesta cena.")]
    public bool desligado;
    [Tooltip("Configuração só desta cena (vazio = a global, Assets/Dados/Resources/MedoDoEscuro.asset).")]
    public MedoDoEscuroConfig configuracao;
}
