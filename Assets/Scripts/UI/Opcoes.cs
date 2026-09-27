using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Opções do jogador (menu de pausa > Opções), salvas em PlayerPrefs:
///  - Volume geral (AudioListener.volume).
///  - Brilho: soma à exposição (Post Exposure) que cada cena já tem no Global Volume.
///  - Dicas de controle ligadas/desligadas (DicasDeControle).
/// Aplicado sozinho ao abrir o jogo e a cada cena carregada.
/// </summary>
public static class Opcoes
{
    const string KeyVolume = "umbra_volume", KeyBrilho = "umbra_brilho", KeyDicas = "umbra_dicas_controle";

    /// <summary>Quanto o brilho no máximo/mínimo muda a exposição (EV).</summary>
    public const float BrilhoMaxEV = 1.5f;

    static float volume = -1f, brilho = float.NaN;
    static int dicas = -1;
    // Exposição original de cada Volume da cena (o brilho soma a ela, não substitui).
    static readonly Dictionary<Volume, float> baseExposure = new Dictionary<Volume, float>();

    /// <summary>0 a 1.</summary>
    public static float VolumeGeral
    {
        get { if (volume < 0f) volume = PlayerPrefs.GetFloat(KeyVolume, 1f); return volume; }
        set { volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeyVolume, volume); PlayerPrefs.Save(); AplicarVolume(); }
    }

    /// <summary>-1 (mais escuro) a 1 (mais claro); 0 = como a cena foi montada.</summary>
    public static float Brilho
    {
        get { if (float.IsNaN(brilho)) brilho = PlayerPrefs.GetFloat(KeyBrilho, 0f); return brilho; }
        set { brilho = Mathf.Clamp(value, -1f, 1f); PlayerPrefs.SetFloat(KeyBrilho, brilho); PlayerPrefs.Save(); AplicarBrilho(); }
    }

    public static bool DicasDeControle
    {
        get { if (dicas < 0) dicas = PlayerPrefs.GetInt(KeyDicas, 1); return dicas == 1; }
        set { dicas = value ? 1 : 0; PlayerPrefs.SetInt(KeyDicas, dicas); PlayerPrefs.Save(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (s, m) => { baseExposure.Clear(); AplicarTudo(); };
        AplicarTudo();
    }

    public static void AplicarTudo() { AplicarVolume(); AplicarBrilho(); }

    static void AplicarVolume() => AudioListener.volume = VolumeGeral;

    static void AplicarBrilho()
    {
        float ev = Brilho * BrilhoMaxEV;
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (v == null || !v.isGlobal || v.sharedProfile == null) continue;
            var p = v.profile;   // cópia só desta sessão (a mesma que o FearFX usa)
            if (!p.TryGet(out ColorAdjustments color)) color = p.Add<ColorAdjustments>(true);
            if (!baseExposure.TryGetValue(v, out float b))
            {
                b = color.postExposure.overrideState ? color.postExposure.value : 0f;
                baseExposure[v] = b;
            }
            color.postExposure.Override(b + ev);
        }
    }
}
