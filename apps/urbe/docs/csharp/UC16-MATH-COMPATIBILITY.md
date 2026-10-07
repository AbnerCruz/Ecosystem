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
