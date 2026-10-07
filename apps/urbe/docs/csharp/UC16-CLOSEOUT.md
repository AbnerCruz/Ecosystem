# UC-16 / Gate G-C2 — Closeout do domínio matemático

## Resultado

UC-16 fecha o domínio matemático do Urbe em C# sem UI e sem interop JavaScript.

Estado canônico após este closeout:

- edição/scanner/catálogo/snippets/autocomplete: PR #258;
- investigação reproduzível/offline/licenças: PR #265;
- renderer C# + backend SVG próprio: PR crítico #274;
- compatibilidade TeX: PR #275;
- diagnósticos estruturados integrados ao renderer: PR #276;
- macros de string limitadas: PR #277 / main `680e6db6`;
- aceite integrado de domínio: `MathDomainAcceptanceTests`.

## Aceite do G-C2

A suíte final exige, sem UI:

1. scanner e catálogo matemático utilizáveis no mesmo domínio;
2. incompatibilidades conhecidas com offsets UTF-16 e sem reescrita;
3. macros explícitas por renderização, preservando o TeX original;
4. corpus real do tutorial exatamente no piso observado de 30 renderizações + 1 diagnóstico;
5. duas renderizações idênticas para a mesma entrada;
6. SVG autocontido sem script/text/href externo;
7. geometria não vazia, dimensões finitas e `viewBox` coerente com as dimensões do resultado;
8. declarações dinâmicas de macro diagnosticadas, nunca executadas.

Isso prova o domínio sem UI. Não prova aparência final, toque, layout do editor, acessibilidade de tela ou instalação; esses itens pertencem respectivamente a UC-18/G-C3 e M4/G-C4.

## Compatibilidade visual

A investigação usou KaTeX apenas como autoridade legada para corpus e comportamento observável. G-C2 não transforma comparação visual humana em teste de domínio.

O que é verificável sem UI fica congelado agora: TeX preservado, saída geométrica SVG determinística, bounds coerentes, corpus real e diagnósticos. A inspeção visual do editor/render final será feita em UC-18/G-C3, como exige NN-017.

## Pendências deliberadamente posteriores

- UC-17: shell/navegação;
- UC-18: editor visual/fonte e Explorer, incluindo consumo visual do renderer;
- UC-27/G-C4: avisos/licenças de terceiros nos artefatos distribuídos;
- UC-31/G-C5: corte do cliente distribuído para C#.

Nenhuma dessas pendências reabre o domínio matemático de M2.
