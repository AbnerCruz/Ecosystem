# Prompt de implementação integral --- Lunet2D

## MISSÃO

Você é o agente principal de engenharia responsável por construir
integralmente o projeto **Lunet2D**.

Não produza apenas planejamento, scaffolding, mockups ou código
demonstrativo. Sua responsabilidade é transformar esta especificação em
um software real, funcional, testado, versionado e publicado no GitHub.

O repositório se chama:

`Lunet2D`

O produto se chama:

**Lunet**

O resultado deverá ser um aplicativo Android instalável distribuído por
**GitHub Releases**.

Ao final de cada estágio funcional, o repositório deve permanecer
compilável e utilizável.

A prioridade absoluta é:

> entregar software funcional de ponta a ponta, não acumular subsistemas
> incompletos.

------------------------------------------------------------------------

## 1. DEFINIÇÃO DO PRODUTO

Lunet é um ambiente completo de desenvolvimento de jogos 2D criado
especificamente para Android.

É simultaneamente:

-   IDE C# mobile;
-   framework 2D próprio;
-   runtime integrado;
-   compilador;
-   ambiente de preview;
-   suíte de ferramentas para desenvolvimento 2D;
-   plataforma de plugins;
-   ambiente agêntico com LLMs;
-   framework multiplayer;
-   sistema de build e distribuição Android;
-   ambiente educacional completo.

A filosofia é inspirada conceitualmente em XNA/MonoGame, mas Lunet não é
um clone de MonoGame e não deve depender de MonoGame como API pública.

O usuário programa jogos em **C#**.

O código é o centro da experiência.

Painéis, inspectors e ferramentas visuais são auxiliares e nunca
substituem obrigatoriamente o código.

------------------------------------------------------------------------

## 2. PRINCÍPIOS INEGOCIÁVEIS

Considere estas regras constitucionais do projeto:

-   Android only.
-   Mobile first.
-   2D only.
-   C# como linguagem principal dos jogos.
-   Offline first.
-   Nenhuma conta obrigatória.
-   IA completamente opcional.
-   Nenhum jogo deve depender da IA.
-   Framework independente da IDE.
-   Jogos independentes da IDE.
-   Projetos armazenados como arquivos normais.
-   Nada de formato proprietário obscuro para código.
-   Usuário mantém propriedade integral dos projetos.
-   Ferramentas visuais são opcionais.
-   Recursos devem atender tanto iniciantes quanto usuários avançados.
-   Plugins são parte fundamental da arquitetura.
-   Ferramentas oficiais devem usar, sempre que tecnicamente possível,
    as mesmas APIs expostas aos plugins.
-   Model agnostic.
-   Provider agnostic.
-   APIs públicas não devem vazar detalhes internos desnecessariamente.
-   Documentação deve funcionar offline.
-   Nenhuma telemetria obrigatória.
-   Nunca sequestrar projeto por assinatura vencida.
-   Sempre preferir sistemas simples, observáveis, testáveis e
    substituíveis.
-   Não adicionar tecnologia apenas por parecer moderna.

------------------------------------------------------------------------

## 3. TECNOLOGIA BASE

Use como linha principal:

-   .NET 10 LTS
-   C# 14
-   `net10.0-android`
-   Android API target compatível com API 36
-   Android mínimo: API 29
-   ABI inicial prioritária: `arm64-v8a`

Não use MAUI como fundação da aplicação.

Lunet é Android-only e deve trabalhar diretamente com a plataforma
Android através de .NET for Android.

------------------------------------------------------------------------

## 4. ARQUITETURA PRINCIPAL

Estruture o código aproximadamente assim:

``` text
src/
├── Lunet.Android/
├── Lunet.Core/
├── Lunet.Editor/
├── Lunet.Language/
├── Lunet.Compiler/
├── Lunet.Framework/
├── Lunet.Runtime/
├── Lunet.Build/
├── Lunet.Editor.SDK/
├── Lunet.PluginHost/
├── Lunet.Agent/
├── Lunet.Networking/
└── Lunet.Studio/
```

Também:

``` text
tests/
samples/
templates/
docs/
runtime-template/
native/
site/
.github/workflows/
```

Respeite regras rígidas de dependência.

Conceitualmente:

