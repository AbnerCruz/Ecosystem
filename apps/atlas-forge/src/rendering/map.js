import { vegetation } from "../geometry/ecology.js";
import { SurfaceTiles } from "./surface.js";
import { drawHumanScale, drawNature } from "./landmarks.js";
import { sampleSurface, MATERIALS, SURFACE_MODEL } from "./surface-field.js";
import {
  clamp,
  coord,
  index,
  inverseOrtho,
  orthographic,
  wrap,
  distance,
} from "../geometry/sphere.js";
export const LAYERS = {
  atlas: "Atlas natural",
  vegetation: "Cobertura vegetal",
  biomass: "Biomassa viva",
  nutrients: "Nitrogênio mineral",
  elevation: "Altitude",
  temperature: "Temperatura",
  rain: "Precipitação",
  drainage: "Drenagem",
  basins: "Bacias",
  biome: "Biomas",
  authored: "Intervenções",
  insolation: "Insolação · W/m²",
};
export const SOLAR_FIELDS = {
  annual: "solarAnnual",
  june: "solarJune",
  december: "solarDecember",
};
export const BIOME_COLORS = [
  [24, 62, 76],
  [222, 234, 229],
  [148, 162, 137],
  [186, 157, 103],
  [159, 154, 105],
  [125, 151, 100],
  [68, 112, 86],
  [37, 99, 71],
  [84, 112, 109],
  [169, 164, 145],
];
const mix = (a, b, f) =>
  a.map((v, i) => Math.round(v + (b[i] - v) * clamp(f, 0, 1)));
