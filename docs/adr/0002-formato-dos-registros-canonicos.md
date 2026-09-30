# ADR-0002 — Formato de `ecosystem.json` e dos registros de governança

## Status

Proposto — ratificação pendente em DEC-0001. Vigora provisoriamente como fundação (MANIFEST §57).

## Contexto

MANIFEST §10 exige um manifesto canônico `ecosystem.json` legível por humanos e máquinas. NN-008, NN-021 e §23.1 exigem handoffs com schema e validação automática; §22.3 e §35 exigem que decisões pendentes do proprietário sejam descobríveis; §0.2 exige uma matriz de enforcement legível por máquina. MANIFEST §4.3 exige que contratos que atravessam processos sejam independentes de linguagem.

## Problema

Definir o formato desses registros e **uma única autoridade** para cada formato (NN-001), sem introduzir abstrações prematuras (NN-020).

## Opções

1. JSON + JSON Schema (2020-12) como contrato canônico de cada formato; validação no CI.
2. JSON validado apenas por código C# (tipos/records como definição do formato).
3. YAML/TOML com validação ad hoc.

## Decisão

Opção 1.

- Schemas em `docs/contracts/schemas/`: `ecosystem.schema.json`, `handoff.schema.json`, `decisions.schema.json`, `enforcement-matrix.schema.json`. Cada um é a **autoridade** do seu formato; cada arquivo de dados declara `$schema` apontando para ele.
- `ecosystem.json` (`schemaVersion: 1`):
  - `components` é um mapa **ID estável → componente**; o nome é apenas exibição (NN-019).
  - Tipos: os de MANIFEST §6 (`product`, `tool`, `service`, `library`, `workspace`, `adapter`, `contract`) mais `check` — verificação interna do repositório, que não é Tool no sentido do manifesto (Tool é capability com interface de usuário) e por isso não fica em `tools/`.
  - `status`: `planned` (não existe), `not-migrated` (existe fora do monorepo), `migrating`, `active`, `deprecated`, `archived`.
  - `source` e `version.authority` tornam explícito onde vive a autoridade de código/versão enquanto um produto não foi migrado (NN-001, NN-014).
  - Componentes compartilhados declaram `responsibility`, `contract`, `consumers`, `owners`, `compatibility`, `extractionReason` (NN-004, NN-022).
  - Campos de capabilities e permissões **não** foram adicionados: seu formato é trabalho da Fase 2.
- Decisões do proprietário: `docs/governance/decisions.json`, IDs `DEC-NNNN`.
- Handoffs: um arquivo por mensagem em `docs/governance/handoffs/<message_id>.json`, IDs `HO-AAAAMMDD-slug`.
- Matriz de enforcement: `docs/governance/enforcement-matrix.json`.
- Evolução incompatível de qualquer formato incrementa `schemaVersion` e exige ADR.

## Consequências

- Formatos consumíveis por qualquer linguagem (o Urbe em JavaScript, o Hub em C#).
- O check em C# valida contra os schemas; não mantém uma segunda definição dos formatos. Regras semânticas entre arquivos (boundaries, referências cruzadas) vivem no check, com ID próprio.
- Registros de handoff crescem com o tempo; retenção/arquivamento será decidido quando houver volume.

## Alternativas rejeitadas

- **Formato definido por código C#:** amarraria o contrato a uma linguagem, contrariando MANIFEST §4.3.
- **YAML/TOML:** ganho de legibilidade marginal, mais ambiguidade de parsing e sem vantagem de validação.

## Referências

MANIFEST §0.2, §4.3, §10, §22.3, §23.1, §35; NN-001, NN-004, NN-008, NN-014, NN-019, NN-021, NN-022.