``` text
Game
 ↓
Lunet.Framework

IDE
 ↓
Framework + Compiler + Runtime + Editor SDK

Plugins
 ↓
Editor SDK / Framework

Agent
 ↓
Tool API

Framework X IDE
Framework X Agent
Game X Agent
```

Não introduza dependências circulares.

------------------------------------------------------------------------

## 5. PROJETOS LUNET

Um projeto deve ser uma pasta normal.

A tela de projetos permite importar ZIPs exportados pelo Lunet e ZIPs com
`lunet.json` na raiz. A importação valida manifesto, arquivo de entrada e
caminhos antes de publicar o projeto completo. Nunca sobrescreve um projeto
existente: em conflito de nome, cria uma cópia com sufixo numérico, preservando
GameId e demais metadados. Limites locais: ZIP de até 256 MiB, até 10.000
entradas, até 512 MiB descompactados e manifesto de até 1 MiB. Arquivos fora
do projeto, caminhos ambíguos e links são recusados. Cancelamento não altera
os projetos; falhas removem a área temporária.

Exemplo:

``` text
MyGame/
├── lunet.json
├── Game.cs
├── Code/
├── Content/
│   ├── Textures/
│   ├── Audio/
│   ├── Fonts/
│   ├── Maps/
│   └── Data/
├── Tools/
├── Plugins/
├── Tests/
└── .lunet/
    ├── cache/
    ├── builds/
    └── agent/
```

`lunet.json` deve controlar pelo menos nome, GameId, package id, versão,
versão do framework, entry point, orientação, resolução virtual, assets,
plugins, dependências, configurações de build e propriedades de
publicação.

------------------------------------------------------------------------

## 6. MODELO DE COMPILAÇÃO

O jogo não deve passar pelo build Android completo cada vez que o
usuário aperta Run.

``` text
C#
 ↓
Roslyn
 ↓
Game Assembly
 ↓
Lunet Runtime
 ↓
Preview
```

O código do jogo deve depender principalmente de `Lunet.Framework`, e
não diretamente das APIs Android.

Separe código do jogo, runtime Android, host Android e framework Lunet.

------------------------------------------------------------------------

## 7. FRAMEWORK 2D

Construa uma API própria, moderna e coerente. Não copie cegamente
MonoGame.

### Core

Inclua conceitos equivalentes a `Game`, `GameTime`, `GameConfiguration`,
`GameServices`, `Lifecycle`, `Logger` e `Dispatcher`.

Game loop: update fixo, rendering desacoplado, interpolação, proteção
contra spiral-of-death, suporte a refresh rates superiores, pause/resume
Android e lifecycle completo. Padrão inicial: 60 updates/s,
configurável.

### Matemática

Use `System.Numerics` sempre que apropriado: `Vector2`, `Vector3`,
`Matrix3x2`, `Matrix4x4`, `Quaternion`.

Implemente apenas extensões realmente úteis: `RectangleF`, `Circle`,
`Transform2D`, `Ray2D`, `Color`, `Geometry`, `MathEx`, `RandomSource`.

### Graphics

API pública deve incluir conceitos como `GraphicsDevice`, `Texture2D`,
`Sprite`, `SpriteBatch`, `RenderTarget2D`, `Shader`, `Material`, `Font`,
`Camera2D`, `Viewport`, `BlendState`, `SamplerState`.

Backend inicial: **OpenGL ES 3.x**.

Inclua sprite batching, texture atlases, spritesheets, render targets,
custom shaders, pixel perfect, clipping, blending, filtering, virtual
resolution, DPI scaling, safe areas, debug drawing e câmera 2D. Evite
allocations por frame.

### Input

Touch é entrada primária. Implemente `Pointer`, `TouchPoint`,
`TouchCollection`, `Tap`, `DoubleTap`, `LongPress`, `Drag`, `Swipe`,
`Pinch`, `Rotate`, `Gesture`, `VirtualStick`, além de Keyboard, Gamepad,
Accelerometer, Gyroscope e Haptics.

### Áudio

Construa `Lunet.Audio` com `SoundEffect`, `SoundInstance`, `Music`,
`AudioBus`, Volume, Pan, Pitch, Loop e Fade. Priorize baixa latência.
Pode utilizar bridge nativa enxuta com Oboe/AAudio se necessário.

### Física

