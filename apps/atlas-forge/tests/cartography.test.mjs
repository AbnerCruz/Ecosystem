import test from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "playwright";
import { definition } from "../src/core/world.js";
import { generate } from "../src/generation/pipeline.js";
import { parseProject, serialize } from "../src/storage/project.js";

test("High-resolution projects hold 64× the former samples and roundtrip without invented entities", () => {
  const { world } = generate(definition("NATIVE-DETAIL", {}, 1440));
  assert.equal(world.state.elevation.length, 1036800);
  assert.deepEqual(world.state.entities, []);
  const source = serialize(world);
  const restored = parseProject(source);
  assert.deepEqual(restored, world);
  assert.throws(
    () =>
      parseProject(
        JSON.stringify({
          ...world,
          definition: {
            ...world.definition,
            grid: { ...world.definition.grid, width: 2880, height: 1440 },
          },
        }),
      ),
    /grade/,
  );
});

test("Camera anchors both projections, wraps the date line and resolves native globe cells at deep zoom", async () => {
  const js = (
    await build({
      stdin: {
        contents:
          'export {MapRenderer} from "./src/rendering/map.js"; export {bindPinch} from "./src/rendering/gestures.js";',
        resolveDir: process.cwd(),
      },
      bundle: true,
      write: false,
      format: "iife",
      globalName: "AtlasTest",
    })
  ).outputFiles[0].text;
  const browser = await chromium.launch({
    headless: true,
    args: ["--no-sandbox"],
  });
  try {
    const page = await browser.newPage({
      viewport: { width: 1100, height: 850 },
      deviceScaleFactor: 2,
    });
    await page.setContent(
      '<canvas id="map" style="width:1000px;height:750px;touch-action:none"></canvas>',
    );
    await page.addScriptTag({ content: js });
    const result = await page.evaluate(() => {
      const canvas = document.getElementById("map"),
        renderer = new AtlasTest.MapRenderer(canvas);
      const grid = { width: 1440, height: 720 };
      const n = grid.width * grid.height;
      const world = {
        definition: { grid, planet: { radius: 6371000 } },
        state: { elevation: Array(n).fill(100), entities: [], overrides: {} },
      };
      const derived = {
        biome: new Uint8Array(n).fill(6),
        basin: Int32Array.from({ length: n }, (_, i) => i),
        downstream: new Int32Array(n).fill(-1),
        discharge: new Float64Array(n),
        terminal: new Uint8Array(n),
      };
      renderer.setWorld(world, derived);
      renderer.showGrid = false;
      renderer.showRivers = false;
      renderer.layer = "basins";
      const anchors = [];
      for (const projection of ["flat", "globe"]) {
        renderer.projection = projection;
        renderer.zoom = 1;
        renderer.pan = { x: 0, y: 0 };
        renderer.render();
        const before = renderer.geographic(570, 340);
        renderer.zoomAt(40, 570, 340);
        renderer.render();
        const after = renderer.geographic(570, 340);
        anchors.push({
          projection,
          error: Math.hypot(before.lat - after.lat, before.lon - after.lon),
          zoom: renderer.zoom,
        });
      }
      renderer.projection = "flat";
      renderer.zoom = 16;
      renderer.pan = { x: 0, y: 0 };
      renderer.resize();
      renderer.focus(20, 179.8);
      const crossing = renderer.geographic(renderer.w / 2 + 50, renderer.h / 2);
      const picked = renderer.point(crossing.lat, crossing.lon);
      const wrapError = Math.hypot(
        picked.x - (renderer.w / 2 + 50),
        picked.y - renderer.h / 2,
      );
      renderer.projection = "globe";
      renderer.zoom = 16;
      renderer.pan = { x: 0, y: 0 };
      renderer.rotation = 0;
      renderer.render();
      const ctx = canvas.getContext("2d");
      const sample = (lat, lon) => {
        const p = renderer.point(lat, lon);
        return Array.from(
          ctx.getImageData(
            Math.round(p.x * renderer.ratio),
            Math.round(p.y * renderer.ratio),
            1,
            1,
          ).data,
        );
      };
      const a = sample(12.125, 0.125),
        b = sample(12.125, 0.375);
      const backing = [
        renderer.globeCache.off.width,
        renderer.globeCache.off.height,
      ];
      renderer.zoomAt(100);
      renderer.render();
      window.renderer = renderer;
      AtlasTest.bindPinch(canvas, renderer);
      return { anchors, wrapError, a, b, backing, maxZoom: renderer.zoom };
    });
    for (const anchor of result.anchors) {
      assert.ok(anchor.error < 1e-8, JSON.stringify(anchor));
      assert.equal(anchor.zoom, 40);
    }
    assert.ok(result.wrapError < 1e-7);
    assert.notDeepEqual(
      result.a,
      result.b,
      "Adjacent source cells must remain distinct in the zoomed globe",
    );
    assert.deepEqual(result.backing, [2000, 1500]);
    assert.equal(result.maxZoom, 128);
    await page.screenshot({ path: "test-results/native-detail-globe.png" });
    // Exercise real two-touch event sequences; centroids pan while distance controls zoom.
    await page.evaluate(() => {
      renderer.zoom = 4;
      renderer.pan = { x: 0, y: 0 };
      renderer.render();
    });
    const cdp = await page.context().newCDPSession(page);
    const send = (type, points) =>
      cdp.send("Input.dispatchTouchEvent", { type, touchPoints: points });
    await send("touchStart", [
      { x: 430, y: 380, id: 1 },
      { x: 570, y: 380, id: 2 },
    ]);
    await send("touchMove", [
      { x: 340, y: 400, id: 1 },
      { x: 660, y: 400, id: 2 },
    ]);
    await send("touchEnd", []);
    assert.ok((await page.evaluate(() => renderer.zoom)) > 8);
  } finally {
    await browser.close();
  }
});

test("Explicit refinement preserves authored footprints and notes; returning to the original resolution restores its complete journal", async () => {
  const { refine } = await import("../src/generation/refine.js");
  const { brush, record, restore } = await import("../src/core/world.js");
  const { coord, index } = await import("../src/geometry/sphere.js");
  const { world } = generate(definition("REFINE-PRESERVE", {}, 36));
  record(world, brush(world, 12, 179, 16, 700));
  world.state.entities.push({
    id: "user-place",
    name: "Meu lugar",
    type: "city",
    lat: 12,
    lon: 179,
    desc: "Autoral",
  });
  const before = structuredClone(world);
  const refined = refine(world, 144).world;
  for (let i = 0; i < refined.state.elevation.length; i++) {
    const p = coord(i, refined.definition.grid),
      parent = index(p.lat, p.lon, before.definition.grid);
    if (before.state.overrides[parent] !== undefined)
      assert.equal(refined.state.elevation[i], before.state.overrides[parent]);
  }
  assert.deepEqual(refined.state.entities, before.state.entities);
  assert.deepEqual(
    world,
    before,
    "Refinement must leave the committed source untouched until successful",
  );
  const roundtrip = parseProject(serialize(refined));
  restore(roundtrip, roundtrip.timeline.checkpoints[0].id);
  assert.deepEqual(roundtrip.definition, before.definition);
  assert.deepEqual(roundtrip.state, before.state);
  assert.deepEqual(roundtrip.journal, before.journal);
});
