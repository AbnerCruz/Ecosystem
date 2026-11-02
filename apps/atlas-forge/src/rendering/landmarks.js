import { vegetation } from "../geometry/ecology.js";
import { index, RAD } from "../geometry/sphere.js";
import { hash32, sampleSurface } from "./surface-field.js";
import { natureSprites } from "./pixel-sprites.js";

// Cartographic symbols, not individual organisms or mineral deposits.
// Locations are quantised in physical metres and never persisted to world state.
const CELL_METRES = 32;
const MAX_CANDIDATES = 3200;
const MAX_MARKS = 1800;
const U32 = 4294967296;
const random = (a, b, salt, seed) => hash32(a, b, salt, seed) / U32;

export function nearbyNature(
  world,
  surface,
  bounds,
  mpp,
  spacing = CELL_METRES,
) {
  if (!world || !surface || !Number.isFinite(mpp) || mpp <= 0 || mpp > 1.8)
    return [];
  const fine = spacing < CELL_METRES;
  if (fine && mpp > 0.2) return [];
  const radius = surface.radius;
  const rows = Math.max(1, Math.round((Math.PI * radius) / spacing));
  const firstRow = Math.max(
    0,
    Math.floor(((Math.max(-90, bounds.south) + 90) / 180) * rows),
  );
  const lastRow = Math.min(
    rows - 1,
    Math.ceil(((Math.min(90, bounds.north) + 90) / 180) * rows),
  );
  const result = [];
  let candidates = 0;
  for (let y = firstRow; y <= lastRow; y++) {
    const lat = -90 + ((y + 0.5) / rows) * 180;
    const circumference = 2 * Math.PI * radius * Math.cos(lat * RAD);
    const cols = Math.max(1, Math.round(circumference / spacing));
    const cell = 360 / cols;
    const firstCol = Math.floor((bounds.west + 180) / cell);
    const lastCol = Math.ceil((bounds.east + 180) / cell);
    for (let rawX = firstCol; rawX <= lastCol; rawX++) {
      if (++candidates > MAX_CANDIDATES || result.length >= MAX_MARKS)
        return result;
      const x = ((rawX % cols) + cols) % cols;
      const jitterY = (random(y, x, 11, surface.seed) - 0.5) * 0.6;
      const jitterX = (random(y, x, 12, surface.seed) - 0.5) * 0.6;
      const point = {
        lat: -90 + ((y + 0.5 + jitterY) / rows) * 180,
        lon: -180 + ((x + 0.5 + jitterX) / cols) * 360,
      };
      const material = sampleSurface(surface, point.lat, point.lon).material;
      if (material === 0 || material === 5) continue;
      const eco = world.state.ecosystem;
      const cover = eco
        ? vegetation(world, index(point.lat, point.lon, eco.grid))
        : 1;
      const roll = random(y, x, 13, surface.seed);
      const forest = material === 2 ? 0.55 * cover : 0.1 * cover;
      const shrubs =
        material === 2
          ? 0.32 * cover
          : material === 1
            ? 0.35 * cover
            : material === 3
              ? 0.08
              : 0.015;
      const stones = material === 4 ? 0.7 : material === 3 ? 0.25 : 0.1;
      let kind;
      if (fine) {
        const canopy = material === 2 ? 0.18 * cover : 0.02 * cover;
        const undergrowth = material === 2 ? 0.65 * cover : 0.45 * cover;
        const pebbles = material === 4 ? 0.6 : material === 3 ? 0.3 : 0.12;
        if ((material === 1 || material === 2) && roll < canopy) kind = "tree";
        else if (roll < canopy + undergrowth) kind = "shrub";
        else if (roll < canopy + undergrowth + pebbles) kind = "stone";
        else continue;
      } else if ((material === 1 || material === 2) && roll < forest)
        kind = "tree";
      else if (roll < forest + shrubs) kind = "shrub";
      else if (roll < forest + shrubs + stones) kind = "stone";
      else continue;
      result.push({
        ...point,
        kind,
        // Stable, measurable symbol footprint; not a botanically validated size.
        diameterMetres:
          (kind === "tree"
            ? fine
              ? 3.4
              : 5
            : kind === "shrub"
              ? fine
                ? 1.2
                : 1.8
              : fine
                ? 1.3
                : 2.5) *
          (0.75 + random(y, x, 14, surface.seed) * 0.7),
      });
    }
  }
  return result;
}