Tenha camada simples própria para AABB, Rectangle, Circle, Point, Ray,
overlap, intersection, distance e SAT helpers, além de camada física
completa baseada internamente em Box2D ou solução equivalente madura.

API pública: `PhysicsWorld`, `RigidBody2D`, `Collider2D`, `Fixture2D`,
`Joint2D`, `Contact`, `Raycast`. Não exponha tipos internos do backend.

### Sistemas complementares

Planeje e implemente Camera2D, animation, tweening, particles, tilemaps,
fonts, UI, content pipeline, caching, save system, JSON/data,
localization, pathfinding A\*, timers, object pools, debug helpers,
optional Scene2D e optional convenience entity/component API.

Não obrigue ECS.

------------------------------------------------------------------------

## 8. NETWORKING

Crie `Lunet.Networking` para multiplayer online real.

Inclua Transport, Serialization, NetworkClock, Connections, Sessions,
Messages, RPC, NetworkIdentity, NetworkObject, NetworkVariable,
Snapshots, Replication, Interpolation, Prediction, Reconciliation, Lobby
abstractions, Matchmaking abstractions e Diagnostics.

Crie `INetworkTransport` e permita transports substituíveis. Pode
utilizar LiteNetLib internamente como primeiro transport realtime, sem
expor seus tipos na API pública.

Suporte client-hosted, dedicated server e backend-integrated
multiplayer. Permita gerar servidor .NET headless quando apropriado, com
projetos separando `Shared/`, `Client/` e `Server/`.

Crie Network Simulator nativo para latency, jitter, packet loss,
duplication e bandwidth throttling.

------------------------------------------------------------------------

## 9. EDITOR DE CÓDIGO E ROSLYN

Não trate o editor como TextBox glorificado. Construa arquitetura
própria com documentos grandes, undo/redo robusto, seleção,
multi-selection, multi-cursor, clipboard, IME Android, syntax
highlighting, line numbers, diagnostics, code folding, search/replace,
indentation, keyboard shortcuts, minimap opcional e virtualização.

Use estrutura eficiente como piece table, rope ou equivalente.

Roslyn deve fornecer parsing, compilation, syntax tree, semantic model,
autocomplete, diagnostics, hover, go-to-definition, find references,
rename, formatting, quick fixes, code actions e symbol inspection.

A compilação incremental é prioridade.

------------------------------------------------------------------------

## 10. PREVIEW E HOT RELOAD

Run executa:

``` text
save buffers
 ↓
compile
 ↓
diagnostics
 ↓
load assembly
 ↓
instantiate game
 ↓
run inside Preview
```

Controles: Run, Stop, Restart, Pause, Step.

Crie conceitos internos Fast Preview e Isolated Preview.

Não prometa hot reload mágico. Classifique mudanças entre hot reload
possível e restart required. Previsibilidade é mais importante que
marketing.

------------------------------------------------------------------------

## 11. IDE MOBILE

A IDE precisa ser desenhada para celular, não como desktop espremido.

Painéis: Explorer, Editor, Preview, Inspector, Console, Problems,
Search, Assets, Documentation, Agent e Profiler.

Permita reorganizar, redimensionar, esconder, salvar layouts e layouts
persistentes. Landscape prioritário, portrait utilizável. Evite seleção
acidental de texto nos controles.

------------------------------------------------------------------------

## 12. INSPECTOR

Implemente inspector padrão baseado em reflexão/metadados. Campos comuns
devem gerar controles automaticamente.

Crie atributos opcionais como Range, ReadOnly, Hidden, Multiline, Color,
File, Asset, Group e Tooltip.

Permita `Inspector<T>` customizado e substituição completa por plugins.

------------------------------------------------------------------------

## 13. LUNET STUDIO TOOLS

Crie uma suíte nativa de ferramentas 2D usando ao máximo o
`Lunet.Editor.SDK`.

### Sprite Studio

Pixel art, raster simples, layers, frames, timeline, onion skin,
palettes, pencil, eraser, fill, eyedropper, linhas, formas, selection,
lasso, transform, flip, rotate, symmetry, grid, zoom, spritesheet,
animation preview, PNG import/export e transparency. Touch e stylus são
de primeira classe.

### Tile Studio

Tilesets, map editing, layers, collision layer, properties, autotiling,
brushes e tile preview.

### Animation Studio

Clips, timeline, frames, events, playback, sprite animation e state
configuration.

