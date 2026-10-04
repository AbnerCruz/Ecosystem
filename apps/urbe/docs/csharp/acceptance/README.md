# Corpus de aceite — UC-2 em andamento

Autoridade: DEC-0024-B / ADR-0016 e `../ROADMAP.md`. Tooling local de migração, sem código do cliente novo. A base imutável é o commit em `oracle.json`; a execução nunca regenera expectativas para fazer um cliente passar.

## Dados e limites

- `oracle.json`: caminho relativo ao Product, tamanho em bytes e SHA-256 de fixtures (incluindo `.urbe` e binários), testes, quatro contratos, requisitos/SPEC/ROADMAP e tutorial. Sem dados reais do usuário ou segredos.
- `cases.json`: todos os goldens convertidos para dados JSON com ID estável, operação, requisitos, fonte, entrada e saída esperada. Nesta base: 40 casos Markdown, 110 Visual, 12 cenários de vault, três de restauração, quatro de crash recovery, 13 de storage (nove FSA/native + quatro IDB/browser), um de migração IDB v1, 26 de identidade/GC e 24 de contrato nativo, 19 de atualização Android/Windows e oito de UI e 11 de abertura de vault e um de IDB legado pelo navegador — 272 casos no total.
- `../PARITY.md`: projeção das fontes, 101 REQ IMPLEMENTAR e inventário observável. Não é uma SPEC concorrente nem resultado do cliente C#.
- Os contratos de persistência/cascas e os E2E estão congelados; cenários nativos possuem a fatia abaixo e UI/E2E têm oito cenários iniciais portáveis; as demais transcrições continuam abertas. UC-2 continua aberto; este corpus não fecha UC-9/10/18 nem gate de plataforma.

`source` usa JSON Pointer para o registro na base. IDs não são renumerados depois de publicados. Mudanças no baseline precisam de diff, justificativa rastreável e revisão de compatibilidade, respeitando DEC-0025-C. `check` e `render` apenas leem o oráculo; não há atualização automática.

## Protocolo de execução

O runner envia um objeto JSON UTF-8 em stdin: `{"schemaVersion":1,"cases":[...]}`. Cada caso tem o formato de `cases.json`. O cliente calcula a saída a partir de `input`; não usa `expected` para produzi-la. Um C# futuro desserializa com `System.Text.Json` e executa o domínio sem conhecer o runner Node ou a casca JS.

O cliente devolve somente JSON em stdout (logs em stderr):

```json
{"schemaVersion":1,"results":[{"id":"markdown-001","output":{"html":"saída calculada"}}]}
```

