using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interação genérica para puzzles sem precisar programar:
/// alavanca, interruptor de luz, porta, gaveta, gatilho de evento.
/// Ligue as ações em "onInteract" pelo Inspector
/// (ex.: LightZone.Toggle, Animator.SetTrigger, GameObject.SetActive).
/// </summary>
public class SimpleInteractable : Interactable
{
    public bool oneShot = false;
    [Tooltip("Alterna entre onInteract e onInteractAgain (ex.: ligar/desligar).")]
    public bool toggle = false;

    [Tooltip("Atalho sem eventos: objetos que são ATIVADOS ao interagir (ex.: saída da porta).")]
    public GameObject[] activateOnInteract;
    [Tooltip("Objetos que são DESATIVADOS ao interagir (ex.: porta fechada).")]
    public GameObject[] deactivateOnInteract;

    public UnityEvent onInteract;
    public UnityEvent onInteractAgain;

    public AudioSource sfx;

    bool used, state;

    public override bool CanInteract(PlayerInteractor who) => base.CanInteract(who) && !(oneShot && used);

    public override void Interact(PlayerInteractor who)
    {
        used = true;
        if (sfx != null) sfx.Play();

        if (!(toggle && state))
        {
            if (activateOnInteract != null) foreach (var g in activateOnInteract) if (g != null) g.SetActive(true);
            if (deactivateOnInteract != null) foreach (var g in deactivateOnInteract) if (g != null) g.SetActive(false);
        }
        if (toggle && state) onInteractAgain?.Invoke();
        else onInteract?.Invoke();
        state = !state;
    }
}
