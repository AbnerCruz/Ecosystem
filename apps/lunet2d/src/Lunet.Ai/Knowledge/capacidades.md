# Lunet — o que existe e o que ainda não existe

Este documento é a referência da IA sobre o próprio Lunet. Use-o para responder "o Lunet consegue…?" e para não prometer recursos inexistentes. Para detalhes de API, pesquise a referência do Framework (`lunet.docs.search`).

## Produto

Lunet é um ambiente code-first para criar jogos 2D em C# no Android: Framework (API do jogo), IDE (editor de código, compilação Roslyn no aparelho, Preview) e Studios (ferramentas visuais opcionais sobre os mesmos arquivos). O código é a fonte da verdade: nenhuma lógica escondida em assets.

## Estrutura de um projeto

- `lunet.json`: manifesto (nome, GameId, ponto de entrada, orientação portrait/landscape). Não edite à mão sem necessidade.
- `Game.cs` (ponto de entrada) e outros `.cs`: todos os `.cs` fora de `Tests/` são compilados juntos.
- `Content/`: conteúdo do jogo, prioritário (`Content/Textures`, `Content/Audio`, `Content/Data`…).
- `Assets/`: arte criada nos Studios (sprites `.sprite.json` + `.png`).
- `Notes/`: notas Markdown, esboços e arquivos importados pela pessoa. Nunca compilados; vão junto no ZIP e no Git.
- `.lunet/`: dados internos (saves do Preview). Não altere.

Caminhos de conteúdo no código: `Content.LoadTexture("Assets/heroi.png")` ou relativos a `Content/` (ex.: `Content.LoadSound("Audio/beep.wav")`).

## Framework (existe)

- Ciclo do jogo: classe que herda `Game` com `Initialize`, `LoadContent`, `Update(GameTime)` (passo fixo), `Draw(GameTime)`, `OnPause`, `OnResume`. `Configuration` define resolução virtual e taxa de atualização.
- Gráficos: `SpriteBatch`, `Texture2D`, `Color`, `Camera2D`, `Viewport`/escala, `SpriteSheet`, `SpriteAnimationClip`, `SpriteAnimator`, `TextureAtlas`, `NineSlice`, `RenderTarget2D`, `Shader`/`Material`, `BlendState`, `DebugDraw`, partículas (`ParticleEmitter`, `ParticleSettings`), `TileMap` (JSON v1, camadas, colisão, culling).
- Texto: `SpriteFont` (bitmap, `CreateDefault`) e `TrueTypeFont`.
- Entrada: toque (`TouchCollection`, `Pointer`), gestos (`GestureRecognizer`: tap, swipe, pinça…), teclado (`Keys`), gamepad, controles virtuais (`VirtualStick`, `VirtualButton`), vibração (`IHaptics`).
- Áudio: `SoundEffect`, `Music`, `AudioMixer`/`AudioBus` (WAV/OGG/MP3 conforme o aparelho).
- Física: Box2D gerenciado (`PhysicsWorld`, `RigidBody2D`, `Collider2D`, `Fixture2D`, `Joint2D`, contatos, raycast).
- Cenas: `Scene2D`, `Entity2D`, `Component2D`, `Transform2D`.
- UI de jogo: `TouchButton`, `TouchSlider`, `TouchToggle`, `TouchListView`, `TouchGridView`, `TouchScrollArea`, layouts (`UiStackLayout`, `UiGridLayout`), temas (`UiTheme`).
- Utilidades: `Tween`/`Ease`, `Timers`, `RandomSource`, `ObjectPool`, `MathEx`, `Geometry`, `GridPathfinder` (A*), saves (`Save`, `ISaveStore`), `Localization`, `GameLog` (`Log.Info`).
- Inspector: atributos `[Inspect]`, `[Range]`, `[Tooltip]`, `[Color]`, `[Asset]`, `[Group]`, `[ReadOnly]`, `[Hidden]`, `[Multiline]`.

## IDE (existe)

Editor C# com realce, autocompletar, diagnósticos ao vivo, ir para definição, referências, renomear, formatar, localizar/substituir, multi-cursor, dobrar código; Run com Preview (pausa, passo, reinício, Inspector, Profiler); Git em C#; exportar/importar ZIP; gerar APK do jogo offline no próprio aparelho; documentação offline.

## Studios (existe)

- Sprite Studio: pixel art até 128×128, até 64 frames, FPS, onion skin, lápis/borracha/balde/conta-gotas/linha/retângulos, pincel 1–5 px, espelhar, paleta. Fonte `.sprite.json` (ARGB por frame) + spritesheet `.png` horizontal. **Sem camadas** por enquanto.
- Tile Studio: mapas TileMap JSON v1 com camadas, camada de colisão, visibilidade, pintura/borracha com tileset PNG. **Sem autotile** por enquanto.
- Audio Studio: WAV PCM (recorte, ganho, loop SMPL, síntese simples).
- Notas e esboços em `Notes/`; importação de arquivos para `Notes/`.

## IA (você)

Ferramentas: ler/buscar/escrever arquivos do projeto (com hash), compilar (`lunet.build.compile`), criar sprites (`lunet.assets.sprite`), ver imagens como grade de pixels (`lunet.image.inspect`), ler o estilo do projeto (`lunet.project.style`), pesquisar a documentação (`lunet.docs.search`/`lunet.docs.read`). Toda escrita fica num checkpoint que a pessoa pode desfazer.

## Ainda NÃO existe (não prometa; diga que não existe e ofereça alternativa)

- Camadas no Sprite Studio, seleção/laço, transformações livres; Animation Studio com eventos e estados.
- Autotile, pincéis múltiplos e propriedades por tile no Tile Studio.
- Atlas, Palette, UI, Physics, Particle e Data Studios.
- Plugins (`Lunet.Editor.SDK`, `*.lunetplugin`).
- Multiplayer em tempo real: só existe uma base experimental TCP/LAN (`Lunet.Networking`); não há UDP, lobby, replicação ou servidor dedicado.
- Depurador com breakpoints.
- Publicação em lojas, assinatura de release com chave do usuário e AAB (o APK gerado é de desenvolvimento).
- Versão desktop do Lunet.
- Ver imagens nativamente: imagens são lidas pela grade de `lunet.image.inspect`.
