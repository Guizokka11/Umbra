using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área iluminada = área segura. Usar um Collider marcado como Trigger
/// cobrindo a região que a luz alcança. Pode ser ligada a uma Light real,
/// que é acesa/apagada junto (ex.: lâmpada piscando, interruptor, vela).
/// Criaturas com "avoidsLight" não entram nessas áreas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LightZone : MonoBehaviour
{
    static readonly List<LightZone> zones = new List<LightZone>();
    /// <summary>Todas as zonas de luz ativas (usado pelo véu de escuridão).</summary>
    public static IReadOnlyList<LightZone> All => zones;
    [Tooltip("Força do buraco de luz no véu de escuridão (0 = não ilumina a tela).")]
    [Range(0f, 1f)] public float screenLight = 1f;

    [Tooltip("Light real opcional, ligada/desligada junto com a zona.")]
    public Light linkedLight;
    [Tooltip("Objetos ligados/desligados junto com a luz (ex.: camada 'luz' do PSD, facho pintado, brilho da lâmpada).")]
    public GameObject[] linkedVisuals;
    [Tooltip("Objetos que aparecem quando a luz APAGA (ex.: camada 'sombra geral' do PSD).")]
    public GameObject[] visualsWhenOff;
    public bool isOn = true;

    [Header("Piscar (opcional)")]
    public bool flicker = false;
    public Vector2 onTimeRange  = new Vector2(2f, 5f);
    public Vector2 offTimeRange = new Vector2(0.1f, 0.6f);

    Collider col;
    float nextToggle;

    void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        Apply();
    }

    void OnEnable()  { zones.Add(this); }
    void OnDisable() { zones.Remove(this); }

    void Update()
    {
        if (!flicker || Time.time < nextToggle) return;
        SetOn(!isOn);
        Vector2 r = isOn ? onTimeRange : offTimeRange;
        nextToggle = Time.time + Random.Range(r.x, r.y);
    }

    public void SetOn(bool on) { isOn = on; Apply(); }
    public void Toggle() => SetOn(!isOn);

    void Apply()
    {
        if (linkedLight != null) linkedLight.enabled = isOn;
        if (linkedVisuals != null) foreach (var v in linkedVisuals) if (v != null) v.SetActive(isOn);
        if (visualsWhenOff != null) foreach (var v in visualsWhenOff) if (v != null) v.SetActive(!isOn);
    }

    public bool Contains(Vector3 p)
    {
        if (!isOn || col == null || !col.enabled) return false;
        return (col.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;
    }

    /// <summary>Algum ponto está iluminado por alguma LightZone ligada?</summary>
    public static bool IsPointLit(Vector3 p)
    {
        for (int i = 0; i < zones.Count; i++)
            if (zones[i].Contains(p)) return true;
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isOn ? new Color(1f, 0.9f, 0.4f, 0.15f) : new Color(0.3f, 0.3f, 0.3f, 0.1f);
        var c = GetComponent<Collider>();
        if (c != null) Gizmos.DrawCube(c.bounds.center, c.bounds.size);
    }
}
