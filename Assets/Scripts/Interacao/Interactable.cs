using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base de tudo que a Luma pode usar com a tecla de interação
/// (empurrar caixas, esconder-se, ler pistas, alavancas, portas...).
/// Para criar um novo tipo, herde desta classe e implemente Interact().
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Tooltip("Texto mostrado quando a Luma está perto (ex.: \"Esconder\", \"Ler\").")]
    public string prompt = "Interagir";

    [Tooltip("Distância extra além do raio padrão do PlayerInteractor.")]
    public float extraRange = 0f;

    [Tooltip("0 = ignora a altura. >0 = a Luma precisa estar a no máximo esta diferença de altura " +
             "(ex.: trinco alto só alcançado em cima do baú).")]
    public float maxVerticalDistance = 0f;

    [Tooltip("Ponto usado para medir a distância. Se vazio, usa o transform.")]
    public Transform interactionPoint;

    public Vector3 Point => interactionPoint != null ? interactionPoint.position : transform.position;

    protected virtual void OnEnable()  { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    public virtual bool CanInteract(PlayerInteractor who) => isActiveAndEnabled;

    /// <summary>Distância "extra" somada na escolha do mais próximo. Maior = menos prioridade
    /// (ex.: pista já lida perde para a caixa ao lado).</summary>
    public virtual float PriorityPenalty => 0f;

    public abstract void Interact(PlayerInteractor who);

    /// <summary>Chamado quando o jogo precisa soltar a Luma à força (ex.: captura).</summary>
    public virtual void ForceRelease(PlayerInteractor who) { }
}
