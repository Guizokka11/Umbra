using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Decide qual câmera está ativa:
///  - Luma dentro de uma CameraZone (ou zona forçada) → câmera fixa da zona.
///  - Fora de todas → câmera que segue a Luma.
/// Um por cena. O menu "Umbra > Câmera > Configurar câmeras da cena" cria tudo.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CameraDirector : MonoBehaviour
{
    public static CameraDirector Instance { get; private set; }

    [Tooltip("Câmera que segue a Luma (CinemachineCamera com Position Composer).")]
    public CinemachineCamera followCamera;
    public Transform player;

    [Header("Prioridades")]
    public int followPriority = 10;
    public int activeZonePriority = 20;

    [Header("Transição ao voltar para a câmera que segue")]
    public bool cutBack = false;
    public float blendBackTime = 1.2f;

    [Tooltip("Altura acima dos pés usada para saber em qual zona a Luma está.")]
    public float playerHeightOffset = 0.8f;

    public CameraZone Current { get; private set; }

    CinemachineBrain brain;
    bool first = true;

    void Awake()
    {
        Instance = this;
        var main = Camera.main;
        if (main != null) brain = main.GetComponent<CinemachineBrain>();
        if (followCamera != null) followCamera.Priority = followPriority;
    }

    void Start()
    {
        if (player == null)
        {
            if (PlayerState.Instance != null) player = PlayerState.Instance.transform;
            else
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) player = p.transform;
            }
        }
        if (followCamera != null && player != null && followCamera.Follow == null) followCamera.Follow = player;
    }

    void LateUpdate()
    {
        if (player == null) return;
        Vector3 point = player.position + Vector3.up * playerHeightOffset;

        CameraZone best = null;
        foreach (var z in CameraZone.All)
        {
            if (z == null || z.zoneCamera == null) continue;
            if (!z.forced && !z.Contains(point)) continue;
            if (best == null || Better(z, best)) best = z;
        }

        if (best == Current && !first) return;
        Switch(best);
    }

    static bool Better(CameraZone a, CameraZone b)
    {
        if (a.forced != b.forced) return a.forced;
        if (a.priority != b.priority) return a.priority > b.priority;
        return a.Volume < b.Volume; // a zona menor (mais específica) vence
    }

    void Switch(CameraZone next)
    {
        // Transição: a da zona nova ao entrar, a do diretor ao voltar para a câmera que segue.
        if (brain != null)
        {
            bool cut = first || (next != null ? next.cut : cutBack);
            float time = next != null ? next.blendTime : blendBackTime;
            brain.DefaultBlend = cut
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, time);
        }

        foreach (var z in CameraZone.All)
            if (z != null && z.zoneCamera != null) z.zoneCamera.Priority = 0;

        if (next != null) next.zoneCamera.Priority = activeZonePriority;
        if (followCamera != null) followCamera.Priority = followPriority;

        Current = next;
        first = false;
    }

    /// <summary>Para respawn: troca de câmera sem transição no próximo frame.</summary>
    public void SnapNextSwitch() => first = true;
}
