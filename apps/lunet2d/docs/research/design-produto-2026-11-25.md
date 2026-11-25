# Estudo — design de produto do Lunet (teclado e app inteiro), 25/11/2026

**Pedido do proprietário (25/11):** "O design do teclado ainda está muito amador, tem que estudar mais, pensar mais, analisar melhor. Não somente do teclado, de todo o app ainda dá pra melhorar." Item **LUNET-537**. Base: casca `Lunet.Client` (ADR 0013), teclado da LUNET-534, capturas headless do CI.

## 1. Auditoria crítica (o que deixa o app amador hoje)

Analisando as capturas do celular (412×915) e de tela larga, por ordem de impacto:

1. **Código sem cor, sem número de linha, sem linha atual.** O editor é um `TextBox` cru. É o sinal mais forte de "protótipo" numa IDE: todo editor de referência (VS Code, Sora Editor/AndroidIDE, Textastic, Spck) mostra realce, números de linha e a linha do cursor.
2. **Teclado com cara de grade de botões, não de teclado.**
   - Teclas em fonte monoespaçada, pequenas e com pouco contraste com o fundo.
   - Letras e modificadores com a mesma cor.
   - Não há balão de prévia ao tocar.
   - Dicas de acento minúsculas poluem as teclas.
   - As abas são "chips" de site, numa faixa separada que gasta 40 dp sem dar nada em troca.
   - Não há sugestões enquanto se digita, que é o que torna o Gboard (e qualquer IDE) rápido.
3. **Cromo genérico.**
   - A barra de topo enfileira ícones sem hierarquia.
   - O título usa o mesmo peso dos botões.
   - A barra de abas inferior não segue as medidas de navegação do Material 3: altura, indicador em pílula, rótulo.
   - A Home é uma grade de cartões cinza iguais, com o caminho da pasta como subtítulo (dado técnico, não informação útil).
4. **Estados vazios e painéis corretos, mas sem acabamento:** espaçamentos desiguais, ícones de 20 dp em alvos de 48 dp sem fundo de estado, nenhum háptico (P7 do UX.md).

## 2. Referências e medidas

- **Família visual (obrigatória):** princípios P1–P13 do Numa ([UX.md](../../../urbe/docs/csharp/UX.md)), que o estudo de 24/11 manda virar regra do Ecosystem.
  - P1: resposta em menos de 100 ms.
  - P3: nada de modal no fluxo.
  - P4: o polegar manda, alvos ≥ 48 dp.
  - P5: de 5 a 7 ações por barra.
  - P6: movimento de 150 a 250 ms.
  - P7: háptico.
  - P9: grade de 8 e tokens.
  - P12: AA.
