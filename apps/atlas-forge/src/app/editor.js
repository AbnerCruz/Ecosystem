import { ecologyPanel } from "./ecology-panel.js";
import {
  definition,
  stats,
  brush,
  record,
  history,
  checkpoint,
  restore,
} from "../core/world.js";
import { index, clamp, area } from "../geometry/sphere.js";
import { BIOMES } from "../solvers/climate.js";
import {
  MapRenderer,
  LAYERS,
  LEGENDS,
  SOLAR_FIELDS,
} from "../rendering/map.js";
import { bindPinch } from "../rendering/gestures.js";
import { drawLocal } from "../rendering/local.js";
import { localMap } from "../generation/local-map.js";
import { markdown, noteFromFile, verifyNotes } from "../documents/markdown.js";
import {
  parseProject,
  serialize,
  download,
  MAX_BYTES,
} from "../storage/project.js";
import { loadLocal, saveLocal } from "../storage/local.js";
import { publish } from "../publishing/export.js";
const $ = (id) => document.getElementById(id),
  renderer = new MapRenderer($("map"));
let creating = false;
let world,
  derived,
  worker,
  job = 0,
  busy = false,
  tool = "inspect",
  selected,
  location,
  drag,
  saveTimer,
  persistenceQueue = Promise.resolve();
