# Catálogo de dados persistidos — Urbe 1.8.2-beta

> Descoberta da issue #33 (AUD-010, AUD-011, AUD-015; REQ-023). **[F]** fato (caminho:linha), **[I]** inferência, **[P]** proposta, **[C]** consolidada.
> Método: leitura estática de `main@91de0f4`. Linhas de `app.js` aproximadas.

## 0. Correções e ampliações ao AUDIT-1X
- [F] `src/legacy/bootstrap.js` é só JSZip (ver LEGACY-MAP L0). A "camada legacy" de dados está em `app.js`: `FS`, `Disco`, `DBK`, `estadoDesejado`, `abrirCidade`, `migrarAntiga`, `urbeEnsureSingleVault`.
- [F] `src/persistence/workspace.js` (71 linhas) é a única recuperação/migração em `src/persistence` (metadados + journal).
- [F] **Não existe diretório de fixtures.** Testes montam vaults inline com `Map`/`vm` (`persistence-world`, `recovery-history`, `integration-runtime`, `app-loader`, `trash`, `tutorial`). Nenhum teste cobre mapa v1/v2 nem `.urbe/*.json` de versão futura.
- [F] **`mapa.v` nunca é lido** (escrito 1, 2 ou 4). O único gatilho real é `mapa.mundo !== 'placas-1'` (`app.js:3230`; `URBE_MUNDO` `app.js:5226`).
- [F] **Dois escritores de `.urbe/mapa.json`:** `rodarSinc` (`app.js:2536-2543`) e `WorkspacePersistence.flush` (`workspace.js:23,34`); conciliação via `estadoDesejado` (`app.js:4469-4487`). O journal só protege o caminho do `WorkspacePersistence`. [I] risco de corrida/dupla escrita.
- [F] Quatro camadas de `estadoDesejado`: `app.js:2041` (v:2), `2517` (v:4), `3226` (final: `version`, `mundo`, `binarios`), `3395` (estabiliza `salvo`), `4469` (reescreve `notas` do DocumentStore).
- [C] **Atualização RM-F1-13 (REQ-040):** `WorkspacePersistence` é o único escritor de `.urbe/mapa.json` no uso normal. `rodarSinc` grava só pastas e binários e agenda/força o `flush`; o mapa vem de `persistence.metadataProvider` (a cadeia `estadoDesejado`), que devolve `null` enquanto a cidade carrega (fica o mapa lido do disco). `mapa.v` é validado no load: ausente = legado (v1); 1..4 = 1.x; `>4` = futuro; qualquer outro valor = desconhecido. Futuro e desconhecido são preservados (`mapaReadonly`). As duas declarações mortas de `estadoDesejado` (v:2 e v:4) foram apagadas. Exceção remanescente: a migração multi-cidade `urbeEnsureSingleVault` grava o mapa do vault `Urbe` antes do `load`; ela entra em RM-F1-19. Testes: `tests/e2e/map-writer.e2e.mjs` (pilha de toda gravação do mapa passa por `workspace.js`) e `tests/vault-format.mjs` §6b/6c.

## 1. Formatos do vault (todas as plataformas)

Acesso: `FS` (`app.js:1943-2015`) sobre 3 backends; `WorkspacePersistence` recebe `FS` como adapter (`app.js:~2024`).

