# SpriteBatch contextual

Extensão do contrato E3 aprovada em 28/11/2026. O framework continua permitindo quantos lotes explícitos o jogo precisar. A conveniência é opt-in por `Lunet.Context.Batch` ou pela importação estática existente.

## Uso básico

Em Project/Imports.cs, acrescente (se ainda não estiver presente):

```csharp
global using static Lunet.Context;
```

Player.cs:

```csharp
public sealed class Player
{
    public RectangleF Rect = new(100, 100, 20, 20);

    public void Draw()
    {
        Batch.Draw(Rect, Color.Red);
    }
}
```

Game.cs:

```csharp
public sealed class MyGame : Game
{
    private readonly Player player = new();

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        Batch.Begin();
        player.Draw();
        Batch.End();
    }
}
```

O exemplo pressupõe os imports essenciais de Lunet e Lunet.Graphics. Não é preciso herdar Game em Player, criar um SpriteBatch por entidade ou passar o lote a cada chamada.

## Contrato

- Sem lote aberto, Context.Batch retorna a instância padrão preguiçosa e reutilizável do dispositivo gráfico do jogo corrente.
- Com exatamente um lote aberto, retorna esse lote, mesmo se criado explicitamente por outro sistema.
- Com vários lotes legados abertos ao mesmo tempo, a resolução contextual lança InvalidOperationException; use a referência explícita. Nenhum novo limite é imposto a chamadas explícitas.
- Begin/BeginSorted/End/CancelSorted continuam explícitos. Não há abertura, flush ou ordenação automática.
- Acesso contextual exige callback síncrono de host ativo e autorização gráfica, como Context.Graphics. Não é propagado para workers. Não é uma instância global compartilhada por todos os jogos.
- A instância padrão não substitui referências explícitas; pode ser guardada durante a sessão quando necessário. Não guarde referências entre sessões.
- O registro do dispositivo é atualizado no fechamento normal, no cancelamento ordenado e na falha de End. Não há rollback de pixels já enviados.

## Passagens explícitas

```csharp
worldBatch.Begin(camera);
player.Draw(); // Batch aponta para worldBatch
worldBatch.End();

uiBatch.Begin();
hud.Draw(); // Batch aponta para uiBatch
uiBatch.End();
```

Também é possível reutilizar o mesmo lote com Begin(camera) e Begin() em passagens sucessivas. O usuário pode continuar passando lotes como parâmetros.

## Validação

Testes adicionados em ContextTests.cs: reutilização, desenho contextual, alternância de lotes explícitos, ambiguidade, cancelamento e exclusividade ordenada, hosts aninhados, worker e ausência de autorização. O ambiente do autor não dispõe de SDK .NET, acesso git direto ou emulador; execução depende do CI. Não considerar os testes aprovados nem o APK publicado sem evidência.
