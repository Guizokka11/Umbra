using UnityEngine;

/// <summary>Tipo de chão, para o som dos passos da Luma.</summary>
public enum Superficie { Madeira, Azulejo }

/// <summary>
/// Opcional: coloque num colisor de chão (ou numa área-gatilho) para mudar o som dos passos ali.
/// Ex.: o piso de azulejo de um banheiro dentro de uma cena de madeira.
/// Sem isto, vale a "superfície padrão" do AmbienteDoComodo da cena.
/// </summary>
public class SuperficieDoChao : MonoBehaviour
{
    public Superficie superficie = Superficie.Azulejo;

    /// <summary>Áreas-gatilho em que a Luma está agora (a última que entrou vale).</summary>
    public static SuperficieDoChao AreaAtual { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        var c = GetComponent<Collider>();
        if (c != null && c.isTrigger && other.CompareTag("Player")) AreaAtual = this;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && AreaAtual == this) AreaAtual = null;
    }

    void OnDisable() { if (AreaAtual == this) AreaAtual = null; }
}
