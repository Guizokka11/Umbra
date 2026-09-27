using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Medidas do cômodo pintado para montar o cenário 3D (chão deitado + paredes em pé).
/// Fica na raiz da arte do PSD. Editado pela janela "Umbra > Cenário > Montar cenário do PSD".
/// Posições em FRAÇÕES da pintura: x 0 = borda esquerda, 1 = direita; y 0 = topo, 1 = base.
/// </summary>
public class CenarioPSD3D : MonoBehaviour
{
    [Tooltip("Nome usado nos arquivos gerados (Assets/Dados/Cenario3D). Vazio = nome da cena.")]
    public string nome;
    [Tooltip("Linha (fração do topo) onde o chão encontra a parede do fundo.")]
    [Range(0.5f, 1f)] public float linhaDoChao = 0.8f;
    [Tooltip("Profundidade do chão (m), da parede do fundo até a borda de baixo da pintura. 0 = automático.")]
    public float profundidadeDoChao = 0f;
    [Tooltip("Até onde a Luma chega perto da câmera (fração do topo).")]
    [Range(0.8f, 1f)] public float limiteDaFrente = 0.95f;
    [Tooltip("Trechos com a parede do fundo em outra linha: (x0, x1, linha do chão daquela parede).")]
    public List<Vector3> trechosDeParede = new List<Vector3>();
    [Tooltip("Paredes laterais pintadas: (x no fundo, y no fundo, x na frente, y na frente) da linha em que tocam o chão.")]
    public List<Vector4> paredesLaterais = new List<Vector4>();
    [Tooltip("Escala do PSD usada na cena (para o tamanho da textura gerada).")]
    public float escala = 1f;
    [Tooltip("Arquivo do PSD (para achar as medidas reais de cada camada em Assets/Dados/PSD).")]
    public string psd;
    [Tooltip("Camadas sem papel definido que descem abaixo da linha do chão viram móveis.")]
    public bool detectarMoveis = true;
    [Tooltip("Faixa (x0, x1) da pintura com parede e chão lisos. É repetida onde a câmera vê além da pintura " +
             "e na lateral dos degraus de parede (em vez de esticar a borda). (0, 0) = escuro.")]
    public Vector2 amostraParede;
    [Tooltip("Vãos nas colisões da parede do fundo (x inicial, x final, z da parede), em metros. " +
             "Ex.: a entrada da escada 3D do Corredor 1. Remontar o cenário mantém o vão aberto.")]
    public List<Vector3> vaosNaParede = new List<Vector3>();
    [Tooltip("Buracos na pintura da parede do fundo (x inicial, x final, z da parede), em metros: ali a parede pintada " +
             "some e aparece o que foi modelado atrás dela (ex.: o vão 3D da escada). Remontar o cenário mantém o buraco.")]
    public List<Vector3> buracosNaPintura = new List<Vector3>();
    [Tooltip("Aberturas na pintura da parede do fundo só até uma altura (x inicial, x final, z da parede, topo), em metros. " +
             "Ex.: a porta da escada de descida do Corredor 1. Acima do topo a parede pintada continua.")]
    public List<Vector4> aberturasNaPintura = new List<Vector4>();
    [Tooltip("Buracos na colisão do chão (x inicial, x final, z inicial, z final), em metros. " +
             "Ex.: o poço da escada de descida do Corredor 1. Remontar o cenário mantém o buraco.")]
    public List<Vector4> buracosNoChao = new List<Vector4>();
}
