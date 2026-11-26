# Estudo visual do Ecosystem (26/11/2026)

**Pedido do proprietário (26/11/2026):** estudar UX, design, arte, cores, minimalismo, referências contemporâneas e discussões de comunidades. Depois, estabelecer os padrões de visual, layout, fontes, inputs, painéis, estrutura, cores, bordas, estilização, animações e efeitos do Ecosystem, registrá-los aqui e aplicá-los no Lunet. Preferência declarada: **preto, azul, branco e roxo**, sem se limitar a eles. Registro: [ADD-0037](../governance/addenda/ADD-0037-linguagem-visual.md) / DEC-0069.

O resultado normativo está em [`linguagem-visual.md`](linguagem-visual.md), com os valores em [`tokens.json`](tokens.json). Este arquivo guarda o caminho: o que foi lido, o que as fontes dizem e por que cada regra existe. Separação usada: **FATO** (o que a fonte diz), **INFERÊNCIA** (o que concluímos) e **REGRA** (o que adotamos).

## 1. O diagnóstico: por que a interface atual parece feia

Análise das capturas do Lunet (casca Avalonia, 25–26/11):

| Sintoma | Causa |
|---|---|
| Campos de texto parecem "buracos" com borda cinza do tema | O `TextBox` usa a cor do fundo da página e a borda padrão do Fluent. O campo não se distingue da página por tom, só por uma linha dura. |
| Dropdowns, sliders e interruptores parecem de outro app | Ficaram com o estilo padrão do Fluent, recolorido só pela cor de acento. |
| Botões secundários com contorno | Botão com borda e fundo lembra formulário de 2010. Os sistemas atuais usam botão tonal sem borda. |
| Fonte "genérica" | `Segoe UI, Roboto, Inter…` cai na fonte do sistema: DejaVu no Linux, Roboto no Android. Não há uma fonte própria embutida. |
| Painéis "caixa dentro de caixa" | Superfície com borda dentro de superfície com borda. Muitos níveis de cinza e linhas competem. |
| Acento roxo em todo lugar | Chips, botões e seleções, tudo em roxo cheio. O acento deixa de indicar a ação principal. |
| Tudo aparece de uma vez | Sem transição, a mudança de estado parece um "salto". |

INFERÊNCIA: o problema não é a paleta (preto e roxo funcionam). É a falta de **sistema**: escala de tons, papéis de cor, componentes com estados e uma fonte.

## 2. O que as referências e as comunidades dizem

### 2.1 Cor em interfaces escuras

