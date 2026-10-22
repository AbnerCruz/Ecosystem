import { performance } from "node:perf_hooks";
import { generate } from "../src/generation/pipeline.js";
import { definition } from "../src/core/world.js";
import { serialize } from "../src/storage/project.js";
import { cpus } from "node:os";
console.log(
  JSON.stringify({
    node: process.version,
    cpu: cpus()[0].model,
    platform: process.platform,
    description: "Wall time in this cloud container; no mobile/DEVICE claim.",
  }),
);
for (const width of [90, 180, 360]) {
  let result;
  const times = [];
  for (let i = 0; i < 4; i++) {
    const t = performance.now();
    result = generate(definition("BENCHMARK", {}, width));
    times.push(performance.now() - t);
  }
  const t = performance.now(),
    serialized = serialize(result.world);
  console.log(
    JSON.stringify({
      width,
      height: width / 2,
      cells: width ** 2 / 2,
      warmMedianMs: times.slice(1).sort((a, b) => a - b)[1],
      coldMs: times[0],
      microsecondsPerCell:
        (times.slice(1).sort((a, b) => a - b)[1] * 1000) / (width ** 2 / 2),
      serializeMs: performance.now() - t,
      projectKiB: Buffer.byteLength(serialized) / 1024,
      heapMiB: process.memoryUsage().heapUsed / 1024 ** 2,
    }),
  );
}
