// UC-21 oracle: the provider layer of the UNMODIFIED Urbe 1.8.4-beta assistant (src/ai/providers.js) in node:vm with
// a scripted fetch. Each scenario gives a provider config, a request and the HTTP responses the server sends; the
// oracle records every request the original makes (URL, method, headers, body), the stream events, the normalized
// result or the error message, and the models list. Timers run at once (retries do not wait).
// Test-only tool: never part of the native app.
//
//   node csharp/tools/assistant-providers-oracle.mjs > csharp/tests/fixtures/assistant/providers-run-a.json
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
let queue = [], seen = [];
function headersOf(h) { const m = new Map(Object.entries(h || {}).map(([k, v]) => [k.toLowerCase(), v])); return { get: k => m.get(k.toLowerCase()) ?? null }; }
function response(r) {
  const body = typeof r.body === 'string' ? r.body : JSON.stringify(r.body ?? '');
  const bytes = new TextEncoder().encode(body);
  // the body arrives in small pieces, cut in the middle of lines and of UTF-8 characters
  const cut = r.chunk || 7;
  let at = 0;
  return {
    ok: r.status >= 200 && r.status < 300, status: r.status, headers: headersOf(r.headers),
    text: async () => body, json: async () => JSON.parse(body),
    body: { getReader: () => ({ read: async () => at >= bytes.length ? { done: true } : { done: false, value: bytes.slice(at, at += cut) }, cancel() {} }) }
  };
}
const ctx = {
  console, JSON, Object, Array, String, Number, Math, RegExp, Error, Promise, Map, Set, Date, TextDecoder, TextEncoder, isFinite,
  setTimeout: (f) => { Promise.resolve().then(f); return 0; }, clearTimeout: () => {},
  fetch: async (url, init) => {
    seen.push({ url, method: (init && init.method) || 'GET', headers: (init && init.headers) || {}, body: init && init.body ? JSON.parse(init.body) : null });
    const r = queue.shift();
    if (!r) throw new Error('no response scripted');
    if (r.network) throw new TypeError('Failed to fetch');
    return response(r);
  }
};
ctx.window = ctx; ctx.globalThis = ctx;
vm.createContext(ctx);
vm.runInContext(readFileSync(join(root, 'src/ai/providers.js'), 'utf8'), ctx, { filename: 'providers.js' });
const P = ctx.UrbeAIProviders;

const sse = events => events.map(e => (e.event ? 'event: ' + e.event + '\n' : '') + 'data: ' + (typeof e.data === 'string' ? e.data : JSON.stringify(e.data)) + '\n\n').join('');
const SSE = { 'content-type': 'text/event-stream' };
const openrouter = { id: 'or', preset: 'openrouter', name: '', apiKey: ' sk-or-1 ', baseUrl: '' };
const anthropicCfg = { id: 'an', preset: 'anthropic', apiKey: 'sk-ant-1' };
const localCfg = { id: 'lo', preset: 'ollama' };
const tools = [{ name: 'read_note', description: 'Lê uma nota', parameters: { type: 'object', properties: { path: { type: 'string' } }, required: ['path'] } }];
const conversation = [
  { role: 'user', content: [{ type: 'text', text: 'Resuma [[Plano]]' }] },
  { role: 'assistant', content: [{ type: 'text', text: 'Vou ler.' }, { type: 'tool_use', id: 'c1', name: 'read_note', input: { path: 'Plano.md' } }] },
  { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'c1', content: '# Plano\nPassos', is_error: false }] },
  { role: 'assistant', content: [{ type: 'tool_use', id: 'c2', name: 'read_note', input: {} }] },
  { role: 'user', content: [{ type: 'tool_result', tool_use_id: 'c2', content: 'falhou', is_error: true }, { type: 'text', text: 'e agora?' }] },
  { role: 'user', content: [{ type: 'text', text: '' }, { type: 'text', text: 'mais' }] }
];
const request = { model: 'm-1', system: 'Sistema', messages: conversation, tools, maxTokens: 512, temperature: 0.2 };

