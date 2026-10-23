import { checkpoint } from "../core/world.js";
import { coord, index } from "../geometry/sphere.js";
import { generate } from "./pipeline.js";
/** Explicit higher-resolution regeneration. Authored source-cell footprints retain absolute heights. */
export function refine(source, width, progress = () => {}) {
  const oldGrid = source.definition.grid;
  if (
    !Number.isInteger(width) ||
    width <= oldGrid.width ||
    width > 1440 ||
    width % 2
  )
    throw new Error("Escolha uma resolução maior, até 1.440 × 720.");
  const draft = structuredClone(source);
  checkpoint(
    draft,
    `Antes do refinamento · ${oldGrid.width} × ${oldGrid.height}`,
  );
  const def = structuredClone(source.definition);
  def.grid.width = width;
  def.grid.height = width / 2;
  const overrides = {};
  for (let i = 0; i < (width * width) / 2; i++) {
    const p = coord(i, def.grid),
      parent = index(p.lat, p.lon, oldGrid);
    if (source.state.overrides[parent] !== undefined)
      overrides[i] = source.state.overrides[parent];
  }
  const result = generate(def, overrides, progress);
  result.world.state = {
    ...structuredClone(source.state),
    elevation: result.world.state.elevation,
    overrides,
  };
  result.world.timeline = draft.timeline;
  return result;
}
