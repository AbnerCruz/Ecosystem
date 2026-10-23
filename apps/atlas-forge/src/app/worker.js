import { generate, derive } from "../generation/pipeline.js";
import { refine } from "../generation/refine.js";
self.onmessage = ({ data }) => {
  const { id, type, world, definition, overrides } = data;
  try {
    const progress = (stage, value) =>
      self.postMessage({ id, progress: { stage, value } });
    const result =
      type === "refine"
        ? refine(world, 1440, progress)
        : type === "generate"
          ? generate(definition, overrides, progress)
          : { world, derived: derive(world, progress) };
    self.postMessage({ id, result });
  } catch (error) {
    self.postMessage({ id, error: error.message });
  }
};
