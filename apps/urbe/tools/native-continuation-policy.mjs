// The owner's mandate for the native Urbe (DEC-0043, and DEC-0051 without the visual simulation) must remain
// operational for every future Urbe agent.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
const base=resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const read=p=>readFileSync(resolve(base,p),'utf8');
const root=read('AGENTS.md');
const local=read('apps/urbe/AGENTS.md');
const roadmap=read('apps/urbe/docs/csharp/ROADMAP.md');
const policy=read('apps/urbe/docs/csharp/PRODUCT-DIRECTION.md');
const src=read('apps/urbe/csharp/src/Urbe.Client/UrbeApp.cs');
const adr=read('docs/adr/0032-urbe-ui-nativa-sem-webview.md');
const adr34=read('docs/adr/0034-urbe-sem-simulacao-visual.md');
for (const [name,content] of [['AGENTS',root],['Urbe AGENTS',local],['ROADMAP',roadmap]]) {
  for(const token of ['DEC-0051','ADD-0028','urbe-simulacao-congelada','1.8.4-beta','Avalonia','Blazor','vault','mapa.json'])
    assert.ok(content.includes(token),name+' missing '+token);
}
assert.match(root, /TODOS os agentes|todo agente presente ou futuro/i);
assert.match(local, /TODOS os agentes/i);
assert.match(roadmap, /TODOS os agentes/i);
// the simulation is out of the product and frozen, never brought back by an agent on its own
assert.match(root, /Nenhum agente a reintroduz/);
assert.match(local, /Não reintroduza nenhuma parte dela/);
assert.match(policy, /simulação visual saiu do produto/i);
assert.match(policy, /Explorer/);
assert.match(adr, /Urbe\.Client/);
assert.match(adr, /ADR-0034/);
assert.match(adr34, /notas\.json/);
assert.match(src, /new MainView\(\)/);
assert.doesNotMatch(src, /BlazorWebView|WebView2/);
console.log('Urbe native continuity mandate: ok');
