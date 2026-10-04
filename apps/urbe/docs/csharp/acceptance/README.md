# Corpus de aceite — primeira fatia de UC-2

Autoridade: DEC-0024-B / ADR-0016 e `../ROADMAP.md`. Tooling local de migração, sem código do cliente novo. A base imutável é o commit em `oracle.json`; a execução nunca regenera expectativas para fazer um cliente passar.

## Dados e limites

- `oracle.json`: caminho relativo ao Product, tamanho em bytes e SHA-256 de fixtures (incluindo `.urbe` e binários), testes, quatro contratos, requisitos/SPEC/ROADMAP e tutorial. Sem dados reais do usuário ou segredos.
- `cases.json`: todos os goldens convertidos para dados JSON com ID estável, operação, requisitos, fonte, entrada e saída esperada. Nesta base: 40 casos Markdown, 110 Visual, 12 cenários de vault, três de restauração, quatro de crash recovery, 13 de storage (nove FSA/native + quatro IDB/browser), um de migração IDB v1 e 26 de identidade/GC — 209 casos no total.
- `../PARITY.md`: projeção das fontes, 101 REQ IMPLEMENTAR e inventário observável. Não é uma SPEC concorrente nem resultado do cliente C#.
- Os contratos de persistência/cascas e os E2E estão congelados, mas **ainda precisam ser transcritos para cenários portáveis**. UC-2 continua aberto; este corpus não fecha UC-9/10/18 nem gate de plataforma.

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
node tests/csharp-domain-parity.mjs
node tests/csharp-storage-parity.mjs
node tests/csharp-vault-parity.mjs
node tests/csharp-parity.mjs
npm run check
```

O adapter JS executa o renderer real em VM. `run all` com ele falha de propósito: ele não implementa DOM Visual. A prova Visual existente é `node tools/run-e2e.mjs app-runtime-visual` em Chromium real (110 casos). Isso não prova o C# inexistente.

## Restante de UC-2

1. Recuperação portável está coberta para o contrato atual: 12 fixtures com load/edição/flush/reabertura, três casos de restauração e quatro casos de crash em gravação multi-arquivo. Rollback atômico de uma restauração que falhe no meio não é requisito explícito de REQ-038/046 e não bloqueia UC-2; pode virar hardening futuro sem congelar o comportamento legado.
2. Completar o contrato nativo para capacidades presentes/ausentes e permissões recusadas por superfície. Gestão de vaults, estado existente da loja `fs`, pastas e binários do backend IDB/browser já têm quatro casos portáveis; a migração histórica `kv["cidade"]` tem um caso próprio, além do E2E real em Chromium.
3. Transcrever UI/E2E para passos e resultados observáveis por superfície; seletores dependem da pilha UC-5. Chromium não valida Android físico ou Windows instalado.
4. Ligar os adapters C# ao mesmo corpus em M1–M4. UC-7 e G-C0 continuam precedendo qualquer código C# de produto.

## Revisão pontual P4-9 — 2026-10-04

ADD-0015/DEC-0031/ADR-0019 autorizam a troca do feed de distribuição. O PR #167 revisa explicitamente somente as entradas `tests/native-contract.mjs` e `tests/native-android.mjs` do inventário: preserva as asserções de filesystem, capacidades, cancelamento, estados e erros; troca a fixture do feed legado pelo feed por Product e acrescenta rejeição de releases cruzadas. Os bytes/hashes anteriores permanecem no Git da base do oráculo; nenhum caso de persistência, golden, requisito, ID ou baseCommit é removido ou regenerado. A revisão não declara paridade C# nem validação em aparelho.
