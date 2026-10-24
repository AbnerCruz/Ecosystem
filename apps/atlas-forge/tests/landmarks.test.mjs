import test from "node:test";
import assert from "node:assert/strict";
import { definition, createWorld } from "../src/core/world.js";
import { surfaceContext } from "../src/rendering/surface-field.js";
import { nearbyNature } from "../src/rendering/landmarks.js";

function scene(height = 1200, biome = 7, seed = "NATURE") {
  const world = createWorld(definition(seed, {}, 36), Array(648).fill(height));
  const surface = surfaceContext(world, {
    biome: new Uint8Array(648).fill(biome),
  });
  return { world, surface };
}

const bounds = {
  west: -0.008,
  east: 0.008,
  south: -0.006,
  north: 0.006,
};

test("Ground props are deterministic, sparse, and independent of redraw or zoom", () => {
  const { world, surface } = scene();
  const before = structuredClone(world);
  const points = nearbyNature(world, surface, bounds, 0.8);
  assert.ok(points.length > 0 && points.length < 1801);
  assert.deepEqual(points, nearbyNature(world, surface, bounds, 0.1));
  assert.deepEqual(
    points,
    nearbyNature(...Object.values(scene()), bounds, 0.8),
  );
  assert.deepEqual(
    world,
    before,
    "Decorative objects never mutate project data",
  );
  assert.equal(nearbyNature(world, surface, bounds, 2.5).length, 0);
  const undergrowth = nearbyNature(world, surface, bounds, 0.03, 6);
  assert.ok(undergrowth.length > 0 && undergrowth.length <= 1800);
  assert.deepEqual(undergrowth, nearbyNature(world, surface, bounds, 0.03, 6));
  assert.equal(nearbyNature(world, surface, bounds, 0.8, 6).length, 0);
});

test("Ocean generates no land symbols, dry land has rock and shrubs", () => {
  const ocean = scene(-4000);
  assert.equal(nearbyNature(ocean.world, ocean.surface, bounds, 0.8).length, 0);
  const desert = scene(1500, 3);
  const points = nearbyNature(desert.world, desert.surface, bounds, 0.8);
  assert.ok(points.some((p) => p.kind === "stone"));
  assert.ok(points.every((p) => p.kind !== "tree"));
});

test("All local symbols keep physical units and remain bounded on large screens", () => {
  const { world, surface } = scene();
  const expanded = nearbyNature(
    world,
    surface,
    { west: -1, east: 1, south: -1, north: 1 },
    0.5,
  );
  assert.ok(expanded.length <= 1800);
  for (const p of expanded) {
    assert.ok(Number.isFinite(p.lat) && Number.isFinite(p.lon));
    assert.ok(p.diameterMetres > 0);
  }
});
