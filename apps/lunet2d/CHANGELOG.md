# Changelog

## 0.0.1 — em desenvolvimento

### Added

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

- Sem medições ainda.

### Deprecated

- Nada.

### Removed

- Nada.

### Security

- Projetos ficam no armazenamento privado do aplicativo nesta etapa.
