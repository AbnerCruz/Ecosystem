import { validateSpecies, validateEcosystem } from "./ecosystem.js";
import { ALGORITHM, LEGACY_ALGORITHM, createWorld, definition } from "../core/world.js";
import { clamp, wrap } from "../geometry/sphere.js";
export const MAX_BYTES = 24 * 1024 * 1024;
function fail(message) {
  throw new Error(`Projeto inválido: ${message}`);
}
function finite(n, a, b, label) {
  if (!Number.isFinite(n) || n < a || n > b) fail(label);
  return n;
}
function text(v, limit, label) {
  if (typeof v !== "string" || v.length > limit) fail(label);
  return v;
}
function heights(v, n) {
  if (!Array.isArray(v) || v.length !== n) fail("dimensão de elevação");
  return v.map((x) => finite(x, -12000, 12000, "altitude em metros"));
}
function entity(v, position) {
  if (!v || typeof v !== "object") fail("entidade");
  const e = {
    id: text(v.id ?? `legacy-${position}`, 120, "id"),
    type: text(v.type ?? "castle", 30, "tipo"),
    name: text(v.name ?? "Lugar sem nome", 180, "nome"),
    lat: finite(v.lat, -90, 90, "latitude"),
    lon: wrap(finite(v.lon, -360, 360, "longitude")),
    desc: text(v.desc ?? "", 20000, "descrição"),
  };
  if (v.note) {
    const note = v.note;
    e.note = {
      id: text(note.id, 120, "id da nota"),
      name: text(note.name, 240, "nome da nota"),
      markdown: text(note.markdown, 1024 * 1024, "Markdown"),
      hash: text(note.hash, 64, "hash"),
      importedAt: text(note.importedAt, 80, "data da nota"),
    };
    if (e.note.hash && !/^[a-f0-9]{64}$/.test(e.note.hash))
      fail("SHA-256 da nota");
  } else if (v.md)
    e.note = {
      id: `legacy-note-${position}`,
      name: text(v.mdName ?? "nota.md", 240, "nome da nota"),
      markdown: text(v.md, 1024 * 1024, "Markdown"),
      hash: "",
      importedAt: "Alpha v1 — data desconhecida",
    };
  if (v.localMap) {
    const map = v.localMap;
    if (map.version !== 1) fail("versão desconhecida de mapa filho");
    if (
      !Array.isArray(map.rooms) ||
      map.rooms.length > 60 ||
      !Array.isArray(map.edges) ||
      map.edges.length > 120
    )
      fail("mapa filho");
    e.localMap = {
      id: text(map.id, 120, "id do mapa"),
      version: 1,
      scaleMeters: finite(map.scaleMeters, 1, 10000, "escala local"),
      rooms: map.rooms.map((r) => ({
        id: text(r.id, 100, "sala id"),
        name: text(r.name, 180, "sala"),
        x: finite(r.x, 0, 100, "x"),
        y: finite(r.y, 0, 100, "y"),
        w: finite(r.w, 1, 40, "largura"),
        h: finite(r.h, 1, 40, "altura"),
      })),
      edges: map.edges.map((pair) => {
        if (
          !Array.isArray(pair) ||
          pair.length !== 2 ||
          pair.some(
            (i) => !Number.isInteger(i) || i < 0 || i >= map.rooms.length,
          )
        )
          fail("conexão de salas");
        return [...pair];
      }),
    };
  }
  return e;
}
function state(v, n) {
  if (!v || !Array.isArray(v.entities) || v.entities.length > 2000)
    fail("estado/entidades");
  const overrides = {};
  for (const [key, val] of Object.entries(v.overrides ?? {})) {
    const i = Number(key);
    if (!Number.isInteger(i) || i < 0 || i >= n || String(i) !== key)
      fail("máscara autoral");
    overrides[i] = finite(val, -12000, 12000, "override");
  }
  const entities = v.entities.map(entity);
  if (new Set(entities.map((e) => e.id)).size !== entities.length)
    fail("IDs duplicados");
  const elevation = heights(v.elevation, n);
  for (const [i, value] of Object.entries(overrides))
    if (elevation[i] !== value) fail("máscara difere do relevo canônico");
  const species = validateSpecies(v.species ?? []);
  return {
    name: text(v.name ?? "Mundo sem nome", 180, "nome do mundo"),
    elevation,
    overrides,
    entities,
    species,
    ecosystem: validateEcosystem(v.ecosystem, species),
  };
}
function journal(v, n) {
  const out = { undo: [], redo: [] };
  for (const name of ["undo", "redo"]) {
    const list = v?.[name] ?? [];
    if (!Array.isArray(list) || list.length > 30) fail("histórico");
    out[name] = list.map((c) => {
      if (
        c.kind !== "elevation" ||
        !Array.isArray(c.changes) ||
        c.changes.length > n
      )
        fail("comando");
      return {
        kind: "elevation",
        changes: c.changes.map((x) => ({
          i: finite(x.i, 0, n - 1, "célula"),
          before: finite(x.before, -12000, 12000, "antes"),
          after: finite(x.after, -12000, 12000, "depois"),
          prior:
            x.prior === null
              ? null
              : finite(x.prior, -12000, 12000, "máscara anterior"),
        })),
      };
    });
    if (out[name].some((c) => c.changes.some((x) => !Number.isInteger(x.i))))
      fail("índice não inteiro");
  }
  return out;
}
export function validate(input) {
  if (!input || input.format !== "atlasforge.world")
    fail("formato desconhecido");
  if (input.version === 1) return migrateV1(input);
  if (![2, 3].includes(input.version))
    fail("versão desconhecida; o arquivo original foi preservado");
  if (
    input.version === 2 &&
    (input.state?.species?.length || input.state?.ecosystem)
  )
    fail("dados ecológicos sem versão compatível; original preservado");
  const d = input.definition;
  if (!d || ![ALGORITHM, LEGACY_ALGORITHM].includes(d.algorithm))
    fail("versão de algoritmo desconhecida");
  const w = d.grid?.width,
    h = d.grid?.height;
  if (!Number.isInteger(w) || w < 12 || w > 1440 || w % 2 || h !== w / 2)
    fail("grade não suportada (12–1440 × metade)");
  const planet = d.planet;
  finite(planet?.radius, 100000, 100000000, "raio");
  finite(planet.ocean, 5, 95, "oceanos");
  finite(planet.tilt, 0, 60, "inclinação");
  finite(planet.baseTemp, -50, 60, "temperatura");
  if (
    d.grid.topology !== "latlon-polar-reflection" ||
    d.grid.datum !== "sphere-sea-level" ||
    d.grid.elevationUnit !== "m"
  )
    fail("topologia, datum ou unidade desconhecidos");
  const def = definition(
    text(d.seed, 200, "seed"),
    {
      radius: planet.radius,
      ocean: planet.ocean,
      tilt: planet.tilt,
      baseTemp: planet.baseTemp,
    },
    w,
  );
  def.algorithm = d.algorithm;
  const out = createWorld(def, []);
  out.state = state(input.state, w * h);
  out.journal = journal(input.journal, w * h);
  const timeline = input.timeline ?? { active: null, checkpoints: [] };
  if (!Array.isArray(timeline.checkpoints) || timeline.checkpoints.length > 12)
    fail("marcos");
  out.timeline.checkpoints = timeline.checkpoints.map((c) => {
    const snapshot = validate({
      ...input,
      definition: c.definition ?? input.definition,
      state: c.state,
      journal: c.journal,
      timeline: { active: null, checkpoints: [] },
    });
    return {
      id: text(c.id, 120, "id de marco"),
      parent: c.parent === null ? null : text(c.parent, 120, "ramo"),
      label: text(c.label, 180, "rótulo"),
      definition: snapshot.definition,
      state: snapshot.state,
      journal: snapshot.journal,
    };
  });
  const ids = new Set(out.timeline.checkpoints.map((c) => c.id));
  if (
    ids.size !== out.timeline.checkpoints.length ||
    (timeline.active !== null && !ids.has(timeline.active))
  )
    fail("identidade de marco");
  const seen = new Set();
  for (const c of out.timeline.checkpoints) {
    if (c.parent !== null && !seen.has(c.parent))
      fail("ciclo ou ancestral ausente");
    seen.add(c.id);
  }
  out.timeline.active = timeline.active;
  return out;
}
export function parseProject(source) {
  if (new TextEncoder().encode(source).length > MAX_BYTES)
    fail("arquivo excede 24 MiB");
  let input;
  try {
    input = JSON.parse(source);
  } catch {
    fail("JSON malformado");
  }
  return validate(input);
}
export function serialize(world) {
  const source = JSON.stringify(validate(world));
  if (new TextEncoder().encode(source).length > MAX_BYTES)
    fail("projeto excede 24 MiB; reduza os marcos");
  return source;
}
function decode(value, n) {
  if (
    typeof value !== "string" ||
    value.length > n * 3 ||
    !/^[A-Za-z0-9+/]*={0,2}$/.test(value)
  )
    fail("base64 v1");
  let binary;
  try {
    binary = atob(value);
  } catch {
    fail("base64 v1");
  }
  if (binary.length !== n * 2) fail("tamanho de altura v1");
  return Array.from({ length: n }, (_, i) =>
    clamp(
      (((binary.charCodeAt(i * 2) + 256 * binary.charCodeAt(i * 2 + 1)) /
        65535) *
        2 -
        1) *
        6000,
      -12000,
      12000,
    ),
  );
}
export function migrateV1(v) {
  const w = v.width,
    h = v.height;
  if (!Number.isInteger(w) || w < 12 || w > 360 || w % 2 || h !== w / 2)
    fail("grade v1");
  const def = definition(
    text(v.seed ?? "ALPHA", 200, "seed"),
    {
      ocean: v.params?.ocean ?? 68,
      tilt: v.params?.tilt ?? 23.5,
      radius: (v.params?.radius ?? 6371) * 1000,
      baseTemp: v.params?.baseTemp ?? 15,
    },
    w,
  );
  const world = createWorld(def, decode(v.elevation, w * h));
  // Imported terrain is entirely authored: the unknown Alpha generator must not replace it.
  world.state.overrides = Object.fromEntries(
    world.state.elevation.map((e, i) => [i, e]),
  );
  if (!Array.isArray(v.entities ?? []) || (v.entities ?? []).length > 2000)
    fail("entidades v1");
  world.state.entities = (v.entities ?? []).map(entity);
  if (!Array.isArray(v.eras ?? []) || (v.eras ?? []).length > 12)
    fail("eras v1 excedem 12; divida o arquivo sem apagar o original");
  for (const [i, era] of (v.eras ?? []).entries()) {
    const s = structuredClone(world.state);
    s.elevation = decode(era.height, w * h);
    s.overrides = Object.fromEntries(s.elevation.map((e, j) => [j, e]));
    world.timeline.checkpoints.push({
      id: `alpha-era-${i}`,
      parent: i ? `alpha-era-${i - 1}` : null,
      label: `Alpha · ${finite(era.year, -1e12, 1e12, "ano da era")} anos`,
      definition: structuredClone(def),
      state: s,
      journal: { undo: [], redo: [] },
    });
  }
  return validate(world);
}
export function download(name, content, type = "application/json") {
  const url = URL.createObjectURL(new Blob([content], { type })),
    a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1500);
}
