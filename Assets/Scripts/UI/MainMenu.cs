using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tela inicial (cena 00_Menu). Monta a própria interface ao dar Play.
/// Novo jogo apaga o progresso; Continuar volta à última porta atravessada.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Textos da tela (Assets/Dados/Menu/TextosDoMenu). Se vazio, usa os padrões.")]
    public MenuTexts textos;

    CanvasGroup group;

    void Start()
    {
        Time.timeScale = 1f;
        var t = textos != null ? textos : ScriptableObject.CreateInstance<MenuTexts>();
        var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var title = Text(canvasGo.transform, t.titulo, 150, new Vector2(0, 250), new Vector2(1600, 200));
        title.characterSpacing = 30f;
        title.color = t.corTitulo;
        var sub = Text(canvasGo.transform, t.subtitulo, 30, new Vector2(0, 140), new Vector2(1400, 60));
        sub.fontStyle = FontStyles.Italic;
        sub.color = new Color(0.75f, 0.68f, 0.78f, 0.8f);

        var bNew = Button(canvasGo.transform, t.botaoNovoJogo, new Vector2(0, -20), SaveGame.NewGame);
        Button cont = null;
        if (SaveGame.HasSave) cont = Button(canvasGo.transform, t.botaoContinuar, new Vector2(0, -100), SaveGame.Continue);
        Button(canvasGo.transform, t.botaoSair, new Vector2(0, SaveGame.HasSave ? -180 : -100), () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });

        var help = Text(canvasGo.transform, t.controles, 24, new Vector2(0, -400), new Vector2(1700, 90));
        help.color = new Color(1f, 1f, 1f, 0.45f);
        if (!string.IsNullOrEmpty(t.creditos))
        {
            var cr = Text(canvasGo.transform, t.creditos, 20, new Vector2(0, -490), new Vector2(1700, 60));
            cr.color = new Color(1f, 1f, 1f, 0.35f);
        }

        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EventSystem.current?.SetSelectedGameObject((cont != null ? cont : bNew).gameObject);

        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        for (float t = 0; t < 2f; t += Time.deltaTime) { group.alpha = t / 2f; yield return null; }
        group.alpha = 1f;
    }

    static TMP_Text Text(Transform parent, string text, float size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.93f, 0.9f, 0.86f);
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    static Button Button(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Botao_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(480, 64);
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0.04f);
        cb.highlightedColor = new Color(1f, 0.85f, 0.6f, 0.22f);
        cb.selectedColor = new Color(1f, 0.85f, 0.6f, 0.22f);
        cb.pressedColor = new Color(1f, 0.85f, 0.6f, 0.4f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        Text(go.transform, label, 34, Vector2.zero, new Vector2(480, 64));
        return b;
    }
}
