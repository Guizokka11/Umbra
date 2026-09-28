using System;
using UnityEngine;

/// <summary>
/// A Inspetora como ameaça constante no 2º andar. Estado global (sobrevive à troca de cena e ao save):
/// ela está sempre em algum cômodo (flag "ronda_em:&lt;cena&gt;" no GameFlags) e troca de cômodo a cada
/// 40–90 s, sempre para um vizinho. Nasce sozinho ao dar Play; a configuração vem de
/// Assets/Dados/Resources/RondaDoAndar.asset.
///
/// O que acontece em cada cena é com o RondaNaCena da cena:
///  - ela está no cômodo da Luma: entra por uma porta (depois do aviso) e faz a ronda;
///  - ela está num vizinho: só se ouve pela porta (passos, galhos raspando);
///  - longe: nada.
/// Só começa depois do flag "banheiro_encontro" e pausa com "fuga_inspetora".
/// </summary>
public class RondaDoAndar : MonoBehaviour
{
    public const string PrefixoFlag = "ronda_em:";

    public static RondaDoAndar Instance { get; private set; }
    public RondaDoAndarConfig Config { get; private set; }

    /// <summary>(de, para) sempre que ela troca de cômodo.</summary>
    public event Action<string, string> OnTroca;

    float proximaTroca = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("Umbra_Ronda");
        DontDestroyOnLoad(go);
        go.AddComponent<RondaDoAndar>();
    }

    void Awake()
    {
        Instance = this;
        Config = Resources.Load<RondaDoAndarConfig>("RondaDoAndar");
        if (Config == null) Config = ScriptableObject.CreateInstance<RondaDoAndarConfig>();   // padrões do código
    }

    // ------------------------------------------------------------------ estado

    /// <summary>Já começou (flag de início) e não está pausada.</summary>
    public bool Ativa => Comecou && !Pausada;
    public bool Comecou => string.IsNullOrEmpty(Config.flagDeInicio) || GameFlags.Has(Config.flagDeInicio);
    public bool Pausada
    {
        get
        {
            if (Config.flagsQuePausam != null)
                foreach (var f in Config.flagsQuePausam) if (GameFlags.Has(f)) return true;
            return false;
        }
    }

    /// <summary>Cena onde ela está agora (nulo antes de começar).</summary>
    public string ComodoAtual
    {
        get
        {
            foreach (var c in Config.comodos) if (c != null && GameFlags.Has(PrefixoFlag + c.cena)) return c.cena;
            return null;
        }
    }

    public bool FazParte(string cena) => AcharComodo(cena) != null;

    public bool SaoVizinhos(string a, string b)
    {
        var c = AcharComodo(a);
        if (c == null || c.vizinhos == null) return false;
        foreach (var v in c.vizinhos) if (v == b) return true;
        return false;
    }

    RondaDoAndarConfig.Comodo AcharComodo(string cena)
    {
        foreach (var c in Config.comodos) if (c != null && c.cena == cena) return c;
        return null;
    }

    void Definir(string cena)
    {
        foreach (var c in Config.comodos) if (c != null && c.cena != cena) GameFlags.Set(PrefixoFlag + c.cena, false);
        GameFlags.Set(PrefixoFlag + cena, true);
    }

    // ------------------------------------------------------------------ tempo

    /// <summary>Garante que ela fica pelo menos "segundos" onde está (ex.: acabou de entrar no cômodo da Luma).</summary>
    public void FicarPeloMenos(float segundos) => proximaTroca = Mathf.Max(proximaTroca, Time.time + segundos);

    /// <summary>Adia a próxima troca (ex.: ela está perseguindo; não pode sumir no meio).</summary>
    public void Adiar(float segundos) => proximaTroca = Time.time + segundos;

    void Update()
    {
        if (!Ativa) return;
        if (ComodoAtual == null)
        {
            Definir(Config.comodoInicial);
            proximaTroca = Time.time + Sorteio(Config.intervaloDeTroca);
            return;
        }
        if (proximaTroca < 0f) proximaTroca = Time.time + Sorteio(Config.intervaloDeTroca);
        if (Time.time < proximaTroca) return;

        // Não troca no meio de uma perseguição/farejada no cômodo da Luma.
        var cena = RondaNaCena.Atual;
        if (cena != null && !cena.PodeSair()) { Adiar(4f); return; }

        MoverParaVizinho();
    }

    /// <summary>Vai para um vizinho sorteado (evitando "evitar", se houver outra opção).</summary>
    public void MoverParaVizinho(string evitar = null)
    {
        var atual = AcharComodo(ComodoAtual);
        if (atual == null || atual.vizinhos == null || atual.vizinhos.Length == 0) return;
        string para = atual.vizinhos[UnityEngine.Random.Range(0, atual.vizinhos.Length)];
        if (para == evitar && atual.vizinhos.Length > 1)
            foreach (var v in atual.vizinhos) if (v != evitar) { para = v; break; }
        string de = atual.cena;
        Definir(para);
        proximaTroca = Time.time + Sorteio(Config.intervaloDeTroca);
        OnTroca?.Invoke(de, para);
    }

    /// <summary>Leva a ronda para um cômodo agora (eventos de roteiro e testes). Com a Luma lá, ela entra com o aviso.</summary>
    public void IrPara(string cena)
    {
        if (!FazParte(cena)) return;
        string de = ComodoAtual;
        if (de == cena) return;
        Definir(cena);
        proximaTroca = Time.time + Sorteio(Config.intervaloDeTroca);
        OnTroca?.Invoke(de, cena);
    }

    static float Sorteio(Vector2 faixa) => UnityEngine.Random.Range(faixa.x, Mathf.Max(faixa.x, faixa.y));
}
