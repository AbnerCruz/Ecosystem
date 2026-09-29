# Entrada

A entrada chega por `InputState`, atualizada a cada quadro.

- Toque: `TouchCollection` com um `TouchPoint` por dedo.
- Gestos: `GestureRecognizer` reconhece toque, arrasto, pinça e outros.
- Controles virtuais: `VirtualControls` desenha analógico e botões na tela.
- Gamepad e teclado: `Gamepad` e `Keys` funcionam com dispositivos conectados.
- Vibração: `IHaptics` (a implementação é do aparelho).

## Dica

Sempre ofereça controle por toque. Gamepad e teclado são opcionais e devem ser um extra.
