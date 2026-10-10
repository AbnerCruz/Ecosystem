# Alternância on/off por toque — LUNET-428

Use o TouchToggle para menus de som, vibração e opções. Em um projeto Em branco, cole no Game.cs:

```csharp
using Lunet;
using Lunet.Graphics;
using Lunet.UI;
public sealed class ToggleDemo : Game
{
    private TouchToggle music = null!;
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private bool musicEnabled;

    protected override void LoadContent()
    {
        music = new TouchToggle(new RectangleF(24, 100, 200, 64));
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        music.Update(Input);
        if (music.WasChanged)
        {
            musicEnabled = music.Value;
            Log.Info(musicEnabled ? "Som ligado" : "Som desligado");
        }
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(17, 22, 32));
        batch.Begin();
        music.Draw(batch, font, "SOM DESLIGADO", "SOM LIGADO",
            TouchButtonStyle.Default, UiTheme.HighContrast.Button, 2);
        batch.End();
    }
}
```

Toque e solte dentro para alternar. Arraste para fora e solte: não muda. Cancel/desabilitar preservam Value; atribuição por código não dispara WasChanged. O componente não grava arquivos automaticamente nem arbitra controles sobrepostos; utilize SaveData se precisar persistir a escolha.

## Teste no Android

1. Execute no Preview offline e alterne a opção duas vezes; observe a paleta e o rótulo.
2. Arraste para fora antes de soltar, use dois dedos e confira que não há cliques duplicados.
3. Pause, reinicie e teste outros projetos; a demonstração reinicia desligada.
4. Registre passou/falhou/não testado e versão do APK. O CI não valida a ergonomia real do aparelho.
