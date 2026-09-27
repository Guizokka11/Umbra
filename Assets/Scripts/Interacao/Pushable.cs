using UnityEngine;

/// <summary>
/// Caixa, cadeira, baú... que a Luma pode segurar e arrastar (empurrar/puxar).
/// Requer Rigidbody (não-kinematic) e Collider sólido.
/// Enquanto segura: Luma anda mais devagar, não pula e não vira o sprite.
/// Se o objeto travar em uma parede e ela se afastar demais, solta sozinha.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pushable : Interactable
{
    [Header("Arrastar")]
    [Range(0.1f, 1f)] public float dragSpeedMultiplier = 0.45f;
    [Tooltip("Solta se a distância até a Luma aumentar mais que isso (objeto travou).")]
    public float maxGapIncrease = 0.4f;
    [Tooltip("Mantém o objeto no mesmo Z (corredores 2.5D).")]
    public bool lockZ = true;
    [Tooltip("Objeto pesado demais: não pode ser arrastado, só usado como plataforma.")]
    public bool locked = false;

    [Header("Empurrão ao encostar (sem segurar)")]
    public bool  allowFreePush = false;
    public float freePushSpeed = 1.2f;

    [Header("Som (opcional)")]
    public AudioSource dragLoop;

    Rigidbody rb;
    Collider[] myCols;
    PlayerInteractor holder;
    Vector3 offset;
    float startGap;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCols = GetComponentsInChildren<Collider>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // não atravessa chão fino
        rb.constraints = RigidbodyConstraints.FreezeRotation
                       | (lockZ ? RigidbodyConstraints.FreezePositionZ : RigidbodyConstraints.None);
        if (string.IsNullOrEmpty(prompt) || prompt == "Interagir") prompt = "Segurar";
    }

    public override bool CanInteract(PlayerInteractor who)
    {
        if (!base.CanInteract(who) || locked) return false;
        // Em cima da caixa não dá para segurá-la (deixa o trinco/objeto alto ser o alvo).
        var col = GetComponent<Collider>();
        if (col != null && who.transform.position.y > col.bounds.max.y - 0.15f) return false;
        return true;
    }

    public override void Interact(PlayerInteractor who)
    {
        if (holder == null) Grab(who);
        else Release();
    }

    void Grab(PlayerInteractor who)
    {
        holder = who;
        var st = who.State;
        st.isGrabbing = true;
        st.heldObject = transform;
        st.Movement.speedMultiplier = dragSpeedMultiplier;
        st.Movement.canJump = false;
        st.Movement.lockFacing = true;

        offset = transform.position - who.transform.position;
        offset.y = 0f;
        startGap = offset.magnitude;

        var cc = st.Controller;
        foreach (var c in myCols) Physics.IgnoreCollision(cc, c, true);

        who.Lock(this);
        if (dragLoop != null) dragLoop.Play();
    }

    public void Release()
    {
        if (holder == null) return;
        var st = holder.State;
        st.isGrabbing = false;
        st.heldObject = null;
        st.Movement.speedMultiplier = 1f;
        st.Movement.canJump = true;
        st.Movement.lockFacing = false;

        var cc = st.Controller;
        foreach (var c in myCols) Physics.IgnoreCollision(cc, c, false);

        holder.Unlock(this);
        holder = null;
        if (dragLoop != null) dragLoop.Stop();
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    public override void ForceRelease(PlayerInteractor who) => Release();

    void FixedUpdate()
    {
        if (holder == null) return;

        Vector3 target = holder.transform.position + offset;
        Vector3 delta  = target - rb.position;
        delta.y = 0f;
        if (lockZ) delta.z = 0f;

        Vector3 v = delta / Time.fixedDeltaTime;
        v = Vector3.ClampMagnitude(v, 8f);
        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);

        Vector3 gap = rb.position - holder.transform.position; gap.y = 0f;
        if (gap.magnitude > startGap + maxGapIncrease) Release();

        if (dragLoop != null) dragLoop.volume = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude);
    }
}
