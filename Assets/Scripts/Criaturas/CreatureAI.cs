using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public enum CreatureState { Idle, Patrol, Investigate, Chase, Search, Return }

/// <summary>
/// IA base das manifestações. Patrulha, ouve ruído, vê a Luma, persegue,
/// procura e volta. Cada criatura/fobia é configurada pelo Inspector:
///  - Nictofobia: avoidsLight = true (não entra na luz, a luz é segura).
///  - Criatura "cega": viewDistance = 0, hearingMultiplier alto (só ouve).
///  - Criatura lenta e implacável: chaseSpeed baixo, loseSightTime alto.
/// Usa NavMeshAgent se houver um no objeto; senão, anda em linha reta no plano XZ.
/// </summary>
public class CreatureAI : MonoBehaviour
{
    public static readonly List<CreatureAI> All    = new List<CreatureAI>();
    public static readonly List<CreatureAI> Active = new List<CreatureAI>();

    [Header("Patrulha")]
    public Transform[] waypoints;
    public float patrolSpeed = 1.6f;
    public float waitAtPoint = 1.5f;
    public bool  startIdle = false;

    [Header("Perseguição")]
    public float chaseSpeed     = 4.2f;
    public float catchDistance  = 0.9f;
    [Tooltip("Tempo sem ver a Luma até desistir da perseguição e procurar.")]
    public float loseSightTime  = 2.5f;
    public float searchTime     = 4f;
    public float investigateSpeed = 2.4f;

    [Header("Visão")]
    public float viewDistance = 8f;
    [Range(0f, 360f)] public float viewAngle = 120f;
    [Tooltip("Tempo de exposição até perceber a Luma (dá chance de reagir).")]
    public float noticeTime = 0.35f;
    public Transform eyes;
    public LayerMask obstacleMask = ~0;

    [Header("Audição")]
    public float hearingMultiplier = 1f;

    [Header("Luz")]
    [Tooltip("Não entra em LightZones acesas. A Luma fica segura na luz.")]
    public bool avoidsLight = true;

    [Header("Visual")]
    public SpriteRenderer sprite;
    public Animator animator;          // estados: Idle, Walk, Chase (opcional)

    [Header("Eventos")]
    public UnityEvent onStartChase;
    public UnityEvent onLosePlayer;
    public UnityEvent onCatch;

    public CreatureState State { get; private set; }

    NavMeshAgent agent;
    PlayerState player;
    Vector3 startPos;
    Vector3 facing = Vector3.right;
    Vector3 lastKnownPos;
    int   wpIndex;
    float waitTimer, stateTimer, noticeTimer, lastSeenTime = -99f;
    bool  sawHiding;
    float farejarPor = -1f;            // > 0: a próxima procura é uma farejada deste tamanho (Farejar)
    float bloqueadaPor;                // patrulha: tempo com o caminho bloqueado pela luz

    /// <summary>Parada farejando (Farejar) neste momento.</summary>
    public bool Farejando => State == CreatureState.Search && farejarPor > 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        startPos = transform.position;
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void OnEnable()  { All.Add(this); Active.Add(this); }
    void OnDisable()
    {
        All.Remove(this); Active.Remove(this);
        if (AudioManager.Instance != null) AudioManager.Instance.PerseguicaoAcabou(this);
    }

    void Start()
    {
        player = PlayerState.Instance;
        SetState(startIdle || waypoints == null || waypoints.Length == 0 ? CreatureState.Idle : CreatureState.Patrol);
    }

    // ---------------------------------------------------------------- loop

    void Update()
    {
        if (player == null) { player = PlayerState.Instance; if (player == null) return; }
        if (player.isDead) { Stop(); return; }

        bool sees = CanSeePlayer();
        if (sees) { lastSeenTime = Time.time; lastKnownPos = player.transform.position; }

        // Percebe a Luma depois de um pequeno tempo de exposição.
        if (State != CreatureState.Chase)
        {
            noticeTimer = sees ? noticeTimer + Time.deltaTime : 0f;
            if (noticeTimer >= noticeTime) { SetState(CreatureState.Chase); onStartChase.Invoke(); }
            else if (HearsPlayer()) { lastKnownPos = player.transform.position; SetState(CreatureState.Investigate); }
        }

        switch (State)
        {
            case CreatureState.Idle:        Stop(); break;
            case CreatureState.Patrol:      TickPatrol(); break;
            case CreatureState.Investigate: TickInvestigate(); break;
            case CreatureState.Chase:       TickChase(sees); break;
            case CreatureState.Search:      TickSearch(); break;
            case CreatureState.Return:      TickReturn(); break;
        }

        UpdateVisual();
    }

