// UC-21 oracle: the agent loop of the UNMODIFIED Urbe 1.8.4-beta assistant (src/ai/agent.js) in node:vm, with the real
// tools (src/ai/tools.js) over a small documents service and a SCRIPTED model (each step returns the next scripted
// response, firing its stream events first). For each scenario it records what was sent to the model (system, prepared
// messages, tools, limits), the events, the conversation after and the result (changes, usage, steps, why it stopped);
// plus prepare(), systemPrompt() and titleFrom() on their own. Message ids are random in the original, so they are left out.
// Test-only tool: never part of the native app.
//
//   node csharp/tools/assistant-agent-oracle.mjs > csharp/tests/fixtures/assistant/agent-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const T0 = Date.UTC(2025, 9, 9, 15);
const RD = Date;
function FD(...a) { if (!new.target) return RD(); return a.length ? new RD(...a) : new RD(T0); }
FD.prototype = RD.prototype; FD.now = () => T0; FD.UTC = RD.UTC; FD.parse = RD.parse;

const NOTES = [
  { path: 'Comece.md', content: '# Comece\n\nVeja [[Plano]].\n\n## Tarefas\n- uma\n- duas\n' },
  { path: 'Projetos/Plano.md', content: '# Plano\n\nPassos em [[Tarefas]].\n' },
  { path: 'Área/Ideias.md', content: Array.from({ length: 30 }, (_, i) => 'Linha ' + (i + 1) + ' ' + 'y'.repeat(800)).join('\n') }
];

function makeCore() {
  let docs = NOTES.map((n, i) => ({ id: 'doc_' + i, title: n.path.split('/').pop().replace(/\.(md|markdown)$/i, ''), links: [], tags: [], modified: '', ...JSON.parse(JSON.stringify(n)) }));
  let seq = 100;
  const documents = {
    get: k => docs.find(d => d.id === k) || docs.find(d => d.path === k) || null,
    list: () => docs.slice(),
    upsert: data => {
      const at = data.id ? docs.findIndex(d => d.id === data.id) : -1;
      const title = data.title || data.path.split('/').pop().replace(/\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, '');
      const d = { links: [], tags: [], ...(at >= 0 ? docs[at] : {}), ...data, id: data.id || 'doc_' + (seq++), title };
      if (at >= 0) docs[at] = d; else docs.push(d);
      return d;
    }
  };
  const trash = { trash: id => { docs = docs.filter(d => d.id !== id); } };
  const core = { service: n => ({ documents, trash, explorer: { addFolder() {} }, knowledge: null, persistence: null }[n] ?? null), commands: { execute() {} } };
  return { core, notes: () => docs.map(d => ({ path: d.path, content: d.content })) };
}

const sandbox = { console, JSON, Object, Array, String, Number, Math, RegExp, Error, Promise, Map, Set, Date: FD, isFinite };
sandbox.window = sandbox; sandbox.globalThis = sandbox;
sandbox.UrbeArtifacts = { RE: { text: /\.(md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv)$/i, note: /\.(md|markdown)$/i, noteText: /\.(md|markdown|txt)$/i } };
let script = [], requests = [];
sandbox.UrbeAIProviders = {
  async stream(cfg, req, opts) {
    requests.push({ provider: cfg.id, model: req.model, system: req.system, messages: JSON.parse(JSON.stringify(req.messages)), tools: req.tools.map(t => t.name), maxTokens: req.maxTokens, temperature: req.temperature ?? null });
    const r = script.shift();
    if (!r) throw new Error('no response scripted');
    for (const e of r.events || []) opts.onEvent(e);
    if (opts.signal && opts.signal.aborted) { const e = new Error('Interrompido.'); e.name = 'AbortError'; throw e; }
    if (r.error) { const e = new Error(r.error); if (r.abort) e.name = 'AbortError'; throw e; }
    return { content: r.content, reasoning: r.reasoning || '', stopReason: r.stopReason || 'end', usage: r.usage || { input: 10, output: 5, cost: null }, model: r.model || 'm-1' };
  }
};
vm.createContext(sandbox);
vm.runInContext(readFileSync(join(root, 'src/ai/tools.js'), 'utf8'), sandbox, { filename: 'tools.js' });
vm.runInContext(readFileSync(join(root, 'src/ai/agent.js'), 'utf8'), sandbox, { filename: 'agent.js' });
const A = sandbox.UrbeAgent;

