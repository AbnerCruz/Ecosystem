# Contrato do adaptador de persistência (REQ-028 · RM-F1-10)

Um adaptador liga o `WorkspacePersistence` (única autoridade de escrita do vault) a um armazenamento físico. Todos os métodos são assíncronos; `vault` é o nome da pasta/cidade dentro da raiz do adaptador; `path` é relativo ao vault, com `/`.

| Método | Semântica |
|---|---|
| `cities()` → `string[]` | vaults existentes na raiz (ordenados) |
| `createCity(name)` / `removeCity(name)` | cria (vazia) / apaga o vault inteiro (e o cache de anexos, no IDB) |
| `list(vault)` → `string[]` | **todos os arquivos** do vault (inclui `.urbe/**`; pastas vazias no IDB aparecem como `<pasta>/.pasta`; pastas ocultas fora de `.urbe` são ignoradas) |
| `read(vault, path)` → `string \| null` | texto UTF‑8; `null` se não existir |
| `readBlob(vault, path)` → `Blob \| null` | bytes; `null` se não existir |
| `write(vault, path, text)` / `writeBlob(vault, path, blob)` | cria pastas intermediárias; substitui o conteúdo |
| `remove(vault, path)` | apaga arquivo; **ausente = sucesso silencioso** |
| `createFolder(vault, path)` / `removeFolder(vault, path)` | cria (idempotente) / remove pasta **vazia** (falha silenciosa se não estiver vazia) |

## Implementações
- `adapters/idb.js` — modo interno do navegador (IndexedDB `knowledge-city`, loja `fs`, chave `<vault>/<path>`); também expõe `store` (configurações `kv` e cache `blobs`).
- `adapters/fsa.js` — pasta real via File System Access API **e** via os handles emulados pela ponte nativa (Windows/Android).
- `adapters/router.js` — escolhe o adaptador ativo (`pasta` × `interno`) e mantém os nomes em português do legado (`cidades`, `listar`, `ler`, `escrever`…) até o fim da migração de `app.js`.

## Conformidade
`tests/lib/adapter-contract-suite.mjs` é a suíte única; roda em Node contra o adaptador FSA sobre a ponte do Electron (`native/desktop/vault-fs.js` real) e no navegador contra IDB e FSA/OPFS (`tests/e2e/adapters.e2e.mjs`).
