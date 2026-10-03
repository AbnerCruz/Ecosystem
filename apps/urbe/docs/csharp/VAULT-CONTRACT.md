# Contrato do vault para os clientes Urbe — UC-3

> Projeção gerada por `node tools/vault-contract.mjs render`. Autoridade única: DATA-CATALOG e ADR-0004, não este documento. Não cria formato nem altera decisão consolidada. Manifesto verificável: `acceptance/vault-manifest.json`.

## Regras de compatibilidade

| Área | Obrigação existente | Evidência da implementação atual |
|---|---|---|
| Vault | Sem vault.json = legado. formatVersion maior que 2 = vault somente leitura. Leitor não sobrescreve formato futuro. | src/persistence/vault-meta.js:5; src/persistence/workspace.js:54 |
| Mapa | Ausente v = legado; inteiros 1..4 aceitos; futuro/desconhecido preservado. Mapa v4 continua legível pela 1.x. | src/persistence/workspace.js:61 |
| Sidecars | v2 tem precedência; nomes v1 preservados durante beta. v2 ilegível/estranho desliga escrita daquele artefato; não apaga. | src/persistence/workspace.js:30 |
| Journal | Recuperação v2 antes de v1; somente versão suportada e documents array. Futuro preservado. Não confundir operação multi-arquivo com transação do SO. | src/persistence/workspace.js:68 |
| Migração | Captura originais antes de escrita; backup restaurável antes da migração; reabrir não duplica migração nem backup. | src/persistence/workspace.js:22; tests/vault-format.mjs |
| Backup | manifest.version=1; paths .urbe/ viram urbe/ na cópia; absent registra inexistentes. SHA-256 de texto UTF-8. size é quantidade de unidades UTF-16, NÃO bytes. | src/persistence/backup.js:21; src/persistence/backup.js:9 |
| Restauração | Cópia ausente ou hash divergente rejeita; absent removidos. Implementação restaura sequencialmente: rejeição pode ocorrer após arquivos anteriores serem gravados. Não alegar atomicidade total. | src/persistence/backup.js:34 |
| Identidade | Nenhum ID injetado nas notas; mapa e sidecar guardam IDs. Rename externo herda somente fingerprint único nos dois lados; vazio/ambíguo não herda. | src/persistence/identity.js:51; src/persistence/identity.js:39 |
| Fingerprint | CRLF/CR → LF; tira espaços/tabs finais e LF finais. Dois FNV de 32 bits sobre unidades UTF-16, wraparound; tamanho UTF-16 em base36. Não usar SHA nem code points em substituição. | src/persistence/identity.js:10; src/persistence/identity.js:12 |
| GC | Simulação padrão; ativo/lixeira nunca órfão; histórico órfão após 30 dias; lixeira nunca expira sem trashDays; composição fica, só referências removidas. Aplicação recusada em vault readonly. | src/persistence/gc.js:13; tests/gc.mjs |
| Exportação | urbe-export.json formatVersion=1, SHA-256 de bytes e size em bytes; journals excluídos; só preferências permitidas, nunca credenciais IA. Import futuro recusado. | src/persistence/export-manifest.js:35; tests/export-manifest.mjs |
| Arquivos | Textos carregados conforme classificação de artefatos; segmentos ocultos não viram notas. Binários preservados como bytes. Caminhos relativos ao vault com /; adapter inclui .urbe. | src/core/artifacts.js; docs/v2/contracts/persistence-adapter.md |

## Precisão e limites

FATO OBSERVADO: vault-meta.parse aceita qualquer número ≤2 como current; o modo readonly global é acionado para future, não para todo corrupt. identity corrupt é derivado e recriado. Estes detalhes não autorizam ampliar leitores nem reduzir a proteção normativa. Qualquer endurecimento que altere comportamento/compatibilidade exige item e revisão próprios.

DECISÃO CONSOLIDADA: ADR-0004 e DATA-CATALOG permanecem acima dos snapshots. Não há schema C# paralelo. O cliente futuro deve executar o mesmo corpus; datas e IDs gerados devem ser testados por invariantes declaradas, nunca mascarados por normalização geral.

FATO OBSERVADO: aprovação de plugins, configurações, cache/handles, conversas e chaves IA pertencem ao aparelho e têm regras distintas de portabilidade. Um vault físico não contém todo o estado IndexedDB. UC-6 deverá tratar migração desses estados, sem exportar segredos.

