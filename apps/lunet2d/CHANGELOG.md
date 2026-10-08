# Changelog

## 0.0.1 — em desenvolvimento

### Fixed

- SAT calcula separação correta quando um polígono contém outro; polígonos sem área não produzem empurrão inválido.
- Medições de alocação isoladas de outras coleções de testes, preservando a exigência de zero bytes no loop; falha concorrente reproduzida também na base anterior.
- Correções rápidas removem apenas a diretiva using, preservando código na mesma linha e comentários; ordenação preserva global using, CRLF e comentários, e recusa blocos ambíguos/condicionais.


### Added

- LUNET-415 (em revisão): `TileMap` JSON v1 em camadas, tileset, colisão, viewport culling e integração com A* e colisão AABB de personagens, com guia offline e testes no host portátil. Tile Studio/DEVICE pendentes.

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
