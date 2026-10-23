import { build } from "esbuild";
import { chromium } from "playwright";
import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { generate } from "../src/generation/pipeline.js";
import { definition } from "../src/core/world.js";
await mkdir("test-results", { recursive: true });
const worker = (
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
        'export { MapRenderer } from "./src/rendering/map.js"; export { bindPinch } from "./src/rendering/gestures.js";',
      resolveDir: process.cwd(),
    },
    bundle: true,
    write: false,
    format: "iife",
    globalName: "AtlasZoom",
    define: { __TERRAIN_WORKER_SOURCE__: JSON.stringify(worker) },
  })
).outputFiles[0].text;
const fixture = generate(definition("AURORA-42", {}, 180));
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox"],
});
const errors = [];
try {
  const context = await browser.newContext({
    viewport: { width: 1000, height: 760 },
    offline: true,
    recordVideo: {
      dir: "test-results/zoom-video",
      size: { width: 1000, height: 760 },
    },
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => errors.push(e.message));
  await page.setContent(
    '<body style="margin:0;background:#10212a;color:#e7d4ac;font:16px system-ui"><p style="margin:12px">Atlas Forge · mesmo terreno durante todo o zoom</p><canvas id="map" style="width:1000px;height:700px;touch-action:none"></canvas>',
  );
  await page.addScriptTag({ content: js });
  await page.evaluate(({ world, derived }) => {
    const r = (window.renderer = new AtlasZoom.MapRenderer(
      document.getElementById("map"),
    ));
    r.setWorld(world, derived);
    r.showGrid = false;
    r.showRivers = false;
    r.render();
    r.canvas.addEventListener(
      "wheel",
      (e) => {
        e.preventDefault();
        const b = r.canvas.getBoundingClientRect();
        r.zoomAt(
          Math.exp(-e.deltaY * 0.003),
          e.clientX - b.left,
          e.clientY - b.top,
        );
        r.render();
      },
      { passive: false },
    );
    AtlasZoom.bindPinch(r.canvas, r);
    window.sample = () => {
      const c = r.canvas.getContext("2d"),
        d = c.getImageData(400, 250, 200, 200).data;
      let hash = 2166136261,
        empty = 0;
      const mean = [0, 0, 0];
      for (let i = 0; i < d.length; i++)
        hash = Math.imul(hash ^ d[i], 16777619) >>> 0;
      for (let i = 0; i < d.length; i += 4) {
        for (let j = 0; j < 3; j++) mean[j] += d[i + j] / 40000;
        if (d[i + 3] !== 255) empty++;
      }
      return {
        hash,
        empty,
        mean,
        p: r.geographic(500, 350),
        height: r.detailedLocation(r.geographic(500, 350)).elevation,
        mpp: r.metresPerPixel(),
        level: r.canvas.dataset.terrainLevel,
        pipeline: r.canvas.dataset.terrainPipeline,
        tiles: r.surface.cache.size,
      };
    };
  }, fixture);
  const ready = () =>
    page.waitForFunction(
      () => renderer.canvas.dataset.terrainReady === "true",
      null,
      { timeout: 60000 },
    );
  await ready();
  await page.screenshot({ path: "test-results/unified-world.png" });
  await page.evaluate(() => {
    renderer.focus(63, -63);
    renderer.render();
  });
  await ready();
  const box = await page.locator("#map").boundingBox();
  await page.mouse.move(box.x + 500, box.y + 350);
  const initial = await page.evaluate(() => sample()),
    frames = [];
  const delta = -Math.log(1.7) / 0.003;
  let steps = 0;
  while (
    (await page.evaluate(() => renderer.metresPerPixel())) > 0.04 &&
    steps < 40
  ) {
    await page.mouse.wheel(0, delta);
    await page.waitForTimeout(20);
    const during = await page.evaluate(() => sample());
    assert.equal(
      during.empty,
      0,
      "Loading must retain the parent terrain, never blank patches",
    );
    await ready();
    const state = await page.evaluate(() => sample());
    assert.equal(state.pipeline, "continuous-pyramid");
    assert.ok(
      Math.abs(state.p.lat - initial.p.lat) < 1e-7 &&
        Math.abs(state.p.lon - initial.p.lon) < 1e-7,
      "Wheel anchor drift",
    );
    assert.ok(
      Math.abs(state.height - initial.height) < 0.001,
      "Zoom changed terrain elevation",
    );
    assert.ok(state.tiles <= 96);
    frames.push(state);
    steps++;
    if ([5, 10, 15, 20, 25].includes(steps))
      await page.screenshot({ path: `test-results/unified-zoom-${steps}.png` });
  }
  assert.ok(frames.at(-1).mpp < 0.04);
  await page.screenshot({ path: "test-results/unified-ground.png" });
  for (let i = 0; i < steps; i++) {
    await page.mouse.wheel(0, -delta);
    await page.waitForTimeout(20);
    await ready();
  }
  const returned = await page.evaluate(() => sample());
  assert.equal(
    returned.hash,
    initial.hash,
    "Zoom out must recover the original pixels",
  );
  const threshold = [];
  for (const mpp of [1501, 1499, 1501]) {
    await page.evaluate((m) => renderer.setMetresPerPixel(m), mpp);
    await ready();
    threshold.push(await page.evaluate(() => sample()));
  }
  const colorDelta =
    threshold[0].mean.reduce(
      (s, v, i) => s + Math.abs(v - threshold[1].mean[i]),
      0,
    ) / 3;
  assert.ok(
    colorDelta < 2,
    `Palette/scene jump at old boundary: ${colorDelta}`,
  );
  assert.equal(threshold[0].hash, threshold[2].hash);
  await page.evaluate(() => {
    renderer.projection = "globe";
    renderer.zoom = 1;
    renderer.pan = { x: 0, y: 0 };
    renderer.rotation = 0;
    renderer.render();
  });
  await ready();
  const globe = await page.evaluate(() => {
    const r = renderer,
      d = r.canvas.getContext("2d").getImageData(0, 0, 1000, 700).data;
    let empty = 0,
      total = 0;
    for (let y = 110; y < 650; y += 7)
      for (let x = 100; x < 900; x += 7) {
        const p = r.geographic(x + 0.5, y + 0.5);
        if (p) {
          total++;
          if (d[(y * 1000 + x) * 4 + 3] !== 255) empty++;
        }
      }
    return { total, empty };
  });
  assert.equal(globe.empty, 0, JSON.stringify(globe));
  await page.screenshot({ path: "test-results/unified-globe.png" });
  for (const p of [
    { lat: 30, lon: 179.999 },
    { lat: 80, lon: -90 },
  ]) {
    await page.evaluate((p) => {
      renderer.focus(p.lat, p.lon);
      renderer.setMetresPerPixel(500);
    }, p);
    await ready();
    assert.equal((await page.evaluate(() => sample())).empty, 0);
  }
  assert.deepEqual(errors, []);
  await writeFile(
    "test-results/zoom-continuity-report.json",
    JSON.stringify(
      {
        initial,
        returned,
        steps,
        frames,
        threshold,
        colorDelta,
        globe,
        errors,
      },
      null,
      2,
    ),
  );
  const video = page.video();
  await context.close();
  await video.saveAs("test-results/zoom-continuity.webm");
  console.log(
    `PASS Continuous wheel zoom: ${steps} levels in/out, stable camera/height/pixels, no loading holes, old-boundary ΔRGB=${colorDelta.toFixed(3)}, globe limb and date line`,
  );
} finally {
  await browser.close();
}
