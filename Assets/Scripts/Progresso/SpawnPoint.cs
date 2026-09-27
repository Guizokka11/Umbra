using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ponto de entrada da cena. A porta que leva até aqui usa o mesmo "id"
/// no campo "spawnId" do LevelExit/DoorExit. Ex.: "do_banheiro".
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    public static readonly List<SpawnPoint> All = new List<SpawnPoint>();

    public string id = "inicio";
    [Tooltip("A Luma entra olhando para a esquerda.")]
    public bool faceLeft;

    void OnEnable()  { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public static SpawnPoint Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var s in All) if (s != null && s.id == id) return s;
        // Awake pode rodar antes do OnEnable de outros objetos: procura na cena.
        foreach (var s in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
            if (s.id == id) return s;
        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.7f, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + (faceLeft ? Vector3.left : Vector3.right) * 0.8f);
    }
}
