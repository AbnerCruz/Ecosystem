# Roadmap

Fonte de verdade do que existe e do que falta. A especificação completa está em [`docs/SPEC.md`](docs/SPEC.md); as seções citadas como **§N** referem-se a ela. A rotina de trabalho está em [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

## Regras deste arquivo

- Uma caixa `[x]` só vale quando **implementação, testes, documentação e integração** estão completos (§24) **e** o comportamento visível ao usuário foi validado no aparelho quando depende de Android.
- Trabalho feito mas ainda não validado em aparelho fica `[ ]` com a marca **(feito, sem validação em aparelho)**.
- Trabalho parcial fica `[ ]` com o que falta escrito ao lado. Nunca marque por aproximação (§30, §31).
- Não se avança para a fase seguinte antes do gate da fase atual **e** da auditoria da fase (ver `docs/DEVELOPMENT.md`). Exceção: itens transversais listados na seção "Transversal".
- Estado do resumo por fase: `bash tools/roadmap-status.sh`.

Legenda de estado das fases: ✅ concluída (gate + auditoria) · 🟡 em andamento · ⬜ não iniciada.

## Estado atual

| Fase | Estado |
| --- | --- |
| 0 Foundation | 🟡 faltam as pastas do §26 (criadas quando houver conteúdo) |
| 1 Vertical Slice | 🟡 gate validado; faltam Explorer, recuperação após crash, "Tutorial" na criação de projeto |
| 2 Framework Core | 🟡 Demo validado; faltam música, gamepad, haptics, atlas, Lifecycle/Dispatcher/GameServices, Transform2D/Ray2D/Geometry |
| 3 IDE | 🟡 editor validado só em desktop; falta a maior parte (docs offline, painéis, busca, rename...) |
| 4–15 | ⬜ |

---

## Fase 0 — Foundation (§28)

Gate: GitHub Actions gera APK instalável. **Atingido** (validado em aparelho, v0.0.1-dev.8).

- [x] Repositório, projetos `Lunet.Framework/Core/Compiler/Runtime/Editor/Android`, `tests/`.
- [x] Regras de dependência verificadas por teste (`ArchitectureTests`).
- [x] README, ROADMAP, CHANGELOG, `docs/adr/`, `VERSION`.
- [x] CI: restore, build, testes, validação de arquitetura, APK, checksums, release de desenvolvimento (§25).
- [x] Android shell e primeiro APK instalável (validado em aparelho).
- [x] Arquivo de solução (`Lunet.slnx`) na raiz.
- [x] `LICENSE.md` proprietária provisória (uso proprietário, sem redistribuição, modificação ou exploração comercial por terceiros sem autorização; código exibido no app é só para estudo). Revisável no futuro.
- [x] `release-notes.json` publicado junto de cada release (§24, §25) (verificado na v0.0.1-dev.22).
- [ ] Estrutura de pastas do §26 criada onde já há conteúdo: `samples/`, `templates/`, `runtime-template/`, `native/`, `site/` (criar cada uma quando a primeira entrega existir, não antes).

## Fase 1 — Vertical Slice (§27, §33)

Gate: no telefone é possível criar projeto, escrever C#, apertar Run e mover sprite com toque. **Atingido** (validado em aparelho).

- [x] Criar projeto, persistir como pasta comum com `lunet.json` (§5).
- [x] Editar C# com salvamento atômico.
- [x] Roslyn no Android, com referências em memória e diagnósticos com arquivo/linha/coluna (ADR 0002).
- [x] Framework mínimo: `Game`, `GameTime`, `SpriteBatch`, `Texture2D`, toque.
- [x] Renderer OpenGL ES 3.x, sprite e toque.
- [x] Preview com Run, Stop, Restart, Pause, Step (§10).
- [x] Fluxo completo criar → editar → Run → mover sprite.
- [ ] Explorer (painel lateral com árvore, pastas recolhíveis, novo arquivo/pasta, renomear, excluir) **(feito, sem validação em aparelho)**.
- [ ] Recuperação após crash: working buffer a cada pausa na digitação, descarte ao salvar, oferta de recuperação ao abrir o projeto (§22) **(feito, sem validação em aparelho)**; falta snapshot do projeto inteiro.
- [ ] Exportar ZIP validado em aparelho (feito, sem validação em aparelho).
- [ ] Criação de projeto: opção "Tutorial" além de "Em branco" e "Demo" (§33) — depende da Fase 11.
- [ ] Workspace mínimo do §33 completo: Explorer, Editor, Preview, Problems/Console, Documentation.
- [ ] Auditoria de fechamento da Fase 1 registrada em `docs/audits/`.

## Fase 2 — Framework Core (§7)

Gate: pequeno jogo 2D completo somente com código. **Atingido** pelo Demo "Coletor de moedas" (validado em aparelho).

Core
- [x] `Game`, `GameTime`, `GameConfiguration`, `GameLog`.
- [x] Loop de passo fixo, interpolação, proteção contra spiral-of-death, pause/resume.
- [ ] `GameServices` (registro de serviços do jogo).
- [ ] `Dispatcher` (executar trabalho na thread do jogo).
- [ ] Lifecycle completo do Android (onStop/onDestroy, perda de contexto GL sem reiniciar o jogo).
- [ ] Suporte a refresh rate superior a 60 Hz no desenho.
- [ ] Timers e object pools (§7 complementares).

Matemática
- [x] `RectangleF`, `Circle`, `Color`, `MathEx`, `RandomSource`.
- [ ] `Transform2D`, `Ray2D`, `Geometry` (SAT, distância, interseções §7 Física/camada simples).

Graphics
- [x] `GraphicsDevice`, `Texture2D`, `SpriteBatch`, `SpriteSheet`, resolução virtual com letterbox, filtro Point/Linear, fonte bitmap embutida.
- [ ] `RenderTarget2D`.
- [ ] `Shader` e `Material` personalizados.
- [ ] `BlendState`, `SamplerState`, `Viewport` como tipos públicos.
- [ ] Clipping (scissor por SpriteBatch), pixel perfect, DPI scaling, safe areas.
- [ ] Texture atlas em runtime (usa o formato do Atlas Studio).
- [ ] Debug drawing.
- [ ] Medição de alocações por quadro (sem alocação no caminho quente).

Input
- [x] Toque (`TouchPoint`), Tap, LongPress, Drag, Swipe, teclado, acelerômetro.
- [ ] `TouchCollection` como tipo público, `Pointer`.
- [ ] DoubleTap, Pinch, Rotate.
- [ ] `VirtualStick` e botões virtuais.
- [ ] Gamepad.
- [ ] Giroscópio.
- [ ] Haptics.

Áudio
- [x] `SoundEffect`, `SoundInstance` (volume, pan, pitch, loop) com SoundPool.
- [ ] `Music` em streaming.
- [ ] `AudioBus` (volume por grupo).
- [ ] Fade.
- [ ] Baixa latência com Oboe/AAudio se a medição exigir (§7).
- [ ] Contador de audio underruns para o Profiler.

Conteúdo e armazenamento
- [x] Decodificador PNG, `ContentManager` com cache, importação de PNG.
- [x] Salvamento JSON com gravação atômica.
- [ ] Carregar fontes, dados (JSON) e mapas via `ContentManager`.
- [ ] Localization.
- [ ] Content pipeline com cache de assets processados e invalidação.

Auditoria
- [ ] Auditoria de fechamento da Fase 2 registrada em `docs/audits/`.

## Fase 3 — IDE (§9, §11, §12, §15)

Gate: experiência de IDE real.

Editor de código (§9)
- [ ] Realce, números de linha, desfazer/refazer, indentação, autocompletar, definição, referências, dica, localizar/substituir, diagnósticos ao vivo **(feito, sem validação em aparelho)**.
- [ ] Multi-cursor / multi-seleção.
- [ ] Code folding.
- [ ] Atalhos de teclado (teclado físico).
- [ ] Minimap opcional.
- [ ] Documentos grandes e virtualização (medir; ADR 0003 prevê view própria se necessário).
- [ ] Estrutura de texto eficiente (piece table/rope) se a medição justificar.

Roslyn (§9)
- [ ] Renomear símbolo.
- [ ] Formatação de código.
- [ ] Quick fixes e code actions.
- [ ] Compilação incremental (reaproveitar compilação entre Runs).
- [ ] Inspeção de símbolos.

IDE mobile (§11)
- [ ] Explorer (ver Fase 1).
- [ ] Painéis: Preview, Inspector, Console, Problems, Search, Assets, Documentation, Agent, Profiler.
- [ ] Reorganizar, redimensionar, esconder painéis; salvar layouts persistentes.
- [ ] Landscape prioritário, portrait utilizável; evitar seleção acidental nos controles.
- [ ] Busca no projeto inteiro.
- [ ] Console e Problems com logs exportáveis (§23).
- [ ] Settings.

Inspector (§12)
- [ ] Inspector padrão por reflexão; atributos Range, ReadOnly, Hidden, Multiline, Color, File, Asset, Group, Tooltip.
- [ ] `Inspector<T>` customizado.
- [ ] Inspecionar variáveis do jogo em execução (fluxo principal §29).

Documentação (§15)
- [ ] Documentação offline gerada de XML docs, Markdown e metadata da API, dentro do app.
- [ ] Cada API com descrição, assinatura, parâmetros, retorno, exemplos, remarks, relacionadas e versão de introdução.
- [ ] Integração com o editor: "Explain in Documentation".
- [ ] Documentação apontando para trechos dos jogos oficiais (depende da Fase 11).

Preview e hot reload (§10)
- [ ] Classificação de mudanças: hot reload possível × restart required.
- [ ] Fast Preview × Isolated Preview.

Git, autosave e recovery (§22)
- [ ] Autosave com journal (ver Fase 1) e recuperação.
- [ ] Git progressivo: status, diff, commit, history, branches, revert, push, pull.

Auditoria
- [ ] Auditoria de fechamento da Fase 3 registrada em `docs/audits/`.

## Fase 4 — Framework Advanced (§7)

Gate: jogos 2D substanciais apenas com APIs oficiais.

- [ ] Camada simples de colisão: AABB, Rectangle, Circle, Point, Ray, overlap, intersection, distance, SAT.
- [ ] Física completa (`PhysicsWorld`, `RigidBody2D`, `Collider2D`, `Fixture2D`, `Joint2D`, `Contact`, `Raycast`) sobre backend maduro (Box2D ou equivalente) sem expor tipos internos; ADR da escolha.
- [ ] `Camera2D`.
- [ ] Animação de sprites e tweening.
- [ ] Partículas.
- [ ] Tilemaps (carregar formato do Tile Studio, colisão).
- [ ] Fontes personalizadas (bitmap e TrueType).
- [ ] UI (layout, âncoras, nine-slice, temas).
- [ ] Pathfinding A*.
- [ ] Scene2D opcional e API entidade/componente opcional (sem obrigar ECS).
- [ ] Debug APIs e helpers.
- [ ] Profiler básico do framework (FPS, frame time, update/render time, draw calls, triângulos, memória, GC) (§23).
- [ ] Auditoria de fechamento da Fase 4 registrada em `docs/audits/`.

## Fase 5 — Studio Tools (§13)

Gate: a maior parte dos assets/dados necessários pode ser produzida dentro do Lunet. As ferramentas usam o `Lunet.Editor.SDK` (criado na Fase 6 ou antes, ver ADR).

- [ ] Sprite Studio: pixel art, layers, frames, timeline, onion skin, paletas, lápis, borracha, balde, conta-gotas, linhas, formas, seleção, laço, transformar, flip, rotate, simetria, grade, zoom, spritesheet, preview de animação, importar/exportar PNG, transparência, toque e stylus.
- [ ] Tile Studio: tilesets, edição de mapas, layers, camada de colisão, propriedades, autotiling, brushes, preview.
- [ ] Animation Studio: clips, timeline, frames, eventos, playback, animação de sprites, estados.
- [ ] Atlas Studio: criação de atlas, packing, preview, metadata.
- [ ] UI Studio: layouts, âncoras, margens, tamanhos, nine-slice, fontes, temas/skins, preview.
- [ ] Physics Studio: colliders, polígonos, círculos, triggers, joints, camadas de colisão, debug view.
- [ ] Particle Studio: emissor, preview, vida, velocidade, cores, tamanho, curvas, burst.
- [ ] Audio Studio: waveform, trim, loop points, volume, preview, preparação de assets.
- [ ] Palette Studio: criar, extrair, salvar/reusar, substituir cores.
- [ ] Data Studio: JSON estruturado, tabelas, schemas, dados de jogo, validação.
- [ ] Auditoria de fechamento da Fase 5 registrada em `docs/audits/`.

## Fase 6 — Plugins (§14)

Gate: plugin criado no Lunet adiciona painel e inspector sem modificar o app.

- [ ] `Lunet.Editor.SDK` (API pública compartilhada por ferramentas oficiais e plugins).
- [ ] `Lunet.PluginHost`: formato `*.lunetplugin` (`plugin.json`, `Plugin.dll`, `assets/`, `docs/`), carregamento, recarga.
- [ ] Permissões declarativas: `workspace.read`, `workspace.write`, `network`, `build`, `editor`, `assets`, `agent.tools`; consentimento explícito do usuário.
- [ ] Extensões: painéis, comandos, menus, inspectors, importers, exporters, templates, code actions, build steps, documentação, agent tools, asset tools, studio tools.
- [ ] Criar e compilar plugin em C# dentro do próprio Lunet.
- [ ] Isolamento de falhas: plugin com erro não derruba o app (§13).
- [ ] Fluxo de aceitação "Plugin" (§29).
- [ ] Auditoria de fechamento da Fase 6 registrada em `docs/audits/`.

## Fase 7 — Networking (§8)

Gate: dois clientes jogam demonstração realtime com latência simulada.

- [ ] `Lunet.Networking`: `INetworkTransport`, transports substituíveis (LiteNetLib interno, sem vazar tipos).
- [ ] Serialização, `NetworkClock`, conexões, sessões, mensagens, RPC.
- [ ] `NetworkIdentity`, `NetworkObject`, `NetworkVariable`.
- [ ] Snapshots, replicação, interpolação, predição, reconciliação.
- [ ] Abstrações de lobby e matchmaking; diagnósticos.
- [ ] Client-hosted, dedicated server e backend-integrated; servidor .NET headless; projetos `Shared/`, `Client/`, `Server/`.
- [ ] Network Simulator: latência, jitter, perda, duplicação, limite de banda.
- [ ] Testes de rede (serialização, reconexão, perda de pacotes).
- [ ] Auditoria de fechamento da Fase 7 registrada em `docs/audits/`.

## Fase 8 — Agentic Workspace (§16)

Gate: modelo configurado altera código, compila, detecta erro, corrige e executa. IA é opcional; nada depende dela.

- [ ] Model Gateway, Provider Adapters (OpenAI, Anthropic, Gemini, OpenRouter, OpenAI-compatible, custom), Capability Discovery.
- [ ] Tool API interna própria (mesmas operações usadas por humano e plugin) e depois bridge MCP.
- [ ] Ferramentas tipadas: Project, Code, Compiler, Runtime, Testing, Assets, Documentation, Build, Versioning/checkpoints.
- [ ] Context Engine por relevância (árvore, símbolos, referências, diagnósticos, arquivos relacionados, logs, histórico, docs, diffs, memória do projeto).
- [ ] Agent Runtime com loop Understand → Plan → Inspect → Act → Compile → Run/Test → Verify → Repair → Finish; não concluir só por ter escrito arquivos.
- [ ] Permissões, observabilidade e custo.
- [ ] Chaves de API no Android Keystore; nunca no projeto, Git ou APK exportado.
- [ ] Edits com checkpoint → mudanças → diff → verificação → aceitar/reverter; modos Ask, Edit, Agent.
- [ ] Fluxo de aceitação "Agent" (§29); testes com saídas ruins de LLM e agente descontrolado.
- [ ] Auditoria de fechamento da Fase 8 registrada em `docs/audits/`.

## Fase 9 — APK Build (§18, §19)

Gate obrigatório: desligar internet, criar projeto, programar, buildar APK, instalar e jogar. Se etapa local for inviável, implementar fallback via GitHub build e **registrar ADR**; não fingir.

- [ ] Experimento inicial de viabilidade: Game.dll + Content + runtime template → APK (AAPT2, alinhamento, assinatura, arm64) — decidir e registrar ADR (§32 risco 4).
- [ ] `runtime-template/` e `Lunet.Build`.
- [ ] Empacotamento, recursos, AAPT2, alinhamento (zipalign) e assinatura.
- [ ] Assinatura de desenvolvimento automática; assinatura de release com chave do usuário (gerar, importar, proteger, verificar, backup, aviso de perda). Nunca usar chave do Lunet nos jogos.
- [ ] Build & Install e exportar APK; requisitos modernos do Android (API alvo).
- [ ] Fluxo de aceitação principal (§29) completo, offline.
- [ ] Auditoria de fechamento da Fase 9 registrada em `docs/audits/`.

## Fase 10 — Cloud Build (§18)

Gate: gerar AAB usando apenas telefone + GitHub.

- [ ] Integração com GitHub (opcional, sem conta obrigatória).
- [ ] Geração de workflow no projeto do usuário; build oficial .NET Android; APK e AAB; logs; dependências nativas e APIs Android avançadas.
- [ ] Auditoria de fechamento da Fase 10 registrada em `docs/audits/`.

## Fase 11 — Tutorial Games (§15)

Gate: cada jogo compila, roda, está documentado e ensina efetivamente seu nível.

- [ ] Tutorial 1 — Platformer (iniciante): estrutura, C# aplicado, Update/Draw, sprites, toque, movimento, colisão, câmera, animações, áudio, UI, checkpoints, save.
- [ ] Tutorial 2 — Top-down Action Roguelite (intermediário): tilemap, inimigos, IA, A*, armas, projéteis, partículas, pools, geração de salas, inventário, upgrades, HUD, serialização, assets, profiling.
- [ ] Tutorial 3 — Online Co-op Action RPG/Survival (avançado): cliente, servidor, shared code, lobby, conexão, replicação, snapshots, interpolação, predição, reconciliação, network variables, combate, inimigos, reconexão, diagnósticos, simulação de rede.
- [ ] Documentação apontando para arquivos e trechos dos três jogos.
- [ ] Fluxo de aceitação "Multiplayer" (§29).
- [ ] Auditoria de fechamento da Fase 11 registrada em `docs/audits/`.

## Fase 12 — Updates / Monetization Infrastructure (§19, §20, §21, §25)

Sem paywall real obrigatório.

- [ ] Settings → Updates com canais Stable, Beta e Development: consultar GitHub Releases, mostrar versão, patchnotes, tamanho, data e canal; Check → Download → Verify → pedir instalação.
- [ ] SDK de content updates (manifest, hashes, substituição atômica, rollback); binário novo só por novo APK/AAB; nunca baixar código remoto para contornar Android/Play.
- [ ] `Lunet.Entitlements` e `IEntitlementProvider`; provider de desenvolvimento habilita tudo; arquitetura para PlayBilling, DirectSubscription e Enterprise; recursos pagos consultam entitlements centralizados; assinatura expirada nunca bloqueia acesso, edição ou exportação dos projetos.
- [ ] Metadata da futura loja no `lunet.json`: GameId, PackageId, DeveloperId, Version, Title, Description, Icon, Screenshots, Tags, AgeMetadata, BuildHash, Signature, ReleaseChannel; publicação por adapters (APK, GitHub Release, AAB). A loja em si **não** é implementada.
- [ ] Landing page estática em `site/` para GitHub Pages (sem atrasar o app).
- [ ] Auditoria de fechamento da Fase 12 registrada em `docs/audits/`.

## Fase 13 — Hardening (§30)

- [ ] Process death, pouca memória, rotação, projeto corrompido, arquivos enormes, muitos assets.
- [ ] Plugins que falham e plugins malformados; falhas do compilador.
- [ ] Saídas ruins de LLM e agente descontrolado; erros de assinatura e de atualização; falhas de rede.
- [ ] Testes de ciclo de vida do Android e smoke tests em CI onde possível.
- [ ] Auditoria de fechamento da Fase 13 registrada em `docs/audits/`.

## Fase 14 — Beta

- [ ] Documentação completa e tutoriais completos.
- [ ] Revisão de API pública (nada vazando detalhes internos).
- [ ] Revisão de desempenho (§23) e testes de migração de projetos.
- [ ] Releases beta no canal Beta.
- [ ] Auditoria de fechamento da Fase 14 registrada em `docs/audits/`.

## Fase 15 — 1.0

Somente quando os acceptance flows funcionarem no aparelho.

- [ ] Fluxo principal (§29) completo, offline.
- [ ] Fluxo Agent, Plugin e Multiplayer (§29).
- [ ] Auditoria final contra o spec inteiro (§1–§36) registrada em `docs/audits/`.

---

## Transversal (valem em qualquer fase)

Princípios (§2), checados em toda auditoria de fase:
- [ ] Offline first: editor, projetos, docs, Roslyn, compilador, Preview, framework, plugins locais, profiler, build local funcionam sem internet.
- [ ] Nenhuma conta obrigatória; nenhuma telemetria obrigatória.
- [ ] Projetos são arquivos normais; usuário mantém propriedade.
- [ ] APIs públicas sem detalhes internos; sem dependências circulares (testado).
- [ ] Ferramentas oficiais usam as mesmas APIs dos plugins.

Qualidade (§30):
- [ ] Testes de: unidade, integração, compilador, sistema de projetos, framework, serialização, rede, plugins, build, smoke e ciclo de vida Android.
- [ ] Sem TODO crítico escondido, mock passado como feature, botão sem comportamento, exceção ignorada, API sem documentação, código morto.

Observabilidade (§23):
- [ ] Profiler: FPS, frame time, update/render time, draw calls, triângulos, memória, GC, memória de assets, audio underruns.
- [ ] Monitorar memória, Roslyn, responsividade do editor, carga de assets e latência de build.
- [ ] Erros de build, Roslyn, runtime, plugin e agente aparecem nos painéis certos; logs exportáveis.

Entrega (§24, §25):
- [x] CHANGELOG com Added/Changed/Fixed/Performance/Deprecated/Removed/Security.
- [x] SemVer com `VERSION`.
- [x] Cada alteração na `main` com CI verde gera development release com APK, checksums e notas.
- [x] `release-notes.json` (ver Fase 0).
- [ ] ADR para toda decisão arquitetural relevante (existentes: 0001–0003).

## Riscos técnicos (§32)

1. Roslyn no Android — **validado** (v0.0.1-dev.8).
2. Runtime/Preview executando jogo compilado — **validado**.
3. Renderer 2D funcional e performático — funcional **validado**; desempenho ainda sem medição.
4. Estratégia de geração de APK do jogo — **não validada**; experimento na Fase 9 (adiantar se houver dúvida sobre viabilidade).

## Aceitação final (§29)

- [ ] Principal: instalar → offline → criar projeto → C# → autocompletar → docs offline → criar/editar sprite → Run → jogar no Preview → inspecionar variáveis → corrigir → áudio → física → gerar APK → instalar o jogo → abrir → jogar → compartilhar.
- [ ] Agent.
- [ ] Plugin.
- [ ] Multiplayer.