function status(message) {
  $("status").textContent = message;
}
function error(e) {
  status(e.message || String(e));
}
function setBusy(value) {
  busy = value;
  $("busy").hidden = !value;
  document
    .querySelectorAll(".edit-action,#publish,#save-project,#undo,#redo")
    .forEach(
      (b) =>
        (b.disabled =
          value ||
          (!world &&
            ["publish", "save-project", "undo", "redo"].includes(b.id))),
    );
}
function cancel(quiet = false) {
  if (worker) {
    worker.terminate();
    worker = null;
  }
  job++;
  setBusy(false);
  if (world && derived) {
    renderer.setWorld(world, derived);
    renderer.render();
  }
  if (!quiet && creating && world) {
    creating = false;
    syncConfig();
    $("world-name").value = world.state.name;
  }
  if (!quiet) status("Operação cancelada; último estado preservado.");
}
function syncConfig() {
  const d = world.definition,
    p = d.planet;
  $("seed").value = d.seed;
  $("ocean").value = p.ocean;
  $("radius").value = p.radius / 1000;
  $("tilt").value = p.tilt;
  $("base-temp").value = p.baseTemp;
  const resolution = $("resolution");
  if (![...resolution.options].some((o) => Number(o.value) === d.grid.width))
    resolution.add(
      new Option(
        `${d.grid.width} × ${d.grid.height} · importado`,
        d.grid.width,
      ),
    );
  resolution.value = d.grid.width;
  resolution.disabled = true;
  $("refine-terrain").hidden = d.grid.width >= 1440;
  const brushSize = $("brush-size");
  const minimum = 360 / d.grid.width;
  brushSize.min = minimum;
  brushSize.max = Math.max(18, minimum);
  brushSize.value = Math.max(minimum, Number(brushSize.value));
  $("brush-radius").value = brushSize.value + "°";
}
function autosave() {
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    const snapshot = structuredClone(world);
    persistenceQueue = persistenceQueue
      .catch(() => {})
      .then(async () => {
        try {
          await saveLocal(snapshot);
          status(
            "Cópia local verificada · exporte JSON para manter um backup.",
          );
        } catch {
          status(
            "Cópia local indisponível. Use Salvar projeto para baixar JSON.",
          );
        }
      });
  }, 600);
}
function refresh() {
  renderer.selected = selected;
  renderer.setWorld(world, derived);
  renderer.render();
  $("world-name").value = world.state.name;
  $("project-name").textContent = world.state.name;
  $("world-title").firstChild.textContent = world.state.name + " ";
  document.querySelector(".map-label").firstChild.textContent = world.state.name
    .toUpperCase()
    .split("")
    .join(" ");
  const s = stats(world, derived);
  $("world-stats").textContent =
    `Modelos regionais: ${world.definition.grid.width} × ${world.definition.grid.height} células · ${s.ocean.toFixed(1)}% oceanos · cume ${Math.round(s.max).toLocaleString("pt-BR")} m`;
  $("undo").disabled = busy || !world.journal.undo.length;
  $("redo").disabled = busy || !world.journal.redo.length;
  const timeline = $("timeline");
  timeline.replaceChildren();
  const o = document.createElement("option");
  o.value = "";
  o.textContent = "Escolha um marco";
  timeline.append(o);
  for (const c of world.timeline.checkpoints) {
    const option = document.createElement("option");
    option.value = c.id;
    option.textContent = `${c.parent ? "↳ " : ""}${c.label}`;
    timeline.append(option);
  }
  timeline.value = world.timeline.active ?? "";
  ecology.refresh();
  renderEntities();
  inspectEntity();
  if (location) inspectLocation(location);
  autosave();
}
function run(type, draft, def, overrides = {}, onComplete) {
  cancel(true);
  const id = ++job;
  setBusy(true);
  $("progress").value = 0;
  $("stage").textContent =
    type === "generate"
      ? "Preparando superfície…"
      : "Recalculando dependências…";
  const url = URL.createObjectURL(
    new Blob([__WORKER_SOURCE__], { type: "text/javascript" }),
  );
  worker = new Worker(url);
  URL.revokeObjectURL(url);
  worker.onmessage = ({ data }) => {
    if (data.id !== job) return;
    if (data.progress) {
      $("stage").textContent = data.progress.stage;
      $("progress").value = data.progress.value;
      return;
    }
    if (data.error) {
      cancel(true);
      status(data.error);
      return;
    }
    const result = data.result;
    if (type === "generate" && draft) {
      result.world.state = {
        ...draft.state,
        elevation: result.world.state.elevation,
        overrides: result.world.state.overrides,
      };
      result.world.journal = structuredClone(draft.journal);
      result.world.timeline = structuredClone(draft.timeline);
    }
    try {
      serialize(result.world);
    } catch (e) {
      cancel(true);
      error(e);
      return;
    }
    creating = false;
    world = result.world;
    $("empty-state").hidden = true;
    derived = result.derived;
    worker.terminate();
    worker = null;
    setBusy(false);
    onComplete?.();
    syncConfig();
    refresh();
    status("Mundo pronto · explore, transforme e conte histórias.");
  };
  worker.onerror = (e) => {
    cancel(true);
    status(`Falha no cálculo: ${e.message}`);
  };
  worker.postMessage({ id, type, world: draft, definition: def, overrides });
}
function deriveDraft(draft, done) {
  run("derive", draft, undefined, {}, done);
}
function inspectLocation(p) {
  location = p;
  ecology.inspect();
  renderer.location = p;
  renderer.render();
  const i = index(p.lat, p.lon, world.definition.grid),
    end = derived.basin[i],
    terminal = derived.terminal[end];
  $("location-title").textContent = BIOMES[derived.biome[i]];
  $("location-subtitle").textContent =
    `${Math.abs(p.lat).toFixed(renderer.detailActive ? 7 : 2)}° ${p.lat >= 0 ? "N" : "S"} · ${Math.abs(p.lon).toFixed(renderer.detailActive ? 7 : 2)}° ${p.lon >= 0 ? "E" : "W"}`;
  const local = renderer.detailedLocation(p);
  $("metric-elevation").textContent = local
    ? `${local.elevation.toLocaleString("pt-BR", { maximumFractionDigits: 2 })} m · local`
    : `${Math.round(world.state.elevation[i]).toLocaleString("pt-BR")} m`;
  if (local)
    $("location-title").textContent = local.materialName + " · terreno local";
  const row = Math.floor(i / world.definition.grid.width);
  $("metric-solar").textContent =
    `${derived[SOLAR_FIELDS[renderer.season]][row].toFixed(1)} W/m²`;
  const pitch = Number(renderer.canvas.dataset.terrainSpacing);
  $("metric-area-label").textContent = local
    ? "ÁREA DA AMOSTRA LOCAL"
    : "ÁREA DA CÉLULA GLOBAL";
  $("metric-area").textContent = local
    ? `${(pitch * pitch * Math.cos((p.lat * Math.PI) / 180)).toLocaleString("pt-BR", { maximumFractionDigits: 3 })} m²`
    : `${(area(row, world.definition.grid, world.definition.planet.radius) / 1e6).toLocaleString("pt-BR", { maximumFractionDigits: 1 })} km²`;
  $("metric-temp").textContent = `${derived.temperature[i].toFixed(1)} °C`;
  $("metric-rain").textContent = `${Math.round(derived.rain[i])} mm/ano`;
  $("metric-flow").textContent =
    `${Math.round(derived.discharge[i]).toLocaleString("pt-BR")} m³/s`;
  $("basin-info").textContent =
    world.state.elevation[i] <= 0
      ? "Célula oceânica · datum: nível do mar."
      : `Bacia ${end} · ${terminal === 2 ? "fechada: retém água em depressão; extensão de lago não modelada" : "saída oceânica"} · runoff assumido: 35% da chuva.`;
  $("latitude").value = p.lat.toFixed(renderer.detailActive ? 7 : 2);
  $("longitude").value = p.lon.toFixed(renderer.detailActive ? 7 : 2);
  $("coordinates").textContent =
    `LAT ${p.lat.toFixed(renderer.detailActive ? 7 : 2)}°  LON ${p.lon.toFixed(renderer.detailActive ? 7 : 2)}°`;
}
const types = {
  castle: "Castelo",
  city: "Cidade",
  ruin: "Ruína",
  dungeon: "Dungeon",
  cave: "Caverna",
  village: "Aldeia",
  camp: "Acampamento",
  temple: "Templo",
  port: "Porto",
  realm: "Reino · sede",
};
function renderEntities() {
  const term = $("search").value.toLowerCase(),
    list = $("entities");
  list.replaceChildren();
  $("entity-count").textContent = world.state.entities.length;
  for (const e of world.state.entities.filter((e) =>
    e.name.toLowerCase().includes(term),
  )) {
    const b = document.createElement("button");
    b.className = "entity" + (e.id === selected ? " selected" : "");
    const symbol = document.createElement("span");
    symbol.className = "entity-icon";
    symbol.textContent = e.type === "city" ? "♜" : "♖";
    const text = document.createElement("span"),
      name = document.createElement("strong"),
      detail = document.createElement("small");
    name.textContent = e.name;
    detail.textContent = `${types[e.type] ?? e.type} · ${e.note ? "nota vinculada" : "sem nota"}`;
    text.append(name, detail);
    b.append(symbol, text);
    b.onclick = () => select(e);
    list.append(b);
  }
  if (!list.children.length) {
    const p = document.createElement("p");
    p.className = "micro";
    p.textContent = term
      ? "Nenhum lugar encontrado."
      : "Use ⚑ Lugar para começar uma história.";
    list.append(p);
  }
}
function select(e) {
  selected = e.id;
  renderer.selected = selected;
  renderer.render();
  inspectLocation(e);
  renderer.focus(e.lat, e.lon);
  renderer.render();
  renderEntities();
  inspectEntity();
  if (innerWidth <= 900) {
    $("right-panel").classList.add("panel-open");
    $("left-panel").classList.remove("panel-open");
  }
}
function inspectEntity() {
  const e = world.state.entities.find((e) => e.id === selected);
  $("entity-editor").hidden = !e;
  if (!e) return;
  $("entity-name").value = e.name;
  $("entity-type").value = types[e.type] ? e.type : "castle";
  $("entity-desc").value = e.desc;
  $("note-preview").innerHTML = e.note ? markdown(e.note.markdown) : "";
  $("note-info").textContent = e.note
    ? `${e.note.name} · cópia incorporada · SHA-256 ${e.note.hash ? e.note.hash.slice(0, 12) + "…" : "pendente (legado)"}`
    : "O arquivo de origem permanece intacto. Importe novamente para atualizar a cópia.";
  $("unlink-note").hidden = !e.note;
  $("inspect-local").hidden = !e.localMap;
  $("generate-local").hidden = !!e.localMap;
}
function mutate(action) {
  if (!world) return;
  cancel(true);
  try {
    action();
    refresh();
  } catch (e) {
    error(e);
  }
}
function current() {
  return world.state.entities.find((e) => e.id === selected);
}
const swatches = [
  "#6d8d72",
  "#b8a67d",
  "#bd8e73",
  "#608f9a",
  "#7eb9be",
  "#977fad",
  "#6b8c5b",
  "#d7b981",
  "#d9ad64",
];
for (const [i, [key, name]] of Object.entries(LAYERS).entries()) {
  const b = document.createElement("button");
  b.className = "layer" + (key === "atlas" ? " active" : "");
  b.dataset.layer = key;
  b.setAttribute("aria-pressed", key === "atlas");
  b.innerHTML = `<span class="swatch" style="--swatch:${swatches[i % swatches.length]}"></span><span></span><span class="layer-indicator">◉</span>`;
  b.children[1].textContent = name;
  b.onclick = () => {
    renderer.layer = key;
    document.querySelectorAll(".layer").forEach((x) => {
      x.classList.toggle("active", x === b);
      x.setAttribute("aria-pressed", String(x === b));
    });
    $("legend-title").textContent = name;
    $("legend-values").replaceChildren(
      ...LEGENDS[key].map((v) => {
        const s = document.createElement("span");
        s.textContent = v;
        return s;
      }),
    );
    $("legend-bar").style.background = [
      "vegetation",
      "biomass",
      "nutrients",
    ].includes(key)
      ? "linear-gradient(to right,#bca473,#1c6e48)"
      : key === "insolation"
        ? "linear-gradient(to right,#182742,#5f7393,#e1bc5e,#d16431)"
        : key === "temperature"
          ? "linear-gradient(to right,#6a86b3,#b2c8c1,#dbc478,#bf583d)"
          : key === "rain"
            ? "linear-gradient(to right,#b89b69,#8ba989,#408491,#484e8f)"
            : key === "basins"
              ? "linear-gradient(to right,#9878a8,#a8b779,#779eb7)"
              : key === "authored"
                ? "linear-gradient(to right,#49665d,#e7ad52)"
                : "";
    $("season-label").hidden = !["temperature", "insolation"].includes(key);
    renderer.render();
    if (world && location) inspectLocation(location);
  };
  $("layers").append(b);
}
$("layers").firstChild.click();
$("world-name").onchange = () =>
  !creating &&
  mutate(() => {
    world.state.name = $("world-name").value.trim() || "Mundo sem nome";
  });
