using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Área que troca para uma câmera fixa enquanto a Luma está dentro dela.
/// Fora de todas as zonas, volta a câmera que segue a Luma.
///
/// Diferente do antigo CamSwitcher, não depende de OnTriggerEnter/Exit:
/// o CameraDirector verifica a posição da Luma todo frame. Isso evita
/// câmera presa depois de respawn, esconderijo ou teleporte, e resolve
/// zonas sobrepostas pela prioridade.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CameraZone : MonoBehaviour
{
    public static readonly List<CameraZone> All = new List<CameraZone>();

    [Tooltip("Câmera fixa desta área (CinemachineCamera).")]
    public CinemachineCamera zoneCamera;

    [Tooltip("Se duas zonas se sobrepõem, vence a de prioridade maior.")]
    public int priority = 0;

    [Header("Transição ao entrar")]
    [Tooltip("Corte seco em vez de transição suave.")]
    public bool cut = false;
    public float blendTime = 1.2f;

    [Header("Forçar por evento")]
    [Tooltip("Ligado = esta câmera fica ativa mesmo com a Luma fora da área (use ForceOn/ForceOff por evento).")]
    public bool forced = false;

    BoxCollider box;

    void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        if (zoneCamera != null) zoneCamera.Priority = 0;
    }

    void OnEnable()  { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool Contains(Vector3 worldPoint)
    {
        if (box == null) box = GetComponent<BoxCollider>();
        Vector3 local = transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public float Volume
    {
        get
        {
            if (box == null) box = GetComponent<BoxCollider>();
            Vector3 s = Vector3.Scale(box.size, transform.lossyScale);
            return Mathf.Abs(s.x * s.y * s.z);
        }
    }

    /// <summary>Liga esta câmera por evento (ex.: revelação de uma criatura).</summary>
    public void ForceOn()  => forced = true;
    public void ForceOff() => forced = false;

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (b == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.12f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.8f);
        Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.matrix = Matrix4x4.identity;
        if (zoneCamera != null)
        {
            Gizmos.DrawLine(transform.TransformPoint(b.center), zoneCamera.transform.position);
            Gizmos.DrawWireSphere(zoneCamera.transform.position, 0.3f);
        }
    }
}
