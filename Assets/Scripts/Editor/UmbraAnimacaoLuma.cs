using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Umbra > Animação > Estados de ação da Luma (placeholders)
/// Cria no Animator da Luma os estados Climb, Push, Pull, Grab, Hide, Caught e Stairs, cada um com um clipe
/// placeholder em Assets/Animations/Placeholders: o primeiro quadro do Idle + uma cor que identifica a ação.
///
/// Para trocar pela arte: abra o clipe (ex.: Luma_Push.anim) na janela Animation, apague a curva
/// "Sprite Renderer.Color" e arraste os quadros novos para a curva "Sprite". O estado e o código não mudam.
/// Rodar de novo é seguro: clipes e estados que já existem não são tocados.
/// </summary>
public static class UmbraAnimacaoLuma
{
    const string ControllerPath = "Assets/Animations/Player.controller";
    const string PastaClipes    = "Assets/Animations/Placeholders";

    struct Placeholder
    {
        public string estado; public Color cor; public float duracao; public bool loop; public bool pisca;
        public Placeholder(string e, Color c, float d, bool l, bool p = false) { estado = e; cor = c; duracao = d; loop = l; pisca = p; }
    }

    static readonly Placeholder[] Estados =
    {
        new Placeholder("Climb",  new Color(0.55f, 0.75f, 1f),       0.4f, false),        // azul: subindo
        new Placeholder("Push",   new Color(1f, 0.9f, 0.45f),        0.6f, true),         // amarelo: empurrando
        new Placeholder("Pull",   new Color(1f, 0.6f, 0.3f),         0.6f, true),         // laranja: puxando
        new Placeholder("Grab",   new Color(0.95f, 0.85f, 0.6f),     1f,   true),         // bege: segurando parada
        new Placeholder("Hide",   new Color(0.35f, 0.35f, 0.45f, 0.7f), 1f, true),        // escuro: escondida
        new Placeholder("Caught", new Color(1f, 0.2f, 0.2f),         0.5f, true, true),   // vermelho piscando: pega
        new Placeholder("Stairs", new Color(0.6f, 1f, 0.6f),         0.6f, true),         // verde: na escada
    };

    [MenuItem("Umbra/Animação/Estados de ação da Luma (placeholders)")]
    public static void Criar()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("Animator da Luma não achado em " + ControllerPath); return; }

        var sm = ctrl.layers[0].stateMachine;
        Sprite baseSprite = PrimeiroSprite(sm.defaultState != null ? sm.defaultState.motion as AnimationClip : null);
        if (baseSprite == null) Debug.LogWarning("Não achei o sprite do Idle; placeholders ficarão só com a cor.");

        if (!AssetDatabase.IsValidFolder(PastaClipes))
            AssetDatabase.CreateFolder("Assets/Animations", "Placeholders");

        int criados = 0;
        for (int i = 0; i < Estados.Length; i++)
        {
            var p = Estados[i];
            string clipPath = PastaClipes + "/Luma_" + p.estado + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = CriarClipe(p, baseSprite);
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            if (sm.states.Any(s => s.state.name == p.estado)) continue;
            // Estados em coluna à direita dos que já existem; o código troca com Animator.Play, sem transições.
            var st = sm.AddState(p.estado, new Vector3(520f, 40f + i * 60f, 0f));
            st.motion = clip;
            criados++;
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("Animator da Luma: " + criados + " estado(s) criado(s). Placeholders em " + PastaClipes);
    }

    static AnimationClip CriarClipe(Placeholder p, Sprite sprite)
    {
        var clip = new AnimationClip { name = "Luma_" + p.estado, frameRate = 12f };

        if (sprite != null)
        {
            var sb = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, sb, new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = sprite },
            });
        }

        // Cor que identifica a ação (apagar esta curva quando entrar a arte).
        string[] canais = { "r", "g", "b", "a" };
        for (int c = 0; c < 4; c++)
        {
            float v = p.cor[c];
            AnimationCurve curva;
            if (p.pisca && c > 0 && c < 3)      // pisca entre branco e a cor (G e B descem)
                curva = new AnimationCurve(new Keyframe(0f, v), new Keyframe(p.duracao * 0.5f, 1f), new Keyframe(p.duracao, v));
            else
                curva = new AnimationCurve(new Keyframe(0f, v), new Keyframe(p.duracao, v));
            clip.SetCurve("", typeof(SpriteRenderer), "m_Color." + canais[c], curva);
        }

        var cfg = AnimationUtility.GetAnimationClipSettings(clip);
        cfg.loopTime = p.loop;
        AnimationUtility.SetAnimationClipSettings(clip, cfg);
        return clip;
    }

    static Sprite PrimeiroSprite(AnimationClip clip)
    {
        if (clip == null) return null;
        foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (b.type != typeof(SpriteRenderer) || b.propertyName != "m_Sprite") continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, b);
            if (keys.Length > 0) return keys[0].value as Sprite;
        }
        return null;
    }
}