$("refine-terrain").onclick = () => {
  if (!world || busy || world.definition.grid.width >= 1440) return;
  if (
    !confirm(
      "Refinar o terreno para 1.440 × 720? A base será gerada novamente pela mesma seed; células com intervenções manterão suas altitudes. Lugares e notas serão preservados. Um marco guardará o estado anterior. O projeto terá limite de 24 MiB.",
    )
  )
    return;
  run("refine", world);
};
$("clear-edits").onclick = () => {
  if (!world || busy) return;
  if (
    !confirm(
      "Descartar todas as intervenções do relevo e regenerar pela seed atual? Lugares e marcos históricos serão preservados.",
    )
  )
    return;
  const draft = structuredClone(world);
  draft.state.overrides = {};
  draft.journal = { undo: [], redo: [] };
  run("generate", draft, draft.definition, {});
};
$("draw-world").onclick = () => {
  if (busy) return;
  if (
    world &&
    !confirm(
      "Criar outro mundo oceânico? Salve o projeto atual antes de continuar.",
    )
  )
    return;
  const def = definition(
    $("seed").value,
    {
      ocean: Number($("ocean").value),
      radius: Number($("radius").value) * 1000,
      tilt: Number($("tilt").value),
      baseTemp: Number($("base-temp").value),
    },
    Number($("resolution").value),
  );
  run("ocean", null, def, {}, () => {
    selected = null;
    location = null;
    renderer.location = null;
    renderer.zoom = 1;
    renderer.pan = { x: 0, y: 0 };
    renderer.rotation = 0;
    world.state.name = $("world-name").value.trim() || "Mundo desenhado";
    setTool("land");
  });
};
$("generation-form").onsubmit = (e) => {
  e.preventDefault();
  if (!$("generation-form").reportValidity()) return;
  const def = definition(
    $("seed").value,
    {
      ocean: Number($("ocean").value),
      radius: Number($("radius").value) * 1000,
      tilt: Number($("tilt").value),
      baseTemp: Number($("base-temp").value),
    },
    !creating && world
      ? world.definition.grid.width
      : Number($("resolution").value),
  );
  const name = $("world-name").value.trim() || "Mundo sem nome";
  const newProject = creating || !world;
  run(
    "generate",
    newProject ? null : world,
    def,
    newProject ? {} : world.state.overrides,
    () => {
      if (newProject) {
        world.state.name = name;
        selected = null;
        location = null;
        renderer.location = null;
        renderer.zoom = 1;
        renderer.pan = { x: 0, y: 0 };
        renderer.rotation = 0;
      }
      creating = false;
      document.querySelector(
        "#generation-form button[type=submit]",
      ).textContent = "Regenerar terreno";
    },
  );
};
for (const projection of ["flat", "globe"])
  $(projection).onclick = () => {
    renderer.projection = projection;
    renderer.pan = { x: 0, y: 0 };
    renderer.zoom = 1;
    for (const p of ["flat", "globe"]) {
      $(p).classList.toggle("active", p === projection);
      $(p).setAttribute("aria-pressed", String(p === projection));
    }
    renderer.render();
  };
