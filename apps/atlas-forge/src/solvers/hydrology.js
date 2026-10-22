import { area, coord, distance, neighbors } from "../geometry/sphere.js";
/** B: acyclic steepest descent on spherical edges; closed depressions retained. */
export function hydrology(elevation, rain, grid, planet) {
  const n = elevation.length,
    downstream = new Int32Array(n).fill(-1),
    basin = new Int32Array(n).fill(-1),
    discharge = new Float64Array(n),
    terminal = new Uint8Array(n);
  const points = Array.from({ length: n }, (_, i) => coord(i, grid));
  for (let i = 0; i < n; i++) {
    if (elevation[i] <= 0) {
      terminal[i] = 1;
      continue;
    }
    let best = 0;
    for (const j of neighbors(i, grid)) {
      const slope =
        (elevation[i] - elevation[j]) /
        Math.max(1, distance(points[i], points[j], planet.radius));
      if (slope > best) {
        best = slope;
        downstream[i] = j;
      }
    }
    if (downstream[i] < 0) terminal[i] = 2;
    discharge[i] =
      ((rain[i] / 1000) *
        0.35 *
        area(Math.floor(i / grid.width), grid, planet.radius)) /
      (365.25 * 86400);
  }
  const order = Array.from({ length: n }, (_, i) => i).sort(
    (a, b) => elevation[b] - elevation[a] || a - b,
  );
  for (const i of order)
    if (downstream[i] >= 0) discharge[downstream[i]] += discharge[i];
  for (let k = order.length - 1; k >= 0; k--) {
    const i = order[k];
    basin[i] = downstream[i] >= 0 ? basin[downstream[i]] : i;
  }
  return { downstream, basin, discharge, terminal };
}
