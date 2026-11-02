import test from "node:test";
import assert from "node:assert/strict";
import {
  area,
  coord,
  index,
  neighbors,
  distance,
  vector,
  geographic,
  orthographic,
  inverseOrtho,
} from "../src/geometry/sphere.js";
import {
  definition,
  brush,
  record,
  history,
  checkpoint,
  restore,
} from "../src/core/world.js";
import { generate } from "../src/generation/pipeline.js";
import { climate, insolation } from "../src/solvers/climate.js";
import { hydrology } from "../src/solvers/hydrology.js";
import { serialize, parseProject } from "../src/storage/project.js";
import { markdown, digest, verifyNotes } from "../src/documents/markdown.js";
import { scriptData } from "../src/publishing/export.js";
import { random } from "../src/generation/random.js";
import { localMap } from "../src/generation/local-map.js";
import { readFile } from "node:fs/promises";
const grid = { width: 24, height: 12 },
  planet = definition().planet;
test("Spherical cell areas sum to 4πR²; polar cells smaller than equatorial", () => {
  let sum = 0;
  for (let y = 0; y < grid.height; y++)
    sum += area(y, grid, planet.radius) * grid.width;
  assert.ok(Math.abs(sum / (4 * Math.PI * planet.radius ** 2) - 1) < 1e-12);
  assert.ok(area(0, grid, 1) < area(5, grid, 1));
});
test("Antimeridian, pole reflection and reciprocal adjacency", () => {
  assert.equal(
    distance({ lat: 0, lon: 179 }, { lat: 0, lon: -179 }, 1),
    (2 * Math.PI) / 180,
  );
  assert.ok(neighbors(0, grid).includes(grid.width - 1));
  assert.ok(neighbors(0, grid).includes(grid.width / 2));
  for (let i = 0; i < 288; i++)
    for (const j of neighbors(i, grid))
      assert.ok(neighbors(j, grid).includes(i));
  assert.equal(index(0, 180, grid), index(0, -180, grid));
});
test("Geographic vector and orthographic round trips including limb and centre", () => {
  for (const p of [
    { lat: 89, lon: 179 },
    { lat: -89, lon: -179 },
    { lat: 12, lon: 20 },
    { lat: 20, lon: 45 },
  ]) {
    const q = geographic(vector(p.lat, p.lon));
    assert.ok(distance(p, q, 1) < 1e-7);
    const screen = orthographic(p.lat, p.lon, 20);
    if (screen.visible) {
      const inverse = inverseOrtho(screen.x, screen.y, 20);
      assert.ok(distance(p, inverse, 1) < 1e-7);
    }
  }
  assert.equal(inverseOrtho(2, 0), null);
});
test("Seed/config reproducible, changed seed differs, subsystem RNG independent", () => {
  const d = definition("test", {}, 48),
    a = generate(d),
    b = generate(d);
  assert.deepEqual(a.world.state.elevation, b.world.state.elevation);
  assert.deepEqual(a.derived.rain, b.derived.rain);
  assert.notDeepEqual(
    a.world.state.elevation,
    generate(definition("other", {}, 48)).world.state.elevation,
  );
  const r = random("s", "terrain"),
    s = random("s", "terrain");
  random("s", "ecology")();
  assert.equal(r(), s());
});
test("Area weighted sea fraction follows target", () => {
  const { world } = generate(definition("ocean", { ocean: 68 }, 120));
  let sea = 0;
  for (let i = 0; i < world.state.elevation.length; i++)
    if (world.state.elevation[i] <= 0)
      sea += area(Math.floor(i / 120), world.definition.grid, 1);
  assert.ok(Math.abs(sea / (4 * Math.PI) - 0.68) < 0.001);
});
test("Solstice insolation: hemispheres reverse; no tilt gives no season", () => {
  assert.ok(insolation(60, 23.5, 0.25) > insolation(-60, 23.5, 0.25));
  assert.ok(insolation(-60, 23.5, 0.75) > insolation(60, 23.5, 0.75));
  assert.ok(
    Math.abs(insolation(30, 0, 0.25) - insolation(30, 0, 0.75)) < 1e-10,
  );
  assert.ok(insolation(90, 23.5, 0.75) < 1e-8);
});
test("Orographic ridge has wetter windward face, rain shadow and lapse cooling", () => {
  const g = { width: 48, height: 24 },
    e = new Float64Array(1152).fill(-100);
  const y = 6; // 41.25 N, eastward winds
  for (let x = 12; x < 36; x++) e[y * 48 + x] = 400;
  for (let x = 20; x < 24; x++) e[y * 48 + x] = 400 + (x - 19) * 700;
  const c = climate(e, g, planet);
  assert.ok(c.rain[y * 48 + 22] > c.rain[y * 48 + 26] * 2);
  assert.ok(c.temperature[y * 48 + 22] < c.temperature[y * 48 + 26]);
});
test("Drainage terminates, conserves runoff at terminals and never climbs", () => {
  const { world, derived: d } = generate(definition("water", {}, 48)),
    e = world.state.elevation,
    g = world.definition.grid;
  let input = 0,
    out = 0;
  for (let i = 0; i < e.length; i++) {
    if (e[i] > 0)
      input +=
        ((d.rain[i] / 1000) *
          0.35 *
          area(Math.floor(i / g.width), g, planet.radius)) /
        (365.25 * 86400);
    if (d.downstream[i] >= 0) {
      assert.ok(e[d.downstream[i]] < e[i]);
      assert.ok(neighbors(i, g).includes(d.downstream[i]));
    } else out += d.discharge[i];
    let k = i;
    for (let steps = 0; ; steps++) {
      assert.ok(steps < e.length);
      if (d.downstream[k] < 0) break;
      k = d.downstream[k];
    }
    assert.equal(d.basin[i], k);
  }
  assert.ok(Math.abs(input - out) < input * 1e-12);
});
test("Synthetic closed basin is endorheic, a sloped valley reaches ocean", () => {
  const e = new Float64Array(288).fill(1000),
    rain = new Float64Array(288).fill(1000),
    centre = 6 * 24 + 12;
  e[centre] = 100;
  for (const j of neighbors(centre, grid)) e[j] = 800;
  e[6 * 24 + 4] = 500;
  e[6 * 24 + 3] = 300;
  e[6 * 24 + 2] = -10;
  const d = hydrology(e, rain, grid, planet);
  assert.equal(d.terminal[centre], 2);
  assert.equal(d.basin[6 * 24 + 4], 6 * 24 + 2);
  assert.equal(d.terminal[6 * 24 + 2], 1);
});
test("Brush crosses antimeridian, undo restores original masks, regeneration preserves edits", () => {
  const def = definition("edit", {}, 48),
    { world } = generate(def),
    before = world.state.elevation.slice();
  record(world, brush(world, 10, 179, 12, 800));
  assert.ok(
    Object.keys(world.state.overrides).some((i) => Number(i) % 48 === 0),
  );
  assert.ok(
    Object.keys(world.state.overrides).some((i) => Number(i) % 48 === 47),
  );
  const after = world.state.elevation.slice();
  history(world, "undo");
  assert.deepEqual(world.state.elevation, before);
  assert.equal(Object.keys(world.state.overrides).length, 0);
  history(world, "redo");
  assert.deepEqual(world.state.elevation, after);
  const regenerated = generate(def, world.state.overrides).world;
  for (const [i, value] of Object.entries(world.state.overrides))
    assert.equal(regenerated.state.elevation[i], value);
});
test("Checkpoint branch preserves old states, configuration and undo history", () => {
  const { world } = generate(definition("branch", {}, 24));
  const a = checkpoint(world, "root");
  record(world, brush(world, 0, 0, 15, 100));
  world.definition.planet.tilt = 45;
  const b = checkpoint(world, "future");
  restore(world, a.id);
  assert.equal(world.definition.planet.tilt, 23.5);
  assert.equal(world.journal.undo.length, 0);
  const c = checkpoint(world, "alternative");
  assert.equal(c.parent, a.id);
  assert.equal(
    world.timeline.checkpoints.find((x) => x.id === b.id).definition.planet
      .tilt,
    45,
  );
  assert.equal(b.journal.undo.length, 1);
  assert.equal(parseProject(serialize(world)).timeline.checkpoints.length, 3);
});
test("Version 3 roundtrip, future/malformed/inconsistent inputs rejected without mutating original", () => {
  const { world } = generate(definition("persist", {}, 24));
  record(world, brush(world, 0, 0, 16, 500));
  const src = serialize(world);
  assert.equal(serialize(parseProject(src)), src);
  for (const mutation of [
    (x) => (x.version = 4),
    (x) => (x.state.elevation[0] = null),
    (x) => (x.state.overrides["99999"] = 5),
    (x) => (x.definition.planet.radius = -1),
    (x) => (x.definition.grid.width = 23),
  ]) {
    const x = JSON.parse(src);
    mutation(x);
    assert.throws(() => parseProject(JSON.stringify(x)));
  }
  assert.equal(serialize(world), src);
  assert.throws(() => parseProject("{bad"));
});
test("Obsolete Alpha v1 imports are rejected without converting or mutating source", async () => {
  const source = await readFile("fixtures/alpha-v1.synthetic.json", "utf8");
  assert.throws(() => parseProject(source), /projeto antigo incompatível/);
  assert.equal(JSON.parse(source).version, 1);
});
test("Hostile Markdown and publication JSON cannot close a script or execute active HTML", () => {
  const source =
    "</script><script>globalThis.PWNED=1</script>\n<img src=x onerror=alert(1)>\n[go](javascript:alert(1))";
  const rendered = markdown(source);
  assert.ok(!rendered.includes("<script>"));
  assert.ok(!rendered.includes("<img"));
  assert.ok(!rendered.includes("<a "));
  const data = scriptData({ note: source });
  assert.ok(!data.includes("<"));
  assert.equal(JSON.parse(data).note, source);
});
test("Local map is stable and all rooms are reachable", () => {
  const e = { id: "fortress" },
    a = localMap(e, "s");
  assert.deepEqual(a, localMap(e, "s"));
  const seen = new Set([0]);
  for (let j = 0; j < 10; j++)
    for (const [x, y] of a.edges) {
      if (seen.has(x)) seen.add(y);
      if (seen.has(y)) seen.add(x);
    }
  assert.equal(seen.size, a.rooms.length);
});

test("SHA-256 snapshots reject tampered Markdown in current and historical states", async () => {
  const { world } = generate(definition("hash", {}, 24));
  world.state.entities.push({
    id: "hash-place",
    type: "castle",
    name: "Hash",
    lat: 0,
    lon: 0,
    desc: "",
    note: {
      id: "n",
      name: "n.md",
      markdown: "safe",
      hash: await digest("safe"),
      importedAt: "2026-10-22",
    },
  });
  checkpoint(world, "verified");
  await verifyNotes(parseProject(serialize(world)));
  world.timeline.checkpoints[0].state.entities[0].note.markdown = "tampered";
  await assert.rejects(verifyNotes(world), /Integridade/);
});

test("Published viewer dependency graph excludes editor and solvers", async () => {
  const { build } = await import("esbuild");
  const result = await build({
    entryPoints: ["src/publishing/viewer.js"],
    bundle: true,
    write: false,
    metafile: true,
  });
  for (const name of Object.keys(result.metafile.inputs))
    assert.ok(!/src\/(solvers|generation|storage|app|core)\//.test(name), name);
});
