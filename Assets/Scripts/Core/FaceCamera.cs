using UnityEngine;

/// <summary>Mantém o sprite virado para a câmera (só no giro horizontal). Usado na Luma e nas criaturas.</summary>
public class FaceCamera : MonoBehaviour
{
    Transform cam;

    void LateUpdate()
    {
        if (cam == null) { if (Camera.main == null) return; cam = Camera.main.transform; }
        Vector3 f = cam.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(f, Vector3.up);
    }
}
