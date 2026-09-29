# Áudio

O áudio é dividido em barramentos (`AudioBus`) controlados por um `AudioMixer`, por exemplo música, efeitos e interface.

- `SoundEffect` e `SoundInstance`: sons curtos, com volume, tom e balanço.
- `Music`: faixas longas, uma por vez, com transição.
- Cada barramento tem volume próprio, ideal para um menu de opções.

Se o aparelho não tiver saída de áudio, o jogo continua rodando com o backend nulo.
