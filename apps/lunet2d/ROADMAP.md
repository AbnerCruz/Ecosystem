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
| 0 Foundation | ✅ concluída (auditoria em `docs/audits/fase-0.md`) |
| 1 Vertical Slice | ✅ concluída (auditoria em `docs/audits/fase-1.md`) |
| 2 Framework Core | ✅ concluída e aprovada (auditoria em `docs/audits/fase-2.md`) |
| 3 IDE | ✅ concluída e aprovada (LUNET-303 / Issue #204; auditoria em `docs/audits/fase-3.md`) |
| 4 Framework Advanced | 🟡 em andamento |
| 5–15 | ⬜ |

---

## Fase 0 — Foundation (§28)

Gate: GitHub Actions gera APK instalável. **Atingido** (validado em aparelho, v0.0.1-dev.8).

- [x] Repositório, projetos `Lunet.Framework/Core/Compiler/Runtime/Editor/Android`, `tests/`.
- [x] Regras de dependência verificadas por teste (`ArchitectureTests`).
- [x] README, ROADMAP, CHANGELOG, `docs/adr/`, `VERSION`.
- [x] CI: restore, build, testes, validação de arquitetura, APK, checksums, release de desenvolvimento (§25).
- [x] Assinatura estável das builds de desenvolvimento (ADR 0004), verificada no CI, para que uma release atualize a anterior.
- [x] Android shell e primeiro APK instalável (validado em aparelho).
- [x] Arquivo de solução (`Lunet.slnx`) na raiz.
- [x] `LICENSE.md` proprietária provisória (uso proprietário, sem redistribuição, modificação ou exploração comercial por terceiros sem autorização; código exibido no app é só para estudo). Revisável no futuro.
- [x] `release-notes.json` publicado junto de cada release (§24, §25) (verificado na v0.0.1-dev.22).
- [x] Estrutura de pastas do §26 existente onde há conteúdo (`src/`, `tests/`, `docs/`, `tools/`, `.github/workflows/`). As pastas `samples/`, `templates/`, `runtime-template/`, `native/` e `site/` serão criadas junto da primeira entrega que as usa: `samples/` e `templates/` na Fase 11 (jogos oficiais e modelos como arquivos), `runtime-template/` na Fase 9, `native/` se a Fase 14 exigir Oboe, `site/` na Fase 12.
- [x] Auditoria de fechamento da Fase 0 registrada em `docs/audits/fase-0.md`.

## Fase 1 — Vertical Slice (§27, §33)

Gate: no telefone é possível criar projeto, escrever C#, apertar Run e mover sprite com toque. **Atingido** (validado em aparelho).

- [x] Criar projeto, persistir como pasta comum com `lunet.json` (§5).
- [x] Editar C# com salvamento atômico.
- [x] Roslyn no Android, com referências em memória e diagnósticos com arquivo/linha/coluna (ADR 0002).
- [x] Framework mínimo: `Game`, `GameTime`, `SpriteBatch`, `Texture2D`, toque.
- [x] Renderer OpenGL ES 3.x, sprite e toque.
- [x] Preview com Run, Stop, Restart, Pause, Step (§10).
- [x] Fluxo completo criar → editar → Run → mover sprite.
- [x] Explorer (painel lateral com árvore, pastas recolhíveis, novo arquivo/pasta, renomear, excluir) — validado em aparelho (pastas).
- [x] Recuperação após crash: working buffer a cada pausa na digitação, descarte ao salvar, oferta de recuperação ao abrir o projeto (§22). No aparelho, fechar o app pelos recentes já salva o texto (salvamento ao pausar); a recuperação de um encerramento sem pausa é coberta por testes automáticos. Snapshot do projeto inteiro: Fase 13.
- [x] Exportar ZIP: menu ⋯ do editor e toque longo na lista de projetos — validado em aparelho na v0.0.1-dev.63 (a barra do editor cortava o ⋯; corrigido).
- [x] Workspace mínimo do §33: Explorer, Editor, Preview, Problems/Console (Documentation: hoje só a referência rápida; painel de documentação completo na Fase 3, opção "Tutorial" na criação de projeto na Fase 11 — itens movidos para lá).
- [x] Auditoria de fechamento da Fase 1 registrada em `docs/audits/fase-1.md`.

## Fase 2 — Framework Core (§7)

Gate: pequeno jogo 2D completo somente com código. **Atingido** pelo Demo "Coletor de moedas" (validado em aparelho).

Core
- [x] `Game`, `GameTime`, `GameConfiguration`, `GameLog`.
- [x] Loop de passo fixo, interpolação, proteção contra spiral-of-death, pause/resume.
- [x] `GameServices` (registro de serviços do jogo; o host registra os padrão).
- [x] `Dispatcher` (executar trabalho na thread do jogo).
- [x] Lifecycle completo do Android: app em segundo plano pausa jogo e áudio; o contexto GL é preservado ao pausar (`PreserveEGLContextOnPause`) (validado em aparelho na v0.0.1-dev.53); se o driver perder o contexto, o jogo reinicia (recriar texturas sem reiniciar fica para a Fase 13).
- [x] Suporte a refresh rate superior a 60 Hz: atualização fixa independente da taxa (testada de 30 a 144 Hz) e pedido do modo de maior taxa no Preview (validado em aparelho na v0.0.1-dev.53).
- [x] Timers e object pools (§7 complementares).

Matemática
- [x] `RectangleF`, `Circle`, `Color`, `MathEx`, `RandomSource`.
- [x] `Transform2D`, `Ray2D`, `Geometry` (distância, interseções de segmentos, polígonos, SAT).

Graphics
- [x] `GraphicsDevice`, `Texture2D`, `SpriteBatch`, `SpriteSheet`, resolução virtual com letterbox, filtro Point/Linear, fonte bitmap embutida.
- [x] `Sprite` (textura/região, origem, cor, escala, rotação; `SpriteBatch.Draw(sprite, posição)`; `Sprite.FromAtlas`).
- [x] `RenderTarget2D` (FBO no OpenGL ES; testado com backend em memória) (validado em aparelho na v0.0.1-dev.53).
- [x] `Shader` (fragmento GLSL ES com uniforms) e `Material` (validado em aparelho na v0.0.1-dev.53).
- [x] `BlendState` (Alpha, Additive, Opaque, Multiply, Premultiplied), `SamplerState` e `Viewport` como tipos públicos (validado em aparelho na v0.0.1-dev.53).
- [x] Clipping (`Begin(clip:)`), pixel perfect (`PixelPerfect`), densidade (`Density`) e área segura (`SafeArea`, recorte de câmera) (validado em aparelho na v0.0.1-dev.53).
- [x] Texture atlas em runtime (`Content.LoadAtlas`, regiões com pivô, `SpriteBatch.Draw(atlas, região, ...)`); o formato JSON fica como contrato para o Atlas Studio (Fase 5).
- [x] Debug drawing (`Line`, `Rect`, `FillRect`, `Circle`, `Cross`) (validado em aparelho na v0.0.1-dev.53).
- [x] Sem alocação por quadro no caminho quente do framework (Tick, SpriteBatch, DrawString, gestos, timers, controles virtuais), verificado por teste de alocação; a medição em aparelho fica com o Profiler (Fase 4).

Input
- [x] Toque (`TouchPoint`), Tap, LongPress, Drag, Swipe, teclado, acelerômetro.
- [x] `TouchCollection` e `Pointer` como tipos públicos.
- [x] DoubleTap, Pinch, Rotate (testados com toques sintéticos; multitoque real só valida em aparelho — validado em aparelho na v0.0.1-dev.53).
- [x] `VirtualStick` e `VirtualButton` (validados no Laboratório, v0.0.1-dev.53).
- [x] Gamepad (botões, sticks, gatilhos, D-pad; `IsButtonDown/Pressed`) (validado em aparelho na v0.0.1-dev.53).
- [x] Giroscópio (`Input.Gyroscope`) (validado em aparelho na v0.0.1-dev.53).
- [x] Haptics (`Haptics.Vibrate`, permissão VIBRATE) (validado em aparelho na v0.0.1-dev.53).

Áudio
- [x] `SoundEffect`, `SoundInstance` (volume, pan, pitch, loop) com SoundPool.
- [x] `Music` em streaming (`Content.LoadMusic`, `Audio.PlayMusic/StopMusic`, MediaPlayer no Android) (validado em aparelho na v0.0.1-dev.53).
- [x] `AudioBus` (Master/Sfx/Music e barramentos próprios, mute) (validado em aparelho na v0.0.1-dev.53).
- [x] Fade de efeitos (`FadeTo`) e de música (validado em aparelho na v0.0.1-dev.53).

Conteúdo e armazenamento
- [x] Decodificador PNG, `ContentManager` com cache, importação de PNG.
- [x] Salvamento JSON com gravação atômica.
- [x] Carregar dados JSON (`Content.LoadJson<T>`) via `ContentManager` (lógica portátil, testada); fontes e mapas entram com Fonts/Tilemaps na Fase 4.
- [x] Localization (`SetLanguage`, reserva, formatação; lógica portátil testada). Conferência do idioma do aparelho: Fase 13.

Auditoria
- [x] Auditoria de fechamento da Fase 2 registrada em `docs/audits/fase-2.md` (aprovada pelo usuário com a v0.0.1-dev.53).

Itens movidos para outras fases, com motivo: baixa latência com Oboe/AAudio → Fase 14 (só se a medição de desempenho exigir); contador de audio underruns → Fase 4 (Profiler); recriar texturas quando o contexto GL é perdido e conferir o idioma do aparelho na `Localization` → Fase 13.

## Fase 3 — Lunet IDE (§9, §11, §12, §15)

Gate: experiência de IDE real. **Atingido e aprovado** em **LUNET-303**, Issue #200, candidato `lunet2d-v0.0.1-dev.1000008`. A validação humana integral foi aprovada pelo proprietário no portal pela Issue #204; registro canônico no handoff `HO-20261004-lunet-f3-candidate`.

- [x] LUNET-302 — Framework/IDE/Studio code-first (ADR 0007) e correções rápidas sem perda de código: PR #152 integrado, CI verde e C6 aprovado no portal pela Issue #155. Issue #149 encerrada; handoff `HO-20261003-lunet-code-first-safe-fixes`.
- [x] LUNET-301 — Importar projeto ZIP (§5, §22): PR #116 integrado, CI verde e roteiro Android aprovado no portal pela Issue #121; sem sobrescrever projetos. Issue #111 encerrada; handoff `HO-20261003-lunet-import-zip`.

Editor de código (§9)
- [x] Realce, números de linha, desfazer/refazer, indentação, autocompletar, definição, referências, dica, localizar/substituir e diagnósticos ao vivo.
- [x] Multi-cursor / multi-seleção (`MultiCursor`: próxima ocorrência, todas, cursor acima/abaixo, digitação replicada).
- [x] Code folding por regiões Roslyn.
- [x] Atalhos de teclado; teclado físico continua opcional no roteiro.
- [x] Minimap opcional.
- [x] Documentos grandes / virtualização: J1 pertenceu ao gate integral aprovado. Os tempos brutos não foram persistidos no formulário do portal e nenhuma falha bloqueante foi registrada; portanto não há evidência para substituir o editor atual apenas por antecipação.
- [x] Estrutura de texto eficiente: `PieceTable` permanece pronta/testada e deliberadamente não integrada enquanto medição/uso real não demonstrar necessidade; eventual ativação será mudança própria, não dívida escondida.

Roslyn (§9)
- [x] Renomear símbolo.
- [x] Formatação de código.
- [x] Quick fixes e code actions.
- [x] Compilação incremental (reaproveitar compilação entre Runs).
- [x] Inspeção de símbolos.

IDE mobile (§11)
- [x] Explorer (ver Fase 1).
- [x] Painéis: Preview, Inspector, Console, Problems, Search, Documentation e Explorer. Assets → Fase 5, Profiler → Fase 4 e Agent → Fase 8 foram transferidos formalmente.
- [x] Reorganizar, redimensionar, esconder painéis; salvar layouts persistentes.
- [x] Landscape prioritário, portrait utilizável; evitar seleção acidental nos controles.
- [x] Busca no projeto inteiro.
- [x] Console e Problems com logs exportáveis (§23).
- [x] Settings.

Inspector (§12)
- [x] Inspector padrão por reflexão; atributos Range, ReadOnly, Hidden, Multiline, Color, File, Asset, Group, Tooltip.
- [x] `Inspector<T>` customizado.
- [x] Inspecionar variáveis do jogo em execução (fluxo principal §29).

Documentação (§15)
- [x] Documentação offline gerada de XML docs, Markdown e metadata da API, dentro do app, como painel Documentation do workspace (§33).
- [x] Cada API com descrição, assinatura, parâmetros, retorno, exemplos, remarks, relacionadas e versão de introdução.
- [x] Integração com o editor: "Explain in Documentation".
- Documentação apontando para trechos dos jogos oficiais foi transferida para a Fase 11, onde os jogos passam a existir; não é pendência do gate da Fase 3.

Preview e hot reload (§10)
- [x] Classificação de mudanças: hot reload possível × restart required.
- [x] Fast Preview × Isolated Preview (ADR 0006).

Git, autosave e recovery (§22)
- [x] Autosave com journal e recuperação.
- [x] Git progressivo: status, diff, commit, history, branches, revert, push, pull. A integração Git local/real é automatizada; H7–H8 com GitHub permaneceram opcionais no roteiro de aparelho e não são requisito para o gate offline-first.

Auditoria
- [x] LUNET-303 — Auditoria de fechamento da Fase 3 registrada em `docs/audits/fase-3.md`; candidato/roteiro A–J/J1 em `docs/audits/fase-3-candidata.md`; validação humana aprovada no portal pela Issue #204. Issue #200; handoff `HO-20261004-lunet-f3-candidate`.

## Fase 4 — Framework Advanced (§7)

Gate: jogos 2D substanciais apenas com APIs oficiais.

Integração transversal autorizada pelo Ecosystem (não altera o gate de Framework Advanced):
- [x] **LUNET-401 — Adapter opcional da Host API do Ecosystem via Android Binder** (P5-4 / DEC-0037-A): integrado e validado. Pareamento, `text.inspect`, revogação/rotação e lifecycle/process-death foram aprovados no gate P5-4 pela Issue #225; Lunet permanece funcional sem provider. O estado idle `0 sessões IPC abertas agora` é esperado e não invalida o pareamento.

- [x] **LUNET-402 — Camada simples de colisão** (§7): consultas e separação SAT integradas pelo PR #291; Issue #288 encerrada, roteiro aprovado pelo proprietário no portal (Issue #303), registro no handoff `HO-20261008-lunet-collision`. Física completa permanece no item separado abaixo.
- [ ] Física completa (`PhysicsWorld`, `RigidBody2D`, `Collider2D`, `Fixture2D`, `Joint2D`, `Contact`, `Raycast`) sobre backend maduro (Box2D ou equivalente) sem expor tipos internos; ADR da escolha.
- [x] **LUNET-403 — `Camera2D`** (§7): posição, zoom, rotação e toque/mundo integrados pelo PR #295; Issue #293 encerrada, APK 1000126 publicado e roteiro aprovado pelo proprietário no portal (Issue #304), registro no handoff `HO-20261008-lunet-camera`.
- [~] **LUNET-404 — Animação de sprites e tweening** (§7, §23): clips, playback e transições sem alocação por quadro; Issue #298. APIs/guias integrados pelo PR #316 e publicados no APK 1000141; validação Android pendente. Handoff `HO-20261008-lunet-animation-api`. Modelo opcional na revisão consolidada #317 (zona ProjectStore, autorização do proprietário).
- [~] **LUNET-405 — Partículas** (§7, §23): pool fixo, burst/emissão contínua, vida, velocidade, gravidade, tamanho e cor; Issue #306. APIs/guias integrados pelo PR #316 e publicados no APK 1000141; roteiro Android pendente. Handoff `HO-20261008-lunet-particles-api`. Modelo opcional na revisão consolidada #317 (zona ProjectStore, autorização do proprietário).
- [~] **LUNET-415 — Tilemaps em camadas, colisão e A*** (§7, §23): Issue #355; integrado pelo PR #358 com JSON v1, viewport, colisão AABB, camadas e A*. CI/consistency e compilação do APK aprovados; DEVICE pendente.
- [~] **LUNET-416 — Camera2D viewport de mundo para tilemap culling** (§7, §23): Issue #363; AABB da vista conservadora com zoom/rotação, testes e guia offline em revisão; CI/DEVICE pendentes. Não conclui Tile Studio.
- [ ] Tilemaps completos (conexão com formato definitivo do Tile Studio, colisão e validação em aparelho) — Fase 4 continua aberta.
- [ ] Fontes personalizadas (bitmap e TrueType).
- [~] **LUNET-408 — Fontes bitmap personalizadas** (§7, §23): imagem/atlas, métricas proporcionais e fallback Unicode em SpriteFont/DrawString; Issue #327; Integrado pelo PR #328 e publicado no APK 1000155; roteiro Android pendente. TrueType permanece pendente; não conclui o item completo de fontes.
- [ ] UI (layout, âncoras, nine-slice, temas).
- [~] **LUNET-417 — Temas imutáveis para UI** (§7, §23): Issue #365; paletas Dark/Light/HighContrast para TouchButton, TouchSlider e painéis/texto, testes de desenho e guia offline em revisão; CI/merge/DEVICE pendentes. Não conclui árvore de UI nem editor visual.
- [~] **LUNET-410 — Botões de UI por toque e estilos de cores** (§7, §23): captura por dedo, clique ao soltar, cancelamento/disable e desenho pelo SpriteBatch; Issue #331. API e quinta página executável no Laboratório em revisão; integração/release e roteiro Android pendentes, sem árvore de UI, foco ou temas completos.

- [~] **LUNET-409 — Layout de UI por âncoras e margens** (§7, §23): LayoutRect/Fixed/Stretch e composição aninhada sem alocação; Issue #329; Integrado pelo PR #330 e publicado no APK 1000164; roteiro Android pendente. Incremento de UI; temas e controles completos permanecem pendentes.

- [~] **LUNET-411 — Slider por toque e demonstração no Laboratório** (§7, §23): faixa/valor/passo, captura por dedo, cancelamento/disable e desenho; Issue #335. Implementação e suíte combinada 438/438 verificadas; integrado pelo PR #336 e publicado no APK 1000160; validação Android pendente. Demonstração executável na página 4 do Laboratório.
- [~] **LUNET-407 — Nine-slice** (§7, §23): painéis/botões com cantos preservados, recorte de atlas e bordas escaláveis; Issue #319. Incremento de UI, sem concluir layout/âncoras/temas; Integrado pelo PR #324 e publicado no APK 1000150; roteiro Android pendente.
- [~] **LUNET-406 — Pathfinding A*** (§7): grade com obstáculos/custos, quatro/oito vizinhos e buffers reutilizáveis; Issue #311. Integrado pelo PR #313 e publicado no APK 1000133; guia offline e CI verdes, aparelho pendente. Handoff `HO-20261008-lunet-pathfinding`.
- [~] **LUNET-412 — Scene2D opcional e API entidade/componente** (§7, §23): Issue #338; ownership exclusivo, ordem de inserção e percursos sem alocação. Demonstração pronta na página 6 do Laboratório; integrado pelo PR #339 e publicado no APK 1000169, suíte 475/475 e CI verdes; DEVICE pendente. Sem obrigar ECS.
- [~] **LUNET-413 — Debug APIs e helpers** (§7, §23): polígonos, raios, eixos de transformação e grades limitadas; Issue #340. Demonstração pronta na página 7 do Laboratório; build sem warnings/errors, 485/485 testes; integração/release e DEVICE pendentes.
- [~] **LUNET-425 — Diagnóstico de engasgos no Profiler** (§7, §23): Issue #387; P50, P95, pior frame e contagem de hitches relativos à cadência da janela, com coleta opt-in sem GC e exibição no Preview Android. CI/integração/APK/DEVICE pendentes. Indica picos de frame pacing; não mede causa de travamento, tempo GPU, swap ou underruns de áudio.
- [ ] Profiler básico do framework (FPS, frame time, update/render time, draw calls, triângulos, memória, GC, audio underruns) (§23), com o painel Profiler no workspace (movido da Fase 3).
- [~] **LUNET-419 — Profiler de Preview Android ao vivo** (§7, §23): Issue #369, janela circular opt-in testável, painel com FPS, frame/CPU Tick, chamadas GL, triângulos, alocações na thread de renderização, heap managed e contagens GC. Update/Draw separados, GPU time, audio underruns e painel persistente do workspace são pendências distintas; CI/DEVICE pendentes.
- [x] **LUNET-418 — Laboratório 2.0, central de validação offline** (§7, §23): Issue #367 encerrada após aprovação humana explícita em 2026-10-08; PR #368 integrado, APK `lunet2d-v0.0.1-dev.1000193` publicado, 521/521 Lunet.Tests + 9/9 inspeção textual aprovados e **12 módulos aceitos no aparelho pelo proprietário** (registro Issue #367, comentário 6071609183). Nenhum projeto existente alterado. Testes do Lab não concluem física completa, TrueType, Tile Studio nem Profiler 419.
- [~] **LUNET-420 — Profiler CPU por fases (Update/Draw)** (§7, §23): Issue #371, complemento do #343 e #369; medição opt-in no GameHost, métricas de passos fixos, CPU Update acumulada e CPU Draw por chamada, agregadas com indicação de amostras válidas no Preview. CI, integração e DEVICE pendentes. GPU time, audio underruns, memória GPU e painel permanente continuam pendentes.
- [~] **LUNET-421 — Seguimento suave e limites do mundo na Camera2D** (§7, §23): Issue #373, Follow amortecido por segundo e ClampToWorld com zoom/rotação; teste completo na página Câmera do Laboratório. CI, release e DEVICE pendentes, sem mudanças de projetos existentes.
- [~] **LUNET-422 — Viewport Fit/Fill e mapeamento de toque** (§7, §23): Issue #375, Fill opt-in sem distorção de sprites para tela sem letterbox; Fit default retrocompatível, PixelPerfect, SafeArea, scissor, config e opção no Laboratório com testes. CI/integração/APK/DEVICE pendentes; Fill pode recortar HUD e não promete mostrar toda cena.
- [~] **LUNET-423 — Controles virtuais precisos e captura multitouch** (§7, §23): Issue #377; `VirtualStick` com curva/sensibilidade/zona morta contínua opt-in, `TouchId`, exclusão de dedo, captura somente Pressed opt-in e Cancel; `VirtualButton` com mesma reserva de dedo e cancelamento. Testes, documentação e CI/Android pendentes; DEVICE necessário para ergonomia e regressão do toque.
- [~] **LUNET-424 — Buffer de transições touch no Preview Android** (§7, §23): Issue #381; manter Pressed/Released até Update/Step, sem List/ToArray por MotionEvent; testes portáteis e documentação incluídos. CI/APK/DEVICE pendentes. Não altera InputState público nem códigos salvos.
- [ ] Auditoria de fechamento da Fase 4 registrada em `docs/audits/`.

## Fase 5 — Lunet Studio (§13)

Gate: a maior parte dos assets/dados necessários pode ser produzida dentro do Lunet, com fonte inspecionável e editável fora do Studio (§13). As ferramentas usam o `Lunet.Editor.SDK` (criado na Fase 6 ou antes, ver ADR).

- [ ] Painel Assets no workspace: lista os arquivos de `Content/` com prévia e uso (movido da Fase 3).
- [ ] Sprite Studio: pixel art, layers, frames, timeline, onion skin, paletas, lápis, borracha, balde, conta-gotas, linhas, formas, seleção, laço, transformar, flip, rotate, simetria, grade, zoom, spritesheet, preview de animação, importar/exportar PNG, transparência, toque e stylus.
- [ ] Tile Studio: tilesets, edição de mapas, layers, camada de colisão, propriedades, autotiling, brushes, preview.
- [ ] Animation Studio: clips, timeline, frames, eventos, playback, animação de sprites, estados.
- [ ] Atlas Studio: criação de atlas, packing, preview, metadata; inclui o pipeline de conteúdo com cache de assets processados em disco e invalidação (movido da Fase 2: só faz sentido com uma etapa de processamento real).
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

- [ ] Painel Agent no workspace (movido da Fase 3).
- [ ] Adaptar Model Gateway, Provider Adapters e Capability Discovery do Agent Runtime compartilhado do Ecosystem; nenhum sistema de providers paralelo no Lunet.
- [ ] Tool API interna própria (mesmas operações usadas por humano e plugin) e depois bridge MCP.
- [ ] Ferramentas tipadas: Project, Code, Compiler, Runtime, Testing, Assets, Documentation, Build, Versioning/checkpoints.
- [ ] Context Engine por relevância (árvore, símbolos, referências, diagnósticos, arquivos relacionados, logs, histórico, docs, diffs, memória do projeto).
- [ ] Integrar Agent Runtime compartilhado do Ecosystem com capabilities Lunet de código, build, execução, inspeção e testes; sem loop, memória ou orquestração próprios.
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

- [ ] Opção "Tutorial" na criação de projeto (§33), abrindo os três jogos oficiais.
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
- [ ] Snapshot do projeto inteiro para recuperação (§22).
- [ ] Recriar texturas, alvos de desenho e shaders quando o contexto GL é perdido, sem reiniciar o jogo.
- [ ] `Localization.UseDeviceLanguage` conferido em aparelhos com idiomas diferentes.
- [ ] Auditoria de fechamento da Fase 13 registrada em `docs/audits/`.

## Fase 14 — Beta

- [ ] Documentação completa e tutoriais completos.
- [ ] Revisão de API pública (nada vazando detalhes internos).
- [ ] Revisão de desempenho (§23), latência de áudio (avaliar Oboe/AAudio se a medição exigir) e testes de migração de projetos.
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
- [x] Ao terminar cada fase, entregar roteiro detalhado de validação da release ao usuário, registrar resultados na auditoria e aguardar aceite explícito antes de marcá-la ✅ ou iniciar a próxima fase (§24, §28, §36; pedido do usuário em 2026-09-29). Diretriz documentada; aplica-se às próximas fases, sem alterar retroativamente as auditorias 0–2.
- [x] Continuidade entre agentes: migrar `CLAUDE.md` para `AGENTS.md`, criar `AgentsChat.md` e retomar o PR #20 corrigindo o build Android (§24, §30, §36; pedido do usuário em 2026-09-29).
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

## Evolução code-first — backlog classificado (§37)

Registro de escopo futuro; não autoriza avanço além do gate atual.

| Capacidade | Destino | Condição / evidência futura |
| --- | --- | --- |
| Debugger: breakpoints, watches, call stack, step in/out/over | Evolução da IDE após gate da Fase 3 | ADR de protocolo/backend; sessão real no Android |
| Frame debugger, grafo de dependências de assets, profiler avançado | Evolução das Fases 4/5 | Métricas reais e fontes de assets documentadas |
| Package manifest, SemVer, dependencies, lockfile, restore, pacotes locais | Extensão futura do planejamento de dependências, antes da 1.0 | ADR de formato/restore; offline, resolução determinística; registry posterior |
| Playtest Agent | Evolução da Fase 8 | Runtime compartilhado; compilar, executar, observar, reproduzir e verificar correções |
| Release Cockpit: id, versão, ícones, orientação, permissões, assinatura, APK/AAB, metadados, screenshots, notas | Evolução das Fases 9/10/12 | Mesmo projeto, assinatura/empacotamento verificados |
| Game services: achievements, leaderboards, saves, contas, analytics, crashes, remote config, monetização | Pós-1.0 | Adapters substituíveis e opcionais; sem fornecedor obrigatório |
| Replay determinístico, time-travel debugging, visual diff, Game Doctor, test matrix | Pós-1.0 | Captura/reprodução verificáveis; sem promessa de determinismo universal |
| Remote Device Preview local | Investigação pós-1.0 | Pareamento autorizado, transporte e segurança por ADR; sem implementação agora |

Scene Studio e shader tooling são expansões futuras do Lunet Studio, sem criar cenas obrigatórias.
