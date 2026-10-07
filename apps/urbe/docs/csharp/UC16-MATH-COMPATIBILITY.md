# UC-16 — Compatibilidade TeX e diagnósticos puros

> Estado: fatia independente de UC-16. Não escolhe renderer, não adiciona pacote e não altera o TeX do usuário.

## Fonte observada

A investigação integrada no PR #265 executou 210 entradas com CSharpMath e registrou 196 renderizações e 14 diagnósticos. O corpus real do tutorial ficou em 30/31; o caso real incompatível é `\\boxed`. Os snippets observados como incompatíveis incluem `\\dfrac`, `\\boldsymbol`, `\\lVert/\\rVert`, `\\overset`, `\\underset`, `\\boxed` e `\\cancel`. Declarações de macro como `\\newcommand`/`\\def` também não podem ser presumidas como suportadas.

Fonte: `math-renderer-observations.json` e `UC16-RENDERER-INVESTIGATION.md`.

## Contrato desta fatia

`MathCompatibility.Analyze(tex)`:

- opera somente sobre texto em memória;
- usa offsets UTF-16, iguais aos do editor;
- preserva a string original sem expansão, reescrita ou normalização;
- ignora comentários TeX e não confunde `\\\\` (quebra de linha) com início de comando;
- emite diagnósticos estáveis para comandos incompatíveis conhecidos;
- emite diagnóstico para declarações dinâmicas de macro;
- pode oferecer sugestão humana, mas nunca aplica alias automaticamente.

A ausência de diagnóstico **não** significa que todo TeX seja renderizável. A autoridade final de parse/render pertence ao renderer que vier a ser aprovado e integrado. Esta camada apenas transforma incompatibilidades já observadas em informação determinística para Core/UI.

## Por que não há aliases automáticos

Os candidatos não são equivalentes de forma geral:

- `\\dfrac → \\frac` pode mudar estilo inline/display;
- `\\boldsymbol → \\mathbf` não preserva todos os símbolos;
- `\\lVert/\\rVert` representam norma (barra dupla), não módulo;
- `\\overset`, `\\underset`, `\\boxed` e `\\cancel` precisam de semântica/layout próprios.

Portanto, REQ-007 (preservação) e a política de migração impedem “consertar” o TeX silenciosamente.

## Fora do escopo

- adotar CSharpMath ou qualquer dependência;
- renderização/SVG;
- expansão de macros;
- UI/editor;
- migração ou escrita de vault;
- declarar UC-16/G-C2 concluídos.


## Consumo pelo renderer

A fatia seguinte integra a análise ao `MathRenderer.RenderSvg` sem alterar a string
TeX nem o contrato posicional existente de `MathRenderResult`.

`MathRenderResult.CompatibilityDiagnostics` expõe os diagnósticos estruturados.
Se existir uma incompatibilidade `BlocksRendering`, o renderer retorna falha antes
de invocar CSharpMath; erros exclusivos do parser continuam com a lista estruturada
vazia, deixando explícita a diferença entre incompatibilidade conhecida e erro
genérico de parse/render.

Isso permite que UC-18 mostre posição, comando e sugestão sem analisar texto de erro.


## Macros configuradas

O cliente JS expõe `UrbeMath.setMacros(m)` e repassa o dicionário para KaTeX.
Não há consumidor interno nem teste legado para essa API, portanto UC-16 porta
somente o contrato útil e seguro observado: **macros de string em memória**.

`MathMacros.Expand(tex, macros)` e o parâmetro opcional `macros` de
`MathRenderer.RenderSvg` mantêm diferenças deliberadas em relação ao estado
global do JS:

- o dicionário é passado explicitamente por renderização; não existe estado global;
- o `MathRenderResult.Tex` e o `aria-label` preservam o TeX original;
- a expansão é usada apenas como entrada efêmera do renderer;
- nomes aceitos são comandos `\\[A-Za-z]+`;
- substituições suportam `#1` a `#9`, grupos balanceados e tokens simples;
- macros podem chamar outras macros, mas ciclos, profundidade, número de
  substituições, definições e tamanho expandido possuem limites;
- comentários TeX não são expandidos;
- declarações dinâmicas dentro da fórmula (`\\newcommand`, `\\def` etc.)
  continuam diagnóstico, não execução.

KaTeX também aceita formas avançadas de macro JavaScript. Elas não são portadas:
não há consumidor real observado e introduzir callbacks/estado global no Core
aumentaria a superfície sem requisito. Se um consumidor real aparecer, isso vira
novo caso verificável em vez de alargar silenciosamente este contrato.