const text = t => ({ type: 'text', text: t });
const use = (id, name, input) => ({ type: 'tool_use', id, name, input });
const say = (t, extra) => ({ events: [{ type: 'text', delta: t.slice(0, 3) }, { type: 'text', delta: t.slice(3) }], content: [text(t)], ...extra });
const call = (calls, extra) => ({ events: calls.flatMap((c, i) => [{ type: 'tool_start', index: i, name: c.name }, { type: 'tool_delta', index: i, partial: '{"' }]), content: calls, stopReason: 'tool_use', ...extra });

const SCENARIOS = [
  { name: 'answer', script: [say('Olá, tudo certo.', { reasoning: 'pensando', usage: { input: 7, output: 3, cost: 0.001 } })] },
  { name: 'parallel-reads', script: [call([use('t1', 'read_note', { path: 'Comece' }), use('t2', 'read_note', { path: 'Plano' }), use('t3', 'get_editor_context', {})]), say('Li [[Comece]] e [[Plano]].')] },
  { name: 'write-approved', approve: ['approve'], script: [call([use('t1', 'edit_note', { path: 'Comece', old_text: '- uma', new_text: '- primeira' })]), say('Feito.')] },
  { name: 'write-rejected', approve: [{ reason: 'não quero' }], script: [call([use('t1', 'write_note', { path: 'Plano', content: 'novo' })]), say('Ok, não mudei.')] },
  { name: 'approve-all', approve: ['approve_all'], script: [call([use('t1', 'create_note', { path: 'A', content: 'a' }), use('t2', 'create_note', { path: 'B', content: 'b' })]), say('Criei duas.')] },
  { name: 'policy-auto', policy: 'auto', script: [call([use('t1', 'append_to_note', { path: 'Comece', content: 'fim' })]), say('Pronto.')] },
  { name: 'destructive-always-asks', policy: 'auto', approve: ['approve'], script: [call([use('t1', 'delete_note', { path: 'Plano' })]), say('Apaguei.')] },
  { name: 'readonly', policy: 'readonly', script: [say('Somente leitura.')] },
  { name: 'agent-writer', agent: 'escritor', script: [say('Escritor aqui.')] },
  { name: 'agent-researcher', agent: 'pesquisador', script: [say('Pesquisador aqui.')] },
  { name: 'unknown-invalid', script: [call([use('t1', 'nada', {}), use('t2', 'read_note', { start_line: 'x' })]), say('Erro.')] },
  { name: 'repeat-loop', script: [call([use('a1', 'read_note', { path: 'Zzz' })]), call([use('a2', 'read_note', { path: 'Zzz' })]), call([use('a3', 'read_note', { path: 'Zzz' })]), call([use('a4', 'read_note', { path: 'Zzz' })]), call([use('a5', 'read_note', { path: 'Zzz' })]), say('não')] },
  { name: 'nudge-empty-answer', script: [call([use('t1', 'list_notes', {})]), { events: [{ type: 'reasoning', delta: 'hmm' }], content: [], reasoning: 'hmm' }, say('Aqui está.')] },
  { name: 'partial-read', script: [call([use('t1', 'read_note', { path: 'Ideias' })]), say('Rascunho.'), call([use('t2', 'read_note', { path: 'Ideias', start_line: 20 })]), say('Completo.')], contextTokens: 20000 },
  { name: 'max-tokens', script: [{ events: [{ type: 'text', delta: 'Corta' }], content: [], stopReason: 'max_tokens' }] },
  { name: 'max-tokens-text', script: [{ events: [{ type: 'text', delta: 'Corta' }], content: [text('Corta')], stopReason: 'max_tokens' }] },
  { name: 'provider-error-after-text', script: [{ events: [{ type: 'text', delta: 'Come' }], error: 'Limite de uso atingido.' }] },
  { name: 'provider-error-empty', script: [{ events: [{ type: 'tool_start', index: 0, name: 'read_note' }], error: 'Falha de rede.' }] },
  { name: 'provider-abort', script: [{ events: [{ type: 'text', delta: 'Co' }], error: 'x', abort: true }] },
  { name: 'abort-in-approval', approve: ['abort'], script: [call([use('t1', 'write_note', { path: 'Plano', content: 'x' })]), say('nunca')] },
  { name: 'delegate', script: [call([use('d1', 'delegate_research', { task: 'O que há sobre plano?' })]), call([use('s1', 'grep_notes', { pattern: 'Passos' })]), say('Resumo: [[Plano]] fala de passos.', { usage: { input: 3, output: 2, cost: 0.5 } }), say('Segundo o pesquisador, [[Plano]].')] },
  { name: 'delegate-error', script: [call([use('d1', 'delegate_research', { task: 'x' })]), { error: 'Caiu.' }, say('O pesquisador falhou.')] },
  { name: 'max-steps', maxSteps: 2, script: [call([use('t1', 'list_notes', {})]), call([use('t2', 'list_notes', { folder: 'Projetos' })])] },
  { name: 'plan-and-remember', script: [call([use('t1', 'update_plan', { steps: [{ step: 'Ler', status: 'in_progress' }] }), use('t2', 'remember', { fact: 'Gosta de listas' })]), say('Anotado.')] },
  { name: 'history', history: [
      { role: 'user', content: [text('Primeiro pedido')] },
      { role: 'assistant', content: [text(''), use('h1', 'list_notes', {})] },
      { role: 'user', content: [text('Continue')] }
    ], script: [say('Continuando.')], contextTokens: 6000, instructions: '  Seja breve.  ', memory: ['Mora em Recife'], temperature: 0.3, maxTokens: 1000 }
];

