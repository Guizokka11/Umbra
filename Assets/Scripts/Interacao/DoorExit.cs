using UnityEngine;

/// <summary>
/// Porta que troca de cômodo quando a Luma aperta E (em vez de só encostar).
/// Pode ficar trancada até um GameFlag existir.
/// </summary>
public class DoorExit : Interactable
{
    public string nextScene;
    [Tooltip("SpawnPoint de chegada na outra cena.")]
    public string spawnId;
    [Tooltip("Vazio = sempre aberta.")]
    public string requiredFlag;
    [TextArea] public string lockedMessage = "Trancada.";
    public float fadeTime = 0.8f;

    void Awake()
    {
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Entrar";
    }

    public override void Interact(PlayerInteractor who)
    {
        if (!string.IsNullOrEmpty(requiredFlag) && !GameFlags.Has(requiredFlag))
        {
            Hud.Toast(lockedMessage);
            return;
        }
        var exit = GetComponent<LevelExit>();
        if (exit == null) exit = gameObject.AddComponent<LevelExit>();
        exit.loadOnEnter = false;
        exit.nextScene = nextScene;
        exit.spawnId = spawnId;
        exit.fadeTime = fadeTime;
        exit.LoadNow();
    }
}
