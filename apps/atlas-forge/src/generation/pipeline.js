import { seedEcosystem } from "../core/ecosystem.js";
import { coord, vector, area, clamp } from "../geometry/sphere.js";
import { noise } from "./random.js";
import { plateModel, plateElevation } from "./geodynamics.js";
import { createWorld } from "../core/world.js";
import { climate, biomes } from "../solvers/climate.js";
import { radiation } from "../solvers/radiation.js";
import { hydrology } from "../solvers/hydrology.js";
export function derive(world, progress = () => {}) {
  const { grid, planet } = world.definition,
    e = world.state.elevation;
  progress("Insolação sazonal", 0.45);
  const solar = radiation(grid, planet);
  progress("Clima e umidade", 0.55);
  const c = climate(e, grid, planet);
  progress("Drenagem e bacias", 0.8);
  const h = hydrology(e, c.rain, grid, planet);
  return {
    ...c,
    ...h,
    ...solar,
    biome: biomes(e, c.temperature, c.rain),
    source: "state.elevation",
    algorithm: world.definition.algorithm,
  };
}
export function generate(def, overrides = {}, progress = () => {}) {
  const { grid, planet } = def,
    n = grid.width * grid.height,
    f = noise(def.seed),
    raw = new Float64Array(n),
    plates = plateModel(def.seed);
  progress("Superfície esférica", 0.1);
  for (let i = 0; i < n; i++) {
    const p = coord(i, grid);
    const v = vector(p.lat, p.lon);
    const folded = f(v);
    raw[i] = plateElevation(plates, v, folded);
  }
  const sorted = Array.from(raw, (value, i) => ({
    value,
    weight: area(Math.floor(i / grid.width), grid, 1),
  })).sort((a, b) => a.value - b.value);
  let acc = 0,
    sea = 0;
  for (const s of sorted) {
    acc += s.weight;
    if (acc >= (4 * Math.PI * planet.ocean) / 100) {
      sea = s.value;
      break;
    }
  }
  const elevation = Array.from(
    raw,
    (v, i) => overrides[i] ?? clamp(v - sea, -12000, 12000),
  );
  const world = createWorld(def, elevation);
  world.state.overrides = { ...overrides };
  const derived = derive(world, progress);
  seedEcosystem(world, derived);
  return { world, derived };
}

/** Authored ocean canvas: background constraints prevent seeded regeneration from inserting continents. */
export function oceanCanvas(def, progress = () => {}) {
  const world = createWorld(
    def,
    Array(def.grid.width * def.grid.height).fill(-800),
  );
  world.state.overrides = Object.fromEntries(
    world.state.elevation.map((h, i) => [i, h]),
  );
  const derived = derive(world, progress);
  seedEcosystem(world, derived);
  return { world, derived };
}
