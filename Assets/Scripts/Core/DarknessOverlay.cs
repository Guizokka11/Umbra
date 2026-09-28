using UnityEngine;

/// <summary>
/// Véu de escuridão estilo Little Nightmares: cobre a tela e escurece tudo (inclusive a Luma),
/// com "buracos" suaves onde há luz acesa (LightZones). Quando uma luz apaga, o escuro volta.
/// Criado sozinho pelo montador das cenas, preso na câmera principal.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DarknessOverlay : MonoBehaviour
{
    [Range(0f, 1f)] public float darkness = 0.7f;
    [Tooltip("Luzinha em volta da Luma para ela nunca sumir por completo.")]
    public float lumaAura = 0.9f;
    [Range(0f, 1f)] public float lumaAuraStrength = 0.35f;
    [Tooltip("Com medo alto o escuro fecha mais.")]
    public float fearExtra = 0.15f;
    public Material material;

    Camera cam;
    Transform quad;
    readonly Vector4[] lights = new Vector4[16];

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (material == null)
        {
            var sh = Shader.Find("Umbra/Escuridao");
            if (sh == null) { enabled = false; return; }
            material = new Material(sh);
        }
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Veu_Escuridao";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        float d = cam.nearClipPlane + 0.05f;
        go.transform.localPosition = new Vector3(0, 0, d);
        float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
        go.transform.localScale = new Vector3(h * 4f, h * 4f, 1f);   // grande: cobre a tela mesmo com lens shift
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quad = go.transform;
    }

    void LateUpdate()
    {
        if (material == null) return;
        int n = 0;
        float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        foreach (var z in LightZone.All)
        {
            if (n >= 15) break;
            if (z == null || !z.isOn || z.screenLight <= 0f) continue;
            var col = z.GetComponent<Collider>();
            Vector3 center = z.linkedLight != null ? z.linkedLight.transform.position : z.transform.position + Vector3.up * 1.5f;
            float radius = col != null ? Mathf.Max(col.bounds.extents.x, 0.5f) * 1.1f : 2f;
            if (!Add(ref n, center, radius, z.screenLight, tanV)) continue;
            // Segundo ponto no chão: a luz "desce" até onde a Luma pisa.
            if (col != null && n < 15) Add(ref n, new Vector3(center.x, col.bounds.min.y + 0.8f, col.bounds.center.z), radius * 0.9f, z.screenLight, tanV);
        }

        var st = PlayerState.Instance;
        if (st != null && n < 16)
            Add(ref n, st.transform.position + Vector3.up * 0.5f, lumaAura, lumaAuraStrength, tanV);

        float fear = FearSystem.Instance != null ? FearSystem.Instance.fear : 0f;
        float perigo = MedoDoEscuro.Instance != null ? MedoDoEscuro.Instance.VeuExtra : 0f;   // escuro perigoso: fecha mais
        material.SetFloat("_Darkness", Mathf.Clamp01(darkness + fear * fearExtra + perigo));
        material.SetVectorArray("_Lights", lights);
        material.SetFloat("_LightCount", n);
        material.SetFloat("_Aspect", cam.aspect);
    }

    bool Add(ref int n, Vector3 world, float radius, float strength, float tanV)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z <= 0.01f) return false;
        float r = radius / (2f * vp.z * tanV);          // raio em "alturas de tela"
        lights[n++] = new Vector4(vp.x, vp.y, r, strength);
        return true;
    }
}
