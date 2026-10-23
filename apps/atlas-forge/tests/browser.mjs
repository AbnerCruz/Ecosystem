import { chromium } from "playwright";
import assert from "node:assert/strict";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { spawn } from "node:child_process";
import { resolve } from "node:path";
await mkdir("test-results", { recursive: true });
const server = spawn(process.execPath, ["scripts/serve.mjs"], {
  env: { ...process.env, PORT: "4175" },
  stdio: ["ignore", "pipe", "inherit"],
});
await new Promise((ok, no) => {
  server.stdout.once("data", ok);
  server.once("error", no);
});
const browser = await chromium.launch({
  headless: true,
  args: ["--no-sandbox"],
});
const results = [];
const context = await browser.newContext({
  viewport: { width: 1440, height: 1000 },
  acceptDownloads: true,
});
const page = await context.newPage(),
  errors = [];
page.on("pageerror", (e) => {
  errors.push(e.message);
  console.error("BROWSER ERROR", e.message);
});
const ready = async () => {
  await page.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("world-stats").textContent.includes("células"),
  );
};
async function saveProject() {
  const event = page.waitForEvent("download");
  await page.locator("#save-project").click();
  const d = await event.catch(async (e) => {
    throw new Error(
      e.message + "; status: " + (await page.locator("#status").innerText()),
    );
  });
  return JSON.parse(await readFile(await d.path(), "utf8"));
}
try {
  await page.goto("http://localhost:4175");
  await page.locator("#empty-state").waitFor({ state: "visible" });
  await page.waitForTimeout(800);
  assert.equal(await page.locator(".entity").count(), 0);
  assert.equal(await page.locator("#save-project").isDisabled(), true);
  await page.locator("#generation-form button[type=submit]").click();
  await ready();
  assert.equal(await page.locator(".entity").count(), 0);
  results.push(
    "Empty first launch; terrain generation requires explicit action and inserts no places or notes",
  );
  await page.screenshot({ path: "test-results/desktop.png", fullPage: true });
  const original = await saveProject();
  assert.equal(original.state.elevation.length, 259200);
  assert.deepEqual(original.state.entities, []);
  await page.locator("[data-tool=raise]").click();
  const box = await page.locator("#map").boundingBox();
  const x = box.x + box.width * 0.53,
    y = box.y + box.height * 0.47;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 20, y + 5, { steps: 3 });
  await page.mouse.up();
  await ready();
  const edited = await saveProject();
  assert.ok(Object.keys(edited.state.overrides).length > 0);
  assert.notDeepEqual(edited.state.elevation, original.state.elevation);
  await page.locator("#undo").click();
  await ready();
  assert.deepEqual(
    (await saveProject()).state.elevation,
    original.state.elevation,
  );
  await page.locator("#redo").click();
  await ready();
  assert.deepEqual(
    (await saveProject()).state.elevation,
    edited.state.elevation,
  );
  results.push("Brush, spherical edit journal, undo and redo");
  await page.locator("#generation-form button[type=submit]").click();
  await ready();
  const regenerated = await saveProject();
  for (const [i, value] of Object.entries(edited.state.overrides))
    assert.equal(regenerated.state.elevation[i], value);
  results.push("Regeneration preserves exact authored values");
  await page.locator("[data-layer=rain]").click();
  await page.locator("[data-layer=drainage]").click();
  await page.locator("#globe").click();
  await page.screenshot({ path: "test-results/globe.png" });
  await page.locator("#flat").click();
  const beforeCancel = await saveProject();
  await page.locator("#seed").fill("CANCELLED-SEED");
  await page.evaluate(() => {
    document.getElementById("generation-form").requestSubmit();
    document.getElementById("cancel").click();
  });
  await ready();
  assert.equal(
    (await saveProject()).definition.seed,
    beforeCancel.definition.seed,
  );
  results.push(
    "Worker termination preserves committed project and ignores cancelled results",
  );
  await page.locator("summary").filter({ hasText: "Memória do mundo" }).click();
  await page.locator("#checkpoint").click();
  await page.locator("#checkpoint").click();
  const checkpointed = await saveProject();
  assert.equal(checkpointed.timeline.checkpoints.length, 2);
  await page
    .locator("#timeline")
    .selectOption(checkpointed.timeline.checkpoints[0].id);
  await ready();
  await page.locator("#checkpoint").click();
  const branched = await saveProject();
  assert.equal(
    branched.timeline.checkpoints[2].parent,
    branched.timeline.checkpoints[0].id,
  );
  results.push("Checkpoint restoration and non-destructive branch");
  await page.locator("[data-tool=place]").click();
  await page.mouse.click(x - 90, y + 25);
  await page.locator("#entity-name").fill("Castelo do teste");
  await page
    .locator("#entity-desc")
    .fill("Lugar autoral para regressão end-to-end.");
  await page.locator("#update-entity").click();
  const hostile =
    '# Nota do castelo\n\nMemória segura.\n</script><script>globalThis.PWNED=1</script>\n<img src=x onerror="globalThis.PWNED=2">\n[perigo](javascript:alert(1))';
  await page.locator("#note-file").setInputFiles({
    name: "castelo.md",
    mimeType: "text/markdown",
    buffer: Buffer.from(hostile),
  });
  await page.waitForFunction(() =>
    document
      .getElementById("note-preview")
      .textContent.includes("Memória segura"),
  );
  assert.equal(await page.evaluate(() => globalThis.PWNED), undefined);
  const beforeLocal = await saveProject();
  assert.equal(beforeLocal.state.entities.at(-1).localMap, undefined);
  assert.equal(await page.locator("#inspect-local").isVisible(), false);
  await page.locator("#generate-local").click();
  await page.locator("#close-local").click();
  const finalProject = await saveProject();
  assert.ok(finalProject.state.entities.at(-1).localMap);
  assert.equal(finalProject.state.entities.at(-1).note.hash.length, 64);
  results.push(
    "Authored place, SHA-256 Markdown snapshot, hostile content escaped, persistent child map",
  );
  await page.locator("#project-file").setInputFiles({
    name: "world.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify(finalProject)),
  });
  await ready();
  assert.deepEqual(await saveProject(), finalProject);
  results.push("JSON export and reimport preserve every project field");
  await page.locator("#project-file").setInputFiles({
    name: "future.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify({ ...finalProject, version: 99 })),
  });
  await page.waitForFunction(() =>
    document
      .getElementById("status")
      .textContent.includes("versão desconhecida"),
  );
  assert.deepEqual(await saveProject(), finalProject);
  results.push("Future version rejected without corrupting committed data");
  // Wait for the verified IndexedDB write before testing restart.
  await page.waitForFunction(async (expected) => {
    const value = await new Promise((resolve, reject) => {
      const request = indexedDB.open("atlas-forge-projects", 1);
      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        const db = request.result;
        const read = db
          .transaction("projects")
          .objectStore("projects")
          .get("last");
        read.onsuccess = () => {
          db.close();
          resolve(read.result);
        };
        read.onerror = () => {
          db.close();
          reject(read.error);
        };
      };
    });
    return (
      value && JSON.stringify(JSON.parse(value)) === JSON.stringify(expected)
    );
  }, finalProject);

  await page.reload();
  await ready();
  assert.deepEqual(await saveProject(), finalProject);
  results.push("Verified IndexedDB persistence survives browser reload");
  const publicationEvent = page.waitForEvent("download");
  await page.locator("#publish").click();
  const publication = await publicationEvent,
    published = await readFile(await publication.path(), "utf8");
  await writeFile("test-results/published.html", published);
  const offlineContext = await browser.newContext({
      offline: true,
      viewport: { width: 1250, height: 850 },
    }),
    viewer = await offlineContext.newPage(),
    requests = [];
  viewer.on("pageerror", (e) => errors.push("viewer: " + e.message));
  viewer.on("request", (r) => {
    if (/^https?:/.test(r.url())) requests.push(r.url());
  });
  await viewer.goto("file://" + resolve("test-results/published.html"));
  await viewer
    .locator("#places button")
    .filter({ hasText: "Castelo do teste" })
    .click();
  assert.match(await viewer.locator("#note").innerText(), /Memória segura/);
  assert.equal(await viewer.evaluate(() => globalThis.PWNED), undefined);
  await viewer.locator("#inspect").click();
  assert.equal(
    await viewer.locator("#local-title").innerText(),
    "Castelo do teste",
  );
  await viewer.locator("#back").click();
  await viewer.locator("#layer").selectOption("temperature");
  await viewer.locator("#projection").click();
  const viewerBox = await viewer.locator("#map").boundingBox();
  await viewer.mouse.move(
    viewerBox.x + viewerBox.width / 2,
    viewerBox.y + viewerBox.height / 2,
  );
  for (let i = 0; i < 5; i++) await viewer.mouse.wheel(0, -240);
  await viewer.waitForFunction(
    () => Number(document.getElementById("map").dataset.zoom) > 20,
  );
  await viewer.locator("#reset").click();
  assert.equal(await viewer.locator("#map").getAttribute("data-zoom"), "1");
  await viewer.screenshot({ path: "test-results/offline-viewer.png" });
  assert.deepEqual(requests, []);
  await offlineContext.close();
  results.push(
    "Published single HTML works via file:// with network disabled, clickable notes and child maps",
  );
  // Single-file editor also boots with no HTTP server and no network.
  const fileContext = await browser.newContext({ offline: true }),
    filePage = await fileContext.newPage();
  filePage.on("pageerror", (e) => errors.push("file editor: " + e.message));
  await filePage.goto("file://" + resolve("dist/atlas-forge.html"));
  await filePage.locator("#empty-state").waitFor({ state: "visible" });
  await filePage.locator("#resolution").selectOption("360");
  await filePage.locator("#generation-form button[type=submit]").click();
  await filePage.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("world-stats").textContent.includes("células"),
  );
  await fileContext.close();
  results.push("Single HTML editor boots offline via file:// with blob Worker");
  const refinementContext = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    acceptDownloads: true,
  });
  const refinedPage = await refinementContext.newPage();
  refinedPage.on("pageerror", (e) => errors.push("refinement: " + e.message));
  await refinedPage.goto("file://" + resolve("dist/atlas-forge.html"));
  await refinedPage.locator("#empty-state").waitFor({ state: "visible" });
  await refinedPage.locator("#project-file").setInputFiles({
    name: "before.json",
    mimeType: "application/json",
    buffer: Buffer.from(JSON.stringify(original)),
  });
  await refinedPage.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("world-stats").textContent.includes("720"),
  );
  refinedPage.once("dialog", (d) => d.accept());
  await refinedPage.locator("#refine-terrain").click();
  await refinedPage.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("resolution").value === "1440",
  );
  assert.equal(await refinedPage.locator(".entity").count(), 0);
  await refinedPage.locator("[data-layer=elevation]").click();
  await refinedPage
    .locator("summary")
    .filter({ hasText: "Ir para coordenadas" })
    .click();
  await refinedPage.locator("#latitude").fill("20");
  await refinedPage.locator("#longitude").fill("120");
  await refinedPage.locator("#coordinate-form button").click();
  for (let i = 0; i < 9; i++) await refinedPage.locator("#zoom-in").click();
  await refinedPage.screenshot({
    path: "test-results/refined-detail.png",
    fullPage: true,
  });
  await refinedPage
    .locator("summary")
    .filter({ hasText: "Memória do mundo" })
    .click();
  await refinedPage.locator("#checkpoint").click();
  await refinedPage.waitForFunction(() =>
    document.getElementById("status").textContent.includes("excede 24 MiB"),
  );
  // The rejected oversized snapshot must not change the timeline or trap the project.
  assert.equal(await refinedPage.locator("#timeline option").count(), 2);
  const prior = await refinedPage
    .locator("#timeline option")
    .nth(1)
    .getAttribute("value");
  await refinedPage.locator("#timeline").selectOption(prior);
  await refinedPage.waitForFunction(
    () =>
      document.getElementById("busy").hidden &&
      document.getElementById("resolution").value === "720",
  );
  const restoredEvent = refinedPage.waitForEvent("download");
  await refinedPage.locator("#save-project").click();
  const restored = JSON.parse(
    await readFile(await (await restoredEvent).path(), "utf8"),
  );
  assert.deepEqual(restored.definition, original.definition);
  assert.deepEqual(restored.state, original.state);
  assert.deepEqual(restored.journal, original.journal);
  await refinementContext.close();
  results.push(
    "Explicit 1,440×720 refinement, no unsolicited places, detail view, oversized checkpoint rejected atomically, original resolution/state/history restored",
  );
  const mobileContext = await browser.newContext({
      viewport: { width: 390, height: 844 },
      isMobile: true,
      hasTouch: true,
      deviceScaleFactor: 1,
    }),
    mobile = await mobileContext.newPage();
  mobile.on("pageerror", (e) => errors.push("mobile: " + e.message));
  await mobile.goto("http://localhost:4175");
  await mobile.waitForFunction(() => document.getElementById("busy").hidden);
  await mobile.locator("[data-panel=left]").tap();
  await mobile.locator("#resolution").selectOption("360");
  await mobile.locator("#generation-form button[type=submit]").tap();
  await mobile.waitForFunction(
    () =>
      document.getElementById("world-stats").textContent.includes("células") &&
      document.getElementById("busy").hidden,
  );
  await mobile.locator("[data-layer=biome]").tap();
  await mobile.locator("[data-panel=left]").tap();
  await mobile.locator("[data-panel=right]").tap();
  assert.equal(await mobile.locator(".entity").count(), 0);
  await mobile.screenshot({ path: "test-results/mobile.png", fullPage: true });
  assert.ok(
    await mobile.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  );
  await mobileContext.close();
  results.push(
    "390×844 mobile viewport, touch panels, layers and places; no horizontal overflow (not physical Android validation)",
  );
  assert.deepEqual(errors, []);
  await writeFile(
    "test-results/report.json",
    JSON.stringify({ results, errors }, null, 2),
  );
  console.log(results.map((s) => "PASS " + s).join("\n"));
} finally {
  await browser.close();
  server.kill();
}
