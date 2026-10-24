import { clamp } from "./sphere.js";
export function vegetation(world, cell) {
  const e = world.state.ecosystem;
  if (!e) return 0;
  return clamp(
    world.state.species
      .filter((s) => s.role === "producer" && s.habitat === "land")
      .reduce((sum, s) => sum + e.biomass[s.id][cell], 0) / 2,
    0,
    1,
  );
}
