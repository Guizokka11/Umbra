using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controla captura, checkpoint e respawn. Um por cena.
/// Quando uma criatura pega a Luma: fade para preto, volta ao último
/// checkpoint, reseta medo e criaturas, fade de volta.
/// </summary>
[DefaultExecutionOrder(-50)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerState player;
    [Tooltip("Ponto inicial caso nenhum checkpoint tenha sido ativado.")]
    public Transform startPoint;
    public float caughtPause = 0.8f;
    public float fadeTime    = 0.6f;

    public UnityEvent onPlayerCaught;
    public UnityEvent onRespawn;

    Vector3 checkpointPos;
    bool respawning;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        if (player == null) player = FindAnyObjectByType<PlayerState>();
        checkpointPos = startPoint != null ? startPoint.position
                      : player != null   ? player.transform.position
                      : Vector3.zero;

        // Entrou por uma porta: aparece no SpawnPoint combinado.
        string spawnId = SaveGame.PendingSpawn;
        SaveGame.PendingSpawn = null;
        var sp = SpawnPoint.Find(spawnId);
        if (sp != null && player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = sp.transform.position;
            if (cc != null) cc.enabled = true;
            var mv = player.GetComponent<PlayerMovement>();
            if (mv != null) mv.facingRight = !sp.faceLeft;
            checkpointPos = sp.transform.position;
        }
        SaveGame.Record(gameObject.scene.name, sp != null ? sp.id : "");
    }

    /// <summary>Volta ao último checkpoint (menu de pausa, "desistir").</summary>
    public void RespawnNow() => PlayerCaught(null);

    public void SetCheckpoint(Vector3 position) => checkpointPos = position;

    public void PlayerCaught(CreatureAI by)
    {
        if (respawning || player == null) return;
        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        respawning = true;
        player.isDead = true;
        player.Movement.canMove = false;
        onPlayerCaught.Invoke();

        yield return new WaitForSeconds(caughtPause);
        if (ScreenFader.Instance != null) yield return ScreenFader.Instance.FadeOut(fadeTime);

        // Solta esconderijos/objetos que ela estava usando.
        var interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null) interactor.ForceRelease();

        player.ResetState();
        player.Movement.Teleport(checkpointPos);
        if (CameraDirector.Instance != null) CameraDirector.Instance.SnapNextSwitch();
        if (FearSystem.Instance != null) FearSystem.Instance.ResetFear();
        foreach (var c in CreatureAI.All) c.ResetCreature();

        yield return new WaitForSeconds(0.2f);
        if (ScreenFader.Instance != null) yield return ScreenFader.Instance.FadeIn(fadeTime);

        onRespawn.Invoke();
        respawning = false;
    }
}
