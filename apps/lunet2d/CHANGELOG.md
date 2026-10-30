# Changelog

- Fixed / host standalone: entrega Game.OnPause na thread GL antes de suspendê-la, usando o mesmo PauseForBackground do PreviewHost. Regressão provada por smoke Android que falhou antes e passou após a correção; JSON privado e restore após force-stop conferidos.

- F4 / Correção de CI integrado do PR #856: criação e destruição de mundos Box2D passam por mutex global de ciclo de vida, como exige a documentação oficial do Box2D. Mantém simulação de mundos distintos paralela; teste de regressão com 256 ciclos concorrentes. O teste PhysicsWorldTests.DynamicBallFallsOntoGround havia falhado de forma intermitente no estado combinado; CI novo e DEVICE pendentes.

- F7 / LUNET-460 (em revisão, CI/DEVICE pendentes): primeiro corte em assembly independente `Lunet.Networking` para LAN confiável em C#: host e clientes, IDs atribuídos pelo host, mensagens limitadas, rejeição de estado enviado por cliente e fila assíncrona consumível pelo jogo. Jogo standalone recebe permissão INTERNET. Não é multiplayer realtime completo nem seguro para internet pública. Consulte ADR 0011 e docs/guides/networking-foundation.md.

- LUNET-451 / Issue #842 (em revisão): três projetos Lunet de referência versionados (portrait, landscape e full-stack com Sprite Studio fonte, tilemap/collision, áudio WAV, física, UI e save). Testes xUnit anteriores aos fixtures compilam/executam os jogos e validam catálogo, formatos e ZIP. Sem alteração no motor/assinatura; CI, APK e DEVICE pendentes.

- LUNET-450 / Issue #834 (em verificação): teste E2E de projeto criado no Lunet, código e assets editados, Preview headless, ZIP e empacotamento offline do mesmo jogo; workflow preparado para instalar, abrir e capturar logs/tela do jogo no Android 13. Não altera o empacotador de produção, as chaves do usuário nem substitui testes físicos da IDE. Conferir CI e roteiro DEVICE antes de declarar conclusão.

