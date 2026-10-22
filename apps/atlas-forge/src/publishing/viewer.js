import { MapRenderer, LAYERS } from "../rendering/map.js";
import { drawLocal } from "../rendering/local.js";
import { index } from "../geometry/sphere.js";
import { markdown } from "../documents/markdown.js";
const $ = (id) => document.getElementById(id),
  data = JSON.parse($("atlas-data").textContent),
  renderer = new MapRenderer($("map"));
renderer.setWorld(data, data.derived);
$("title").textContent = data.name;
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
  $("projection").textContent =
    renderer.projection === "flat" ? "◉ Globo" : "▱ Mapa";
  renderer.render();
};
function select(entity) {
  renderer.selected = entity.id;
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
    if (renderer.projection === "globe") renderer.rotation -= dx * 0.4;
    else {
      renderer.pan.x += dx;
      renderer.pan.y += dy;
    }
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
        const i = index(p.lat, p.lon, data.definition.grid);
        $("coords").textContent =
          `${p.lat.toFixed(2)}°, ${p.lon.toFixed(2)}° · ${Math.round(data.state.elevation[i])} m · ${data.derived.temperature[i].toFixed(1)} °C · ${Math.round(data.derived.rain[i])} mm/ano`;
      }
    }
  }
  drag = null;
};
$("map").onpointercancel = () => {
  drag = null;
};
function zoom(f) {
  renderer.zoom = Math.max(0.6, Math.min(5, renderer.zoom * f));
  renderer.render();
}
$("plus").onclick = () => zoom(1.2);
$("minus").onclick = () => zoom(1 / 1.2);
$("map").addEventListener(
  "wheel",
  (e) => {
    e.preventDefault();
    zoom(e.deltaY > 0 ? 0.9 : 1.1);
  },
  { passive: false },
);
$("map").onkeydown = (e) => {
  if (e.key === "+" || e.key === "=") zoom(1.2);
  if (e.key === "-") zoom(1 / 1.2);
  if (e.key === "ArrowLeft") {
    renderer.rotation -= 10;
    renderer.pan.x -= 15;
    renderer.render();
  }
  if (e.key === "ArrowRight") {
    renderer.rotation += 10;
    renderer.pan.x += 15;
    renderer.render();
  }
};
new ResizeObserver(() => renderer.render()).observe($("map"));
renderer.render();
