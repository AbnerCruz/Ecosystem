# Laço principal e tempo

O laço usa **passo fixo**: `Update` roda quantas vezes forem necessárias para acompanhar o relógio, sempre com o mesmo `GameTime.Delta`.

- Use `time.Delta` para velocidades: `posicao += velocidade * time.Delta`.
- `time.Total` é o tempo desde o início do jogo.
- Se o aparelho engasgar, o laço recupera o tempo perdido com um limite, para não travar.

## Timers

Use `Timers` para ações atrasadas ou repetidas em vez de contar quadros à mão.

## Boas práticas

- Não aloque objetos dentro de `Update` ou `Draw`: use `ObjectPool` para balas, partículas e efeitos.
- Use `RandomSource` com semente para resultados reproduzíveis.
