using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Umbra > Terror > Preparar ronda na cena aberta
/// Para as cenas do 2º andar (02_Corredor1, 03_Banheiro, 06_Dormitorio2). Adiciona só o que falta, com Undo:
///  - o asset de configuração Assets/Dados/Resources/RondaDoAndar.asset (sons vazios);
///  - "Ronda do andar" (RondaNaCena) com:
///     · a Inspetora da ronda: reaproveita a Inspetora da cena que não é de um encontro roteirizado
///       (ex.: a do Dormitório 2); senão cria "Inspetora (ronda do andar)" com o sprite da Inspetora;
///     · uma "Porta da ronda" em cada porta para um cômodo vizinho (onde ela entra/sai e de onde vem o som),
///       com o caminho da sombra na parede;
///     · pontos de ronda (copia os da Inspetora da cena, se houver; senão espalha pelo cômodo).
///  - SequenciaDeCaptura e SomDaCriatura na Inspetora.
/// Pode rodar de novo sem duplicar. Não mexe no cenário nem em nada que já existia.
/// </summary>
public static class UmbraRonda
{
    const string ConfigPath = "Assets/Dados/Resources/RondaDoAndar.asset";
    const string SpriteInspetora = "Assets/Sprites/Criaturas/Inspetora.png";
    const string NomeRaiz = "Ronda do andar";

