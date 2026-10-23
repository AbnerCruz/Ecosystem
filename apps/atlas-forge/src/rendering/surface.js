import {
  surfaceContext,
  generateSurfaceTile,
  TILE_SIZE,
  maxSurfaceLevel,
  hash32,
} from "./surface-field.js";
import { wrap, distance } from "../geometry/sphere.js";
export class SurfaceTiles {
  constructor(redraw) {
    this.redraw = redraw;
    this.cache = new Map();
    this.epoch = 0;
    this.signature = "";
  }
  setWorld(world, derived) {
    this.context = surfaceContext(world, derived);
    this.epoch++;
    this.cache.clear();
    this.signature = "";
    if (this.worker)
      this.worker.postMessage({
        type: "world",
        epoch: this.epoch,
        context: this.context,
      });
  }
  start() {
    if (this.worker || this.fallback) return;
    if (typeof __TERRAIN_WORKER_SOURCE__ !== "undefined") {
      const url = URL.createObjectURL(
        new Blob([__TERRAIN_WORKER_SOURCE__], { type: "text/javascript" }),
      );
      this.worker = new Worker(url);
      URL.revokeObjectURL(url);
      this.worker.onmessage = ({ data }) => {
        if (data.epoch !== this.epoch) return;
        if (data.error) {
          this.error = data.error;
          this.redraw();
          return;
        }
        this.accept(data);
      };
      this.worker.postMessage({
        type: "world",
        epoch: this.epoch,
        context: this.context,
      });
    } else this.fallback = true;
  }
  accept(data) {
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = TILE_SIZE;
    canvas
      .getContext("2d")
      .putImageData(new ImageData(data.rgba, TILE_SIZE, TILE_SIZE), 0, 0);
    const elevationCanvas = document.createElement("canvas");
    elevationCanvas.width = elevationCanvas.height = TILE_SIZE;
    elevationCanvas
      .getContext("2d")
      .putImageData(new ImageData(data.topo, TILE_SIZE, TILE_SIZE), 0, 0);
    const key = `${data.z}/${data.x}/${data.y}`;
    this.cache.set(key, { ...data, canvas, elevationCanvas });
    while (this.cache.size > 96)
      this.cache.delete(this.cache.keys().next().value);
    this.redraw();
  }
  pause() {
    if (!this.active) return;
    this.active = false;
    this.signature = "";
    if (this.worker)
      this.worker.postMessage({ type: "tiles", epoch: this.epoch, tiles: [] });
  }
  render(renderer, ctx) {
    this.active = true;
    this.start();
    const center = renderer.geographic(renderer.w / 2, renderer.h / 2);
    if (!center) return false;
    const corners = [];
    for (let y = 0; y <= 2; y++)
      for (let x = 0; x <= 2; x++) {
        const p = renderer.geographic(
          (renderer.w * x) / 2,
          (renderer.h * y) / 2,
        );
        if (p)
          corners.push({
            lat: p.lat,
            lon: center.lon + wrap(p.lon - center.lon),
          });
      }
    if (!corners.length) return false;
    const radius = this.context.radius;
    const p = renderer.geographic(renderer.w / 2 + 1, renderer.h / 2 + 1);
    const mpp = p
      ? distance(center, p, radius) / Math.SQRT2
      : (Math.PI * radius) / renderer.scale;
    let z = Math.max(
      0,
      Math.min(
        maxSurfaceLevel(radius),
        Math.ceil(
          Math.log2((Math.PI * radius) / (TILE_SIZE * Math.max(0.001, mpp))),
        ),
      ),
    );
    let n = 2 ** z;
    const west = Math.min(...corners.map((p) => p.lon)),
      east = Math.max(...corners.map((p) => p.lon));
    const north = Math.max(...corners.map((p) => p.lat)),
      south = Math.min(...corners.map((p) => p.lat));
    let firstX, lastX, firstY, lastY;
    for (;;) {
      firstX = Math.floor(((west + 180) / 180) * n);
      lastX = Math.floor(((east + 180) / 180) * n);
      firstY = Math.max(0, Math.floor(((90 - north) / 180) * n));
      lastY = Math.min(n - 1, Math.floor(((90 - south) / 180) * n));
      if ((lastX - firstX + 1) * (lastY - firstY + 1) <= 80 || z === 0) break;
      z--;
      n = 2 ** z;
    }
    const missing = [];
    let ready = 0,
      total = 0;
    ctx.imageSmoothingEnabled = z < maxSurfaceLevel(radius);
    for (let yy = firstY; yy <= lastY; yy++)
      for (let xx = firstX; xx <= lastX; xx++) {
        const x = ((xx % (2 * n)) + 2 * n) % (2 * n),
          key = `${z}/${x}/${yy}`,
          tile = this.cache.get(key);
        total++;
        if (!tile) {
          missing.push([z, x, yy]);
          continue;
        }
        ready++;
        // Touch for true LRU, preserving bounded memory while panning.
        this.cache.delete(key);
        this.cache.set(key, tile);
        const lon = -180 + (xx * 180) / n,
          lat = 90 - (yy * 180) / n;
        const a = renderer.point(lat, lon),
          b = renderer.point(lat, lon + 180 / n),
          c = renderer.point(lat - 180 / n, lon),
          d = renderer.point(lat - 180 / n, lon + 180 / n);
        if (![a, b, c, d].every((p) => p.visible)) continue;
        const image =
          renderer.layer === "elevation" ? tile.elevationCanvas : tile.canvas;
        if (renderer.projection === "flat")
          ctx.drawImage(image, a.x, a.y, b.x - a.x + 0.35, c.y - a.y + 0.35);
        else {
          this.triangle(ctx, image, a, b, c, [
            0,
            0,
            TILE_SIZE,
            0,
            0,
            TILE_SIZE,
          ]);
          this.triangle(ctx, image, d, c, b, [
            TILE_SIZE,
            TILE_SIZE,
            0,
            TILE_SIZE,
            TILE_SIZE,
            0,
          ]);
        }
        if (
          renderer.layer !== "elevation" &&
          z === maxSurfaceLevel(radius) &&
          Math.abs(b.x - a.x) / TILE_SIZE > 7
        )
          this.grass(ctx, renderer, tile, lon, lat, 180 / n);
      }
    const signature = `${this.epoch}:${missing.map((t) => t.join("/")).join(",")}`;
    if (missing.length && signature !== this.signature) {
      this.signature = signature;
      if (this.worker)
        this.worker.postMessage({
          type: "tiles",
          epoch: this.epoch,
          tiles: missing,
        });
      else {
        const version = this.epoch;
        for (const tile of missing)
          setTimeout(() => {
            if (version === this.epoch)
              this.accept(generateSurfaceTile(this.context, ...tile));
          }, 0);
      }
    }
    renderer.canvas.dataset.terrainReady = String(ready === total && total > 0);
    renderer.canvas.dataset.terrainLevel = String(z);
    renderer.canvas.dataset.terrainSpacing = String(
      (Math.PI * radius) / (n * TILE_SIZE),
    );
    renderer.canvas.dataset.terrainTiles = String(this.cache.size);
    return true;
  }
  triangle(ctx, img, a, b, c, uv) {
    const [u0, v0, u1, v1, u2, v2] = uv;
    const det = (u1 - u0) * (v2 - v0) - (u2 - u0) * (v1 - v0);
    const ax = ((b.x - a.x) * (v2 - v0) - (c.x - a.x) * (v1 - v0)) / det;
    const bx = ((c.x - a.x) * (u1 - u0) - (b.x - a.x) * (u2 - u0)) / det;
    const ay = ((b.y - a.y) * (v2 - v0) - (c.y - a.y) * (v1 - v0)) / det;
    const by = ((c.y - a.y) * (u1 - u0) - (b.y - a.y) * (u2 - u0)) / det;
    ctx.save();
    ctx.beginPath();
    const centre = { x: (a.x + b.x + c.x) / 3, y: (a.y + b.y + c.y) / 3 };
    const expand = (p) => {
      const r = Math.hypot(p.x - centre.x, p.y - centre.y);
      return {
        x: p.x + ((p.x - centre.x) / r) * 0.7,
        y: p.y + ((p.y - centre.y) / r) * 0.7,
      };
    };
    const aa = expand(a),
      bb = expand(b),
      cc = expand(c);
    ctx.moveTo(aa.x, aa.y);
    ctx.lineTo(bb.x, bb.y);
    ctx.lineTo(cc.x, cc.y);
    ctx.closePath();
    ctx.clip();
    ctx.transform(
      ax,
      ay,
      bx,
      by,
      a.x - ax * u0 - bx * v0,
      a.y - ay * u0 - by * v0,
    );
    ctx.drawImage(img, 0, 0);
    ctx.restore();
  }
  grass(ctx, renderer, tile, lon, lat, span) {
    const step = span / TILE_SIZE;
    for (let y = 0; y < TILE_SIZE; y++)
      for (let x = 0; x < TILE_SIZE; x++) {
        const p = renderer.point(
          lat - (y + 0.5) * step,
          lon + (x + 0.5) * step,
        );
        if (
          p.x < -80 ||
          p.y < -80 ||
          p.x > renderer.w + 80 ||
          p.y > renderer.h + 80
        )
          continue;
        const material = tile.material[y * TILE_SIZE + x];
        if (material !== 1 && material !== 2) continue;
        const q = renderer.point(
          lat - (y + 1.5) * step,
          lon + (x + 1.5) * step,
        );
        const size = Math.min(200, Math.max(3, Math.abs(q.x - p.x))),
          seed = hash32(
            tile.x * TILE_SIZE + x,
            tile.y * TILE_SIZE + y,
            17,
            this.context.seed,
          );
        ctx.strokeStyle = material === 2 ? "#78a855a0" : "#b4c76d9c";
        ctx.lineWidth = Math.max(0.7, Math.min(2, size / 30));
        ctx.beginPath();
        for (let blade = 0; blade < 7; blade++) {
          const h = hash32(seed, blade, 1),
            ox = ((h % 97) / 97 - 0.5) * size * 0.8,
            oy = (((h >>> 8) % 97) / 97 - 0.5) * size * 0.8;
          const length = size * (0.13 + ((h >>> 16) % 50) / 300);
          ctx.moveTo(p.x + ox, p.y + oy);
          ctx.quadraticCurveTo(
            p.x + ox - length * 0.2,
            p.y + oy - length * 0.6,
            p.x + ox + ((h % 3) - 1) * length * 0.3,
            p.y + oy - length,
          );
        }
        ctx.stroke();
      }
  }
}
