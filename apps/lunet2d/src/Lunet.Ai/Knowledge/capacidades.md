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

## Multiplayer (existe, `using Lunet.Networking;`)

- Sessão: `NetworkSession.Host(new UdpTransport("<GameId>"), porta, new NetworkSessionOptions { GameId = "<GameId>", PlayerName = "Ana" })` (host com jogador; `dedicated: true` para servidor sem jogador) ou `NetworkSession.Join(new UdpTransport("<GameId>"), "192.168.0.10", porta, opções)`. Chame `session.Update()` em todo `Update`. Eventos `PeerJoined`, `PeerLeft`, `Connected`, `Disconnected`; `session.Peers`, `LocalPeerId`, `IsServer`, `Clock.ServerTime`, `Clock.RoundTripTime`.
- Mensagens: classe que implementa `INetSerializable` (`Serialize(NetWriter)`/`Deserialize(NetReader)`), `session.Register<T>(id)` com o MESMO id em todos os aparelhos, `session.On<T>((peer, msg) => …)`, `SendToServer`, `SendTo(peerId, …)`, `Broadcast`, com `DeliveryMode` (`Unreliable`, `Sequenced`, `Reliable`, `ReliableOrdered`). O servidor decide; o cliente envia intenções (input), nunca o resultado.
- Objetos: classe `: NetworkObject` com campos `NetworkVariable<T>` (int, float, double, bool, byte, uint, string, Vector2 ou `INetSerializable`) listados em `protected override IEnumerable<NetworkVariableBase> Variables => [Posicao, Vida];`; `session.Replication.Register("tipo", () => new Tipo())` em todos; o servidor faz `Spawn<Tipo>("tipo", donoPeerId, o => …)` e `Despawn`; clientes desenham remotos com `variavel.Interpolated` e o próprio com `ClientPrediction<TInput,TState>` (`Apply` + `Reconcile(ultimoInputProcessado, estadoDoServidor)`).
- Testes e mesmo aparelho: `new LoopbackNetwork()` + `CreateTransport()` (porta virtual; endereço "loopback"); `new NetworkSimulator(transporte, NetworkConditions.Presets[i].Conditions)` simula rede ruim.
- Descoberta LAN: `new LanDiscovery()`, `Announce(new LobbyInfo(...))`, `Update()`, `Lobbies`; `new DirectoryMatchmaker(descoberta).Find(gameId, versao)`.
- Mesmo aparelho: `SplitScreen.Layout(n, w, h)` (1–4 regiões), `SplitScreen.TouchesOf(jogador, Input.Touches, regioes)`, `batch.Begin(camera, regiao)` para uma câmera por jogador; `TurnOrder` para hot-seat/assíncrono (`Current`, `EndTurn(versao)`, `Eliminate`, `ToJson`/`FromJson`). Só um gamepad por vez.
- Servidor dedicado: `Game.IsHeadless` é true quando o jogo roda no `lunet-server` (PC, sem tela); use `NetworkSession.Host(..., dedicated: true)` nesse caso.
- Guia: `docs/guides/multiplayer.md`.

## IDE (existe)

Editor C# com realce, autocompletar, diagnósticos ao vivo, ir para definição, referências, renomear, formatar, localizar/substituir, multi-cursor, dobrar código; Run com Preview (pausa, passo, reinício, Inspector, Profiler); Git em C#; exportar/importar ZIP; gerar APK do jogo offline no próprio aparelho; documentação offline.

## Studios (existe)

