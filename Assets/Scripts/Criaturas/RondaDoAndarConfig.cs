using UnityEngine;

/// <summary>
/// Configuração da ronda da Inspetora no 2º andar (asset em Assets/Dados/Resources/RondaDoAndar.asset,
/// criado pelo menu "Umbra > Terror > Preparar ronda na cena aberta"). Os sons ficam vazios: a equipe preenche.
/// </summary>
[CreateAssetMenu(menuName = "Umbra/Ronda do andar (configuração)", fileName = "RondaDoAndar")]
public class RondaDoAndarConfig : ScriptableObject
{
    [System.Serializable]
    public class Comodo
    {
        [Tooltip("Nome da cena do cômodo.")]
        public string cena;
        [Tooltip("Cômodos com porta para este (ela só anda entre vizinhos).")]
        public string[] vizinhos;
    }

    [Header("Cômodos (seguem a planta do 2º andar)")]
    public Comodo[] comodos =
    {
        new Comodo { cena = "02_Corredor1",   vizinhos = new[] { "03_Banheiro", "06_Dormitorio2" } },
        new Comodo { cena = "03_Banheiro",    vizinhos = new[] { "02_Corredor1" } },
        new Comodo { cena = "06_Dormitorio2", vizinhos = new[] { "02_Corredor1" } },
    };
    [Tooltip("Onde ela está quando a ronda começa.")]
    public string comodoInicial = "06_Dormitorio2";

    [Header("Quando")]
    [Tooltip("A ronda só começa depois deste flag (fim do encontro do banheiro).")]
    public string flagDeInicio = "banheiro_encontro";
    [Tooltip("Com qualquer um destes flags ligado, a ronda pausa (ex.: a fuga do LazyRoom, que tem a própria Inspetora).")]
    public string[] flagsQuePausam = { "fuga_inspetora" };
    [Tooltip("Segundos entre uma troca de cômodo e outra (mínimo, máximo).")]
    public Vector2 intervaloDeTroca = new Vector2(40f, 90f);

    [Header("Aviso antes de entrar")]
    [Tooltip("Segundos de aviso (passos se aproximando, luz tremendo) antes de ela aparecer na porta.")]
    public Vector2 tempoDeAviso = new Vector2(5f, 8f);
    [Tooltip("A sombra passa pela parede do fundo este tanto antes de ela aparecer.")]
    public float sombraAntes = 2.5f;
    public float duracaoDaSombra = 1.8f;
    [Tooltip("A porta range este tanto antes de ela aparecer.")]
    public float portaAntes = 1.2f;

    [Header("Farejar esconderijos")]
    [Tooltip("Passando a esta distância de um esconderijo ocupado, ela para e fareja.")]
    public float raioDoFaro = 2.6f;
    public Vector2 tempoFarejando = new Vector2(3f, 5f);
    [Tooltip("Chance de farejar um esconderijo VAZIO ao passar perto (engana o jogador).")]
    [Range(0f, 1f)] public float chanceEsconderijoVazio = 0.25f;
    [Tooltip("Medo que sobe na Luma escondida quando ela fareja o esconderijo.")]
    public float medoAoFarejar = 0.2f;

    [Header("Sons (vazios: ver Assets/Audio/LEIAME.md)")]
    [Tooltip("Passos dela no cômodo vizinho / chegando (um sorteado por passo).")]
    public AudioClip[] passos;
    [Tooltip("Loop de galhos e cabelo raspando, ouvido pela parede.")]
    public AudioClip galhosRaspando;
    [Tooltip("Porta rangendo quando ela está para entrar ou sair.")]
    public AudioClip portaRangendo;
    [Tooltip("Farejando na frente de um esconderijo.")]
    public AudioClip farejar;
    [Range(0f, 1f)] public float volumeNoVizinho = 0.45f;
    [Tooltip("Distância (m) da Luma até a porta do vizinho em que o som some.")]
    public float alcanceDoSomVizinho = 14f;
}
