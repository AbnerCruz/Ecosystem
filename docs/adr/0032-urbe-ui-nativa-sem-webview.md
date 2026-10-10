# ADR-0032 — Urbe C#: interface nativa sem WebView, com a 1.8.4-beta como especificação

## Status

Aceito — **DEC-0043** (ordem do proprietário em 2026-10-10, [ADD-0020](../governance/addenda/ADD-0020-urbe-e-a-1-8-4-nativo-sem-web.md)). A escolha concreta da pilha foi delegada pelo proprietário ("contanto que funcione em ambos") e fica condicionada ao gate **G-N0** abaixo. Substitui a parte de UI/hosts do [ADR-0025](0025-urbe-pilha-ui-hosts-csharp.md).

## Contexto

FATO: o ADR-0025 (DEC-0035-A) escolheu Blazor WebAssembly PWA no Web e .NET MAUI Blazor Hybrid em Android/Windows. No Hybrid a interface é HTML/CSS renderizada numa WebView. O principal motivo da escolha foi o alvo Web/PWA e o editor sobre DOM.

FATO: em 09/10/2026 o proprietário retirou o alvo Web/PWA do cliente C#. Em 10/10/2026 declarou que não quer tecnologia web, quer "um aplicativo desktop e mobile feito em C#", como o Lunet (Android nativo em C# com Views do Android e OpenGL, `apps/lunet2d/src/Lunet.Android/`), e que o cliente C# atual "é outro aplicativo": a 1.8.4-beta é o Urbe.

FATO: o cliente atual (`apps/urbe/csharp/src/Urbe.UI`) apresenta um shell de abas Início/Explorer/Editor/Cidade/Mais (`ShellNavigation.cs`), com a tela inicial mostrando o estado da migração (`Pages/Foundation.razor`) e a cidade como uma aba de botões HTML (`Pages/World.razor`). A 1.8.4 é o oposto: a cidade em canvas de tela cheia é o aplicativo (`apps/urbe/index.html`: `#game`, `#hud`, `#mini`, `#mapao`, `#tools`, `#housePanel`, `#fileSidebar`, `#editorFull`).

FATO: o domínio está em C# puro, sem UI, em `Urbe.Core` (≈17 mil linhas, 464 casos de teste), incluindo o motor de mundo da 1.8.4 (`LegacyTectonicField`, `LegacyHydrologyField`, `LegacyRiverField`, `LegacyTileSampler`, `LegacyTileChunks`, `LegacyVegetation`, `LegacyWorldPixelTextures`, `LegacyWorldSprites`, `LegacyWorldBuildings`, `LegacyWorldGroundDecor`). Hoje esse motor gera RGBA que a UI Blazor converte em PNG `data:` para a WebView.

INFERÊNCIA: um mundo 2D contínuo, animado e com pan/pinch é desenhado de forma mais direta e eficiente numa superfície nativa do que por imagens transportadas a uma WebView; e o pedido do proprietário elimina o motivo original do Blazor.

## Problema

Escolher como o cliente C# desenha sua interface em Android e Windows, sem tecnologia web, reaproveitando o `Urbe.Core`, de modo que o aplicativo seja a 1.8.4-beta (cidade em tela cheia + painéis sobrepostos) e que o editor de texto funcione com teclado virtual e IME.

## Opções

