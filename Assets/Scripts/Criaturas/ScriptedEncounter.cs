using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Encontro com roteiro: as luzes apagam, uma criatura entra, patrulha por um
/// tempo e vai embora. A Luma precisa se esconder. Se for pega, o encontro
/// recomeça do checkpoint. Ao terminar, salva "doneFlag".
/// Chamar Begin() por um evento (ex.: ItemPickup.onPickup, TriggerEvent).
/// </summary>
public class ScriptedEncounter : MonoBehaviour
{
    public CreatureAI creature;
    public LightZone[] lightsOff;
    [Tooltip("Tempo entre apagar as luzes e a criatura aparecer.")]
    public float arriveDelay = 1.5f;
    public float duration = 12f;
    public string doneFlag;
    [Tooltip("Onde a Luma volta se for pega durante o encontro.")]
    public Transform respawnPoint;
    [TextArea] public string startSubtitle;

    public UnityEvent onBegin;
    public UnityEvent onEnd;

    bool running;
    Coroutine co;

    void Start()
    {
        if (creature != null) creature.gameObject.SetActive(false);
        if (GameManager.Instance != null) GameManager.Instance.onRespawn.AddListener(Restart);
    }

    public void Begin()
    {
        if (running || GameFlags.Has(doneFlag)) return;
        running = true;
        if (respawnPoint != null && GameManager.Instance != null) GameManager.Instance.SetCheckpoint(respawnPoint.position);
        co = StartCoroutine(Run());
        onBegin?.Invoke();
    }

    void Restart()
    {
        if (!running) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (creature != null) creature.gameObject.SetActive(false);
        if (lightsOff != null) foreach (var l in lightsOff) if (l != null) l.SetOn(false);
        if (!string.IsNullOrEmpty(startSubtitle)) Hud.Subtitle(startSubtitle, 2.5f);

        yield return new WaitForSeconds(arriveDelay);
        if (creature != null)
        {
            creature.gameObject.SetActive(true);
            creature.ResetCreature();
            creature.SetPatrol();
        }

        yield return new WaitForSeconds(duration);
        // Só vai embora quando não estiver perseguindo.
        while (creature != null && creature.State == CreatureState.Chase) yield return null;

        if (creature != null) creature.gameObject.SetActive(false);
        if (lightsOff != null) foreach (var l in lightsOff) if (l != null) l.SetOn(true);
        running = false;
        GameFlags.Set(doneFlag);
        onEnd?.Invoke();
    }
}
