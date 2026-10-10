# UC-19 — Texturas da Cidade: paridade de pixels com 1.8.4-beta

**Origem normativa:** ordem do proprietário de 2026-10-09: preservar a aparência original do mundo (gráficos, cores, sprites e estilo); UC-19 do roadmap C# e REQ-011/022/026. Não é autorização para redesenhar a cidade.

## O que esta fatia entrega

`LegacyWorldPixelTextures.CreateTile(biome, variant)` em `Urbe.Core` transpõe a rotina de textura **16×16 RGBA opaca** de `src/world/pixel-art.js`, incluindo:

- 19 IDs de bioma, ordem e paleta hexadecimal do cliente 1.8.4-beta;
- as quatro variantes por bioma;
- ruído/hash compatível com `Math.imul`, `>>> 13` e `>>> 16`, ondas da água, rocha, neve e vegetação;
- retorno como buffer independente, sem estado persistido e sem dependência de JS, WebView, runtime Web ou Host.

O teste `LegacyWorldPixelTexturesTests` compara o SHA-256 concatenado das **76** texturas em ordem legada. Oráculo calculado em Node 22 executando exatamente a rotina `makeTexture` do JS legado, sem usar imagens externas, modelos generativos ou reinterpretação de cores:

`84d2e9695bba78e69f1a60b17bba8d5fee8c56c5efc8a4d71877f5771a820d05`

Há hashes individuais para água, rio, campo, neve e pico, além de testes de opacidade, isolamento de buffer e falha explícita de bioma/variante desconhecidos. Qualquer mudança futura nos pixels exige confrontar a autoridade visual legada e revisar intencionalmente o oráculo — não basta atualizar a string do hash para tornar o teste verde.

## Limites técnicos (sem alegar paridade prematuramente)

**Não** foi migrado o renderizador completo nem aplicado este gerador ao Razor da Cidade. As texturas ainda não aparecem no APK; não há alteração de screenshots, controles, modelos 3D, terreno salvo, personalizações, sprites de casas/árvores ou zoom. O algoritmo original também faz composição de chunk, declive, sombra, costas orgânicas, detalhes e sprites; isso continua como trabalho posterior da UC-19.

A integração deve desenhar as texturas como pixel art **sem suavização**, usar a escala original, depois confrontar screenshots da 1.8.4-beta e do Android C# no mesmo cenário (incluindo animação, HUD e paleta). Até o proprietário aprovar o resultado no aparelho, G-C3/G-C4 permanecem abertos.

## Execução dos testes

```sh
dotnet test --project apps/urbe/csharp/tests/Urbe.Core.Tests -c Release --filter LegacyWorldPixelTexturesTests
```

**Sem mudança de autoridade:** `src/world/pixel-art.js` continua intocado para recuperação do cliente legado; a nova função C# é a implementação-alvo incremental, não um segundo escritor de dados ou um serviço compartilhado do Ecosystem. A portabilidade para outros Products não foi declarada sem segundo consumidor (NN-022).
