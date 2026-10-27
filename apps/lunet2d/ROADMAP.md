# Roadmap

Fonte de verdade do que existe e do que falta. A especificação completa está em [`docs/SPEC.md`](docs/SPEC.md); as seções citadas como **§N** referem-se a ela. A rotina de trabalho está em [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

## Regras deste arquivo

- Uma caixa `[x]` só vale quando **implementação, testes, documentação e integração** estão completos (§24) **e** o comportamento visível ao usuário foi validado no aparelho quando depende de Android.
- Trabalho feito mas ainda não validado em aparelho fica `[ ]` com a marca **(feito, sem validação em aparelho)**.
- Trabalho parcial fica `[ ]` com o que falta escrito ao lado. Nunca marque por aproximação (§30, §31).
- Não se avança para a fase seguinte antes do gate da fase atual **e** da auditoria da fase (ver `docs/DEVELOPMENT.md`). Exceção: itens transversais listados na seção "Transversal".
- Estado do resumo por fase: `bash tools/roadmap-status.sh`.

Legenda de estado das fases: ✅ concluída (gate + auditoria) · 🟡 em andamento · ⬜ não iniciada.

## Prioridade de execução — ordem do proprietário em 25/10/2026

O proprietário solicitou explicitamente framework, editores, UI e build APK utilizáveis,
aceitando limitações pré-beta e rejeitando refinamento repetido de recursos já funcionais.
O escopo das fases permanece integral; a execução deixa de ser uma fila serial entre
F4, F5 e F9. Esta ordem é a exceção explícita prevista em DEVELOPMENT §1.4.
Priorizar o fluxo **criar projeto → produzir assets → animar → executar → gerar APK**.
Pendências de validação continuam abertas, mas não impedem ferramentas independentes.
A conexão UI do Tile Studio pertence à entrega F5; não bloqueia seu próprio início.
Não ampliar instrumentação de áudio enquanto esses fluxos estiverem ausentes.

- [~] **LUNET-440 — Sprite e animação utilizáveis (Issue #775):** tela Android com desenho, frames, playback, persistência fonte e exportação de spritesheet. Primeiro incremento; não equivale aos Studios completos.
- [~] **LUNET-442 — Sprite Studio ferramentas de desenho (Issue #779):** linha, retângulo, preenchimento, espelhamento vertical, cor ARGB, grade e zoom/pan por toque; CI .NET/Android aprovado, DEVICE pendente. Sem declarar layers, seleção e estados concluídos.
- [ ] **LUNET-443 — Sprite/assets disponíveis no código (Issue #783, feito, sem validação em aparelho):** Preview rápido e isolado devem aceitar `Content.LoadTexture("Assets/arquivo.png")`; salvar a animação no Sprite Studio deve persistir o `.sprite.json` (frames/FPS) e atualizar automaticamente a spritesheet `.png` em `Assets/`, preservando ordem, transparência e uso do SpriteAnimator, mantendo `Content/` prioritário e sem migrar arquivos. Testes portáteis e CI obrigatórios; DEVICE pendente.
- [ ] **LUNET-444 — Tilemap 100% criado dentro do Lunet (Issue #785, feito, sem validação em aparelho):** criar tilemap sem PNG importado, gerar atlas editável `.sprite.json` + `.png` em `Content/Textures`, abrir edição de pixels via Tile Studio, recarregar textura, preservar contrato TileMap JSON v1. CI/DEVICE pendentes.
- [ ] **LUNET-445 — F9 prova técnica APK do jogo (Issue #787, prévia CI):** template Android sem IDE com Game.dll e Content/Assets empacotados, compilador GameCode parametrizado por `GameSourceRoot` para jogos reais; compilação ARM64, checagem de assinatura, ID distinto e artifact no workflow `lunet2d-game-apk-smoke`. Agora inclui UI Arquivo → Gerar APK, snapshot ZIP privado no GitHub, build Actions, download de artifact e exportação/instalação SAF. CI e DEVICE do fluxo completo pendentes; offline, assinatura persistente e AAB ainda faltam.
- [ ] APK do **jogo** (fluxo completo F9 ainda aberto): template standalone e fallback cloud via LUNET-445/#787, **packager local offline integrado pelo PR #804**, correção de assinatura #814, alinhamento #817 e identidades/orientação #824. O usuário já conseguiu abrir dois jogos na versão anterior; falta validar no Android físico que os APKs da release #1000428 coexistem sem desinstalação, com orientação correta, saves e atualização. Não confundir packager local com Android SDK integral.
- [~] **LUNET-441 — Primeiro Tile Studio (Issue #777):** editor Android por toque, tileset PNG, camadas, colisão, desfazer/refazer e JSON v1 conectado ao runtime; CI/DEVICE pendentes. Autotiling, propriedades, brushes avançados e zoom continuam F5.
- [x] **LUNET-446 — Painel Assets (integrado PR #795, DEVICE aprovado na Issue #800):** workspace com acesso Assets, catálogo de `Content/` e `Assets/`, busca/filtros, preview de PNG/tileset, reprodução WAV/OGG/MP3 via MediaPlayer, código de uso copiável e atalhos Sprite/Tile Studios/JSON. Não cria segundo índice persistente nem modifica binários. Testes de catálogo, CI e validação DEVICE registrados em `HO-20261026-lunet-assets-panel`; isso não encerra F5.\n- [x] **LUNET-447 — Primeiro Audio Studio WAV PCM (PR #801 integrado; DEVICE aprovado na Issue #806):** editor nativo de waveform, recorte, volume, marcadores SMPL de loop, pré-escuta, cópia WAV em Content/Audio sem substituir o original, importação SAF e criação interna de efeitos sonoros. Edição inicial limitada a PCM WAV 8/16 bits mono/estéreo; MP3/OGG seguem reprodução apenas; sem DAW, filtros avançados ou edição multitrack. Evidências em `HO-20261026-lunet-audio-studio`; não conclui o Audio Studio integral de F5.
- [ ] Em seguida: Assets/Tile/Audio/UI Studios e demais editores de F5, com melhoria contínua da navegação. Física, áudio, A* e todo o restante do roadmap permanecem requisitos.

## Estado atual

| Fase | Estado |
| --- | --- |
| 0 Foundation | ✅ concluída (auditoria em `docs/audits/fase-0.md`) |
| 1 Vertical Slice | ✅ concluída (auditoria em `docs/audits/fase-1.md`) |
| 2 Framework Core | ✅ concluída e aprovada (auditoria em `docs/audits/fase-2.md`) |
| 3 IDE | ✅ concluída e aprovada (LUNET-303 / Issue #204; auditoria em `docs/audits/fase-3.md`) |
| 4 Framework Advanced | 🟡 em andamento |
| 5 Lunet Studio | 🟡 em andamento por ordem explícita do proprietário |
| 6–15 | ⬜ |

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
- [~] **LUNET-435 — Física Box2D gerenciada (incremento do item acima, Issue #477):** world/step, corpo, círculo/caixa, material/sensor, impulso, junta de distância, eventos de início, raycast closest; ADR 0009 e testes. PR #478 integrado e APK 1000309 publicado; expansão integrada pelo PR #480. Demo Box2D integrada pelo PR #484 e aprovada no aparelho pela Issue #488; carga 1/64 corpos integrada pelo PR #499 e aprovada no aparelho pela Issue #503 (sem números brutos de performance anexados); cenários de juntas/filtros/contatos avançados integrados pelo PR #630 e DEVICE aprovado pela Issue #638 (22/10/2026); auditoria técnica em `docs/audits/fase-4.md`; não concluir gate da Fase 4.
- [x] **LUNET-403 — `Camera2D`** (§7): posição, zoom, rotação e toque/mundo integrados pelo PR #295; Issue #293 encerrada, APK 1000126 publicado e roteiro aprovado pelo proprietário no portal (Issue #304), registro no handoff `HO-20261008-lunet-camera`.
- [x] **LUNET-404 — Animação de sprites e tweening** (§7, §23): Issue #298; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #458, #475. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-405 — Partículas** (§7, §23): Issue #306; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #449. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-415 — Tilemaps em camadas, colisão e A** (§7, §23): Issue #355; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #445. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-416 — Camera2D viewport de mundo para tilemap culling** (§7, §23): Issue #363; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #455. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [ ] Tilemaps completos: runtime v1/camadas/colisão/culling/A* aprovados (#445/#455); round-trip `ToJson` e contrato documentado (#495). **Ponte de edição C# v1 integrada:** operações imutáveis de criação/células/camadas/ordem com validação e testes (sem reescrever projetos), compatibilidade aprovada pela Issue #515; isso não comprova uma UI Studio ainda inexistente. Conexão UI efetiva permanece ausente e é entregue em Fase 5, por repriorização explícita de 25/10; não bloqueia o início do Studio.
- [x] Fontes personalizadas (bitmap e TrueType): APIs bitmap/TTF integradas, suíte/API/guia e DEVICE aprovados nas Issues #457, #442 e #431; limitações de shaping descritas no guia.
- [x] **LUNET-430 — Fontes TrueType offline** (§7, §23): Issue #414; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #431. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-426 — Correção da fonte bitmap embutida** (§7, §23): Issue #390; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #442. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-408 — Fontes bitmap personalizadas** (§7, §23): Issue #327; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #457. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [ ] UI (layout, âncoras, nine-slice, temas).
- [x] **LUNET-433 — Grade responsiva para inventários e menus** (§7, §23): Issue #427; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #474. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-432 — Toque seguro após relayout mobile** (§7, §23): Issue #422; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #432. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-431 — UI responsiva por pilhas de tamanho fixo/flexível** (§7, §23): Issue #417; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #434. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-429 — Lista de seleção mobile com renderização virtualizada** (§7, §23): Issue #410; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #433. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-427 — Scroll vertical para UI mobile** (§7, §23): Issue #393; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #438. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-428 — Alternância on/off por toque** (§7, §23): Issue #399; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #437. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-417 — Temas imutáveis para UI** (§7, §23): Issue #365; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #443. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-410 — Botões de UI por toque e estilos de cores** (§7, §23): Issue #331; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #456. Evidências por código/testes/guia em `docs/audits/fase-4.md`.

- [x] **LUNET-409 — Layout de UI por âncoras e margens** (§7, §23): Issue #329; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #447. Evidências por código/testes/guia em `docs/audits/fase-4.md`.

- [x] **LUNET-411 — Slider por toque e demonstração no Laboratório** (§7, §23): Issue #335; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #444. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [~] **LUNET-407 — Nine-slice** (§7, §23): painéis/botões com cantos preservados, recorte de atlas e bordas escaláveis; Issue #319. Incremento de UI, sem concluir layout/âncoras/temas; Integrado pelo PR #324 e publicado no APK 1000150; roteiro Android pendente.
- [x] **LUNET-406 — Pathfinding A** (§7, §23): Issue #311; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #448. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-412 — Scene2D opcional e API entidade/componente** (§7, §23): Issue #338; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #446. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-413 — Debug APIs e helpers** (§7, §23): Issue #340; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #452. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-425 — Diagnóstico de engasgos no Profiler** (§7, §23): Issue #387; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #440. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [~] **LUNET-438 — Música WAV PCM em streaming e underruns reais** (§7/§23, Issue #764): implementado no backend Android existente; PCM mono/estéreo 8/16 bits via AudioTrack, mantendo demais formatos no MediaPlayer. Medição cobre apenas essa saída; SoundPool e formatos comprimidos continuam sem contador. Testes portáteis aprovados; CI/APK/DEVICE pendentes, sem fechar Profiler ou Fase 4.
- [ ] Profiler básico do framework (FPS, frame time, update/render time, draw calls, triângulos, memória, GC, audio underruns) (§23), com o painel Profiler no workspace (movido da Fase 3). Cobertura parcial em LUNET-438: AudioTrack mede música WAV PCM; SoundPool/MediaPlayer não expõem contadores de underruns; não substituir por falhas de Play, estimativas ou zero. Aba workspace integrada pelo PR #493.
- [~] **LUNET-414 — painel Profiler no workspace (continuação, Issue #492):** última captura opt-in do Preview rápido/isolado preservada após Stop, relatório compartilhável e aba redimensionável com layout salvo; reutiliza métricas reais já existentes. Build .NET 10 e 661/661 testes locais, 22/22 checks; PR #493 integrado e DEVICE aprovado pela Issue #502; áudio underruns continua sem medição.
- [~] **LUNET-414 — armazenamento de texturas no Profiler** (Issue #343): contador real de recursos de textura RGBA8 conhecidos, incluindo render targets uma vez, pico, tamanho e lifetime; exibido no Preview como estimativa, sem fingir medir VRAM total, tempo GPU ou underruns. Texturas integradas pelo PR #485 e DEVICE aprovado pela Issue #487; aba workspace integrada pelo PR #493, sem medição de underruns.
- [x] **LUNET-419 — Profiler de Preview Android ao vivo** (§7, §23): Issue #369; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #450. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-418 — Laboratório 2.0, central de validação offline** (§7, §23): Issue #367 encerrada após aprovação humana explícita em 2026-10-08; PR #368 integrado, APK `lunet2d-v0.0.1-dev.1000193` publicado, 521/521 Lunet.Tests + 9/9 inspeção textual aprovados e **12 módulos aceitos no aparelho pelo proprietário** (registro Issue #367, comentário 6071609183). Nenhum projeto existente alterado. Testes do Lab não concluem física completa, TrueType, Tile Studio nem Profiler 419.
- [x] **LUNET-420 — Profiler CPU por fases (Update/Draw)** (§7, §23): Issue #371; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #453. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-421 — Seguimento suave e limites do mundo na Camera2D** (§7, §23): Issue #373; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #454. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-422 — Viewport Fit/Fill e mapeamento de toque** (§7, §23): Issue #375; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #436. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-423 — Controles virtuais precisos e captura multitouch** (§7, §23): Issue #377; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #435. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [x] **LUNET-424 — Buffer de transições touch no Preview Android** (§7, §23): Issue #381; implementação integrada e coberta pela suíte; DEVICE aprovado pelo proprietário nas Issues #439. Evidências por código/testes/guia em `docs/audits/fase-4.md`.
- [~] **LUNET-436 — Auditoria de fechamento da Fase 4 registrada em `docs/audits/fase-4.md`** (§7, §23, §28, Issue #495): matriz código/testes/documentação/DEVICE, reconciliação das aprovações existentes, round-trip do formato TileMap v1, descarte do atlas da fonte embutida e jogo reproduzível de validação conjunta (`gate-framework-advanced`). Round-trip/jogo conjunto aprovados pela Issue #500; isso não resolve requisitos ausentes nem aprova o gate integral da fase.

## Fase 5 — Lunet Studio (§13)

Gate: a maior parte dos assets/dados necessários pode ser produzida dentro do Lunet, com fonte inspecionável e editável fora do Studio (§13). As ferramentas usam o `Lunet.Editor.SDK` (criado na Fase 6 ou antes, ver ADR).

- [ ] Painel Assets no workspace: lista os arquivos de `Content/` com prévia e uso (movido da Fase 3).
- [ ] Sprite Studio: pixel art, layers, frames, timeline, onion skin, paletas, lápis, borracha, balde, conta-gotas, linhas, formas, seleção, laço, transformar, flip, rotate, simetria, grade, zoom, spritesheet, preview de animação, importar/exportar PNG, transparência, toque e stylus. LUNET-440 integrado parcialmente; LUNET-442/#779 adiciona linha, retângulos, flip vertical, cor ARGB, zoom/pan e grade, ainda sem layers, seleção/laço, rotação, simetria, timeline com eventos ou validação em DEVICE.
- [ ] Tile Studio: tilesets, edição de mapas, layers, camada de colisão, propriedades, autotiling, brushes, preview. **LUNET-441 / #777 em revisão:** edição por toque, escolha visual de tileset, pintura/borracha, camadas, visibilidade/colisão e desfazer/refazer usando TileMap JSON v1; ainda faltam zoom/pan, brushes múltiplos, autotiling, propriedades, preview do jogo e validação Android.
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

- [ ] Correção de múltiplos jogos e orientação (PR #824, feito, validação Android físico pendente): novos `PackageId` únicos mesmo com slugs colidentes; importar cópias em uma mesma biblioteca separa identidade sem alterar restauração sem conflito; legados com colisão solicitam autorização antes de bifurcar; novo projeto e arquivo de projeto configuram portrait/landscape; `AndroidManifest.xml` binário recebe a orientação de cada projeto antes da assinatura. CI verifica dois APKs instaláveis no mesmo emulador Android 13 e AXML; o proprietário valida ambos rodando simultaneamente no telefone.

- [ ] Falha de instalação do APK gerado no Lunet: `resources.arsc` STORED com alinhamento obrigatório em 4 bytes; teste unitário de offsets, `zipalign -c`, `apksigner`, `aapt`, `adb install` no Android 13 emulado; repetir no Bosque Lumi no celular real (DEVICE pendente). PR #817. Não confundir assinatura válida com pacote instalável.

- [ ] LUNET-448 / regressão na geração local: corrigir conversão `IKey` → `IPrivateKey` usando JavaCast JNI, checar assinatura RSA com certificado do jogo, fornecer etapa/stack trace no erro; CI deve verificar APK e DEVICE deve gerar/instalar/atualizar Bosque Lumi em modo offline. PR #814; não marcar concluído só com testes de fonte ou CI.

Gate obrigatório: desligar internet, criar projeto, programar, buildar APK, instalar e jogar. Se etapa local for inviável, implementar fallback via GitHub build e **registrar ADR**; não fingir.

- [ ] Experimento inicial de viabilidade: Game.dll + Content + runtime template → APK (AAPT2, alinhamento, assinatura, arm64) — smoke #787, packager offline #804 e correções #814/#817/#824 já integrados; execução de jogos em DEVICE relatada pelo proprietário, porém a aceitação da revisão com dois pacotes coexistentes e orientação ainda depende de teste físico. ADR 0010 documenta o fallback cloud histórico e precisa registrar sua superação parcial (§32 risco 4).
- [ ] `runtime-template/` e `Lunet.Build`.
- [ ] Empacotamento, recursos, AAPT2, alinhamento (zipalign) e assinatura.
- [ ] Assinatura de desenvolvimento automática; assinatura de release com chave do usuário (gerar, importar, proteger, verificar, backup, aviso de perda). Nunca usar chave do Lunet nos jogos.
- [ ] Build & Install e exportar APK: fluxo GitHub opt-in pela interface e exportação do APK implementados no PR #788; assinatura de debug efêmera, offline, AAB e validação DEVICE ainda pendentes.
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
- [x] **LUNET-449 — Inventário de estado real (Issue #831):** auditoria de 19 frentes em `docs/audits/inventario-estado-2026-10-27.md`; PR #832 integrado à `main` (merge `db537b1`) após CI do Lunet e consistency verdes. Auditado o estado documental, sem afirmar DEVICE desta tarefa nem fechar gates de F4/F5/F9.
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
4. Estratégia de geração de APK do jogo — **viabilidade comprovada com execução de jogos reais no aparelho**, compilação/empacotamento local em #804, correções #814/#817/#824; gate integral F9 e DEVICE específico da correção de coexistência/orientação continuam abertos.

## Aceitação final (§29)

- [ ] Principal: instalar → offline → criar projeto → C# → autocompletar → docs offline → criar/editar sprite → Run → jogar no Preview → inspecionar variáveis → corrigir → áudio → física → gerar APK → instalar o jogo → abrir → jogar → compartilhar.
- [ ] Agent.
- [ ] Plugin.
- [ ] Multiplayer.

## Evolução code-first — backlog classificado (§37)

Registro de escopo futuro; não autoriza avanço além do gate atual.

| Capacidade | Destino | Condição / evidência futura |
| --- | --- | --- |
| LUNET-437 — Editor visual modular: Aseprite + Photoshop + Illustrator + LPC; desenho manual e montagem por peças criadas/reutilizadas pelo usuário | Evolução futura do Lunet Studio (SPEC §13), Issue #631; [visão registrada](docs/plans/visual-editor-vision.md) | Registro para não esquecer; sem implementação agora. Decompor requisitos e aceites antes de planejar; não altera fase/gate atual nem amplia automaticamente o gate da Fase 5. |
| Debugger: breakpoints, watches, call stack, step in/out/over | Evolução da IDE após gate da Fase 3 | ADR de protocolo/backend; sessão real no Android |
| Frame debugger, grafo de dependências de assets, profiler avançado | Evolução das Fases 4/5 | Métricas reais e fontes de assets documentadas |
| Package manifest, SemVer, dependencies, lockfile, restore, pacotes locais | Extensão futura do planejamento de dependências, antes da 1.0 | ADR de formato/restore; offline, resolução determinística; registry posterior |
| Playtest Agent | Evolução da Fase 8 | Runtime compartilhado; compilar, executar, observar, reproduzir e verificar correções |
| Release Cockpit: id, versão, ícones, orientação, permissões, assinatura, APK/AAB, metadados, screenshots, notas | Evolução das Fases 9/10/12 | Mesmo projeto, assinatura/empacotamento verificados |
| Game services: achievements, leaderboards, saves, contas, analytics, crashes, remote config, monetização | Pós-1.0 | Adapters substituíveis e opcionais; sem fornecedor obrigatório |
| Replay determinístico, time-travel debugging, visual diff, Game Doctor, test matrix | Pós-1.0 | Captura/reprodução verificáveis; sem promessa de determinismo universal |
| Remote Device Preview local | Investigação pós-1.0 | Pareamento autorizado, transporte e segurança por ADR; sem implementação agora |

Scene Studio e shader tooling são expansões futuras do Lunet Studio, sem criar cenas obrigatórias.
