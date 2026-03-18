using UnityEngine;

public class BillboardSprite : MonoBehaviour
{
    private Camera cam;
    void Start() => cam = Camera.main;

    void LateUpdate()
    {
        transform.LookAt(
            transform.position + cam.transform.rotation * Vector3.forward,
            cam.transform.rotation * Vector3.up
        );
    }
}