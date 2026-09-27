using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Painel de leitura de pistas. Pausa o movimento da Luma enquanto aberto.
/// Montar: Canvas > Panel (root) com TMP_Text título, TMP_Text corpo,
/// Image opcional e um objeto "Sol" (ícone do sol da Amelie).
/// </summary>
public class ClueUI : MonoBehaviour
{
    public static ClueUI Instance { get; private set; }

    public GameObject root;
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Image image;
    [Tooltip("Ícone do pequeno sol. Só aparece em pistas verdadeiras (da Amelie).")]
    public GameObject sunIcon;

    public KeyCode closeKey  = KeyCode.E;
    public KeyCode closeKey2 = KeyCode.Escape;

    Action onClose;
    float openedAt;

    void Awake()
    {
        Instance = this;
        if (root != null) root.SetActive(false);
    }

    public void Show(ClueData data, Action closed = null)
    {
        onClose = closed;
        openedAt = Time.unscaledTime;

        if (titleText != null) titleText.text = data.title;
        if (bodyText  != null) bodyText.text  = data.text;
        if (image != null)
        {
            image.gameObject.SetActive(data.image != null);
            image.sprite = data.image;
        }
        if (sunIcon != null) sunIcon.SetActive(data.ShowsSun);

        var st = PlayerState.Instance;
        if (st != null) { st.isReading = true; st.Movement.canMove = false; }

        root.SetActive(true);
    }

    void Update()
    {
        if (root == null || !root.activeSelf) return;
        // Evita fechar no mesmo frame em que abriu (mesma tecla E).
        if (Time.unscaledTime - openedAt < 0.2f) return;
        if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(closeKey2)) Close();
    }

    public void Close()
    {
        root.SetActive(false);
        var st = PlayerState.Instance;
        if (st != null) { st.isReading = false; st.Movement.canMove = true; }
        var cb = onClose; onClose = null;
        cb?.Invoke();
    }
}
