// UC-19 oracle for Personalização (appearance and ground): the UNMODIFIED 1.8.4-beta
// src/customize/customize.js and src/world/pixel-art.js run by Node with a minimal page.
// Per case it records palette(), variaveis(), carregarTexturas() and the textures UrbeArt
// holds after configure({palette, textures}), plus chunkPixels of a fixture with that ground.
// The canvas is a small stub of the 2D calls customize.js uses (putImageData, drawImage 1:1 or
// nearest, getImageData); smoothed downscales are NOT exercised here. Test-only.
//   node customize-theme-oracle.mjs <apps/urbe/src> < cases.json
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';

const src = process.argv[2];
const cases = JSON.parse(readFileSync(0, 'utf8'));
const b64 = a => Buffer.from(a).toString('base64');

function canvas() {
  const c = { width: 0, height: 0, _px: null };
  c.getContext = () => ({
    imageSmoothingEnabled: true,
    createImageData: (w, h) => ({ data: new Uint8ClampedArray(w * h * 4), width: w, height: h }),
    putImageData(img) { c._px = new Uint8ClampedArray(img.data); c.width = img.width; c.height = img.height; },
    drawImage(s, dx, dy, dw, dh) {
      const out = new Uint8ClampedArray(dw * dh * 4);
      for (let y = 0; y < dh; y++) for (let x = 0; x < dw; x++) {
        const sx = Math.min(s.width - 1, Math.floor((x + .5) * s.width / dw)), sy = Math.min(s.height - 1, Math.floor((y + .5) * s.height / dh));
        for (let k = 0; k < 4; k++) out[(y * dw + x) * 4 + k] = s._px[(sy * s.width + sx) * 4 + k];
      }
      c._px = out; c.width = dw; c.height = dh;
    },
    getImageData: (x, y, w, h) => ({ data: c._px ? c._px.slice(0, w * h * 4) : new Uint8ClampedArray(w * h * 4), width: w, height: h })
  });
  return c;
}

const posMod = (n, m) => ((n % m) + m) % m;
const FIX = {
  mixed: { cx: 1, cy: -1, biome: (x, y) => [5, 6, 7, 13, 10, 14, 15, 12, 18][posMod(x + y * 2, 9)], elev: (x, y) => 0.42 + (x % 7) * 0.007 - (y % 5) * 0.004 },
  coast: { cx: 0, cy: 0, biome: x => x < 8 ? 1 : 6, elev: x => x < 8 ? 0.34 : 0.56 }
};

const out = [];
for (const c of cases) {
  const files = new Map(Object.entries(c.files || {}));
  const el = () => ({ style: { setProperty() { }, removeProperty() { } }, setAttribute() { }, remove() { }, textContent: '' });
  const services = {
    documents: {
      get: p => files.has(p) ? { id: p, path: p, title: p.split('/').pop(), content: files.get(p), revision: 1 } : null,
      list: () => [...files.keys()].map(p => ({ id: p, path: p, title: p.split('/').pop(), content: files.get(p) })),
      upsert: d => { files.set(d.path, d.content); return d; }
    },
    persistence: { vault: 'Urbe' }
  };
  const ctx = {
    Math, JSON, Object, Array, String, Number, Map, Set, Promise, isFinite, console, setTimeout() { }, clearTimeout() { },
    Uint8ClampedArray, Uint8Array, Float32Array, Image: function () { },
    document: { documentElement: Object.assign(el(), { style: { setProperty() { }, removeProperty() { }, colorScheme: '' } }), querySelector: () => null, getElementById: () => null, createElement: t => t === 'canvas' ? canvas() : el(), head: { appendChild() { } } },
    location: { search: '' }, localStorage: { getItem: () => null, setItem() { }, removeItem() { } },
    UrbeCore: {
      service: n => services[n] || null, provide: (n, api) => { services[n] = api; },
      events: { on() { }, emit() { } }, commands: { register() { } }
    }
  };
  ctx.window = ctx; ctx.globalThis = ctx;
  runInNewContext(readFileSync(`${src}/world/pixel-art.js`, 'utf8'), ctx, { filename: 'pixel-art.js' });
  runInNewContext(readFileSync(`${src}/customize/customize.js`, 'utf8'), ctx, { filename: 'customize.js' });
  const C = services.customize, A = ctx.UrbeArt;
  const n = C.normalize(c.text);
  C.replace(n.config);
  const cfg = C.get();
  const pal = C.palette(), vars = C.variables();
  const r = { config: JSON.stringify(cfg), palette: pal, variables: { vars: vars.vars, attrs: vars.attrs, claro: vars.claro, fundo: vars.fundo } };
  const t = await C.loadTextures();
  r.textures = {
    errors: t.erros, paleta: t.paleta,
    chao: Object.fromEntries(Object.entries(t.chao).map(([k, v]) => [k, v.map(b64)])),
    construcoes: Object.fromEntries(Object.entries(t.construcoes).map(([k, v]) => [k, { w: v.width, h: v.height, data: b64(v._px) }]))
  };
  // the world.custom flow of aplicarCidade
  const palette = {};
  Object.keys(cfg.cidade.paleta || {}).forEach(k => (C.biomeId(k) || []).forEach(id => { palette[id] = cfg.cidade.paleta[k]; }));
  Object.keys(t.paleta).forEach(id => { if (!palette[id]) palette[id] = t.paleta[id]; });
  A.configure({ palette, textures: t.chao });
  r.style = { pal: Object.fromEntries(A.IDS.map(id => [id, A.PAL[id]])), tex: Object.fromEntries(A.IDS.map(id => [id, [0, 1, 2, 3].map(v => b64(A.texture(id, v)))])) };
  r.chunks = {};
  for (const [name, f] of Object.entries(FIX)) {
    const world = { CH: 16, sea: 0.42, biome: f.biome, elevation: f.elev, decor: () => null };
    r.chunks[name] = b64(A.chunkPixels(world, f.cx, f.cy));
  }
  const sp = C.saveTheme('Tema "x"/y é'); r.savedTheme = { path: sp, content: files.get(sp) };
  out.push(r);
}
process.stdout.write(JSON.stringify(out));