## Manifesto de fixtures

Cada um dos 12 diretórios tem ID estável, expectativa, caso UC-2 e todos os arquivos (inclusive .urbe e binários) com tamanho em bytes e SHA-256. A expectativa é evidência de teste, não schema normativo. O oráculo inicial continua imutável.

| Fixture | Caso | Arquivos |
|---|---|---|
| futuro-desconhecido | vault-futuro-desconhecido | 9 |
| futuro-v2 | vault-futuro-v2 | 9 |
| v1-cidades-mescladas | vault-v1-cidades-mescladas | 7 |
| v1-journal-pendente | vault-v1-journal-pendente | 4 |
| v1-mapa-v2 | vault-v1-mapa-v2 | 4 |
| v1-mapa-v4 | vault-v1-mapa-v4 | 9 |
| v1-mundo-antigo | vault-v1-mundo-antigo | 4 |
| v1-notas-sem-id | vault-v1-notas-sem-id | 4 |
| v1-orfaos | vault-v1-orfaos | 7 |
| v1-paginas | vault-v1-paginas | 5 |
| v1-personalizacao | vault-v1-personalizacao | 8 |
| vault-futuro | vault-vault-futuro | 4 |

## Projeção literal das regras consolidadas do catálogo

As seções abaixo são extraídas da fonte; qualquer mudança na fonte exige regeneração e revisão do diff. O catálogo contém descoberta histórica e atualizações: a política consolidada da seção 9 prevalece sobre propostas antigas da seção 7 e lacunas históricas da seção 8. Não tratar a seção 8 como novo bloqueio nem copiar uma proposta como decisão.

