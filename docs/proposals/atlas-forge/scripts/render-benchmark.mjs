import { chromium } from "playwright";
import { build } from "esbuild";
import { generate } from "../src/generation/pipeline.js";
import { definition } from "../src/core/world.js";
const js = (
  await build({
    entryPoints: ["src/rendering/map.js"],
    bundle: true,
    write: false,
    format: "iife",
    globalName: "AtlasRender",
  })
).outputFiles[0].text;
const { world, derived } = generate(definition("RENDER-BENCHMARK"));
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
        ({ world, derived }) => {
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
          for (const projection of ["flat", "globe"]) {
            renderer.projection = projection;
            renderer.render();
            const samples = [];
            for (let i = 0; i < 12; i++) {
              const t = performance.now();
              renderer.rotation += 2;
              renderer.render();
              samples.push(performance.now() - t);
            }
            samples.sort((a, b) => a - b);
            out.projections[projection] = {
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
