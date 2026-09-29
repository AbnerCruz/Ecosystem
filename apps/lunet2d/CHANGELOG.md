# Changelog

## 0.0.1 — em desenvolvimento

### Added

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

- Nenhuma alteração anterior.

### Fixed

- Nenhuma correção anterior.

### Performance

- O caminho quente do framework (Tick, SpriteBatch, texto, gestos, timers) não aloca por quadro, verificado por teste; `Input.Gestures` passou a ser `ReadOnlySpan<Gesture>` e `DrawState` virou struct.

### Deprecated

- Nada.

### Removed

- Nada.

### Security

- Projetos ficam no armazenamento privado do aplicativo nesta etapa.
