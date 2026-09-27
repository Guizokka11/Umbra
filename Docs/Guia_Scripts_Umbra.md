# Umbra: guia de setup dos scripts

Os scripts já estão no projeto, em `Assets/Scripts`, e a Unity já compilou todos. Não é preciso copiar nada.

```
Assets/Scripts/
  PlayerMovement.cs, PlayerSpriteController.cs, CamSwitcher.cs   (já existiam)
  Core/        PlayerState, FearSystem, LightZone, GameManager, Checkpoint, LevelExit
  Interacao/   Interactable, PlayerInteractor, Pushable, HidingSpot, SimpleInteractable,
               NoiseEmitter, PressurePlate, PuzzleCounter, SymbolLock, SymbolDial
  Criaturas/   CreatureAI
  Narrativa/   ClueData, Clue, ClueJournal, MemoryTrigger, NightmareCorridor, FinalDoor
  UI/          ScreenFader, ClueUI
  Editor/      UmbraSetup  (menu "Umbra" no topo da Unity)
```

## Fazer funcionar em 5 passos

1. **TextMeshPro:** menu `Window > TextMeshPro > Import TMP Essential Resources` (uma vez só no projeto).
2. **Tag da Luma:** selecione o objeto `Player` (o que tem o `PlayerMovement`) e coloque a Tag `Player`.
3. **Global Volume:** a `SampleScene` já tem um. Numa cena nova, crie com `GameObject > Volume > Global Volume`.
4. **Menu `Umbra > 1. Configurar cena`:** adiciona `PlayerState`, `FearSystem` e `PlayerInteractor` na Luma, cria o `GameManager`, o Canvas `UI_Umbra` (fade, texto de interação, painel de pistas e overlay de memória) e liga a vinheta do medo. Tudo já vem conectado.
5. **Menu `Umbra > Verificar cena`:** mostra uma lista do que ainda falta. Quando aparecer "Cena OK", dê Play.

Salve a cena (Ctrl+S) depois do passo 4.

## Controles

| Tecla | Ação |
|---|---|
| WASD / setas | andar |
| Shift | correr (faz barulho) |
| Espaço | pular |
| E | interagir; apertar de novo solta, sai ou fecha |
| F (segurar) | abraçar o urso: Luma para e o medo diminui |

## Criar peças: menu `Umbra > Criar`

Cada item cria o objeto no centro da Scene View, no mesmo plano da Luma, já configurado. Depois é só trocar o visual pela arte.

| Item do menu | Para que serve | O que ajustar depois |
|---|---|---|
| Zona de luz | Área segura com luz real | Tamanho do BoxCollider = área iluminada; `flicker` para piscar |
| Caixa empurrável | Baú, cadeira, escada de rodinhas, saco de farinha | `lockZ` em corredores; `locked` = só plataforma |
| Esconderijo | Armário, mesa com toalha, confessionário | Mover `HidePoint` e `ExitPoint`; `hideSprite` desligado para "embaixo da mesa" |
| Pista | Bilhete, desenho, registro | Preencher o asset em `Assets/Dados/Pistas` (título, texto, origem) |
| Checkpoint | Ponto de retorno ao ser pega | Colocar antes de cada perigo |
| Saída de área | Troca de cena com fade | Escrever o nome da cena; adicioná-la em Build Profiles |
| Criatura com patrulha | Sombra, Arquivista, Manifestação | Mover waypoints A e B; valores na tabela abaixo |
| Memória | Fragmento de memória | Preencher os quadros (imagem, frase, duração) |
| Interruptor ou alavanca | Trinco, cordão de lâmpada, quadro de força, gaveta | Ligar ações em `onInteract` |
| Placa de pressão | Contrapeso do monta-cargas | `required` = quantos objetos |
| Emissor de ruído | Distração (pilha de papéis) | Chamar `Emit()` pelo `onInteract` de um interruptor |
| Tábua que range | Chão que faz barulho se ela correr | Posicionar no chão do Escritório |
| Cadeado de 3 símbolos | Caixinha de música da Amelie | Colocar os sprites dos símbolos em cada roda e o `solution` |
| Porta final | Confronto final | Chamar `Begin()` quando a cena começar |