### Atlas Studio

Texture atlas creation, packing, preview e metadata.

### UI Studio

Layouts, anchors, margins, sizing, nine-slice, fonts, themes/skins e
preview.

### Physics Studio

Collider editing, polygons, circles, triggers, joints, collision layers
e debug view.

### Particle Studio

Particle emitter editor, preview, lifetime, velocity, colors, size,
curves e burst.

### Audio Studio

Waveform, trim, loop points, volume, preview e preparação básica de
assets. Não tente virar DAW.

### Palette Studio

Palette creation, extraction, save/reuse e color replacement helpers.

### Data Studio

Structured JSON editing, tables, schemas, game data e validation.

------------------------------------------------------------------------

## 14. PLUGIN SDK

Plugins são requisito fundamental.

Formato sugerido: `*.lunetplugin`, contendo `plugin.json`, `Plugin.dll`,
`assets/` e `docs/`.

Plugins podem adicionar panels, commands, menus, inspectors, importers,
exporters, templates, code actions, build steps, documentation, agent
tools, asset tools e custom studio tools.

Crie permissões declarativas como `workspace.read`, `workspace.write`,
`network`, `build`, `editor`, `assets`, `agent.tools`.

Não prometa sandbox perfeita. Plugins são código executável confiado
explicitamente pelo usuário.

O usuário deve conseguir criar plugins em C# dentro do próprio Lunet.

Ferramentas oficiais devem, sempre que tecnicamente razoável, usar as
mesmas APIs públicas disponíveis aos plugins.

------------------------------------------------------------------------

## 15. DOCUMENTAÇÃO OFFLINE E TUTORIAIS

Documentação completa vem no aplicativo, gerada a partir de XML docs,
Markdown, API metadata e examples.

Cada API deve possuir descrição, assinatura, parâmetros, retorno,
exemplos, remarks, APIs relacionadas e versão de introdução quando
aplicável.

Integre documentação ao editor.

Crie três jogos oficiais completos:

### Tutorial 1 --- Platformer

Nível iniciante: estrutura, C# básico aplicado, Game, Update, Draw,
sprites, touch, movimento, colisão, câmera, animações, áudio, UI,
checkpoints e save.

### Tutorial 2 --- Top-down Action Roguelite

Nível intermediário: arquitetura maior, tilemap, inimigos, IA, A\*,
armas, projéteis, partículas, object pools, geração de salas,
inventário, upgrades, HUD, serialização, assets e profiling.

### Tutorial 3 --- Online Co-op Action RPG/Survival

Nível avançado: cliente, servidor, shared code, lobby, conexão,
replication, snapshots, interpolation, prediction, reconciliation,
network variables, combate, inimigos, reconnection, diagnostics e
network simulation.

Os três devem ser jogos completos e jogáveis.

Documentação deve apontar diretamente para arquivos e trechos desses
projetos, e o editor deve oferecer algo equivalente a
`Explain in Documentation`.

------------------------------------------------------------------------

## 16. AGENTIC WORKSPACE

Implemente um **model-agnostic agentic workspace with tool-using LLM
agents**, não simplesmente chatbot.

Arquitetura: Model Gateway, Provider Adapters, Capability Discovery,
Context Engine, Agent Runtime, Tool Registry, Permission System,
Verification, Observability, Cost Tracking e MCP Bridge.

Capabilities podem incluir text, reasoning, vision, tools, structured
output, streaming e context size.

Permita adapters para OpenAI, Anthropic, Gemini, OpenRouter,
OpenAI-compatible e custom endpoint. Nunca espalhe lógica específica de
provider pelo restante da aplicação.

Agent loop:

``` text
Understand
 ↓
Plan
 ↓
Inspect
 ↓
Act
 ↓
Compile
 ↓
Run/Test
 ↓
Verify
 ↓
Repair
 ↓
Finish
```

O agente não pode considerar tarefa concluída apenas porque escreveu
arquivos.

Ferramentas tipadas devem cobrir Project, Code, Compiler, Runtime,
Testing, Assets, Documentation, Build e Versioning/checkpoints.

Não crie infraestrutura paralela específica para IA. Humano, plugin e
agente devem compartilhar operações fundamentais.

