using UnityEngine;

/// <summary>
/// Sons do cômodo (um por cena). O AudioManager toca o que estiver aqui:
///  - loop grave contínuo (zumbido da casa, vento, respiração do prédio);
///  - rangidos soltos em intervalos aleatórios, vindos de lados aleatórios;
///  - a superfície padrão dos passos da Luma nesta cena.
/// Outros scripts chamam SilencioAntesDoSusto(segundos) para o ambiente quase sumir antes de um susto
/// (pode ser ligado direto num UnityEvent: ScriptedEncounter, TriggerEvent, FlagSwitch...).
/// Colocado pelo menu "Umbra > Terror > Colocar sons na cena aberta". Os sons ficam vazios: a equipe de som preenche.
/// </summary>
public class AmbienteDoComodo : MonoBehaviour
{
    [Header("Loop grave")]
    [Tooltip("Loop contínuo do cômodo. Sugestão: amb_<cena>_loop.wav (ver Assets/Audio/LEIAME.md).")]
    public AudioClip loopGrave;
    [Range(0f, 1f)] public float volumeLoop = 0.5f;

    [Header("Rangidos aleatórios")]
    [Tooltip("Sons curtos soltos (madeira, canos, passos distantes). Um é sorteado a cada vez.")]
    public AudioClip[] rangidos;
    [Tooltip("Volume mínimo e máximo de cada rangido.")]
    public Vector2 volumeRangidos = new Vector2(0.25f, 0.6f);
    [Tooltip("Segundos entre um rangido e o próximo (mínimo e máximo).")]
    public Vector2 intervaloRangidos = new Vector2(8f, 20f);
    [Tooltip("Quanto o rangido pode vir da esquerda/direita (0 = sempre no centro).")]
    [Range(0f, 1f)] public float espalhamento = 0.8f;

    [Header("Passos")]
    public Superficie superficiePadrao = Superficie.Madeira;

    [Header("Perseguição")]
    [Tooltip("Música de perseguição só desta cena (vazio = a do AudioManager).")]
    public AudioClip musicaDePerseguicao;

    void OnEnable()  { if (AudioManager.Instance != null) AudioManager.Instance.DefinirAmbiente(this); }
    void Start()     { if (AudioManager.Instance != null) AudioManager.Instance.DefinirAmbiente(this); }
    void OnDisable() { if (AudioManager.Instance != null) AudioManager.Instance.SairDoAmbiente(this); }

    /// <summary>Abaixa o ambiente quase a zero por alguns segundos (e sem rangidos), depois volta devagar.</summary>
    public void SilencioAntesDoSusto(float segundos)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Silencio(segundos);
    }

    /// <summary>Atalho para achar o ambiente da cena atual (pode ser nulo).</summary>
    public static AmbienteDoComodo Atual => AudioManager.Instance != null ? AudioManager.Instance.Ambiente : null;
}
