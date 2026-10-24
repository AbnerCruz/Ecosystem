import { vegetation } from "../geometry/ecology.js";
import { clamp, index, RAD } from "../geometry/sphere.js";
import { hash32, sampleSurface } from "./surface-field.js";

// Cartographic symbols, not individual organisms or mineral deposits.
// Locations are quantised in physical metres and never persisted to world state.
const CELL_METRES = 32;
const MAX_CANDIDATES = 3200;
const MAX_MARKS = 1800;
const U32 = 4294967296;
const random = (a, b, salt, seed) => hash32(a, b, salt, seed) / U32;

export function nearbyNature(world, surface, bounds, mpp) {
  if (
    !world ||
    !surface ||
    !Number.isFinite(mpp) ||
    mpp <= 0 ||
    mpp > 1.8
  )
    return [];
  const radius = surface.radius;
  const rows = Math.max(1, Math.round((Math.PI * radius) / CELL_METRES));
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
    const cols = Math.max(1, Math.round(circumference / CELL_METRES));
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
      if ((material === 1 || material === 2) && roll < forest)
        kind = "tree";
      else if (roll < forest + shrubs) kind = "shrub";
      else if (roll < forest + shrubs + stones) kind = "stone";
      else continue;
      result.push({
        ...point,
        kind,
        // Stable, measurable symbol footprint; not a botanically validated size.
        diameterMetres:
          (kind === "tree" ? 5 : kind === "shrub" ? 1.8 : 2.5) *
          (0.75 + random(y, x, 14, surface.seed) * 0.7),
      });
    }
  }
  return result;
}

export function drawNature(renderer, ctx) {
  const mpp = renderer.metresPerPixel();
  if (renderer.layer !== "atlas" || !renderer.showNature || mpp > 1.8)
    return;
  const surface = renderer.surface?.context;
  if (!surface) return;
  const marks = nearbyNature(
    renderer.world,
    surface,
    renderer.visibleBounds(),
    mpp,
  );
  ctx.save();
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
    const radius = Math.max(0.7, size / 2);
    ctx.beginPath();
    if (mark.kind === "tree") {
      ctx.fillStyle = "#205a3bc9";
      ctx.strokeStyle = "#9bc783d8";
      ctx.lineWidth = Math.max(0.5, Math.min(2, size / 12));
      ctx.arc(p.x, p.y, radius, 0, Math.PI * 2);
      ctx.fill();
      if (size > 8) {
        ctx.stroke();
        ctx.fillStyle = "#365437";
        ctx.beginPath();
        ctx.arc(p.x, p.y, Math.max(0.8, size * 0.1), 0, Math.PI * 2);
        ctx.fill();
      }
    } else if (mark.kind === "shrub") {
      ctx.fillStyle = "#6d9655d0";
      ctx.ellipse(p.x, p.y, radius, radius * 0.8, 0, 0, Math.PI * 2);
      ctx.fill();
    } else {
      ctx.fillStyle = "#9b9c8cc9";
      ctx.strokeStyle = "#424e47d8";
      ctx.lineWidth = 0.8;
      ctx.moveTo(p.x - radius, p.y + radius * 0.2);
      ctx.lineTo(p.x - radius * 0.3, p.y - radius);
      ctx.lineTo(p.x + radius * 0.85, p.y - radius * 0.45);
      ctx.lineTo(p.x + radius, p.y + radius * 0.65);
      ctx.closePath();
      ctx.fill();
      if (size > 6) ctx.stroke();
    }
  }
  ctx.restore();
}

// Fixed on-screen inset; the displayed dimensions scale with actual m/pixel.
// A human's height is presented as a vertical diagram, not map footprint.
export function drawHumanScale(renderer, ctx) {
  if (!renderer.showHumanScale || !renderer.detailActive) return;
  const mpp = renderer.metresPerPixel();
  if (!Number.isFinite(mpp) || mpp <= 0 || mpp > 3) return;
  const x = Math.max(6, renderer.w - 194);
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
      : housePixels > 110
        ? "Casa maior que o quadro"
        : "Dimensões proporcionais",
    x + 10,
    y + 94,
  );
  if (housePixels >= 6 && housePixels <= 110) {
    ctx.strokeStyle = "#f2e7cf";
    ctx.lineWidth = 1;
    ctx.strokeRect(
      x + 50,
      y + 73 - (8 / mpp) * 0.25,
      housePixels,
      (8 / mpp) * 0.25,
    );
  }
  ctx.restore();
}