| Artefato | Caminho | Schema | Escritor → Leitor | Versão / migração hoje |
|---|---|---|---|---|
| Notas/textuais | `**/*.{md,markdown,txt,html,htm,js,mjs,css,json,yaml,yml,csv}` fora de segmentos iniciados por `.` | texto; frontmatter `chave: valor` de uma linha (`documents.js:16-27`); sem ID no arquivo | `flush` (`workspace.js:34`), `rodarSinc` (`app.js:2540`) → `load` (`workspace.js:9-16`), `syncFromDisk` | nenhuma; conteúdo do usuário nunca é injetado (`app.js:1902-1907`) |
| `.urbe/mapa.json` | vault | v4: `{v,app,version,mundo,salvo,camera,regioes[],notas{<path>:{id,x,y,sprite,tags,anexos,criado,modificado,aiLocal}},construcoes[]}` (`app.js:3226`,`4469-4487`) | `app.js`, `workspace.js:23` → `workspace.js:10`, `app.js:3228`, `projection.js:7-10` | `v` escrito e não lido; leitura defensiva; `mundo` dispara `urbeReorganizarCidade` (`app.js:3230,5382`) |
| `.urbe/journal.json` | vault | `{version:1,timestamp,documents[{id,path,content,tags,created,modified}],metadata,trash,history,compositions}` (`workspace.js:33`) | só em operação multi-arquivo (`workspace.js:32`); removido após flush (`:36`) → `workspace.js:11,18` | `version===1`; outra versão é ignorada e **apagada** (R-1) |
| `.urbe/history.json` | vault | `{version:1,documents:{<docId>:[{timestamp,path,content,removed?}]}}`, 40/doc, janela 5 s (`history.js:4-10`) | `workspace.js:23` → `history.import` | `version===1`, senão vazio e **sobrescrito** |
| `.urbe/trash.json` | vault | `{version:1,items:[{document:{id,path,title,content,properties,tags,links,created,modified,revision},deletedAt,originalPath}]}` (`trash.js:6,11`) | idem | `import` não confere versão; sem TTL/purga |
| `.urbe/compositions.json` | vault | `{version:1,items:[{id:'cmp_…',name,type,sources[docId],theme,styles,overrides,customCSS,htmlSource,order[docId],created,modified}]}` (`composition/store.js:1`) | idem | `version===1`, senão `[]` e **sobrescrito** |
| `.urbe/tutorial.json` | vault | `{versao,em}` (`tutorial.js:10,28`) | `adapter.write` direto | marca do Tutorial |
| `.urbe/merged-v1.json` | vault | `{<cidade>:{folder,importedAt}}` (`app.js:4580,4613`) | `urbeEnsureSingleVault` | marcador da migração multi-cidade |
| `.urbe/origens/<cidade>.json` | vault | cópia do `mapa.json` antigo | idem (`app.js:4606`) | backup de origem |
| Binários/assets | fora da lista de texto, `.urbe/` e dotdirs | bytes; metadados em `mapa.construcoes[].files[]:{nome,tipo,mime,relPath,folderPath,tamanho,cacheId}` | `rodarSinc` (`app.js:2541`, `FS.escreverBlob` 2497-2503); assinatura `rel|tamanho|cacheId` (2535) | `WorkspacePersistence` não gerencia binários |
| `<pasta>/.pasta` | só IDB | marcador de pasta vazia (`app.js:2004`) | `FS.criarPasta` | — |
| `Personalização/tema.json` | vault | `{"$schema":"urbe-tema-1",versao:1,tema,cores,texto,forma,animacoes,editor,cidade{…texturas,paleta},estilos{<path.css>:bool},css}` só diferenças do padrão (`customize.js:14,210,282`) | customize → customize (`normalize` com erros/avisos) | `$schema`/`versao` fixos; referencia arquivos **por path** |
| `Personalização/temas/*.json` | vault | `{nome,claro,base,cores}` | usuário/IA | sem versão |
| `Personalização/estilos/*.css` | vault | CSS livre ligado por path em `tema.json.estilos` | idem | — |
| `Personalização/texturas/*.json` | vault | `{nome,chao:{},construcoes:{}}` (`panel.js:126`) | idem | sem versão |
| `Personalização/plugins/*.js` | vault | `urbe.plugin({nome,versao,descricao,ligar,desligar})` via `new Function` (`plugins.js:97`) | usuário/IA → `plugins.js` | aprovação por hash no aparelho (§2) |
| `Páginas/**/*.page.json` | vault | `{version:1,kind:'urbe-page',meta,theme,layout,sections[{id,type,props,style}]}`; imagens como data-URL (`pages/engine.js:9,376,395`, `studio.js:407`) | `studio.js:544,568`, IA | `normalize` **força `version:VERSION`** e descarta chaves desconhecidas com aviso (`engine.js:376-388`) |
| `Páginas/Modelos/*.template.json`, `Páginas/Blocos/*.block.json` | vault | `kind:'urbe-template'`/`'urbe-block'` (`studio.js:522,192`) | `studio.js` | sem versão em blocos |
| Livros | dentro de `.page.json` (`layout.format:'book'`, `engine.js:334`) | — | — | — |
| HTML exportado | download (`studio.js:493`, `composition/ui.js:23`) | embute JS que grava `localStorage["urbe-page-theme"]` (`engine.js:578-579`) | — | — |
| `Tutorial/**` | vault | notas geradas de `src/tutorial/content.js` | `tutorial.js` | versionada por hash (`C.version`) |
| `Urbe-vault.zip` | download | todo o vault físico exceto `.urbe/journal.json` (`app.js:1735-1752`) | JSZip | sem manifesto nem versão de formato |

