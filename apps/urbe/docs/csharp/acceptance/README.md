# Corpus de aceite — UC-2 em andamento

Autoridade: DEC-0024-B / ADR-0016 e `../ROADMAP.md`. Tooling local de migração, sem código do cliente novo. A base imutável é o commit em `oracle.json`; a execução nunca regenera expectativas para fazer um cliente passar.

## Dados e limites

- `oracle.json`: caminho relativo ao Product, tamanho em bytes e SHA-256 de fixtures (incluindo `.urbe` e binários), testes, quatro contratos, requisitos/SPEC/ROADMAP e tutorial. Sem dados reais do usuário ou segredos.
- `cases.json`: todos os goldens convertidos para dados JSON com ID estável, operação, requisitos, fonte, entrada e saída esperada. Nesta base: 40 casos Markdown, 110 Visual, 12 cenários de vault, três de restauração, quatro de crash recovery, 13 de storage (nove FSA/native + quatro IDB/browser), um de migração IDB v1, 26 de identidade/GC e 24 de contrato nativo, 19 de atualização Android/Windows e oito de UI e 11 de abertura de vault e um de IDB legado e 16 de storage real e oito de identidade/layout/map-writer/escala S/multi-city pelo navegador e um round-trip ZIP real no navegador — 297 casos no total.
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
- `browser.storage`: os mesmos passos `{method,args}` do contrato, agora sobre IndexedDB e FSA/OPFS reais. A casca cria o vault inicial isolado, exceto quando `seed` já fornece a loja histórica; `cities/createCity/removeCity` recebem argumentos globais, outros métodos recebem o vault implícito ou o `step.vault` explícito. Blobs viram bytes; listagens são ordenadas. Não injeta permissão do SO nem compartilha banco/pasta OPFS entre casos. Os 16 casos cobrem texto, unicode, binário, pastas, ocultos/path inválido, gestão/isolamento de vaults e lote de 40 arquivos.
- `browser.world`: fixture codificada, cenário semântico e contagem mínima de documentos → fatos de identidade e layout. Oito cenários transcrevem IDs de região/asset e vínculos, rename de pasta, reorganização de mundo antigo/manter/desfazer, backup original e registros, move/rename externos com app fechado/aberto, persistência automática/escritor único de `.urbe/mapa.json`, abertura do vault sintético S (50 notas/2 assets) e a migração multi-city com três boots idempotentes, IDs/geometria de origem e arquivamento sem apagar stores. IDs/posições de referência são capturados antes das ações e comparados depois; o golden contém relações verdadeiras, sem copiar IDs aleatórios. Binário movido verifica presença conforme o E2E; posição usa casas reais da cidade, não a projeção ainda não hidratada na criação.
- `browser.zip`: fixture histórica codificada → export ZIP real, manifesto/hash, inclusão de notas/mapa/binário, exclusão do journal e de chave de IA, import em app limpo com preferência permitida, cancelamento de ZIP adulterado e recusa de formato futuro. O segredo no caso é uma string sintética de teste, nunca uma credencial real.
- `surface-protocol.json`: 11 protocolos semânticos para Web/Windows/Android e compatibilidade cruzada. Eles fixam precondições, passos, resultados e evidência de offline, permissões, install/update, lifecycle/back, save/print e ZIP entre superfícies. O arquivo é validado pelo checker, mas fica explicitamente `not-executed`: execução real pertence a UC-23/24/25/29 e jamais vira verde por simulação.
- `visual.serialize`: DOM **inerte** criado de `input.html`, frontmatter de `input.bodyEditor` conforme `splitFrontmatter`, serialização `editor-visual.md` com integração matemática. Resultado `output.markdown`. Nunca executar scripts/carregar imagens do corpus. Normalizações e quebras de linha seguem o golden; o avaliador não normaliza strings.

`all` tem prazo de 300 segundos para executar sequencialmente as famílias de navegador; operações individuais mantêm 120 segundos. Não há timeout convertido em sucesso.

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

Os 14 E2E congelados têm seus critérios funcionais atuais decompostos no corpus portável: lifecycle documental e escala S são casos separados para não acoplar semântica ao tamanho da fixture. Os 297 casos canônicos falham em omissão, divergência, timeout ou resultado fabricado.

O que depende de aparelho/host instalado não é uma lacuna de dados: está declarado em `surface-protocol.json` com 11 protocolos e estado obrigatório `not-executed`. UC-23/24/25/29 deverão anexar evidência real por superfície; Chromium, mocks ou chamadas de bridge não substituem essa prova.

Depois de UC-2, o trabalho deixa de ampliar o oráculo por inércia: os clientes C# de M1–M4 devem executar o mesmo corpus, e qualquer mudança de baseline exige diff e justificativa explícitos. UC-7 e G-C0 continuam precedendo código C# de produto.

## Revisão pontual P4-9 — 2026-10-04

ADD-0015/DEC-0031/ADR-0019 autorizam a troca do feed de distribuição. O PR #167 revisa explicitamente somente as entradas `tests/native-contract.mjs` e `tests/native-android.mjs` do inventário: preserva as asserções de filesystem, capacidades, cancelamento, estados e erros; troca a fixture do feed legado pelo feed por Product e acrescenta rejeição de releases cruzadas. Os bytes/hashes anteriores permanecem no Git da base do oráculo; nenhum caso de persistência, golden, requisito, ID ou baseCommit é removido ou regenerado. A revisão não declara paridade C# nem validação em aparelho.

`lifecycleObserve` compara conteúdo salvo, identidade capturada e presença na lixeira. `rememberPosition` captura a posição do mapa persistido após o rename e verifica ID/caminho antigo removido; `positionStable` compara essa posição com a projeção do mundo após reabertura (mesma ordem do E2E original). A fonte não afirma posição estável após restaurar da lixeira; `editHistory` exige histórico registrado. `gc` exige diálogo explicativo e confirmação/cancelamento, paleta sem comando bruto, preservação de IDs ativos e fontes da composição. O campo `now` fixa `Date.now` somente no contexto isolado desses casos para tornar a retenção reproduzível; não muda o relógio do host.

`browser.world` com `scenario: scale-s` usa apenas os parâmetros portáveis `size`, `seed`, `docs` e `assets`; o cliente de referência materializa o gerador sintético congelado de performance. O aceite compara contagens/título e ausência de erros, não bytes específicos do gerador, para que um cliente C# possa produzir uma fixture equivalente sem depender de JavaScript.

## Revisão crítica REQ-110 (Issue #261)

A correção do importador DEVICE acrescenta um requisito e capability, corrige restauração de metadados e revê o E2E do comportamento defeituoso. Baseline anterior preservado em `history/pre-REQ-110-oracle.json` e `history/pre-REQ-110-native-cases.json`. O conjunto continua com 297 IDs; somente três saídas de declaração de contrato nativo ganham a capability aditiva `importSelection` (Web ausente, Windows/Android presente). Nenhum golden Markdown/Visual ou fixture histórico foi regenerado. Os novos casos de importação são suplementares e executam código de produção, streams e filesystem reais. Esta revisão é explícita e sujeita ao PR crítico; não é atualização implícita do oráculo para mascarar falha.
