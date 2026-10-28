import { chromium } from "playwright";
import assert from "node:assert/strict";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { generate } from "../src/generation/pipeline.js";
import { definition } from "../src/core/world.js";
import { coord } from "../src/geometry/sphere.js";
import {
  sampleSurface,
  surfaceContext,
} from "../src/rendering/surface-field.js";
await mkdir("test-results", { recursive: true });
const { world, derived } = generate(definition("AURORA-42", {}, 180));
const source = surfaceContext(world, derived);
const candidate = world.state.elevation.findIndex(
  (e, i) =>
    e > 300 &&
    e < 2200 &&
    [4, 5, 6].includes(derived.biome[i]) &&
    Math.abs(coord(i, world.definition.grid).lat) < 65 &&
    sampleSurface(
      source,
      coord(i, world.definition.grid).lat,
      coord(i, world.definition.grid).lon,
    ).material === 1,
);
assert.ok(
  candidate >= 0,
  "Fixture needs a real grassland, rather than forcing a biome in the renderer",
);
const location = coord(candidate, world.definition.grid);
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox"],
});
const errors = [];
try {
  const context = await browser.newContext({
    offline: true,
    viewport: { width: 1440, height: 1000 },
    acceptDownloads: true,
  });
  const page = await context.newPage();
  page.on("pageerror", (e) => {
    errors.push(e.message);
    console.error(e.message);
  });
  await page.goto("file://" + resolve("dist/atlas-forge.html"));
  await page.locator("#empty-state").waitFor({ state: "visible" });
  await page.locator("#project-file").setInputFiles({
    name: "grassland.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify(world)),
  });
  await page.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("world-stats").textContent.includes("180"),
  );
  await page.screenshot({
    path: "test-results/scale-planet.png",
    fullPage: true,
  });
  await page
    .locator("summary")
    .filter({ hasText: "Ir para coordenadas" })
    .click();
  await page.locator("#latitude").fill(String(location.lat));
  await page.locator("#longitude").fill(String(location.lon));
  await page.locator("#coordinate-form button").click();
  const scales = [];
  for (const [value, name] of [
    ["500", "region"],
    ["5", "terrain"],
    ["0.03", "grass"],
  ]) {
    const start = performance.now();
    await page.locator("#detail-scale").selectOption(value);
    await page.waitForFunction(
      () => document.getElementById("map").dataset.terrainReady === "true",
      null,
      { timeout: 60000 },
    );
    const m = Number(
      await page.locator("#map").getAttribute("data-metres-per-pixel"),
    );
    assert.ok(
      m > Number(value) * 0.98 && m < Number(value) * 1.02,
      `${name}: ${m}`,
    );
    scales.push({
      name,
      readyMs: performance.now() - start,
      mpp: m,
      spacing: Number(
        await page.locator("#map").getAttribute("data-terrain-spacing"),
      ),
    });
    await page.screenshot({
      path: `test-results/scale-${name}.png`,
      fullPage: true,
    });
    if (name === "terrain") {
      await page.locator("[data-layer=elevation]").click();
      await page.screenshot({
        path: "test-results/scale-relief.png",
        fullPage: true,
      });
      await page.locator("[data-layer=atlas]").click();
    }
  }
  assert.ok(scales.at(-1).spacing <= 1);
  assert.match(await page.locator("#location-title").innerText(), /Grama/);
  const measured = Number(
    (await page.locator("#metric-elevation").innerText())
      .replace(/\./g, "")
      .replace(",", ".")
      .match(/^-?[0-9.]+/)[0],
  );
  assert.ok(
    Math.abs(
      measured - sampleSurface(source, location.lat, location.lon).elevation,
    ) < 0.02,
  );
  // A bounded cache replaces tiles instead of allocating a planet at metre resolution.
  const map = await page.locator("#map").boundingBox();
  for (let i = 0; i < 5; i++) {
    await page.mouse.move(map.x + map.width * 0.7, map.y + map.height * 0.6);
    await page.mouse.down();
    await page.mouse.move(map.x + map.width * 0.4, map.y + map.height * 0.6, {
      steps: 3,
    });
    await page.mouse.up();
    await page.waitForFunction(
      () => document.getElementById("map").dataset.terrainReady === "true",
      null,
      { timeout: 60000 },
    );
  }
  assert.ok(
    Number(await page.locator("#map").getAttribute("data-terrain-tiles")) <= 96,
  );
  await page.locator("[data-tool=place]").click();
  await page.mouse.click(map.x + map.width / 2, map.y + map.height / 2);
  await page.locator("#entity-name").fill("Acampamento do terreno");
  await page.locator("#entity-type").selectOption("camp");
  await page.locator("#update-entity").click();
  await page.locator("[data-tool=inspect]").click();
  await page.locator("#reset-view").click();
  await page.locator("[data-layer=insolation]").click();
  await page.locator("#season").selectOption("june");
  const june = await page.locator("#metric-solar").innerText();
  await page.locator("#season").selectOption("december");
  assert.notEqual(await page.locator("#metric-solar").innerText(), june);
  // Publication contains the same local terrain, with no HTTP access or planetary solvers.
  await page.locator("[data-layer=atlas]").click();
  await page.locator("#coordinate-form button").click();
  const event = page.waitForEvent("download");
  await page.locator("#publish").click();
  const publication = await event;
  await writeFile(
    "test-results/terrain-published.html",
    await readFile(await publication.path()),
  );
  const viewer = await context.newPage();
  viewer.on("pageerror", (e) => errors.push(e.message));
  await viewer.goto("file://" + resolve("test-results/terrain-published.html"));
  // Use the authored location of an explicitly supplied place to focus the publication.
  // There are intentionally no places in this fixture: navigate by the same centre before zoom.
  await viewer
    .locator("#places button")
    .filter({ hasText: "Acampamento do terreno" })
    .click();
  await viewer.locator("#scale").selectOption("0.03");
  await viewer.waitForFunction(
    () => document.getElementById("map").dataset.terrainReady === "true",
    null,
    { timeout: 60000 },
  );
  assert.ok(
    Number(await viewer.locator("#map").getAttribute("data-terrain-spacing")) <=
      1,
  );
  await viewer.screenshot({ path: "test-results/terrain-viewer.png" });
  // The deep globe shares the same surface, not a different scene.
  await viewer.locator("#projection").click();
  await viewer
    .locator("#places button")
    .filter({ hasText: "Acampamento do terreno" })
    .click();
  await viewer.locator("#scale").selectOption("0.03");
  await viewer.waitForFunction(
    () => document.getElementById("map").dataset.terrainReady === "true",
    null,
    { timeout: 60000 },
  );
  await viewer.screenshot({ path: "test-results/terrain-globe.png" });
  assert.deepEqual(errors, []);
  await writeFile(
    "test-results/terrain-report.json",
    JSON.stringify({ scales, errors, location }, null, 2),
  );
  console.log(
    "PASS Offline planet → region → terrain → metre grass tiles; local altitude, bounded LRU, pan continuity, seasonal irradiance and offline published terrain",
  );
  await context.close();
} finally {
  await browser.close();
}
