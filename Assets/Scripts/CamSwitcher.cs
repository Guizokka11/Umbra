using UnityEngine;
using Unity.Cinemachine;

public class CamSwitcher : MonoBehaviour
{
    [Tooltip("A CinemachineCamera desta sala/área.")]
    public CinemachineCamera activeCam;

    [Tooltip("Prioridade quando o jogador ESTÁ dentro da área.")]
    public int priorityActive = 15;

    [Tooltip("Prioridade quando o jogador NÃO está na área.")]
    public int priorityInactive = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            activeCam.Priority = priorityActive;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            activeCam.Priority = priorityInactive;
        }
    }
}
