using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// PRÓLOGO: O PESADELO.
/// A figura da Amelie se afasta mantendo sempre a mesma distância da Luma,
/// não importa o quanto ela corra. Depois de um tempo (ou ao atingir uma
/// posição), Amelie se vira, diz a frase incompleta, a escuridão toma o
/// corredor e a cena termina (onWakeUp: carregar o dormitório / LevelExit).
/// Colocar no objeto da figura da Amelie. O corredor deve seguir para +X.
/// </summary>
public class NightmareCorridor : MonoBehaviour
{
    public float keepDistance = 6f;
    public float minWalkSpeed = 1.2f;
    [Tooltip("Tempo de corrida até o fim do pesadelo.")]
    public float duration = 20f;

    [Header("Final")]
    public SpriteRenderer ameliesprite;
    public TMP_Text subtitle;
    [TextArea] public string lastLine = "Eu volto antes de você...";
    public float lineTime = 2.5f;
    [Tooltip("Luzes do corredor apagadas uma a uma no final.")]
    public LightZone[] corridorLights;

    public UnityEvent onWakeUp;

    Transform player;
    float t;
    bool ending;

    void Start()
    {
        if (PlayerState.Instance != null) player = PlayerState.Instance.transform;
        if (subtitle != null) subtitle.text = "";
    }

    void Update()
    {
        if (player == null || ending) return;
        t += Time.deltaTime;

        // O corredor segue para +X: Amelie sempre fica "keepDistance" à frente.
        float targetX = Mathf.Max(player.position.x + keepDistance, transform.position.x);
        float speed = Mathf.Max(minWalkSpeed, (targetX - transform.position.x) * 4f);
        float x = Mathf.MoveTowards(transform.position.x, targetX + minWalkSpeed * Time.deltaTime, speed * Time.deltaTime);
        transform.position = new Vector3(x, transform.position.y, transform.position.z);

        if (t >= duration) StartCoroutine(End());
    }

    IEnumerator End()
    {
        ending = true;
        var st = PlayerState.Instance;
        if (st != null) st.Movement.canMove = false;

        if (ameliesprite != null) ameliesprite.flipX = !ameliesprite.flipX; // se vira uma vez
        if (subtitle != null) subtitle.text = lastLine;
        else Hud.Subtitle(lastLine, lineTime);
        yield return new WaitForSeconds(lineTime);
        if (subtitle != null) subtitle.text = "";

        if (corridorLights != null)
            for (int i = corridorLights.Length - 1; i >= 0; i--)
            {
                corridorLights[i].SetOn(false);
                yield return new WaitForSeconds(0.25f);
            }

        if (ScreenFader.Instance != null) yield return ScreenFader.Instance.FadeOut(0.5f);
        onWakeUp.Invoke();
    }
}
