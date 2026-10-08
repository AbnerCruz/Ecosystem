# ADR-0030 — Documento canônico e autoria textual da matemática temporal

## Status

Aceito — fundação ratificada pela DEC-0040, alternativa A, junto à constituição de `math-authoring`.

## Contexto

O editor visual e a autoria textual devem editar um mesmo modelo semântico
serializável, incluindo cenas, dependências, apresentação, tempo e assets.
C# arbitrário não tem round-trip visual lossless viável. O primeiro slice precisa
ser seguro, local, depurável e testável antes de uma linguagem autoral sofisticada.

## Problema

Escolher uma representação inicial concreta sem duas fontes de verdade,
execução arbitrária em imports, perda de dados ou promessa de preservação de
comentários/estilo que o formato não representa. Manter extensão futura sem
publicar prematuramente contrato do Ecosystem.

## Opções

| Opção | Benefício | Custo/risco |
|---|---|---|
| A — JSON versionado tipado, AST explícita, parser/formatter C# | BCL offline, inspeção/diff, dados seguros, único modelo, bundle pequeno | Verboso para power users; spans e diagnósticos exigem trabalho; não tem comentários |
| B — DSL declarativa própria como fonte textual | Autoria curta e próxima de relações matemáticas; comments/spans podem ser representáveis | Parser/CST/formatter, edição visual e migração aumentam custo antes do slice; requer decisão de gramática |
| C — Subconjunto declarativo de C# via Roslyn | Familiar a usuários avançados e futura extensão | Dependência estrutural, bundle/Android/AOT e distinção entre dados/código; parse não basta para provar segurança/representabilidade |
| D — C# arbitrário como arquivo autoral | Poder expressivo | Execução não confiável; visual não representa todo código; não há round-trip seguro geral |

## Decisão

**Decisão aceita: A** para a v1. JSON é a visão textual direta de
`ProjectDocument`; AST tipada é a representação de expressão no modelo, não uma
segunda string autoritativa. Um campo de expressão digitada serve como rascunho
até parse/commit e o formatter deriva texto da AST. Não publicar DSL definitiva.
A escolha A/B de identidade da DEC-0040 inclui esta fundação, explicitamente
descrita nas consequências. Uma futura DSL exige novo ADR e usa o mesmo modelo.

Contrato local evolutivo: schemaVersion, IDs estáveis, nós com kind/version,
AST/data, estilos/transforms, bindings tipados, câmera, timeline e catálogo de
assets. Parser rejeita chaves duplicadas, números não finitos, tipos/refs inválidos,
versão futura, limites excedidos e caminhos inseguros antes de publicar revisão.
Nós desconhecidos ou extensões futuras falham de forma explícita; nunca descartá-los
e sobrescrever o original silenciosamente. Migração opera sobre cópia validada.

Formatter determinístico ordena propriedades/coleções semânticas por regra
documentada; arrays cuja ordem é semântica não são ordenados por conveniência.
Números usam cultura invariável. Round-trip é semântico, não byte a byte de
whitespace, ordem não semântica ou comentários inexistentes em JSON. Metadata
autoral representável é persistida, nunca deduzida por heurística da UI.

Operações visuais e texto válido passam por validação/transações do mesmo
controlador. Rascunho textual tem revisão-base; alteração visual concorrente
exige rebase/descartar explícito. Parse inválido não altera a revisão publicada.
Undo/redo registra transações validadas, incluindo mudanças vindas das duas visões.

## Consequências

- MA-002 pode provar o modelo único com custo menor antes de uma DSL.
- Edição textual inicial é mais verbosa; quick-create visual e entrada de
  expressão familiar reduzem atrito sem prometer um sistema de código separado.
- JSON/AST/parser permanecem dentro do Product; não são capability/SDK global.
- Imports nunca executam C#, shell, scripts de asset ou código sugerido por IA.
- API C# futura é extensão com trust model separado e fronteira explícita de
  representabilidade; não pode ser convertida silenciosamente em documento visual.
- Projeto portátil proposto: manifesto JSON e assets por hash em pacote local,
  com save/backup/recovery transacional; layout físico detalhado e migrations
  testadas em MA-003 antes de aceitar dados de usuários.

## Alternativas rejeitadas

B/C são adiadas até necessidade/evidência e novo ADR; D é incompatível com o
aceite e a segurança. Também rejeitados dois modelos visual/código sincronizados
por heurística, equações como strings sem AST, fórmulas como screenshots e
geração de formatos paralelos exclusivos de IA.

## Referências

MANIFEST NN-001/005/009/011/016/018; ADR-0011/0029;
[SPEC](../proposals/math-studio/SPEC.md),
[plano técnico](../proposals/math-studio/IMPLEMENTATION.md),
[roadmap](../proposals/math-studio/ROADMAP.md).