$("rivers").onchange = () => {
  renderer.showRivers = $("rivers").checked;
  renderer.render();
};
$("grid").onchange = () => {
  renderer.showGrid = $("grid").checked;
  renderer.render();
};
$("nature").onchange = () => {
  renderer.showNature = $("nature").checked;
  renderer.render();
};
$("human-scale").onchange = () => {
  renderer.showHumanScale = $("human-scale").checked;
  renderer.render();
};
$("season").onchange = () => {
  renderer.season = $("season").value;
  renderer.render();
  if (world && location) inspectLocation(location);
};
function setTool(value) {
  tool = value;
  document.querySelectorAll("[data-tool]").forEach((b) => {
    const active = b.dataset.tool === value;
    b.classList.toggle("active", active);
    b.setAttribute("aria-pressed", String(active));
  });
  $("brush-options").hidden = !["raise", "lower", "land", "sea"].includes(
    value,
  );
  $("map").style.cursor =
    value === "inspect" ? "grab" : value === "place" ? "crosshair" : "cell";
}
document
  .querySelectorAll("[data-tool]")
  .forEach((b) => (b.onclick = () => setTool(b.dataset.tool)));
$("brush-size").oninput = () =>
  ($("brush-radius").value = $("brush-size").value + "°");
$("brush-force").oninput = () =>
  ($("brush-strength").value = $("brush-force").value + " m");