- **Material 3:**
  - Barra de topo regular de 64 dp, com ícones de 24 dp em alvos de 48×48 ([medidas M3](https://docs.rs/crate/egui-material3/latest/source/src/topappbar.rs); [SAP Fiori M3 — top app bar](https://www.sap.com/design-system/fiori-design-android/v26-1/components/m3-standard-components/top-app-bar/usage)).
  - Barra de navegação com ícone + rótulo, de 3 a 5 destinos, sem truncar rótulo ([SAP Fiori M3 — navigation bar](https://www.sap.com/design-system/fiori-design-android/v26-4/components/m3-standard-components/navigation-bar/usage)).
  - Superfícies escuras em níveis tonais (container lowest → highest), elevação por clareamento e não por sombra ([Material — tone-based surfaces](https://material.io/blog/tone-based-surface-color-m3), [dark theme](https://m2.material.io/design/color/dark-theme)).
- **Teclado (Gboard e LatinIME):**
  - Teclas com cantos arredondados desde o Gboard 6.2 ([Android Authority](https://androidauthority.com/gboard-rounded-keys-beta-3532723)).
  - Altura e espaçamento ajustáveis, com presets de 0,85 a 1,15 ([XDA](https://www.xda-developers.com/gboard-8-7-2-extra-tall-extra-short-keyboard-height/), [Android Central](https://androidcentral.com/apps-software/gboard-for-android-new-option-for-resizing)).
  - O espaço vertical entre teclas faz parte do alvo de toque ([LatinIME](https://gitlab.e.foundation/e/os/android_packages_inputmethods_LatinIME/-/commit/40ba31a9cc865b665bd16a67cbdbd76fffba887b)).
  - Teclas de 50 dip no exemplo AOSP ([SoftKeyboard dimens](https://android.googlesource.com/platform/development/+/c8e94bf/samples/SoftKeyboard/res/values/dimens.xml)) e 44 no teclado Fuchsia ([constants.dart](https://fuchsia.googlesource.com/topaz/+/07b01d6bc1f90e09dc3a9f6c4a05fd10c5c1f7e5/shell/keyboard/lib/constants.dart)).
  - Faixa superior do Gboard: sugestões e atalhos de ferramentas na mesma linha.
- **Editor de código:**
  - AvaloniaEdit 12.0.0 (MIT): realce por definição, números de linha, linha atual, virtualização. A 12.0 traz "melhor UX com caneta ou toque" ([releases](https://github-users.yhajoa.workers.dev/AvaloniaUI/AvaloniaEdit/releases)).
  - O histórico de bugs de IME dele ([estudo de 24/11](../plans/lunet-casca-avalonia-2026-11-25.md)) deixa de pesar: o editor de código não usa mais o IME do sistema (LUNET-534).

## 3. Decisões

| # | Decisão | Medida de aceite |
|---|---|---|
| D1 | **Editor de código = AvaloniaEdit** (`TextEditor`) com números de linha, realce C#, linha atual e seleção nas cores do tema. IME desligado na área de texto; o teclado Lunet edita o `Document` (um passo de desfazer por ação). O `TextBox` fica só nas Notas (prosa, teclado do sistema). Substitui o caminho provisório; a view sobre `PieceTable` (LUNET-533) só volta se o AvaloniaEdit falhar no aparelho. | Teste headless: números de linha visíveis; realce aplicado (`SyntaxHighlighting` C#); teclas, atalhos e métodos editam o documento e desfazem em um passo. |
| D2 | **Teclado com anatomia de teclado.** (a) Faixa superior única de 44 dp: à esquerda, três ícones (Teclas ⌨, Atalhos `{}`, Métodos ƒ); no resto, **sugestões** de símbolos que começam com a palavra sob o cursor (catálogo + jogo), tocáveis como no Gboard. (b) Letras em fonte de interface (não mono), 22 dp, sobre `SurfaceKey` (mais claro); modificadores (⇧ ⌫ ?123 ⏎) sobre `SurfaceKeyMod` (mais escuro); Enter com acento. (c) Espaçamento: 6 dp horizontal e 8 dp vertical dentro do alvo de toque, raio 8. (d) **Balão de prévia** acima da tecla ao tocar (letras), sem animação. (e) Sem dicas de acento impressas nas teclas: o balão do toque longo mostra os acentos. (f) Altura: 4 linhas de letras + números = 5 × 50 dp + 44 de faixa ≈ 294 dp no retrato; menos de 40% da janela, sempre. | Teste: faixa de 44 dp; contraste AA de letra sobre tecla; sugestões filtradas pelo prefixo; balão visível ao pressionar; alturas dentro dos limites. |
| D3 | **Barra de topo M3** de 56 dp: voltar, título (nome do jogo) com subtítulo do estado (Parado, Rodando, 2 problemas) e ação primária Rodar/Parar à direita. Console, Problemas e Mais entram num único botão "⋯" de painéis (P5: no máximo 3 ações). | Teste de layout: ≤ 3 controles na barra; alvo ≥ 48 dp. |
| D4 | **Barra de abas M3:** 64 dp, ícone 24 dp, indicador em pílula de 56×28 sob o ícone, rótulo 12 dp. Some quando um teclado está aberto. | Teste: altura e indicador; seleção visível. |
| D5 | **Home útil:** cartões com a cor da miniatura derivada do jogo, nome e "editado há X". O caminho da pasta sai do cabeçalho e vai para Ajustes. Os destinos (Jogar, Studios…) viram uma fileira de ícones com rótulo, não botões de texto. | Captura antes/depois; teste: nenhum texto técnico no cabeçalho. |
| D6 | **Superfícies tonais** nos tokens: `Surface` < `SurfaceContainer` < `SurfaceContainerHigh` < `SurfaceKey`, todas com texto AA. | `Tokens_TextPairsMeetAA` cobre os novos pares. |
| D7 | **Háptico** (P7): toque em tecla e ação concluída chamam um gancho `PlatformHaptics`; o Android usa `PerformHapticFeedback` (KeyboardTap / Confirm). | Teste do gancho; aparelho pendente. |

## 4. Ordem de execução

D1 → D2 → D6 → D3/D4 → D5 → D7. Cada passo leva capturas headless de celular e tela larga ao PR e passa no emulador do CI. O que depende do toque real (latência sentida, háptico, conforto da altura) fica listado como **não testado no aparelho** até o proprietário relatar.
