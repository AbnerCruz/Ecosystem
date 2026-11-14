# LUNET-480 — Diagnóstico geral: o que o Lunet deveria ser × o que ele é (14/11/2026)

**Tipo de missão:** estudo (protocolo transversal, etapas 0–4). **Nada no produto foi alterado.** A consolidação em tarefas (etapa 5) e as decisões estruturais dependem do alinhamento com o proprietário; este documento propõe, mas não decide.
**Base observada:** Ecosystem `main` `86db344` (branch `claude/lunet2d-analysis-redesign-4ljh4e`).
**Fontes normativas lidas:** `MANIFEST.md`, `AGENTS.md`, `apps/lunet2d/AGENTS.md`, `docs/SPEC.md` (§§1–4, 9–13, 16), `ROADMAP.md`, ADR 0003 e 0007, `docs/governance/protocolo-estudo-desenvolvimento.md`, auditoria `docs/audits/inventario-estado-2026-10-27.md`, pesquisa `docs/research/mobile-workbench-ux-2026-11-01.md`, plano `docs/plans/visual-editor-vision.md`, Issues #860, #864, #865.
**Limites honestos:** sem .NET SDK nem aparelho neste ambiente: nenhum build, teste, screenshot ou medição foi executado. As afirmações sobre código são leitura direta com `caminho:linha`; estimativas aparecem como **INFERÊNCIA**.

Legenda: **FATO** (verificado no código/documento) · **INFERÊNCIA** · **PROPOSTA**.

---

## 1. O que o Lunet deveria ser

Somando SPEC §1, as Issues de direção #864/#865 (30/10/2026) e a fala do proprietário em 14/11/2026:

