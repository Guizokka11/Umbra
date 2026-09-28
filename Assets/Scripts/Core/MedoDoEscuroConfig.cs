using UnityEngine;

/// <summary>
/// Configuração do "escuro perigoso" (Arco 1, nictofobia). Asset em Assets/Dados/Resources/MedoDoEscuro.asset,
/// criado pelo menu "Umbra > Terror > Medo do escuro: preparar na cena aberta". Uma cena pode desligar ou usar
/// outra configuração com o componente EscuroNaCena. Os sons ficam vazios: a equipe preenche.
/// </summary>
[CreateAssetMenu(menuName = "Umbra/Medo do escuro (configuração)", fileName = "MedoDoEscuro")]
public class MedoDoEscuroConfig : ScriptableObject
{
    [Header("Onde vale")]
    [Tooltip("Cenas em que o escuro é perigoso (sem precisar de EscuroNaCena). Arco 1.")]
    public string[] cenas = { "01_Dormitorio1", "02_Corredor1", "03_Banheiro", "06_Dormitorio2", "08_Biblioteca", "09_LazyRoom", "10_LivingRoom" };

    [Header("Escuro")]
    [Tooltip("Segundos fora de qualquer luz até o escuro começar a pesar.")]
    public float atraso = 4f;
    [Tooltip("Segundos (depois do atraso) até o escuro chegar no máximo.")]
    public float tempoAteOMaximo = 10f;
    [Tooltip("Quanto a intensidade cai por segundo na luz (0.1 = 10 s para sumir).")]
    public float alivioPorSegundo = 0.1f;
    [Tooltip("Medo extra por segundo no escuro, no máximo (soma ao que o FearSystem já faz).")]
    public float medoExtraPorSegundo = 0.06f;
    [Tooltip("Na luz, o medo cai só isto por segundo (alívio lento). 0 = usa o valor do FearSystem da cena.")]
    public float quedaDoMedoNaLuz = 0.035f;

    [Header("Efeitos por cima da tela")]
    [Tooltip("Quanto a vinheta fecha a mais no máximo.")]
    [Range(0f, 1f)] public float vinhetaExtra = 0.25f;
    [Tooltip("Quanto o véu de escuridão (DarknessOverlay) fecha a mais no máximo.")]
    [Range(0f, 1f)] public float veuExtra = 0.12f;

    [Header("Mãos de sombra")]
    [Tooltip("Desenhos das mãos (pretos, pulso embaixo, dedos para cima). Vazio = mãos provisórias geradas pelo jogo.")]
    public Sprite[] maos;
    [Range(0, 10)] public int quantidadeDeMaos = 6;
    [Tooltip("Tamanho de cada mão (fração da altura da tela).")]
    [Range(0.1f, 1f)] public float tamanhoDaMao = 0.5f;
    [Tooltip("Quanto as mãos entram na tela no máximo (fração do tamanho).")]
    [Range(0f, 1f)] public float quantoEntram = 0.55f;
    [Range(0f, 1f)] public float opacidadeDasMaos = 0.9f;

    [Header("Sussurros")]
    [Tooltip("Loop de sussurros que sobe com o escuro.")]
    public AudioClip sussurrosLoop;
    [Tooltip("Sussurros soltos, de um lado ou do outro.")]
    public AudioClip[] sussurrosSoltos;
    [Range(0f, 1f)] public float volumeDosSussurros = 0.6f;
    public Vector2 intervaloDosSoltos = new Vector2(3f, 7f);

    [Header("Pânico (medo no máximo no escuro)")]
    [Range(0f, 1f)] public float medoDoPanico = 0.95f;
    [Tooltip("O pânico acaba quando o medo cai abaixo disto (ou ela sai do escuro).")]
    [Range(0f, 1f)] public float medoParaSairDoPanico = 0.8f;
    [Tooltip("Velocidade no pânico (1 = normal). Menos que 1 também impede correr.")]
    [Range(0.2f, 1f)] public float velocidadeNoPanico = 0.6f;
    [Tooltip("Raio do barulho que ela faz no pânico (m).")]
    public float raioDoBarulho = 9f;
    [Tooltip("Segundos entre um barulho e outro no pânico.")]
    public float intervaloDoBarulho = 2.5f;
    [Tooltip("Choro/soluço da Luma no pânico (sorteado a cada barulho).")]
    public AudioClip[] choro;
    [Tooltip("No pânico, a Inspetora da ronda vem para este cômodo (se estiver no andar).")]
    public bool chamaARonda = true;
    [Tooltip("Espera mínima entre dois chamados da ronda (s).")]
    public float intervaloEntreChamados = 45f;
}
