using UnityEngine;

/// <summary>
/// Trigger que salva o ponto de respawn quando a Luma passa.
/// Colocar antes de cada encontro com criatura ou puzzle sob pressão.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Onde a Luma reaparece. Se vazio, usa a posição deste objeto.")]
    public Transform spawnPoint;
    public bool onlyOnce = true;

    bool used;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if ((onlyOnce && used) || !other.CompareTag("Player") || GameManager.Instance == null) return;
        used = true;
        GameManager.Instance.SetCheckpoint(spawnPoint != null ? spawnPoint.position : transform.position);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnPoint != null ? spawnPoint.position : transform.position, 0.3f);
    }
}
