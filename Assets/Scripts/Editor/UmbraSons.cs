using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

/// <summary>
/// Umbra > Terror > Colocar sons na cena aberta
///  1. Cria (ou completa) Assets/Audio/UmbraMixer.mixer com os grupos Ambiente, Efeitos, Música e Criaturas,
///     cada um com o volume exposto (Vol_Ambiente, Vol_Efeitos, Vol_Musica, Vol_Criaturas) para a tela de Opções.
///  2. Cria (ou completa) Assets/Audio/Resources/AudioManager.prefab (o som do jogo; nasce sozinho ao dar Play).
///  3. Na cena aberta, adiciona só o que falta: o objeto "Som: ambiente do cômodo" (AmbienteDoComodo),
///     SomDaCriatura em cada CreatureAI/InspetoraMass e liga AudioSources soltos (barulhos, arrastar) ao grupo Efeitos.
/// Pode rodar de novo: não duplica nada. Tudo com Undo (menos os assets novos).
/// </summary>
public static class UmbraSons
{
    const string Pasta = "Assets/Audio";
    const string MixerPath = Pasta + "/UmbraMixer.mixer";
    const string PrefabPath = Pasta + "/Resources/AudioManager.prefab";
    const string NomeAmbiente = "Som: ambiente do cômodo";
    const BindingFlags BF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [MenuItem("Umbra/Terror/Colocar sons na cena aberta", priority = 100)]
    static void ColocarSons()
    {
        var relatorio = new List<string>();
        Pastas();
        var mixer = MixerCompleto(relatorio);
        PrefabDoManager(mixer, relatorio);

        var cena = EditorSceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Colocar sons na cena");
        int undoGrupo = Undo.GetCurrentGroup();

        // Ambiente do cômodo
        var amb = Object.FindAnyObjectByType<AmbienteDoComodo>(FindObjectsInactive.Include);
        if (amb == null)
        {
            var go = new GameObject(NomeAmbiente);
            Undo.RegisterCreatedObjectUndo(go, "Ambiente do cômodo");
            amb = go.AddComponent<AmbienteDoComodo>();
            amb.superficiePadrao = cena.name.Contains("Banheiro") ? Superficie.Azulejo : Superficie.Madeira;
            relatorio.Add("+ \"" + NomeAmbiente + "\" (passos: " + amb.superficiePadrao + ")");
        }
        else relatorio.Add("= ambiente do cômodo já existia (" + amb.name + ")");

        // Criaturas
        int criaturas = 0;
        foreach (var c in Object.FindObjectsByType<CreatureAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.GetComponent<SomDaCriatura>() == null) { Undo.AddComponent<SomDaCriatura>(c.gameObject); criaturas++; }
        foreach (var m in Object.FindObjectsByType<InspetoraMass>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (m.GetComponent<SomDaCriatura>() == null) { Undo.AddComponent<SomDaCriatura>(m.gameObject); criaturas++; }
        relatorio.Add(criaturas > 0 ? "+ SomDaCriatura em " + criaturas + " criatura(s)" : "= criaturas já tinham som (ou não há criaturas)");

        // AudioSources soltos da cena → grupo Efeitos
        var efeitos = Grupo(mixer, AudioManager.GrupoEfeitos);
        int fontes = 0;
        var soltas = new List<AudioSource>();
        foreach (var n in Object.FindObjectsByType<NoiseEmitter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) soltas.Add(n.sfx);
        foreach (var p in Object.FindObjectsByType<Pushable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) soltas.Add(p.dragLoop);
        foreach (var s in Object.FindObjectsByType<SimpleInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) soltas.Add(s.sfx);
        foreach (var src in soltas)
        {
            if (src == null || src.outputAudioMixerGroup != null || efeitos == null) continue;
            Undo.RecordObject(src, "Grupo Efeitos");
            src.outputAudioMixerGroup = efeitos;
            fontes++;
        }
        if (fontes > 0) relatorio.Add("+ " + fontes + " AudioSource(s) da cena ligados ao grupo Efeitos");

        Undo.CollapseUndoOperations(undoGrupo);
        EditorSceneManager.MarkSceneDirty(cena);
        string texto = "Cena " + cena.name + ":\n\n" + string.Join("\n", relatorio) +
                       "\n\nSalve a cena (Ctrl+S). Os sons ficam vazios: veja Assets/Audio/LEIAME.md.";
        Debug.Log("[Umbra] Sons: " + texto.Replace("\n\n", " | ").Replace("\n", " | "));
        EditorUtility.DisplayDialog("Umbra — sons", texto, "OK");
    }

    // ------------------------------------------------------------------ assets

    static void Pastas()
    {
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets", "Audio");
        if (!AssetDatabase.IsValidFolder(Pasta + "/Resources")) AssetDatabase.CreateFolder(Pasta, "Resources");
    }

    static AudioMixerGroup Grupo(AudioMixer mixer, string nome)
    {
        if (mixer == null) return null;
        foreach (var g in mixer.FindMatchingGroups(string.Empty)) if (g.name == nome) return g;
        return null;
    }

    /// <summary>
    /// A API de criar mixer e grupos é interna no editor (UnityEditor.Audio.AudioMixerController):
    /// usa reflexão. Se uma versão nova do Unity mudar essa API, o erro aparece aqui e dá para criar o mixer
    /// à mão (Create > Audio Mixer, grupos e "Expose" do Volume com os nomes Vol_...).
    /// </summary>
    static AudioMixer MixerCompleto(List<string> relatorio)
    {
        var asm = typeof(Editor).Assembly;
        var tCtrl = asm.GetType("UnityEditor.Audio.AudioMixerController");
        var tGrupo = asm.GetType("UnityEditor.Audio.AudioMixerGroupController");
        var tCaminho = asm.GetType("UnityEditor.Audio.AudioGroupParameterPath");
        var tExposto = asm.GetType("UnityEditor.Audio.ExposedAudioParameter");
        if (tCtrl == null || tGrupo == null || tCaminho == null || tExposto == null)
        {
            relatorio.Add("! Não consegui criar o AudioMixer (API interna do Unity mudou). Crie à mão: ver LEIAME.md.");
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null)
        {
            mixer = (AudioMixer)tCtrl.GetMethod("CreateMixerControllerAtPath", BF).Invoke(null, new object[] { MixerPath });
            relatorio.Add("+ AudioMixer " + MixerPath);
        }
        var master = tCtrl.GetProperty("masterGroup", BF).GetValue(mixer);
        var propExpostos = tCtrl.GetProperty("exposedParameters", BF);
        var campoGuid = tExposto.GetField("guid", BF);
        var campoNome = tExposto.GetField("name", BF);

        foreach (var nome in AudioManager.Grupos)
        {
            object grupo = Grupo(mixer, nome);
            if (grupo == null)
            {
                grupo = tCtrl.GetMethod("CreateNewGroup", BF).Invoke(mixer, new object[] { nome, false });
                tCtrl.GetMethod("AddChildToParent", BF).Invoke(mixer, new[] { grupo, master });
                relatorio.Add("+ grupo " + nome + " no mixer");
            }

            // Volume do grupo exposto como Vol_<grupo>.
            string param = AudioManager.ParametroDoGrupo(nome);
            var guid = tGrupo.GetMethod("GetGUIDForVolume", BF).Invoke(grupo, null);
            var expostos = (Array)propExpostos.GetValue(mixer);
            bool tem = false;
            foreach (var e in expostos) if ((string)campoNome.GetValue(e) == param) tem = true;
            if (tem) continue;
            var caminho = Activator.CreateInstance(tCaminho, BF, null, new[] { grupo, guid }, null);
            tCtrl.GetMethod("AddExposedParameter", BF).Invoke(mixer, new[] { caminho });
            expostos = (Array)propExpostos.GetValue(mixer);
            for (int i = 0; i < expostos.Length; i++)
            {
                var e = expostos.GetValue(i);
                if (!campoGuid.GetValue(e).Equals(guid)) continue;
                campoNome.SetValue(e, param);            // struct: muda a cópia e devolve ao array
                expostos.SetValue(e, i);
            }
            propExpostos.SetValue(mixer, expostos);
            relatorio.Add("+ volume exposto " + param);
        }
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        return mixer;
    }

    static void PrefabDoManager(AudioMixer mixer, List<string> relatorio)
    {
        var existente = AssetDatabase.LoadAssetAtPath<AudioManager>(PrefabPath);
        if (existente == null)
        {
            var go = new GameObject("AudioManager");
            var am = go.AddComponent<AudioManager>();
            am.mixer = mixer;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            relatorio.Add("+ prefab " + PrefabPath + " (coloque os sons nele)");
        }
        else if (existente.mixer == null && mixer != null)
        {
            var raiz = PrefabUtility.LoadPrefabContents(PrefabPath);
            raiz.GetComponent<AudioManager>().mixer = mixer;
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPath);
            PrefabUtility.UnloadPrefabContents(raiz);
            relatorio.Add("+ mixer ligado ao prefab do AudioManager");
        }
    }
}