const results = [];
for (const s of SCENARIOS) {
  const env = makeCore();
  script = JSON.parse(JSON.stringify(s.script)); requests = [];
  const approvals = (s.approve || []).slice(), events = [], memory = [], opened = [];
  const controller = new AbortController();
  const messages = (s.history || [{ role: 'user', content: [text('Pedido do usuário')] }]).map((m, i) => ({ id: 'h_' + i, ...JSON.parse(JSON.stringify(m)) }));
  const snap = m => ({ role: m.role, text: m.content.filter(b => b.type === 'text').map(b => b.text).join('|'), tools: m.content.filter(b => b.type === 'tool_use').map(b => b.name + (b._pending ? '*' : '') + (b._partial ? '~' : '')).join(','), reasoning: m.meta && m.meta.reasoning || null });
  const result = await A.run({
    agent: A.agentById(s.agent || 'geral'), policy: s.policy || 'ask', messages, signal: controller.signal, core: env.core, provider: { id: 'p1' }, model: 'm-1',
    editor: () => ({ path: 'Comece.md', selection: '' }), openNote: id => opened.push(id), remember: f => memory.push(f), vaultName: 'Cofre',
    contextTokens: s.contextTokens, instructions: s.instructions, memory: s.memory, maxSteps: s.maxSteps, temperature: s.temperature, maxTokens: s.maxTokens,
    approve: async req => {
      events.push({ type: 'approve', name: req.call.name, label: req.label, access: req.access, preview: req.preview ? JSON.parse(JSON.stringify(req.preview)) : null });
      const a = approvals.shift();
      if (a === 'abort') { controller.abort(); return 'approve'; }
      if (a && a.reason) return { reject: true, reason: a.reason };
      return a || 'approve';
    },
    onUpdate: e => {
      const o = { type: e.type };
      if (e.message) Object.assign(o, snap(e.message));
      if (e.call) o.call = e.call.name;
      if (e.status) o.status = e.status;
      if (e.result) o.result = { content: e.result.content, is_error: e.result.is_error, status: e.result._status, changes: e.result._changes };
      if (e.steps) o.steps = JSON.parse(JSON.stringify(e.steps));
      if (e.text) o.text = e.text;
      if (e.usage) o.usage = { ...e.usage };
      events.push(o);
    }
  });
  results.push({
    name: s.name, agent: s.agent || 'geral', policy: s.policy || 'ask', history: s.history || null, script: s.script, approve: s.approve || [],
    contextTokens: s.contextTokens ?? null, instructions: s.instructions ?? null, memory: s.memory ?? null, maxSteps: s.maxSteps ?? null, temperature: s.temperature ?? null, maxTokens: s.maxTokens ?? null,
    requests, events,
    messages: messages.map(m => { const { id, ...rest } = m; return JSON.parse(JSON.stringify(rest)); }),
    result: JSON.parse(JSON.stringify(result)), notes: env.notes(), remembered: memory, opened
  });
}

