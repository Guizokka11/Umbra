using UnityEngine;

/// <summary>Brilho que pulsa devagar em objetos que a Luma pode pegar/ler.</summary>
public class GlowPulse : MonoBehaviour
{
    public float speed = 2f;
    public Vector2 alpha = new Vector2(0.25f, 0.8f);
    SpriteRenderer sr;
    Vector3 baseScale;

    void Awake() { sr = GetComponent<SpriteRenderer>(); baseScale = transform.localScale; }

    void Update()
    {
        float k = (Mathf.Sin(Time.time * speed + transform.position.x) + 1f) * 0.5f;
        if (sr != null) { var c = sr.color; c.a = Mathf.Lerp(alpha.x, alpha.y, k); sr.color = c; }
        transform.localScale = baseScale * Mathf.Lerp(0.9f, 1.1f, k);
    }
}
