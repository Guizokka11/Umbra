# Sons do Umbra — o que gravar ou baixar

Todos os campos de som do jogo já existem e estão **vazios**. Basta arrastar o arquivo para o campo indicado.
Enquanto um campo estiver vazio, o jogo simplesmente não toca aquele som (não dá erro).

## Onde colocar os arquivos

```
Assets/Audio/
  Ambiente/     loops e rangidos dos cômodos
  Passos/       passos da Luma
  Stingers/     sustos curtos
  Musica/       música de perseguição
  Medo/         batimento e respiração
  Criaturas/    Inspetora e outras criaturas
  Efeitos/      portas, móveis arrastados, objetos
```

## Formato

- **WAV**, 48 kHz, 16 ou 24 bits.
- **Mono** para passos, rangidos, stingers e criaturas. **Estéreo** para loops de ambiente e música.
- **Loops** (arquivos terminados em `_loop`) precisam emendar sem clique: fim e começo no mesmo ponto da onda.
  No Unity não precisa marcar nada: o código já toca esses arquivos em loop.
- Deixe ~0,05 s de silêncio antes do ataque nos sons curtos, nunca mais que isso (o susto precisa ser imediato).
- Volume: normalize os loops de ambiente bem mais baixo (≈ -18 dB) que os stingers (≈ -3 dB).
  O jogo ajusta o resto; é mais fácil subir um som baixo do que consertar um som estourado.
- Tom: terror por atmosfera. Nada de "jump scare" de filme B; sons secos, próximos, orgânicos (madeira, respiração, galhos).

## Como o som está organizado (AudioMixer `UmbraMixer`)

| Grupo      | O que toca                                                  | Volume em Pausa > Opções |
|------------|-------------------------------------------------------------|--------------------------|
| Ambiente   | loop grave e rangidos de cada cômodo                        | Ambiente                 |
| Efeitos    | passos, stingers, batimento, respiração, portas, móveis     | Efeitos                  |
| Música     | música de perseguição                                       | Música                   |
| Criaturas  | presença e grito das criaturas (som 3D, vem de onde ela está)| Criaturas                |

---

## 1. Sons do jogo todo — prefab `Assets/Audio/Resources/AudioManager.prefab`

Selecione o prefab no Project e preencha no Inspector:

| Campo | Arquivo sugerido | Duração | Onde toca |
|---|---|---|---|
| Passos da Luma > Madeira > Passos | `Passos/passo_madeira_01.wav` … `_06` (6 variações) | 0,2–0,4 s | cada passo andando em chão de madeira (quase todas as cenas) |
| Passos da Luma > Madeira > Passos Correndo | `Passos/passo_madeira_corre_01.wav` … `_04` | 0,2–0,3 s | passos correndo (Shift). Vazio = usa os de andar, mais alto |
| Passos da Luma > Azulejo > Passos | `Passos/passo_azulejo_01.wav` … `_06` | 0,2–0,4 s | banheiro (e onde houver `SuperficieDoChao` = Azulejo) |
| Passos da Luma > Azulejo > Passos Correndo | `Passos/passo_azulejo_corre_01.wav` … `_04` | 0,2–0,3 s | correndo no azulejo |
| Aterrissar | `Passos/aterrissar_01.wav` | 0,3 s | Luma cai no chão depois de um pulo ou de descer de um móvel |
| Stingers (lista) | `Stingers/stinger_01.wav` … `_05` | 1–3 s | sustos curtos gerais (sorteados quando o susto não tem som próprio) |
| Stinger Inicio Perseguicao | `Stingers/stinger_perseguicao.wav` | 1–2 s | no instante em que uma criatura começa a perseguir |
| Musica De Perseguicao | `Musica/musica_perseguicao_loop.wav` | 30–60 s, loop | entra com fade quando uma criatura persegue a Luma; sai devagar quando ela é perdida |
| Batimento | `Medo/medo_batimento_loop.wav` | 1–2 s, loop (≈ 70 bpm) | coração da Luma. Volume e velocidade sobem com o medo (acima de 30%) |
| Respiracao | `Medo/medo_respiracao_loop.wav` | 4–8 s, loop | respiração da Luma, mais forte com medo alto. Grave com uma criança/voz leve, sem ofegar demais |

Os passos são sorteados e ganham uma leve variação de tom a cada passo: 6 variações bastam.

