# ADR-0001 — Registro de decisões arquiteturais

## Status

Proposto — ratificação pendente em DEC-0001. Vigora provisoriamente como fundação (MANIFEST §57).

## Contexto

MANIFEST §29 exige ADRs para decisões estruturais e define as seções mínimas. NN-009 exige que decisões materiais sobrevivam à conversa em que surgiram; NN-011 exige que mudanças arquiteturais sejam explícitas e rastreáveis.

## Problema

Definir onde e em que formato ADRs vivem, como são numerados, quais estados possuem e como a conformidade é verificada.

## Opções

1. ADRs em Markdown no repositório (`docs/adr/NNNN-titulo.md`), verificados pelo CI.
2. ADRs como GitHub Issues/Discussions.
3. ADRs em ferramenta externa (wiki, Notion).

## Decisão

Opção 1.

- Local: `docs/adr/NNNN-titulo-em-kebab.md`; número de 4 dígitos, sequencial, nunca reutilizado (NN-019).
- Primeira linha: `# ADR-NNNN — Título`.
- Seções obrigatórias: `Status`, `Contexto`, `Problema`, `Opções`, `Decisão`, `Consequências`, `Alternativas rejeitadas` (MANIFEST §29).
- Estados: `Proposto`, `Aceito`, `Rejeitado`, `Substituído`, `Obsoleto`.
- Índice em `docs/adr/README.md`.
- Decisão que cabe ao proprietário: ADR `Proposto` + entrada `pending` em `docs/governance/decisions.json`; ao ser decidida, o ADR passa a `Aceito`/`Rejeitado` citando a DEC.
- Conformidade verificada por `CHK-ADR`.

## Consequências

- Decisões ficam versionadas junto do código que afetam, legíveis offline e por agentes, e o Hub pode lê-las do GitHub (MANIFEST §17).
- Custo: disciplina de escrever ADRs; o check impede ADRs incompletos.

## Alternativas rejeitadas

- **Issues/Discussions:** não versionados com o código, difíceis de validar no CI, dependentes de rede.
- **Ferramenta externa:** cria uma segunda autoridade fora do repositório (NN-001) e depende de conta/serviço.

## Referências

MANIFEST §24, §29, §57.10; NN-009, NN-011, NN-019.
