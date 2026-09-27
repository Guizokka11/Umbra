using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Efeitos de medo na imagem: quanto maior o medo, mais a imagem perde cor, treme nas bordas
/// (aberração cromática), ganha granulação e "respira" (distorção). Em pânico, pulsa como batimento.
/// Colocar no Global Volume (o montador já coloca).
/// </summary>
[RequireComponent(typeof(Volume))]
public class FearFX : MonoBehaviour
{
    public float baseSaturation = -18f;
    public float fearSaturation = -55f;
    public float maxChromatic = 0.7f;
    public float baseGrain = 0.25f;
    public float maxGrain = 0.7f;
    public float panicPulse = 0.18f;

    Volume vol;
    ColorAdjustments color;
    ChromaticAberration chroma;
    FilmGrain grain;
    LensDistortion lens;

    void Start()
    {
        vol = GetComponent<Volume>();
        var p = vol.profile;   // cópia só desta sessão de jogo
        if (!p.TryGet(out color)) color = p.Add<ColorAdjustments>(true);
        if (!p.TryGet(out chroma)) chroma = p.Add<ChromaticAberration>(true);
        if (!p.TryGet(out grain)) grain = p.Add<FilmGrain>(true);
        if (!p.TryGet(out lens)) lens = p.Add<LensDistortion>(true);
    }

    void Update()
    {
        float f = FearSystem.Instance != null ? FearSystem.Instance.fear : 0f;
        float panic = Mathf.InverseLerp(0.6f, 1f, f);
        float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * Mathf.Lerp(5f, 9f, panic))), 8f);   // "tum-tum"

        if (color != null) color.saturation.Override(Mathf.Lerp(baseSaturation, fearSaturation, f));
        if (chroma != null) chroma.intensity.Override(Mathf.Lerp(0.05f, maxChromatic, f) + beat * panic * 0.3f);
        if (grain != null) grain.intensity.Override(Mathf.Lerp(baseGrain, maxGrain, f));
        if (lens != null) lens.intensity.Override(-beat * panic * panicPulse);
    }
}