## 4. Identidade e DocumentStore
- [F] ID `doc_<uuid>` (`documents.js:8-11`), atribuído em `make`; `upsert` reutiliza o ID pelo índice de path em minúsculas (`documents.js:61`); rename/move preservam ID (`explorer/operations.js`); lixeira restaura com o mesmo ID (`trash.js:7`).
- [F] O ID só sobrevive entre sessões por: `mapa.notas[path].id`, `journal.documents[].id` e referências em history/trash/compositions. Nota **sem entrada no mapa recebe novo ID a cada carga** (`workspace.js:16`); fallback sem persistência usa `id:rel` (`app.js:2127`).
- [F] Título vem do path (`titleFromPath`, `documents.js:12-15`); índice de path case-insensitive.
- [F] Links `[[alvo]]` textuais resolvidos por título/path (`knowledge-index.js:12-17`); **nenhum código reescreve links ao renomear**.
- [F] Metadados espaciais por path: `WorldProjection.spatial` `Map<path,…>` (`projection.js:6-10,18-22`), migrado em `document:updated`; `regions` por `caminho`. `projection.metadata()` (que gera `notas` com `id`) **não é chamado em produção** (só teste); a escrita real vem de `estadoDesejado` (`app.js:4469`, com `b.documentId`).
- [F] Fora do mapa o mundo usa IDs voláteis por sessão (`id('b')`, `id('r')`, `app.js:28`); `building.documentId` liga casa a documento (`app.js:4456-4467, 4599-4602`).
- [C] **Atualização RM-F1-14 (REQ-041):** `regioes[].id` (`reg_…`), `regioes[].parentId`, `construcoes[].id` (`ast_…`) e `construcoes[].parentId` (ID do documento da nota dona) são gravados no mapa v4, de forma aditiva: `parentNoteName`, `caminho`, `nome` e a geometria continuam lá para a 1.x ler. Mapas sem ID recebem IDs determinísticos na abertura (`src/world/stable-ids.js`, hash FNV de `caminho` / `relPath`), então duas aberturas antes do primeiro save dão os mesmos IDs; eles são persistidos no primeiro save (depois do backup de migração). O vínculo asset → nota é resolvido por `parentId` e, na falta dele, por `parentNoteName`. No mundo em memória o ID estável fica em `uid` (o `id` volátil de sessão continua existindo). Rename de pasta dentro do app mantém o ID; rename **externo** depende da reconciliação de RM-F1-15. Testes: `tests/stable-ids.mjs`, `tests/e2e/stable-ids.e2e.mjs`.
- [C] **Atualização RM-F1-15 (REQ-042):** `.urbe/identity.json` `{version:1,docs:{<docId>:{path,fingerprint,seen}}}` é gravado pelo `WorkspacePersistence` (`src/persistence/identity.js`); nada é escrito nas notas. Fingerprint = hash do conteúdo normalizado (CRLF, espaços finais, linhas vazias finais); conteúdo vazio não participa. **No load:** nota sem ID no mapa recebe o do sidecar pelo mesmo caminho; se ainda faltar, herda o ID de uma entrada cujo caminho sumiu **e** cujo fingerprint é único dos dois lados. Cópias idênticas são ambíguas: ninguém herda, e o evento `workspace:reconciled` informa. A casa guarda a posição quando a nota fica no mesmo bairro (rename simples ou pasta inteira renomeada). Pasta renomeada por fora mantém `reg_`, forma e nome novo, e os assets (`ast_`) passam a apontar para o caminho novo. Movida para outra pasta, a nota mantém ID e metadados e ganha lugar no bairro novo; sem isso a camada do mundo reverteria o move. **Com o app aberto:** `syncFromDisk` trata "sumiu + apareceu com o mesmo conteúdo" como rename (`source:'disk.rename'`). A camada legada monta a cidade a partir do mapa já reconciliado (`persistence.meta`), não do disco. Versão maior do sidecar é preservada (`sideReadonly.identity`); sidecar ilegível é recriado, porque é derivado. Testes: `tests/identity.mjs`, `tests/e2e/identity.e2e.mjs`.
- [C] **Atualização RM-F1-16 (REQ-042):** GC de órfãos (`src/persistence/gc.js`, comando `workspace.gc`; a versão com diálogo é `workspace.cleanOrphans`). Documento vivo **ou na lixeira** nunca é órfão. O histórico órfão só sai após `orphanDays` (padrão 30) sem mudança. A lixeira só expira com `trashDays` explícito (padrão: nunca). Composições nunca são apagadas; só perdem as fontes que não existem mais. O comando roda em simulação por padrão. Aplicado, passa pela migração com backup se o vault ainda for 1.x e registra em `vault.json.maintenance`. É recusado em vault somente leitura. Fixture `v1-orfaos`; testes `tests/gc.mjs` e `tests/e2e/gc.e2e.mjs`.
- **Referências por path/nome:** `tema.json` (estilos, tema, texturas), aprovação de plugins, `mapa.regioes[].caminho`, `construcoes[].caminho/files[].relPath/folderPath/parentNoteName`.
- **Referências por docId:** compositions (`sources`, `order`), history, trash, `editor.workspace` (localStorage global), `buildings[].documentId`.
- [F] **Tipos de artefato: não existem.** `load` trata todo arquivo textual como documento (`workspace.js:9`); tipo decidido por consumidores via path/extensão/`kind`.


## 7. Contrato de compatibilidade 2.x [C — ADR-0004]
1. **Leitura:** a 2.x lê todos os formatos da 1.x (mapa v1/v2/v4, sem `id`, journals v1, history/trash/compositions v1, páginas v1, tema v1).
2. **Escrita:** a 2.x escreve o formato 2.x com `formatVersion` no vault (`.urbe/vault.json`) e **proteção forward** para artefatos desconhecidos (nunca sobrescrever/descartar).
3. **Backup:** toda migração de formato gera backup restaurável antes de escrever (`.urbe/backup/…`), é idempotente e reversível por restauração.
4. **1.x abrindo 2.x:** objetivo é não corrompê-lo. [I] A 1.8.2 já em produção **não pode ser corrigida retroativamente**; a 2.x deve continuar escrevendo o subconjunto que a 1.x preserva (arquivos `.md`, `mapa.json` legível) e manter novos formatos em arquivos que a 1.x ignora (novos nomes em `.urbe/`), exceto as falhas R-1 que exigem novos nomes de arquivo para history/trash/compositions **[P]**: gravar `.urbe/history.v2.json` etc., mantendo o v1 intacto durante a transição. **Pergunta aberta OD-04.**


## 8. Lacunas para SPEC
Política forward/backward exata por arquivo (§7.4); estrutura de `vault.json`; fixtures (REQ-037); manifesto do export (REQ-044); GC de órfãos; identidade de assets/plugins/temas; formato do sidecar de identidade (REQ-042).


## 9. Política de migração por formato (contrato 2.x — REQ-023, REQ-035, ADR-0004)

