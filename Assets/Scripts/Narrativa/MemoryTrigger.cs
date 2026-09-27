using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fragmento de memória da Luma (noite da chegada, noite da despedida).
/// Disparado por trigger ao passar, ou por evento (ex.: onRead de uma Clue).
/// Mostra quadros curtos (imagem + frase) sobre a tela, com tremor leve,
/// e devolve o controle. Pode sobrescrever o áudio com um som abafado.
/// </summary>
public class MemoryTrigger : MonoBehaviour
{
    [System.Serializable]
    public class Frame
    {
        public Sprite image;
        [TextArea] public string line;
        public float duration = 2f;
    }

    public Frame[] frames;
    public bool playOnTriggerEnter = true;
    public bool onlyOnce = true;

    [Header("UI (compartilhada entre todas as memórias)")]
    public CanvasGroup overlay;
    public Image frameImage;
    public TMP_Text frameText;
    public float fade = 0.4f;
    public float shake = 4f;

    [Header("Áudio (opcional)")]
    public AudioSource memoryAudio;

    public UnityEvent onFinished;

    bool played;

    void OnTriggerEnter(Collider other)
    {
        if (playOnTriggerEnter && other.CompareTag("Player")) Play();
    }

    public void Play()
    {
        if (onlyOnce && played) return;
        played = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        var st = PlayerState.Instance;
        if (st != null) { st.isReading = true; st.Movement.canMove = false; }
        if (memoryAudio != null) memoryAudio.Play();

        overlay.gameObject.SetActive(true);
        Vector2 basePos = frameImage != null ? frameImage.rectTransform.anchoredPosition : Vector2.zero;

        foreach (var f in frames)
        {
            if (frameImage != null) { frameImage.sprite = f.image; frameImage.enabled = f.image != null; }
            if (frameText  != null) frameText.text = f.line;

            yield return FadeTo(1f);
            for (float t = 0; t < f.duration; t += Time.deltaTime)
            {
                if (frameImage != null)
                    frameImage.rectTransform.anchoredPosition = basePos + Random.insideUnitCircle * shake;
                yield return null;
            }
            yield return FadeTo(0f);
        }

        if (frameImage != null) frameImage.rectTransform.anchoredPosition = basePos;
        overlay.gameObject.SetActive(false);
        if (st != null) { st.isReading = false; st.Movement.canMove = true; }
        onFinished.Invoke();
    }

    IEnumerator FadeTo(float target)
    {
        float s = overlay.alpha;
        for (float t = 0; t < fade; t += Time.deltaTime)
        {
            overlay.alpha = Mathf.Lerp(s, target, t / fade);
            yield return null;
        }
        overlay.alpha = target;
    }
}
