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
  return { version: PLATE_MODEL, plates };
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
  const angularGap =
    Math.acos(Math.max(-1, Math.min(1, second))) -
    Math.acos(Math.max(-1, Math.min(1, first)));
  const boundary = Math.exp(-angularGap / 0.115);
  const offset = neighbor.center.map((v, i) => v - nearest.center[i]);
  const tangent = unit(offset.map((v, i) => v - dot(offset, p) * p[i]));
  const a = cross(nearest.angularVelocity, p);
  const b = cross(neighbor.angularVelocity, p);
  const closing = dot(a.map((v, i) => v - b[i]), tangent);
  const convergent = Math.max(0, closing / 0.035);
  const divergent = Math.max(0, -closing / 0.035);
  // Blend crust types across a finite plate-boundary belt, no Voronoi cliffs.
  const buoyancy =
    (nearest.continental * 2450 - 1550) * (1 - boundary * 0.5) +
    (neighbor.continental * 2450 - 1550) * boundary * 0.5;
  const continentalPair = nearest.continental + neighbor.continental;
  const fold = 0.8 + 0.2 * Math.sin(foldedNoise * 1.7);
  const compression =
    boundary *
    Math.min(1.8, convergent) *
    (continentalPair ? 3600 : 1900) *
    fold;
  const rift =
    boundary * Math.min(1.5, divergent) * (nearest.continental ? -1200 : 850);
  const rolling = foldedNoise * (nearest.continental ? 1150 : 540);
  return buoyancy + compression + rift + rolling;
}
