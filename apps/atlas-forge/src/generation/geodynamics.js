import { random } from "./random.js";

export const PLATE_MODEL = "spherical-kinematics-1";
const GOLDEN_ANGLE = Math.PI * (3 - Math.sqrt(5));
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const cross = (a, b) => [
  a[1] * b[2] - a[2] * b[1],
  a[2] * b[0] - a[0] * b[2],
  a[0] * b[1] - a[1] * b[0],
];
const magnitude = (v) => Math.hypot(v[0], v[1], v[2]);
const unit = (v) => {
  const n = magnitude(v) || 1;
  return v.map((a) => a / n);
};

/**
 * Plate seeds are distributed over the sphere, not a rectangular longitude grid.
 * Angular velocities are in arbitrary model units; convergence has a
 * physically meaningful sign, but neither uplift nor motion is calibrated.
 */
export function plateModel(seed, count = 24) {
  const rng = random(seed, "kinematic-plates-v1");
  const phase = rng() * 2 * Math.PI;
  // Broad cratons are coherent across multiple kinematic plates. Their
  // margins are a continuous buoyancy field, not Voronoi continent polygons.
  const cratonRng = random(seed, "continental-provinces-v1");
  const cratonPhase = cratonRng() * Math.PI * 2;
  const cratons = Array.from({ length: 6 }, (_, i) => {
    const z = -1 + (2 * (i + 0.5 + (cratonRng() - 0.5) * 0.32)) / 6;
    const phi = i * GOLDEN_ANGLE + cratonPhase + (cratonRng() - 0.5) * 0.65;
    const r = Math.sqrt(Math.max(0, 1 - z * z));
    const reach = 0.62 + cratonRng() * 0.43;
    return { center: [r * Math.cos(phi), z, r * Math.sin(phi)], reach: Math.cos(reach) };
  });
  const plates = [];
  for (let i = 0; i < count; i++) {
    const z = -1 + (2 * (i + 0.5 + (rng() - 0.5) * 0.42)) / count;
    const phi = i * GOLDEN_ANGLE + phase + (rng() - 0.5) * 0.38;
    const r = Math.sqrt(Math.max(0, 1 - z * z));
    const center = [r * Math.cos(phi), z, r * Math.sin(phi)];
    const ref = unit([rng() - 0.5, rng() - 0.5, rng() - 0.5]);
    const direction = unit(cross(center, ref));
    const speed = 0.012 + 0.029 * rng();
    // Euler rotation: velocity at any point p is angularVelocity × p.
    const angularVelocity = cross(center, direction).map((n) => n * speed);
    plates.push({
      center,
      angularVelocity,
      continental: rng() < 0.48 ? 1 : 0,
    });
  }
  return { version: PLATE_MODEL, plates, cratons };
}

/**
 * Continental buoyancy + plate-boundary compression/extension + folded relief.
 * Distances, normals and velocities are spherical; longitude seams and poles
 * are continuous. Values are approximate elevations, not tectonic history.
 */
export function plateElevation(model, p, foldedNoise = 0) {
  let first = -Infinity;
  let second = -Infinity;
  let nearest = null;
  let neighbor = null;
  for (const plate of model.plates) {
    const d = dot(p, plate.center);
    if (d > first) {
      second = first;
      neighbor = nearest;
      first = d;
      nearest = plate;
    } else if (d > second) {
      second = d;
      neighbor = plate;
    }
  }
  if (!neighbor) return foldedNoise * 700;
  // Angular distance difference linearized in cos-space, avoiding two
  // acos calls per terrain cell on memory-limited Android devices.
  const midpoint = Math.max(-0.99, Math.min(0.99, (first + second) / 2));
  const angularGap =
    (first - second) / Math.max(0.18, Math.sqrt(1 - midpoint * midpoint));
  const boundary = Math.exp(-angularGap / 0.115);
  const offset = neighbor.center.map((v, i) => v - nearest.center[i]);
  const tangent = unit(offset.map((v, i) => v - dot(offset, p) * p[i]));
  const a = cross(nearest.angularVelocity, p);
  const b = cross(neighbor.angularVelocity, p);
  const closing = dot(a.map((v, i) => v - b[i]), tangent);
  const convergent = Math.max(0, closing / 0.035);
  const divergent = Math.max(0, -closing / 0.035);
  // Interplate continent cores are spatially continuous; folded relief
  // roughens their shelves, coasts and interior drainage divides.
  let province = -1;
  for (const core of model.cratons) {
    const strength = (dot(p, core.center) - core.reach) / (1 - core.reach);
    province = Math.max(province, strength);
  }
  const crust = Math.max(
    0,
    Math.min(1, 0.48 + province * 1.9 + foldedNoise * 0.055),
  );
  const buoyancy = -2450 + 4050 * crust;
  const continentalPair = nearest.continental + neighbor.continental;
  const fold = 0.8 + 0.2 * Math.sin(foldedNoise * 1.7);
  const compression =
    boundary *
    Math.min(1.8, convergent) *
    (continentalPair ? 2800 + 1600 * crust : 1400) *
    fold;
  const rift =
    boundary * Math.min(1.5, divergent) * (crust > 0.3 ? -1250 : 900);
  const rolling = foldedNoise * (620 + crust * 700);
  return buoyancy + compression + rift + rolling;
}
