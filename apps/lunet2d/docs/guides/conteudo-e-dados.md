# Conteúdo e dados

`ContentManager` carrega imagens, sons e textos do projeto e mantém cache, então carregar duas vezes o mesmo arquivo é barato.

- Imagens PNG viram `Texture2D`.
- `Localization` troca textos por idioma.
- `ISaveStore` grava o progresso do jogador de forma segura: se o app fechar no meio, o arquivo anterior continua válido.

## Exemplo

```csharp
var textura = Content.Load<Texture2D>("player.png");
```