O Context Engine deve construir contexto por relevância usando project
tree, symbols, references, diagnostics, related files, runtime logs,
task history, documentation, diffs/checkpoints e project memory.
Embeddings são opcionais.

Crie Tool API interna própria e depois bridge MCP.

API keys devem usar Android Keystore e nunca entrar no projeto, Git ou
APK exportado.

Edits agênticos devem seguir checkpoint → changes → diff → verification
→ accept/revert.

Modos sugeridos: Ask, Edit e Agent.

------------------------------------------------------------------------

## 17. OFFLINE FIRST

Sem internet devem continuar funcionando editor, projetos, documentação,
Roslyn, compiler, preview, framework, tools, plugins locais, profiler,
debugger, local build, APK export e jogos.

Internet adiciona LLM agents, GitHub, cloud build, package downloads,
updates, publicação e serviços multiplayer externos.

------------------------------------------------------------------------

## 18. LOCAL APK PACKAGER E CLOUD BUILD

Valide cedo o empacotamento local:

``` text
Game code
 ↓
Roslyn
 ↓
Game.dll

Game.dll + Content
 ↓
Lunet Android Runtime Template
 ↓
APK packaging
 ↓
alignment
 ↓
signing
 ↓
Game.apk
```

Valide AAPT2, packaging, alignment, signing, installation, arm64 e
requisitos modernos do Android.

Se alguma etapa local for concretamente inviável, preserve a arquitetura
e implemente fallback via GitHub build. Não finja funcionamento.

Cloud/advanced build:

``` text
Lunet project
 ↓
GitHub
 ↓
GitHub Actions
 ↓
official .NET Android toolchain
 ↓
APK / AAB
```

Atenda AAB, full Android builds, native dependencies e advanced Android
APIs.

------------------------------------------------------------------------

## 19. SIGNING, UPDATES E DISTRIBUIÇÃO

Development signing automático. Release signing usa chave do usuário,
com geração/importação/proteção/verificação/backup e aviso sobre perda
da chave.

Nunca use chave do Lunet nos jogos do usuário.

Separe content updates de binary updates. Assets/dados podem usar
manifest, hashes, atomic replacement e rollback. Mudança de código gera
novo APK/AAB. Não implemente download arbitrário de código remoto para
contornar Android/Play.

------------------------------------------------------------------------

## 20. MONETIZAÇÃO

Prepare arquitetura agora, sem paywall durante desenvolvimento inicial.

Crie `Lunet.Entitlements` e `IEntitlementProvider`.

Provider de desenvolvimento habilita tudo.

Arquitetura futura deve permitir PlayBillingProvider,
DirectSubscriptionProvider e EnterpriseProvider.

Nunca espalhe `if (user.IsPro)` pelo sistema. Features pagas consultam
entitlements centralizados.

Regra absoluta: assinatura expirada nunca impede acesso, edição ou
exportação dos próprios projetos.

Suporte arquitetural futuro para mensal, anual, trial, promoções,
feature entitlements e planos diferentes, preservando offline-first.

------------------------------------------------------------------------

## 21. FUTURA LOJA

Não implemente a loja agora.

Apenas prepare metadata consistente: GameId, PackageId, DeveloperId,
Version, Title, Description, Icon, Screenshots, Tags, AgeMetadata,
BuildHash, Signature e ReleaseChannel.

Publicação deve ser adapter-based para APK, GitHub Release e AAB,
permitindo futura loja Lunet sem reescrever os projetos.

------------------------------------------------------------------------

## 22. GIT, AUTOSAVE E RECOVERY

Integração Git progressiva: status, diff, commits, history, branches,
revert, push e pull. GitHub é opcional. Checkpoints do Agent não
dependem obrigatoriamente de Git.

Autosave deve usar working buffer, autosave journal, atomic save e
recovery snapshot. Após crash, ofereça recuperação. Permita exportar
projeto inteiro como ZIP.

------------------------------------------------------------------------

## 23. PERFORMANCE E OBSERVABILIDADE

Monitore memory, allocations, frame time, Roslyn memory, editor
responsiveness, asset loading e build latency.

Evite parsing desnecessário, allocations por frame, leitura completa
constante, reconstrução total da UI e caches ilimitados.

Profiler deve mostrar FPS, frame time, update time, render time, draw
calls, triangles, memory, GC, asset memory e audio underruns.

