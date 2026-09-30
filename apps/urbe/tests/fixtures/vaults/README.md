# Fixtures de vaults históricos

Geradas por `node tools/make-fixtures.mjs` (REQ-037). Cada pasta é um vault; `expect.json` lista os invariantes que `tests/vault-migration.mjs` verifica
(bytes das notas, IDs, arquivos de versão futura, recuperação de journal). `idb-legado-kv-cidade.json` é o formato v1 que só existia no IndexedDB (`kv["cidade"]`).

| Fixture | Cobre |
|---|---|
| v1-mapa-v2 | mapa v:2 (sem id, sem mundo/version), leitura defensiva |
| v1-mapa-v4 | mapa v4 completo (ids, regiões, construções/assets), history/trash/compositions v1 |
| v1-notas-sem-id | notas sem `id` no mapa (R-2: ID novo a cada carga) |
| v1-journal-pendente | journal.json v1 de uma operação interrompida (recuperação) |
| v1-cidades-mescladas | migração multi-cidade (`Cidades/`, `.urbe/origens/`, `merged-v1.json`) |
| v1-personalizacao | tema.json, temas, estilos, texturas, plugins |
| v1-paginas | páginas, modelos e blocos v1 |
| v1-mundo-antigo | mapa com `mundo` antigo (dispara reorganização na 1.8.2) |
| futuro-desconhecido | versões maiores em todos os formatos (proteção forward, R-1/R-14) |
