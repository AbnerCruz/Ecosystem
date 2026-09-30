# ADR-0006 — Identidade documental em sidecar e modelo de artefatos

- Status: Proposed (pendente de confirmação do proprietário — OD-10)
- Data: 2026-09-30
- Requisitos: REQ-013, REQ-014, REQ-039, REQ-041, REQ-042
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

IDs `doc_*` só persistem em `mapa.json`/journal; nota sem entrada recebe novo ID a cada carga (R-2). Metadados espaciais e regiões dependem de path (R-3). Todo arquivo textual vira documento (AUD-010) e as extensões divergem em cinco lugares.

## Forças

- arquivos do usuário permanecem dele (`app.js:1902-1907`);
- edição externa (Obsidian, sync, rename) é normal;
- compatibilidade com mapas 1.x por path.

## Alternativas consideradas

### A — ID em frontmatter

Portável, mas altera arquivos do usuário.

### B — Sidecar `.urbe/identity.json` (path↔ID + fingerprint) com reconciliação

Não toca arquivos; exige heurística de reconciliação e GC.

### C — Status quo

Mantém órfãos e perda de relações.

## Decisão

Recomendação: **B**. Sidecar `.urbe/identity.json` `{version, docs:{<id>:{path,fingerprint,seen}}}`; `syncFromDisk` reconcilia por path e, se ausente, por fingerprint (rename externo); referências internas novas usam ID e mantêm leitura por path/nome da 1.x; GC de órfãos em history/trash/compositions. **Modelo de artefatos:** fonte única `artifactTypes` (nota, página, plugin, tema, estilo, textura, composição, asset, sistema) com regras de caminho/extensão; `document.open` roteia por tipo; o DocumentStore só indexa tipos textuais editáveis como nota. Frontmatter opt-in fica ADIADO.

## Consequências positivas

- identidade resiliente a edição externa
- um único lugar define tipos e extensões

## Consequências negativas / trade-offs

- heurística de fingerprint pode errar em cópias idênticas
- novo arquivo no vault

## Migração

Ler sidecar ausente → gerar a partir de `mapa.notas[path].id`; nenhuma reescrita de notas.

## Validação

fixtures com rename externo, cópia de vault sem `.urbe/`, órfãos; teste do modelo de artefatos (G1).

## Relações

- documentos afetados: DATA-CATALOG §4, SPEC §5
