using UnityEngine;

/// <summary>
/// Porta por onde a Inspetora da ronda entra e sai deste cômodo, ligada a um cômodo vizinho.
/// Daqui também vem o som dela quando está no vizinho (passos, galhos raspando).
/// A posição do objeto é onde ela aparece (no chão, logo na frente da porta).
/// Criada pelo menu "Umbra > Terror > Preparar ronda na cena aberta" (uma por porta para um vizinho).
/// </summary>
public class PortaDaRonda : MonoBehaviour
{
    [Tooltip("Cena do outro lado desta porta.")]
    public string comodoVizinho;
    [Tooltip("Luzes que tremem no aviso antes de ela entrar por aqui.")]
    public LightZone[] luzesQueTremem;
    [Tooltip("Caminho da sombra dela na parede do fundo antes de aparecer (início e fim). Vazio = sem sombra.")]
    public Transform sombraInicio, sombraFim;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.1f, new Vector3(1f, 2.2f, 0.1f));
        if (sombraInicio != null && sombraFim != null)
        {
            Gizmos.color = new Color(0f, 0f, 0f, 0.8f);
            Gizmos.DrawLine(sombraInicio.position, sombraFim.position);
            Gizmos.DrawWireSphere(sombraFim.position, 0.15f);
        }
    }
}
