import test from "node:test";
import assert from "node:assert/strict";
import {
  surfaceContext,
  sampleSurface,
  generateSurfaceTile,
  maxSurfaceLevel,
  TILE_SIZE,
  tileCoordinates,
} from "../src/rendering/surface-field.js";
import { definition, createWorld } from "../src/core/world.js";
import { serialize, parseProject } from "../src/storage/project.js";
import {
  radiation,
  insolation,
  SOLAR_CONSTANT,
} from "../src/solvers/radiation.js";
import { area } from "../src/geometry/sphere.js";
const fixture = (seed = "GROUND") => {
  const world = createWorld(definition(seed, {}, 36), Array(648).fill(1000));
  return {
    world,
    context: surfaceContext(world, { biome: new Uint8Array(648).fill(5) }),
  };
};
test("Continuous metric terrain is reproducible, crosses the date line and poles, and follows authored regional elevation", () => {
  const { world, context } = fixture();
  const p = sampleSurface(context, 12, 30);
  assert.ok(
    Math.abs(sampleSurface(context, 5, -175).elevation - 1000) < 1e-7,
    "Every authored macro sample stays an exact anchor at finer levels",
  );
  assert.deepEqual(p, sampleSurface(fixture().context, 12, 30));
  assert.notEqual(
    p.elevation,
    sampleSurface(fixture("OTHER").context, 12, 30).elevation,
  );
  assert.ok(
    Math.abs(
      sampleSurface(context, 12, -180).elevation -
        sampleSurface(context, 12, 180).elevation,
    ) < 1e-7,
  );
  assert.ok(
    Math.abs(
      sampleSurface(context, 90, -120).elevation -
        sampleSurface(context, 90, 70).elevation,
    ) < 1e-7,
  );
  assert.ok(
    Math.abs(p.elevation - sampleSurface(context, 12 + 1e-7, 30).elevation) <
      0.01,
    "A centimetre step cannot become a different continent",
  );
  world.state.elevation = world.state.elevation.map((h) => h + 75);
  assert.ok(
    Math.abs(
      sampleSurface(surfaceContext(world, { biome: context.biome }), 12, 30)
        .elevation -
        p.elevation -
        75,
    ) < 1e-8,
  );
  const roundtrip = parseProject(serialize(world));
  assert.deepEqual(
    sampleSurface(surfaceContext(roundtrip, { biome: context.biome }), 12, 30),
    sampleSurface(surfaceContext(world, { biome: context.biome }), 12, 30),
  );
});
test("Adjacent and parent/child tiles address the same terrain; metre detail is sparse rather than a planet-sized allocation", () => {
  const { context } = fixture();
  const z = maxSurfaceLevel(context.radius);
  assert.ok((Math.PI * context.radius) / (2 ** z * TILE_SIZE) <= 1);
  const tile = generateSurfaceTile(context, z, 400000, 100000);
  assert.equal(tile.rgba.length, TILE_SIZE * TILE_SIZE * 4);
  assert.equal(tile.material.length, TILE_SIZE * TILE_SIZE);
  const left = tileCoordinates(z, 400000, 100000, 1, 0.4),
    right = tileCoordinates(z, 400001, 100000, 0, 0.4);
  assert.deepEqual(left, right);
  assert.deepEqual(
    sampleSurface(context, left.lat, left.lon),
    sampleSurface(context, right.lat, right.lon),
  );
  const parent = tileCoordinates(z - 1, 200000, 50000, 0.2, 0.3),
    child = tileCoordinates(z, 400000, 100000, 0.4, 0.6);
  assert.ok(
    Math.abs(parent.lat - child.lat) < 1e-10 &&
      Math.abs(parent.lon - child.lon) < 1e-10,
  );
});
test("Solar energy integrates to S/4 in every season; tilt reverses hemispheres and polar night is dark", () => {
  for (const tilt of [0, 23.5, 60]) {
    const def = definition("ENERGY", { tilt }, 720),
      r = radiation(def.grid, def.planet);
    for (const name of ["solarAnnual", "solarJune", "solarDecember"]) {
      assert.equal(r[name].length, def.grid.height);
      const mean =
        Array.from(r[name]).reduce(
          (s, q, y) =>
            s + q * area(y, def.grid, def.planet.radius) * def.grid.width,
          0,
        ) /
        (4 * Math.PI * def.planet.radius ** 2);
      assert.ok(
        Math.abs(mean - SOLAR_CONSTANT / 4) < 0.005,
        `${tilt}/${name}: ${mean}`,
      );
    }
  }
  assert.equal(insolation(90, 23.5, 0.75), 0);
  assert.ok(insolation(70, 23.5, 0.25) > insolation(-70, 23.5, 0.25));
  assert.throws(() => insolation(NaN, 23, 0), /inválidos/);
});

test("Single HTML build preserves literal JavaScript replacement tokens and parses without a server", async () => {
  const { execFileSync } = await import("node:child_process");
  const { readFile } = await import("node:fs/promises");
  const { Script } = await import("node:vm");
  execFileSync(process.execPath, ["scripts/build.mjs"], { stdio: "pipe" });
  const html = await readFile("dist/atlas-forge.html", "utf8");
  const scripts = [
    ...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script\s*>/gi),
  ];
  assert.equal(scripts.length, 1);
  assert.doesNotThrow(
    () => new Script(scripts[0][1]),
    "Literal $& in compiled JS must not turn into </body>",
  );
});

test("Ground cover interpolates physical parent fields rather than enlarging biome pixels", () => {
  const { world } = fixture();
  const biome = Uint8Array.from({ length: 648 }, (_, i) =>
    i % 36 < 1 ? 5 : 7,
  );
  const context = surfaceContext(world, {
    biome,
    temperature: new Float32Array(648).fill(20),
    rain: new Float32Array(648).fill(500),
  });
  const left = sampleSurface(context, 5, -170.000001),
    right = sampleSurface(context, 5, -169.999999);
  assert.equal(left.material, 1);
  assert.equal(right.material, 1);
  assert.ok(Math.abs(left.elevation - right.elevation) < 0.02);
});
