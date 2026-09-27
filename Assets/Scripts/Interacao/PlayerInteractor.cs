using UnityEngine;
using TMPro;

/// <summary>
/// Encontra o Interactable mais próximo e usa com a tecla de interação.
/// Enquanto a Luma está "presa" a algo (escondida, segurando caixa),
/// a tecla sempre vai para esse objeto, para que ela possa soltar/sair.
/// Também empurra levemente Pushables ao encostar (sem segurar).
/// Colocar no objeto Player.
/// </summary>
[RequireComponent(typeof(PlayerState))]
public class PlayerInteractor : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.E;
    public float   radius = 1.2f;

    [Header("UI (opcional)")]
    public TMP_Text promptLabel;

    public PlayerState State { get; private set; }
    public Interactable Current { get; private set; }
    public Interactable Locked  { get; private set; }

    int readingFrame = -10;

    void Awake() { State = GetComponent<PlayerState>(); }

    void Update()
    {
        // Evita reabrir uma pista no mesmo frame em que ela foi fechada com a mesma tecla.
        if (State.isReading) readingFrame = Time.frameCount;
        bool justClosed = Time.frameCount - readingFrame <= 1;

        Current = Locked != null ? Locked : FindNearest();

        if (promptLabel != null)
        {
            bool show = Current != null && !State.isReading && !State.isDead;
            promptLabel.gameObject.SetActive(show);
            if (show) promptLabel.text = Current.prompt;
        }

        if (Input.GetKeyDown(interactKey) && !justClosed && Current != null && !State.isReading && !State.isDead
            && !State.isHuggingBear)
        {
            Current.Interact(this);
        }
    }

    Interactable FindNearest()
    {
        Interactable best = null;
        float bestD = float.MaxValue;
        Vector3 p = transform.position;
        foreach (var it in Interactable.All)
        {
            if (it == null || !it.CanInteract(this)) continue;
            if (it.maxVerticalDistance > 0f && Mathf.Abs(it.Point.y - p.y) > it.maxVerticalDistance) continue;
            Vector3 d = it.Point - p; d.y = 0f;
            float dist = d.magnitude;
            float score = dist + it.PriorityPenalty;
            if (dist <= radius + it.extraRange && score < bestD) { best = it; bestD = score; }
        }
        return best;
    }

    /// <summary>Prende a tecla de interação a um objeto (esconderijo, caixa segurada).</summary>
    public void Lock(Interactable target)   { Locked = target; }
    public void Unlock(Interactable target) { if (Locked == target) Locked = null; }

    public void ForceRelease()
    {
        if (Locked != null) Locked.ForceRelease(this);
        Locked = null;
    }

    // Empurrão simples ao encostar em caixas (sem segurar).
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var rb = hit.rigidbody;
        if (rb == null || rb.isKinematic) return;
        var pushable = rb.GetComponent<Pushable>();
        if (pushable == null || !pushable.allowFreePush || State.isGrabbing) return;
        if (hit.moveDirection.y < -0.3f) return; // pisando em cima

        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, pushable.lockZ ? 0f : hit.moveDirection.z);
        rb.linearVelocity = new Vector3(dir.x * pushable.freePushSpeed, rb.linearVelocity.y, dir.z * pushable.freePushSpeed);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