- Documentação / LUNET-449: inventário auditável de 27/10/2026 confronta código na main, testes, PRs, handoffs, aprovações DEVICE e releases; reconcilia o estado do painel Assets (#795/#800), Audio Studio (#801/#806) e geração local de APK (#804/#814/#817/#824) sem alterar executáveis ou concluir os gates F4/F5/F9.


- F9: corrigido conflito real `INSTALL_FAILED_CONFLICTING_PROVIDER` entre dois APKs gerados: o `mono.MonoRuntimeProvider` agora recebe `android:authorities` único por projeto, além do `PackageId`, preservando a inicialização .NET. CI valida os dois providers e instalação simultânea no Android 13.


- Pré-beta / F9: jogos criados ou importados com identidades Android independentes e estáveis, resolução explícita para colisões legadas, orientação Vertical/Horizontal persistente no projeto e aplicada ao `android:screenOrientation` binário antes de assinar o APK. Smoke CI instala dois jogos juntos e verifica manifestos; DEVICE físico ainda precisa validar.


- Standalone Smoke (artefato de CI): cada execução agora utiliza um package ID distinto, evitando conflitos de atualização entre APKs assinados por chaves debug efêmeras; o CI passa a verificar assinatura e alinhamento do APK de diagnóstico.

- Pré-beta / correção de instalação do APK do jogo: alinha em 4 bytes os dados dos arquivos ZIP armazenados, especialmente `resources.arsc`, para Android 11+; testes de regressão de offsets, `zipalign` no CI e instalação do pacote exportado em emulador Android 13. DEVICE físico ainda requer validação.

- Pré-beta / LUNET-448 (correção de regressão): conversão JNI verificada de chave RSA do Android Keystore na geração de APK offline do jogo (evita `InvalidCastException` do downcast gerenciado); verificação da assinatura antes do resultado e indicação/log da etapa de falha. Contratos de regressão portáteis e roteiro DEVICE preparados antes da correção; testes reais no aparelho ainda pendentes.

- Pré-beta / LUNET-447: primeiro Audio Studio nativo com síntese de seno/quadrada/triangular/ruído dentro do Lunet, importação de WAV PCM, waveform, recorte, ganho, marcadores SMPL de loop, reprodução e exportação de cópia WAV16 em Content/Audio sem sobrescrever o original. Integra Arte, menu Ferramentas e Assets. Testes portáteis; CI/APK/DEVICE pendentes. Não é DAW nem editor MP3/OGG.

- Pré-beta / LUNET-446: painel Assets no workspace com categorias, busca, preview de imagens/map tileset, escuta de áudio, acesso direto aos editores Sprite/Tile/JSON e exemplo de carregamento C#. Catálogo deriva de ListFiles sem índice paralelo; CI/DEVICE pendentes.

- Pré-beta / LUNET-445 (#787): runtime-template Android separado da IDE, compilador GameCode com `GameSourceRoot` e prova por workflow GitHub Actions que empacota Game.dll, Content e Assets num APK de jogo distinto; CI de prova e fluxo cloud integrado na IDE (Arquivo → Gerar APK), consentimento explícito, repositório privado próprio, upload ZIP, workflow, monitoramento, download do artifact e exportação/instalação no Android. Assinatura debug temporária; offline e DEVICE ainda pendentes.

- Pré-beta / LUNET-444 (#785): Tile Studio cria mapas e tilesets completos dentro do Lunet sem importação; gera PNG + JSON editável e integra editor de pixels acessível pelo próprio mapa. Suporte inicial de atlas 64/128px, tiles 16/32/64 e tamanhos de mapa 16/32/64. CI e DEVICE pendentes.

- Pré-beta / LUNET-443 (#783): Sprite Studio passa a gerar e atualizar a spritesheet PNG com todos os frames automaticamente ao salvar o `.sprite.json` (mantendo FPS/frames editáveis); botão de uso exibe SpriteSheet/SpriteAnimationClip/SpriteAnimator. Corrigido carregamento de `Assets/` no Preview rápido/isolado sem afetar `Content/`. DEVICE pendente.

- Pré-beta / LUNET-442 (#779): Sprite Studio ampliado com linha, retângulo contornado/preenchido, flip vertical, zoom e deslocamento, grade opcional e cor ARGB personalizada. Preserva o JSON v1 e frames existentes; CI/DEVICE pendentes.

- Pré-beta / LUNET-441 (#777): Tile Studio Android inicial com paleta visual de PNG, pintura/borracha por toque, camadas, visibilidade e colisão, undo/redo e mapas JSON v1 editáveis no projeto. Sem duplicar o TileMap, alterar o áudio ou prometer APK do jogo; DEVICE pendente.

- Pré-beta: primeiro Sprite/Animation Studio nativo (LUNET-440), desenho por toque, frames, playback, JSON editável e spritesheet PNG. Prioridade de execução revista por ordem do proprietário: editores/UI/build antes de refinamentos adicionais de áudio.

## 0.0.1 — em desenvolvimento

### Added

- LUNET-438: música WAV PCM mono/estéreo 8/16 bits em streaming no AudioTrack Android, mantendo loop, volume, fades e pausa/retomada do mixer existente. Profiler/relatório mostram o contador nativo de underruns apenas das faixas medidas; efeitos SoundPool e formatos comprimidos permanecem explicitamente não medidos. Sem novo formato de projeto ou API do jogo.

### Fixed

- LUNET-438: pausa do mixer enfileirada na thread GL antes de suspender o Preview, sem depender de um próximo quadro para interromper o áudio em segundo plano.

- LUNET-432 (em revisão): toques capturados não provocam clique fantasma em botões nem inércia antiga após mudar o layout; Bounds iguais não interrompem gestos, e sliders continuam seguindo o dedo como na API anterior.

- LUNET-426 (em revisão): corrigir CRLF, Unicode fora do BMP e validação de layout/escala na fonte bitmap embutida; medida e desenho coerentes, sem enviar coordenadas infinitas ao OpenGL.

- LUNET-424 (em revisão): Preview Android agora preserva o início e o fim de toques rápidos entre eventos de 90/120 Hz e os Updates fixos, mesmo quando o dedo move antes da atualização. Buffer interno sem List/ToArray por MotionEvent; evita falhas intermitentes de captura Pressed nos joysticks opt-in.

- SAT calcula separação correta quando um polígono contém outro; polígonos sem área não produzem empurrão inválido.
- Medições de alocação isoladas de outras coleções de testes, preservando a exigência de zero bytes no loop; falha concorrente reproduzida também na base anterior.
- Correções rápidas removem apenas a diretiva using, preservando código na mesma linha e comentários; ordenação preserva global using, CRLF e comentários, e recusa blocos ambíguos/condicionais.


### Added

- LUNET-435 / #629: cenários Juntas e Filtros/eventos dentro do módulo Colisões do Laboratório; pêndulo, trilho, impulso, colisão filtrada, raycast e contadores de entrada/saída de sensores e contatos, com reinício e troca segura de mundo. SAT e carga 1/64 preservados; validação Android específica ainda pendente.

- Fase 4 / Tilemaps completos (em revisão): ponte imutável `TileMap.Create/WithTile/WithLayerSettings/RenameLayer/AddLayer/RemoveLayer/MoveLayer` para autoria de mapas JSON v1. Preserva dados antigos, valida camadas/células/IDs e atualiza maior tile após remoção; o Tile Studio visual continua Fase 5.

- LUNET-435 (em revisão): cenário opcional de 64 corpos Box2D no módulo Colisões do Laboratório, comparável com o caso de 1 corpo pelo Profiler, reset seguro e testes de integração; desempenho no Android ainda precisa de medição real.

- LUNET-436 / Issue #495: jogo reproduzível de validação conjunta do gate, descarte seguro do atlas de `SpriteFont.CreateDefault` e exportação `TileMap.ToJson()` compatível com JSON v1, preservando camadas, visibilidade, colisão e Unicode; auditoria integral da Fase 4 e roteiro consolidado. Aprovações anteriores reconciliadas sem concluir o gate; underruns de áudio e demais bloqueios reais continuam explícitos.

- Fase 4 / LUNET-414: aba Profiler no workspace preserva a última captura medida no Preview rápido/isolado após Stop e permite compartilhar o relatório; coleta continua opt-in em ▥ Perf. Não altera projetos ou executa jogo em segundo plano.

- LUNET-414 (em revisão): métrica opt-in visível no Profiler do Preview para tamanho estimado RGBA8 de texturas vivas, pico e quantidade de objetos GL (inclui render targets sem double count), com testes. VRAM total, buffers, GPU time e áudio underruns continuam não medidos.

- LUNET-435 (Fase 4, Laboratório em revisão): tela Colisões alterna SAT original e simulação real Box2D (bola, chão, sensor e reset) no mesmo módulo 10/12, sem alterar projetos existentes.

- LUNET-435 (Fase 4, expansão em revisão): cápsulas/segmentos, juntas revoluta/prismática, rotação e força de corpos, filtro por categoria, remoção de fixtures, contatos de fim e eventos de sensores (início/fim), com testes e documentação gerada; não altera projetos existentes.

- LUNET-435 (em revisão): primeiro incremento de física completa com backend Box2D.NET em C# gerenciado, corpos, fixtures de círculo/caixa, gravidade, juntas de distância, contatos iniciados e raycast. Sem alterar jogos salvos; validação Android e itens avançados pendentes.

- LUNET-433 (em revisão): grade de interface 2D responsiva para menus e inventários, com cálculo de colunas por largura, altura total e consulta virtualizada O(1), hit-test e TouchGridView com seleção, rolagem e renderização de janela, compatível com TouchScrollArea; exemplo C# offline e sem alocação no layout.

- LUNET-430 (em revisão): font atlas TrueType gerado offline em C# (`TrueTypeFont.Bake` e `Content.LoadTrueTypeFont`), integração com SpriteBatch, métricas proporcionais, cache de fonte, testes e limites de memória.

- LUNET-431 (em revisão): layout responsivo de UI horizontal/vertical com flex, espaçamento, padding e alinhamento transversal; converte safe area em bounds para controles mobile, sem GC por quadro.

- LUNET-429 (em revisão): `TouchListView` para listas grandes de missão/inventário com scroll inercial, hit-test por linha, seleção sem clique acidental ao arrastar e desenho apenas das linhas visíveis.

- LUNET-428 (em revisão): TouchToggle para menus mobile de opções, com paletas on/off, captura por dedo, testes e guia offline executável.

- LUNET-427 (em revisão): TouchScrollArea para painéis verticais de inventário, missão e menus mobile, com inércia, limites, multitoque, indicador visual proporcional de scroll (GetThumbBounds) e clip por SpriteBatch; testes e guia offline.

- LUNET-425 (em revisão): mediana/P95, pior frame e engasgos por cadência observada no Profiler de Preview Android, com janela circular sem GC e cobertura para 60/90/120 Hz. Não confundir frame pacing com tempo GPU.

- LUNET-423 (em revisão): precisão dos controles virtuais, resposta exponencial/sensibilidade, zona morta contínua opcional, identificação/reserva do dedo multitouch e cancelamento; padrões antigos preservados.

- LUNET-422 (em revisão): escala Fit/Fill em GraphicsDevice e GameConfiguration para preencher a tela preservando proporção (Fill recorta excedentes); alternância testável no módulo Gráficos do Laboratório 2.0 e compatibilidade dos toques.

- LUNET-421 (em revisão): Camera2D.Follow estável entre taxas de atualização, ClampToWorld com zoom/rotação e opção LIMITES na página Câmera do Laboratório, com testes de qualidade e alocação.

- LUNET-420 (em revisão): instrumentação opt-in de tempos CPU Update/Draw e contagem de passos do GameHost, exibidas e agregadas no Profiler Android; não mede GPU e não altera projetos existentes.

- LUNET-419 (em revisão): botão Perf no Preview Android, com janela de métricas reais de FPS, CPU Tick, draw calls, triângulos, alocações gerenciadas, heap e GC. Dados coletados só quando ligado; não inventa tempo GPU, métricas de áudio ou custo separado de Update/Draw.

- LUNET-418 (em revisão): Laboratório 2.0 com índice, 12 módulos offline prontos para Run, navegação anterior/próxima e testes reais de animação, partículas, tilemap/A*, SAT, bitmap fonts, nine-slice e UiTheme oficial. Template separado do ProjectTemplates, sem alterar projetos existentes.

- LUNET-416 (em revisão): `Camera2D.GetWorldViewBounds` calcula a AABB visível sob zoom/rotação para recortar TileMap.Draw sem corte de cantos, com testes de precisão e alocação.

- LUNET-415 (integrado PR #358; DEVICE pendente): `TileMap` JSON v1 em camadas, tileset, colisão, viewport culling e integração com A* e colisão AABB de personagens, com guia offline e testes no host portátil. Tile Studio/DEVICE pendentes.

- Helpers `DebugDraw.Polygon`, `Ray`, `Axes` e `Grid`: geometria de depuração transformada, limites/validação antes de desenhar e zero alocação no loop; página 7 pronta no Laboratório (LUNET-413).

- `Lunet.Scenes.Scene2D`, `Entity2D` e `Component2D`: organização opcional de movimento/desenho, ownership exclusivo, percursos em ordem de inserção sem alocação e flags independentes; sexta página pronta para Run no Laboratório (LUNET-412).

- `Lunet.UI.TouchButton`/`TouchButtonStyle`: clique ao soltar com captura por ID, estados/cancelamento/disable e desenho de fundo/texto com cores configuráveis, sem alocação no loop; quinta página executável do Laboratório com clique, arraste, disable, pausa e reposicionamento por LayoutRect (LUNET-410).

- `Lunet.UI.LayoutRect`: âncoras, pivôs, margens Fixed/Stretch e composição de retângulos aninhados, sem alocação por cálculo; guia offline de UI com redimensionamento e toque alinhado (LUNET-409).

- `UiTheme`: paletas locais imutáveis Dark/Light/HighContrast aplicáveis a botões, sliders, fundo, painel e texto, sem estado global; teste de desenho e ausência de alocação por quadro (LUNET-417, em revisão).

- `TouchSlider` e `TouchSliderStyle`: arraste contínuo ou em passos, captura por dedo, cancelamento e estado desabilitado; demonstração executável na página 4 do Laboratório, sem copiar código (LUNET-411).

- `SpriteFont.FromBitmap` e `BitmapGlyph`: fontes bitmap personalizadas sobre textura/atlas, métricas proporcionais, deslocamento, Unicode/fallback e layout sem alocação no loop; guia offline de placar (LUNET-408).

- APIs `SpriteAnimationClip`/`SpriteAnimator`, `Tween` e `ParticleEmitter`/`ParticleSettings`: animação, transições e partículas code-first, sem alocação no loop. Guias offline completos para projetos Em branco.
- `NineSlice` e `SpriteBatch.Draw(panel, ...)`: painéis/botões redimensionáveis com cantos preservados, recorte de atlas, escala das bordas e compressão proporcional, sem alocação por desenho; guia offline interativo (LUNET-407).


- `Lunet.Pathfinding`: A* em grade com custos/obstáculos, quatro/oito vizinhos sem cortar cantos, buffers reutilizáveis sem alocação por busca e guia interativo offline (LUNET-406).

- `Camera2D`: posição, zoom, rotação, conversão mundo/vista e toque no mundo; lote com câmera opcional em `SpriteBatch`, HUD sem câmera, terceira página do modelo Laboratório e guia offline interativo (LUNET-403).
- Consultas `Geometry.Distance`, `ClosestPoint` e `Intersection` para círculos, pontos e AABBs, sem alocação; guia offline com demonstração de colisão no Preview (LUNET-402).
- Candidato exato e entrega do roteiro integral da Fase 3 no portal (LUNET-303); aprovações ZIP/C6 reconciliadas sem aprovar o gate inteiro.

- Identidade Framework / IDE / Studio code-first, um Product e um projeto, política Visual ↔ Source e backlog classificado (ADR 0007).

- Importar ZIP na tela de projetos: restaura projetos exportados pelo Lunet, aceita lunet.json na raiz, cria cópia numerada quando o nome já existe e rejeita ZIP inválido sem deixar projeto parcial.

- Ferramentas → "Medir o editor com arquivos grandes" (tempos no aparelho para decidir sobre virtualização) e auditoria técnica da Fase 3 com roteiro de teste.
- Preview isolado (opção em Configurações): o jogo roda em processo separado, e travas ou falta de memória não derrubam o IDE (ADR 0006).
- API do framework com exemplo em cada tipo, parâmetros e retornos descritos; testes garantem a cobertura e que os exemplos compilam.
- Recuperação de alterações não salvas com até cinco versões por arquivo; a tela de recuperação permite escolher a versão, e o menu Ferramentas oferece acesso manual enquanto houver um buffer pendente.
- `AgentsChat.md`: registro assíncrono de entregas, revisões, bloqueios e passagem de trabalho entre agentes.

- Git no aparelho (`Lunet.Git`, C# puro, ADR 0005): status, diff, commit, histórico, ramos, reverter, mesclar, buscar, pull, push (HTTPS com token) e clonar; painel em ⋯ → Ferramentas → Git.
- Inspector do jogo em execução (botão 🔍 no Preview): campos por reflexão, atributos `Range`, `ReadOnly`, `Hidden`, `Multiline`, `Color`, `File`, `Asset`, `Group`, `Tooltip`, `Inspect` e `Inspector<T>` customizado; edição ao vivo.
- Classificação de mudanças entre Runs (só corpos × reinício necessário) no status.
- Layout do workspace: painel embaixo ou à direita (automático por orientação), divisória arrastável, largura do Explorer, layouts salvos.
- IDE: formatar documento, renomear símbolo (recusa conflitos), correções rápidas (using faltando/sobrando, ";", "você quis dizer", ordenar usings), estrutura do arquivo, informações do símbolo, dobrar código, multi-cursor, minimapa, atalhos de teclado físico, comandos de linha (duplicar, apagar, mover, comentar, indentar), configurações e exportar logs.
- Compilação incremental: árvores de sintaxe reaproveitadas e resultado em cache quando nada mudou.
- `PieceTable` (estrutura de texto para documentos grandes) e menu ⋯ reorganizado em categorias.
- Busca no projeto inteiro (maiúsculas, palavra inteira, regex) e item "Documentação do símbolo" no menu; campo de busca da documentação legível.
- Documentação offline no app: painel com guias, busca e navegação por namespaces, tipos e membros (gerado de XML docs por `Lunet.Docs`); "Explicar na Documentação" na dica do símbolo; API do framework documentada.
- Lista de projetos: toque longo abre exportar ZIP e excluir projeto (com confirmação).
- `Sprite` (região, origem, cor, escala, rotação) e o contexto OpenGL preservado ao ir para segundo plano.
- Preview pede o modo de tela de maior taxa de atualização (90/120 Hz onde houver).
- Conteúdo: `Content.LoadJson<T>`, `Content.LoadAtlas` (`TextureAtlas` com regiões e pivô), `Content.UnloadTexture` e `Localization` (`Game.Localization`, idiomas em `Data/strings.<idioma>.json`, reserva e formatação).
- Entrada: `TouchCollection` (sem alocação) e `Pointer` unificado com bordas de pressionar/soltar.
- Gráficos: `BlendState`, `SamplerState`, `Viewport`, recorte por lote (`Begin(clip:)`), `RenderTarget2D`, `Shader`/`Material`, `DebugDraw`, `PixelPerfect`, `Density` e `SafeArea`; backend OpenGL ES com FBOs, blend, scissor e programas de shader.
- Modelo "Laboratório" com segunda página de gráficos (mistura, shader, amostragem, recorte, alvo de desenho, pixel perfect e área segura).
- Áudio: `AudioMixer` (`Audio`) com barramentos Master/Sfx/Music e próprios, fade de efeitos e de música, `Music` em streaming (`Content.LoadMusic`) e pausa automática em segundo plano; backend Android com MediaPlayer.
- Entrada: `Gamepad` (botões, sticks, gatilhos), giroscópio e `Haptics.Vibrate`; app em segundo plano pausa o jogo.
- Modelo de projeto "Laboratório", que testa música, volume, fade, vibração, acelerômetro, giroscópio, controle, pinça/giro, joystick e botão de tela.
- Framework: `GameServices`, `Dispatcher` (thread do jogo), `Timers`, `ObjectPool<T>`, `Transform2D`, `Ray2D`, `Geometry` (segmentos, polígonos, SAT), gestos DoubleTap/Pinch/Rotate e `VirtualStick`/`VirtualButton`.
- Explorer: painel lateral com a árvore do projeto (pastas recolhíveis, novo arquivo/pasta, renomear, excluir com confirmação); operações protegidas contra sair do projeto e contra apagar `lunet.json`/entrada.
- Recuperação após crash: buffers de trabalho em `.lunet/autosave` a cada pausa na digitação, apagados ao salvar; o app oferece recuperar ao reabrir.
- `LICENSE.md`: licença proprietária provisória (sem redistribuição, modificação ou exploração comercial sem autorização; código exibido no app é só educacional; jogos dos usuários pertencem a eles).
- Planejamento no repositório: especificação integral (`docs/SPEC.md`), ROADMAP cobrindo as 16 fases, critérios, riscos e aceitação, rotina de desenvolvimento com auditoria de fechamento de fase (`docs/DEVELOPMENT.md`, `docs/audits/`), `CLAUDE.md` e `tools/roadmap-status.sh`; testes que garantem que o ROADMAP cobre todas as fases do spec.
- `Lunet.slnx` e `release-notes.json` nas releases.
- Editor de código: realce de sintaxe C#, números de linha, desfazer/refazer (com junção de digitação), indentação automática, autocompletar em chips, localizar/substituir (com regex), ir para definição, dica do símbolo, referências e diagnósticos ao vivo (Roslyn). Biblioteca `Lunet.Editor` testada em desktop.
- Fonte bitmap embutida (`SpriteFont`, `SpriteBatch.DrawString`) com acentos do português; `SpriteSheet`; filtro de textura (`TextureFilter.Point`).
- Salvamento (`Save.Save/Load`) em JSON por chave, gravação atômica; teclado (`Input.IsKeyDown/IsKeyPressed`) e acelerômetro; `MathEx`, `RandomSource` (determinístico) e `Circle`.
- Modelo de projeto "Demo: Coletor de moedas", que exercita texto, gestos, som, salvamento e aleatório; escolha de modelo ao criar projeto.
- Gestos: Tap, LongPress, Drag e Swipe (`Input.Gestures`), entregues a um único passo de `Update`.
- Áudio: `SoundEffect`/`SoundInstance` (volume, pan, pitch, loop), `Content.LoadSound` e backend Android com SoundPool.
- Pipeline de conteúdo: decodificador PNG próprio, `ContentManager` com cache, `IContentSource` e importação de PNG para `Content/Textures` pelo app.
- Framework: `Game`, passo fixo, `SpriteBatch`, `Texture2D`, resolução virtual com letterbox e toque.
- Compilador Roslyn com diagnósticos (arquivo/linha/coluna) e referências em memória.
- Runtime: carregamento do jogo em contexto descartável; exceções do jogo não derrubam o app.
- Projetos: `lunet.json`, template inicial, salvamento atômico, proteção contra caminhos fora do projeto, ZIP.
- App: botão Run, Preview OpenGL ES com Stop/Restart/Pause/Step, painéis Problemas e Console, referência rápida.
- CI: testes, APK e releases de desenvolvimento a partir da `main`; testes de regras de dependência.

- Estrutura Android inicial, armazenamento local de projetos C# e exportação ZIP.
- Workflow de build e publicação do APK como artifact de CI.

### Changed

- A rotina de fechamento de cada fase agora exige roteiro detalhado de teste no aparelho e aprovação explícita do usuário, registrados na auditoria antes de avançar.
- Instruções compartilhadas migradas de `CLAUDE.md` para `AGENTS.md`, com referências e teste de existência atualizados.
- ROADMAP: itens sem validação em aparelho voltam a pendentes; resumo da Fase 3 e README alinhados ao estado registrado.

### Fixed

- Inspector vazio no modelo Coletor de moedas: `score`, `best` e `lives` agora aparecem (`[Inspect]`).
- Build Android do painel Git: `System.IO.Path` explícito e remoção da atribuição indevida ao parâmetro de progresso `_` na inicialização do repositório.

- Barra do editor: com 6 botões o ⋯ (menu, onde fica "Exportar projeto (ZIP)") saía da tela em aparelhos estreitos; os botões agora dividem a largura.
- **Perda de código ao sair do Preview:** ao parar o jogo o editor era recriado vazio e o salvamento seguinte gravava esse texto vazio por cima do arquivo. Agora só se salva o que o editor realmente carregou (`EditorSession`), e o texto é salvo antes de a tela ser recriada. Também evita regravar um arquivo renomeado ou apagado.
- Ao criar projeto, escolher outro modelo agora desmarca o anterior (os botões de opção não tinham id).
- Laboratório: botão para ligar/desligar o pixel perfect e botão de resolução virtual alternativa; em aparelhos com escala já inteira o pixel perfect não muda nada, e agora dá para forçar uma escala fracionada.
- As releases de desenvolvimento agora usam sempre a mesma chave de assinatura; antes cada build tinha uma chave nova e o Android recusava atualizar uma sobre a outra. **Ao instalar esta versão, desinstale a anterior uma única vez (exporte os projetos em ZIP antes).**

### Performance

- O caminho quente do framework (Tick, SpriteBatch, texto, gestos, timers) não aloca por quadro, verificado por teste; `Input.Gestures` passou a ser `ReadOnlySpan<Gesture>` e `DrawState` virou struct.

### Deprecated

- Nada.

### Removed

- Nada.

### Security

- Projetos ficam no armazenamento privado do aplicativo nesta etapa.
