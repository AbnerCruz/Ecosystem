import { chromium } from "playwright";
import { build } from "esbuild";
import { generate } from "../src/generation/pipeline.js";
import { definition } from "../src/core/world.js";
const terrainWorker = (
  await build({
    entryPoints: ["src/rendering/surface-worker.js"],
    bundle: true,
    write: false,
    format: "iife",
  })
).outputFiles[0].text;
const js = (
  await build({
    stdin: {
      contents:
        'export { MapRenderer } from "./src/rendering/map.js"; export { generate } from "./src/generation/pipeline.js"; export { definition } from "./src/core/world.js";',
      resolveDir: process.cwd(),
    },
    bundle: true,
    write: false,
    format: "iife",
    globalName: "AtlasRender",
    define: { __TERRAIN_WORKER_SOURCE__: JSON.stringify(terrainWorker) },
  })
).outputFiles[0].text;
const { world, derived } = generate(definition("RENDER-BENCHMARK", {}, 720));
const plain = Object.fromEntries(
  Object.entries(derived).map(([key, value]) => [
    key,
    ArrayBuffer.isView(value) ? Array.from(value) : value,
  ]),
);
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox"],
});
try {
  const page = await browser.newPage({
    viewport: { width: 1200, height: 800 },
  });
  await page.setContent(
    '<canvas id="map" style="width:900px;height:650px"></canvas>',
  );
  await page.addScriptTag({ content: js });
  console.log(
    JSON.stringify(
      await page.evaluate(
        async ({ world, derived }) => {
          const renderer = new AtlasRender.MapRenderer(
            document.getElementById("map"),
          );
          renderer.setWorld(world, derived);
          const out = {
            viewport: "900×650 CSS px",
            browser: navigator.userAgent,
            unit: "ms per synchronous Canvas render; excludes presentation to screen",
            projections: {},
          };
          for (const projection of ["flat", "globe"])
            for (const zoom of [1, 8, 64]) {
              renderer.projection = projection;
              renderer.zoom = zoom;
              renderer.pan = { x: 0, y: 0 };
              const cold = performance.now();
              renderer.render();
              const coldMs = performance.now() - cold;
              const waitStart = performance.now();
              while (renderer.canvas.dataset.terrainReady !== "true") {
                if (performance.now() - waitStart > 60000)
                  throw new Error("Terrain readiness timeout");
                await new Promise((resolve) => setTimeout(resolve, 25));
              }
              const readyMs = performance.now() - cold;
              const samples = [];
              for (let i = 0; i < 12; i++) {
                const t = performance.now();
                if (projection === "globe") renderer.rotation += 0.1;
                else renderer.pan.x += 2;
                renderer.render();
                samples.push(performance.now() - t);
              }
              samples.sort((a, b) => a - b);
              out.projections[`${projection}@${zoom}x`] = {
                coldMs,
                readyMs,
                cachedTiles: renderer.surface.cache.size,
                medianMs: samples[6],
                p95Ms: samples[11],
              };
            }
          return out;
        },
        { world, derived: plain },
      ),
      null,
      2,
    ),
  );
} finally {
  await browser.close();
}
