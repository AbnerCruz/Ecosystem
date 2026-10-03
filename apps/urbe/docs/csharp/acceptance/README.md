# Corpus de aceite — primeira fatia de UC-2

Autoridade: DEC-0024-B / ADR-0016 e `../ROADMAP.md`. Tooling local de migração, sem código do cliente novo. A base imutável é o commit em `oracle.json`; a execução nunca regenera expectativas para fazer um cliente passar.

## Dados e limites

- `oracle.json`: caminho relativo ao Product, tamanho em bytes e SHA-256 de fixtures (incluindo `.urbe` e binários), testes, quatro contratos, requisitos/SPEC/ROADMAP e tutorial. Sem dados reais do usuário ou segredos.
- `cases.json`: todos os goldens convertidos para dados JSON com ID estável, operação, requisitos, fonte, entrada e saída esperada. Nesta base: 40 casos Markdown, 110 Visual, 12 cenários de vault três de restauração e nove de storage.
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
- `storage.scenario`: métodos/argumentos explícitos do adapter → valores de leitura, listagem, bytes e rejeição esperada. Adapter JS usa FSA real sobre ponte/arquivos temporários do host; permissão recusada é injetada no adapter, não prova diálogo ou permissão do SO. IDsB e gestão de vaults ainda pendentes.
- `vault.scenario`: arquivos com bytes UTF-8/base64 e passos explícitos → caminhos carregados, hashes, IDs, proteção forward, recuperação e backup. O adapter usa o domínio JS real em memória, sem abrir dados do usuário.
- `vault.restore`: migração, alteração e restauração → hash recuperado ou rejeição de cópia corrompida/ausente.
- `visual.serialize`: DOM **inerte** criado de `input.html`, frontmatter de `input.bodyEditor` conforme `splitFrontmatter`, serialização `editor-visual.md` com integração matemática. Resultado `output.markdown`. Nunca executar scripts/carregar imagens do corpus. Normalizações e quebras de linha seguem o golden; o avaliador não normaliza strings.

Omissões, IDs duplicados/desconhecidos, `error`, formato incompatível, saída divergente, processo com erro, timeout e JSON inválido falham. Não há skip verde. Só a ordem de propriedades JSON é ignorada; arrays e strings permanecem exatos. `all` exige todas as operações; uma execução por operação prova somente aquela família.

## Verificação reproduzível

Na pasta `apps/urbe`:

```bash
node tools/csharp-parity.mjs check
node tools/csharp-parity.mjs run markdown.render node tools/parity-js-client.mjs
node tools/csharp-parity.mjs run vault.scenario node tools/parity-vault-client.mjs
node tools/csharp-parity.mjs run vault.restore node tools/parity-vault-client.mjs
node tools/csharp-parity.mjs run storage.scenario node tools/parity-storage-client.mjs
node tests/csharp-storage-parity.mjs
node tests/csharp-vault-parity.mjs
node tests/csharp-parity.mjs
npm run check
```

O adapter JS executa o renderer real em VM. `run all` com ele falha de propósito: ele não implementa DOM Visual. A prova Visual existente é `node tools/run-e2e.mjs app-runtime-visual` em Chromium real (110 casos). Isso não prova o C# inexistente.

## Restante de UC-2

1. Converter o IndexedDB legado e ampliar as falhas de recuperação. As 12 fixtures já têm load/edição/flush/reabertura, hashes, IDs, proteção forward e backup íntegro/idempotente. Três casos cobrem restauração válida e rejeição de cópia corrompida/ausente; a rejeição não afirma atomicidade da restauração inteira.
2. Ampliar contrato adapter com gestão de vaults e backend IDB/browser (nove casos FSA/native já portáveis); contrato nativo para capacidades presentes/ausentes e permissões recusadas.
3. Transcrever UI/E2E para passos e resultados observáveis por superfície; seletores dependem da pilha UC-5. Chromium não valida Android físico ou Windows instalado.
4. Ligar os adapters C# ao mesmo corpus em M1–M4. UC-7 e G-C0 continuam precedendo qualquer código C# de produto.
