import { area, clamp, coord, distance } from "../geometry/sphere.js";
export const ALGORITHM = "spherical-tectonic-2";
export const DEFAULTS = {
  radius: 6371000,
  ocean: 68,
  tilt: 23.5,
  baseTemp: 15,
};
export function definition(seed = "AURORA-42", params = {}, width = 180) {
  return {
    seed,
    algorithm: ALGORITHM,
    planet: { ...DEFAULTS, ...params },
    grid: {
      width,
      height: width / 2,
      topology: "latlon-polar-reflection",
      datum: "sphere-sea-level",
      elevationUnit: "m",
    },
  };
}
export function createWorld(def, elevation) {
  return {
    format: "atlasforge.world",
    version: 3,
    definition: def,
    state: {
      name: "Aurora",
      elevation: Array.from(elevation),
      entities: [],
      overrides: {},
      species: [],
      ecosystem: null,
    },
    journal: { undo: [], redo: [] },
    timeline: { active: null, checkpoints: [] },
  };
}
export function stats(world, derived) {
  const { grid, planet } = world.definition;
  let total = 0,
    ocean = 0,
    rain = 0,
    temp = 0,
    max = -Infinity;
  world.state.elevation.forEach((e, i) => {
    const a = area(Math.floor(i / grid.width), grid, planet.radius);
    total += a;
    if (e <= 0) ocean += a;
    rain += (derived?.rain[i] || 0) * a;
    temp += (derived?.temperature[i] || 0) * a;
    max = Math.max(max, e);
  });
  return {
    ocean: (ocean / total) * 100,
    rain: rain / total,
    temperature: temp / total,
    max,
  };
}
export function brush(
  world,
  lat,
  lon,
  radiusDegrees,
  delta,
  mode = "relative",
) {
  if (
    !["relative", "land", "sea"].includes(mode) ||
    !Number.isFinite(delta) ||
    !(radiusDegrees > 0)
  )
    throw new Error("Pincel inválido.");
  const { grid, planet } = world.definition,
    changes = [],
    r = ((radiusDegrees * Math.PI) / 180) * planet.radius;
  for (let i = 0; i < grid.width * grid.height; i++) {
    const d = distance({ lat, lon }, coord(i, grid), planet.radius);
    if (d > r) continue;
    const before = world.state.elevation[i],
      after =
        mode === "land"
          ? Math.max(before, clamp(Math.abs(delta), 1, 12000))
          : mode === "sea"
            ? Math.min(before, -clamp(Math.abs(delta), 1, 12000))
            : clamp(before + delta * (1 - d / r), -12000, 12000);
    if (after === before && world.state.overrides[i] === after) continue;
    changes.push({ i, before, after, prior: world.state.overrides[i] ?? null });
    world.state.elevation[i] = after;
    world.state.overrides[i] = after;
  }
  return changes;
}
export function record(world, changes) {
  if (!changes.length) return;
  world.journal.undo.push({ kind: "elevation", changes });
  world.journal.undo = world.journal.undo.slice(-30);
  world.journal.redo = [];
}
export function history(world, direction) {
  const source = direction === "undo" ? world.journal.undo : world.journal.redo;
  const command = source.pop();
  if (!command) return false;
  for (const c of command.changes) {
    world.state.elevation[c.i] = direction === "undo" ? c.before : c.after;
    if (direction === "undo" && c.prior === null)
      delete world.state.overrides[c.i];
    else world.state.overrides[c.i] = direction === "undo" ? c.prior : c.after;
  }
  world.journal[direction === "undo" ? "redo" : "undo"].push(command);
  return true;
}
export function checkpoint(world, label) {
  if (world.timeline.checkpoints.length >= 12)
    throw new Error(
      "Limite de 12 marcos. Exporte o projeto antes de iniciar outro histórico.",
    );
  const item = {
    id: crypto.randomUUID(),
    parent: world.timeline.active,
    label,
    definition: structuredClone(world.definition),
    state: structuredClone(world.state),
    journal: structuredClone(world.journal),
  };
  world.timeline.checkpoints.push(item);
  world.timeline.active = item.id;
  return item;
}
export function restore(world, id) {
  const c = world.timeline.checkpoints.find((x) => x.id === id);
  if (!c) throw new Error("Marco não encontrado.");
  world.definition = structuredClone(c.definition);
  world.state = structuredClone(c.state);
  world.journal = structuredClone(c.journal);
  world.timeline.active = id;
}