| Opção | Desenho | Pontos fortes | Riscos/custo |
|---|---|---|---|
| **A — Avalonia 12 (UI C# única, Skia) com heads Desktop e Android** **(escolhida)** | `Urbe.Core` intacto; `Urbe.Client` (Avalonia: canvas do mundo, HUD, painéis, editor); heads `Urbe.Desktop` e `Urbe.Android` | Um código de UI para Android e Windows; desenho 2D nativo (Skia) adequado à cidade em pixel-art; sem WebView; testes headless de UI no CI | Editor com IME/teclado virtual no Android precisa de prova (G-N0); nova dependência estrutural |
| B — Caminho do Lunet: Android nativo (Views + Canvas/OpenGL) e outro host nativo para Windows | Duas cascas nativas sobre o Core | Pilha já provada no repositório para Android | Duas UIs para manter e provar; Windows começa do zero |
| C — Manter MAUI Blazor Hybrid e refazer só o desenho das telas | Mesma pilha | Menor troca imediata | Continua sendo web por baixo, contra a ordem do proprietário |

## Decisão

**Opção A**, condicionada a **G-N0**. Fronteira:

```text
Urbe.Core        C# puro (existente, inalterado): vault, domínio, Markdown, páginas, matemática, mundo 1.8.4
Urbe.Client      Avalonia: mundo em tela cheia, HUD, painéis e editor sobrepostos (como a 1.8.4)
Urbe.Desktop     head Avalonia Desktop (Windows; também executa em Linux para desenvolvimento e testes)
Urbe.Android     head Avalonia Android
```

`Urbe.Client` não conhece Android/Windows concretos; capacidades de plataforma (pasta do vault, etc.) entram por interfaces implementadas nos heads. Tudo nasce local em `apps/urbe` (NN-022).

**Gate G-N0 (spike):** (1) o mundo gerado pelo `Urbe.Core` desenhado na superfície nativa com pan/pinch contínuo em Android real e desktop; (2) um editor de texto nativo recebendo digitação pelo teclado virtual do Android, com acentos/IME, seleção e colagem; (3) builds Android e Windows no CI. Se (2) falhar no controle de texto do Avalonia, o editor usa o controle de texto nativo da plataforma embutido pela própria Avalonia (`NativeControlHost`) antes de qualquer outra troca. Se G-N0 falhar como um todo, volta-se a esta ADR com a opção B, sem novo ciclo de decisão de pilha pelo proprietário (ele delegou a escolha).

## Consequências

- **A 1.8.4-beta é a especificação** de experiência e visual: cada tela/fluxo do cliente nativo é comparado lado a lado com a 1.8.4. Telas que não existem na 1.8.4 não entram antes da paridade, salvo melhoria aprovada.
- **Casca Blazor congelada:** `Urbe.UI`, `Urbe.App` e `Urbe.Web` não recebem funcionalidades novas a partir desta ADR. Continuam compilando até o cliente nativo cobrir o que elas fazem; então são removidas num item próprio (UC-34), mantendo uma única autoridade de UI. Fatias de UI Blazor em andamento (PRs abertos) devem ser reconciliadas por seus autores: o que for domínio (`Urbe.Core`) segue; o que for só casca Blazor não é necessário.
- **`Urbe.Core` é preservado** e é o único lugar das regras; o cliente nativo não duplica domínio.
- O APK beta MAUI já distribuído (`app.urbe.csharp`) não é alterado por esta ADR. A identidade e o canal do APK nativo são definidos no item de beta (UC-25), como mudança crítica de distribuição.
- A ordem de entrega passa a ser: G-N0 → cidade da 1.8.4 em tela cheia → vault real → painel da casa/explorer/editor sobrepostos → beta Android → Windows → paridade restante → produção de artefatos (direção nova, DEC-0043).
- Avalonia entra como dependência estrutural do Urbe (licença MIT), registrada em `apps/urbe/docs/csharp/DEPENDENCIES.md`. Pacotes com versão fixa.
- NN-017 continua: CI verde não substitui a validação no aparelho de G-N0, G-C3 e G-C4.

## Alternativas rejeitadas

- **B (caminho do Lunet):** fica como plano de contingência de G-N0. Rejeitada como primeira escolha porque exige duas interfaces para Android e Windows.
- **C (manter Blazor Hybrid):** contraria a ordem do proprietário (sem tecnologia web) e mantém o motivo da insatisfação.
- **Uno Platform:** não traz vantagem sobre A para um mundo 2D desenhado à mão e acrescenta uma camada XAML/WinUI maior.

## Referências

- DEC-0043, ADD-0020; DEC-0035 / [ADR-0025](0025-urbe-pilha-ui-hosts-csharp.md) (substituído na parte de UI/hosts); DEC-0042 / ADD-0019; [ADR-0016](0016-migracao-do-urbe-para-csharp.md).
- `apps/urbe/docs/csharp/ROADMAP.md`, `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`.
- MANIFEST NN-002, NN-003, NN-005, NN-011, NN-017, NN-020, NN-022, NN-023.
- Avalonia: https://docs.avaloniaui.net/
