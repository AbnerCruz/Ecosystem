import { checkpoint } from "../core/world.js";
import {
  environment,
  seedEcosystem,
  vegetation,
  totals,
} from "../core/ecosystem.js";
import { ecologyYear } from "./ecosystem.js";
import { derive } from "../generation/pipeline.js";
import { coord, index, area, distance } from "../geometry/sphere.js";
/** Finite-volume transport: eroded volume is deposited downstream. Authored nodes are fixed. */
export function erosion(world, derived, years) {
  const { grid, planet } = world.definition,
    e = world.state.elevation,
    delta = new Float64Array(e.length);
  let moved = 0;
  for (let i = 0; i < e.length; i++) {
    const j = derived.downstream[i];
    if (
      j < 0 ||
      e[i] <= 0 ||
      e[j] >= e[i] ||
      world.state.overrides[i] !== undefined ||
      world.state.overrides[j] !== undefined
    )
      continue;
    const p = coord(i, grid),
      q = coord(j, grid),
      slope = (e[i] - e[j]) / Math.max(1, distance(p, q, planet.radius));
    const cover = world.state.ecosystem
      ? vegetation(world, index(p.lat, p.lon, world.state.ecosystem.grid))
      : 0;
    const depth = Math.min(
      (e[i] - e[j]) * 0.02,
      years * 0.002 * (derived.rain[i] / 1000) * slope * (1 - 0.85 * cover),
    );
    const sourceArea = area(Math.floor(i / grid.width), grid, planet.radius),
      targetArea = area(Math.floor(j / grid.width), grid, planet.radius);
    const volume = Math.min(
      depth * sourceArea,
      (Math.max(0, 12000 - e[j]) * targetArea) / 8,
    );
    delta[i] -= volume / sourceArea;
    delta[j] += volume / targetArea;
    moved += volume;
  }
  for (let i = 0; i < e.length; i++) e[i] += delta[i];
  return moved;
}
export function advanceEra(input, years, progress = () => {}) {
  if (!Number.isInteger(years) || years < 1 || years > 10000)
    throw new Error("Escolha uma duração inteira entre 1 e 10.000 anos.");
  const world = structuredClone(input);
  let derived = derive(world);
  if (!world.state.ecosystem) seedEcosystem(world, derived);
  const before = totals(world),
    start = world.state.ecosystem.year;
  checkpoint(world, `Antes da era · ano ${start}`);
  // The prior edit journal is retained in the checkpoint; its indices precede simulation.
  world.journal = { undo: [], redo: [] };
  let env = environment(world, derived),
    productionKg = 0,
    respirationKg = 0,
    sedimentM3 = 0;
  for (let year = 1; year <= years; year++) {
    const flux = ecologyYear(world, env);
    productionKg += flux.productionKg;
    respirationKg += flux.respirationKg;
    if (year % 100 === 0 || year === years) {
      const dt = year % 100 || 100;
      sedimentM3 += erosion(world, derived, dt);
      derived = derive(world);
      env = environment(world, derived);
      progress(`Era: ${year} / ${years} anos`, year / years);
    }
  }
  const after = totals(world);
  world.state.ecosystem.lastEra = {
    start,
    end: start + years,
    productionKg,
    respirationKg,
    sedimentM3,
    nitrogenErrorKg: after.nitrogenKg - before.nitrogenKg,
    organicErrorKg:
      after.biomassKg +
      after.detritusKg -
      before.biomassKg -
      before.detritusKg -
      productionKg +
      respirationKg,
  };
  return { world, derived };
}
