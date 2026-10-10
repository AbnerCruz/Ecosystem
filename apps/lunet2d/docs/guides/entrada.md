# Entrada

A entrada chega por `InputState`, atualizada a cada quadro.

- Toque: `TouchCollection` com um `TouchPoint` por dedo.
- Gestos: `GestureRecognizer` reconhece toque, arrasto, pinça e outros.
- Controles virtuais: `VirtualControls` desenha analógico e botões na tela.
- Gamepad e teclado: `Gamepad` e `Keys` funcionam com dispositivos conectados.
- Vibração: `IHaptics` (a implementação é do aparelho).

## Dica

Sempre ofereça controle por toque. Gamepad e teclado são opcionais e devem ser um extra.

## Joysticks de movimento e mira com dedos separados (LUNET-423)

Use sempre coordenadas **virtuais** do jogo, não pixels físicos do Android.
O raio define quanto o polegar deve percorrer; `Area` define onde pode iniciar um toque.
Para preservar a sensibilidade dos jogos já criados, as opções abaixo são **opt-in**.

```csharp
using System.Numerics;
using Lunet;
using Lunet.Input;

var leftArea = new RectangleF(0, 300, 320, 340);
var rightArea = new RectangleF(320, 300, 320, 340);
var move = new VirtualStick(new Vector2(110, 505), 70, leftArea, floating: true)
{
    DeadZone = 0.12f,
    RescaleDeadZone = true,
    ResponseExponent = 1.4f,
    Sensitivity = 1f,
    RequireFreshPress = true
};
var aim = new VirtualStick(new Vector2(530, 505), 75, rightArea, floating: true)
{
    DeadZone = 0.06f,
    RescaleDeadZone = true,
    ResponseExponent = 1.8f,
    RequireFreshPress = true
};

// Na atualização fixa do jogo:
move.Update(Input);
aim.Update(Input, move.TouchId); // não aceita o dedo do analógico de movimento
var velocity = move.Direction;   // magnitude 0–1
var aimDirection = aim.Direction;
// Um botão de tiro pode reservar o dedo que está mirando:
var fire = new VirtualButton(new Circle(new Vector2(610, 400), 32))
{
    RequireFreshPress = true
};
fire.Update(Input, aim.TouchId);
```

- `ResponseExponent > 1` ajuda na microcorreção perto do centro; `< 1` torna a resposta mais agressiva.
- `RescaleDeadZone` tira o salto da zona morta: com 20% de zona morta e deslocamento de 30%, a intensidade é 12,5%.
- `Sensitivity` multiplica a intensidade antes do limite de 100%; alterar esse valor não modifica `Area` nem o tamanho visual do raio.
- `RequireFreshPress = true` evita capturar um dedo que começou em outro lugar e apenas atravessou o controle. É desativado por padrão para preservar jogos antigos.
- `TouchId` vale `-1` quando livre; na ordem `move.Update` e depois `aim.Update(Input, move.TouchId)`, cada um mantém seu dedo até `Released`/`Cancelled`.
- `Cancel()` solta imediatamente o dedo capturado; use em menus, pausa ou mudança de layout. Em modo flutuante, restaura o centro original.
- `Update(input, excludedTouchId)` aceita **um** dedo reservado por chamada. Para três ou mais zonas sobrepostas, defina zonas de início não sobrepostas ou faça arbitragem explícita no jogo.
- Isso é uma API de controles do Framework, não uma interface para mudar a sensibilidade no aplicativo Lunet. Os valores podem ser expostos no menu de cada jogo.

Teste no Laboratório ou num projeto `Em branco` no Android: arraste os dois analógicos simultaneamente, solte apenas um, passe dedos atravessando a área do outro, pause e chame `Cancel()`. Confira toque rápido, diagonal e reabertura do Preview.


## Transições rápidas no Preview Android (LUNET-424)

No Preview, o host guarda a posição original de um `Pressed` até o primeiro `Update`/ `Step` que realmente executar, mesmo que o Android envie vários `Moved` antes disso. Um toque completo entre dois updates é apresentado em duas atualizações sucessivas: primeiro `Pressed`, depois `Released` (ou `Cancelled`). Isso evita perder o toque por diferença entre refresh da tela e passo fixo do jogo. O jogo continua recebendo `InputState` e `VirtualStick` sem qualquer mudança de API.

Para verificar no celular: use `RequireFreshPress = true`, toque e arraste rapidamente dentro da área de um analógico, depois faça toques curtíssimos e dois toques independentes; confira que nenhum dedo captura o analógico vizinho. Teste a 60 e 90/120 Hz, se disponível. Ao reiniciar o Preview, dedos de uma sessão anterior não devem ficar capturados. Validação DEVICE permanece pendente até o teste real.
