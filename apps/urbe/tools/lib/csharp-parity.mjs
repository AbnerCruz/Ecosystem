// UC-1/UC-2: tooling de aceite do Product, não código de runtime nem infraestrutura compartilhada.
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { ROOT, parseRequirements, parseSpec, parseRoadmap } from './v2-docs.mjs';
import { makeVaultCases, makeRestoreCases, makeCrashCases } from './parity-vault.mjs';
import { makeStorageCases, makeLegacyIdbCases } from './parity-storage.mjs';
import { makeDomainCases } from './parity-domain.mjs';

export const read = (p) => readFileSync(join(ROOT, p), 'utf8');
export const json = (p) => JSON.parse(read(p));
export const sha = (b) => createHash('sha256').update(b).digest('hex');
export function files(dir) {
  return readdirSync(join(ROOT, dir), { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? files(`${dir}/${e.name}`) : e.isFile() ? [`${dir}/${e.name}`] : []).sort();
}
const cell = (s) => String(s).replace(/\|/g, '\\|').replace(/\n/g, ' ');
const lineOf = (p, text) => `${p}:${read(p).split('\n').findIndex((s) => s.includes(text)) + 1}`;

// Inventário inclui arquivos ocultos das fixtures: .urbe e .pasta fazem parte do contrato.
export function oraclePaths() {
  return [...files('tests').filter((p) => !['tests/csharp-parity.mjs', 'tests/csharp-vault-parity.mjs', 'tests/csharp-vault-contract.mjs', 'tests/csharp-storage-parity.mjs', 'tests/csharp-domain-parity.mjs'].includes(p)),
    ...files('docs/v2/contracts'), ...files('tutorial'),
    'docs/v2/REQUIREMENTS.md', 'docs/v2/SPEC.md', 'docs/v2/ROADMAP.md',
    'docs/v2/discovery/DATA-CATALOG.md', 'docs/v2/adr/0004-compatibilidade-1x-e-protecao-forward.md'].sort();
}
export function snapshot(paths = oraclePaths()) {
  return paths.map((path) => { const bytes = readFileSync(join(ROOT, path));
    return { path, bytes: bytes.length, sha256: sha(bytes) }; });
}
export function verifyOracle(manifest) {
  if (manifest.schemaVersion !== 1 || manifest.product !== 'urbe' || !/^[a-f0-9]{40}$/.test(manifest.baseCommit))
    throw new Error('identidade/versão/base do oráculo inválida');
  const expected = oraclePaths();
  const paths = manifest.files.map((f) => f.path);
  if (new Set(paths).size !== paths.length || JSON.stringify(paths) !== JSON.stringify(expected))
    throw new Error('inventário do oráculo alterado (arquivo ausente, duplicado ou novo)');
  for (const f of snapshot(expected)) {
    const frozen = manifest.files.find((x) => x.path === f.path);
    if (f.bytes !== frozen.bytes || f.sha256 !== frozen.sha256) throw new Error(`oráculo alterado: ${f.path}`);
  }
}

export function requirementRows() {
  const items = parseRoadmap(), spec = parseSpec();
  return [...parseRequirements().values()].filter((r) => r.estado === 'IMPLEMENTAR').map((r) => {
    const related = items.filter((i) => i.reqs.includes(r.id));
    const source = lineOf('docs/v2/REQUIREMENTS.md', `| ${r.id} |`);
    const clause = lineOf('docs/v2/SPEC.md', `**${r.id}**`);
    return { ...r, source, clause, section: spec.get(r.id), related };
  });
}

export function renderParity(corpus) {
  const rows = requirementRows();
  if (rows.some((r) => !r.section || !r.related.length)) throw new Error('REQ sem SPEC/ROADMAP');
  const out = ['# Matriz de paridade — Urbe em C#', '',
    '> Projeção gerada por `node tools/csharp-parity.mjs render`, nunca autoridade concorrente. Fontes: REQUIREMENTS, SPEC, ROADMAP da 2.0 e corpus UC-2. A base congelada é `oracle.json`. Verificar: `node tools/csharp-parity.mjs check`.', '',
    '## Como ler', '',
    'Cada linha é um critério a provar no cliente C#, não uma declaração de que a 1.x já o satisfaz. Estado do item JS é evidência histórica de entrega, não resultado da execução nem prova de paridade. Os itens não concluídos são lacunas explícitas; nenhum requisito desaparece por causa do congelamento DEC-0025-C.', '',
    'Web/PWA, Windows e Android são superfícies alvo. Linhas de governança/build são verificadas no repositório; hardening de uma casca vale naquela superfície. No corte, devem ser traduzidas pela pilha decidida em UC-5, com justificativa por requisito; não se impõe Electron/Capacitor ao C# por acidente. REQ-003/025/031 são obrigações do legado e exigem equivalência de resultado, não copiar sua implementação.', '',
    'O cliente C# ainda não existe: **paridade C# não verificada em todas as linhas**. Dados normativos valem sobre comportamento herdado; goldens preservam normalizações e não autorizam reproduzir vulnerabilidades. Validações de toque, layout, offline e instalação são humanas por superfície.', '',
    `## Requisitos IMPLEMENTAR (${rows.length})`, '',
    '| ID | Critério de aceite / fonte | Superfície | Evidência JS e lacunas |', '|---|---|---|---|'];
  for (const r of rows) {
    const surface = r.id === 'REQ-055' ? 'Windows (casca atual)' : r.id === 'REQ-056' ? 'Android (casca atual)' :
      r.id === 'REQ-107' ? 'Web/Android móvel; teclado virtual' :
      ['QUALITY', 'ARCH', 'DISTRIBUTION'].includes(r.classe) ? 'Repositório + Web/Windows/Android' : 'Web/Windows/Android';
    const evidence = r.related.map((i) => `${i.id} ${i.campos.Estado}: ${i.campos.Testes}`).join('; ');
    out.push(`| ${r.id} | ${cell(r.texto)} Fonte: ${r.source}; ${r.clause} (${r.section}). | ${surface} | ${cell(evidence)} |`);
  }
  out.push('', '## Comportamentos observáveis (fontes executáveis e tutorial)', '',
    'O inventário abaixo inclui todos os testes atuais de comportamento e todos os tópicos do tutorial. Cada fonte permanece disponível na base do oráculo; as expectativas detalhadas são as suas asserções/instruções. Uma linha no inventário não significa cobertura automatizada de todos os seus fluxos.', '',
    '| Fonte | Critério observável | Superfície / prova |', '|---|---|---|');
  for (const p of [...files('tests'), ...files('tutorial')].filter((p) =>
    /^tests\/(?:[^/]+\.mjs|security\/[^/]+\.mjs|e2e\/[^/]+\.e2e\.mjs)$/.test(p) || /^tutorial\/.*\.md$/.test(p))) {
    const s = read(p);
    const title = p.startsWith('tutorial/') ? (s.match(/^#\s+(.+)$/m)?.[1] || p) :
      (s.split('\n').filter((l) => l.startsWith('//')).slice(0, 3).join(' ').replace(/\/\//g, '').trim() || p);
    out.push(`| ${p}:1 | ${cell(title)} | ${p.startsWith('tutorial/') ? 'Web/Windows/Android; aceite manual conforme tópico' : p.includes('/e2e/') ? 'Chromium; demais superfícies ainda não provadas' : 'Node/contrato simulado; não prova aparelho'} |`);
  }
  out.push('', '## Operações dos contratos atuais', '',
    'Sem escolher a implementação C#, estas são as operações e saídas obrigatórias descritas nos quatro contratos. O hash congela o documento inteiro, inclusive invariantes fora das tabelas.', '',
    '| Fonte | Operação / API | Resultado observável |', '|---|---|---|');
  for (const p of files('docs/v2/contracts').filter((p) => p.endsWith('.md'))) {
    read(p).split('\n').forEach((l, i) => {
      if (!l.startsWith('|') || /^\|[-\s|]+$/.test(l)) return;
      const cells = l.split(/(?<!\\)\|/).slice(1, -1).map((x) => x.trim());
      if (['Método', 'Membro', 'Capacidade'].includes(cells[0])) return;
      out.push(`| ${p}:${i + 1} | ${cell(cells[0])} | ${cell(cells.slice(1).join('; '))} |`);
    });
  }
  out.push('', '## Casos portáveis UC-2', '',
    '| Família | Casos | Comparação |', '|---|---|---|');
  for (const op of [...new Set(corpus.cases.map((c) => c.operation))]) {
    out.push(`| ${op} | ${corpus.cases.filter((c) => c.operation === op).length} | Igualdade estrutural exata da saída; strings/bytes sem normalização implícita |`);
  }
  out.push('', '## Lacunas e ligação com o Ecosystem', '',
    '- UC-2 contém dados executáveis de Markdown, serialização Visual e 12 cenários de vault (load/edição/flush/reload, hashes, identidade, forward e backup íntegro/idempotente). Contratos nativos/persistência e E2E estão congelados por hash; três casos verificam restauração válida e rejeição de backup corrompido/ausente; quatro casos injetam crash antes/durante/depois de gravação multi-arquivo; 13 casos de storage verificam FSA sobre ponte nativa e o adapter IDB/browser, e um caso próprio cobre a migração histórica `kv["cidade"]` para o vault único sem confundi-la com a loja `fs` atual. Ainda faltam capacidades nativas completas e UI. A execução no C# integra UC-9/10/18/23/24/25.',
    '- REQ aceito ainda não entregue no JS continua como obrigação do C#, com o teste/aceite do ROADMAP acima; um golden de comportamento antigo nunca fecha esse requisito.',
    '- Android físico, Windows instalado e PWA offline ainda precisam de evidência própria. Simulações e Chromium não os validam.',
    '- Context/capabilities do Ecosystem têm contrato na Fase 2; transporte/Host API da Fase 5 ainda não implementados. Urbe fornece seu domínio por adapter quando esses contratos estiverem prontos; nunca depende do plano de controle nem diretamente de outro Product.',
    '- Agent Runtime R1 nasce no Product de IA; não se duplica o loop nem se adiciona referência de Product para Product. UC-21 e P6-4 dependem do Workspace/Host e de Extraction Review antes de consumo compartilhado.',
    '- Distribuição: Urbe JS mantém canal e identidade atuais até P4-9/UC-26/UC-31. Nenhum teste usa chave de assinatura de teste como substituta.', '');
  return out.join('\n');
}

export function makeCorpus() {
  const markdown = json('tests/fixtures/markdown-golden.json');
  const visual = json('tests/fixtures/visual-golden.json');
  return { schemaVersion: 1, product: 'urbe', cases: [
    ...markdown.map((g, i) => ({ id: `markdown-${String(i + 1).padStart(3, '0')}`, operation: 'markdown.render',
      requirements: ['REQ-027', 'REQ-057'], source: `tests/fixtures/markdown-golden.json#/${i}`,
      input: { markdown: g.input }, expected: { html: g.html } })),
    ...visual.map((g, i) => ({ id: `visual-${String(i + 1).padStart(3, '0')}`, operation: 'visual.serialize',
      requirements: ['REQ-027'], source: `tests/fixtures/visual-golden.json#/${i}`,
      input: { html: g.html, bodyEditor: g.bodyEditor }, expected: { markdown: g.md } })),
    ...makeVaultCases(), ...makeRestoreCases(), ...makeCrashCases(), ...makeStorageCases(), ...makeLegacyIdbCases(), ...makeDomainCases()] };
}
export function validateCorpus(corpus) {
  if (corpus.schemaVersion !== 1 || corpus.product !== 'urbe') throw new Error('corpus inválido');
  const canonical = makeCorpus();
  if (JSON.stringify(corpus) !== JSON.stringify(canonical)) throw new Error('casos não correspondem às fontes de aceite');
  if (new Set(corpus.cases.map((c) => c.id)).size !== corpus.cases.length) throw new Error('ID de caso duplicado');
}
// Falha em caso omitido, duplicado, desconhecido, erro ou saída diferente; não existe skip verde.
export function compareResults(cases, results) {
  if (!Array.isArray(results) || results.length !== cases.length) throw new Error('resultado incompleto');
  const seen = new Set();
  for (const r of results) {
    const c = cases.find((c) => c.id === r.id);
    if (!c || seen.has(r.id)) throw new Error('ID de resultado desconhecido/duplicado');
    seen.add(r.id);
    if (r.error || !deepEqual(c.expected, r.output)) throw new Error(`paridade falhou: ${r.id}`);
  }
}
function deepEqual(a, b) {
  if (a === b) return true;
  if (!a || !b || typeof a !== 'object' || typeof b !== 'object' || Array.isArray(a) !== Array.isArray(b)) return false;
  const ak = Object.keys(a).sort(), bk = Object.keys(b).sort();
  return JSON.stringify(ak) === JSON.stringify(bk) && ak.every((k) => deepEqual(a[k], b[k]));
}