    void TickPatrol()
    {
        if (waypoints == null || waypoints.Length == 0) { SetState(CreatureState.Idle); return; }
        Vector3 target = waypoints[wpIndex].position;
        if (Reached(target))
        {
            Stop();
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtPoint) { waitTimer = 0f; wpIndex = (wpIndex + 1) % waypoints.Length; }
        }
        else if (!MoveTowards(target, patrolSpeed))
        {
            // Caminho bloqueado pela luz: espera um pouco e tenta o próximo ponto (senão ficaria parada para sempre).
            bloqueadaPor += Time.deltaTime;
            if (bloqueadaPor >= waitAtPoint) { bloqueadaPor = 0f; wpIndex = (wpIndex + 1) % waypoints.Length; }
        }
        else bloqueadaPor = 0f;
    }

    void TickInvestigate()
    {
        if (Reached(lastKnownPos) || !MoveTowards(lastKnownPos, investigateSpeed))
            SetState(CreatureState.Search);
    }

    void TickChase(bool sees)
    {
        // Viu a Luma entrando no esconderijo: vai até lá e a puxa para fora.
        if (player.isHidden && Time.time - lastSeenTime < 0.6f) sawHiding = true;

        if (player.isHidden && !sawHiding) { SetState(CreatureState.Search); onLosePlayer.Invoke(); return; }

        Vector3 target = sees || sawHiding ? player.transform.position : lastKnownPos;
        MoveTowards(target, chaseSpeed);

        if (Flat(player.transform.position - transform.position).magnitude <= catchDistance
            && (!player.isHidden || sawHiding))
        {
            Catch();
            return;
        }

        if (!sees && !sawHiding && Time.time - lastSeenTime > loseSightTime)
        {
            SetState(CreatureState.Search);
            onLosePlayer.Invoke();
        }
    }

    void TickSearch()
    {
        Stop();
        stateTimer += Time.deltaTime;
        // Olha para os dois lados enquanto procura.
        if (Mathf.Repeat(stateTimer, 1.5f) < Time.deltaTime) facing = -facing;
        if (stateTimer >= (farejarPor > 0f ? farejarPor : searchTime)) SetState(CreatureState.Return);
    }

    void TickReturn()
    {
        Vector3 home = waypoints != null && waypoints.Length > 0 ? waypoints[wpIndex].position : startPos;
        if (Reached(home) || !MoveTowards(home, patrolSpeed))
            SetState(waypoints != null && waypoints.Length > 0 && !startIdle ? CreatureState.Patrol : CreatureState.Idle);
    }

    // ------------------------------------------------------------ percepção

    public bool CanSeePlayer()
    {
        if (player == null || player.isHidden || viewDistance <= 0f) return false;

        Vector3 from = eyes != null ? eyes.position : transform.position + Vector3.up * 0.8f;
        Vector3 to   = player.ChestPosition;
        Vector3 dir  = to - from;

        float mult = FearSystem.Instance != null ? FearSystem.Instance.DetectionMultiplier : 1f;
        if (dir.magnitude > viewDistance * mult) return false;

        if (Vector3.Angle(facing, Flat(dir)) > viewAngle * 0.5f) return false;

        if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, dir.magnitude, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.transform.root == player.transform.root;
        return true;
    }

    bool HearsPlayer()
    {
        float noise = player.NoiseRadius * hearingMultiplier;
        return noise > 0f && Vector3.Distance(transform.position, player.transform.position) <= noise;
    }

    // ------------------------------------------------------------- movimento

    /// <summary>Retorna false se o caminho está bloqueado (ex.: pela luz).</summary>
    bool MoveTowards(Vector3 target, float speed)
    {
        Vector3 dir = Flat(target - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return true;
        facing = dir.normalized;

        Vector3 next = transform.position + facing * Mathf.Max(speed * Time.deltaTime, 0.3f);
        if (avoidsLight && LightZone.IsPointLit(next + Vector3.up * 0.5f)) { Stop(); return false; }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.SetDestination(target);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position,
                new Vector3(target.x, transform.position.y, target.z), speed * Time.deltaTime);
        }
        return true;
    }

    void Stop()
    {
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
    }

    bool Reached(Vector3 target) => Flat(target - transform.position).magnitude < 0.25f;

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // --------------------------------------------------------------- estados

    void SetState(CreatureState s)
    {
        // Música de perseguição: entra quando começa a perseguir, sai quando desiste/pega/volta ao início.
        if (AudioManager.Instance != null && (s == CreatureState.Chase) != (State == CreatureState.Chase))
        {
            if (s == CreatureState.Chase) AudioManager.Instance.PerseguicaoComecou(this);
            else AudioManager.Instance.PerseguicaoAcabou(this);
        }
        State = s;
        stateTimer = 0f;
        noticeTimer = 0f;
        if (s != CreatureState.Chase) sawHiding = false;
        if (s != CreatureState.Investigate && s != CreatureState.Search) farejarPor = -1f;
    }

    void Catch()
    {
        onCatch.Invoke();
        // Tira a Luma do esconderijo se ela estiver em um.
        var interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null) interactor.ForceRelease();
        // Com SequenciaDeCaptura, a captura tem cena (tela escura, som, puxão) e ela chama o PlayerCaught no fim.
        var seq = GetComponent<SequenciaDeCaptura>();
        if (seq != null && seq.enabled) seq.Executar(this);
        else if (GameManager.Instance != null) GameManager.Instance.PlayerCaught(this);
        SetState(CreatureState.Idle);
    }

    public void ResetCreature()
    {
        if (agent != null && agent.isOnNavMesh) agent.Warp(startPos);
        else transform.position = startPos;
        wpIndex = 0;
        lastSeenTime = -99f;
        SetState(startIdle || waypoints == null || waypoints.Length == 0 ? CreatureState.Idle : CreatureState.Patrol);
    }

    /// <summary>Para eventos de roteiro: força a perseguição (ex.: revelação do inimigo).</summary>
    public void StartChase()
    {
        if (player != null) { lastKnownPos = player.transform.position; lastSeenTime = Time.time; }
        SetState(CreatureState.Chase);
        onStartChase.Invoke();
    }

    /// <summary>
    /// Um barulho aconteceu em "position" (pilha de papéis, tábua que range...).
    /// Se estiver ao alcance da audição, a criatura vai investigar.
    /// Chamado pelo NoiseEmitter.
    /// </summary>
    public void HearNoiseAt(Vector3 position, float radius)
    {
        if (State == CreatureState.Chase) return;
        if (Vector3.Distance(transform.position, position) > radius * Mathf.Max(hearingMultiplier, 0.01f)) return;
        lastKnownPos = position;
        SetState(CreatureState.Investigate);
    }

    /// <summary>
    /// Vai até "ponto" e fica ali farejando por alguns segundos (olhando para os lados), depois volta à ronda.
    /// Não acha quem está escondido: é só tensão (ex.: parar na frente do esconderijo da Luma).
    /// </summary>
    public void Farejar(Vector3 ponto, float segundos)
    {
        if (State == CreatureState.Chase) return;
        lastKnownPos = ponto;
        SetState(CreatureState.Investigate);
        farejarPor = Mathf.Max(0.5f, segundos);
    }

    /// <summary>Coloca a criatura num lugar e faz dele o ponto de volta (ResetCreature).</summary>
    public void Posicionar(Vector3 posicao)
    {
        startPos = posicao;
        if (agent != null && agent.isOnNavMesh) agent.Warp(posicao);
        else transform.position = posicao;
    }

    /// <summary>Troca os pontos da ronda (recomeça do primeiro) e volta a patrulhar, se não estiver perseguindo.</summary>
    public void DefinirRonda(Transform[] pontos)
    {
        waypoints = pontos;
        wpIndex = 0;
        waitTimer = 0f;
        bloqueadaPor = 0f;
        if (State != CreatureState.Chase)
            SetState(pontos != null && pontos.Length > 0 ? CreatureState.Patrol : CreatureState.Idle);
    }

    public void SetIdle()   => SetState(CreatureState.Idle);
    public void SetPatrol() => SetState(CreatureState.Patrol);

    void UpdateVisual()
    {
        if (sprite != null && Mathf.Abs(facing.x) > 0.01f) sprite.flipX = facing.x < 0f;
        if (animator == null) return;
        string anim = State == CreatureState.Chase ? "Chase"
                    : Farejando && animator.HasState(0, Animator.StringToHash("Sniff")) ? "Sniff"
                    : State == CreatureState.Idle || State == CreatureState.Search ? "Idle" : "Walk";
        int hash = Animator.StringToHash(anim);
        if (animator.HasState(0, hash)) animator.Play(hash);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 from = eyes != null ? eyes.position : transform.position + Vector3.up * 0.8f;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
        Vector3 f = Application.isPlaying ? facing : Vector3.right;
        Gizmos.DrawLine(from, from + Quaternion.Euler(0, viewAngle * 0.5f, 0) * f * viewDistance);
        Gizmos.DrawLine(from, from + Quaternion.Euler(0, -viewAngle * 0.5f, 0) * f * viewDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, catchDistance);
    }
}