**Extensões textuais divergentes** [F]: `workspace.js:9,45` (sem `.canvas`), `app.js:3105-3106`, `app.js:4472`, `ai/tools.js:39`, `native/bridge.js:44` (único com `.canvas`, só no espelho). [I] `.canvas` do Obsidian vira asset binário no app e texto no espelho nativo. → REQ-039.

### 1.1 Forma do mapa e referências
- [F] Regiões identificadas por `caminho` (path). Construções (assets) **sem ID**: identidade = `name`/`caminho`/`x` (dedup `app.js:4609`); pai por **nome** (`parentNoteName`, `app.js:3226`).
- [F] Formas antigas ainda lidas: v1 `{v:1,camera,regions,buildings}` só existia em IDB `kv["cidade"]` (`app.js:1573-1585, 2291-2309`); v2 (`app.js:2061`) e v4 (`2531`) usam os mesmos nomes. `notas[path].id` ausente é aceito (`workspace.js:16`, `id:m.id||null`).
- [F] `salvo` é congelado enquanto o resto do mapa não muda (`app.js:3390-3408`).

## 2. Estado local do aparelho (fora do vault)

Storage isolado por origem: web (host), Electron `app://urbe` (`main.js:14`), Android `https://localhost` (`capacitor.config.json`). [I] Migrar entre plataformas só funciona via arquivos do vault.

**localStorage**
| Chave | Conteúdo | Escritor → Leitor | Por vault? |
|---|---|---|---|
| `urbe.aparencia.v1` | `{vars,attrs,scheme}` (cache do tema) | `customize.js:~152` → `index.html:26` | não |
| `urbe.modoSeguro` | `'1'` | `customize.js:143,271` | não |
| `urbe.plugins.v1` | `{"<vault>::<path>":{hash:'sha256:…'|'fnv:…',em}}` (aprovação) | `plugins.js:19-25,135-136` | chave `vault::path` (vault sempre `'Urbe'` na 1.8) |
| `urbe.plugin.<vault>.<id>.<k>` | JSON livre do plugin | `plugins.js:43,79-80` | sim |
| `urbe.editor.workspace.v1` | `{tabs[{id,pinned}],activeId,back[docId],forward[docId]}` (≤80) | `editor/workspace.js:4-7` | **não; guarda docIds** |
| `urbe.explorer.v2` | `{favorites,recent,expanded}` | `explorer/model.js:9,13-14` | não |
| `urbe.tutorial.v1::<vault>` | versão | `tutorial.js:13` | sim |
| `urbe.tip.<k>`, `urbe.touchDebug` | flags | `ui/tips.js:7-8`, `ui/touch-debug.js:8` | não |

**IndexedDB `knowledge-city` v3** (`app.js:1521-1528`)
- `kv`: `ultimaCidade`, `pastaRaiz` (handle FSA, só web), `nativoAcessoRecusado`, `cidade` (v1 legado, consumido e apagado por `migrarAntiga`), chaves só-leitura da IA legada `urbe.ai.config.v1`, `urbe.ai.global.v21` (`ai/store.js:38`).
- `fs`: vault interno para navegadores sem FSA (`<cidade>/<rel>`; texto `String`, binário `Blob`; `app.js:1979-2013`) — **único dado do usuário fora de arquivo real**; some se o site perder dados (aviso `app.js:2222-2226`).
- `blobs`: cache offline de anexos `<cidade>|asset_<id>` (`cacheAssetKey`, `app.js:1596`).

**IndexedDB `urbe-ai` v1** (`ai/store.js:6-22`): `kv["config"]` `{version:1,providers[{id,preset,name,baseUrl,apiKey,kind?}],providerId,model,policy,agentId,instructions,memory,customAgents,maxSteps,modelCache}` e `conversations` `{id,title,…,vault,messages,runs,usage}` (`ai/ui.js:37`). **`apiKey` em texto puro** (`ai/ui.js:370-380`); contrato "nunca vai ao vault" (`store.js:3-5`).

**Service Worker** (`sw.js:1,113,127-128`): cache `urbe-shell-v<versão>` (~100 URLs + PDF.js opcional); rede-primeiro com timeout 4 s; limpa caches `urbe-shell-*` antigos.
**OPFS/sessionStorage/cookies:** [F] não usados.

