import { clamp, coord, RAD } from "../geometry/sphere.js";
export { insolation } from "./radiation.js";
export const BIOMES = [
  "Oceano",
  "Gelo",
  "Tundra",
  "Deserto",
  "Estepe",
  "Pradaria",
  "Floresta temperada",
  "Floresta tropical",
  "Taiga",
  "Alpino",
];

/**
 * Resolution-aware annual moisture advection over the sphere.
 * Moisture flux q is expressed in mm·m/year per metre of wind-front;
 * precipitation in a cell is consumed flux divided by windward cell width.
 * This is a stationary, single-layer model, NOT fluid dynamics.
 */
export function climate(elevation, grid, planet) {
  const n = elevation.length,
    temperature = new Float32Array(n),
    summer = new Float32Array(n),
    winter = new Float32Array(n),
    rain = new Float32Array(n),
    wind = new Int8Array(n);
  for (let y = 0; y < grid.height; y++) {
    const lat = coord(y * grid.width, grid).lat;
    const absolute = Math.abs(lat);
    const direction = absolute > 30 && absolute < 60 ? 1 : -1;
    const belt = clamp(
      0.45 +
        0.9 * Math.exp(-Math.pow(absolute / 18, 2)) +
        0.65 * Math.exp(-Math.pow((absolute - 55) / 17, 2)) -
        0.18 * Math.exp(-Math.pow((absolute - 30) / 12, 2)),
      0.3,
      1.85,
    );
    const length = Math.max(
      100,
      (2 * Math.PI * planet.radius * Math.max(0.006, Math.cos(lat * RAD))) /
        grid.width,
    );
    const sourceFlux = (1300 + 450 * belt) * 2100000;
    for (let x = 0; x < grid.width; x++) {
      const i = y * grid.width + x,
        alt = Math.max(0, elevation[i]);
      const marine = elevation[i] <= 0;
      const average =
        planet.baseTemp + 33 * (Math.cos(lat * RAD) ** 2 - 0.65) -
        alt * 0.0065;
      const amplitude =
        0.45 *
        planet.tilt *
        Math.sin(lat * RAD) *
        (marine ? 0.35 : 1.05);
      temperature[i] = average;
      summer[i] = average + amplitude;
      winter[i] = average - amplitude;
      wind[i] = direction;
    }
    let q = sourceFlux * 0.65;
    const marineRecovery = 1 - Math.exp(-length / 420000);
    const backgroundRelease = 1 - Math.exp(-length / 3700000);
    // 6 complete circumnavigations make the periodic seam independent of
    // a single initial moisture value without adding a climate-sized solver.
    for (let pass = 0; pass < 6; pass++) {
      for (let s = 0; s < grid.width; s++) {
        const x = direction > 0 ? s : grid.width - 1 - s;
        const i = y * grid.width + x,
          previous =
            y * grid.width +
            ((x - direction + grid.width) % grid.width);
        const sea = elevation[i] <= 0;
        if (sea) q += (sourceFlux - q) * marineRecovery;
        const uphill = Math.max(
          0,
          Math.max(0, elevation[i]) - Math.max(0, elevation[previous]),
        );
        const orographic = 1 - Math.exp(-uphill / 1250);
        const fraction = clamp(
          backgroundRelease + orographic * (1 - backgroundRelease) * 0.83,
          0,
          0.91,
        );
        const release = q * fraction;
        q -= release;
        if (pass === 5)
          rain[i] = clamp(55 + (release / length) * belt, 0, 9000);
      }
    }
  }
  return { temperature, summer, winter, rain, wind };
}

/** Coarse biome classification, informed by temperature and water demand. */
export function biomes(elevation, temperature, rain) {
  return Uint8Array.from(elevation, (h, i) => {
    if (h <= 0) return 0;
    const t = temperature[i];
    const p = rain[i];
    if (t < -13) return 1;
    if (t < -4) return 2;
    if (h > 3500 && t < 13) return 9;
    const demand = 230 + Math.max(-5, t) * 30;
    if (p < 0.42 * demand) return 3;
    if (p < 0.83 * demand) return 4;
    if (t < 5) return p > 700 ? 8 : 2;
    if (p < 1.25 * demand) return 5;
    if (t >= 22 && p > 1250) return 7;
    return 6;
  });
}