// prepare() on its own
const big = 'x'.repeat(1500);
const PREP = [
  { messages: [{ role: 'assistant', content: [text('oi')] }, { role: 'user', content: [text('a')] }], budget: 99999 },
  { messages: [{ role: 'user', content: [text('a'), text('')] }, { role: 'user', content: [text('b')] }, { role: 'assistant', content: [] }], budget: 99999 },
  { messages: [{ role: 'user', content: [text('a')] }, { role: 'assistant', content: [use('x', 'list_notes', {}), use('y', 'read_note', { path: 'a' })] }, { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'y', content: 'ok', is_error: false }, { type: 'tool_result', tool_use_id: 'z', content: 'órfão', is_error: false }] }], budget: 99999 },
  { messages: [{ role: 'user', content: [text('a')] }, { role: 'assistant', content: [use('x', 'list_notes', null)] }], budget: 99999 },
  { messages: [{ role: 'user', content: [{ type: 'tool_result', tool_use_id: 'q', content: 5, is_error: 0 }, text('b')] }], budget: 99999 },
  { messages: [{ role: 'user', content: [text('a')] }, { role: 'assistant', content: [use('x', 'read_note', { path: 'a' })] }, { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'x', content: big, is_error: false }] },
    { role: 'assistant', content: [text('r1')] }, { role: 'user', content: [text('b')] }, { role: 'assistant', content: [text('r2')] }, { role: 'user', content: [text('c')] }], budget: 1000 },
  { messages: [{ role: 'user', content: [text('a'.repeat(500))] }, { role: 'assistant', content: [text('r1'.repeat(200))] }, { role: 'user', content: [text('b'.repeat(300))] }, { role: 'assistant', content: [text('r2')] }, { role: 'user', content: [text('c')] }], budget: 600 },
  { messages: [{ role: 'user', content: [text('a'.repeat(5000))] }, { role: 'assistant', content: [text('b')] }], budget: 100 },
  { messages: [{ role: 'user', content: [text('a'.repeat(500))] }, { role: 'assistant', content: [use('k', 'list_notes', { folder: 'é' })] }, { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'k', content: 'r', is_error: true }, text('d'.repeat(400))] }, { role: 'assistant', content: [text('fim')] }, { role: 'user', content: [text('e')] }], budget: 500 }
];
const prepares = PREP.map(p => ({ messages: p.messages, budget: p.budget, out: JSON.parse(JSON.stringify(A.prepare(JSON.parse(JSON.stringify(p.messages)), p.budget))) }));

const prompts = [
  { agent: 'geral', policy: 'ask', vaultName: 'Cofre', noteCount: 3, openNote: 'Comece.md', instructions: null, memory: [] },
  { agent: 'organizador', policy: 'readonly', vaultName: null, noteCount: 0, openNote: null, instructions: '\n Use tópicos \n', memory: ['a', 'b'] }
].map(o => ({ ...o, out: A.systemPrompt({ ...o, agent: A.agentById(o.agent) }) }));

const titles = ['', '  um   pedido \n curto ', 'x'.repeat(48), 'y'.repeat(49), null].map(t => ({ text: t, out: A.titleFrom(t) }));
const agents = A.AGENTS.map(a => ({ ...a }));
const access = ['read', 'write', 'destructive', 'ui', 'memory', 'plan'];
const allow = [];
for (const a of A.AGENTS) for (const policy of ['ask', 'auto', 'readonly']) for (const ac of access)
  allow.push({ agent: a.id, policy, access: ac, allow: A.allowTool(a, policy, { access: ac }), approval: [false, true].map(r => A.needsApproval({ access: ac }, policy, r)) });

process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/ai/agent.js + src/ai/tools.js in node:vm, scripted model', notes: NOTES, agents, allow, scenarios: results, prepares, prompts, titles }, null, 1) + '\n');
