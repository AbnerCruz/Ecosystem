import { clamp, coord, RAD } from "../geometry/sphere.js";
export const SOLAR_CONSTANT = 1361;
export const RADIATION_MODEL = "daily-mean-circular-1";
/** Daily mean incoming solar flux at top of atmosphere, W/m²; circular orbit. */
export function insolation(lat, tilt, season, solarConstant = SOLAR_CONSTANT) {
  if (
    !Number.isFinite(lat) ||
    Math.abs(lat) > 90 ||
    !Number.isFinite(tilt) ||
    tilt < 0 ||
    tilt > 90 ||
    !Number.isFinite(season) ||
    !Number.isFinite(solarConstant) ||
    solarConstant < 0
  )
    throw new Error("Parâmetros de insolação inválidos.");
  const p = lat * RAD,
    d = tilt * RAD * Math.sin(season * Math.PI * 2);
  const h = Math.acos(clamp(-Math.tan(p) * Math.tan(d), -1, 1));
  return Math.max(
    0,
    (solarConstant / Math.PI) *
      (h * Math.sin(p) * Math.sin(d) + Math.cos(p) * Math.cos(d) * Math.sin(h)),
  );
}
/** Latitude-only fields: one sample per grid ROW, not per cell or Canvas pixel. */
export function radiation(grid, planet) {
  const solarAnnual = new Float32Array(grid.height),
    solarJune = new Float32Array(grid.height),
    solarDecember = new Float32Array(grid.height);
  const samples = 96;
  for (let y = 0; y < grid.height; y++) {
    const lat = coord(y * grid.width, grid).lat;
    let annual = 0;
    for (let k = 0; k < samples; k++)
      annual += insolation(lat, planet.tilt, (k + 0.5) / samples);
    solarAnnual[y] = annual / samples;
    solarJune[y] = insolation(lat, planet.tilt, 0.25);
    solarDecember[y] = insolation(lat, planet.tilt, 0.75);
  }
  return {
    solarAnnual,
    solarJune,
    solarDecember,
    solarModel: {
      id: RADIATION_MODEL,
      solarConstant: SOLAR_CONSTANT,
      unit: "W/m²",
      location: "top-of-atmosphere",
      sampling: "latitude-row",
      annualSamples: samples,
    },
  };
}
