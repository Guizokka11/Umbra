using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Susto. Ao passar pela área, em três tempos:
///  1. CALMA: o ambiente abaixa por 1–3 s (o silêncio antes);
///  2. SUSTO: aparição rápida + stinger alto + tremor curto de câmera + vinheta fechando;
///  3. DEPOIS: a Luma puxa o ar, respiração acelerada e medo alto por alguns segundos.
/// Tipos:
///  - Aparição: algo aparece por um instante (galhos na escada, livro que cai), podendo ir de A para B;
///  - Falso: só um barulho, nada aparece (use antes de um susto de verdade);
///  - Espelho: a Luma vê o próprio reflexo; a criatura aparece ATRÁS dela no reflexo; a Luma se vira e não há
///    nada na sala; pouco depois a luz pisca uma vez.
/// Regras: sustos não mostram legenda; no máximo UM susto grande por cena, e só depois de um trecho calmo
/// (sem perseguição, medo baixo, alguns segundos sem outro susto); cada susto acontece uma vez (onceFlag).
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScareFlash : MonoBehaviour
{
    public enum Tipo { Aparicao, Falso, Espelho }

    [Header("Tipo")]
    public Tipo tipo = Tipo.Aparicao;
    [Tooltip("Susto grande: no máximo um por cena. Sustos falsos não contam.")]
    public bool grande = true;

    [Header("O que aparece")]
    public GameObject target;
    [Tooltip("Quanto tempo a aparição fica na tela (rápido assusta mais).")]
    public float seconds = 1.2f;
    [Tooltip("Se preenchido, o alvo se move de A para B enquanto aparece.")]
    public Transform from, to;
    public LightZone[] flickerLights;

    [Header("1. Calma antes")]
    [Tooltip("Segundos com o ambiente abaixado antes do susto (mínimo, máximo).")]
    public Vector2 calma = new Vector2(1f, 3f);

    [Header("2. Susto")]
    [Tooltip("Stinger deste susto. Vazio = o AudioManager sorteia um dos stingers gerais.")]
    public AudioClip stinger;
    [Tooltip("Volume do stinger (1 = normal do AudioManager). Susto grande: alto.")]
    public float volumeDoStinger = 1.3f;
    [Tooltip("O ambiente fica abaixado também durante o susto por este tempo.")]
    public float silenciarAmbiente = 1.5f;
    [Tooltip("Tremor da câmera (m). 0 = sem tremor.")]
    public float tremor = 0.12f;
    public float duracaoDoTremor = 0.35f;
    [Tooltip("Quanto a vinheta fecha na hora (0 a 1).")]
    public float vinheta = 0.35f;
    [Tooltip("Medo somado na hora.")]
    public float fear = 0.25f;

    [Header("3. Depois")]
    [Tooltip("Medo mínimo logo depois (respiração acelerada).")]
    [Range(0f, 1f)] public float medoDepois = 0.75f;
    public float segundosDepois = 5f;

    [Header("Regras")]
    [Tooltip("Só dispara com a Luma calma (sem perseguição, medo baixo, sem susto recente). Se não estiver, espera ela ficar calma dentro da área.")]
    public bool exigirCalma = true;
    [Range(0f, 1f)] public float medoMaximoParaDisparar = 0.6f;
    [Tooltip("Segundos de calma exigidos (desde a entrada na cena e desde o último susto).")]
    public float calmaMinima = 8f;
    [Tooltip("Flag para não repetir. Vazio = um flag automático com a cena e o nome deste objeto.")]
    public string onceFlag;

    [Header("Espelho (só no tipo Espelho)")]
    [Tooltip("Centro do vidro do espelho.")]
    public Transform espelhoCentro;
    [Tooltip("Largura e altura do vidro (m).")]
    public Vector2 espelhoTamanho = new Vector2(2.6f, 1.1f);
    [Tooltip("Material \"Umbra/Sprite Reflexo\" (Assets/Dados/Materiais/SpriteReflexo.mat).")]
    public Material materialDoReflexo;
    [Tooltip("Mostra o reflexo da Luma no espelho quando ela passa (antes e durante o susto).")]
    public bool reflexoDaLuma = true;
    [Tooltip("Depois que ela se vira: segundos até a luz piscar uma vez.")]
    public float luzPiscaDepois = 2f;

    [HideInInspector, Tooltip("Sustos não mostram legenda (limpe com Umbra > Terror > Tirar legendas dos sustos).")]
    [TextArea] public string subtitle;

    // ------------------------------------------------------------------ regras globais

    static bool grandeNestaCena;
    static float ultimoSusto = -99f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => SceneManager.sceneLoaded += (s, m) => grandeNestaCena = false;

    bool fired, dentro;
    float rearmarEm;
    Material matReflexo;
    SpriteRenderer reflexoLuma;
    Vector4 vidro;

    string Flag => !string.IsNullOrEmpty(onceFlag) ? onceFlag : "susto:" + gameObject.scene.name + "/" + name;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    /// <summary>Testes: apaga os flags dos sustos da cena e zera as regras (um grande por cena, calma).</summary>
    public static int RearmarDaCena()
    {
        int n = 0;
        foreach (var s in FindObjectsByType<ScareFlash>(FindObjectsSortMode.None))
        {
            GameFlags.Set(s.Flag, false);
            s.fired = false;
            s.rearmarEm = 0f;
            n++;
        }
        grandeNestaCena = false;
        ultimoSusto = -99f;
        return n;
    }

    void OnValidate() { if (tipo == Tipo.Falso) grande = false; }

    void Awake()
    {
        if (target != null) target.SetActive(false);
        if (tipo == Tipo.Espelho) PrepararEspelho();
    }

    void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) { dentro = true; Tentar(); } }
    void OnTriggerStay(Collider other)  { if (other.CompareTag("Player")) Tentar(); }
    void OnTriggerExit(Collider other)  { if (other.CompareTag("Player")) dentro = false; }

    void Tentar()
    {
        if (fired || Time.time < rearmarEm || GameFlags.Has(Flag) || !PodeDisparar()) return;
        fired = true;
        StartCoroutine(tipo == Tipo.Espelho ? Espelho() : Run());
    }

    bool PodeDisparar()
    {
        if (grande && tipo != Tipo.Falso && grandeNestaCena) return false;          // um susto grande por cena
        var st = PlayerState.Instance;
        if (st == null || st.isDead || st.isHidden) return false;
        if (!exigirCalma) return true;
        if (Time.timeSinceLevelLoad < calmaMinima || Time.time - ultimoSusto < calmaMinima) return false;
        if (FearSystem.Instance != null && FearSystem.Instance.fear > medoMaximoParaDisparar) return false;
        foreach (var c in CreatureAI.Active) if (c != null && c.State == CreatureState.Chase) return false;
        foreach (var m in InspetoraMass.Todas) if (m != null && m.EstaPerseguindo) return false;
        return true;
    }

    /// <summary>Marca o susto como acontecido (só na hora do susto: se a Luma sair antes, ele fica armado).</summary>
    void Aconteceu()
    {
        GameFlags.Set(Flag);
        if (grande && tipo != Tipo.Falso) grandeNestaCena = true;
        ultimoSusto = Time.time;
    }

    // ------------------------------------------------------------------ aparição / falso

    IEnumerator Run()
    {
        // 1. Calma: o ambiente some antes.
        float c = Random.Range(calma.x, Mathf.Max(calma.x, calma.y));
        if (AudioManager.Instance != null) AudioManager.Instance.Silencio(c + silenciarAmbiente);
        yield return new WaitForSeconds(c);
        Aconteceu();

        if (tipo == Tipo.Falso)
        {
            // Só o barulho: nada aparece, tremor e medo pequenos.
            if (AudioManager.Instance != null) AudioManager.Instance.TocarStinger(stinger, 0f, volumeDoStinger);
            if (FearSystem.Instance != null) FearSystem.Instance.AddFear(fear * 0.5f);
            yield break;
        }

        // 2. Susto.
        var luzes = GuardarLuzes();
        foreach (var l in luzes.Keys) l.SetOn(false);
        yield return new WaitForSeconds(0.08f);
        if (target != null) target.SetActive(true);
        Impacto();

        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            if (target != null && from != null && to != null)
                target.transform.position = Vector3.Lerp(from.position, to.position, t / seconds);
            if (Random.value < 0.08f) foreach (var l in luzes.Keys) l.Toggle();
            yield return null;
        }
        if (target != null) target.SetActive(false);
        RestaurarLuzes(luzes);

        // 3. Depois.
        Depois();
    }

    void Impacto()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.TocarStinger(stinger, 0f, volumeDoStinger);
        TremorDeCamera.Tremer(tremor, duracaoDoTremor);
        if (FearSystem.Instance != null)
        {
            FearSystem.Instance.AddFear(fear);
            FearSystem.Instance.PulsoDeVinheta(vinheta);
        }
    }

    void Depois()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.Arfar();
        if (FearSystem.Instance != null) FearSystem.Instance.SegurarMedo(medoDepois, segundosDepois);
    }

    Dictionary<LightZone, bool> GuardarLuzes()
    {
        var d = new Dictionary<LightZone, bool>();
        if (flickerLights != null) foreach (var l in flickerLights) if (l != null && !d.ContainsKey(l)) d[l] = l.isOn;
        return d;
    }

    static void RestaurarLuzes(Dictionary<LightZone, bool> luzes)
    {
        foreach (var kv in luzes) if (kv.Key != null) kv.Key.SetOn(kv.Value);
    }

    // ------------------------------------------------------------------ espelho

    void PrepararEspelho()
    {
        if (espelhoCentro == null) return;
        Vector3 c = espelhoCentro.position;
        vidro = new Vector4(c.x - espelhoTamanho.x * 0.5f, c.x + espelhoTamanho.x * 0.5f,
                            c.y - espelhoTamanho.y * 0.5f, c.y + espelhoTamanho.y * 0.5f);
        var baseMat = materialDoReflexo;
        if (baseMat == null)
        {
            var sh = Shader.Find("Umbra/Sprite Reflexo");
            if (sh != null) baseMat = new Material(sh);
        }
        if (baseMat == null) { Debug.LogWarning("[Umbra] Susto do espelho sem material de reflexo.", this); return; }
        matReflexo = new Material(baseMat);
        matReflexo.SetVector("_Espelho", vidro);

        // A criatura só existe dentro do vidro.
        if (target != null)
            foreach (var sr in target.GetComponentsInChildren<SpriteRenderer>(true)) sr.sharedMaterial = matReflexo;

        if (reflexoDaLuma)
        {
            var go = new GameObject("Reflexo da Luma");
            go.transform.SetParent(transform, false);
            reflexoLuma = go.AddComponent<SpriteRenderer>();
            reflexoLuma.sharedMaterial = matReflexo;
            reflexoLuma.sortingOrder = 200;                  // na frente da criatura no reflexo
            reflexoLuma.enabled = false;
        }
    }

    SpriteRenderer SpriteDaLuma()
    {
        var st = PlayerState.Instance;
        return st != null ? st.GetComponentInChildren<SpriteRenderer>() : null;
    }

    bool LumaNoEspelho(float margem = 0.3f)
    {
        var st = PlayerState.Instance;
        if (st == null || espelhoCentro == null) return false;
        float x = st.transform.position.x;
        return x > vidro.x - margem && x < vidro.y + margem;
    }

    void LateUpdate()
    {
        if (reflexoLuma == null) return;
        var sr = SpriteDaLuma();
        bool mostra = sr != null && sr.enabled && sr.sprite != null && LumaNoEspelho(0.8f);
        reflexoLuma.enabled = mostra;
        if (!mostra) return;
        // O reflexo fica no plano do vidro, um pouco menor (está "mais longe"), só a parte de cima aparece.
        var st = PlayerState.Instance;
        float pivo = sr.transform.position.y - st.transform.position.y;
        reflexoLuma.sprite = sr.sprite;
        reflexoLuma.flipX = sr.flipX;
        reflexoLuma.transform.position = new Vector3(sr.transform.position.x, vidro.z - 0.35f + pivo * 0.9f, espelhoCentro.position.z - 0.01f);
        reflexoLuma.transform.rotation = Quaternion.identity;
        reflexoLuma.transform.localScale = sr.transform.lossyScale * 0.9f;
    }

    IEnumerator Espelho()
    {
        var st = PlayerState.Instance;
        // 1. Calma: chegando ao espelho o ambiente abaixa (a área cobre a aproximação); ela se vê no vidro.
        float c = Random.Range(calma.x, Mathf.Max(calma.x, calma.y));
        if (AudioManager.Instance != null) AudioManager.Instance.Silencio(c + 6f);
        yield return new WaitForSeconds(c);
        // O susto é quando ela estiver na frente do vidro. Se for embora antes, o susto fica armado para a próxima vez.
        float limite = Time.time + 6f;
        while (st != null && !(LumaNoEspelho(-0.45f) && !st.isHidden && !st.isDead))      // bem na frente do vidro
        {
            if (!dentro || Time.time > limite) { fired = false; rearmarEm = Time.time + 3f; yield break; }
            yield return null;
        }
        if (st == null) { fired = false; yield break; }
        Aconteceu();

        // 2. Susto: a Inspetora ATRÁS dela no reflexo.
        var mv = st.Movement;
        float lado = mv != null && mv.facingRight ? 1f : -1f;
        if (target != null)
        {
            float x = st.transform.position.x - lado * 0.35f;                        // atrás, do lado de onde ela veio
            x = Mathf.Clamp(x, vidro.x + 0.3f, vidro.y - 0.3f);                      // sempre dentro do vidro
            target.SetActive(true);
            // Pelo centro do desenho (o pivô do sprite pode estar em qualquer lugar): ela enche o vidro, atrás da Luma.
            Vector3 alvo = new Vector3(x, (vidro.z + vidro.w) * 0.5f + 0.1f, espelhoCentro.position.z - 0.005f);
            var r = target.GetComponentInChildren<SpriteRenderer>();
            Vector3 centro = r != null ? r.bounds.center : target.transform.position;
            target.transform.position += new Vector3(alvo.x - centro.x, alvo.y - centro.y, alvo.z - target.transform.position.z);
        }
        Impacto();
        yield return new WaitForSeconds(Mathf.Max(0.2f, seconds));

        // A Luma se vira... e não há nada na sala.
        if (target != null) target.SetActive(false);
        bool podiaAndar = mv != null && mv.canMove;
        if (mv != null)
        {
            mv.facingRight = !mv.facingRight;
            mv.canMove = false;
        }
        yield return new WaitForSeconds(1.1f);
        if (mv != null && podiaAndar && !st.isDead) mv.canMove = true;

        // 3. Depois: respiração acelerada; 2 s depois de se virar, a luz pisca uma vez.
        Depois();
        yield return new WaitForSeconds(Mathf.Max(0f, luzPiscaDepois - 1.1f));
        var luzes = GuardarLuzes();
        foreach (var l in luzes.Keys) l.SetOn(false);
        yield return new WaitForSeconds(0.14f);
        RestaurarLuzes(luzes);
    }

    void OnDrawGizmosSelected()
    {
        if (tipo != Tipo.Espelho || espelhoCentro == null) return;
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(espelhoCentro.position, new Vector3(espelhoTamanho.x, espelhoTamanho.y, 0.02f));
    }
}
