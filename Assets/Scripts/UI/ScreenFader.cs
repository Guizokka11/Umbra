using System.Collections;
using UnityEngine;

/// <summary>
/// Fade de tela. Criar um Canvas (Screen Space - Overlay, Sort Order alto)
/// com uma Image preta em tela cheia + CanvasGroup, e colocar este script nele.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Tooltip("Começa a cena preta e clareia (bom para o despertar da Luma).")]
    public bool fadeInOnStart = true;
    public float startFadeTime = 2f;

    CanvasGroup group;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = fadeInOnStart ? 1f : 0f;
    }

    void Start()
    {
        if (fadeInOnStart) StartCoroutine(FadeIn(startFadeTime));
    }

    public IEnumerator FadeOut(float time) => FadeTo(1f, time);
    public IEnumerator FadeIn(float time)  => FadeTo(0f, time);

    public IEnumerator FadeTo(float target, float time)
    {
        float start = group.alpha;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        group.alpha = target;
    }
}
