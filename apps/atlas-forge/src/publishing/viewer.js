import { SURFACE_MODEL } from "../rendering/surface-field.js";
import { MapRenderer, LAYERS, SOLAR_FIELDS } from "../rendering/map.js";
import { bindPinch } from "../rendering/gestures.js";
import { drawLocal } from "../rendering/local.js";
import { index } from "../geometry/sphere.js";
import { markdown } from "../documents/markdown.js";
const $ = (id) => document.getElementById(id),
  data = JSON.parse($("atlas-data").textContent),
  renderer = new MapRenderer($("map"));
if (data.surfaceModel !== SURFACE_MODEL)
  throw new Error(
    "Versão de terreno local desconhecida; publicação preservada.",
  );
renderer.setWorld(data, data.derived);
$("title").textContent = data.name;
const eco = data.state.ecosystem;
$("eco-summary").textContent = eco
  ? `Ano ${eco.year} · ${data.state.species.length} espécies · ecologia regional ${eco.grid.width} × ${eco.grid.height}`
  : "";
function inspectEcology(p) {
  if (!eco) return;
  const i = index(p.lat, p.lon, eco.grid);
  const box = $("eco-region");
  box.replaceChildren();
  const title = document.createElement("p");
  title.textContent = `Nitrogênio mineral: ${eco.nutrients[i].toFixed(4)} kg N/m² · detritos: ${eco.detritus[i].toFixed(4)} kg/m²`;
  box.append(title);
  for (const s of data.state.species) {
    const row = document.createElement("p");
    row.textContent = `${s.name}: ${eco.biomass[s.id][i].toFixed(6)} kg/m²`;
    box.append(row);
  }
}

for (const [key, name] of Object.entries(LAYERS)) {
  const o = document.createElement("option");
  o.value = key;
  o.textContent = name;
  $("layer").append(o);
}
$("layer").onchange = () => {
  renderer.layer = $("layer").value;
  renderer.render();
};
$("season").onchange = () => {
  renderer.season = $("season").value;
  renderer.render();
};
$("projection").onclick = () => {
  renderer.projection = renderer.projection === "flat" ? "globe" : "flat";
  renderer.pan = { x: 0, y: 0 };
  renderer.zoom = 1;
  $("projection").textContent =
    renderer.projection === "flat" ? "◉ Globo" : "▱ Mapa";
  renderer.render();
};
function select(entity) {
  renderer.selected = entity.id;
  renderer.focus(entity.lat, entity.lon);
  renderer.render();
  $("entity-title").textContent = entity.name;
  $("description").textContent = entity.desc;
  $("note").innerHTML = entity.note ? markdown(entity.note.markdown) : "";
  $("note").className = "markdown";
  $("inspect").hidden = !entity.localMap;
  $("inspect").onclick = () => {
    $("local-title").textContent = entity.name;
    drawLocal($("local-map"), entity);
    $("room").textContent =
      "Mapa filho persistente · escala de referência: 100 m.";
    $("local").showModal();
    $("local-map").onclick = (event) => {
      const r = event.currentTarget.getBoundingClientRect(),
        x = ((event.clientX - r.left) / r.width) * 100,
        y = ((event.clientY - r.top) / r.height) * 100;
      const room = entity.localMap.rooms.find(
        (s) => x >= s.x && x <= s.x + s.w && y >= s.y && y <= s.y + s.h,
      );
      $("room").textContent = room?.name ?? "Corredor";
    };
  };
}
for (const e of data.state.entities) {
  const b = document.createElement("button");
  b.textContent = `⌖ ${e.name}`;
  b.onclick = () => select(e);
  $("places").append(b);
}
$("back").onclick = () => $("local").close();
let drag;
$("map").onpointerdown = (e) => {
  drag = {
    x: e.clientX,
    y: e.clientY,
    lastX: e.clientX,
    lastY: e.clientY,
    moved: false,
  };
  $("map").setPointerCapture(e.pointerId);
};
$("map").onpointermove = (e) => {
  if (!drag) return;
  const dx = e.clientX - drag.lastX,
    dy = e.clientY - drag.lastY;
  if (Math.hypot(e.clientX - drag.x, e.clientY - drag.y) > 4) drag.moved = true;
  if (drag.moved) {
    renderer.dragBy(dx, dy);
    renderer.render();
  }
  drag.lastX = e.clientX;
  drag.lastY = e.clientY;
};
$("map").onpointerup = (e) => {
  if (!drag) return;
  if (!drag.moved) {
    const r = $("map").getBoundingClientRect(),
      x = e.clientX - r.left,
      y = e.clientY - r.top,
      entity = renderer.hit(x, y);
    if (entity) select(entity);
    else {
      const p = renderer.geographic(x, y);
      if (p) {
        inspectEcology(p);
        const i = index(p.lat, p.lon, data.definition.grid);
        $("coords").textContent =
          `${p.lat.toFixed(2)}°, ${p.lon.toFixed(2)}° · ${Math.round(data.state.elevation[i])} m · ${data.derived.temperature[i].toFixed(1)} °C · ${Math.round(data.derived.rain[i])} mm/ano · ${data.derived[SOLAR_FIELDS[renderer.season]][Math.floor(i / data.definition.grid.width)].toFixed(1)} W/m² (topo da atmosfera)`;
      }
    }
  }
  drag = null;
};
$("map").onpointercancel = () => {
  drag = null;
};
function zoom(f, x, y) {
  renderer.zoomAt(f, x, y);
  renderer.render();
}
$("plus").onclick = () => zoom(1.2);
$("minus").onclick = () => zoom(1 / 1.2);
$("map").addEventListener(
  "wheel",
  (e) => {
    e.preventDefault();
    const rect = $("map").getBoundingClientRect();
    zoom(
      Math.exp(-Math.max(-250, Math.min(250, e.deltaY)) * 0.003),
      e.clientX - rect.left,
      e.clientY - rect.top,
    );
  },
  { passive: false },
);
$("map").onkeydown = (e) => {
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
  if (e.key === "Home") $("reset").click();
  if (e.key === "ArrowUp") {
    renderer.pan.y += 40;
    renderer.render();
  }
  if (e.key === "ArrowDown") {
    renderer.pan.y -= 40;
    renderer.render();
  }
  if (e.key === "+" || e.key === "=") zoom(1.2);
  if (e.key === "-") zoom(1 / 1.2);
  if (e.key === "ArrowLeft") {
    renderer.pan.x += 40;
    renderer.render();
  }
  if (e.key === "ArrowRight") {
    renderer.pan.x -= 40;
    renderer.render();
  }
};
$("scale").onchange = () => {
  if ($("scale").value === "planet") {
    $("reset").click();
    return;
  }
  renderer.setMetresPerPixel(Number($("scale").value));
};
$("reset").onclick = () => {
  renderer.zoom = 1;
  renderer.rotation = 0;
  renderer.pan = { x: 0, y: 0 };
  renderer.render();
};
bindPinch($("map"), renderer, () => {
  drag = null;
});
new ResizeObserver(() => renderer.render()).observe($("map"));
renderer.render();