Logs devem ser claros e exportáveis. Build errors, Roslyn errors,
runtime crashes, plugin errors e agent errors devem aparecer nos painéis
apropriados.

------------------------------------------------------------------------

## 24. DOCUMENTAÇÃO DE ARQUITETURA, ROADMAP E CHANGELOG

Crie `docs/adr/` e use Architecture Decision Records com Context,
Decision, Consequences e Alternatives.

Crie `ROADMAP.md` com checkboxes. Uma checkbox só pode ser marcada
quando a feature passa seu critério de aceite, incluindo implementação,
testes, documentação e integração.

Crie `CHANGELOG.md` com Added, Changed, Fixed, Performance, Deprecated,
Removed e Security.

Mantenha também `release-notes.json`.

Use Semantic Versioning e mantenha `VERSION`.

------------------------------------------------------------------------

## 25. CI/CD E RELEASES

Configure GitHub Actions.

Cada alteração na `main` deve executar restore, build, tests,
architecture validation, gerar APK quando tudo passar, checksums,
development release, anexar APK e release notes e atualizar metadata.

Não publique build quebrado.

Release deve conter algo equivalente a:

``` text
Lunet-vX.Y.Z-arm64.apk
release-manifest.json
release-notes.md
SHA256SUMS.txt
```

Dentro do aplicativo, crie Settings → Updates com canais Stable, Beta e
Development.

Consulte GitHub Releases e mostre versão, patchnotes, tamanho, data e
canal. Fluxo: Check → Download → Verify → solicitar instalação Android.

Crie landing page estática simples para GitHub Pages, sem permitir que
trabalho cosmético atrase o aplicativo.

------------------------------------------------------------------------

## 26. ESTRUTURA DO REPOSITÓRIO

``` text
Lunet2D/
├── README.md
├── ROADMAP.md
├── CHANGELOG.md
├── VERSION
├── LICENSE.md
├── src/
├── native/
├── tests/
├── samples/
├── templates/
├── docs/
├── runtime-template/
├── site/
└── .github/
    └── workflows/
```

Ajuste somente quando houver justificativa arquitetural.

------------------------------------------------------------------------

## 27. DESENVOLVIMENTO POR FATIAS VERTICAIS

Não produza 100 classes e zero experiência completa.

Primeira fatia:

``` text
Create Project
 ↓
Edit C#
 ↓
Compile
 ↓
Draw sprite
 ↓
Read touch
 ↓
Preview
```

Depois expanda, sempre preservando uma versão utilizável.

------------------------------------------------------------------------

## 28. FASES E GATES

Cada gate de fase é validado pelo usuário na release candidata. Ao terminar
a implementação da fase, entregue instruções detalhadas e reproduzíveis
para testar no aparelho cada funcionalidade e o gate: link do APK,
preparação, passos, resultados esperados, regressões relevantes e como
relatar falhas. Registre a versão e o retorno na auditoria. Uma fase
só passa à próxima após aprovação explícita do usuário; se falhar,
corrija e envie nova versão com roteiro de reteste.

### Fase 0 --- Foundation

Repo, solution, projects, architecture rules, README, ROADMAP,
CHANGELOG, ADR, VERSION, CI, Android shell e primeiro APK.

**Gate:** GitHub Actions gera APK instalável.

### Fase 1 --- Vertical Slice

Project creation, explorer, C# editor, Roslyn, diagnostics, minimal
Framework, OpenGL ES, sprite, touch, Preview, Run/Stop.

**Gate:** no telefone é possível criar projeto, escrever C#, apertar Run
e mover sprite com touch.

### Fase 2 --- Framework Core

Core, Time, Graphics, Math, Input, Content, Storage e Audio.

**Gate:** pequeno jogo 2D completo somente com código.

### Fase 3 --- IDE

Editor definitivo, IntelliSense, autocomplete, diagnostics, navigation,
search, Problems, Console, layouts e docs.

**Gate:** experiência de IDE real.

### Fase 4 --- Framework Advanced

Physics, animation, camera, tilemaps, particles, fonts, UI, pathfinding,
optional Scene2D e debug APIs.

**Gate:** jogos 2D substanciais apenas com APIs oficiais.

### Fase 5 --- Studio Tools

Sprite, Tile, Animation, Atlas, UI, Physics, Particle, Audio, Palette e
Data Studios.