const scenarios = [
  { name: 'openai-text-and-tools', cfg: openrouter, req: request, responses: [{ status: 200, headers: SSE, chunk: 5, body: sse([
    { data: { model: 'org/m-1', choices: [{ delta: { reasoning: 'pensa' } }] } },
    { data: { choices: [{ delta: { content: 'Olá, ' } }] } },
    { data: { choices: [{ delta: { content: 'ação ✓' } }] } },
    { data: { choices: [{ delta: { tool_calls: [{ index: 0, id: 'call_a', function: { name: 'read_', arguments: '' } }] } }] } },
    { data: { choices: [{ delta: { tool_calls: [{ index: 0, function: { name: 'note', arguments: '{"pa' } }] } }] } },
    { data: { choices: [{ delta: { tool_calls: [{ index: 0, function: { arguments: 'th":"A.md"}' } }, { index: 2, id: 'call_c', function: { name: 'list_notes', arguments: { folder: 'X' } } }] } }] } },
    { data: { choices: [{ delta: {}, finish_reason: 'tool_calls' }], usage: { prompt_tokens: 120, completion_tokens: 30, cost: '0.0012' } } },
    { data: '[DONE]' }
  ]) }] },
  { name: 'openai-length-and-loose-args', cfg: { id: 'c', preset: 'custom', baseUrl: 'http://srv:8080/v1/' }, req: { model: 'x', messages: [{ role: 'user', content: [{ type: 'text', text: 'oi' }] }] }, responses: [{ status: 200, headers: SSE, body:
    ': comentário\r\n' + sse([{ data: { choices: [{ delta: { content: 'parcial' }, finish_reason: 'length' }] } }]).replace(/\n/g, '\r\n') }] },
  { name: 'openai-truncated-tool-json', cfg: localCfg, req: { model: 'llama', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }], tools }, responses: [{ status: 200, headers: SSE, body: sse([
    { data: { choices: [{ delta: { tool_calls: [{ index: 0, id: 't1', function: { name: 'read_note', arguments: '{"path":"A' } }] } }] } },
    { data: { choices: [{ delta: { tool_calls: [{ index: 1, id: 't2', function: { name: 'read_note', arguments: '{"path":"B"' } }] } }] } },
    { data: { choices: [{ delta: { tool_calls: [{ index: 2, id: 't3', function: { name: 'read_note', arguments: 'nada' } }] } }] } }
  ]) }] },
  { name: 'openai-json-not-stream', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 200, headers: { 'content-type': 'application/json' }, body: { model: 'm-b', choices: [{ message: { content: 'inteiro' }, finish_reason: 'stop' }], usage: { prompt_tokens: 3, completion_tokens: 1 } } }] },
  { name: 'openai-error-in-stream', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 200, headers: SSE, body: sse([{ data: { error: { code: 402, message: 'no credits' } } }]) }] },
  { name: 'anthropic-text-thinking-tools', cfg: anthropicCfg, req: request, responses: [{ status: 200, headers: SSE, chunk: 11, body: sse([
    { event: 'message_start', data: { type: 'message_start', message: { model: 'claude-x', usage: { input_tokens: 10, cache_read_input_tokens: 5, output_tokens: 1 } } } },
    { event: 'content_block_start', data: { type: 'content_block_start', index: 0, content_block: { type: 'thinking' } } },
    { event: 'content_block_delta', data: { type: 'content_block_delta', index: 0, delta: { type: 'thinking_delta', thinking: 'hmm' } } },
    { event: 'content_block_start', data: { type: 'content_block_start', index: 1, content_block: { type: 'text', text: '' } } },
    { event: 'content_block_delta', data: { type: 'content_block_delta', index: 1, delta: { type: 'text_delta', text: 'Lendo ' } } },
    { event: 'content_block_delta', data: { type: 'content_block_delta', index: 1, delta: { type: 'text_delta', text: 'já.' } } },
    { event: 'content_block_start', data: { type: 'content_block_start', index: 2, content_block: { type: 'tool_use', id: 'tu_1', name: 'read_note' } } },
    { event: 'content_block_delta', data: { type: 'content_block_delta', index: 2, delta: { type: 'input_json_delta', partial_json: '{"path":' } } },
    { event: 'content_block_delta', data: { type: 'content_block_delta', index: 2, delta: { type: 'input_json_delta', partial_json: '"B.md"}' } } },
    { event: 'message_delta', data: { type: 'message_delta', delta: { stop_reason: 'tool_use' }, usage: { output_tokens: 42 } } },
    { event: 'ping', data: { type: 'ping' } },
    { event: 'message_stop', data: { type: 'message_stop' } }
  ]) }] },
  { name: 'anthropic-overloaded', cfg: anthropicCfg, req: { model: 'c', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 200, headers: SSE, body: sse([{ event: 'error', data: { type: 'error', error: { type: 'overloaded_error', message: 'Overloaded' } } }]) }] },
  { name: 'http-401', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 401, body: { error: { message: 'bad key' } } }] },
  { name: 'http-402', cfg: anthropicCfg, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 402, body: { error: { message: 'pay' } } }] },
  { name: 'http-404-model', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 404, body: { error: { message: 'No such model: m' } } }] },
  { name: 'http-400-tools', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 400, body: { error: { message: 'No endpoints found that support tool use' } } }] },
  { name: 'http-413', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 413, body: 'payload too large <html>' }] },
  { name: 'http-400-object-error', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ status: 400, body: { error: { code: 'x', detail: { a: 1 } } } }] },
  { name: 'http-429-then-ok', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [
    { status: 429, headers: { 'retry-after': '2' }, body: { error: { message: 'slow down' } } },
    { status: 503, body: 'down' },
    { status: 200, headers: SSE, body: sse([{ data: { choices: [{ delta: { content: 'ok' }, finish_reason: 'stop' }] } }]) }] },
  { name: 'http-500-four-times', cfg: openrouter, req: { model: 'm', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [
    { status: 500, body: { message: 'boom' } }, { status: 500, body: { message: 'boom' } }, { status: 500, body: { message: 'boom' } }, { status: 500, body: { message: 'boom' } }] },
  { name: 'network-local', cfg: localCfg, req: { model: 'llama', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ network: true }, { network: true }, { network: true }] },
  { name: 'network-then-ok', cfg: anthropicCfg, req: { model: 'c', messages: [{ role: 'user', content: [{ type: 'text', text: 'x' }] }] }, responses: [{ network: true }, { status: 200, headers: SSE, body: sse([{ event: 'message_stop', data: { type: 'message_stop' } }]) }] },
  { name: 'missing-key', cfg: { id: 'o', preset: 'openai' }, req: { model: 'm', messages: [] }, responses: [] },
  { name: 'missing-url', cfg: { id: 'c', preset: 'custom' }, req: { model: 'm', messages: [] }, responses: [] },
  { name: 'missing-model', cfg: openrouter, req: { model: '', messages: [] }, responses: [] }
];

