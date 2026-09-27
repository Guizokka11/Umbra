using UnityEngine;

/// <summary>
/// Câmera em trilho: acompanha a Luma só no eixo X, entre dois limites,
/// mantendo altura, distância e ângulo fixos (enquadramento de pintura).
/// Colocar na CinemachineCamera de uma CameraZone (sem Position Composer).
/// </summary>
[ExecuteAlways]
public class CameraRail : MonoBehaviour
{
    public Transform target;
    public float minX = -3f;
    public float maxX = 3f;
    [Tooltip("Suavidade: quanto maior, mais rápido alcança a Luma.")]
    public float followSpeed = 4f;
    [Tooltip("Deslocamento no X em relação à Luma (olhar um pouco à frente).")]
    public float offsetX = 0f;

    [Header("Girar em vez de andar (sem paralaxe)")]
    [Tooltip("A câmera fica parada no lugar onde a pintura foi montada e GIRA para seguir a Luma. " +
             "Assim os móveis em profundidade nunca se descolam da pintura.")]
    public bool rotateInstead = true;
    [Tooltip("Distância da câmera até a parede pintada (z = 0).")]
    public float wallDistance = 10f;
    [Tooltip("Bordas da pintura (x): a câmera nunca gira a ponto de mostrar além delas.")]
    public float canvasMinX = -100f, canvasMaxX = 100f;
    public float verticalFov = 30f;
    [Header("Tremor de medo")]
    public float fearShake = 0.35f;

    float yaw;
    Vector3 basePos;
    bool hasBase;

    void Start()
    {
        if (target == null && PlayerState.Instance != null) target = PlayerState.Instance.transform;
        if (target == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) target = p.transform;
        }
        Snap();
    }

    public void Snap()
    {
        if (target == null) return;
        if (rotateInstead)
        {
            if (!hasBase) { basePos = transform.position; hasBase = true; }
            yaw = YawFor(target.position.x);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return;
        }
        var pos = transform.position;
        pos.x = Mathf.Clamp(target.position.x + offsetX, minX, maxX);
        transform.position = pos;
    }

    float YawFor(float x)
    {
        float d = Mathf.Max(wallDistance, 0.1f);
        float yaw = Mathf.Atan2(x + offsetX - basePos.x, d) * Mathf.Rad2Deg;
        // Limite: a borda da tela (meio campo de visão horizontal) não passa da borda da pintura.
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float h = Mathf.Atan(Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
        float lo = Mathf.Atan2(canvasMinX - basePos.x, d) * Mathf.Rad2Deg + h;
        float hi = Mathf.Atan2(canvasMaxX - basePos.x, d) * Mathf.Rad2Deg - h;
        if (lo > hi) return 0f;
        return Mathf.Clamp(yaw, lo, hi);
    }

    void LateUpdate()
    {
        if (target == null || !Application.isPlaying) return;
        if (rotateInstead)
        {
            if (!hasBase) { basePos = transform.position; hasBase = true; }
            yaw = Mathf.LerpAngle(yaw, YawFor(target.position.x), 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            float f = FearSystem.Instance != null ? FearSystem.Instance.fear : 0f;
            float s = Mathf.InverseLerp(0.55f, 1f, f) * fearShake;
            float t = Time.time * 7f;
            transform.rotation = Quaternion.Euler((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * s, yaw + (Mathf.PerlinNoise(0.7f, t) - 0.5f) * s, 0f);
            transform.position = basePos;
            return;
        }
        var pos = transform.position;
        float want = Mathf.Clamp(target.position.x + offsetX, minX, maxX);
        pos.x = Mathf.Lerp(pos.x, want, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        transform.position = pos;
        float fear = FearSystem.Instance != null ? FearSystem.Instance.fear : 0f;
        float sh = Mathf.InverseLerp(0.55f, 1f, fear) * fearShake;
        float tt = Time.time * 7f;
        transform.rotation = Quaternion.Euler((Mathf.PerlinNoise(tt, 0.3f) - 0.5f) * sh, (Mathf.PerlinNoise(0.7f, tt) - 0.5f) * sh, 0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var p = transform.position;
        Gizmos.DrawLine(new Vector3(minX, p.y, p.z), new Vector3(maxX, p.y, p.z));
    }
}
