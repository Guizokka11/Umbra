using System.Collections;
using UnityEngine;

/// <summary>
/// Susto curto: ao passar pela área, a luz pisca e algo aparece por um instante
/// (reflexo no espelho, silhueta atrás da porta, galhos no fim do corredor).
/// Opcionalmente o objeto atravessa a cena de A para B.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScareFlash : MonoBehaviour
{
    public GameObject target;
    public float seconds = 1.2f;
    public LightZone[] flickerLights;
    public float fear = 0.25f;
    [Tooltip("Se preenchido, o alvo se move de A para B enquanto aparece.")]
    public Transform from, to;
    [TextArea] public string subtitle;
    public string onceFlag;

    bool fired;

    void Reset() { GetComponent<Collider>().isTrigger = true; }
    void Awake() { if (target != null) target.SetActive(false); }

    void OnTriggerEnter(Collider other)
    {
        if (fired || !other.CompareTag("Player")) return;
        if (!string.IsNullOrEmpty(onceFlag) && GameFlags.Has(onceFlag)) return;
        fired = true;
        GameFlags.Set(onceFlag);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (FearSystem.Instance != null) FearSystem.Instance.AddFear(fear);
        if (!string.IsNullOrEmpty(subtitle)) Hud.Subtitle(subtitle, 2.5f);
        if (flickerLights != null) foreach (var l in flickerLights) if (l != null) l.SetOn(false);
        yield return new WaitForSeconds(0.15f);
        if (target != null) target.SetActive(true);

        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            if (target != null && from != null && to != null)
                target.transform.position = Vector3.Lerp(from.position, to.position, t / seconds);
            if (flickerLights != null && Random.value < 0.08f)
                foreach (var l in flickerLights) if (l != null) l.Toggle();
            yield return null;
        }
        if (target != null) target.SetActive(false);
        if (flickerLights != null) foreach (var l in flickerLights) if (l != null) l.SetOn(true);
    }
}