    [MenuItem("Umbra/Terror/Preparar ronda na cena aberta", priority = 101)]
    static void Preparar()
    {
        var cfg = Config(out bool cfgNova);
        var cena = EditorSceneManager.GetActiveScene();
        RondaDoAndarConfig.Comodo comodo = null;
        foreach (var c in cfg.comodos) if (c != null && c.cena == cena.name) comodo = c;
        if (comodo == null)
        {
            EditorUtility.DisplayDialog("Umbra — ronda",
                "A cena " + cena.name + " não faz parte da ronda do andar.\n\nCômodos da ronda (em " + ConfigPath + "): " +
                string.Join(", ", System.Array.ConvertAll(cfg.comodos, c => c.cena)), "OK");
            return;
        }

        var rel = new List<string>();
        if (cfgNova) rel.Add("+ configuração " + ConfigPath);
        Undo.SetCurrentGroupName("Preparar ronda");
        int grupo = Undo.GetCurrentGroup();

        // Raiz
        var ronda = Object.FindAnyObjectByType<RondaNaCena>(FindObjectsInactive.Include);
        if (ronda == null)
        {
            var go = new GameObject(NomeRaiz);
            Undo.RegisterCreatedObjectUndo(go, "Ronda do andar");
            ronda = go.AddComponent<RondaNaCena>();
            rel.Add("+ \"" + NomeRaiz + "\"");
        }
        Undo.RecordObject(ronda, "Ronda do andar");
        var raiz = ronda.transform;
        var luma = Object.FindAnyObjectByType<PlayerState>(FindObjectsInactive.Include);
        Vector3 centro = luma != null ? luma.transform.position : raiz.position;

        // Inspetora
        CreatureAI modelo = null;                       // outra Inspetora da cena (ex.: a do encontro do banheiro)
        if (ronda.inspetora == null)
        {
            var usada = new HashSet<CreatureAI>();
            foreach (var enc in Object.FindObjectsByType<ScriptedEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (enc.creature != null) usada.Add(enc.creature);
            CreatureAI existente = null, qualquer = null;
            foreach (var ai in Object.FindObjectsByType<CreatureAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ai.name.StartsWith("Inspetora")) continue;
                qualquer = qualquer != null ? qualquer : ai;
                if (!usada.Contains(ai)) existente = ai;
            }
            if (existente != null)
            {
                ronda.inspetora = existente;
                rel.Add("= usa a Inspetora da cena: \"" + existente.name + "\" (fica sumida enquanto a ronda não a trouxer)");
            }
            else
            {
                modelo = qualquer;
                ronda.inspetora = CriarInspetora(raiz, qualquer, centro);
                rel.Add("+ \"" + ronda.inspetora.name + "\"");
            }
        }
        var ins = ronda.inspetora;
        if (ins.GetComponent<SequenciaDeCaptura>() == null) { Undo.AddComponent<SequenciaDeCaptura>(ins.gameObject); rel.Add("+ SequenciaDeCaptura na Inspetora"); }
        if (ins.GetComponent<SomDaCriatura>() == null) { Undo.AddComponent<SomDaCriatura>(ins.gameObject); rel.Add("+ SomDaCriatura na Inspetora"); }

        // Portas para os vizinhos
        var portas = new List<PortaDaRonda>(raiz.GetComponentsInChildren<PortaDaRonda>(true));
        foreach (var door in Object.FindObjectsByType<DoorExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string viz = door.nextScene;
            if (string.IsNullOrEmpty(viz) || System.Array.IndexOf(comodo.vizinhos, viz) < 0) continue;
            if (portas.Exists(p => p.comodoVizinho == viz)) continue;
            var p = CriarPorta(raiz, door, viz, centro);
            portas.Add(p);
            rel.Add("+ porta da ronda → " + viz + " (em \"" + door.name + "\")");
        }
        ronda.portas = portas.ToArray();
        foreach (var viz in comodo.vizinhos)
            if (!portas.Exists(p => p.comodoVizinho == viz))
                rel.Add("! sem porta para " + viz + " nesta cena: crie um filho com PortaDaRonda à mão, se ela deve entrar por lá");

        // Pontos de ronda
        if (ronda.pontosDeRonda == null || ronda.pontosDeRonda.Length == 0)
        {
            var fonte = ins.waypoints != null && ins.waypoints.Length > 0 ? ins.waypoints
                      : modelo != null && modelo.waypoints != null && modelo.waypoints.Length > 0 ? modelo.waypoints : null;
            if (fonte != null)
            {
                ronda.pontosDeRonda = (Transform[])fonte.Clone();
                rel.Add("= pontos de ronda: os " + fonte.Length + " já feitos para a Inspetora da cena");
            }
            else
            {
                ronda.pontosDeRonda = CriarPontos(raiz, centro);
                rel.Add("+ " + ronda.pontosDeRonda.Length + " pontos de ronda espalhados (ajuste à mão se precisar)");
            }
        }

        EditorUtility.SetDirty(ronda);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cena);
        if (rel.Count == 0) rel.Add("= já estava tudo pronto");
        string texto = "Cena " + cena.name + ":\n\n" + string.Join("\n", rel) +
                       "\n\nConfira no Scene View as portas (caixa roxa) e o caminho da sombra (linha preta).\nSalve a cena (Ctrl+S).";
        Debug.Log("[Umbra] Ronda: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — ronda", texto, "OK");
    }

    // ------------------------------------------------------------------ testes (no Play)

    [MenuItem("Umbra/Terror/Testar ronda: trazer a Inspetora para esta cena (Play)", priority = 102)]
    static void TesteTrazer()
    {
        var r = RondaDoAndar.Instance;
        if (!Application.isPlaying || r == null) { EditorUtility.DisplayDialog("Umbra — ronda", "Use no Play.", "OK"); return; }
        string aqui = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!r.Comecou) GameFlags.Set(r.Config.flagDeInicio);                  // pula o encontro do banheiro
        foreach (var c in r.Config.comodos)                                     // parte de um vizinho, para entrar pela porta dele
            if (c.cena != aqui && r.SaoVizinhos(aqui, c.cena)) { r.IrPara(c.cena); break; }
        r.IrPara(aqui);
        Debug.Log("[Umbra] Ronda (teste): a Inspetora está vindo para " + aqui + ". Aviso de " + r.Config.tempoDeAviso.x + "–" + r.Config.tempoDeAviso.y + " s.");
    }

    [MenuItem("Umbra/Terror/Testar ronda: mandar a Inspetora para um vizinho (Play)", priority = 103)]
    static void TesteMandar()
    {
        var r = RondaDoAndar.Instance;
        if (!Application.isPlaying || r == null) { EditorUtility.DisplayDialog("Umbra — ronda", "Use no Play.", "OK"); return; }
        if (!r.Comecou) GameFlags.Set(r.Config.flagDeInicio);
        string aqui = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (r.ComodoAtual == null) r.IrPara(aqui);
        r.MoverParaVizinho();
        Debug.Log("[Umbra] Ronda (teste): a Inspetora foi para " + r.ComodoAtual + ".");
    }

    // ------------------------------------------------------------------ partes

    static RondaDoAndarConfig Config(out bool nova)
    {
        nova = false;
        var cfg = AssetDatabase.LoadAssetAtPath<RondaDoAndarConfig>(ConfigPath);
        if (cfg != null) return cfg;
        if (!AssetDatabase.IsValidFolder("Assets/Dados")) AssetDatabase.CreateFolder("Assets", "Dados");
        if (!AssetDatabase.IsValidFolder("Assets/Dados/Resources")) AssetDatabase.CreateFolder("Assets/Dados", "Resources");
        cfg = ScriptableObject.CreateInstance<RondaDoAndarConfig>();
        AssetDatabase.CreateAsset(cfg, ConfigPath);
        AssetDatabase.SaveAssets();
        nova = true;
        return cfg;
    }

    static CreatureAI CriarInspetora(Transform raiz, CreatureAI modelo, Vector3 perto)
    {
        const float altura = 2.3f;
        var go = new GameObject("Inspetora (ronda do andar)");
        Undo.RegisterCreatedObjectUndo(go, "Inspetora da ronda");
        go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(perto.x, 0f, perto.z);

        Sprite sprite = null;
        if (modelo != null)
        {
            var sr0 = modelo.GetComponentInChildren<SpriteRenderer>(true);
            if (sr0 != null) sprite = sr0.sprite;
        }
        if (sprite == null)
            foreach (var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteInspetora))
                if (o is Sprite sp) { sprite = sp; break; }

        var sgo = new GameObject("Sprite");
        sgo.transform.SetParent(go.transform, false);
        var sr = sgo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 150;
        if (sprite != null)
        {
            float s = altura / Mathf.Max(sprite.bounds.size.y, 0.01f);
            sgo.transform.localScale = Vector3.one * s;
            sgo.transform.localPosition = new Vector3(0f, -sprite.bounds.min.y * s, 0f);
        }
        sgo.AddComponent<FaceCamera>();
        var olhos = new GameObject("Olhos").transform;
        olhos.SetParent(go.transform, false);
        olhos.localPosition = new Vector3(0f, altura * 0.75f, 0f);

        var ai = go.AddComponent<CreatureAI>();
        ai.eyes = olhos;
        ai.sprite = sr;
        ai.obstacleMask = ~0;
        ai.patrolSpeed = 1.2f; ai.chaseSpeed = 3.3f; ai.investigateSpeed = 2f;
        ai.viewDistance = 5f; ai.viewAngle = 110f; ai.noticeTime = 0.45f;
        ai.avoidsLight = true; ai.waitAtPoint = 1.8f; ai.catchDistance = 0.9f; ai.searchTime = 4f;
        ai.onStartChase = new UnityEvent(); ai.onLosePlayer = new UnityEvent(); ai.onCatch = new UnityEvent();
        go.SetActive(false);                           // a ronda liga quando ela entra
        return ai;
    }

    static PortaDaRonda CriarPorta(Transform raiz, DoorExit door, string vizinho, Vector3 centro)
    {
        Vector3 pd = door.transform.position; pd.y = 0f;
        Vector3 paraDentro = centro - pd; paraDentro.y = 0f;
        paraDentro = paraDentro.sqrMagnitude > 0.01f ? paraDentro.normalized : Vector3.back;

        var go = new GameObject("Porta da ronda → " + vizinho);
        Undo.RegisterCreatedObjectUndo(go, "Porta da ronda");
        go.transform.SetParent(raiz, false);
        go.transform.position = pd + paraDentro * 0.5f;          // aparece logo na frente da porta
        var p = go.AddComponent<PortaDaRonda>();
        p.comodoVizinho = vizinho;

        // Sombra: na parede da porta, vindo de dentro do cômodo até a porta, um pouco antes de ela aparecer.
        float lado = Mathf.Abs(paraDentro.x) > 0.05f ? Mathf.Sign(paraDentro.x) : 1f;
        Vector3 naParede = door.transform.position + Vector3.forward * 0.02f; naParede.y = 0f;
        p.sombraFim = Marco(go.transform, "Sombra: fim", naParede);
        p.sombraInicio = Marco(go.transform, "Sombra: início", naParede + Vector3.right * lado * 3f);
        return p;
    }

    static Transform[] CriarPontos(Transform raiz, Vector3 centro)
    {
        // Espalha 4 pontos entre as portas/esconderijos/spawns mais distantes, na linha em que a Luma anda.
        float minX = float.MaxValue, maxX = float.MinValue;
        void Incluir(Vector3 p) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
        foreach (var d in Object.FindObjectsByType<DoorExit>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Incluir(d.transform.position);
        foreach (var h in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Incluir(h.transform.position);
        foreach (var s in Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Incluir(s.transform.position);
        if (minX > maxX) { minX = centro.x - 4f; maxX = centro.x + 4f; }
        minX += 1f; maxX -= 1f;
        if (maxX < minX) maxX = minX;
        var pts = new Transform[4];
        for (int i = 0; i < pts.Length; i++)
        {
            float x = Mathf.Lerp(minX, maxX, (float)i / (pts.Length - 1));
            pts[i] = Marco(raiz, "Ponto de ronda " + (i + 1), new Vector3(x, 0f, centro.z));
        }
        return pts;
    }

    static Transform Marco(Transform pai, string nome, Vector3 pos)
    {
        var go = new GameObject(nome);
        Undo.RegisterCreatedObjectUndo(go, nome);
        go.transform.SetParent(pai, false);
        go.transform.position = pos;
        return go.transform;
    }
}
