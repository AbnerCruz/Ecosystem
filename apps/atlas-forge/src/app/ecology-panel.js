import {
  PRESETS,
  seedEcosystem,
  environment,
  habitat,
} from "../core/ecosystem.js";
import { index } from "../geometry/sphere.js";
export function ecologyPanel(api) {
  const panel = document.createElement("details");
  panel.className = "section";
  panel.innerHTML = `<summary>Biosfera e eras</summary>
 <p class="micro">Estoques regionais em kg/m². Clima e alimento limitam as populações; vegetação reduz a erosão. Modelo agregado simplificado.</p>
 <p id="eco-summary"></p><div id="eco-region" class="micro"></div>
 <button id="eco-init" class="edit-action">Inicializar natureza neste projeto legado</button>
 <label>Espécie<select id="eco-species"></select></label>
 <button id="eco-new" class="edit-action">Nova espécie</button>
 <form id="eco-form">
 <label>Nome<input id="eco-name" required maxlength="180"></label>
 <label>Papel<select id="eco-role"><option value="producer">Produtor</option><option value="consumer">Consumidor</option><option value="decomposer">Decompositor</option></select></label>
 <label>Habitat<select id="eco-habitat"><option value="land">Terrestre</option><option value="water">Aquático</option></select></label>
 <label>Alimentos (selecione várias opções)<select id="eco-diet" multiple size="5"></select></label>
 <div id="eco-numbers"></div>
 <label><input id="eco-managed" type="checkbox"> Manejado (sem dispersão automática)</label>
 <button type="submit" class="edit-action">Salvar espécie</button></form>
 <label>Introdução em kg/m²<input id="eco-density" type="number" min="0.000001" max="10" step="any" value="0.01"></label>
 <label>Destino<select id="eco-target"><option value="local">Região selecionada no mapa</option><option value="all">Todos os habitats adequados</option></select></label>
 <button id="eco-introduce" class="edit-action">Introduzir espécie salva</button>
 <label>Duração da era<select id="eco-years"><option value="100">100 anos</option><option value="1000" selected>1.000 anos</option><option value="10000">10.000 anos</option></select></label>
 <button id="eco-advance" class="edit-action">Simular era e preservar marco anterior</button>
 <p id="eco-report" class="micro"></p>`;
  const sidebar = document.getElementById("left-panel");
  sidebar.insertBefore(panel, sidebar.querySelectorAll("details")[1]);
  const $ = (id) => panel.querySelector("#eco-" + id);
  const fields = [
    ["massKg", "Massa individual típica (kg)", 0.000001, 1e12],
    ["minTemp", "Temperatura mínima (°C)", -100, 100],
    ["maxTemp", "Temperatura máxima (°C)", -100, 100],
    ["minRain", "Chuva mínima (mm/ano)", 0, 10000],
    ["productivity", "Produtividade máxima (kg/m²/ano)", 0, 20],
    ["mortality", "Mortalidade (taxa/ano)", 0, 5],
    ["consumption", "Consumo (kg/kg/ano)", 0, 10],
    ["efficiency", "Assimilação (fração)", 0, 1],
    ["dispersalKm", "Dispersão (km/ano)", 0, 10000],
  ];
  for (const [key, label, min, max] of fields) {
    const l = document.createElement("label");
    l.textContent = label;
    const input = document.createElement("input");
    Object.assign(input, {
      id: "eco-" + key,
      type: "number",
      min,
      max,
      step: "any",
      required: true,
    });
    l.append(input);
    $("numbers").append(l);
  }
  let editing = null;
  function fill(s) {
    editing = s.id;
    $("name").value = s.name;
    $("role").value = s.role;
    $("habitat").value = s.habitat;
    $("managed").checked = s.managed;
    for (const [key] of fields) $(key).value = s[key];
    $("diet").replaceChildren();
    for (const prey of [
      ...(api.getWorld()?.state.species ?? []),
      { id: "detritus", name: "Detritos" },
    ]) {
      if (prey.id === s.id) continue;
      const o = new Option(prey.name, prey.id);
      o.selected = s.diet.includes(prey.id);
      $("diet").append(o);
    }
  }
  function guard(action) {
    try {
      action();
    } catch (e) {
      api.error(e);
    }
  }
  $("species").onchange = () => {
    const s = api
      .getWorld()
      ?.state.species.find((s) => s.id === $("species").value);
    if (s) fill(s);
  };
  $("new").onclick = () =>
    fill({
      ...structuredClone(PRESETS[2]),
      id: crypto.randomUUID(),
      name: "Nova espécie",
      diet: [],
    });
  $("init").onclick = () =>
    guard(() => {
      const draft = structuredClone(api.getWorld());
      seedEcosystem(draft, api.getDerived());
      api.commit(draft);
    });
  $("form").onsubmit = (e) => {
    e.preventDefault();
    guard(() => {
      const draft = structuredClone(api.getWorld());
      if (!draft?.state.ecosystem)
        throw Error("Inicialize a biosfera primeiro.");
      const s = {
        id: editing ?? crypto.randomUUID(),
        name: $("name").value,
        role: $("role").value,
        habitat: $("habitat").value,
        managed: $("managed").checked,
        diet:
          $("role").value === "producer"
            ? []
            : [...$("diet").selectedOptions].map((o) => o.value),
      };
      for (const [key] of fields) s[key] = Number($(key).value);
      const i = draft.state.species.findIndex((x) => x.id === s.id);
      if (i < 0) {
        draft.state.species.push(s);
        draft.state.ecosystem.biomass[s.id] = Array(
          draft.state.ecosystem.nutrients.length,
        ).fill(0);
      } else draft.state.species[i] = s;
      api.commit(draft);
      $("species").value = s.id;
      fill(s);
    });
  };
  $("introduce").onclick = () =>
    guard(() => {
      const draft = structuredClone(api.getWorld()),
        eco = draft?.state.ecosystem,
        s = draft?.state.species.find((s) => s.id === editing);
      if (!eco || !s) throw Error("Salve uma espécie antes de introduzi-la.");
      const density = Number($("density").value);
      if (!Number.isFinite(density) || density <= 0 || density > 10)
        throw Error("Densidade deve estar entre 0 e 10 kg/m².");
      const p = api.getLocation();
      if ($("target").value === "local" && !p)
        throw Error("Selecione uma região no mapa.");
      const env = environment(draft, api.getDerived());
      const cells =
        $("target").value === "local"
          ? [index(p.lat, p.lon, eco.grid)]
          : Array.from({ length: eco.nutrients.length }, (_, i) => i);
      let count = 0;
      for (const i of cells)
        if (habitat(s, env.elevation[i], env.temperature[i], env.rain[i]) > 0) {
          eco.biomass[s.id][i] += density;
          count++;
        }
      if (!count) throw Error("Nenhum habitat adequado no destino escolhido.");
      eco.lastEra = null;
      api.commit(draft);
    });
  $("advance").onclick = () => api.advance(Number($("years").value));
  function refresh() {
    const w = api.getWorld(),
      eco = w?.state.ecosystem;
    const previous = editing;
    $("summary").textContent = eco
      ? `Ano ${eco.year.toLocaleString("pt-BR")} · ${w.state.species.length} espécies · ${eco.grid.width} × ${eco.grid.height} regiões ecológicas`
      : "Biosfera ainda não inicializada.";
    $("init").hidden = !!eco;
    $("species").replaceChildren(
      ...(w?.state.species ?? []).map((s) => new Option(s.name, s.id)),
    );
    $("species").value = w?.state.species.some((s) => s.id === previous)
      ? previous
      : (w?.state.species[0]?.id ?? "");
    $("species").onchange();
    const r = eco?.lastEra;
    $("report").textContent = r
      ? `Era ${r.start}–${r.end}: ${r.sedimentM3.toExponential(3)} m³ de sedimentos transportados. Resíduo do balanço de N: ${r.nitrogenErrorKg.toExponential(2)} kg. Balanço orgânico: ${r.organicErrorKg.toExponential(2)} kg. Erosão acoplada a cada 100 anos; células desenhadas ficam fixas.`
      : "A era calcula recursos e populações anualmente. Cancelar preserva o estado anterior. Introduções são aportes externos explícitos.";
  }
  function inspect() {
    const world = api.getWorld(),
      eco = world?.state.ecosystem,
      p = api.getLocation(),
      box = $("region");
    box.replaceChildren();
    if (!eco || !p) return;
    const i = index(p.lat, p.lon, eco.grid),
      heading = document.createElement("p");
    heading.textContent = `Região selecionada: N mineral ${eco.nutrients[i].toFixed(4)} kg/m² · detritos ${eco.detritus[i].toFixed(4)} kg/m²`;
    box.append(heading);
    for (const s of world.state.species) {
      const row = document.createElement("div");
      row.textContent = `${s.name}: ${eco.biomass[s.id][i].toFixed(6)} kg/m²`;
      box.append(row);
    }
  }
  refresh();
  return { refresh, inspect };
}