function zoom(f, point) {
  renderer.zoomAt(f, point?.x, point?.y);
  renderer.render();
}
$("detail-scale").onchange = () => {
  const value = $("detail-scale").value;
  if (value === "planet") {
    $("reset-view").click();
    return;
  }
  if (!world) return;
  const p = location ?? renderer.geographic(renderer.w / 2, renderer.h / 2);
  renderer.setMetresPerPixel(Number(value), p);
  if (p) inspectLocation(p);
};
$("zoom-in").onclick = () => zoom(1.2);
$("zoom-out").onclick = () => zoom(1 / 1.2);
$("reset-view").onclick = () => {
  renderer.zoom = 1;
  renderer.pan = { x: 0, y: 0 };
  renderer.rotation = 0;
  renderer.render();
};
$("map").addEventListener(
  "wheel",
  (e) => {
    e.preventDefault();
    zoom(
      Math.exp(-Math.max(-250, Math.min(250, e.deltaY)) * 0.003),
      pointer(e),
    );
  },
  { passive: false },
);
function pointer(e) {
  const r = $("map").getBoundingClientRect();
  return { x: e.clientX - r.left, y: e.clientY - r.top };
}
function paint(p, pressure) {
  const delta =
    Number($("brush-force").value) *
    (tool === "lower" ? -1 : 1) *
    (pressure > 0 ? clamp(pressure * 2, 0.2, 2) : 1);
  const changes = brush(
    drag.draft,
    p.lat,
    p.lon,
    Number($("brush-size").value),
    delta,
    ["land", "sea"].includes(tool) ? tool : "relative",
  );
  for (const c of changes) {
    const first = drag.changes.get(c.i);
    if (first) first.after = c.after;
    else drag.changes.set(c.i, c);
  }
  renderer.setWorld(drag.draft, derived, true);
  renderer.render();
}
$("map").onpointerdown = (e) => {
  if (busy || !world || e.button !== 0 || drag) return;
  if (
    renderer.metresPerPixel() < 1500 &&
    ["raise", "lower", "land", "sea"].includes(tool)
  ) {
    status(
      "Este pincel altera a grade regional. Afaste para editar relevo regional; edição métrica do solo ainda não está disponível.",
    );
    return;
  }
  $("map").setPointerCapture(e.pointerId);
  const p = pointer(e),
    geo = renderer.geographic(p.x, p.y);
  drag = {
    id: e.pointerId,
    start: p,
    last: p,
    moved: false,
    changes: new Map(),
    draft: ["raise", "lower", "land", "sea"].includes(tool)
      ? structuredClone(world)
      : null,
  };
  if (drag.draft && geo) paint(geo, e.pressure);
};
$("map").onpointermove = (e) => {
  const p = pointer(e),
    geo = renderer.geographic(p.x, p.y);
  if (geo)
    $("coordinates").textContent =
      `LAT ${geo.lat.toFixed(renderer.detailActive ? 7 : 2)}°  LON ${geo.lon.toFixed(renderer.detailActive ? 7 : 2)}°`;
  if (!drag || e.pointerId !== drag.id) return;
  const dx = p.x - drag.last.x,
    dy = p.y - drag.last.y;
  if (Math.hypot(p.x - drag.start.x, p.y - drag.start.y) > 4) drag.moved = true;
  if (drag.draft && geo && Math.hypot(dx, dy) > 2) paint(geo, e.pressure);
  else if (tool === "inspect" && drag.moved) {
    renderer.dragBy(dx, dy);
    renderer.render();
  }
  drag.last = p;
};
$("map").onpointerup = (e) => {
  if (!drag || e.pointerId !== drag.id) return;
  const end = drag;
  drag = null;
  if (end.draft) {
    record(end.draft, Array.from(end.changes.values()));
    if (end.changes.size) deriveDraft(end.draft);
    return;
  }
  if (end.moved) return;
  const p = pointer(e),
    geo = renderer.geographic(p.x, p.y);
  if (!geo) return;
  if (tool === "place") {
    mutate(() => {
      const entity = {
        id: crypto.randomUUID(),
        type: "castle",
        name: `Lugar ${world.state.entities.length + 1}`,
        lat: geo.lat,
        lon: geo.lon,
        desc: "",
      };
      world.state.entities.push(entity);
      select(entity);
    });
  } else {
    const e = renderer.hit(p.x, p.y);
    if (e) select(e);
    else inspectLocation(geo);
  }
};
$("map").onpointercancel = () => {
  drag = null;
  if (world && derived) {
    renderer.setWorld(world, derived);
    renderer.render();
  }
};
$("coordinate-form").onsubmit = (e) => {
  e.preventDefault();
  if (world) {
    const p = {
      lat: Number($("latitude").value),
      lon: Number($("longitude").value),
    };
    inspectLocation(p);
    renderer.focus(p.lat, p.lon);
    renderer.render();
  }
};
function undo(direction) {
  if (busy || !world) return;
  const draft = structuredClone(world);
  if (history(draft, direction)) deriveDraft(draft);
}
$("undo").onclick = () => undo("undo");
$("redo").onclick = () => undo("redo");
$("search").oninput = () => world && renderEntities();
$("update-entity").onclick = () =>
  mutate(() => {
    const e = current();
    if (!e) return;
    e.name = $("entity-name").value.trim() || "Lugar sem nome";
    e.type = $("entity-type").value;
    e.desc = $("entity-desc").value;
    status("Lugar salvo no projeto.");
  });
