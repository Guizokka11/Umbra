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
    [Tooltip("Opcional: em vez de patrulhar por \"duration\", a criatura faz esta busca (checa os esconderijos um por um).")]
    public BuscaDaCriatura busca;

    public UnityEvent onBegin;
    public UnityEvent onEnd;

    bool running;

    /// <summary>O encontro está acontecendo agora.</summary>
    public bool Rodando => running;
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
        if (busca != null) busca.Parar();
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
            if (busca == null) creature.SetPatrol();
        }

        if (busca != null && creature != null)
        {
            busca.Iniciar(creature);
            while (!busca.Terminou) yield return null;
        }
        else yield return new WaitForSeconds(duration);
        // Só vai embora quando não estiver perseguindo.
        while (creature != null && creature.State == CreatureState.Chase) yield return null;
        // A perseguição acabou porque ela PEGOU a Luma: não termina o encontro; o respawn chama Restart e ele recomeça.
        var luma = PlayerState.Instance;
        if (luma != null && luma.isDead) yield break;

        if (creature != null) creature.gameObject.SetActive(false);
        if (lightsOff != null) foreach (var l in lightsOff) if (l != null) l.SetOn(true);
        running = false;
        GameFlags.Set(doneFlag);
        onEnd?.Invoke();
    }
}
