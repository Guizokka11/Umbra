using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pista no cenário. Interagir abre o ClueUI com o conteúdo do ClueData.
/// </summary>
public class Clue : Interactable
{
    public ClueData data;
    [Tooltip("Some do cenário depois de lida (ex.: bilhete que Luma guarda).")]
    public bool removeAfterReading = false;
    [Tooltip("Brilho/partícula que some depois de lida.")]
    public GameObject highlight;

    public UnityEvent onRead;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Examinar";
    }

    void Start()
    {
        if (data != null && ClueJournal.Has(data.id) && highlight != null) highlight.SetActive(false);
    }

    public override float PriorityPenalty =>
        data != null && ClueJournal.Has(data.id) ? 1.5f : 0f;

    public override void Interact(PlayerInteractor who)
    {
        if (data == null) return;
        ClueJournal.Add(data);
        if (highlight != null) highlight.SetActive(false);
        onRead.Invoke();

        if (ClueUI.Instance != null) ClueUI.Instance.Show(data, OnClosed);
        else OnClosed();
    }

    void OnClosed()
    {
        if (removeAfterReading) gameObject.SetActive(false);
    }
}