export function drawNature(renderer, ctx) {
  const mpp = renderer.metresPerPixel();
  if (renderer.layer !== "atlas" || !renderer.showNature || mpp > 1.8) return;
  const surface = renderer.surface?.context;
  if (!surface) return;
  const bounds = renderer.visibleBounds();
  const fine = mpp <= 0.2;
  let cache = renderer.natureCache;
  if (
    !cache ||
    cache.surface !== surface ||
    cache.ecosystem !== renderer.world.state.ecosystem ||
    cache.fine !== fine ||
    bounds.west < cache.bounds.west ||
    bounds.east > cache.bounds.east ||
    bounds.south < cache.bounds.south ||
    bounds.north > cache.bounds.north
  ) {
    const spanLon = bounds.east - bounds.west;
    const spanLat = bounds.north - bounds.south;
    const padded = {
      west: bounds.west - spanLon * 0.2,
      east: bounds.east + spanLon * 0.2,
      south: Math.max(-90, bounds.south - spanLat * 0.2),
      north: Math.min(90, bounds.north + spanLat * 0.2),
    };
    cache = {
      surface,
      ecosystem: renderer.world.state.ecosystem,
      fine,
      bounds: padded,
      marks: [
        ...nearbyNature(renderer.world, surface, padded, mpp),
        ...(fine ? nearbyNature(renderer.world, surface, padded, mpp, 6) : []),
      ],
    };
    renderer.natureCache = cache;
  }
  const marks = cache.marks;
  if (!renderer.pixelSprites) renderer.pixelSprites = natureSprites();
  ctx.save();
  ctx.imageSmoothingEnabled = false;
  for (const mark of marks) {
    const p = renderer.point(mark.lat, mark.lon);
    const size = mark.diameterMetres / mpp;
    if (
      !p.visible ||
      p.x < -size ||
      p.y < -size ||
      p.x > renderer.w + size ||
      p.y > renderer.h + size
    )
      continue;
    // Nearest-neighbor 16×16 tile with coherent integer pixel clusters.
    const sprites = renderer.pixelSprites;
    const variant =
      hash32(
        Math.round(mark.lat * 100000),
        Math.round(mark.lon * 100000),
        71,
        surface.seed,
      ) % 4;
    const texture = sprites[mark.kind][variant];
    const drawSize = Math.max(2, Math.round(size));
    const px = Math.round((p.x - drawSize / 2) * renderer.ratio) / renderer.ratio;
    const py = Math.round((p.y - drawSize / 2) * renderer.ratio) / renderer.ratio;
    ctx.drawImage(texture, px, py, drawSize, drawSize);
  }
  ctx.restore();
}

// Fixed on-screen inset; the displayed dimensions scale with actual m/pixel.
// A human's height is presented as a vertical diagram, not map footprint.
export function drawHumanScale(renderer, ctx) {
  if (!renderer.showHumanScale || !renderer.detailActive) return;
  const mpp = renderer.metresPerPixel();
  if (!Number.isFinite(mpp) || mpp <= 0 || mpp > 3) return;
  const x = Math.max(6, renderer.w - 246);
  const y = Math.max(6, renderer.h - 110);
  ctx.save();
  ctx.fillStyle = "#101c22e8";
  ctx.fillRect(x, y, 188, 104);
  ctx.font = "11px system-ui";
  ctx.textAlign = "left";
  ctx.fillStyle = "#f2e7cf";
  ctx.fillText("ESCALA HUMANA", x + 10, y + 17);
  const humanPixels = 1.7 / mpp;
  const housePixels = 10 / mpp;
  const houseFits = housePixels >= 6 && housePixels <= 78 && 8 / mpp <= 46;
  if (humanPixels >= 5 && humanPixels <= 63) {
    const hx = x + 25;
    const ground = y + 78;
    ctx.strokeStyle = "#efcf8e";
    ctx.lineWidth = Math.max(1, Math.min(3, humanPixels / 8));
    ctx.beginPath();
    ctx.arc(hx, ground - humanPixels * 0.88, humanPixels * 0.1, 0, Math.PI * 2);
    ctx.moveTo(hx, ground - humanPixels * 0.75);
    ctx.lineTo(hx, ground - humanPixels * 0.32);
    ctx.moveTo(hx, ground - humanPixels * 0.58);
    ctx.lineTo(hx - humanPixels * 0.17, ground - humanPixels * 0.35);
    ctx.moveTo(hx, ground - humanPixels * 0.58);
    ctx.lineTo(hx + humanPixels * 0.17, ground - humanPixels * 0.35);
    ctx.moveTo(hx, ground - humanPixels * 0.32);
    ctx.lineTo(hx - humanPixels * 0.15, ground);
    ctx.moveTo(hx, ground - humanPixels * 0.32);
    ctx.lineTo(hx + humanPixels * 0.15, ground);
    ctx.stroke();
  }
  ctx.fillStyle = "#f2e7cf";
  ctx.fillText("Pessoa · 1,7 m", x + 45, y + 38);
  ctx.fillText("Casa · 10 × 8 m", x + 45, y + 56);
  ctx.fillStyle = "#b6c3bb";
  ctx.fillText(
    mpp > 0.34
      ? "Aproxime para ver a pessoa"
      : houseFits
        ? "Dimensões proporcionais"
        : "Casa maior que o quadro",
    x + 10,
    y + 94,
  );
  if (houseFits) {
    ctx.strokeStyle = "#f2e7cf";
    ctx.lineWidth = 1;
    ctx.strokeRect(x + 105, y + 41, housePixels, 8 / mpp);
  }
  ctx.restore();
}
