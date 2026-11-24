# Crie e rode um projeto vazio

Abra **Projetos**, toque em **Novo jogo**, selecione o modelo **Em branco** e abra `Game.cs`. Use orientação vertical. Substitua o conteúdo do arquivo por este exemplo completo.

```csharp full
using Lunet;
using Lunet.Graphics;

public sealed class PrimeiroJogo : Game
{
    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
    }
}
```

Toque em **Rodar**. A tela preta é o resultado esperado. Volte ao código, troque `Color.Black` por `Color.CornflowerBlue` e toque em **Rodar** de novo. A mudança de cor confirma que o Preview está executando a nova versão.

## Próximo passo

[Faça seu primeiro jogo responder ao toque](guide:responder-ao-toque)