- `markdown.render`: `input.markdown` → `output.html` exato conforme `editor-markdown.md`. Mesmo estado da base: módulo math carregado, KaTeX ausente.
- `identity.text`: normalização e fingerprint exatos sobre UTF-16 (inclui emoji, acentos, NUL e CRLF).
- `identity.parse`: estado de sidecar ausente/corrompido/atual/futuro.
- `identity.pair`: renames por fingerprint único; duplicidade/ambiguidade/vazio não herdam ID.
- `gc.plan`: plano determinístico de retenção de histórico/lixeira e fontes de composição, sem gravação.
- `storage.scenario`: métodos/argumentos explícitos do adapter → valores de leitura, listagem, bytes e rejeição esperada. Adapter JS usa FSA real sobre ponte/arquivos temporários do host e o `idb.js` real sobre um IndexedDB determinístico do tooling; o E2E congelado `tests/e2e/adapters.e2e.mjs` prova IDB real no Chromium. A permissão recusada do FSA é injetada no adapter e não prova diálogo ou permissão do SO.
- `idb.legacy-city`: fixture histórica `kv["cidade"]` da 1.x → projeção observável da migração já congelada pelo E2E: chave antiga removida, origem `Cidade anterior` preservada e conteúdo reunido no vault único `Urbe/Cidades/Cidade anterior/**`. O runner Node prova a transformação semântica; `tests/e2e/fixtures.e2e.mjs` continua sendo a prova do boot/migração real no Chromium.
- `vault.scenario`: arquivos com bytes UTF-8/base64 e passos explícitos → caminhos carregados, hashes, IDs, proteção forward, recuperação e backup. O adapter usa o domínio JS real em memória, sem abrir dados do usuário.
- `vault.restore`: migração, alteração e restauração → hash recuperado ou rejeição de cópia corrompida/ausente.
- `vault.crash-recovery`: falha injetada antes/durante/depois de uma gravação multi-arquivo → estado parcial observável, presença do journal e estado final após reabertura. Cobre falha ao criar o journal, ao gravar o segundo arquivo, ao remover uma nota e ao remover o próprio journal.
- `native.scenario`: `surface` (web/windows/android), passos `{method,args}` e recusa opcional `expectError` → `output.values`. Dados adicionais `deny` (write/save) injetam falha do host; `cancel` controla o diálogo desktop e `blocked` aceita recusa ou silêncio para URLs proibidas. O cliente executa bridge/preload/main reais com Electron/Capacitor simulados; filesystem desktop usa pasta temporária. O C# futuro deve fornecer resultados equivalentes por superfície, sem depender do nome do host legado. `contract` compara capacidades e API funcional; `stat` só kind, `vault` presença de label/path, `save` bytes/cancelamento/destino, `print` resultado e isolamento configurado (Windows) ou submissão ao plugin (Android). Essas projeções estão explícitas: caminhos absolutos, timestamps e conteúdo de PDF não são goldens.
- `update.scenario`: `surface`, respostas de host/rede injetadas e passos `check/install/subscribe` → estados completos, eventos, replay, URLs abertas (Android) ou reinício solicitado (Windows). Android usa bridge real e releases JSON offline; Windows usa main/preload reais, IPC com origem confiável e updater simulado, sem baixar/instalar binários. Timers automáticos são capturados e não executados. O caminho temporário contém todas as gravações. A comparação preserva estados/versões/percentuais/mensagens; Windows conserva a primeira linha do erro conforme o legado. Não prova autenticidade de release, assinatura ou instalação física.
- `ui.scenario`: arquivos UTF-8 iniciais e passos `{method,args}` (`boot/create/observe/reload/visualAppend/route/rename/delete/restore/lifecycleObserve/editHistory/gc/linkTargets/rememberPosition/positionStable/preview`) → `values`. Ações são semânticas; seletores ficam no adapter JS e a pilha C# escolhe seus próprios seletores. `observe` projeta caminho, conteúdo exato, conteúdo persistido, identidade estável contra o ID capturado na criação e presença no mundo. IDs aleatórios não são substituídos por IDs fixos. UI real executa em Chromium/IDB; não prova interação por toque ou host nativo.
- `browser.vault`: arquivos UTF-8/base64 e probes explícitos → documentos presentes, hashes exatos, versão de vault, preservação dos sidecars v1 e backup do mapa original. O fluxo `historical` abre e salva pelo app real; `forward` tenta editar Alfa, aguarda o debounce e mede hashes de artefatos futuros, mapa futuro e readonly sem novos arquivos. As projeções seguem `fixtures.e2e.mjs`: hashes de conteúdo textual; readonly binário compara presença/tamanho conforme a fonte, sem afirmar bytes que o helper não lê. O conjunto contém os oito vaults históricos e três de versão futura do E2E original.
- `browser.idb-legacy`: fixture `kv["cidade"]` semeada antes do boot → conteúdo Alfa exato e chave antiga removida após abrir pelo app real. O adapter usa IDB real em Chromium isolado; não mistura essa loja histórica com o backend `fs` atual.
- `visual.serialize`: DOM **inerte** criado de `input.html`, frontmatter de `input.bodyEditor` conforme `splitFrontmatter`, serialização `editor-visual.md` com integração matemática. Resultado `output.markdown`. Nunca executar scripts/carregar imagens do corpus. Normalizações e quebras de linha seguem o golden; o avaliador não normaliza strings.

Omissões, IDs duplicados/desconhecidos, `error`, formato incompatível, saída divergente, processo com erro, timeout e JSON inválido falham. Não há skip verde. Só a ordem de propriedades JSON é ignorada; arrays e strings permanecem exatos. `all` exige todas as operações; uma execução por operação prova somente aquela família.

## Verificação reproduzível

Na pasta `apps/urbe`:

```bash
node tools/csharp-parity.mjs check
node tools/csharp-parity.mjs run markdown.render node tools/parity-js-client.mjs
node tools/csharp-parity.mjs run vault.scenario node tools/parity-vault-client.mjs
node tools/csharp-parity.mjs run vault.restore node tools/parity-vault-client.mjs
node tools/csharp-parity.mjs run vault.crash-recovery node tools/parity-vault-client.mjs
node tools/csharp-parity.mjs run storage.scenario node tools/parity-storage-client.mjs
node tools/csharp-parity.mjs run idb.legacy-city node tools/parity-storage-client.mjs
node tools/csharp-parity.mjs run identity.text node tools/parity-domain-client.mjs
node tools/csharp-parity.mjs run gc.plan node tools/parity-domain-client.mjs
node tools/csharp-parity.mjs run native.scenario node tools/parity-native-client.mjs
node tools/csharp-parity.mjs run update.scenario node tools/parity-update-client.mjs
node tools/csharp-parity.mjs run all node tools/parity-reference-client.mjs
node tests/csharp-update-parity.mjs
node tests/csharp-native-parity.mjs
node tests/csharp-domain-parity.mjs
node tests/csharp-storage-parity.mjs
node tests/csharp-vault-parity.mjs
node tests/csharp-parity.mjs
npm run check
```