const results = [];
for (const s of scenarios) {
  queue = s.responses.slice(); seen = [];
  const events = [];
  let result = null, error = null;
  try { result = await P.stream(s.cfg, s.req, { onEvent: e => events.push(e) }); }
  catch (e) { error = { message: e.message, status: e.status ?? null, network: !!e.network }; }
  results.push({ name: s.name, cfg: s.cfg, req: s.req, responses: s.responses, requests: seen, events, result, error });
}

const models = [];
for (const m of [
  { name: 'openrouter-models', cfg: openrouter, responses: [{ status: 200, body: { data: [{ id: 'a/b', name: 'B', context_length: 8000, pricing: { prompt: '0.000001', completion: '0.000002' }, supported_parameters: ['tools', 'temperature'] }, { id: 'c', supported_parameters: ['x'] }, { name: 'só nome' }] } }] },
  { name: 'anthropic-models', cfg: anthropicCfg, responses: [{ status: 200, body: { data: [{ id: 'claude-1', display_name: 'Claude 1' }, { id: 'claude-2' }] } }] },
  { name: 'anthropic-models-fallback', cfg: anthropicCfg, responses: [{ status: 404, body: { error: { message: 'nope' } } }] },
  { name: 'anthropic-models-401', cfg: anthropicCfg, responses: [{ status: 401, body: { error: { message: 'bad' } } }] }
]) {
  queue = m.responses.slice(); seen = [];
  let list = null, error = null;
  try { list = await P.listModels(m.cfg); } catch (e) { error = { message: e.message, status: e.status ?? null }; }
  models.push({ name: m.name, cfg: m.cfg, responses: m.responses, requests: seen, list, error });
}

process.stdout.write(JSON.stringify({ source: 'apps/urbe 1.8.4-beta src/ai/providers.js in node:vm with a scripted fetch', presets: P.PRESETS, scenarios: results, models }, null, 1) + '\n');