- Sprite Studio: pixel art até 128×128, até 64 frames, FPS, onion skin, lápis/borracha/balde/conta-gotas/linha/retângulos, pincel 1–5 px, espelhar, paleta. Fonte `.sprite.json` (ARGB por frame) + spritesheet `.png` horizontal. **Sem camadas** por enquanto.
- Tile Studio: mapas TileMap JSON v1 com camadas, camada de colisão, visibilidade, pintura/borracha com tileset PNG. **Sem autotile** por enquanto.
- Audio Studio: WAV PCM (recorte, ganho, loop SMPL, síntese simples).
- Music Studio (Audio Studio → Compor música): músicas multifaixa em `Content/Audio/<nome>.song.json` (fonte) exportadas para `<nome>.wav` (o jogo carrega com `Content.LoadMusic("Audio/<nome>.wav")` e `Audio.PlayMusic(musica, loop: true)`). Formato: `{"Version":1,"Name":"…","Bpm":120,"StepsPerBeat":4,"Swing":0,"Root":60,"Scale":"major","Loop":true,"MasterVolume":0.9,"Tracks":[{"Name":"Melodia","Instrument":{"Name":"Lead","Wave":"Square","Attack":0.005,"Decay":0.1,"Sustain":0.7,"Release":0.1,"Cutoff":20000,"Gain":0.5,"Drums":false},"Volume":0.8,"Pan":0,"Echo":0,"Reverb":0}],"Patterns":[{"Name":"A","Steps":16,"Notes":[{"Track":0,"Step":0,"Pitch":60,"Length":2,"Velocity":0.8}]}],"Order":[0]}`. Pitch é MIDI (60 = Dó central); em faixa com `"Drums":true`, 36 bumbo, 38 caixa, 39 palmas, 42 chimbal fechado, 46 aberto, 49 prato, 45 tom. Ondas: Sine, Square, Pulse25, Pulse12, Triangle, Saw, Noise, Fm. Escalas: major, minor, harmonic-minor, dorian, mixolydian, phrygian, lydian, major-pentatonic, minor-pentatonic, blues, chromatic. Depois de escrever um `.song.json`, peça à pessoa para abrir no Music Studio e exportar o WAV.
- Efeitos sonoros desenhados (Audio Studio → Desenhar efeito): `Content/Audio/<nome>.sfx.json` + `<nome>.wav` (`Content.LoadSound("Audio/<nome>.wav").Play()`); parâmetros StartHz, Slide, ArpeggioSemitones, VibratoDepth, Duty, Attack, Sustain, Punch, Decay, LowPass, HighPass, Repeat, Crush, Volume.
- Particle Studio: efeitos em `Content/Effects/<nome>.particles.json` (JSON de `ParticleSettings.ToJson(taxa, max)`); no jogo `Content.LoadParticleEmitter("Effects/<nome>.particles.json", Texture2D.CreateCircle(GraphicsDevice, 16, Color.White))`, `Start()`/`Burst(n, pos)`, `Update(dt)`, `Draw(batch)` (use `BlendState.Additive` quando `Settings.Additive`). `ParticleSettings` aceita `Direction`, `Spread`, `Shape` (`ParticleShape.Point/Circle/Rectangle/Line`) com `ShapeSize`, `Drag`, `LifetimeVariance`, `SizeVariance`, `RotationVariance`, `MinAngularVelocity`/`MaxAngularVelocity`, `ColorStops` (`new ParticleColorStop(0.5, cor)`), `Additive`.
- Data Studio: tabelas em `Content/Data/<nome>.json` (lista de objetos) com esquema opcional `<nome>.schema.json` (`{"Version":1,"ClassName":"Inimigo","Columns":[{"Name":"id","Type":"Text","Key":true,"Required":true},{"Name":"vida","Type":"Integer","Min":1,"Max":100},{"Name":"tipo","Type":"Choice","Choices":["terra","ar"]},{"Name":"drop","Type":"Reference","ReferenceTable":"itens"}]}`; tipos Text, Integer, Number, Boolean, Choice, Reference, Json). A classe gerada fica em `Data/<Classe>.cs`; no jogo `Content.LoadJson<List<Inimigo>>("Data/inimigos.json")`. Prefira dados em tabela a números espalhados no código.
- Palette Studio: paleta oficial do jogo em `Assets/palette.json` (`{"Version":1,"Name":"…","Colors":[{"Name":"Ceu","Argb":4283392680}]}`, ARGB decimal); rampas, harmonias, contraste, `.gpl`/hex e exportação para `Data/GamePalette.cs` (`GamePalette.Ceu`, `GamePalette.All`). Sprite Studio ajusta sprites à paleta.
- Notas e esboços em `Notes/`; importação de arquivos para `Notes/`.

## IA (você)

Ferramentas: ler/buscar/escrever arquivos do projeto (com hash), mapa de símbolos (`lunet.code.outline`), compilar (`lunet.build.compile`, com avisos de arquitetura), criar sprites (`lunet.assets.sprite`), ver imagens como grade de pixels (`lunet.image.inspect`), ler o estilo do projeto (`lunet.project.style`), pesquisar a documentação (`lunet.docs.search`/`lunet.docs.read`) e, quando a pessoa permite, a internet pública (`lunet.web.search`/`lunet.web.fetch`). Toda escrita fica num checkpoint que a pessoa pode desfazer. Modos: Ensinar (só explica, sem escrita) e Fazer (implementa); no Auto, o pedido decide.

## Ainda NÃO existe (não prometa; diga que não existe e ofereça alternativa)

- Camadas no Sprite Studio, seleção/laço, transformações livres; Animation Studio com eventos e estados.
- Autotile, pincéis múltiplos e propriedades por tile no Tile Studio.
- Atlas, UI e Physics Studios.
- Plugins (`Lunet.Editor.SDK`, `*.lunetplugin`).
- Multiplayer pela internet pública com segurança: não há autenticação, criptografia, relay/NAT traversal nem diretório online de partidas; use LAN ou um servidor dedicado próprio. Não há delta compression nem compensação de lag.
- Depurador com breakpoints.
- Publicação em lojas, assinatura de release com chave do usuário e AAB (o APK gerado é de desenvolvimento).
- Versão desktop completa do Lunet: existe uma prévia (spike) para Windows/Linux/macOS que abre, edita, compila e roda projetos, ainda sem Studios, IA e shaders customizados na prévia.
- Ver imagens nativamente: imagens são lidas pela grade de `lunet.image.inspect`.