## 2. Ambiente de cada cena — objeto `Som: ambiente do cômodo` (componente AmbienteDoComodo)

Cada cena tem esse objeto (criado pelo menu **Umbra > Terror > Colocar sons na cena aberta**). Preencha:

- **Loop Grave**: `Ambiente/amb_<cena>_loop.wav`, 60–120 s, loop estéreo.
- **Rangidos** (lista): 4–8 sons curtos (0,5–3 s) que tocam soltos, de um lado ou do outro, a cada 8–20 s
  (dá para mudar em *Intervalo Rangidos* e *Volume Rangidos*).
- **Musica De Perseguicao** (opcional): só se a cena tiver uma música de perseguição própria.

| Cena | Loop grave sugerido | Rangidos sugeridos |
|---|---|---|
| 00_Pesadelo | `amb_pesadelo_loop` — zumbido distorcido, grave, meio submerso | `rangido_pesadelo_01…` — risadas de criança muito distantes, sussurros ininteligíveis |
| 01_Dormitorio1 | `amb_dormitorio_loop` — quarto grande à noite, vento no vidro | `rangido_madeira_01…06` (tábuas, estrado de cama), respiração de criança dormindo |
| 02_Corredor1 | `amb_corredor_loop` — corredor comprido, ar parado, lâmpada zumbindo | tábuas, porta distante batendo leve, canos |
| 03_Banheiro | `amb_banheiro_loop` — azulejo, eco curto, goteira ritmada | `rangido_cano_01…`, goteira forte, descarga distante |
| 06_Dormitorio2 | `amb_dormitorio2_loop` — silêncio pesado, várias respirações no mesmo ritmo | tábuas soltas (bem baixinho: aqui o silêncio é o susto) |
| 08_Biblioteca | `amb_biblioteca_loop` — sala abafada, papel, relógio distante | livro caindo, página virando sozinha, estante rangendo |
| 09_LazyRoom | `amb_ninho_loop` — o ninho da Inspetora: galhos estalando baixo, cabelo arrastando | galhos quebrando, estalos secos |
| 10_LivingRoom | `amb_salaestar_loop` — sala ampla, lareira apagada, vento na chaminé | a voz "...Luma..." NÃO vai aqui (é roteiro); só tábuas e vento |

## 3. Criaturas — componente `SomDaCriatura` (em cada criatura das cenas)

| Criatura | Loop Presenca | Ao Perseguir |
|---|---|---|
| Inspetora (massa, `InspetoraMass`) | `Criaturas/inspetora_presenca_loop.wav` — galhos e cabelo arrastando, respiração úmida; 5–10 s, loop mono | `Criaturas/inspetora_perseguir.wav` — estalo de galhos + grito abafado, 1–2 s |
| Outras criaturas (`CreatureAI`) | `Criaturas/<nome>_presenca_loop.wav` — respiração, passos pesados | `Criaturas/<nome>_perseguir.wav` |

O loop fica mais alto quando a criatura se move e some a ~14 m (campo *Alcance*).

## 4. Sustos (`ScareFlash`) — campo **Stinger** de cada susto

Opcional: sem som próprio, o susto usa um dos stingers gerais do AudioManager.
Exemplo: o susto da escada do Corredor 1 (`Susto_Escada`) → `Stingers/susto_escada_galhos.wav` (galhos raspando rápido, 1,5 s).

## 5. Efeitos já existentes nas cenas (AudioSource → campo *AudioClip*)

| Onde | Arquivo sugerido | Duração |
|---|---|---|
| Móveis empurráveis (`Pushable` > Drag Loop): estantes, baú | `Efeitos/arrastar_movel_loop.wav` | 2–4 s, loop |
| Barulhos (`NoiseEmitter` > Sfx): tábuas soltas do dormitório 2 | `Efeitos/tabua_rangendo_01.wav` | 0,5–1 s |
| Objetos que se usa (`SimpleInteractable` > Sfx) | depende do objeto | curto |

## Para quem mexe no código

- `AudioManager.Instance.TocarStinger(clip, silenciar)` — toca um susto curto (e silencia o ambiente junto, se `silenciar` > 0).
- `AmbienteDoComodo.SilencioAntesDoSusto(segundos)` — o ambiente quase some por alguns segundos (dá para ligar num UnityEvent).
- `AudioManager.Instance.Grupo(AudioManager.GrupoEfeitos)` — grupo do mixer para ligar um AudioSource novo.
