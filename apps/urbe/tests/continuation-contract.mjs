// Protect the Urbe C# continuation contract against accidental return to
// a redesigned city, static PNG sprites or new Web/PWA distribution.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const read = (path) => readFileSync(join(root, path), 'utf8');
const roadmap = read('docs/csharp/ROADMAP.md');
const visual = read('docs/csharp/UC19-VISUAL-ACCEPTANCE.md');
const procedural = read('docs/csharp/UC19-PROCEDURAL-CITY.md');
const world = read('csharp/src/Urbe.UI/WorldArt.cs');
const worldUi = read('csharp/src/Urbe.UI/Pages/World.razor');
const css = read('csharp/src/Urbe.UI/wwwroot/urbe.css');

assert.match(roadmap, /## Contrato de continuidade — comando "continue o Urbe"/);
assert.match(roadmap, /Android primeiro; Windows depois; nenhum novo Web\/PWA público/);
assert.match(roadmap, /mundo top-down contínuo original 1\.8\.4-beta/);
assert.match(roadmap, /CI verde não aprova automaticamente qualidade visual nem DEVICE/);
assert.match(roadmap, /Identidade obrigatória do produto — jogo\/simulador de mundo aberto/);
assert.match(roadmap, /src\/world\/life\.js/);
assert.match(roadmap, /JOGO PRIMEIRO/);
assert.match(roadmap, /simulação temporal/);
assert.match(roadmap, /Não aceitar como produto pronto/);
assert.match(visual, /geração procedural C# em execução/);
assert.match(visual, /oráculo de comparação/);
assert.match(visual, /PR #423 .*rejeitado\/fechado/);
assert.doesNotMatch(visual, /o app recebe \*\*imagens estáticas\*\*/);
assert.doesNotMatch(visual, /exportar PNGs fiéis de/);
assert.match(procedural, /PR #397 \*\*integrado\*\*/);
assert.match(procedural, /PR #425 \*\*integrado\*\*/);
assert.doesNotMatch(procedural, /PR subordinado à branch Android/);

for (const fn of [
  'LegacyWorldPixelTextures.CreateTile',
  'LegacyWorldGroundDecor.Paint',
  'LegacyWorldSprites.CreateTree',
  'LegacyWorldBuildings.CreateBuilding',
]) assert.ok(world.includes(fn), 'C# runtime art source missing: ' + fn);
assert.match(worldUi, /@WorldArt\.BoardBackgroundStyle/);
assert.doesNotMatch(world + worldUi + css, /world\/v184\/[A-Za-z0-9_-]+\.png/);
console.log('ok: single continuation protocol + original runtime procedural art preserved');
