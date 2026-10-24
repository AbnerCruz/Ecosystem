import test from "node:test";
import assert from "node:assert/strict";
import {
  definition,
  brush,
  record,
  history,
  restore,
} from "../src/core/world.js";
import { generate } from "../src/generation/pipeline.js";
import { environment, totals } from "../src/core/ecosystem.js";
import { ecologyYear } from "../src/solvers/ecosystem.js";
import { erosion, advanceEra } from "../src/solvers/eras.js";
import { serialize, parseProject } from "../src/storage/project.js";
import { area } from "../src/geometry/sphere.js";
const fixture = () => generate(definition("biosphere", {}, 24));
const near = (a, b) =>
  assert.ok(
    Math.abs(a - b) < Math.max(1, Math.abs(a), Math.abs(b)) * 1e-10,
    `${a} != ${b}`,
  );
test("Annual trophic resources conserve nitrogen and account for organic production/respiration across unequal cell areas", () => {
  const { world, derived } = fixture(),
    before = totals(world),
    env = environment(world, derived);
  let p = 0,
    r = 0;
  for (let y = 0; y < 100; y++) {
    const f = ecologyYear(world, env);
    p += f.productionKg;
    r += f.respirationKg;
  }
  const after = totals(world);
  near(before.nitrogenKg, after.nitrogenKg);
  near(
    before.biomassKg + before.detritusKg + p,
    after.biomassKg + after.detritusKg + r,
  );
  assert.ok(world.state.ecosystem.biomass.cattle.every((x) => x === 0));
  serialize(world);
});
test("No prey or suitable habitat means decline, no spontaneous population creation", () => {
  const { world, derived } = fixture(),
    eco = world.state.ecosystem;
  for (const s of world.state.species) eco.biomass[s.id].fill(0);
  eco.biomass.lion.fill(0.1);
  ecologyYear(world, environment(world, derived));
  assert.ok(
    eco.biomass.lion.reduce((a, b) => a + b, 0) < eco.nutrients.length * 0.1,
  );
  assert.ok(eco.biomass.grass.every((x) => x === 0));
});
test("Erosion conserves spherical volume, plant cover reduces transport and authored constraints survive", () => {
  const { world, derived } = fixture();
  const bare = structuredClone(world),
    covered = structuredClone(world);
  bare.state.ecosystem.biomass.grass.fill(0);
  bare.state.ecosystem.biomass.trees.fill(0);
  covered.state.ecosystem.biomass.grass.fill(3);
  const volume = (w) =>
    w.state.elevation.reduce(
      (sum, e, i) =>
        sum +
        e *
          area(
            Math.floor(i / w.definition.grid.width),
            w.definition.grid,
            w.definition.planet.radius,
          ),
      0,
    );
  const before = volume(bare),
    v = erosion(bare, derived, 1000),
    less = erosion(covered, derived, 1000);
  assert.ok(v > 0 && less < v);
  near(volume(bare), before);
  const authored = structuredClone(world);
  record(authored, brush(authored, 0, 0, 30, 800, "land"));
  const overrides = { ...authored.state.overrides };
  erosion(authored, derived, 1000);
  for (const [i, h] of Object.entries(overrides))
    assert.equal(authored.state.elevation[i], h);
});
test("Era commits a causal successor, preserves input and restores complete prior state", () => {
  const { world } = fixture(),
    before = serialize(world);
  const result = advanceEra(world, 1000);
  assert.equal(serialize(world), before);
  assert.equal(result.world.state.ecosystem.year, 1000);
  assert.notDeepEqual(result.world.state.elevation, world.state.elevation);
  assert.ok(result.world.state.ecosystem.lastEra.sedimentM3 > 0);
  const roundtrip = parseProject(serialize(result.world));
  restore(roundtrip, roundtrip.timeline.checkpoints[0].id);
  assert.deepEqual(roundtrip.state, world.state);
  assert.deepEqual(roundtrip.definition, world.definition);
});
test("Land and sea strokes are idempotent, persistent and reversible", () => {
  const { world } = fixture(),
    before = structuredClone(world.state);
  record(world, brush(world, 0, 179, 30, 800, "land"));
  assert.equal(brush(world, 0, 179, 30, 800, "land").length, 0);
  assert.ok(Object.keys(world.state.overrides).length);
  history(world, "undo");
  assert.deepEqual(world.state, before);
  record(world, brush(world, 0, 179, 30, 800, "sea"));
  assert.ok(Object.values(world.state.overrides).every((x) => x <= -800));
  assert.deepEqual(parseProject(serialize(world)).state, world.state);
});
test("Old V2 projects are rejected and current species/ecosystem input is validated", () => {
  const { world } = fixture(),
    legacy = structuredClone(world);
  legacy.version = 2;
  legacy.state.species = [];
  delete legacy.state.ecosystem;
  const original = JSON.stringify(legacy);
  assert.throws(() => parseProject(original), /projeto antigo incompatível/);
  assert.equal(JSON.stringify(legacy), original);
  for (const mutate of [
    (w) => (w.state.species[0].id = "__proto__"),
    (w) => (w.state.species[2].diet = ["missing"]),
    (w) => (w.state.ecosystem.biomass.grass[0] = -1),
    (w) => (w.state.ecosystem.model = "future"),
    (w) => (w.version = 2),
  ]) {
    const bad = structuredClone(world);
    mutate(bad);
    assert.throws(() => serialize(bad));
  }
});

test("Ten-thousand-year successor stays finite and its nitrogen balance closes", () => {
  const { world } = fixture(),
    before = totals(world);
  const result = advanceEra(world, 10000);
  assert.equal(result.world.state.ecosystem.year, 10000);
  near(totals(result.world).nitrogenKg, before.nitrogenKg);
  serialize(result.world);
});
test("Ocean canvas remains ocean on seeded regeneration except explicitly drawn land", async () => {
  const { oceanCanvas } = await import("../src/generation/pipeline.js");
  const { world } = oceanCanvas(definition("draw", {}, 24));
  assert.ok(world.state.elevation.every((h) => h < 0));
  record(world, brush(world, 0, 0, 30, 800, "land"));
  const { world: regenerated } = generate(
    world.definition,
    world.state.overrides,
  );
  assert.deepEqual(regenerated.state.elevation, world.state.elevation);
  assert.deepEqual(world.state.entities, []);
});
