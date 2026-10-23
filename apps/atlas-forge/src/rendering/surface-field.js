import { clamp, index, coord, RAD, wrap } from "../geometry/sphere.js";
export const SURFACE_MODEL = "spherical-detail-1";
export const TILE_SIZE = 96;
export const MATERIALS = ["Água", "Grama", "Mata", "Areia", "Rocha", "Neve"];
export function hash32(a, b, c, seed = 0) {
  let h =
    Math.imul(a | 0, 374761393) ^
    Math.imul(b | 0, 668265263) ^
    Math.imul(c | 0, 2147483647) ^
    seed;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return (h ^ (h >>> 16)) >>> 0;
}
function noise(x, y, z, seed) {
  const a = Math.floor(x),
    b = Math.floor(y),
    c = Math.floor(z);
  const fade = (t) => t * t * (3 - 2 * t),
    u = fade(x - a),
    v = fade(y - b),
    w = fade(z - c);
  const lerp = (p, q, t) => p + (q - p) * t;
  const n = (dx, dy, dz) =>
    (hash32(a + dx, b + dy, c + dz, seed) / 4294967295) * 2 - 1;
  return lerp(
    lerp(lerp(n(0, 0, 0), n(1, 0, 0), u), lerp(n(0, 1, 0), n(1, 1, 0), u), v),
    lerp(lerp(n(0, 0, 1), n(1, 0, 1), u), lerp(n(0, 1, 1), n(1, 1, 1), u), v),
    w,
  );
}
export function surfaceContext(world, derived) {
  let seed = 2166136261;
  for (const c of `${world.definition.seed}:${SURFACE_MODEL}`)
    seed = Math.imul(seed ^ c.charCodeAt(0), 16777619) >>> 0;
  const { grid, planet } = world.definition;
  let north = 0,
    south = 0;
  for (let x = 0; x < grid.width; x++) {
    north += world.state.elevation[x];
    south += world.state.elevation[(grid.height - 1) * grid.width + x];
  }
  const context = {
    seed,
    grid,
    radius: planet.radius,
    elevation: world.state.elevation,
    biome: derived.biome,
    temperature: derived.temperature,
    rain: derived.rain,
    north: north / grid.width,
    south: south / grid.width,
    nodeResiduals: new Map(),
  };
  context.northResidual = rawResidual(context, 90, 0);
  context.southResidual = rawResidual(context, -90, 0);
  return context;
}
export function baseHeight(ctx, lat, lon, residual = false) {
  const north = residual ? ctx.northResidual : ctx.north,
    south = residual ? ctx.southResidual : ctx.south;
  const read = (i) => {
    if (!residual) return ctx.elevation[i];
    if (ctx.nodeResiduals.has(i)) return ctx.nodeResiduals.get(i);
    const p = coord(i, ctx.grid),
      v = rawResidual(ctx, p.lat, p.lon);
    ctx.nodeResiduals.set(i, v);
    if (ctx.nodeResiduals.size > 8192)
      ctx.nodeResiduals.delete(ctx.nodeResiduals.keys().next().value);
    return v;
  };
  const { width: w, height: h } = ctx.grid;
  const fx = ((wrap(lon) + 180) / 360) * w - 0.5,
    fy = ((90 - clamp(lat, -90, 90)) / 180) * h - 0.5;
  const x = Math.floor(fx),
    y = Math.floor(fy),
    tx = fx - x,
    ty = fy - y;
  const row = (yy) => {
    if (yy < 0) return north;
    if (yy >= h) return south;
    const a = read(yy * w + (((x % w) + w) % w)),
      b = read(yy * w + ((((x + 1) % w) + w) % w));
    return a + (b - a) * tx;
  };
  // Pole value is unique for every longitude; interpolate from last centre to pole.
  if (fy < 0) return north + (row(0) - north) * Math.max(0, fy * 2 + 1);
  if (fy > h - 1)
    return (
      ctx.south + (row(h - 1) - ctx.south) * Math.max(0, (h - 0.5 - fy) * 2)
    );
  return row(y) + (row(y + 1) - row(y)) * ty;
}
function rawResidual(ctx, lat, lon) {
  const p = lat * RAD,
    l = wrap(lon) * RAD,
    r = ctx.radius;
  const x = r * Math.cos(p) * Math.cos(l),
    y = r * Math.sin(p),
    z = r * Math.cos(p) * Math.sin(l);

  let detail = 0;
  // A single continuous field at every LOD; no per-tile RNG and no rescaled continents.
  const scales = [24000, 6000, 1500, 375, 94, 24, 6, 1.5];
  const amplitudes = [450, 140, 45, 12, 3.5, 0.9, 0.25, 0.06];
  for (let k = 0; k < scales.length; k++)
    detail +=
      noise(x / scales[k], y / scales[k], z / scales[k], ctx.seed + k * 7919) *
      amplitudes[k];
  return detail;
}
function interpolatedClimate(ctx, values, lat, lon) {
  const { width: w, height: h } = ctx.grid;
  const fx = ((wrap(lon) + 180) / 360) * w - 0.5,
    fy = clamp(((90 - lat) / 180) * h - 0.5, 0, h - 1);
  const x = Math.floor(fx),
    y = Math.floor(fy),
    tx = fx - x,
    ty = fy - y;
  const read = (xx, yy) =>
    values[Math.min(h - 1, yy) * w + (((xx % w) + w) % w)];
  const a = read(x, y) * (1 - tx) + read(x + 1, y) * tx,
    b = read(x, y + 1) * (1 - tx) + read(x + 1, y + 1) * tx;
  return a + (b - a) * ty;
}
export function sampleSurface(ctx, lat, lon) {
  const base = baseHeight(ctx, lat, lon);
  const detail = rawResidual(ctx, lat, lon) - baseHeight(ctx, lat, lon, true);
  const coast = clamp(Math.abs(base) / 120, 0, 1);
  const elevation = base + detail * coast;
  const biome = ctx.biome[index(lat, lon, ctx.grid)];
  let material;
  if (ctx.temperature && ctx.rain) {
    // Continuous parent climate plus the same standard lapse-rate approximation.
    // Nearest-neighbour biome IDs would create continent-sized rectangular patches.
    const temperature =
      interpolatedClimate(ctx, ctx.temperature, lat, lon) -
      0.0065 * (elevation - base);
    const rain = interpolatedClimate(ctx, ctx.rain, lat, lon);
    material =
      elevation <= 0
        ? 0
        : temperature < 0
          ? 5
          : elevation > 3300
            ? 4
            : rain < 170
              ? 3
              : rain < 400
                ? 1
                : temperature < 6
                  ? 2
                  : rain < 850
                    ? 1
                    : 2;
  } else
    material =
      elevation <= 0
        ? 0
        : biome === 1 || biome === 2
          ? 5
          : biome === 3
            ? 3
            : elevation > 3300 || biome === 9
              ? 4
              : [6, 7, 8].includes(biome)
                ? 2
                : 1;
  return { elevation, material, base, detail: elevation - base };
}
export function maxSurfaceLevel(radius) {
  return Math.max(1, Math.ceil(Math.log2((Math.PI * radius) / TILE_SIZE)));
}
export function tileCoordinates(z, x, y, u, v) {
  const n = 2 ** z;
  return {
    lat: 90 - ((y + v) * 180) / n,
    lon: wrap(-180 + ((x + u) * 180) / n),
  };
}
export function generateSurfaceTile(ctx, z, x, y) {
  const size = TILE_SIZE,
    n = 2 ** z;
  x = ((x % (2 * n)) + 2 * n) % (2 * n);
  const heights = new Float32Array((size + 2) * (size + 2));
  const material = new Uint8Array(size * size),
    rgba = new Uint8ClampedArray(size * size * 4);
  for (let j = -1; j <= size; j++)
    for (let i = -1; i <= size; i++) {
      const p = tileCoordinates(z, x, y, (i + 0.5) / size, (j + 0.5) / size);
      const s = sampleSurface(ctx, clamp(p.lat, -90, 90), p.lon);
      heights[(j + 1) * (size + 2) + i + 1] = s.elevation;
      if (i >= 0 && i < size && j >= 0 && j < size)
        material[j * size + i] = s.material;
    }
  const topo = new Uint8ClampedArray(size * size * 4);
  const palette = [
    [28, 77, 91],
    [80, 118, 55],
    [47, 91, 47],
    [190, 169, 115],
    [127, 126, 114],
    [225, 233, 230],
  ];
  const metres = (Math.PI * ctx.radius) / (n * size),
    lat = tileCoordinates(z, x, y, 0.5, 0.5).lat;
  for (let j = 0; j < size; j++)
    for (let i = 0; i < size; i++) {
      const k = (j + 1) * (size + 2) + i + 1,
        m = material[j * size + i],
        h = heights[k];
      const dx =
        (heights[k + 1] - heights[k - 1]) /
        (2 * metres * Math.max(0.02, Math.cos(lat * RAD)));
      const dy = (heights[k + size + 2] - heights[k - size - 2]) / (2 * metres);
      const illumination = clamp(
        0.85 + (-dx * 0.6 - dy * 0.4) / Math.sqrt(1 + dx * dx + dy * dy),
        0.35,
        1.3,
      );
      const grain =
        ((hash32(x * size + i, y * size + j, z, ctx.seed) % 19) - 9) * 0.55;
      const c = palette[m];
      for (let a = 0; a < 3; a++)
        rgba[(j * size + i) * 4 + a] = clamp(
          c[a] * illumination + grain + (m === 0 ? clamp(h / 800, -18, 0) : 0),
          0,
          255,
        );
      rgba[(j * size + i) * 4 + 3] = 255;
      const t = clamp((h - 500) / 3000, 0, 1),
        interval = Math.max(1, Math.min(100, metres * 0.8));
      const crossing =
        h > 0 &&
        (Math.floor(h / interval) !== Math.floor(heights[k + 1] / interval) ||
          Math.floor(h / interval) !==
            Math.floor(heights[k + size + 2] / interval));
      const ground =
        h <= 0 ? [24, 67, 85] : [120 + 90 * t, 145 + 55 * t, 94 + 95 * t];
      for (let a = 0; a < 3; a++)
        topo[(j * size + i) * 4 + a] = crossing
          ? ground[a] * 0.62
          : ground[a] * illumination;
      topo[(j * size + i) * 4 + 3] = 255;
    }
  return { z, x, y, rgba, topo, material };
}
