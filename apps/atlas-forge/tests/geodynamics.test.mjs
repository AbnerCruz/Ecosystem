import test from "node:test";
import assert from "node:assert/strict";
import { plateModel, plateElevation } from "../src/generation/geodynamics.js";
import { definition, ALGORITHM } from "../src/core/world.js";
import { generate } from "../src/generation/pipeline.js";
import { parseProject, serialize } from "../src/storage/project.js";
import { climate } from "../src/solvers/climate.js";
import { vector, area } from "../src/geometry/sphere.js";

test("Geodynamic plates have reproducible spherical centres, tangential rotations and finite elevations", () => {
  const a = plateModel("SHELF"), b = plateModel("SHELF");
  assert.deepEqual(a, b);
  assert.notDeepEqual(a, plateModel("DIFFERENT"));
  assert.equal(a.plates.length, 24);
  for (const plate of a.plates) {
    assert.ok(Math.abs(Math.hypot(...plate.center) - 1) < 1e-12);
    const cross = plate.center.reduce((s, v, i) => s + v * plate.angularVelocity[i], 0);
    assert.ok(Math.abs(cross) < 1e-12);
  }
  for (let i = -90; i <= 90; i += 15) {
    const p = vector(i, 180);
    const h = plateElevation(a, p, 0.3);
    assert.ok(Number.isFinite(h));
    assert.ok(Math.abs(h - plateElevation(a, vector(i, -180), 0.3)) < 1e-8);
  }
});

test("New tectonic generation is reproducible and rejects obsolete algorithm projects", () => {
  const def = definition("TECTONIC-ONLY", {}, 48);
  assert.equal(def.algorithm, ALGORITHM);
  const a = generate(def).world;
  assert.equal(parseProject(serialize(a)).definition.algorithm, ALGORITHM);
  assert.deepEqual(generate(def).world.state.elevation, a.state.elevation);
  const abandoned = structuredClone(a);
  abandoned.definition.algorithm = "spherical-causal-1";
  assert.throws(() => parseProject(JSON.stringify(abandoned)), /motor antigo/);
  assert.deepEqual(parseProject(serialize(a)).state.elevation, a.state.elevation);
});

test("Tectonic sea level quantile is area weighted and elevations remain bounded", () => {
  const { world } = generate(definition("CONTINENTS", { ocean: 71 }, 120));
  let sea = 0;
  for (let i = 0; i < world.state.elevation.length; i++) {
    const h = world.state.elevation[i];
    assert.ok(Number.isFinite(h) && h >= -12000 && h <= 12000);
    if (h <= 0) sea += area(Math.floor(i / 120), world.definition.grid, 1);
  }
  assert.ok(Math.abs(sea / (4 * Math.PI) - 0.71) < 0.001);
});

test("Metric climate rain is finite, has orographic lift and remains within bounded ratio across resolutions", () => {
  const planet = definition().planet;
  const results = [];
  for (const w of [48, 96]) {
    const h = w / 2, elevation = new Float64Array(w * h).fill(-50);
    const y = Math.floor((90 - 41.25) * h / 180);
    for (let x = Math.round(w * 0.25); x < Math.round(w * 0.75); x++) {
      elevation[y * w + x] = 400;
    }
    for (let x = Math.round(w * 0.4); x <= Math.round(w * 0.5); x++) {
      elevation[y * w + x] += 1500 * Math.sin(Math.PI * (x / w - 0.4) / 0.1);
    }
    const c = climate(elevation, {width:w,height:h}, planet);
    assert.ok(Array.from(c.rain).every(x=>x>=0 && x<=9000 && Number.isFinite(x)));
    const windward = c.rain[y * w + Math.round(w * 0.45)];
    const leeward = c.rain[y * w + Math.round(w * 0.65)];
    assert.ok(windward > leeward, String(windward) + " vs " + String(leeward));
    results.push([windward, leeward]);
  }
  assert.ok(results[0][0] / results[1][0] > 0.15);
  assert.ok(results[0][0] / results[1][0] < 6);
});
