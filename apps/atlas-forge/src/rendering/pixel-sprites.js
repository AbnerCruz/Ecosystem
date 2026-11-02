// A tiny procedural 16×16 pixel-art atlas, deliberately drawn in integer pixels.
// Offscreen canvases are local UI caches; no sprite is saved to a project.
const COLORS = {
  tree: ["#173f31", "#286747", "#378a52", "#62a45b", "#98bc68", "#65513b"],
  shrub: ["#314d34", "#527c42", "#72984e", "#a3b76a", "#c4bd73", "#574c34"],
  stone: ["#414e52", "#687278", "#909897", "#c2c1ae", "#ddddc9", "#47433d"],
};

function sprite(kind, variant) {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 16;
  const ctx = canvas.getContext("2d");
  const c = COLORS[kind];
  let state = (0xa529 + variant * 7919 + kind.length * 2654435761) >>> 0;
  const rng = () => {
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    return (state >>> 0) / 4294967296;
  };
  if (kind === "tree") {
    ctx.fillStyle = c[5];
    ctx.fillRect(7, 11, 3, 4);
  }
  const r = kind === "tree" ? 6 : kind === "shrub" ? 5 : 5;
  const cx = 8, cy = kind === "tree" ? 7 : 9;
  // Clustered, stepped silhouette with shadow, body and lit crown.
  for (let y = 1; y < 15; y++) {
    for (let x = 1; x < 15; x++) {
      const dx = (x - cx) / r;
      const dy = (y - cy) / (kind === "tree" ? 5.7 : 4.1);
      const rough = (rng() - 0.5) * 0.25;
      if (dx * dx + dy * dy > 1 + rough) continue;
      let palette = 1;
      if (dy > 0.35 || dx > 0.4) palette = 0;
      if (dx < 0.1 && dy < 0.12) palette = 2;
      if (dx < -0.35 && dy < -0.2) palette = 3;
      if (dx < -0.55 && dy < -0.45 && rng() > 0.45) palette = 4;
      ctx.fillStyle = c[palette];
      ctx.fillRect(x, y, 1, 1);
    }
  }
  if (kind === "stone") {
    ctx.fillStyle = c[0];
    ctx.fillRect(3, 13, 10, 1);
  }
  return canvas;
}

export function natureSprites() {
  const result = {};
  for (const kind of Object.keys(COLORS)) {
    result[kind] = Array.from({ length: 4 }, (_, i) => sprite(kind, i));
  }
  return result;
}