O adapter Markdown executa o renderer real em VM e continua recusando `all`, pois só implementa sua família. O novo `parity-reference-client.mjs` despacha todas as famílias e abre Chromium apenas para Visual/UI; `tests/e2e/app-runtime-parity.e2e.mjs` executa `run all` no filtro `app-runtime` já usado pelo CI. O download local do Chromium falhou. O CI do commit 8693684 executou 256/256 casos, incluindo os quatro cenários UI iniciais; o commit 9c4d297 confirmou 260/260 casos e 5/5 E2E. Os 11 casos browser.vault aguardam execução no próximo PR. A prova Visual anterior é `node tools/run-e2e.mjs app-runtime-visual` em Chromium real (110 casos). Isso não prova o C# inexistente.

## Restante de UC-2

O inventário dos 14 E2E e os critérios ainda sem dados portáveis estão em [E2E-COVERAGE.md](E2E-COVERAGE.md).

1. Recuperação portável está coberta para o contrato atual: 12 fixtures com load/edição/flush/reabertura, três casos de restauração e quatro casos de crash em gravação multi-arquivo. Rollback atômico de uma restauração que falhe no meio não é requisito explícito de REQ-038/046 e não bloqueia UC-2; pode virar hardening futuro sem congelar o comportamento legado.
2. Contrato nativo agora possui 24 casos portáveis: capacidades e APIs presentes/ausentes no Web/Windows/Android, vault, bytes, caminhos inválidos, falhas injetadas de escrita/exportação, exportação binária/cancelamento, allowlist de links, impressão/PDF/cancelamento, status de armazenamento e minimização Android, atualização Android com HTTP 403. Os 19 novos casos cobrem atualização/eventos com hosts e rede simulados; faltam botões/dialogs/permissões do SO e lifecycle real. A minimização verifica chamada/listener, não executa a UX de voltar; flags da janela PDF não provam isolamento numa instalação real. Backslash é contido/normalizado no desktop e recusado no Android; o corpus não transforma essa diferença herdada em vulnerabilidade a reproduzir. Gestão de vaults, estado existente da loja `fs`, pastas e binários do backend IDB/browser já têm quatro casos portáveis; a migração histórica `kv["cidade"]` tem um caso próprio, além do E2E real em Chromium.
3. Continuar a transcrição UI/E2E além dos oito casos iniciais (primeira abertura, criação/salvamento/reabertura, edição Visual e roteamento, lifecycle documental e GC cancelar/aplicar); seletores dependem da pilha UC-5. Chromium não valida Android físico ou Windows instalado.
4. Ligar os adapters C# ao mesmo corpus em M1–M4. UC-7 e G-C0 continuam precedendo qualquer código C# de produto.

## Revisão pontual P4-9 — 2026-10-04

ADD-0015/DEC-0031/ADR-0019 autorizam a troca do feed de distribuição. O PR #167 revisa explicitamente somente as entradas `tests/native-contract.mjs` e `tests/native-android.mjs` do inventário: preserva as asserções de filesystem, capacidades, cancelamento, estados e erros; troca a fixture do feed legado pelo feed por Product e acrescenta rejeição de releases cruzadas. Os bytes/hashes anteriores permanecem no Git da base do oráculo; nenhum caso de persistência, golden, requisito, ID ou baseCommit é removido ou regenerado. A revisão não declara paridade C# nem validação em aparelho.

`lifecycleObserve` compara conteúdo salvo, identidade capturada e presença na lixeira. `rememberPosition` captura a posição do mapa persistido após o rename e verifica ID/caminho antigo removido; `positionStable` compara essa posição com a projeção do mundo após reabertura (mesma ordem do E2E original). A fonte não afirma posição estável após restaurar da lixeira; `editHistory` exige histórico registrado. `gc` exige diálogo explicativo e confirmação/cancelamento, paleta sem comando bruto, preservação de IDs ativos e fontes da composição. O campo `now` fixa `Date.now` somente no contexto isolado desses casos para tornar a retenção reproduzível; não muda o relógio do host.
