import { random } from "./random.js";
/** A: connected authored-seed room graph; illustrative, not urban simulation. */
export function localMap(entity, seed) {
  const rng = random(seed, entity.id),
    rooms = Array.from({ length: 9 }, (_, i) => ({
      id: `${entity.id}-room-${i}`,
      name: [
        "Entrada",
        "Guarda",
        "Arquivo",
        "Pátio",
        "Grande salão",
        "Aposentos",
        "Cisterna",
        "Oficina",
        "Torre",
      ][i],
      x: 8 + (i % 3) * 30 + rng() * 3,
      y: 8 + Math.floor(i / 3) * 28 + rng() * 3,
      w: 19 + rng() * 3,
      h: 16 + rng() * 3,
    }));
  return {
    id: `${entity.id}-map`,
    version: 1,
    scaleMeters: 100,
    rooms,
    edges: Array.from({ length: 8 }, (_, i) => [i, i + 1]),
  };
}
