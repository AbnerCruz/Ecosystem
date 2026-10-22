import {
  clamp,
  coord,
  index,
  inverseOrtho,
  orthographic,
} from "../geometry/sphere.js";
export const LAYERS = {
  atlas: "Atlas natural",
  elevation: "Altitude",
  temperature: "Temperatura",
  rain: "Precipitação",
  drainage: "Drenagem",
  basins: "Bacias",
  biome: "Biomas",
  authored: "Intervenções",
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
  atlas: ["Oceano", "Planície", "Montanha"],
  elevation: ["−6.000 m", "0 m", "+6.000 m"],
  temperature: ["−30 °C", "10 °C", "45 °C"],
  rain: ["0", "1.000", "3.000 mm/ano"],
  drainage: ["Relevo", "Fluxo > 1.500 m³/s", "Sumidouro"],
  basins: ["Cores por bacia", "Oceânica / fechada"],
  biome: ["Biomas climáticos · heurística"],
  authored: ["Relevo", "Área preservada"],
};
export function color(i, world, d, layer, season = "annual") {
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
  if (!["temperature", "rain", "basins", "biome"].includes(layer) && e > 0) {
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
    this.layer = "atlas";
    this.season = "annual";
    this.raster = document.createElement("canvas");
    this.cache = "";
  }
  setWorld(world, derived) {
    this.world = world;
    this.derived = derived;
    this.cache = "";
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
        x: this.left + ((lon + 180) / 360) * this.scale * 2,
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
    return lon < -180 || lon >= 180 || lat < -90 || lat > 90
      ? null
      : { lat, lon };
  }
  render() {
    this.resize();
    const ctx = this.canvas.getContext("2d");
    ctx.setTransform(this.ratio, 0, 0, this.ratio, 0, 0);
    ctx.clearRect(0, 0, this.w, this.h);
    if (!this.world || !this.derived) return;
    const { grid } = this.world.definition,
      key = `${this.layer}:${this.season}`;
    if (this.cache !== key) {
      this.raster.width = grid.width;
      this.raster.height = grid.height;
      const rc = this.raster.getContext("2d"),
        im = rc.createImageData(grid.width, grid.height);
      for (let i = 0; i < grid.width * grid.height; i++) {
        const c = color(i, this.world, this.derived, this.layer, this.season);
        im.data.set([...c, 255], i * 4);
      }
      rc.putImageData(im, 0, 0);
      this.cache = key;
    }
    if (this.projection === "flat") {
      ctx.imageSmoothingEnabled = true;
      ctx.drawImage(
        this.raster,
        this.left,
        this.top,
        this.scale * 2,
        this.scale,
      );
    } else {
      const r = this.scale * 0.83,
        side = Math.max(1, Math.min(640, Math.ceil(r * 2)));
      const key = `${side}:${grid.width}:${grid.height}`;
      if (this.globeCache?.key !== key) {
        const off = document.createElement("canvas");
        off.width = off.height = side;
        const oc = off.getContext("2d"),
          im = oc.createImageData(side, side);
        const rows = new Int32Array(side * side).fill(-1),
          longitudes = new Float64Array(side * side);
        // Latitude and longitude relative to the camera are invariant under axial rotation.
        // Cache the expensive inverse projection; rotation then only offsets longitude indices.
        for (let y = 0; y < side; y++)
          for (let x = 0; x < side; x++) {
            const p = inverseOrtho(
              ((x + 0.5) / side) * 2 - 1,
              1 - ((y + 0.5) / side) * 2,
              0,
            );
            if (!p) continue;
            const k = y * side + x;
            rows[k] =
              Math.floor(index(p.lat, 0, grid) / grid.width) * grid.width;
            longitudes[k] = ((p.lon + 180) / 360) * grid.width;
          }
        this.globeCache = { key, off, oc, im, rows, longitudes };
      }
      const { off, oc, im, rows, longitudes } = this.globeCache;
      const source = this.raster
        .getContext("2d")
        .getImageData(0, 0, grid.width, grid.height).data;
      const shift = (this.rotation / 360) * grid.width,
        w = grid.width;
      for (let k = 0; k < rows.length; k++) {
        if (rows[k] < 0) continue;
        const i =
            (rows[k] + (((Math.floor(longitudes[k] + shift) % w) + w) % w)) * 4,
          t = k * 4;
        im.data[t] = source[i];
        im.data[t + 1] = source[i + 1];
        im.data[t + 2] = source[i + 2];
        im.data[t + 3] = 255;
      }
      oc.putImageData(im, 0, 0);
      ctx.drawImage(
        off,
        this.w / 2 - r + this.pan.x,
        this.h / 2 - r + this.pan.y,
        r * 2,
        r * 2,
      );
    }
    if (this.showGrid) {
      ctx.strokeStyle = "#e4e7cf16";
      ctx.lineWidth = 0.7;
      for (let lat = -60; lat <= 60; lat += 30)
        this.line(
          Array.from({ length: 181 }, (_, i) => ({ lat, lon: -180 + i * 2 })),
          ctx,
        );
      for (let lon = -180; lon < 180; lon += 30)
        this.line(
          Array.from({ length: 91 }, (_, i) => ({ lat: -90 + i * 2, lon })),
          ctx,
        );
    }
    if (
      this.showRivers &&
      ["atlas", "drainage", "elevation"].includes(this.layer)
    ) {
      ctx.strokeStyle = "#8ec6c5aa";
      ctx.lineWidth = 0.8;
      for (let i = 0; i < this.world.state.elevation.length; i++) {
        const j = this.derived.downstream[i];
        if (
          j < 0 ||
          this.derived.discharge[i] < 1500 ||
          this.world.state.elevation[i] <= 0
        )
          continue;
        this.line([coord(i, grid), coord(j, grid)], ctx);
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
  }
  line(points, ctx) {
    ctx.beginPath();
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
    ctx.stroke();
  }
  hit(x, y) {
    return this.world?.state.entities.find((e) => {
      const p = this.point(e.lat, e.lon);
      return p.visible && Math.hypot(p.x - x, p.y - y) < 18;
    });
  }
}
