import { generateSurfaceTile } from "./surface-field.js";
let context,
  epoch = 0,
  queue = [],
  running = false;
self.onmessage = ({ data }) => {
  if (data.type === "world") {
    context = data.context;
    epoch = data.epoch;
    queue = [];
  }
  if (data.type === "tiles" && data.epoch === epoch) {
    queue = data.tiles;
    void pump();
  }
};
async function pump() {
  if (running) return;
  running = true;
  while (queue.length && context) {
    const tile = queue.shift(),
      version = epoch;
    try {
      const result = generateSurfaceTile(context, ...tile);
      if (version === epoch)
        self.postMessage({ epoch, ...result }, [
          result.rgba.buffer,
          result.topo.buffer,
          result.material.buffer,
        ]);
    } catch (e) {
      self.postMessage({ epoch, error: e.message });
      queue = [];
    }
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  running = false;
}