Regras comuns: (1) a 2.x **lê** o formato 1.x; (2) só **escreve** o formato 2.x; (3) versão desconhecida/maior ⇒ **preservar o arquivo intacto, avisar e operar em modo seguro (somente leitura) para aquele artefato**; (4) toda migração faz backup em `.urbe/backup/` antes de escrever e é idempotente; (5) `tools/check-catalog.mjs` exige que todo caminho persistido pelo código apareça nesta tabela.

| Caminho | Versão 1.x | Versão/arquivo na 2.x | Leitura 2.x | Escrita 2.x | Versão desconhecida | Fixture / teste |
|---|---|---|---|---|---|---|
| `.urbe/mapa.json` | `v:4` (lê v1/v2/v4) | mesmo nome, `v:4` aditivo (`id` de região/asset em `regioes[]`/`construcoes[]`, `mundo`, `version`) | v1, v2, v4 | v4 aditivo (legível pela 1.x) | `v>4`: preservar, abrir sem gravar mapa (layout derivado dos caminhos) | `v1-mapa-v2`, `v1-mapa-v4`, `futuro-desconhecido` |
| `.urbe/journal.json` | `version:1` | `.urbe/journal.v2.json` (`version:2`) | v1 (recupera e remove só se `version===1`) e v2 | só v2 | preservar (não apagar) | `v1-journal-pendente`, `futuro-desconhecido` |
| `.urbe/history.json` | `version:1` | `.urbe/history.v2.json` (`version:2`) | v1 (importa na 1ª carga) e v2 | só v2; v1 permanece intacto | preservar | `v1-mapa-v4`, `futuro-desconhecido` |
| `.urbe/trash.json` | `version:1` | `.urbe/trash.v2.json` (`version:2`) | v1 e v2 | só v2; v1 intacto | preservar | `v1-mapa-v4`, `futuro-desconhecido` |
| `.urbe/compositions.json` | `version:1` | `.urbe/compositions.v2.json` (`version:2`) | v1 e v2 | só v2; v1 intacto | preservar | `v1-mapa-v4`, `futuro-desconhecido` |
| `.urbe/tutorial.json` | `{versao,em}` | mesmo nome (aditivo) | sim | sim | preservar | `tests/tutorial.mjs` |
| `.urbe/merged-v1.json`, `.urbe/origens/*` | marcador da migração multi-cidade | mesmo nome e formato (`{<cidade>:{folder,importedAt}}`), + campo aditivo `pendente` enquanto o mapa não foi fundido; `vault.json.migrations` registra `{id:'multi-city',sources}`; `vault.json.archivedCities` esconde da lista (RM-F1-19) | sim | nunca apaga origem | preservar | `v1-cidades-mescladas`, `tests/multi-city.mjs` |
| `.urbe/vault.json` | — | novo: `{formatVersion,createdBy,lastWriter,migrations[],maintenance[],archivedCities?}` (`maintenance`: últimas 20 limpezas `{kind:'gc',at,removed,orphanDays,trashDays}`, RM-F1-16) | sim | sim | `formatVersion` maior: modo seguro | `vault.mjs` (RM-F1-04) |
| `.urbe/identity.json` | — | novo: `{version:1,docs:{<docId>:{path,fingerprint,seen}}}` | sim | sim | preservar | RM-F1-15 |
| `.urbe/backup/<data>-<de>-<para>/` | — | novo: cópias restauráveis pré-migração | sim | sim | — | RM-F1-07 |
| `urbe-export.json` (dentro do ZIP) | — | novo: manifesto do export | sim | sim | recusar import de `format` desconhecido | RM-F1-18 |
| `Personalização/tema.json` | `versao:1` | mesmo | v1 | v1 (preserva chaves desconhecidas) | preservar; modo seguro de tema | `v1-personalizacao`, `futuro-desconhecido` |
| `Personalização/{temas,estilos,texturas,plugins}/*` | sem versão | mesmo | sim | sim | preservar | `v1-personalizacao` |
| `Páginas/**/*.page.json` (+ Modelos/Blocos) | `version:1` | mesmo; preserva chaves desconhecidas e `version` | v1 | v1 sem reescrever `version` | preservar (somente leitura no Studio) | `v1-paginas`, `futuro-desconhecido` |
| Notas e demais textos | — | sem mudança (nenhum ID no arquivo) | sim | bytes preservados | — | todas as fixtures |

