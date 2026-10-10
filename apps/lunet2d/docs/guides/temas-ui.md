# Temas de UI (LUNET-417)

`UiTheme` é uma paleta local e imutável para reunir cores de `TouchButton`, `TouchSlider`, fundo, painéis e texto. Não cria texturas, fontes ou regras globais; cada tela decide seu tema. Os estilos individuais antigos continuam funcionando.

- `UiTheme.Dark`: superfícies escuras, destaques azuis e texto claro.
- `UiTheme.Light`: superfícies claras, texto escuro e destaques azuis.
- `UiTheme.HighContrast`: fundo preto, texto branco e botões amarelos com texto preto.

No `LoadContent`, conserve `SpriteBatch`, `SpriteFont` e controles normalmente. No `Draw`, selecione o tema e aplique-o:

```csharp
var theme = UiTheme.Dark;
GraphicsDevice.Clear(theme.Background);
batch.Begin();
batch.FillRect(new RectangleF(8, 10, 340, 240), theme.Panel);
batch.DrawString(font, "Configuracoes", new System.Numerics.Vector2(24, 24), theme.Text);
button.Draw(batch, font, "Continuar", theme.Button);
slider.Draw(batch, theme.Slider);
batch.End();
```

Para personalizar, crie `new UiTheme(background, panel, text, buttonStyle, sliderStyle)` com `TouchButtonStyle` e `TouchSliderStyle` definidos pelo jogo. Trocar o valor local do tema não modifica os controles, não perde o estado de toque e não aloca recursos gráficos; a aplicação acontece na próxima chamada `Draw`.

**Teste em aparelho quando integrado:** use seu jogo com botão e slider, troque entre as três paletas e confira fundos, contraste visual e estados pressionados/desabilitados. Confirme que toque, arraste e pausa continuam funcionando e que o projeto antigo manteve suas cores. O teste automatizado desenha os três temas num backend de memória; aparência GL real e legibilidade no aparelho seguem pendentes.

Limites: não há árvore de UI, cascata CSS, preferências globais, persistência do tema, templates novos nem Studio visual nesta etapa.