1. **Um ambiente de criação de jogos 2D em C#, code-first**, com três camadas que funcionam sozinhas e juntas: Framework, Editor (IDE) e Studio (#865: "independência, não exclusividade").
2. **Editor C# de verdade**, generalista e especializado em jogos; o atual é chamado pelo proprietário de "protótipo do protótipo" (#865). Ele deve ser repensado por inteiro, e não só retocado (#864 §5).
3. **Mobile primeiro, desktop previsto desde o desenho** (#864 §4). Na experiência, a qualidade dos dois ambientes é o critério principal: painéis planejados, abas, atalhos para toque, teclado e mouse.
4. **A IA faz parte do fluxo**, para construir e para consultar, sem precisar sair do editor. A referência funcional é o Urbe, onde "dar um comando no próprio editor" já funciona (ADR-0033/ADD-0027: o Urbe nativo é a referência funcional e de UX). O SPEC §16 exige um agente com ferramentas tipadas, compilar → rodar → verificar → reparar, diff com aceitar/reverter, modos Ask/Edit/Agent e chave no Android Keystore.
5. **Tudo o que o criador precisa fica dentro do projeto**: código, anotações, esboços, esquemas e assets. O usuário não deveria precisar sair do app para consultar as próprias notas.
6. **Studios completos** (Sprite/Animation/Tile/Audio/UI/Physics/Particle/Palette/Data, SPEC §13), na direção Aseprite + Photoshop + Illustrator + LPC (`docs/plans/visual-editor-vision.md`).

### 1.1 Contradições normativas que precisam de decisão

| # | Conflito | Evidência | Consequência |
|---|---|---|---|
| C1 | SPEC §2 diz **"Android only"**; SPEC §3 manda usar .NET for Android direto, sem MAUI. Já #864/#865 pedem desktop "desde o desenho". | `docs/SPEC.md:78`, `:118-121`; Issue #864 §4 | Nenhum agente pode escolher tecnologia de UI multiplataforma sem antes atualizar o SPEC por decisão (NN-011). |
| C2 | SPEC §11 diz **"landscape prioritário"**. Para o proprietário, a experiência no celular é a dor principal, e o celular é usado em retrato. | `docs/SPEC.md:416` | A prioridade de orientação precisa ser revista. |
| C3 | O Studio deve ser autônomo (#865), mas `Lunet.Studio.csproj` referencia `Lunet.Framework`. | Issue #865, "Lacunas" | Isso afeta o modelo de documento dos Studios; é melhor resolver antes de reescrevê-los. |
| C4 | O LUNET-470 recebeu **validação DEVICE aprovada** em 02/11 (Issue #891). Hoje, o proprietário descreve a interface como "a pior que já vi". | `docs/governance/handoffs/HO-20261101-lunet-mobile-workbench-ux.json` | O gate aprovou um roteiro estreito (teclado, Back, scrim), não a qualidade da experiência. Faltam critérios de aceite de UX (ver §6.5). |

---

## 2. O que o Lunet é hoje (auditoria do código)

### 2.1 Resumo

O **motor está bom; a casca não**. Quase todo o valor técnico está nas bibliotecas: `Lunet.Framework`, `Lunet.Compiler`, `Lunet.Editor`, `Lunet.Git`, o empacotador offline de APK e cerca de 750 testes. A apresentação é o gargalo: a interface inteira são cerca de **9.200 linhas de Views Android montadas à mão em código**, sem sistema de design, com diálogos modais servindo de navegação e sem nenhuma possibilidade de rodar no desktop.

### 2.2 Arquitetura da interface (FATO)

- `src/Lunet.Android/` tem 9.221 linhas em `.cs` e **nenhum recurso Android**: sem `Resources/`, layouts, temas, estilos, drawables ou ícones. Toda a interface é instanciada em C# imperativo.
- `MainActivity` é **uma única Activity parcial espalhada por 14 arquivos** (`MainActivity*.cs`, 3.926 linhas). Ela concentra projetos, workspace, editor, preview, Studios, Git, IA, build, IPC e benchmark. Não existe camada de componentes nem view-model: estado, layout e regra de interação ficam misturados.
- **Cada troca de tela reconstrói tudo** com `SetContentView` (`ShowProjects`, `MainActivity.cs:112`; `ShowWorkspace`, `:365`). Não há transição, não há preservação de rolagem e cada abertura aloca tudo de novo.
- A **navegação secundária é feita de `AlertDialog`**, com 81 ocorrências (`grep -c AlertDialog`). Menus, formulários, Studios, IA e Git abrem em diálogo sobre o editor.
- **Não existe design system.** Há 6 cores constantes em `MainActivity.Workbench.cs:16-21` e mais 57 cores soltas pelo código (`SetBackgroundColor`/`Color.Rgb`). Os Studios usam botões Android padrão de outro estilo (`SpriteStudioView.cs:79-85`). Os ícones são caracteres Unicode (‹ ☰ ⋯ ⌕ ↶ ↷ ●).
- **Portabilidade para desktop: zero.** Nada da camada visual sobrevive fora do Android. As Issues #864/#865 pedem justamente o contrário.

### 2.3 Workbench (tela principal do editor) (FATO + INFERÊNCIA)

- **Cromo vertical fixo:** barra superior de 58dp (`MainActivity.cs:376-391`), linha de documento de 52dp (`:393-418`) e navegação inferior de 58dp (`MainActivity.Workbench.cs:63-75`), cerca de **168dp** no total, além da linha de chips de autocompletar e das abas do painel quando ele está aberto.
  **INFERÊNCIA:** num celular de ~800dp de altura, com teclado de ~300dp, sobram cerca de 300dp de código, perto de 15 linhas. Ferramentas mobile maduras usam uma única barra contextual.
- **Navegação duplicada:** a barra inferior tem Erros/Console, e o painel repete Problemas/Console/Profiler em abas próprias (`MainActivity.cs:425-442`).
- **Command palette:** é um `AlertDialog` com `ListView` de altura fixa de 340dp e 22 comandos escritos à mão (`MainActivity.Workbench.cs:120-143`). Não mostra atalhos, não lista recentes, não busca arquivos ou símbolos e não se estende a plugins. O comando "Menu completo" leva aos menus aninhados antigos (`MainActivity.Ide.cs:150-186`).
- **Studios, IA e Git não têm lugar no layout.** São diálogos em tela cheia que tampam o código, sem split e sem abas.

### 2.4 Editor de código (FATO)

- O editor é um `EditText` com spans (ADR 0003). O realce **remove e reaplica todos os spans do arquivo** a cada pausa (`Editor/CodeEditText.cs:201-213`). O próprio ADR reconhece que isso "não escala para dezenas de milhares" de linhas.
- O "dobrar código" é feito com `AbsoluteSizeSpan(1)` e cor transparente, e **qualquer edição desdobra tudo** (adendo ao ADR 0003).
- O `PieceTable` existe e tem testes, mas **não está ligado** ao editor (adendo ao ADR 0003).
- **O que vale manter:** `Lunet.Editor` (análise Roslyn, refatoração, formatação, find/replace, undo), que é independente de view e testado no desktop. Esse é o ativo a preservar em qualquer redesenho.

### 2.5 IA (FATO), o ponto mais crítico

| Exigência | Estado no Lunet | Evidência |
|---|---|---|
| Estar no fluxo | Fica 3 níveis abaixo: **Ações → "Assistente e ferramentas" → "IA · Assistente de código"**, e abre num `AlertDialog` modal sobre o editor. | `MainActivity.Workbench.cs:137`, `MainActivity.Ide.cs:174`, `MainActivity.Ai.cs:111` |
| Configurar uma vez | **A chave existe só em memória** e some ao fechar o app, então é preciso configurar a cada sessão. O endpoint é uma URL crua e o modelo é um ID digitado à mão, sem presets nem catálogo. | `MainActivity.Ai.cs:15-18`, `:28`, `:40-45` |
| Keystore (SPEC §16) | Não existe. | idem |
| Contexto do editor | Não envia o arquivo aberto, a seleção, o cursor, os diagnósticos, o console nem a tela do jogo. O agente só tem ferramentas de arquivo (`list/read/search/excerpt/write/replace`). | `Lunet.Ai/LunetAiSession.cs:60-75`, `LunetAiProjectFiles.cs:188-193` |
| Compilar → rodar → verificar | Não existe. O próprio aviso na tela admite: "A IA não compila nem testa automaticamente". | `MainActivity.Ai.cs:79-80` |
| Diff aceitar/reverter | Não existe. As escritas vão direto ao disco, e o editor recarrega o arquivo. | `MainActivity.Ai.cs:140-151` |
| Conversa legível | Um `TextView` com as últimas 14 mensagens em texto puro, em caixa de 260dp. Sem streaming, sem markdown, sem blocos de código copiáveis. | `MainActivity.Ai.cs:82-95` |
| Permissão de escrita | É um checkbox por diálogo, e o estado se perde ao fechar. | `MainActivity.Ai.cs:72-76` |

**Comparação com o Urbe (FATO, leitura do código, somente leitura):**
- O Urbe abre o assistente como painel lateral de 440px em telas a partir de 900px e em tela cheia no celular (`apps/urbe/csharp/src/Urbe.Client/MainView.Assistant.cs:176-183`).
- Sobre a nota aberta, ele oferece um **companheiro compacto** (`:205-218`) e a invocação por sintaxe dentro do texto (`&Agente&:`, UC-80, `:185-200`).
- Ele envia a nota aberta e a seleção como contexto (`:226-230`).
- Tem presets de providers OpenRouter/Anthropic/OpenAI/Gemini, com link para criar a chave (`Urbe.Core/Assistant/AssistantProviders.cs:100-103`), catálogo de modelos e IA local.

O Lunet reaproveitou o `AgentRuntime.Core` (correto, ADR-0033), mas **não reaproveitou nada da UX**, e é isso que o proprietário percebe.

### 2.6 Studios (FATO)

- **Sprite Studio** (`SpriteStudioView.cs`):
  - São **7 fileiras de botões de texto** em `HorizontalScrollView` empilhadas antes do canvas (`:42-73`). **INFERÊNCIA:** cerca de 330dp de controles, ou seja, menos da metade da tela fica para desenhar.
  - Zoom só por botões "Zoom +/−", limitado a 1–8× (`:89-95`), sem pinça.
  - Cor só digitando hexadecimal ARGB (`:96-110`); a paleta tem 9 cores fixas.
  - **Não há camadas**, seleção, laço ou transformação.
  - O tamanho máximo é 128×128 com 64 frames (`Lunet.Studio/SpriteDocument.cs:17,35`).
  - O undo **serializa o documento inteiro em JSON a cada checkpoint**, com teto de 16 MB (`SpriteHistory.cs:8-12`). **INFERÊNCIA:** um sprite de 128×128 com 64 frames gera cerca de 10 MB de JSON, ou seja, ~1 passo de undo.
  - **Cada edição grava JSON e PNG no disco** (`:116-118`).
  - Abre como `Dialog` em tela cheia, a partir de uma lista em `AlertDialog` (`MainActivity.Studio.cs:13-35`, `:59-77`).
- **Tile Studio:** guarda snapshots integrais do mapa no undo, e o limite histórico é 64×64 (auditoria de 27/10, itens 2 e 8). Faltam zoom/pan, autotiling e pincéis.
- **Audio Studio:** só WAV PCM, com fonte de até 24 MiB.
- **Atlas/Palette/UI/Physics/Particle/Data Studio:** não existem.

### 2.7 O que funciona e deve ser preservado (FATO)

- **Framework:** Box2D com mutex de ciclo de vida, `SpriteBatch` sem alocação, TileMap com A*, partículas, UI e câmera.
- **Compilação:** Roslyn on-device com `ChangeClassifier` para decidir entre hot reload e restart.
- **Build e versionamento:** empacotador offline de APK com assinatura, alinhamento e identidades; Git em C# puro.
- **Persistência:** `AutosaveJournal`, `AtomicFile`, `WorkspaceLayout` persistido.
- **IA:** `AgentRuntime.Core` projetado.
- **Testes:** cerca de 750 testes, mais a jornada E2E com emulador.

**Nada disso precisa ser reescrito.**

### 2.8 Processo (FATO)

O agente anterior registra em todas as entradas do `AgentsChat.md` que **não tinha .NET nem Android SDK**. Toda a verificação foi feita pelo CI, e a interface nunca foi vista por ele. Isso explica uma interface criada "às cegas", como sequência de botões e diálogos. Daqui em diante, qualquer trabalho de UI precisa de **captura de tela automática** (emulador do CI) anexada ao PR.

---

## 3. Pesquisa externa: mecanismos aprendidos

| Referência | Mecanismo observado | Lição para o Lunet | Decisão |
|---|---|---|---|
| **Godot, editor Android** ([docs](https://docs.godotengine.org/en/stable/tutorials/editor/using_the_android_editor.html), [beta Play Store](https://godotengine.org/article/android_godot_editor_play_store_beta_release/)) | A documentação admite "UX not optimized for Android phones" e recomenda tablet com teclado e mouse. Desde 2025 o jogo roda **embutido** no editor, com controle de velocidade e mudo (4.6). O GABE gera APK/AAB no aparelho ([4.6 beta](https://godotengine.org/article/dev-snapshot-godot-4-6-beta-1/), [GABE](https://godotengine.org/article/gabe-stable-release/)). | Portar a UI desktop para o celular falha. O celular precisa de desenho próprio. Preview embutido com controles é a expectativa básica. | **Adaptar** (preview como painel) / **rejeitar** a UI desktop espremida. |
| **Comunidade Godot** ([r/godot](https://reddit.birdcat.cafe/r/godot/comments/1jv0uk7/whats_everyone_thoughts_on_the_mobile_version_of), [proposta #9144](https://github.imc.re/godotengine/godot-proposals/discussions/9144), [#7639](https://github.imc.re/godotengine/godot-proposals/discussions/7639)) | Reclamações repetidas: o teclado abre a cada ação; a orientação é forçada para paisagem; Ctrl+atalhos não funcionam no Gboard. Pedido: teclado do editor acoplável. "No celular, inútil." | O IME só deve aparecer em campo de texto. É preciso uma barra de teclas extras própria. Retrato tem de funcionar. | **Incorporar.** |
| **TIC-80 / PICO-8** ([wiki TIC-80](https://github.com/nesbox/TIC-80/wiki), [borbware](https://borbware.github.io/tic80-basics/tic80-basics/0-what-is-tic80)) | Código, sprite, mapa, sfx e música são **abas do mesmo ambiente**, a uma tecla ou toque de distância. "Criar o jogo sem sair do ambiente." | Os Studios devem ser abas do workspace, não diálogos. O mesmo projeto aparece em várias vistas. | **Incorporar** (é o modelo mental certo para o Lunet). |
| **Apps mobile de pixel art** (Resprite, [PxPad](https://apps.apple.com/app/pxpad/id6747214858), [Pixel Artistry](https://apps.apple.com/se/app/pixel-artistry/id1625694752?l=en-GB), convenção do Procreate ([Xojo](https://blog.xojo.com/2019/01/16/the-two-finger-tap-undo-gesture-for-ios-apps/))) | Toque com 2 dedos desfaz e com 3 refaz; pinça faz zoom e 2 dedos fazem pan e rotação; gestos são mapeáveis e **podem ser desligados** ([Pigment](https://support.pixiteapps.com/hc/en-us/articles/48619251072403-Enable-Disable-Two-Finger-Undo)); existe modo só caneta. | O canvas dos Studios precisa de um motor de gestos comum e configurável. | **Incorporar.** |
| **Pixelorama no Android** ([devlog v1.1.7](https://orama-interactive.itch.io/pixelorama/devlog/1128716/pixelorama-v117-is-out-with-an-experimental-android-version)) | Os próprios autores dizem que "a UI não é amigável no mobile" e planejam um "Simple Mode". Toque longo vira clique direito. | Uma ferramenta rica precisa de **divulgação progressiva**: um modo essencial e um modo completo. | **Adaptar.** |
| **Aseprite** ([comunidade](https://community.aseprite.org/c/features/7)) | Elogios: timeline de camadas × frames, preview em tempo real e atalhos configuráveis. Críticas: curva de aprendizado e posição fixa da timeline. | O modelo de documento precisa de camadas × frames × células. A timeline deve ser um dock móvel. | **Incorporar** o modelo de dados. |
| **LDtk** ([auto-layers](https://ldtk.io/docs/general/auto-layers), [r/gamedev](https://redlib.hackliberty.org/r/gamedev/comments/1l5vhrw/tiled_vs_ldtk/mwk0zd4/?context=3)) | Motivo citado para preferir LDtk ao Tiled: **regras de autotile visuais** sobre IntGrid, sem script; mostra as células afetadas pela regra; exporta para Tiled. | O Tile Studio deve ter IntGrid com regras visuais e exportação para formato aberto. | **Adaptar.** |
| **Sora Editor / AndroidIDE** ([sora-editor](https://www.github.com/Rosemoe/sora-editor), [overview](https://project-sora.github.io/sora-editor-docs/guide/editor-overview)) | Editor **com view própria**: análise incremental em background, completions publicadas sob demanda, lupa de texto, sticky scroll, módulo LSP e TextMate. É a base do AndroidIDE e do Android Code Studio. | Uma view própria de código é o padrão maduro no Android, e o teto do `EditText` já está documentado no ADR 0003. Não se copia o código (Java), mas se aprende a arquitetura. | **Investigar** (spike). |
| **Cursor / Zed** ([Zed agent panel](https://zed.dev/docs/ai/agent-panel), [fórum Cursor](https://forum.cursor.com/t/bring-back-per-change-apply-inline-diff-review-you-re-throwing-away-your-best-ux-advantage/160856)) | O Zed permite aceitar ou rejeitar **cada hunk**, ou tudo de uma vez, numa revisão multi-arquivo. No Cursor, quando o diff inline sumiu, a comunidade reagiu forte: a revisão por mudança é "a melhor vantagem de UX". | Revisão por hunk é o que gera confiança na IA. Edição inline sobre a seleção é o gesto principal. | **Incorporar.** |
| **IA dentro de editores de jogos** ([Ziva](https://ziva.sh/), [AI Assistant Hub](https://www.summerengine.com/blog/godot-ai-assistant-hub-alternative), [Beckett MCP](https://forum.godotengine.org/t/beckett-zero-sidecar-mcp-server-for-godot-4-2-the-ai-sees-and-optionally-playtests-your-game/141177)) | O diferencial é o contexto de jogo: ler a árvore de cena e os erros do editor, **ver e jogar o jogo**. O Hub é só uma interface para o modelo que o usuário escolhe. | O Lunet deve dar à IA ferramentas de compilar, rodar, capturar frame, ler console e inspector. Isso nenhum editor de texto genérico oferece. | **Incorporar.** |
| **Material 3 adaptativo** ([codelab](https://developer.android.com/codelabs/adaptive-material-guidance), [canonical layouts](https://goo.gle/large-screen-layouts)) | Classes de janela compacta/média/expandida. Lista-detalhe e painel de apoio ficam **lado a lado só na expandida**; nas outras, vão em pilha. | Um único modelo de layout atende celular, tablet e desktop, com regras por largura. | **Incorporar.** |
| **Armazenamento de segredos** ([SecureStorage](https://learn.microsoft.com/en-us/xamarin/essentials/secure-storage), [Android blog](https://android-developers.googleblog.com/2020/02/data-encryption-on-android-with-jetpack.html)) | AES-GCM com chave no AndroidKeyStore e IV aleatório por cifragem. | Resolve o "configurar a IA toda hora" e cumpre o SPEC §16. | **Incorporar.** |
| **Avalonia 12** ([Avalonia 12](https://avaloniaui.net/blog/avalonia-12), [embed native views](https://docs.avaloniaui.net/docs/guides/platforms/android/embed-native-views)) | Backend Android reescrito; números do fabricante, não verificados: start 1.960→460ms e scroll 42→120 FPS. `NativeControlHost` embute Views Android, como o GL do preview e um editor nativo. O Urbe já usa Avalonia para Android e Windows (ADR-0032). | É candidato a casca única para Android e desktop. Riscos não verificados: controle de código com IME no Android e GL embutido. | **Investigar** (spike, §6.1). |

## 4. Comunidades: o que aprovam, rejeitam e desejam

- **Aprovam:**
  - Criar o jogo inteiro sem sair do ambiente (TIC-80/PICO-8).
  - Revisão por mudança da IA (Cursor, Zed).
  - Gestos de 2 e 3 dedos (convenção do Procreate).
  - Regras visuais de tile (LDtk).
  - Timeline de camadas × frames (Aseprite).
  - Preview do jogo embutido (Godot 4.4+).
- **Rejeitam:**
  - UI de desktop espremida no celular (Godot, Pixelorama, ditos pelos próprios autores).
  - Teclado que abre sozinho (Godot; Acode #2374).
  - IA aplicando mudanças sem revisão (fórum do Cursor).
  - Gestos que não podem ser desligados (pedidos no Resprite e no Pigment).
- **Desejam:**
  - Teclado e barra de teclas próprios do editor (Godot #7639).
  - Retrato utilizável (Godot #9144).
  - "Modo simples" para toque (Pixelorama).
  - IA que enxerga e testa o jogo (Beckett, Ziva).
- **Confiança:** padrões repetidos em várias fontes valem como "padrão". Itens de uma única fonte ou de páginas de fornecedor são indício. Nenhuma estatística foi inventada.

## 5. Confronto: matriz

| Necessidade | Lunet hoje | Decisão | Critério verificável |
|---|---|---|---|
| IA sem sair do código | Diálogo a 3 níveis de profundidade | **Incorporar:** IA como superfície permanente (§6.3) | Do editor até o prompt em ≤1 toque, com seleção e arquivo como contexto automático |
| Configurar IA uma vez | Chave em memória | **Incorporar:** Keystore com presets de provider | Reiniciar o app mantém provider, modelo e chave. A chave nunca aparece no projeto, no log ou no APK |
| IA que verifica | Só escreve arquivos | **Incorporar:** ferramentas de compilar, diagnósticos, rodar, capturar e console | O agente só declara sucesso com compilação sem erros. Teste com LLM falso que erra e depois corrige |
| Revisão da IA | Escrita direta no disco | **Incorporar:** checkpoint → diff por hunk → aceitar/reverter | Reverter restaura os bytes originais. Teste de ida e volta |
| Notas, esboços e esquemas no app | Não existem | **Incorporar:** pasta `Notes/` em Markdown + esboço rápido (PNG/JSON) no projeto | Abrir nota ao lado do código no tablet e em aba no celular, sem sair do app |
| Código com espaço | ~15 linhas com teclado (INFERÊNCIA) | **Incorporar:** uma barra contextual + barra de teclas extras | ≥ 22 linhas visíveis com teclado num aparelho de 6,1", medido em captura |
| Studios como parte do fluxo | Diálogos em tela cheia | **Incorporar:** abas do workspace (modelo TIC-80) | Alternar entre código e sprite em 1 toque, preservando estado e undo de ambos |
| Canvas de desenho | Botões de zoom, cor em hex | **Incorporar:** motor de gestos comum + seletor HSV/paleta | Pinça, pan, desfazer com 2 dedos e refazer com 3, desligáveis. Teste de reconhecedor |
| Undo dos Studios | Snapshot JSON inteiro | **Incorporar:** undo por diferença (região de pixels/células) | Um sprite de 128×128 com 64 frames mantém ≥ 100 passos de undo em ≤ 16 MB |
| Gravação dos Studios | JSON + PNG a cada traço | **Adaptar:** autosave com debounce + journal existente | Nenhum traço perdido após kill do processo. Gravação ≤ 1 por segundo |
| Camadas no sprite | Não existem | **Incorporar:** documento camadas × frames × células (Aseprite) | O formato `.sprite.json` v2 lê o v1 sem perda |
| Editor escalável | EditText com respan total | **Investigar:** view própria (modelo Sora), reaproveitando `Lunet.Editor` e `PieceTable` | Arquivo de 20 mil linhas a ≤ 16 ms por quadro de rolagem, medido em aparelho |
| Desktop | Impossível hoje | **Decisão do proprietário** (§6.1) | Spike com a mesma tela rodando em Android e Windows |
| Hot reload "mágico" | Não prometido (correto) | **Preservar** | — |
| Reescrever o Framework ou o compilador | — | **Rejeitar** | Não há motivo: são as partes boas |

---

## 6. Proposta de direção (PROPOSTA, aguarda alinhamento)

### 6.1 Tecnologia da casca (exige decisão: NN-011, SPEC §2/§3)

| Opção | Prós | Contras |
|---|---|---|
| **A. Views Android reestruturadas** (componentes, tema, view-models) | Menor risco imediato, IME nativo | **Não leva ao desktop.** Contradiz #864 |
| **B. Avalonia** (mesma pilha do Urbe), com views nativas embutidas no Android onde for crítico (preview GL e, se necessário, editor) | Uma casca para Android e Windows. Know-how e padrões de UX do Urbe no mesmo repositório, sem dependência entre Products (NN-002). C# declarativo, temas e estilos | Precisa de spike: IME de código no Android, GL embutido, latência e tamanho do APK |
| **C. UI própria desenhada pelo Lunet.Framework** (como o editor do Godot) | Dogfooding total | Custo enorme, IME e acessibilidade difíceis. É a raiz exata dos problemas do Godot no celular |

**Recomendação:** **B, condicionada a um spike de 2–3 dias** com critérios mensuráveis:
- Editor C# com IME correto (acentos, autocorreção, teclado de terceiros).
- Preview GL embutido a 60 FPS.
- Abertura a frio ≤ 1,5 s e rolagem a 60 FPS num aparelho modesto.
- Aumento do APK medido.

Se o spike falhar, cai-se para **A** com desktop adiado. As camadas de domínio já existentes (Editor, Compiler, Studio documents, Ai) não mudam em nenhuma opção.

### 6.2 Arquitetura de informação

**Celular (retrato):**
- Barra superior única: projeto ▸ arquivo, **Run/Stop**, **IA**.
- Área principal com **abas de vista**: Código · Jogo · Studio · Notas.
- Barra de teclas extras acima do teclado (`{ } ( ) ; = < > " Tab`, setas, desfazer/refazer sem roubar o foco).
- Erros e Console num **bottom sheet** arrastável.
- Arquivos em gaveta.
- A palette de comandos é a busca universal: arquivos, símbolos, comandos e perguntas à IA.

**Tablet e desktop (expandida):**
- Rail de navegação.
- Explorer.
- Editor em split (código | jogo, ou código | sprite).
- Painel de apoio à direita para IA, Inspector e Notas.
- Dock inferior para Problemas, Console e Profiler.
- Layouts persistidos (o `WorkspaceLayout` atual já tem o modelo).
- Atalhos de teclado reais, com conjunto padrão no estilo VS Code, mostrados na palette.

**Direção visual:**
- Tokens de cor, tipo, espaçamento e raio num tema único, com claro e escuro.
- Ícones vetoriais coerentes.
- Hierarquia por peso, não por bordas.
- Movimento curto e funcional.
- Alvos ≥ 48dp.
- Verificação com TalkBack.

### 6.3 IA como colega de trabalho (o ponto mais crucial)

1. **Configuração de uma vez:**
   - Presets de provider (OpenRouter, Anthropic, OpenAI, Gemini, compatível/local) com link "criar chave" e catálogo de modelos.
   - A chave fica no Keystore.
   - Teste de conexão com mensagem clara de erro: 401, cota ou modelo inexistente.
2. **Três gestos:**
   - **Inline:** selecionar código → "Pedir à IA" → diff no próprio editor.
   - **Companheiro:** um painel que não tampa o código (sheet no celular, painel lateral no tablet) e já sabe o arquivo, a seleção, os erros e o console.
   - **Agente:** tarefas longas com plano visível e passos observáveis.
3. **Contexto automático:**
   - Arquivo aberto, seleção e diagnósticos.
   - Saída do console e frame atual do jogo (captura).
   - Notas do projeto e API do Lunet (a documentação offline já existe em `Lunet.Docs`).
4. **Ferramentas de jogo:**
   - `compile`, `diagnostics`, `run`/`stop`, `capture_frame`, `console.read`, `inspector.read`.
   - Depois: assets (criar sprite/tile/som via documentos dos Studios).
   - Humano e agente usam as mesmas operações (SPEC §16).
5. **Confiança:** checkpoint antes de agir, revisão por hunk em vários arquivos, "aceitar tudo" e "reverter", e custo e tokens visíveis.
6. **Sem copiar o Urbe:** reaproveitar o `AgentRuntime.Core` (já projetado) e seguir os mesmos padrões de UX. Qualquer extração comum passa por Extraction Review (NN-022).

### 6.4 Studios

1. **Um motor de canvas comum:**
   - Gestos configuráveis.
   - Rail de ferramentas e folha de propriedades contextual.
   - Dock de camadas e timeline.
   - Undo por diferença e autosave com debounce.
   - Isso serve a Sprite, Tile, Atlas, UI e Particle, e é feito uma vez só.
2. **Sprite v2:**
   - Camadas × frames × células.
   - Seleção e laço com transformação.
   - Simetria.
   - Paleta e seletor HSV.
   - Onion skin configurável.
   - Exportação PNG e spritesheet, com leitura do v1.
3. **Tile v2:**
   - Zoom e pan.
   - IntGrid com regras visuais de autotile (LDtk).
   - Pincéis.
   - Mapa grande por chunks.
   - Preview do jogo em split.
4. **Autonomia (#865):** formatos abertos (PNG, WAV, JSON documentado), sem depender do Framework para editar.

### 6.5 Critérios de aceite de UX (para não repetir o C4)

Todo PR de UI deve trazer:
- Capturas automáticas do emulador nas larguras compacta e expandida, em retrato e paisagem, com e sem teclado.
- Contagem de toques das jornadas-chave (abrir projeto → editar → rodar; pedir à IA → revisar → aceitar; código → sprite → código).
- Métricas de quadro em rolagem.
- Roteiro DEVICE com **avaliação qualitativa** do proprietário, e não só "funciona".

## 7. Sequência proposta (a consolidar no ROADMAP após o alinhamento)

| ID proposto | Entrega | Depende de |
|---|---|---|
| LUNET-481 | **IA utilizável já, sem esperar a casca nova:** chave no Keystore, presets e catálogo de modelos, contexto de arquivo, seleção e diagnósticos, ferramentas `compile`/`diagnostics`, sucesso só com build verde, checkpoint com reverter. Domínio em `Lunet.Ai`/`Lunet.Core`, testado em headless | — |
| LUNET-482 | Spike da casca (opção B) com os critérios de §6.1 e relatório | Decisão C1 |
| LUNET-483 | Sistema de design (tokens, tema, ícones) e nova arquitetura de informação do workspace (§6.2) | 482 |
| LUNET-484 | Superfícies de IA: inline, companheiro e agente, com revisão por hunk | 481, 483 |
| LUNET-485 | Notas e esboços dentro do projeto | 483 |
| LUNET-486 | Motor de canvas comum + Sprite v2 | 483, C3 |
| LUNET-487 | Tile v2 | 486 |
| LUNET-488 | Editor de código: view própria ou caminho decidido pelo spike, com metas de desempenho | 482 |

O LUNET-481 pode começar imediatamente: não depende de decisão de casca e ataca a dor mais citada pelo proprietário ("configurar toda hora e nem funcionar direito").

## 8. Decisões necessárias do proprietário

1. **C1/C2:** atualizar o SPEC §2/§3/§11 de "Android only / landscape prioritário" para "Android primeiro, desktop previsto; retrato e paisagem de primeira classe".
2. **Tecnologia da casca:** autorizar o spike da opção B (Avalonia) antes de qualquer reescrita da UI.
3. **Escopo do redesenho:** confirmar que a camada visual será **refeita** sobre os módulos existentes (sem reescrever Framework, Compiler, Editor services nem Build).
4. **Notas e esboços no projeto:** confirmar o formato (Markdown em `Notes/` + esboços em PNG/JSON) como parte do produto.

Essas decisões serão registradas como `pending` no portal (`docs/governance/decisions.json`), com ADR "Proposto", na etapa de consolidação. Este documento não as resolve.
