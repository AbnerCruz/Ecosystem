# Revisão global de UX e design — 28/11/2026

## Método e escopo
Auditoria de README, AGENTS §1.4, DEC-0069, ADD-0037, linguagem-visual v1, tokens e vitrine HTML/CSS/JS; confronto com fontes primárias, discussões de profissionais e requisitos de domínios distintos. Esta missão altera documentação e portal de exemplo, não o código dos aplicativos. Não houve entrevista, teste com usuários ou avaliação de hardware nesta etapa.

## Auditoria
O estudo de 26/11 partiu de capturas do Lunet e da estética de ferramentas escuras. A preferência do proprietário tinha a ressalva de não limitar a paleta. O contrato perdeu essa ressalva: escuro obrigatório, violeta/azul fixos, um botão principal por tela, três tamanhos de fonte, campos sem bordas, cartões não aninháveis. A vitrine repetia jogo, APK, sprites, mapa e código como se fossem exemplos universais. Além disso, WCAG texto grande foi traduzido inadequadamente como 20/600 e APCA tratado como obrigação equivalente a WCAG. A v2 corrige isso sem retirar coerência nem os estilos que já funcionam.

## Pesquisa externa e confronto
| Referência | Evidência | Decisão Ecosystem |
|---|---|---|
| [Adobe Spectrum — Design tokens](https://spectrum.adobe.com/page/design-tokens/) | Primitivos, semânticos e componentes; valores variam por plataforma, densidade e tema. | Papéis fixos, valores por tema; componente só se necessário. |
| [Fluent 2 — Color](https://fluent2.microsoft.design/color/) e [tokens](https://fluent2.microsoft.design/design-tokens/) | Paletas de marca e papéis semânticos, aliases de claro/escuro. | Marca independente de status, múltiplos acentos demonstráveis. |
| [Atlassian — tokens](https://atlassian.design/foundations/tokens/design-tokens) | Um conjunto de nomes com valores por tema. | Namespace v2 e perfil declarado; não copiar hex diretamente. |
| [IBM Carbon — color](https://www.carbondesignsystem.com/building-blocks/foundations/color/overview), [themes](https://www.carbondesignsystem.com/building-blocks/foundations/themes/overview) e [data table](https://www.carbondesignsystem.com/building-blocks/core/components/data-table/guidelines) | Temas múltiplos e densidades de tabela; camadas contextuais. | Densidade por tarefa, não por dispositivo; grades e bordas legítimas. |
| [W3C Design Tokens Community Group](https://www.w3.org/community/design-tokens/) | Especificação comunitária favorece aliases, temas e marcas. | Separar papéis de valores; não alegar conformidade formal de formato nesta migração. |
| [WCAG 2.2](https://www.w3.org/TR/wcag/), [texto](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), [não textual](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) | Limiares dependem do uso; limites visuais de controles e foco podem ser essenciais. | Campo sem borda é opção, não obrigação; AA baseline, APCA complementar. |
| [Android — window size classes](https://developer.android.com/develop/adaptive-apps/guides/use-window-size-classes), [touch targets](https://support.google.com/accessibility/android/answer/7101858?hl=en) | Layout muda com janela, alvo tátil recomendado 48dp. | Variar layout e densidade sem sacrificar alvo. |
| [Slack Engineering — Gradual Design System](https://slack.engineering/the-gradual-design-system-how-we-built-slack-kit/) | Componentes precisam ser extensíveis e atender usos diferentes. | Evolução incremental, sem extrair dependências prematuramente. |
| [Radix Colors](https://www.radix-ui.com/colors) | Rampas com finalidades de fundo, estados, bordas, texto. | Rampas úteis, sem universalizar a estética escura ou proibir bordas. |

## Comunidades — evidência qualitativa, não amostra representativa
- [r/userexperience: criatividade e design systems](https://www.reddit.com/r/userexperience/comments/xurn7j): profissionais descrevem tanto restrições quanto ganhos de previsibilidade. Decisão: liberdade de composição dentro de contratos de interação.
- [r/UXDesign: dificuldades de workflow](https://www.reddit.com/r/UXDesign/comments/1p3rwxs/trying_to_understand_what_actually_makes_design/): aparecem custo de manutenção, atraso de evolução de componentes e valor de consistência. Decisão: poucos tokens semânticos, variantes e extensão local documentada.
- [r/UXDesign: handoff e componentes](https://www.reddit.com/r/UXDesign/comments/13he9rz): complexidade do próprio sistema pode criar obstáculos. Decisão: não transformar cada componente local em decisão de governança.
- [r/userexperience: exemplos que funcionaram](https://www.reddit.com/r/userexperience/comments/1r8umyh/examples_of_design_systems_that_worked/): relatos de times contornando sistemas lentos ou aceitando UX pior. Decisão: evitar bloqueio de inovação sem perder critérios verificáveis.

## Confronto entre domínios
| Cenário | Necessidade | Regra antiga que falha | Resposta v2 |
|---|---|---|---|
| IDE | múltiplas áreas, código e preview | uma ação por tela, densidade por plataforma | ações por região, compactação por entrada |
| Leitura e notas | conforto, fonte e papel | dark obrigatório, três tamanhos | temas e escala editorial |
| Gestão e planilha | dados densos e relações | proibição de bordas e cartões aninhados | tabelas, grades e superfícies funcionais |
| Mapas e criação | cores de dados, arte e interação | limite de acento e gradientes | paleta independente e liberdade de arte |
| Produto futuro cotidiano | acessibilidade e rapidez | navegação prescrita | adaptação a tarefa e janela |

## Decisões
Obrigatório: acessibilidade, semântica, estado, feedback, adaptação e evidência. Recomendado: valores e padrões de partida. Livre: identidade, cor, composição, densidade, tipografia de domínio. Preservar v1 para consumidores existentes; v2 no namespace global de tokens, sem reescrever aplicativos. Exibir temas claro/escuro, acentos e densidades na vitrine com exemplos não centrados em jogos. Não declarar que paletas demonstrativas são identidade de qualquer Product.

## Limites
Discussões são relatos, não consenso; não atribuir conclusões universais a amostras informais. Valores de referência são escolhas de implementação, sujeitos a testes dos pares reais e à identidade de cada Product. O contrato não exige que todos os Products implementem todos os exemplos; não constitui certificação WCAG.
