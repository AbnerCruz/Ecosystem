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

## Tempo contextual (Context.Time)

`Time` é o mesmo valor `GameTime` passado ao callback corrente, inclusive quando o host roda múltiplos passos fixos por quadro ou um passo manual.

```csharp
public void Update()
{
    if (Input.TryGetPointer(out var target))
    {
        var direction = target - Position;
        var distance = direction.Length();
        if (distance > 0f)
            Position += direction / distance * MathF.Min(distance, 100f * Time.DeltaSeconds);
    }
}
```

O exemplo pressupõe `Position` como um campo Vector2 e `global using static Lunet.Context;` em Project/Imports.cs. O Game continua chamando `player.Update()` de dentro do seu Update.

- `Time.DeltaSeconds`: passo fixo no Update, intervalo do quadro no Draw.
- `Time.TotalSeconds`: tempo simulado acumulado.
- `Time.Interpolation`: fração de interpolação usada no callback.
- Acesso fora de Update/Draw (inclusive Initialize, LoadContent, Dispatcher, Pause, Resume, UnloadContent e workers) lança InvalidOperationException, em vez de inventar tempo zero ou retornar um valor obsoleto. Timers e áudio executados sincronicamente dentro do Update recebem o mesmo valor.
- Preserva as assinaturas Update(GameTime) e Draw(GameTime) de Game e a passagem explícita para quem preferir.
- Testes em ContextTimeTests.cs verificam equivalência exata, múltiplos passos, pausa, Step, restauração de contexto em hosts aninhados e ausência de vazamento em worker ou falha. Execução pendente de CI.

## Ciclo de vida sem parâmetros

O Game também aceita as sobrescritas sem parâmetros, com Time, Input e Batch contextuais:

```csharp
public sealed class MyGame : Game
{
    private readonly Player player = new();

    protected override void Update() => player.Update();

    protected override void Draw()
    {
        GraphicsDevice.Clear(Color.Black);
        Batch.Begin();
        player.Draw();
        Batch.End();
    }
}
```

As assinaturas existentes `Update(GameTime time)` e `Draw(GameTime time)` continuam virtuais e disponíveis, inclusive em binários previamente compilados. O host chama exclusivamente a versão com GameTime; sua implementação base delega à versão sem parâmetros. Assim, uma sobrescrita com GameTime tem precedência, salvo se ela própria chamar base.Update(time) / base.Draw(time) para encadear a lógica. É permitido misturar as duas formas entre Update e Draw. Não há detecção por reflexão, execução duplicada automática, alteração dos timers ou mudança no tempo do loop. A forma contextual requer callback síncrono do host como antes.

A compatibilidade de código fonte não é absoluta: reflexão não tipada por nome (por exemplo, GetMethod("Update", bindingFlags)) pode se tornar ambígua; especifique os tipos dos parâmetros. Uma classe antiga que já declarava método Update()/Draw() sem override pode receber diagnóstico de ocultação; revise se deseja torná-lo uma sobrescrita.

Os testes ParameterlessLifecycleTests verificam execução única, precedência, chamada base intencional, mistura de estilos, pausa e Step. CI pendente.