## 3. Configuração por plataforma
- **Web/PWA:** vault por FSA (`showDirectoryPicker`, id `urbe-vaults`) ou IDB `fs`; um vault lógico `Urbe`, com `Cidades/<nome>` para cidades antigas (`app.js:4577, 5612`).
- **Electron:** `userData/config.json` `{vault:<abs>}` (`main.js:21-26`, padrão `Documentos/Urbe`); escrita atômica tmp+rename (`vault-fs.js:49-56`); chokidar ignora dot-paths e `.urbe-tmp-` (`main.js:36`).
- **Android:** `Documents/Urbe` via Capacitor Filesystem (`bridge.js:~226`, `UrbeAndroidPlugin.java:184`); exige `MANAGE_EXTERNAL_STORAGE` em SDK≥30; espelho lê texto ≤4 MB (`bridge.js:52`); vault único `Urbe` (`bridge.js:117-121`).
- [F] Não há schema de configuração global além de `tema.json` (vault) e `config.json` (Electron).

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

## 5. Migrações existentes
| Migração | Onde | Gatilho | Comportamento | Testes |
|---|---|---|---|---|
| Journal (crash) | `workspace.js:18-20` | `journal.json` `version===1` | documentos do journal **substituem** o disco; grava tudo; apaga journal | `recovery-history`, `integration-runtime:23-24`, `native-bridge:98-100` |
| Cidade única v1 → vault | `migrarAntiga` (`app.js:2291-2309`) | IDB `kv.cidade` | cria "Cidade anterior", converte, apaga `cidade` | **nenhum** |
| Multi-cidades → vault `Urbe` | `urbeEnsureSingleVault` (`app.js:4577-4620`) | todo boot (`:5612`) | copia para `Urbe/Cidades/<nome>/`, desloca x, `origens/`, `merged-v1.json`; **nunca apaga origem**; mapa sem `mundo`/`id` | `integration-runtime:28-38` |
| Mundo/terreno | `app.js:3230, 5382` | `mundo !== 'placas-1'` | `urbeReorganizarCidade` **muta x,y,w,h,cells** e grava | **nenhum dedicado** |
| IA legada | `ai/store.js:36-42` | `kv.config` ausente | importa `apiKey` OpenRouter, instruções, memória | [I] `ai-agent.mjs` (não verificado) |
| Mapa v1/v2/v4 | `app.js:2100-2200, 3228-3232` | toda abertura | leitura defensiva sem branch por `v` | só mapas mínimos |
| Tutorial | `tutorial.js` | versão ≠ `C.version` | recria o que falta, pergunta antes de sobrescrever | `tutorial.mjs:30-46` |

## 6. Riscos de compatibilidade 1.x → 2.x
- **R-1 [F]** Versão desconhecida vira perda silenciosa: `history.import`/`compositions.import` (`history.js:10`, `store.js:1`) descartam `version!==1` e o flush **sobrescreve**; journal com versão ≠ 1 é ignorado e apagado (`workspace.js:18-20`); `trash.import` aceita qualquer forma. [I] Abrir vault 2.x na 1.8.2 destrói histórico, composições e recuperação. → REQ-035, REQ-036.
- **R-2 [F]** Sem ID no arquivo. [I] Edição externa (Obsidian, sync, rename, `syncFromDisk` `workspace.js:42-60`) gera doc novo com ID novo e órfãos em history/trash/compositions (sem GC); vault copiado sem `.urbe/` perde relações. → REQ-042.
- **R-3 [F]** Espaciais/regiões por path; rename externo perde posição. → REQ-041 (REQ-013).
- **R-4 [F]** `mundo:'placas-1'` acopla mapa ao gerador de terreno; mudança reposiciona a cidade sem undo; vault mesclado sem `mundo` reorganiza na primeira abertura. → REQ-043.
  - [C] **Mitigado em RM-F1-17:** `mundo` segue explícito (`URBE_MUNDO`). Toda reorganização (abertura com `mundo` diferente, mapa sem `mundo`, comando "Organizar os bairros", API `city.layout.reorganize` de plugins) passa por `src/world/layout-guard.js`. Antes de mexer em qualquer posição, ele tira uma foto do layout (desfazer) e grava backup do mapa em `.urbe/backup/<data>-layout-layout/`. O registro vai para `vault.json.maintenance` (`{kind:'layout',reason,from,to,backup}`). **Regra do mapa sem `mundo`** (mapa v2 ou vault mesclado pela 1.x): é tratado como "sem versão" e reorganizado uma vez, com backup e oferta de desfazer. Na abertura aparece o diálogo "Cidade reorganizada" (Desfazer / Manter a nova; fechar = manter). O comando `city.undoReorganize` desfaz a última reorganização da sessão. Desfeita, a cidade é gravada com `mundo` atual e não é reorganizada de novo. Vault ou mapa somente leitura: reorganiza só em memória, sem backup. Testes: `tests/layout-guard.mjs`, `tests/e2e/layout.e2e.mjs`.
