import { SURFACE_MODEL } from "../rendering/surface-field.js";
import { escape } from "../documents/markdown.js";
import { validate } from "../storage/project.js";
// Replacing < in JSON also prevents closing script tags from hostile note text.
export const scriptData = (data) =>
  JSON.stringify(data)
    .replace(/</g, "\\u003c")
    .replace(/\u2028/g, "\\u2028")
    .replace(/\u2029/g, "\\u2029");
export function publish(world, derived) {
  const project = validate(world),
    layers = {};
  for (const key of [
    "solarAnnual",
    "solarJune",
    "solarDecember",
    "temperature",
    "summer",
    "winter",
    "rain",
    "wind",
    "downstream",
    "basin",
    "discharge",
    "terminal",
    "biome",
  ])
    layers[key] = Array.from(derived[key]);
  const data = {
    name: project.state.name,
    definition: project.definition,
    state: project.state,
    derived: { ...layers, solarModel: derived.solarModel },
    surfaceModel: SURFACE_MODEL,
  };
  return `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: blob:; worker-src blob:; connect-src 'none'; base-uri 'none'; form-action 'none'"><title>${escape(data.name)} · Atlas publicado</title><style>${__VIEWER_CSS__}</style></head><body><header><div><span class="eyebrow">ATLAS FORGE · EDIÇÃO DE LEITURA</span><h1 id="title"></h1></div><div><select id="layer" aria-label="Camada"></select><select id="season" aria-label="Estação"><option value="annual">Média anual</option><option value="june">Junho</option><option value="december">Dezembro</option></select><select id="scale" aria-label="Escala"><option value="planet">Mundo inteiro</option><option value="500">500 m/pixel</option><option value="5">5 m/pixel</option><option value="0.03">3 cm/pixel</option></select><button id="projection">◉ Globo</button></div></header><main><section class="map-shell"><canvas id="map" aria-label="Mapa interativo publicado" tabindex="0"></canvas><div class="viewer-zoom"><button id="plus" aria-label="Aproximar">+</button><button id="minus" aria-label="Afastar">−</button><button id="reset" aria-label="Enquadrar planeta">⌖</button></div><p id="coords">Selecione um lugar ou uma célula.</p></section><aside><span class="eyebrow">LUGARES & HISTÓRIAS</span><div id="places"></div><h2 id="entity-title">Explore este mundo</h2><p id="description"></p><article id="note"></article><button id="inspect" hidden>Inspecionar mapa local ↗</button><p class="micro">Publicação offline e imutável. Insolação em W/m² no topo da atmosfera, órbita circular. Clima e biomas heurísticos; drenagem simplificada. Nenhum solver carregado. Notas são cópias, sem acesso ao vault.</p></aside></main><dialog id="local"><button id="back">← Voltar ao planeta</button><h2 id="local-title"></h2><canvas id="local-map"></canvas><p id="room"></p></dialog><script id="atlas-data" type="application/json">${scriptData(data)}</script><script>${__VIEWER_SOURCE__}</script></body></html>`;
}
