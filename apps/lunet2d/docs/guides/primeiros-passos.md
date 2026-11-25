# Primeiros passos

Todo jogo Lunet é uma classe que herda de `Game`. O Lunet chama seus métodos no tempo certo; você só escreve o que o jogo faz.

## O esqueleto

```csharp
using Lunet;
using Lunet.Graphics;

public class MeuJogo : Game
{
    protected override void Update(GameTime time)
    {
        // lógica: mover, colidir, pontuar
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
    }
}
```

- `Update` roda em passo fixo, sempre com o mesmo intervalo, então a física é previsível.
- `Draw` roda uma vez por quadro exibido, e só desenha.
- Não guarde estado dentro de `Draw`.

## Dicas

- Toque em uma linha do editor e use o menu para abrir a documentação do símbolo.
- Erros em tempo de execução aparecem no console do jogo, sem fechar o app.

## Importações

Projetos novos têm o arquivo real `Project/Imports.cs`, acessível no grupo Projeto do Explorer. Por isso o Game.cs inicial não precisa repetir as diretivas já globais. Projetos antigos continuam válidos. Leia [importações, ordenação e Context](../manual/ordenacao-importacoes-contexto.md).