- **Linear** refez o tema em **LCH** porque o espaço é perceptualmente uniforme (vermelho e amarelo com L = 50 parecem igualmente claros). Isso permite gerar superfícies em elevações coerentes (fundo, painéis, diálogos, modais) a partir de poucas cores-base. FATO ([Linear, "How we redesigned the Linear UI"](https://linear.app/blog/how-we-redesigned-the-linear-ui); [refresh](https://linear.app/blog/behind-the-latest-design-refresh)). Análises da estética "Linear" descrevem uma tela quase preta, densidade controlada por opacidades de branco e **um único acento índigo-violeta**. FATO, fonte secundária ([uxdesign.cc](https://bootcamp.uxdesign.cc/the-rise-of-linear-style-design-origins-trends-and-techniques-4fd96aab7646); [buildmvpfast](https://www.buildmvpfast.com/blog/linear-aesthetic-tokens-density-keyboard-first-ux-2026)).
- **OKLCH/OKLab** (Ottosson, 2020): o canal L acompanha a claridade percebida, enquanto no HSL amarelo e azul com o mesmo L parecem muito diferentes. O Tailwind v4 passou a paleta para OKLCH. Rampas são geradas variando L e mantendo matiz e croma. FATO ([Evil Martians](https://evilmartians.com/chronicles/oklch-in-css-why-quit-rgb-hsl); [HackerNoon](https://hackernoon.com/why-hsl-is-broken-for-design-systems-and-how-oklch-fixes-it)).
- **Radix Colors**: escala de **12 passos com papel fixo por passo**:
  - 1–2: fundos;
  - 3–5: fundo de componente (normal, hover, ativo);
  - 6–8: bordas (sutil, interativa, foco);
  - 9–10: cor sólida e hover;
  - 11–12: texto secundário e principal.

  Os passos 11 e 12 passam 4,5:1 sobre o passo 3. FATO ([Radix, "Understanding the scale"](https://www.radix-ui.com/colors/docs/palette-composition/understanding-the-scale)).
- **Material (tema escuro)**: superfícies mais elevadas ficam **mais claras**. O Material recomenda cinza muito escuro (#121212) em vez de preto puro e não usa cores saturadas em superfícies grandes. No M3, as superfícies têm 5 tons de contêiner. FATO ([Material dark theme](https://m2.material.io/design/color/dark-theme); [tone-based surfaces](https://material.io/blog/tone-based-surface-color-m3)).
- **Comunidades (MacRumors, GitHub, Reddit via PopSci, LogRocket)**:
  - Preto puro divide opiniões: economiza bateria em OLED, mas causa halação e "smear" ao rolar.
  - Cinza quase preto é o padrão mais aceito.
  - Texto branco puro cansa: o fórum do Discourse pediu para removê-lo.
  - Um modo escuro feito como inversão do claro "parece barato".

  FATO ([MacRumors](https://forums.macrumors.com/threads/mojave-dark-mode-ui-looks-cheap-and-nasty.2142857); [GitHub community](https://www.Github.com/orgs/community/discussions/23189); [Discourse](https://meta.discourse.org/t/remove-the-pure-white-on-text/408966?tl=en); [LogRocket](https://blog.logrocket.com/ux-design/dark-mode-ui-design-best-practices-and-examples/); [Inkbot](https://inkbotdesign.com/dark-mode/)).
- **60-30-10**: cerca de 60% de cor dominante, 30% secundária e 10% de acento. Quando o acento passa de cerca de 30% da tela, "tudo compete". No escuro, a maior parte é neutra (60–80%) e o acento fica em 5–10%. FATO ([UX Planet](https://uxplanet.org/the-60-30-10-rule-a-simple-way-to-creating-catchy-user-interfaces-e9e2cf957213); [freeCodeCamp](https://www.freecodecamp.org/news/the-60-30-10-rule-in-design/)).
- **Contraste**:
  - WCAG 2.x superestima o contraste de cores escuras: 4,5:1 perto do preto pode ser quase ilegível.
  - O APCA é sensível à polaridade e mais rigoroso com texto corrido.
  - Prática recomendada: cumprir o WCAG 2.x (exigido por lei) e usar o APCA como verificação complementar no escuro.

  FATO ([APCA, Myndex](https://www.github.com/Myndex/SAPC-APCA/blob/master/documentation/WhyAPCA.md); [web.dev](https://web.dev/articles/color-and-contrast-accessibility)).

### 2.2 Profundidade, bordas e sombras

- Resumos do *Refactoring UI*:
  - usar borda pouco; separar com cor de fundo, sombra ou espaço;
  - a luz vem de cima;
  - no escuro, sombras quase não aparecem, e a profundidade vem de tons escuros em degraus;
  - bordas, quando necessárias, em branco translúcido.

  FATO, fonte secundária ([resumo Refactoring UI](https://cdn.jsdelivr.net/npm/claude-code-skills@0.5.0/data/skills/ux-ui-design-system/references/refactoring-ui.md); [Toptal](https://www.toptal.com/designers/ui/dark-mode-ui-design-tips); [MattQ](https://mattqdev.github.io/blog/how-to-make-a-good-dark-mode-design)).
- **O que é lido como "barato" ou "cara de IA"**: botões com gradiente, cartões com borda brilhante e vidro desfocado em tudo; espaçamento inconsistente; superfícies demais deixam a tela "lamacenta". A hierarquia deve vir de peso e tamanho da fonte, não só de cor. FATO ([RAXXO](https://dev.to/raxxostudios/dark-mode-design-that-doesnt-look-ai-2cn3); [DEV](https://dev.to/howard_shaw_3c36a3a6cb900/i-finally-added-dark-mode-and-it-forced-me-to-fix-more-than-colors-2n84)).
- **Raycast/Linear (descrições de terceiros)**: bordas em `rgba(255,255,255,0.06–0.10)` e um brilho quente atrás só do elemento em destaque. FATO, fonte secundária ([open-design Raycast](https://open-design.ai/systems/raycast/)).
- **Apple (Liquid Glass, 2025)**:
  - o vidro fica para controles e navegação; o conteúdo usa materiais padrão;
  - a Apple pede uso comedido;
  - críticos apontaram perda de legibilidade em baixo contraste.

  FATO ([HIG Materials](https://developer.apple.com/design/human-interface-guidelines/materials.md); [Adopting Liquid Glass](https://developer.apple.com/documentation/technologyoverviews/adopting-liquid-glass.md); [Wikipedia](https://en.wikipedia.org/wiki/Liquid_Glass)).

### 2.3 Tipografia

- **Inter** é a escolha mais recomendada para interface: x-height alta, zero cortado, algarismos tabulares e alternâncias contextuais. **Geist** é a alternativa moderna, mais voltada a tamanhos grandes. SF Pro só é licenciada para plataformas Apple. Algarismos tabulares precisam ser ligados onde números se alinham. FATO ([madegooddesigns](https://madegooddesigns.com/inter-font/); [fontalternatives](https://fontalternatives.com/blog/geist-vs-inter-vs-satoshi-product-ui/)).
- **Geist (Vercel)**: um sistema com Sans e Mono e cerca de 14 tamanhos de 12 a 48 px, em pesos de 400 a 600. FATO, parcialmente de fonte secundária ([Geist](https://vercel.com/geist)).
- Faixa recomendada: corpo de 14–18 px e títulos de 20–32 px, conferidos no aparelho. FATO ([uxdesign.cc](https://uxdesign.cc/strategic-ui-font-selection-ratio-efficiency-shape-language-support-8c510d707905)).

### 2.4 Inputs e formulários

- **NN/g**:
  - placeholder não substitui o rótulo, porque some ao digitar e pode parecer campo preenchido;
  - rótulo e dica ficam **fora** do campo, sempre visíveis;
  - rótulo flutuante é melhor que placeholder, mas pior que rótulo fora do campo quando há espaço.

  FATO ([NN/g placeholders](https://www.nngroup.com/articles/form-design-placeholders/)).
- **Smashing**: o rótulo diz o propósito, continua útil com o foco no campo e visível depois de preenchido. Os campos do Material (preenchidos, só com sublinhado) foram criticados por parecer desativados e ter área de toque confusa. FATO ([Smashing forms](https://www.smashingmagazine.com/2017/06/designing-efficient-web-forms); [Material text fields](https://smashingmagazine.com/2021/02/material-design-text-fields)).
- **Baymard**: rótulos inline no celular atrapalham. Recomendações para inputs eficientes. FATO ([Baymard](https://baymard.com/blog/input-fields)).
- **Foco**:
  - WCAG 2.4.7 (AA) exige indicador visível;
  - o 2.4.13 (AAA) usa contorno sólido de pelo menos 2 px como referência;
  - contraste de 3:1 contra o vizinho (1.4.11);
  - deslocamento de 2 px é comum para não encostar no texto.

  FATO ([Sara Soueidan](https://sarasoueidan.com/blog/focus-indicators/); [W3C 2.4.13](https://www.w3.org/WAI/WCAG22/Understanding/focus-appearance)).

### 2.5 Painéis e folhas (sheets)

- **NN/g e Material, sobre bottom sheets**:
  - ter botão de fechar visível, além de arrastar;
  - manter a alça;
  - o scrim fecha a folha modal;
  - uma folha por vez (não empilhar);
  - não usar folha para confirmação crítica nem fluxo longo;
  - fechar sem salvar cancela.

  FATO ([NN/g bottom sheet](https://www.nngroup.com/articles/bottom-sheet/); [Material sheets](https://material.io/components/sheets-bottom); [eBay](https://playbook.ebay.com/design-system/components/bottom-sheet)).

### 2.6 Movimento

- **Material**: easing assimétrico (acelera e desacelera com suavidade); curva padrão com cerca de 300 ms; curva enfática com cerca de 650 ms em transições grandes. FATO ([Material easing](https://m2.material.io/go/design-easing)).
- **Fluent (Microsoft)**: 250 ms (normal), 167 ms (rápido) e 83 ms (mais rápido). FATO ([Microsoft timing](https://learn.microsoft.com/en-us/windows/apps/design/motion/timing-and-easing)).
- **Consenso de sistemas**:
  - micro-feedback em 100–150 ms;
  - transições pequenas em 150–250 ms;
  - interface comum até cerca de 300 ms;
  - mola (spring) para gestos;
  - curva fixa para transições de duração conhecida.

  FATO ([Emil Kowalski, standards](https://raw.githubusercontent.com/emilkowalski/skills/main/skills/review-animations/STANDARDS.md); [userinterface.wiki](https://www.mintlify.com/raphaelsalaja/userinterface-wiki/animation/to-spring-or-not-to-spring)).

### 2.7 Ícones

- Grade de 24 × 24 com margem de 1 px (Lucide). Um único traço em todo o conjunto (1,5–2 px), cantos arredondados, uma biblioteca só e sem misturar contorno com preenchido. FATO ([Lucide guide](https://lucide.dev/guide/design/icon-design-guide); [referência de iconografia](https://raw.githubusercontent.com/referodesign/refero_skill/f78b4eccf112d7a179b92afeafdd7e8684560ac2/references/icons.md)).

## 3. Conclusões que viraram regra

1. **Escuro primeiro, nunca preto puro na interface.** O fundo é quase preto, levemente frio, puxando para azul e violeta, para casar com a paleta preferida. Preto puro fica só no palco do jogo e em mídia.
2. **Profundidade por tom.** No máximo 4 níveis visíveis numa tela: fundo, superfície, superfície elevada e sobreposição. A borda vira linha fina translúcida, usada só quando o tom não basta.
3. **Escala neutra de 12 passos com papéis fixos** (método Radix), gerada em LCH com um leve matiz frio.
4. **Dois acentos com papéis distintos:**
   - **violeta** = marca e ação principal;
   - **azul** = informação, links e seleção;
   - **branco** = texto principal (nunca #FFF puro em texto corrido).

   Cada Product escolhe qual acento lidera: no Lunet é o violeta.
5. **Acento em até 10% da tela.** Uma ação principal cheia por tela; as demais são tonais ou só texto.
6. **Inter embutida** (OFL) para a interface; mono própria para código; algarismos tabulares em números.
7. **Campo preenchido em tom, sem borda.** Rótulo fora e acima, anel de foco de 2 px, erro abaixo, 48 dp.
8. **Movimento curto:** 90/150/220/300 ms, saída mais rápida que entrada, mola só em gesto, respeitando o "reduzir movimento".
9. **Efeitos com orçamento:** no máximo um brilho por tela, gradiente só em momentos de marca, sem vidro no Android.
10. **Contraste duplo:** WCAG 2 AA obrigatório e APCA como piso no escuro. Os testes de cada Product verificam isso.

## 4. Fontes consultadas

Linear (blog), Vercel Geist, Radix Colors, Evil Martians (OKLCH), HackerNoon, Material Design 2/3 (tema escuro, easing, sheets), Microsoft Fluent (timing), Apple HIG (materials, Liquid Glass), NN/g (placeholders, bottom sheet), Smashing Magazine (formulários, campos do Material), Baymard (inputs), Sara Soueidan e W3C (foco), Myndex/APCA e web.dev (contraste), Lucide (ícones), UX Planet e freeCodeCamp (60-30-10), MacRumors, GitHub Community, Discourse Meta, PopSci/Reddit, LogRocket, Toptal, Inkbot e DEV Community (discussões de modo escuro e do visual "barato"). Os links estão nas seções acima. Valores de terceiros sobre Linear, Raycast e Vercel foram tratados como aproximados, não como especificação oficial.
