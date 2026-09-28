using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Captura com consequência. Numa criatura com CreatureAI, a captura vira uma cena curta:
///  1. a tela escurece de repente e um som alto toca;
///  2. a câmera congela e a Luma é puxada para fora do quadro (para o lado da criatura);
///  3. só então GameManager.PlayerCaught escurece de vez e volta ao checkpoint.
/// O CreatureAI chama Executar() no lugar do PlayerCaught quando este componente existe.
/// </summary>
public class SequenciaDeCaptura : MonoBehaviour
{
    [Tooltip("Som alto da captura. Vazio = um dos stingers gerais do AudioManager.")]
    public AudioClip somDaCaptura;
    [Tooltip("Quanto a tela escurece na hora (0 a 1).")]
    [Range(0f, 1f)] public float escuroNaHora = 0.82f;
    [Tooltip("Duração do puxão (s).")]
    public float tempoDoPuxao = 0.7f;
    [Tooltip("Quantos metros ela é arrastada (para fora do quadro).")]
    public float distanciaDoPuxao = 6f;
    [Tooltip("Pausa no escuro antes de voltar ao checkpoint.")]
    public float pausaNoFim = 0.25f;
    [Tooltip("A criatura vai junto no puxão (arrasta a Luma).")]
    public bool criaturaVaiJunto = true;

    bool rodando;

    /// <summary>No meio da captura (a ronda não pode sumir com ela agora).</summary>
    public bool Rodando => rodando;

    public void Executar(CreatureAI quem)
    {
        if (rodando) return;
        StartCoroutine(Rodar(quem));
    }

    IEnumerator Rodar(CreatureAI quem)
    {
        rodando = true;
        var st = PlayerState.Instance;
        if (st == null)
        {
            if (GameManager.Instance != null) GameManager.Instance.PlayerCaught(quem);
            rodando = false;
            yield break;
        }

        // 1. Escuro de repente + som alto + medo no máximo.
        st.isDead = true;
        st.Movement.canMove = false;
        if (FearSystem.Instance != null) FearSystem.Instance.fear = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.TocarStinger(somDaCaptura, 3f);
        if (ScreenFader.Instance != null) StartCoroutine(ScreenFader.Instance.FadeTo(escuroNaHora, 0.06f));

        // 2. Câmera parada; a Luma é arrastada para o lado da criatura, acelerando, até sair do quadro.
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.enabled = false;
        var cc = st.Controller;
        st.Movement.enabled = false;                                            // sem Move() com o controller desligado
        if (cc != null) cc.enabled = false;
        Vector3 inicio = st.transform.position;
        Vector3 dir = quem != null ? quem.transform.position - inicio : Vector3.right;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
        dir = new Vector3(Mathf.Sign(dir.x == 0f ? 1f : dir.x), 0f, 0f);      // puxa pela lateral do quadro
        Vector3 fim = inicio + dir * distanciaDoPuxao;
        Vector3 inicioCriatura = quem != null ? quem.transform.position : Vector3.zero;
        yield return new WaitForSeconds(0.12f);                                 // o susto antes do puxão
        for (float t = 0f; t < tempoDoPuxao; t += Time.deltaTime)
        {
            float k = t / tempoDoPuxao;
            k *= k;                                                              // acelera
            st.transform.position = Vector3.Lerp(inicio, fim, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.15f;
            if (criaturaVaiJunto && quem != null) quem.transform.position = inicioCriatura + (st.transform.position - inicio);
            yield return null;
        }
        if (cc != null) cc.enabled = true;
        st.Movement.enabled = true;                                             // canMove continua falso até o respawn
        yield return new WaitForSeconds(pausaNoFim);

        // 3. Agora sim: escurece de vez e volta ao checkpoint.
        if (GameManager.Instance != null) GameManager.Instance.PlayerCaught(quem);
        while (st.isDead) yield return null;                                     // ResetState no respawn
        if (brain != null) brain.enabled = true;
        rodando = false;
    }

    void OnDisable()
    {
        // Desligada no meio (troca de cena, ronda saiu): não deixa a câmera congelada.
        if (!rodando) return;
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.enabled = true;
        rodando = false;
    }
}
