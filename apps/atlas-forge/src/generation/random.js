export function random(seed, stream = "terrain") {
  let state = 2166136261;
  for (const ch of `${seed}:${stream}`)
    state = Math.imul(state ^ ch.charCodeAt(0), 16777619) >>> 0;
  return () => {
    state += 0x6d2b79f5;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
export function noise(seed) {
  const rng = random(seed),
    waves = Array.from({ length: 24 }, (_, i) => {
      const frequency = 3.4 * 2 ** Math.floor(i / 4);
      return {
        x: (rng() * 2 - 1) * frequency,
        y: (rng() * 2 - 1) * frequency,
        z: (rng() * 2 - 1) * frequency,
        phase: rng() * Math.PI * 2,
        amp: 1 / 2 ** Math.floor(i / 4),
      };
    });
  return ([x, y, z]) =>
    waves.reduce(
      (sum, w) => sum + Math.sin(x * w.x + y * w.y + z * w.z + w.phase) * w.amp,
      0,
    );
}
