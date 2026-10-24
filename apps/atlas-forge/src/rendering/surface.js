import {
  surfaceContext,
  generateSurfaceTile,
  TILE_SIZE,
  maxSurfaceLevel,
  hash32,
} from "./surface-field.js";
import { wrap, clamp } from "../geometry/sphere.js";
const FADE_MS = 180;
const keyOf = (z, x, y) => `${z}/${x}/${y}`;
const wrappedX = (x, n) => ((x % (2 * n)) + 2 * n) % (2 * n);

/** One tile pyramid for every camera scale. Ancestors remain until children arrive. */
export class SurfaceTiles {
  constructor(redraw) {
    this.redraw = redraw;
    this.cache = new Map();
    this.epoch = 0;
    this.signature = "";
    this.wanted = new Set();
    this.cacheRevision = 0;
  }
  setWorld(world, derived) {
    this.context = surfaceContext(world, derived);
    this.epoch++;
    this.cache.clear();
    this.cacheRevision++;
    this.signature = "";
    this.wanted.clear();
    this.error = null;
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
    const make = (rgba) => {
      const canvas = document.createElement("canvas");
      canvas.width = canvas.height = TILE_SIZE;
      canvas
        .getContext("2d")
        .putImageData(new ImageData(rgba, TILE_SIZE, TILE_SIZE), 0, 0);
      return canvas;
    };
    this.cacheRevision++;
    this.cache.set(keyOf(data.z, data.x, data.y), {
      ...data,
      canvas: make(data.rgba),
      elevationCanvas: make(data.topo),
      born: performance.now(),
    });
    while (this.cache.size > 96) {
      const key = [...this.cache.keys()].find(
        (k) => !this.wanted.has(k) && !k.startsWith("0/"),
      );
      if (!key) break;
      this.cache.delete(key);
    }
    this.redraw();
  }
  pause() {
    this.active = false;
    this.signature = "";
    if (this.worker)
      this.worker.postMessage({ type: "tiles", epoch: this.epoch, tiles: [] });
  }
  bounds(renderer) {
    if (renderer.projection === "flat") {
      const b = renderer.visibleBounds();
      return {
        ...b,
        west: Math.max(b.west, (b.west + b.east) / 2 - 180),
        east: Math.min(b.east, (b.west + b.east) / 2 + 180),
      };
    }
    const center = renderer.geographic(renderer.w / 2, renderer.h / 2);
    const points = [];
    for (let y = 0; y <= 4; y++)
      for (let x = 0; x <= 4; x++) {
        const p = renderer.geographic(
          (renderer.w * x) / 4,
          (renderer.h * y) / 4,
        );
        if (!p || !center)
          return { west: -180, east: 180, south: -90, north: 90 };
        points.push({ lat: p.lat, lon: center.lon + wrap(p.lon - center.lon) });
      }
    return {
      west: Math.min(...points.map((p) => p.lon)),
      east: Math.max(...points.map((p) => p.lon)),
      south: Math.min(...points.map((p) => p.lat)),
      north: Math.max(...points.map((p) => p.lat)),
    };
  }
  range(bounds, z) {
    const n = 2 ** z;
    return {
      firstX: Math.floor(((bounds.west + 180) / 180) * n),
      lastX: Math.ceil(((bounds.east + 180) / 180) * n) - 1,
      firstY: Math.max(0, Math.floor(((90 - bounds.north) / 180) * n)),
      lastY: Math.min(n - 1, Math.floor(((90 - bounds.south) / 180) * n)),
    };
  }
  render(renderer, ctx) {
    this.active = true;
    this.start();
    const bounds = this.bounds(renderer),
      radius = this.context.radius;
    // Latitude pitch controls the screen footprint; the field never depends on LOD.
    const mpp =
      renderer.projection === "flat"
        ? (Math.PI * radius) / renderer.scale
        : renderer.metresPerPixel();
    let z = clamp(
      Math.ceil(
        Math.log2((Math.PI * radius) / (TILE_SIZE * Math.max(0.001, mpp))),
      ),
      0,
      maxSurfaceLevel(radius),
    );
    if (!Number.isFinite(z)) z = 0;
    let range;
    for (;;) {
      range = this.range(bounds, z);
      if (
        (range.lastX - range.firstX + 1) * (range.lastY - range.firstY + 1) <=
          48 ||
        z === 0
      )
        break;
      z--;
    }
    const levels = [
      ...new Set([
        0,
        Math.max(0, z - 4),
        Math.max(0, z - 2),
        Math.max(0, z - 1),
        z,
      ]),
    ].sort((a, b) => a - b);
    const wanted = [];
    for (const level of levels) {
      const r =
          level === 0
            ? { firstX: 0, lastX: 1, firstY: 0, lastY: 0 }
            : this.range(bounds, level),
        n = 2 ** level;
      for (let y = r.firstY; y <= r.lastY; y++)
        for (let x = r.firstX; x <= r.lastX; x++)
          wanted.push([level, wrappedX(x, n), y]);
    }
    this.wanted = new Set(wanted.map((t) => keyOf(...t)));
    const missing = wanted.filter((t) => !this.cache.has(keyOf(...t)));
    const signature = `${this.epoch}:${missing.map((t) => t.join("/")).join(",")}`;
    if (signature !== this.signature) {
      this.signature = signature;
      if (this.worker)
        this.worker.postMessage({
          type: "tiles",
          epoch: this.epoch,
          tiles: missing,
        });
      else {
        this.fallbackQueue = missing;
        if (!this.fallbackRunning) this.pumpFallback();
      }
    }
    const now = performance.now();
    this.level = z;
    this.levelBlend = clamp(
      Math.log2((Math.PI * radius) / (TILE_SIZE * mpp)) - z + 1,
      0,
      1,
    );
    this.fading = false;
    // Cached intermediate levels also remain usable when reversing or panning.
    const tiles = [...this.cache.values()]
      .filter((t) => t.z <= z)
      .sort((a, b) => a.z - b.z);
    if (renderer.projection === "flat") this.flat(renderer, ctx, tiles, now);
    else this.globe(renderer, ctx, tiles, z, now);
    let ready = true;
    for (let y = range.firstY; y <= range.lastY; y++)
      for (let x = range.firstX; x <= range.lastX; x++) {
        const tile = this.cache.get(keyOf(z, wrappedX(x, 2 ** z), y));
        if (!tile) ready = false;
        else if (tile.z > 0 && now - tile.born < FADE_MS) {
          ready = false;
          this.fading = true;
        }
      }
    renderer.canvas.dataset.terrainReady = String(ready);
    renderer.canvas.dataset.terrainLevel = String(z);
    renderer.canvas.dataset.terrainSpacing = String(
      (Math.PI * radius) / (2 ** z * TILE_SIZE),
    );
    renderer.canvas.dataset.terrainTiles = String(this.cache.size);
    renderer.canvas.dataset.terrainPipeline = "continuous-pyramid";
    if (this.fading) this.redraw();
    return true;
  }
  pumpFallback() {
    this.fallbackRunning = true;
    const epoch = this.epoch;
    setTimeout(() => {
      const tile = this.fallbackQueue?.shift();
      if (tile && epoch === this.epoch)
        this.accept(generateSurfaceTile(this.context, ...tile));
      this.fallbackRunning = false;
      if (this.fallbackQueue?.length) this.pumpFallback();
    }, 0);
  }
  alpha(tile, now) {
    const a = tile.z === 0 ? 1 : clamp((now - tile.born) / FADE_MS, 0, 1);
    if (a < 1) this.fading = true;
    return a * (tile.z === this.level && tile.z > 0 ? this.levelBlend : 1);
  }
  flat(renderer, ctx, tiles, now) {
    ctx.imageSmoothingEnabled = false;
    const span = renderer.scale * 2;
    for (const tile of tiles) {
      const n = 2 ** tile.z,
        size = renderer.scale / n;
      const y = renderer.top + tile.y * size;
      if (y > renderer.h || y + size < 0) continue;
      const origin = renderer.left + tile.x * size;
      const first = Math.ceil((-origin - size) / span),
        last = Math.floor((renderer.w - origin) / span);
      for (let copy = first; copy <= last; copy++) {
        const x = origin + copy * span;
        ctx.globalAlpha = this.alpha(tile, now);
        ctx.drawImage(
          renderer.layer === "elevation" ? tile.elevationCanvas : tile.canvas,
          x,
          y,
          size + 0.1,
          size + 0.1,
        );
        if (
          renderer.layer !== "elevation" &&
          tile.z === maxSurfaceLevel(this.context.radius)
        ) {
          // Blades emerge continuously at their physical size, without a ground mode.
          ctx.globalAlpha *= clamp((size / TILE_SIZE - 3) / 14, 0, 1);
          if (ctx.globalAlpha > 0)
            this.grass(
              ctx,
              renderer,
              tile,
              -180 + (tile.x * 180) / n,
              90 - (tile.y * 180) / n,
              180 / n,
            );
        }
      }
    }
    ctx.globalAlpha = 1;
  }
  globe(renderer, ctx, tiles, z, now) {
    // Inverse projection covers the limb and poles without dropped tile triangles.
    const density = renderer.ratio,
      w = Math.ceil(renderer.w * density),
      h = Math.ceil(renderer.h * density);
    const camera = `${w}/${h}/${renderer.scale}/${renderer.pan.x}/${renderer.pan.y}`;
    if (this.projection?.camera !== camera) {
      const canvas = document.createElement("canvas");
      canvas.width = w;
      canvas.height = h;
      const off = canvas.getContext("2d"),
        im = off.createImageData(w, h);
      const u = new Float64Array(w * h).fill(-1),
        v = new Float64Array(w * h);
      for (let y = 0; y < h; y++)
        for (let x = 0; x < w; x++) {
          const p = renderer.geographic(
            (x + 0.5) / density,
            (y + 0.5) / density,
          );
          if (p) {
            const k = y * w + x;
            u[k] = (wrap(p.lon - renderer.rotation) + 180) / 360;
            v[k] = clamp((90 - p.lat) / 180, 0, 1 - 1e-12);
          }
        }
      this.projection = { camera, canvas, off, im, u, v };
    }
    const { canvas, off, im, u, v } = this.projection,
      n = 2 ** z;
    const frameKey = [this.epoch, this.cacheRevision, camera, renderer.rotation, z, renderer.layer, this.levelBlend].join(":");
    if (this.projection.frameFinal && this.projection.frameKey === frameKey) {
      ctx.imageSmoothingEnabled = false;
      ctx.drawImage(canvas, 0, 0, renderer.w, renderer.h);
      return;
    }
    const groups = new Map();
    for (let k = 0; k < u.length; k++) {
      if (u[k] < 0) continue;
      const longitude = (((u[k] + renderer.rotation / 360) % 1) + 1) % 1;
      const x = Math.floor(longitude * 2 * n),
        y = Math.floor(v[k] * n),
        id = x + y * 2 * n;
      let chain = groups.get(id);
      if (!chain) {
        chain = [];
        for (let level = 0; level <= z; level++) {
          const q = 2 ** level,
            tile = this.cache.get(
              keyOf(level, Math.floor(longitude * 2 * q), Math.floor(v[k] * q)),
            );
          if (tile) {
            const alpha = this.alpha(tile, now);
            if (alpha === 1) chain = [];
            chain.push([tile, alpha]);
          }
        }
        groups.set(id, chain);
      }
      const out = k * 4;
      im.data[out + 3] = chain.length ? 255 : 0;
      for (const [tile, alpha] of chain) {
        const q = 2 ** tile.z;
        const fx = clamp(
            (longitude * 2 * q - tile.x) * TILE_SIZE - 0.5,
            0,
            TILE_SIZE - 1,
          ),
          fy = clamp((v[k] * q - tile.y) * TILE_SIZE - 0.5, 0, TILE_SIZE - 1);
        const ix = Math.min(TILE_SIZE - 1, Math.floor(fx + 0.5)),
          iy = Math.min(TILE_SIZE - 1, Math.floor(fy + 0.5));
        const src = (iy * TILE_SIZE + ix) * 4;
        const data = renderer.layer === "elevation" ? tile.topo : tile.rgba;
        for (let channel = 0; channel < 3; channel++) {
          const value = data[src + channel];
          im.data[out + channel] =
            alpha === 1
              ? value
              : im.data[out + channel] * (1 - alpha) + value * alpha;
        }
      }
    }
    off.putImageData(im, 0, 0);
    this.projection.frameKey = frameKey;
    this.projection.frameFinal = !this.fading;
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(canvas, 0, 0, renderer.w, renderer.h);
    if (
      renderer.layer !== "elevation" &&
      z === maxSurfaceLevel(this.context.radius)
    ) {
      const alpha = clamp(
        ((Math.PI * this.context.radius) /
          (2 ** z * TILE_SIZE) /
          renderer.metresPerPixel() -
          3) /
          14,
        0,
        1,
      );
      for (const tile of tiles.filter((t) => t.z === z)) {
        ctx.globalAlpha = alpha * this.alpha(tile, now);
        if (ctx.globalAlpha > 0)
          this.grass(
            ctx,
            renderer,
            tile,
            -180 + (tile.x * 180) / n,
            90 - (tile.y * 180) / n,
            180 / n,
          );
      }
      ctx.globalAlpha = 1;
    }
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