$("delete-entity").onclick = () =>
  mutate(() => {
    world.state.entities = world.state.entities.filter(
      (e) => e.id !== selected,
    );
    selected = null;
  });
$("link-note").onclick = () => $("note-file").click();
$("note-file").onchange = async (e) => {
  const file = e.target.files[0],
    target = selected,
    sourceWorld = world;
  e.target.value = "";
  if (!file) return;
  try {
    const note = await noteFromFile(file);
    if (world !== sourceWorld)
      throw new Error(
        "O mundo mudou durante a importação da nota. Selecione o arquivo novamente.",
      );
    mutate(() => {
      const entity = world.state.entities.find((e) => e.id === target);
      if (entity) {
        if (entity.note?.name === note.name) note.id = entity.note.id;
        entity.note = note;
      }
    });
    status("Cópia Markdown incorporada; arquivo original intacto.");
  } catch (e) {
    error(e);
  }
};
$("unlink-note").onclick = () =>
  mutate(() => {
    delete current().note;
  });
$("generate-local").onclick = () => {
  mutate(() => {
    const entity = current();
    if (!entity || entity.localMap) return;
    entity.localMap = localMap(entity, world.definition.seed);
  });
  $("inspect-local").click();
};
$("inspect-local").onclick = () =>
  mutate(() => {
    const entity = current();
    if (!entity) return;
    if (!entity.localMap) return;
    $("local-title").textContent = entity.name;
    drawLocal($("local-canvas"), entity);
    $("room-info").textContent = "Selecione uma sala.";
    $("local-dialog").showModal();
    $("local-canvas").onclick = (e) => {
      const r = e.currentTarget.getBoundingClientRect(),
        x = ((e.clientX - r.left) / r.width) * 100,
        y = ((e.clientY - r.top) / r.height) * 100;
      const room = entity.localMap.rooms.find(
        (s) => x >= s.x && x <= s.x + s.w && y >= s.y && y <= s.y + s.h,
      );
      $("room-info").textContent = room?.name ?? "Corredor";
    };
  });
