import { clamp, coord, RAD } from "../geometry/sphere.js";
/** B: daily mean insolation (W/m²) on circular orbit, ideal clear sky. */
export function insolation(lat, tilt, season) {
  const p = lat * RAD,
    d = tilt * RAD * Math.sin(season * Math.PI * 2);
  const h = Math.acos(clamp(-Math.tan(p) * Math.tan(d), -1, 1));
  return (
    (1361 / Math.PI) *
    (h * Math.sin(p) * Math.sin(d) + Math.cos(p) * Math.cos(d) * Math.sin(h))
  );
}
/** A: climatology with lapse rate and moisture sweeps. Not atmospheric CFD. */
export function climate(elevation, grid, planet) {
  const n = elevation.length,
    temperature = new Float32Array(n),
    summer = new Float32Array(n),
    winter = new Float32Array(n),
    rain = new Float32Array(n),
    wind = new Int8Array(n);
  for (let y = 0; y < grid.height; y++) {
    const lat = coord(y * grid.width, grid).lat,
      direction = Math.abs(lat) > 30 && Math.abs(lat) < 60 ? 1 : -1;
    for (let x = 0; x < grid.width; x++) {
      const i = y * grid.width + x,
        lapse = Math.max(0, elevation[i]) * 0.0065;
      temperature[i] =
        planet.baseTemp + 24 * (Math.cos(lat * RAD) - 0.65) - lapse;
      const amplitude =
        planet.tilt *
        0.45 *
        Math.sin(lat * RAD) *
        (elevation[i] <= 0 ? 0.35 : 1);
      summer[i] = temperature[i] + amplitude;
      winter[i] = temperature[i] - amplitude;
      wind[i] = direction;
    }
    let moisture = 0.5;
    // Repeated circumnavigations reduce dependence on the arbitrary longitude seam.
    for (let pass = 0; pass < 5; pass++)
      for (let s = 0; s < grid.width; s++) {
        const x = direction > 0 ? s : grid.width - 1 - s,
          i = y * grid.width + x;
        const prev =
          y * grid.width + ((x - direction + grid.width) % grid.width);
        if (elevation[i] <= 0) moisture += (1 - moisture) * 0.45;
        const lift = Math.max(0, elevation[i] - Math.max(0, elevation[prev]));
        const fraction = clamp(0.025 + lift / 3200, 0.025, 0.85),
          release = moisture * fraction;
        moisture -= release;
        const latitudeFactor = 0.45 + 0.55 * Math.cos(lat * RAD) ** 2;
        if (pass === 4) rain[i] = 70 + release * 20000 * latitudeFactor;
      }
  }
  return { temperature, summer, winter, rain, wind };
}
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
export function biomes(elevation, temperature, rain) {
  return Uint8Array.from(elevation, (h, i) =>
    h <= 0
      ? 0
      : temperature[i] < -8
        ? 1
        : temperature[i] < 0
          ? 2
          : h > 3300
            ? 9
            : rain[i] < 170
              ? 3
              : rain[i] < 400
                ? 4
                : temperature[i] < 6
                  ? 8
                  : rain[i] < 850
                    ? 5
                    : temperature[i] > 22 && rain[i] > 1200
                      ? 7
                      : 6,
  );
}
