import { ECOSYSTEM_MODEL } from "../core/ecosystem.js";
const fail = (name) => {
  throw new Error(`Projeto inválido: ${name}`);
};
const number = (v, min, max, name) => {
  if (!Number.isFinite(v) || v < min || v > max) fail(name);
  return v;
};
const text = (v, max, name) => {
  if (typeof v !== "string" || v.length > max || !v.trim()) fail(name);
  return v;
};
export function validateSpecies(list) {
  if (!Array.isArray(list) || list.length > 24)
    fail("catálogo: limite de 24 espécies");
  const species = list.map((s) => {
    if (!s || typeof s !== "object") fail("espécie");
    const id = text(s.id, 120, "ID de espécie");
    if (
      !/^[a-zA-Z0-9_-]+$/.test(id) ||
      ["detritus", "__proto__", "constructor", "prototype"].includes(id)
    )
      fail("ID reservado ou inválido");
    if (
      !["producer", "consumer", "decomposer"].includes(s.role) ||
      !["land", "water"].includes(s.habitat) ||
      typeof s.managed !== "boolean"
    )
      fail("tipo/habitat/manejo da espécie");
    if (
      !Array.isArray(s.diet) ||
      s.diet.length > 24 ||
      new Set(s.diet).size !== s.diet.length
    )
      fail("dieta");
    const out = {
      id,
      name: text(s.name, 180, "nome da espécie"),
      role: s.role,
      habitat: s.habitat,
      diet: s.diet.map((x) => text(x, 120, "presa")),
      managed: s.managed,
    };
    for (const [key, min, max] of [
      ["massKg", 0.000001, 1e12],
      ["minTemp", -100, 100],
      ["maxTemp", -100, 100],
      ["minRain", 0, 10000],
      ["productivity", 0, 20],
      ["mortality", 0, 5],
      ["consumption", 0, 10],
      ["efficiency", 0, 1],
      ["dispersalKm", 0, 10000],
    ])
      out[key] = number(s[key], min, max, key);
    if (out.minTemp >= out.maxTemp) fail("intervalo de temperatura");
    return out;
  });
  const ids = new Set(species.map((s) => s.id));
  if (ids.size !== species.length) fail("IDs de espécies duplicados");
  for (const s of species) {
    if (s.role === "producer" && s.diet.length)
      fail("produtor não consome presas");
    if (s.diet.some((id) => id === s.id || (!ids.has(id) && id !== "detritus")))
      fail("presa ausente ou autoconsumo");
  }
  return species;
}
export function validateEcosystem(value, species) {
  if (value === null || value === undefined) {
    if (species.length) fail("catálogo sem estoques ecológicos");
    return null;
  }
  if (value.model !== ECOSYSTEM_MODEL) fail("modelo ecológico desconhecido");
  const w = value.grid?.width;
  if (
    !Number.isInteger(w) ||
    w < 12 ||
    w > 36 ||
    w % 2 ||
    value.grid.height !== w / 2
  )
    fail("grade ecológica");
  const n = (w * w) / 2,
    field = (values, name) => {
      if (!Array.isArray(values) || values.length !== n) fail(name);
      return values.map((v) => number(v, 0, 1e9, name));
    };
  if (!value.biomass || Object.keys(value.biomass).length !== species.length)
    fail("estoques de biomassa");
  const biomass = {};
  for (const s of species)
    biomass[s.id] = field(value.biomass[s.id], "biomassa");
  const year = number(value.year, 0, 1e9, "ano");
  if (!Number.isInteger(year)) fail("ano inteiro");
  let lastEra = null;
  if (value.lastEra) {
    lastEra = {};
    for (const key of [
      "start",
      "end",
      "productionKg",
      "respirationKg",
      "sedimentM3",
      "nitrogenErrorKg",
      "organicErrorKg",
    ])
      lastEra[key] = number(
        value.lastEra[key],
        key.endsWith("ErrorKg") ? -1e30 : 0,
        1e30,
        `era: ${key}`,
      );
    if (lastEra.start >= lastEra.end || lastEra.end !== year)
      fail("intervalo da era");
  }
  return {
    model: ECOSYSTEM_MODEL,
    grid: { width: w, height: w / 2 },
    year,
    nutrients: field(value.nutrients, "nitrogênio mineral"),
    detritus: field(value.detritus, "detritos"),
    biomass,
    lastEra,
  };
}
