using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Tremor curto da câmera (sustos). Roda depois do Cinemachine no mesmo quadro e desloca a câmera por cima
/// da posição que ele calculou; no quadro seguinte o Cinemachine põe a câmera de volta no lugar.
/// Uso: TremorDeCamera.Tremer(forca, duracao). Criado sozinho na Main Camera na primeira vez.
/// </summary>
[DefaultExecutionOrder(10000)]
public class TremorDeCamera : MonoBehaviour
{
    float forca, duracao, t = 1f;
    Vector3 ultimo;
    CinemachineBrain brain;

    /// <summary>forca em metros (0.1 = leve, 0.25 = forte).</summary>
    public static void Tremer(float forca, float duracao)
    {
        var cam = Camera.main;
        if (cam == null || forca <= 0f || duracao <= 0f) return;
        var tr = cam.GetComponent<TremorDeCamera>();
        if (tr == null) tr = cam.gameObject.AddComponent<TremorDeCamera>();
        tr.forca = Mathf.Max(tr.t < tr.duracao ? tr.forca : 0f, forca);
        tr.duracao = duracao;
        tr.t = 0f;
    }

    void Awake() => brain = GetComponent<CinemachineBrain>();

    void LateUpdate()
    {
        // Sem Cinemachine mexendo na câmera (ex.: congelada na captura), desfaz o deslocamento anterior.
        if (brain == null || !brain.isActiveAndEnabled) transform.position -= ultimo;
        ultimo = Vector3.zero;
        if (t >= duracao) return;
        t += Time.unscaledDeltaTime;
        float k = 1f - Mathf.Clamp01(t / duracao);                    // diminui até parar
        ultimo = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * forca * k * k;
        transform.position += transform.rotation * ultimo;
        ultimo = transform.rotation * ultimo;
    }
}