## Como ligar ações sem programar (`UnityEvent`)

Todos os eventos (`onInteract`, `onSolved`, `onActivated`, `onRead`, `onFinished`...) funcionam igual ao `onClick` de um botão:

1. No Inspector, clique em **+** no evento.
2. Arraste o objeto alvo para o campo.
3. Escolha a função. Exemplos:
   - `LightZone > Toggle` acende ou apaga uma luz.
   - `GameObject > SetActive (bool)` mostra ou esconde algo (vapor do espelho, pista escondida, porta aberta).
   - `CreatureAI > StartChase` inicia uma perseguição de roteiro.
   - `PuzzleCounter > AddFrom (GameObject)` conta uma parte do puzzle.
   - `NoiseEmitter > Emit` faz barulho.
   - `MemoryTrigger > Play` toca uma memória.

## Receitas dos puzzles do roteiro

**Trinco alto (Dormitório 1):** caixa empurrável (baú) + interruptor no trinco, alto o bastante para só ser alcançado em cima do baú. `onInteract` do trinco: `GameObject.SetActive(false)` na porta fechada.

**Espelho do Banheiro:** dois interruptores (chuveiros), cada um com `onInteract` → `PuzzleCounter.AddFrom` (arraste o próprio chuveiro no parâmetro). No `PuzzleCounter`: `required` 2, `activeTime` 12. `onSolved`: ativa o sprite de vapor e o objeto da pista P03. `onExpired`: desativa os dois. A pista começa desativada.

**Caixinha de música (Corredor 1):** cadeado de 3 símbolos com `solution` = lua, chave, estrela. `onUnlocked`: toca o áudio, ativa a chave da Biblioteca e chama `FearSystem.ResetFear`.

**Cordões de lâmpada (Corredor 2):** interruptor alto em cada lâmpada, `onInteract` → `LightZone.SetOn(true)`. Cadeira empurrável para alcançar.

**Contrapeso do monta-cargas (Cozinha):** placa de pressão com `required` 2 + dois sacos (caixas empurráveis). `onActivated` ativa a alavanca da cabine; a alavanca chama a `Saída de área` para `03_Andar3`.

**Escritório (Arquivista):** criatura com `viewDistance` 1,5, `hearingMultiplier` 2,5, `avoidsLight` desligado. Tábuas que rangem no chão. Pilha de papéis = interruptor que chama `NoiseEmitter.Emit` do outro lado da sala.

**Porta final:** trigger na entrada da Passagem chama `FinalDoor.Begin`. `onClosed` chama uma memória final e a saída para `05_Epilogo`.

## Valores das criaturas

| Criatura | avoidsLight | viewDistance | hearingMultiplier | chaseSpeed | loseSightTime |
|---|---|---|---|---|---|
| A Sombra | ligado | 7 | 1 | 4 | 2,5 |
| O Arquivista | desligado | 1,5 | 2,5 | 3,5 | 3 |
| Manifestação do Abandono | desligado | 9 | 1 | 3,4 | 6 |

## Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Nada acontece ao apertar E | Faltou `PlayerInteractor` na Luma ou a tag `Player` |
| Tela preta ao dar Play | Normal por 2 segundos (fade de despertar). Se continuar, há erro no Console |
| Criatura não se move | Sem waypoints, ou `startIdle` ligado |
| Criatura atravessa paredes | Sem NavMesh. Adicione um `NavMeshAgent` e faça o bake (AI Navigation) |
| Luma não é vista nunca | O raycast bate em algo. Ajuste `obstacleMask` para só paredes |
| Pista não abre | O `Clue` está sem `ClueData`, ou falta o Canvas `UI_Umbra` |
| Caixa não se move | Rigidbody com `Is Kinematic` marcado, ou `locked` ligado |