function gradient(value, stops) {
  for (let j = 1; j < stops.length; j++)
    if (value < stops[j][0])
      return mix(
        stops[j - 1][1],
        stops[j][1],
        (value - stops[j - 1][0]) / (stops[j][0] - stops[j - 1][0]),
      );
  return stops.at(-1)[1];
}
export const LEGENDS = {
  vegetation: ["0%", "50%", "100% · regional"],
  biomass: ["0", "2", "4+ kg/m² · regional"],
  nutrients: ["0", "0,05", "0,1+ kg N/m² · regional"],
  atlas: ["Oceano", "Planície", "Montanha"],
  elevation: ["−6.000 m", "0 m", "+6.000 m"],
  temperature: ["−30 °C", "10 °C", "45 °C"],
  rain: ["0", "1.000", "3.000 mm/ano"],
  drainage: ["Relevo", "Fluxo > 1.500 m³/s", "Sumidouro"],
  basins: ["Cores por bacia", "Oceânica / fechada"],
  biome: ["Biomas climáticos · heurística"],
  authored: ["Relevo", "Área preservada"],
  insolation: ["0", "500", "1.000 W/m² · topo da atmosfera"],
};
export function color(i, world, d, layer, season = "annual") {
  if (["vegetation", "biomass", "nutrients"].includes(layer)) {
    const eco = world.state.ecosystem;
    if (!eco) return [70, 70, 70];
    const p = coord(i, world.definition.grid),
      j = index(p.lat, p.lon, eco.grid);
    const value =
      layer === "vegetation"
        ? vegetation(world, j)
        : layer === "nutrients"
          ? eco.nutrients[j] * 10
          : world.state.species.reduce(
              (sum, s) => sum + eco.biomass[s.id][j],
              0,
            ) / 4;
    return mix([188, 164, 115], [28, 110, 72], value);
  }
  const e = world.state.elevation[i];
  let c;
  if (layer === "temperature")
    c = gradient(
      d[
        season === "june"
          ? "summer"
          : season === "december"
            ? "winter"
            : "temperature"
      ][i],
      [
        [-30, [106, 134, 179]],
        [0, [178, 200, 193]],
        [18, [219, 196, 120]],
        [45, [191, 88, 61]],
      ],
    );
  else if (layer === "insolation")
    c = gradient(
      d[SOLAR_FIELDS[season] ?? SOLAR_FIELDS.annual][
        Math.floor(i / world.definition.grid.width)
      ],
      [
        [0, [24, 39, 66]],
        [250, [95, 115, 147]],
        [500, [225, 188, 94]],
        [1000, [209, 100, 49]],
      ],
    );
  else if (layer === "rain")
    c = gradient(d.rain[i], [
      [0, [184, 155, 105]],
      [500, [139, 169, 137]],
      [1500, [64, 132, 145]],
      [3000, [72, 78, 143]],
    ]);
  else if (layer === "basins" && e > 0) {
    const b = d.basin[i];
    c = [75 + ((b * 47) % 130), 80 + ((b * 71) % 130), 85 + ((b * 29) % 100)];
  } else if (layer === "biome") c = BIOME_COLORS[d.biome[i]];
  else if (e <= 0)
    c = gradient(e, [
      [-9000, [12, 31, 45]],
      [-2000, [22, 58, 72]],
      [0, [45, 87, 94]],
    ]);
  else if (layer === "elevation")
    c = gradient(e, [
      [0, [116, 152, 112]],
      [800, [184, 177, 128]],
      [2500, [164, 131, 106]],
      [5000, [225, 219, 201]],
      [8000, [250, 247, 233]],
    ]);
  else {
    c = mix(BIOME_COLORS[d.biome[i]], [156, 147, 123], clamp(e / 5000, 0, 0.8));
    if (e > 3000) c = mix(c, [231, 231, 214], (e - 3000) / 2500);
  }
  if (
    !["temperature", "rain", "basins", "biome", "insolation"].includes(layer) &&
    e > 0
  ) {
    const { width: w } = world.definition.grid,
      left = Math.floor(i / w) * w + (((i % w) + w - 1) % w);
    const shade = clamp((world.state.elevation[left] - e) / 2300, -0.28, 0.28);
    c = c.map((v) => clamp(Math.round(v * (1 + shade)), 0, 255));
  }
  if (layer === "authored" && world.state.overrides[i] !== undefined)
    c = mix(c, [231, 173, 82], 0.7);
  if (layer === "drainage" && d.terminal[i] === 2) c = [197, 141, 220];
  return c;
}
export class MapRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.projection = "flat";
    this.rotation = 0;
    this.zoom = 1;
    this.pan = { x: 0, y: 0 };
    this.showGrid = true;
    this.showRivers = true;
    this.showNature = true;
    this.showHumanScale = true;
    this.layer = "atlas";
    this.season = "annual";
    this.raster = document.createElement("canvas");
    this.cache = "";
  }
  setWorld(world, derived, forceTerrain = false) {
    const changed =
      forceTerrain ||
      this.world?.state.elevation !== world.state.elevation ||
      this.derived !== derived ||
      this.sourceSeed !== world.definition.seed;
    this.sourceSeed = world.definition.seed;
    this.world = world;
    this.derived = derived;
    if (!changed) return;
    this.cache = "";
    if (this.surface) this.surface.setWorld(world, derived);
    this.globeCache = null;
    this.contourCache = null;
    const grid = world.definition.grid;
    this.rivers = [];
    for (let i = 0; i < world.state.elevation.length; i++) {
      const j = derived.downstream[i];
      if (
        j >= 0 &&
        derived.discharge[i] >= 1500 &&
        world.state.elevation[i] > 0
      )
        this.rivers.push([coord(i, grid), coord(j, grid)]);
    }
  }
  zoomAt(factor, x = this.w / 2, y = this.h / 2) {
    this.resize();
    const previous = this.zoom;
    const maxZoom = this.world
      ? Math.max(
          128,
          (2 * Math.PI * this.world.definition.planet.radius) /
            (((this.scale * 2) / this.zoom) * 0.025),
        )
      : 128;
    this.zoom = clamp(this.zoom * factor, 0.5, maxZoom);
    const f = this.zoom / previous;
    // Both projections scale about the translated camera origin.
    this.pan.x = x - this.w / 2 - (x - this.w / 2 - this.pan.x) * f;
    this.pan.y = y - this.h / 2 - (y - this.h / 2 - this.pan.y) * f;
    this.resize();
  }
  metresPerPixel() {
    const a = this.geographic(this.w / 2, this.h / 2),
      b = this.geographic(this.w / 2 + 1, this.h / 2 + 1);
    return a && b && this.world
      ? distance(a, b, this.world.definition.planet.radius) / Math.SQRT2
      : Infinity;
  }
  setMetresPerPixel(target, p) {
    this.resize();
    if (p) this.focus(p.lat, p.lon);
    // Geodesic metres/pixel changes slightly with the camera; converge on the target.
    for (let i = 0; i < 5; i++) {
      const factor = this.metresPerPixel() / target;
      if (!Number.isFinite(factor) || Math.abs(factor - 1) < 1e-9) break;
      this.zoomAt(factor);
    }
    this.render();
  }
  detailedLocation(p) {
    if (!this.surface || !this.detailActive) return null;
    const s = sampleSurface(this.surface.context, p.lat, p.lon);
    return { ...s, materialName: MATERIALS[s.material] };
  }
  dragBy(dx, dy) {
    if (this.projection === "globe" && this.zoom <= 2)
      this.rotation -= (dx * 180) / (Math.PI * this.scale * 0.83);
    else this.pan.x += dx;
    this.pan.y += dy;
  }
  focus(lat, lon) {
    this.resize();
    if (this.projection === "globe") {
      this.rotation = lon;
      this.pan = { x: 0, y: 0 };
    }
    const p = this.point(lat, lon);
    this.pan.x += this.w / 2 - p.x;
    this.pan.y += this.h / 2 - p.y;
    this.resize();
  }
  resize() {
    const rect = this.canvas.getBoundingClientRect(),
      ratio = Math.min(devicePixelRatio || 1, 2);
    if (
      this.canvas.width !== Math.round(rect.width * ratio) ||
      this.canvas.height !== Math.round(rect.height * ratio)
    ) {
      this.canvas.width = Math.round(rect.width * ratio);
      this.canvas.height = Math.round(rect.height * ratio);
    }
    this.w = rect.width;
    this.h = rect.height;
    this.ratio = ratio;
    this.scale =
      (this.projection === "globe"
        ? Math.min(this.w, this.h) / 2 / 0.83
        : Math.min(this.w / 2, this.h)) *
      0.91 *
      this.zoom;
    this.left = (this.w - this.scale * 2) / 2 + this.pan.x;
    this.top = (this.h - this.scale) / 2 + this.pan.y;
  }
  point(lat, lon) {
    if (this.projection === "flat")
      return {
        x:
          this.left +
          ((lon + 180) / 360) * this.scale * 2 +
          Math.round(
            (this.w / 2 - this.left - ((lon + 180) / 360) * this.scale * 2) /
              (this.scale * 2),
          ) *
            this.scale *
            2,
        y: this.top + ((90 - lat) / 180) * this.scale,
        visible: true,
      };
    const p = orthographic(lat, lon, this.rotation);
    return {
      x: this.w / 2 + p.x * this.scale * 0.83 + this.pan.x,
      y: this.h / 2 - p.y * this.scale * 0.83 + this.pan.y,
      visible: p.visible,
    };
  }
  geographic(x, y) {
    if (this.projection === "globe")
      return inverseOrtho(
        (x - this.w / 2 - this.pan.x) / (this.scale * 0.83),
        -(y - this.h / 2 - this.pan.y) / (this.scale * 0.83),
        this.rotation,
      );
    const lon = ((x - this.left) / (this.scale * 2)) * 360 - 180,
      lat = 90 - ((y - this.top) / this.scale) * 180;
    return lat < -90 || lat > 90 ? null : { lat, lon: wrap(lon) };
  }
  render() {
    this.resize();
    const ctx = this.canvas.getContext("2d");
    ctx.setTransform(this.ratio, 0, 0, this.ratio, 0, 0);
    ctx.clearRect(0, 0, this.w, this.h);
    if (!this.world || !this.derived) return;
    this.detailActive = ["atlas", "elevation"].includes(this.layer);
    if (this.detailActive && !this.surface) {
      this.surface = new SurfaceTiles(() => {
        if (!this.surfaceFrame)
          this.surfaceFrame = requestAnimationFrame(() => {
            this.surfaceFrame = null;
            this.render();
          });
      });
      this.surface.setWorld(this.world, this.derived);
    }
    if (!this.detailActive && this.surface) this.surface.pause();
    this.canvas.dataset.terrainActive = String(this.detailActive);
    this.canvas.dataset.metresPerPixel = String(this.metresPerPixel());
    const { grid } = this.world.definition,
      key = `${this.layer}:${this.season}`;
    if (this.cache !== key && !this.detailActive) {
      this.raster.width = grid.width;
      this.raster.height = grid.height;
      const rc = this.raster.getContext("2d"),
        im = rc.createImageData(grid.width, grid.height);
      for (let i = 0; i < grid.width * grid.height; i++) {
        const c = color(i, this.world, this.derived, this.layer, this.season);
        im.data.set([...c, 255], i * 4);
      }
      rc.putImageData(im, 0, 0);
      this.source = im.data;
      this.cache = key;
    }
    if (this.detailActive) {
      this.surface.render(this, ctx);
    } else if (this.projection === "flat") {
      ctx.imageSmoothingEnabled = (this.scale * 2) / grid.width < 2;
      const span = this.scale * 2;
      const first = Math.floor(-this.left / span);
      const last = Math.ceil((this.w - this.left) / span);
      for (let copy = first; copy < last; copy++)
        ctx.drawImage(
          this.raster,
          this.left + copy * span,
          this.top,
          span,
          this.scale,
        );
    } else {
      // Project only the visible screen, never enlarge a capped globe texture.
      const r = this.scale * 0.83;
      const density = this.ratio;
      const width = Math.ceil(this.w * density),
        height = Math.ceil(this.h * density);
      const key = `${width}:${height}:${r}:${this.pan.x}:${this.pan.y}:${grid.width}`;
      if (this.globeCache?.key !== key) {
        const off = document.createElement("canvas");
        off.width = width;
        off.height = height;
        const oc = off.getContext("2d"),
          im = oc.createImageData(width, height);
        const rows = new Int32Array(width * height).fill(-1),
          longitudes = new Float64Array(width * height);
        for (let y = 0; y < height; y++)
          for (let x = 0; x < width; x++) {
            const p = inverseOrtho(
              ((x + 0.5) / density - this.w / 2 - this.pan.x) / r,
              -((y + 0.5) / density - this.h / 2 - this.pan.y) / r,
              0,
            );
            if (!p) continue;
            const k = y * width + x;
            rows[k] =
              Math.floor(index(p.lat, 0, grid) / grid.width) * grid.width;
            longitudes[k] = ((p.lon + 180) / 360) * grid.width;
          }
        this.globeCache = { key, off, oc, im, rows, longitudes };
      }
      const { off, oc, im, rows, longitudes } = this.globeCache;
      const shift = (this.rotation / 360) * grid.width;
      if (
        this.globeCache.source !== this.source ||
        this.globeCache.rotation !== this.rotation
      ) {
        for (let k = 0; k < rows.length; k++) {
          if (rows[k] < 0) continue;
          const i =
            (rows[k] +
              (((Math.floor(longitudes[k] + shift) % grid.width) + grid.width) %
                grid.width)) *
            4;
          const t = k * 4;
          im.data[t] = this.source[i];
          im.data[t + 1] = this.source[i + 1];
          im.data[t + 2] = this.source[i + 2];
          im.data[t + 3] = 255;
        }
        oc.putImageData(im, 0, 0);
        this.globeCache.source = this.source;
        this.globeCache.rotation = this.rotation;
      }
      ctx.drawImage(off, 0, 0, this.w, this.h);
    }
    if (
      !this.detailActive &&
      ["atlas", "elevation", "drainage"].includes(this.layer)
    )
      this.contours(ctx);
    if (this.detailActive && this.showNature) drawNature(this, ctx);
    if (this.showGrid) {
      ctx.strokeStyle = "#e4e7cf16";
      ctx.lineWidth = 0.7;
      const step =
        this.zoom < 3 ? 30 : this.zoom < 10 ? 10 : this.zoom < 32 ? 2 : 0.5;
      const bounds = this.visibleBounds();
      const sample = Math.min(2, step / 3);
      for (
        let lat = Math.ceil(bounds.south / step) * step;
        lat <= bounds.north;
        lat += step
      ) {
        const points = [];
        for (
          let lon = bounds.west;
          lon <= bounds.east + sample / 2;
          lon += sample
        )
          points.push({ lat, lon: Math.min(lon, bounds.east) });
        this.line(points, ctx);
      }
      for (
        let lon = Math.ceil(bounds.west / step) * step;
        lon <= bounds.east;
        lon += step
      ) {
        const points = [];
        for (
          let lat = bounds.south;
          lat <= bounds.north + sample / 2;
          lat += sample
        )
          points.push({ lat: Math.min(lat, bounds.north), lon });
        this.line(points, ctx);
      }
    }
    if (
      this.showRivers &&
      ["atlas", "drainage", "elevation"].includes(this.layer)
    ) {
      ctx.strokeStyle = "#8ec6c5aa";
      ctx.lineWidth = 0.8;
      ctx.beginPath();
      for (const pair of this.rivers) this.line(pair, ctx, true);
      ctx.stroke();
    }
    if (this.location) {
      const p = this.point(this.location.lat, this.location.lon);
      if (p.visible) {
        ctx.strokeStyle = "#fff3c9";
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.arc(p.x, p.y, 9, 0, Math.PI * 2);
        ctx.moveTo(p.x - 15, p.y);
        ctx.lineTo(p.x + 15, p.y);
        ctx.moveTo(p.x, p.y - 15);
        ctx.lineTo(p.x, p.y + 15);
        ctx.stroke();
      }
    }

    for (const e of this.world.state.entities) {
      const p = this.point(e.lat, e.lon);
      if (!p.visible) continue;
      ctx.beginPath();
      ctx.arc(p.x, p.y, e.id === this.selected ? 7 : 5, 0, Math.PI * 2);
      ctx.fillStyle = "#e7c58e";
      ctx.fill();
      ctx.strokeStyle = "#292c29";
      ctx.lineWidth = 2;
      ctx.stroke();
      ctx.font = "12px Georgia";
      ctx.textAlign = "center";
      ctx.lineWidth = 3;
      ctx.strokeStyle = "#14252be0";
      ctx.strokeText(e.name, p.x, p.y - 13);
      ctx.fillStyle = "#f2e7cf";
      ctx.fillText(e.name, p.x, p.y - 13);
    }
    this.scaleBar(ctx);
    drawHumanScale(this, ctx);
  }
  contours(ctx) {
    if (this.zoom < 2) return;
    const { grid } = this.world.definition,
      { width: w, height: h } = grid;
    const values = this.world.state.elevation;
    const interval = this.zoom >= 8 ? 250 : 500;
    ctx.lineWidth = 0.8;
    ctx.beginPath();
    if (!this.contourCache) {
      this.contourCache = [];
      for (let y = 0; y < h - 1; y++)
        for (let x = 0; x < w; x++) {
          const v = [
            values[y * w + x],
            values[y * w + ((x + 1) % w)],
            values[(y + 1) * w + ((x + 1) % w)],
            values[(y + 1) * w + x],
          ];
          const low = Math.min(...v),
            high = Math.max(...v);
          // Most cells cross no contour. Allocate geometry only for actual crossings.
          const first = Math.max(0, Math.ceil(low / 250) * 250);
          if (first > high) continue;
          const lat = 90 - ((y + 0.5) * 180) / h,
            lon = -180 + ((x + 0.5) * 360) / w;
          const vertices = [
            { lat, lon },
            { lat, lon: lon + 360 / w },
            { lat: lat - 180 / h, lon: lon + 360 / w },
            { lat: lat - 180 / h, lon },
          ];
          for (let level = first; level <= high; level += 250)
            for (const triangle of [
              [0, 1, 2],
              [0, 2, 3],
            ]) {
              const crossings = [];
              for (let edge = 0; edge < 3; edge++) {
                const a = triangle[edge],
                  b = triangle[(edge + 1) % 3];
                if (v[a] < level === v[b] < level) continue;
                const t = (level - v[a]) / (v[b] - v[a]);
                crossings.push({
                  lat:
                    vertices[a].lat + (vertices[b].lat - vertices[a].lat) * t,
                  lon:
                    vertices[a].lon + (vertices[b].lon - vertices[a].lon) * t,
                });
              }
              if (crossings.length === 2)
                this.contourCache.push({ level, points: crossings });
            }
        }
    }
    for (const segment of this.contourCache) {
      if (
        segment.level &&
        ((this.layer !== "elevation" && this.zoom < 5) ||
          segment.level % interval)
      )
        continue;
      const p = this.point(segment.points[0].lat, segment.points[0].lon),
        q = this.point(segment.points[1].lat, segment.points[1].lon);
      if (
        (!p.visible && !q.visible) ||
        Math.max(p.x, q.x) < 0 ||
        Math.min(p.x, q.x) > this.w ||
        Math.max(p.y, q.y) < 0 ||
        Math.min(p.y, q.y) > this.h
      )
        continue;
      this.line(segment.points, ctx, true);
    }
    ctx.strokeStyle = "#e6dbb860";
    ctx.stroke();
  }
  visibleBounds() {
    if (this.projection === "flat")
      return {
        west: (-this.left / (2 * this.scale)) * 360 - 180,
        east: ((this.w - this.left) / (2 * this.scale)) * 360 - 180,
        south: Math.max(-90, 90 - ((this.h - this.top) / this.scale) * 180),
        north: Math.min(90, 90 + (this.top / this.scale) * 180),
      };
    const center = this.geographic(this.w / 2, this.h / 2);
    const points = [];
    for (let y = 0; y <= 4; y++)
      for (let x = 0; x <= 4; x++) {
        const p = this.geographic((this.w * x) / 4, (this.h * y) / 4);
        if (!p || !center)
          return { west: -180, east: 180, south: -90, north: 90 };
        points.push({ lat: p.lat, lon: center.lon + wrap(p.lon - center.lon) });
      }
    const south = Math.max(-90, Math.min(...points.map((p) => p.lat)) - 2),
      north = Math.min(90, Math.max(...points.map((p) => p.lat)) + 2);
    return {
      south,
      north,
      west:
        south < -88 || north > 88
          ? -180
          : Math.min(...points.map((p) => p.lon)) - 2,
      east:
        south < -88 || north > 88
          ? 180
          : Math.max(...points.map((p) => p.lon)) + 2,
    };
  }
  scaleBar(ctx) {
    const center = this.geographic(this.w / 2, this.h / 2);
    const offset = this.geographic(this.w / 2 + 80, this.h / 2);
    const radius = this.world.definition.planet.radius;
    const nativeKm =
      (Math.PI * radius) / this.world.definition.grid.height / 1000;
    ctx.font = "11px system-ui";
    ctx.textAlign = "left";
    const spacing = Number(this.canvas.dataset.terrainSpacing);
    const text = this.detailActive
      ? `Terreno · ${spacing < 1 ? spacing.toFixed(2) + " m" : spacing < 1000 ? Math.round(spacing) + " m" : (spacing / 1000).toFixed(1) + " km"} por amostra`
      : `${this.zoom.toFixed(1)}× · grade ${nativeKm.toFixed(1)} km N–S`;
    this.canvas.dataset.terrainModel = this.detailActive
      ? SURFACE_MODEL
      : "global-grid";
    this.canvas.dataset.zoom = this.zoom;
    this.canvas.dataset.resolution = nativeKm;
    const contoursVisible =
      !this.detailActive &&
      this.zoom >= 2 &&
      ["atlas", "elevation", "drainage"].includes(this.layer);
    ctx.fillStyle = "#101c22e8";
    ctx.fillRect(
      14,
      14,
      Math.max(235, ctx.measureText(text).width + 24),
      contoursVisible || this.detailActive ? 80 : 55,
    );
    ctx.fillStyle = "#e9e5d4";
    ctx.fillText(text, 24, 33);
    if (contoursVisible)
      ctx.fillText(
        this.layer === "elevation" || this.zoom >= 5
          ? `Curvas ${this.zoom >= 8 ? 250 : 500} m · costa 0 m`
          : "Costa · altitude 0 m",
        24,
        76,
      );
    if (this.detailActive && this.canvas.dataset.terrainReady !== "true") {
      ctx.fillText(
        this.surface.error || "Carregando blocos de terreno…",
        24,
        76,
      );
    }
    if (center && offset) {
      const km = distance(center, offset, radius) / 1000;
      const power = 10 ** Math.floor(Math.log10(km));
      const length =
        [1, 2, 5, 10].filter((x) => x * power <= km).at(-1) * power;
      const pixels = (80 * length) / km;
      ctx.strokeStyle = "#e9e5d4";
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(24, 47);
      ctx.lineTo(24, 53);
      ctx.moveTo(24, 50);
      ctx.lineTo(24 + pixels, 50);
      ctx.lineTo(24 + pixels, 47);
      ctx.stroke();
      ctx.fillText(
        `${length >= 1 ? length.toLocaleString("pt-BR") + " km" : Math.round(length * 1000) + " m"} no centro`,
        34 + pixels,
        54,
      );
    } else
      ctx.fillText("Arraste para reencontrar o planeta · ⌖ enquadra", 24, 54);
  }
  line(points, ctx, append = false) {
    if (!append) ctx.beginPath();
    let prev;
    for (const geo of points) {
      const p = this.point(geo.lat, geo.lon);
      if (!p.visible || (prev && Math.abs(p.x - prev.x) > this.scale)) {
        prev = null;
        continue;
      }
      if (prev) ctx.lineTo(p.x, p.y);
      else ctx.moveTo(p.x, p.y);
      prev = p;
    }
    if (!append) ctx.stroke();
  }
  hit(x, y) {
    return this.world?.state.entities.find((e) => {
      const p = this.point(e.lat, e.lon);
      return p.visible && Math.hypot(p.x - x, p.y - y) < 18;
    });
  }
}
