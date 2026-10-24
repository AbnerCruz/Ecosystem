import { chromium } from "playwright";
import assert from "node:assert/strict";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve } from "node:path";
import { generate } from "../src/generation/pipeline.js";
import { definition } from "../src/core/world.js";
import { serialize } from "../src/storage/project.js";
await mkdir("test-results", { recursive: true });
const browser = await chromium.launch({ args: ["--no-sandbox"] });
const context = await browser.newContext({
  acceptDownloads: true,
  viewport: { width: 1440, height: 1000 },
});
await context.setOffline(true);
const page = await context.newPage(),
  errors = [];
page.on("pageerror", (e) => errors.push(e.message));
const ready = () =>
  page.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("world-stats").textContent.includes("células"),
  );
const save = async () => {
  const event = page.waitForEvent("download");
  await page.locator("#save-project").click();
  return JSON.parse(await readFile(await (await event).path(), "utf8"));
};
try {
  await page.goto("file://" + resolve("dist/atlas-forge.html"));
  await page.locator("#empty-state").waitFor({ state: "visible" });
  const fixture = generate(definition("eco-browser", {}, 24)).world;
  await page.locator("#project-file").setInputFiles({
    name: "world.json",
    mimeType: "application/json",
    buffer: Buffer.from(serialize(fixture)),
  });
  await ready();
  const panel = page
    .locator("details")
    .filter({ has: page.locator("#eco-form") });
  await panel.locator("summary").click();
  await page.locator("#eco-new").click();
  await page.locator("#eco-name").fill("Cervo de fantasia <script>");
  await page.locator("#eco-diet").selectOption(["grass", "trees"]);
  await page.locator("#eco-minTemp").fill("-100");
  await page.locator("#eco-maxTemp").fill("100");
  await page.locator("#eco-minRain").fill("0");
  await page.locator("#eco-form button").click();
  let saved = await save();
  assert.equal(saved.state.species.length, 9);
  const custom = saved.state.species.at(-1);
  assert.equal(custom.name, "Cervo de fantasia <script>");
  assert.ok(saved.state.ecosystem.biomass[custom.id].every((x) => x === 0));
  await page.locator("#eco-target").selectOption("all");
  await page.locator("#eco-introduce").click();
  saved = await save();
  assert.ok(saved.state.ecosystem.biomass[custom.id].some((x) => x > 0));
  await page.locator("#eco-years").selectOption("10000");
  await page.locator("#eco-advance").click();
  await page.locator("#cancel").click();
  await ready();
  assert.equal((await save()).state.ecosystem.year, 0);
  await page.locator("#eco-years").selectOption("1000");
  await page.locator("#eco-advance").click();
  await ready();
  saved = await save();
  assert.equal(saved.state.ecosystem.year, 1000);
  assert.ok(saved.state.ecosystem.lastEra.sedimentM3 > 0);
  assert.equal(saved.timeline.checkpoints.length, 1);
  await page.locator("[data-layer=vegetation]").click();
  await page.screenshot({
    path: "test-results/ecosystem-editor.png",
    fullPage: true,
  });
  const event = page.waitForEvent("download");
  await page.locator("#publish").click();
  const html = await readFile(await (await event).path(), "utf8");
  const dest = resolve("test-results/ecosystem-published.html");
  await writeFile(dest, html);
  const viewer = await context.newPage();
  viewer.on("pageerror", (e) => errors.push(e.message));
  await viewer.goto("file://" + dest);
  assert.match(await viewer.locator("#eco-summary").textContent(), /1000/);
  await viewer.locator("#layer").selectOption("biomass");
  await viewer.locator("#map").click({ position: { x: 250, y: 250 } });
  assert.match(
    await viewer.locator("#eco-region").textContent(),
    /Cervo de fantasia <script>/,
  );
  assert.equal(await viewer.locator("#eco-region script").count(), 0);
  await page.locator("#project-file").setInputFiles({
    name: "era.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify(saved)),
  });
  await ready();
  assert.equal((await save()).state.ecosystem.year, 1000);
  await page
    .locator("details")
    .filter({ has: page.locator("#timeline") })
    .locator("summary")
    .click();
  await page
    .locator("#timeline")
    .selectOption(saved.timeline.checkpoints[0].id);
  await ready();
  assert.equal((await save()).state.ecosystem.year, 0);
  // Real pointer land/sea strokes remain separate from camera movement.
  for (const mode of ["land", "sea"]) {
    await page.locator(`[data-tool=${mode}]`).click();
    const box = await page.locator("#map").boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
    await ready();
    const w = await save();
    const values = Object.values(w.state.overrides);
    assert.ok(values.length);
    assert.ok(values.every((v) => (mode === "land" ? v >= 800 : v <= -800)));
    await page.locator("#undo").click();
    await ready();
    assert.deepEqual((await save()).state.overrides, {});
  }
  assert.deepEqual(errors, []);
  console.log(
    "PASS offline custom species, introduction, cancellation, 1000-year era, roundtrip, branch restoration, authored land/sea and interactive ecological publication",
  );
} finally {
  await browser.close();
}