$("close-local").onclick = () => $("local-dialog").close();
$("help").onclick = () => $("help-dialog").showModal();
$("close-help").onclick = () => $("help-dialog").close();
$("save-project").onclick = () => {
  if (!world) return;
  try {
    download("atlas-forge-world.json", serialize(world));
    status("JSON preparado para download; mantenha sua cópia em local seguro.");
  } catch (e) {
    error(e);
  }
};
$("publish").onclick = () => {
  if (!world) return;
  try {
    download("atlas-publicado.html", publish(world, derived), "text/html");
    status("Atlas HTML independente preparado · abra o arquivo offline.");
  } catch (e) {
    error(e);
  }
};
$("new-project").onclick = () => {
  if (busy) return;
  if (
    world &&
    !confirm(
      "Preparar outro mundo? Salve uma cópia JSON do projeto atual antes de gerar. Ele continua aberto até a nova geração terminar.",
    )
  )
    return;
  creating = true;
  $("resolution").disabled = false;
  $("resolution").value = "720";
  $("seed").value = crypto.randomUUID().slice(0, 8).toUpperCase();
  $("world-name").value = "Mundo sem nome";
  document.querySelector("#generation-form button[type=submit]").textContent =
    "Criar novo mundo";
  $("left-panel").classList.add("panel-open");
  $("world-name").focus();
  status(
    "Escolha os parâmetros e gere o novo mundo. O projeto atual continua aberto.",
  );
};
$("open-project").onclick = () => $("project-file").click();
$("project-file").onchange = async (e) => {
  const file = e.target.files[0];
  e.target.value = "";
  if (!file) return;
  cancel(true);
  const request = ++job;
  setBusy(true);
  try {
    if (file.size > MAX_BYTES) throw new Error("Projeto excede 24 MiB.");
    const incoming = await verifyNotes(parseProject(await file.text()));
    if (request !== job) return;
    selected = null;
    location = null;
    renderer.location = null;
    deriveDraft(incoming);
  } catch (e) {
    if (request !== job) return;
    setBusy(false);
    error(e);
  }
};
$("checkpoint").onclick = () =>
  mutate(() => {
    const draft = structuredClone(world);
    checkpoint(
      draft,
      `Marco ${world.timeline.checkpoints.length + 1} · ${new Date().toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`,
    );
    serialize(draft);
    world = draft;
    status(
      "Marco completo preservado; alterações seguintes não modificam o passado.",
    );
  });
