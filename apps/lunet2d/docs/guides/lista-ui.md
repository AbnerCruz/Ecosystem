# Lista de UI com seleção por toque — LUNET-429

`TouchListView` combina a rolagem de `TouchScrollArea` com seleção segura de linhas. É um controle reutilizável do Lunet para menus, missões, inventário e configurações, sem criar árvore de interface ou serviços externos. O jogo continua dono das listas e de seus dados.

Cole este exemplo completo em `Game.cs` de um projeto em branco:

```csharp
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class ListDemo : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private TouchListView list = null!;
    private readonly string[] missions = new string[40];

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        for (int i = 0; i < missions.Length; i++)
            missions[i] = "MISSAO " + (i + 1);
        list = new TouchListView(new RectangleF(16, 80, 328, 420), missions.Length, 48, 4);
    }

    protected override void Update(GameTime time)
    {
        list.Update(Input, (float)time.DeltaSeconds);
        if (list.ActivatedIndex >= 0)
            Log.Info("Missao " + (list.ActivatedIndex + 1) + " ativada");
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(17, 20, 30));
        list.Draw(batch, font, missions, TouchButtonStyle.Default, Color.Blue, 2);
    }
}
```

O controle faz **hit-test com viewport**, captura um único ID de dedo até soltar e só confirma `ActivatedIndex` no `Released` da linha original. Se houver arraste, deslocamento lateral maior que o limiar, cancelamento, saída da lista ou troca de dedo, não ativa a linha. `WasSelectionChanged` diferencia mudança de seleção de um segundo toque na mesma linha. Eventos voltam a -1/falso no próximo Update.

O `Draw` recebe os textos do jogo e **desenha apenas as linhas visíveis** (sem construir dezenas de botões). Ele inicia e encerra seus próprios lotes, desenha o conteúdo com `Begin(clip: Bounds)` e renderiza a barra proporcional. Não coloque `list.Draw` dentro de outro `SpriteBatch.Begin/End`. `GetVisibleRange`, `HitTest` e `GetItemBounds` podem ser usados para desenhar sprites ou informações adicionais somente nas linhas da viewport. `ScrollToItem`, `Select`, `ItemCount`, `Bounds`, `Cancel` e `IsEnabled` atendem menus dinâmicos.

A implementação não aloca objetos durante `Update` ou consultas geométricas. A lista de rótulos e a fonte pertencem ao jogo; não há persistência, alteração de projetos existentes, fonte escondida ou rede.

## Teste no aparelho Android

1. Crie um projeto em branco, cole o código e execute no Preview. A lista deve ocupar o centro, respeitando margens e clipping.
2. Toque em **MISSAO 1**: ela fica selecionada. Toque novamente: a seleção não muda, mas a ativação acontece outra vez.
3. Arraste rapidamente para cima: a lista acompanha e depois desacelera; o gesto não deve abrir uma missão por engano.
4. Tente dois dedos simultâneos, arrastar horizontalmente, arrastar para fora e voltar. Apenas o dedo capturado pode confirmar, e o gesto deve ser cancelado quando houver arraste.
5. No Preview isolado, pause, retome e reinicie; confirme que a lista não ultrapassa o começo/fim e que a rolagem permanece fluida.
6. Abra um projeto antigo e execute-o para verificar ausência de regressão.

Verifique a versão exata do APK e registre passou/falhou/não testado. CI verde não substitui validação visual e tátil real, e a Fase 4 não está concluída.