- **R-5 [F]** Escrita dupla e camadas de `estadoDesejado`. → REQ-040, REQ-028.
- **R-6 [F]** Referências por nome/path (`parentNoteName`, `tema.json`, aprovação de plugins) quebram em rename. → REQ-041.
- **R-7 [F]** Plugins são código com poder total aprovados por hash no aparelho; API `urbe.*` em PT (`nota:criada`, `notas.escrever`); `urbe.plugins.v1` usa `vault::path` com vault `'Urbe'` (`plugins.js:21`). → REQ-032, REQ-051.
- **R-8 [F]** Chaves de IA em texto puro no IDB; conversas guardam `vault`; sem export nem backup entre plataformas; fora do ZIP. → REQ-044, REQ-053.
- **R-9 [F]** Estado local fora de qualquer backup (aprovações, `editor.workspace` com docIds, explorer, IA, `modoSeguro`); ZIP só arquivos, sem manifesto. → REQ-044, REQ-047.
- **R-10 [F]** Modo IDB `fs` é dado só do navegador, sem integridade/export garantido. → REQ-046.
- **R-11 [F]** Extensões textuais divergentes (§1). → REQ-039.
- **R-12 [F]** `aiLocal` persistido sem consumidor (grep em `src/ai`). → REQ-048.
- **R-13 [F]** Migração multi-cidade roda a cada boot e nunca remove origem; vaults 1.x terão cópias duplicadas em `Cidades/…`. → REQ-045.
- **R-14 [F]** `normalize` de páginas reescreve `version` e descarta chaves desconhecidas (`engine.js:376-388`); [I] abrir/salvar página 2.x em 1.x perde dados. → REQ-049.
- **R-15 [F]** Escrita não atômica na web/FSA (`createWritable`, `app.js:1987-1990`); só Electron usa tmp+rename (`vault-fs.js:49`). → REQ-046.

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
| `.urbe/merged-v1.json`, `.urbe/origens/*` | marcador da migração multi-cidade | mesmo nome; `vault.json.migrations` registra conclusão | sim | nunca apaga origem | preservar | `v1-cidades-mescladas` |
| `.urbe/vault.json` | — | novo: `{formatVersion,createdBy,lastWriter,migrations[],maintenance[]}` (`maintenance`: últimas 20 limpezas `{kind:'gc',at,removed,orphanDays,trashDays}`, RM-F1-16) | sim | sim | `formatVersion` maior: modo seguro | `vault.mjs` (RM-F1-04) |
| `.urbe/identity.json` | — | novo: `{version:1,docs:{<docId>:{path,fingerprint,seen}}}` | sim | sim | preservar | RM-F1-15 |
| `.urbe/backup/<data>-<de>-<para>/` | — | novo: cópias restauráveis pré-migração | sim | sim | — | RM-F1-07 |
| `urbe-export.json` (dentro do ZIP) | — | novo: manifesto do export | sim | sim | recusar import de `format` desconhecido | RM-F1-18 |
| `Personalização/tema.json` | `versao:1` | mesmo | v1 | v1 (preserva chaves desconhecidas) | preservar; modo seguro de tema | `v1-personalizacao`, `futuro-desconhecido` |
| `Personalização/{temas,estilos,texturas,plugins}/*` | sem versão | mesmo | sim | sim | preservar | `v1-personalizacao` |
| `Páginas/**/*.page.json` (+ Modelos/Blocos) | `version:1` | mesmo; preserva chaves desconhecidas e `version` | v1 | v1 sem reescrever `version` | preservar (somente leitura no Studio) | `v1-paginas`, `futuro-desconhecido` |
| Notas e demais textos | — | sem mudança (nenhum ID no arquivo) | sim | bytes preservados | — | todas as fixtures |
