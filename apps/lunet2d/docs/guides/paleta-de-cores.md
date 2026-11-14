# Paleta de cores do jogo

A paleta oficial mantém sprites, tiles, interface e código com as mesmas cores. Ela fica em **um** arquivo do projeto, `Assets/palette.json`, que vai junto no Git e no ZIP.

## Criar

**Studio → Paleta de cores.** Na primeira vez, escolha:

- **Extrair das artes do projeto**: as cores principais dos seus sprites e PNGs;
- **Começar vazia**; ou
- uma **paleta inicial** (Noite de lua, Floresta, Deserto, Neon, Pastel, Monocromática).

## Editar

- Toque numa cor para mudar matiz, saturação, brilho, opacidade, hex e **nome** (o nome vira o nome no código).
- Segure para mover, duplicar ou remover.
- **Rampa de tons**: gera de 3 a 7 tons da cor selecionada. As sombras puxam para tons frios e as luzes para tons quentes (o "hue shifting" da pixel art), o que evita tons lamacentos.
- **Harmonia**: complementar, análoga, tríade, complementar dividida, tétrade ou monocromática.
- **Das artes do projeto**: acrescenta as cores principais já usadas.
- **Contraste**: mostra quais pares de cores são legíveis para texto e HUD (4,5:1 ou mais).
- **Importar .gpl / hex** e **Exportar .gpl** (GIMP, Krita, Aseprite) em `Notes/`.

## No código

**Código C#** gera `Data/GamePalette.cs`:

```csharp
GraphicsDevice.Clear(GamePalette.Ceu);
batch.DrawString(fonte, "Pontos", posição, GamePalette.Branco);
```

Exporte de novo quando mudar a paleta.

## No Sprite Studio e na IA

- A fileira de cores mostra primeiro as cores do projeto (separadas por um traço), depois as recentes e as do sprite.
- **⋯ → Ajustar o sprite à paleta do projeto** troca cada cor fora da paleta pela mais próxima (pode desfazer).
- **⋯ → Adicionar as cores do sprite à paleta do projeto**.
- A IA lê a paleta oficial antes de desenhar e usa `GamePalette.<Nome>` no código quando o arquivo existe.

## Validação no aparelho (DEVICE)

1. Crie a paleta a partir de uma inicial; edite uma cor e renomeie; gere uma rampa e uma harmonia.
2. Feche e abra de novo: as cores continuam (arquivo `Assets/palette.json`).
3. No Sprite Studio, confira as cores do projeto primeiro; desenhe com uma cor fora da paleta e use "Ajustar o sprite à paleta"; desfaça.
4. Gere o código C#, use `GamePalette.<Nome>` no `Game.cs` e rode.
5. Peça à IA um sprite novo e confira que ele usa só as cores da paleta.