$("timeline").onchange = () => {
  if (!$("timeline").value) return;
  if (!world) return;
  const draft = structuredClone(world);
  try {
    restore(draft, $("timeline").value);
    selected = null;
    deriveDraft(draft);
  } catch (e) {
    error(e);
  }
};
$("cancel").onclick = () => cancel();
document.querySelectorAll("[data-panel]").forEach(
  (b) =>
    (b.onclick = () => {
      const p = $(b.dataset.panel + "-panel"),
        was = p.classList.contains("panel-open");
      document
        .querySelectorAll(".sidebar")
        .forEach((x) => x.classList.remove("panel-open"));
      p.classList.toggle("panel-open", !was);
    }),
);
document.addEventListener("keydown", (e) => {
  if (/INPUT|TEXTAREA|SELECT/.test(e.target.tagName)) return;
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") {
    e.preventDefault();
    undo(e.shiftKey ? "redo" : "undo");
  }
  if (e.key.toLowerCase() === "v") setTool("inspect");
  if (e.key.toLowerCase() === "b") setTool("raise");
  if (e.key === "Escape") {
    if (busy) cancel();
    document
      .querySelectorAll(".sidebar")
      .forEach((x) => x.classList.remove("panel-open"));
  }
  if (e.target === $("map")) {
    if (
      [
        "ArrowLeft",
        "ArrowRight",
        "ArrowUp",
        "ArrowDown",
        "+",
        "-",
        "=",
        "Home",
      ].includes(e.key)
    )
      e.preventDefault();
    if (e.key === "Home") $("reset-view").click();
    if (e.key === "ArrowUp") {
      renderer.pan.y += 40;
      renderer.render();
    }
    if (e.key === "ArrowDown") {
      renderer.pan.y -= 40;
      renderer.render();
    }
    if (e.key === "+") zoom(1.2);
    if (e.key === "-") zoom(1 / 1.2);
    if (e.key === "ArrowLeft") {
      renderer.pan.x += 40;
      renderer.render();
    }
    if (e.key === "ArrowRight") {
      renderer.pan.x -= 40;
      renderer.render();
    }
  }
});
bindPinch($("map"), renderer, () => {
  drag = null;
  if (world && derived) renderer.setWorld(world, derived);
});
new ResizeObserver(() => renderer.render()).observe($("map"));
async function init() {
  const request = job;
  try {
    const saved = await loadLocal();
    if (request !== job) return;
    if (saved) {
      deriveDraft(saved);
      return;
    }
  } catch {
    if (request !== job) return;
    status("Armazenamento local indisponível; use exportação JSON.");
  }
  $("empty-state").hidden = false;
  $("save-project").disabled = true;
  $("publish").disabled = true;
  $("undo").disabled = true;
  $("redo").disabled = true;
  status(
    "Oficina vazia · abra um projeto ou escolha os parâmetros e gere o seu mundo.",
  );
}
const ecology = ecologyPanel({
  getWorld: () => world,
  getDerived: () => derived,
  getLocation: () => location,
  commit: (draft) => {
    if (busy) return;
    serialize(draft);
    world = draft;
    refresh();
  },
  advance: (years) => {
    if (world && !busy) run("era", world, undefined, { years });
  },
  error,
});
init();