**Gate:** maior parte dos assets/dados necessários pode ser produzida
dentro do Lunet.

### Fase 6 --- Plugins

SDK, manifests, compiler, host, permissions, custom
panels/inspectors/importers/exporters/templates/docs/agent tools.

**Gate:** plugin criado no Lunet adiciona painel e inspector sem
modificar o app.

### Fase 7 --- Networking

Transport abstraction, serialization, sessions, RPC, identity,
replication, snapshots, interpolation, prediction, reconciliation,
simulator e headless server.

**Gate:** dois clientes jogam demonstração realtime com latência
simulada.

### Fase 8 --- Agentic Workspace

Gateway, providers, capabilities, tools, context, checkpoints, diff,
agent loop, verification, MCP, permissions e cost reporting.

**Gate:** modelo configurado altera código, compila, detecta erro,
corrige e executa.

### Fase 9 --- APK Build

Runtime template, packaging, resources, AAPT2, alignment, signing, Build
& Install e export APK.

**Gate obrigatório:** desligar internet, criar projeto, programar,
buildar APK, instalar e jogar.

### Fase 10 --- Cloud Build

GitHub integration, workflow generation, official Android build, APK,
AAB, logs e advanced dependencies.

**Gate:** gerar AAB usando apenas telefone + GitHub.

### Fase 11 --- Tutorial Games

Construa integralmente os três jogos oficiais.

**Gate:** cada jogo compila, roda, está documentado e ensina
efetivamente seu nível.

### Fase 12 --- Updates / Monetization Infrastructure

Updater, channels, patchnotes, content update SDK, entitlement
architecture e future subscription adapters. Sem paywall real
obrigatório.

### Fase 13 --- Hardening

Teste process death, low memory, rotation, corrupted project, huge
files, many assets, plugin crashes, malformed plugins, compiler
failures, bad LLM outputs, runaway agent, signing errors, update errors
e networking failures.

### Fase 14 --- Beta

Docs completos, tutorials completos, API review, performance review,
migration tests e beta releases.

### Fase 15 --- 1.0

Somente quando os acceptance flows completos funcionarem.

------------------------------------------------------------------------

## 29. ACCEPTANCE FLOWS

### Principal

``` text
Install Lunet
 ↓
Disable internet
 ↓
Create project
 ↓
Write C#
 ↓
Use autocomplete
 ↓
Read offline docs
 ↓
Create/edit sprite
 ↓
Run
 ↓
Play inside Preview
 ↓
Inspect variables
 ↓
Fix code
 ↓
Use audio
 ↓
Use physics
 ↓
Build APK
 ↓
Install generated game
 ↓
Open generated game
 ↓
Play it
 ↓
Share APK
```

### Agent

``` text
Configure provider/model
 ↓
Ask agent to add gameplay feature
 ↓
Agent inspects project
 ↓
Plans
 ↓
Edits
 ↓
Compiles
 ↓
Gets errors
 ↓
Repairs
 ↓
Runs game
 ↓
Verifies
 ↓
Shows diff/results
```

### Plugin

``` text
Create plugin project
 ↓
Write C#
 ↓
Compile inside Lunet
 ↓
Install plugin
 ↓
Reload
 ↓
New panel appears
 ↓
Plugin adds custom inspector
```

### Multiplayer

``` text
Open Tutorial 3
 ↓
Start host/server
 ↓
Connect second client
 ↓
Players move
 ↓
Replication works
 ↓
Enable latency simulation
 ↓
Interpolation/prediction remain stable
 ↓
Disconnect/reconnect
 ↓
Session recovers appropriately
```

------------------------------------------------------------------------

## 30. QUALIDADE, TESTES E COMMITS

Não aceite TODO crítico escondido, mock como feature real, botão sem
comportamento, checkbox falsa, exception ignorada, API sem docs, código
morto, duplicação de infraestrutura ou hacks que inviabilizem evolução.

Crie unit tests, integration tests, compiler tests, project-system
tests, framework tests, serialization tests, networking tests, plugin
tests, build tests, smoke tests e Android lifecycle tests onde possível.

Faça commits pequenos e coerentes, por exemplo:

``` text
feat(framework): add fixed game loop
feat(graphics): implement sprite batching
feat(editor): add Roslyn diagnostics
fix(runtime): recover preview after compile failure
docs(adr): document assembly-based game model
```

