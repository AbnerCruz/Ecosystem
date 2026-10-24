import { coord, index, clamp, area } from "../geometry/sphere.js";
export const ECOSYSTEM_MODEL = "resources-trophic-1";
export const N_PER_BIOMASS = 0.02;
const species = (id, name, role, diet, massKg, extra = {}) => ({
  id,
  name,
  role,
  diet,
  massKg,
  habitat: "land",
  minTemp: -10,
  maxTemp: 40,
  minRain: 200,
  productivity: 1,
  mortality: 0.08,
  consumption: 1,
  efficiency: 0.2,
  dispersalKm: 20,
  managed: false,
  ...extra,
});
export const PRESETS = [
  species("grass", "Gramíneas", "producer", [], 0.01, {
    minTemp: -15,
    productivity: 2,
    mortality: 0.15,
  }),
  species("trees", "Árvores", "producer", [], 1000, {
    minRain: 650,
    productivity: 1,
    mortality: 0.02,
    dispersalKm: 5,
  }),
  species("deer", "Cervos", "consumer", ["grass", "trees"], 100, {
    consumption: 3,
    dispersalKm: 50,
  }),
  species("cattle", "Bovinos domésticos", "consumer", ["grass"], 600, {
    managed: true,
    consumption: 3,
    minTemp: 0,
  }),
  species("lion", "Leões", "consumer", ["deer", "cattle"], 190, {
    minTemp: 10,
    minRain: 100,
    consumption: 2,
    dispersalKm: 80,
  }),
  species("algae", "Algas", "producer", [], 0.001, {
    habitat: "water",
    minRain: 0,
    minTemp: -2,
    productivity: 1.5,
  }),
  species("fish", "Peixes herbívoros", "consumer", ["algae"], 2, {
    habitat: "water",
    minRain: 0,
    minTemp: -2,
    consumption: 2,
    dispersalKm: 30,
  }),
  species("fungi", "Fungos decompositores", "decomposer", ["detritus"], 0.01, {
    minRain: 300,
    consumption: 2,
    dispersalKm: 5,
  }),
];
export function habitat(s, e, t, rain) {
  if (
    (s.habitat === "land") !== e > 0 ||
    t < s.minTemp ||
    t > s.maxTemp ||
    rain < s.minRain
  )
    return 0;
  return (
    clamp(Math.min((t - s.minTemp + 2) / 8, (s.maxTemp - t + 2) / 8), 0, 1) *
    (s.habitat === "water" ? 1 : clamp(rain / 800, 0.1, 1))
  );
}
export function environment(world, derived, grid = world.state.ecosystem.grid) {
  const n = grid.width * grid.height,
    out = { elevation: [], temperature: [], rain: [] };
  for (let i = 0; i < n; i++) {
    const p = coord(i, grid),
      j = index(p.lat, p.lon, world.definition.grid);
    out.elevation.push(world.state.elevation[j]);
    out.temperature.push(derived.temperature[j]);
    out.rain.push(derived.rain[j]);
  }
  return out;
}
export function seedEcosystem(world, derived) {
  const width = Math.min(36, world.definition.grid.width),
    grid = { width, height: width / 2 },
    n = (width * width) / 2;
  const env = environment(world, derived, grid);
  world.state.species = structuredClone(PRESETS);
  world.state.ecosystem = {
    model: ECOSYSTEM_MODEL,
    grid,
    year: 0,
    nutrients: Array(n).fill(0.08),
    detritus: Array(n).fill(0.1),
    biomass: {},
    lastEra: null,
  };
  for (const s of world.state.species)
    world.state.ecosystem.biomass[s.id] = Array.from({ length: n }, (_, i) =>
      s.managed
        ? 0
        : habitat(s, env.elevation[i], env.temperature[i], env.rain[i]) *
          (s.role === "producer"
            ? 0.5
            : s.role === "decomposer"
              ? 0.01
              : 0.002),
    );
}
export { vegetation } from "../geometry/ecology.js";
export function totals(world) {
  const e = world.state.ecosystem;
  if (!e) return null;
  const out = { biomassKg: 0, detritusKg: 0, nitrogenKg: 0, species: {} };
  for (const s of world.state.species) out.species[s.id] = 0;
  for (let i = 0; i < e.nutrients.length; i++) {
    const a = area(
      Math.floor(i / e.grid.width),
      e.grid,
      world.definition.planet.radius,
    );
    let b = 0;
    for (const s of world.state.species) {
      const m = e.biomass[s.id][i] * a;
      out.species[s.id] += m;
      b += m;
    }
    out.biomassKg += b;
    out.detritusKg += e.detritus[i] * a;
    out.nitrogenKg +=
      e.nutrients[i] * a + N_PER_BIOMASS * (b + e.detritus[i] * a);
  }
  return out;
}