Evite commits gigantes chamados `update`.

------------------------------------------------------------------------

## 31. NÃO PARE EM PLANEJAMENTO

Você já possui o planejamento.

Sua função agora é implementar.

Não responda apenas com arquitetura, proposta, ideias ou lista de
arquivos.

Crie arquivos, escreva código, execute builds, teste e corrija.

Se alguma feature não puder ser concluída corretamente:

1.  mantenha-a desmarcada no ROADMAP;
2.  documente o blocker;
3.  preserve o build funcional;
4.  prossiga com tarefas independentes;
5.  retorne posteriormente ao blocker.

Nunca transforme limitação real em falsa implementação.

------------------------------------------------------------------------

## 32. RISCOS TÉCNICOS PRIORITÁRIOS

Antes de investir pesado em ferramentas avançadas, valide:

1.  Roslyn funcionando adequadamente no Android.
2.  Runtime/Preview executando jogo compilado.
3.  Renderer 2D funcional e performático.
4.  Estratégia de geração de APK do jogo.

Esses experimentos devem acontecer cedo.

Se uma hipótese arquitetural precisar mudar por limitação real da
plataforma, registre ADR e escolha a solução que melhor preserve os
princípios do Lunet.

------------------------------------------------------------------------

## 33. EXPERIÊNCIA DO PRIMEIRO APK

Mesmo a primeira versão utilizável deve apresentar:

``` text
Lunet
 ↓
Projects
 ↓
New Project
 ↓
Blank / Tutorial
 ↓
Workspace
```

Workspace mínimo:

``` text
Explorer
Editor
Preview
Problems/Console
Documentation
```

Inclua projeto inicial extremamente simples.

O usuário deve conseguir tocar Run e ver resultado.

------------------------------------------------------------------------

## 34. RESULTADO ESPERADO NO GITHUB

Quando houver versão funcional:

`Lunet2D` → Releases → versão mais recente → APK anexado.

Devo conseguir:

1.  abrir GitHub pelo celular;
2.  entrar em Releases;
3.  baixar APK;
4.  instalar;
5.  abrir Lunet;
6.  criar projeto;
7.  programar;
8.  executar;
9.  testar.

Cada nova alteração na `main` que passe pelos gates deve gerar nova
development release instalável.

------------------------------------------------------------------------

## 35. META FINAL

Não estamos construindo uma demonstração técnica.

Estamos construindo:

> **um ambiente completo, moderno, extensível, offline-first e agentic
> para desenvolver jogos 2D em C# diretamente em um dispositivo
> Android.**

O iniciante deve conseguir aprender dentro do próprio aplicativo.

O intermediário deve conseguir produzir jogos completos.

O avançado deve conseguir trabalhar diretamente com APIs de baixo nível,
substituir abstrações, criar ferramentas, plugins, infraestrutura
multiplayer e workflows próprios.

A IA amplia o ambiente, jamais é sua fundação.

As ferramentas visuais aceleram o desenvolvimento, jamais aprisionam o
usuário.

O framework permanece utilizável sem a IDE.

Os projetos permanecem propriedade e sob controle do usuário.

------------------------------------------------------------------------

## 36. INSTRUÇÃO FINAL DE EXECUÇÃO

Comece imediatamente pela auditoria do repositório `Lunet2D`.

Se estiver vazio, inicialize toda a estrutura.

Implemente primeiro Fundação e Vertical Slice.

Depois avance milestone por milestone.

A cada etapa:

``` text
implement
→ build
→ test
→ run
→ inspect
→ fix
→ document
→ update ROADMAP
→ update CHANGELOG
→ commit
→ push
```

Não espere confirmação entre milestones quando o próximo passo estiver
claramente definido.

Não abandone problema simplesmente criando workaround visual.

Resolva causas raiz.

Sempre preserve `main` em estado utilizável.

Quando houver APK testável, publique-o em GitHub Releases.

Continue evoluindo conforme ROADMAP.

O primeiro grande marco é inequívoco:

> **GitHub Release → baixar Lunet APK → instalar no Android → criar um
> projeto C# → apertar Run → jogar o resultado dentro do próprio
> Lunet.**

A partir desse ponto, continue implementando a especificação completa
até que os gates restantes sejam satisfeitos.
