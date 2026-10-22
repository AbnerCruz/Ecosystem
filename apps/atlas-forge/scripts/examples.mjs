import { build } from "esbuild";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { generate, derive } from "../src/generation/pipeline.js";
import { definition, brush, record, checkpoint } from "../src/core/world.js";
import { localMap } from "../src/generation/local-map.js";
import { serialize } from "../src/storage/project.js";
const { world } = generate(definition("MIRAVENTO-42"));
world.state.name = "Miravento";
checkpoint(world, "Origem procedural");
record(world, brush(world, 25, -20, 12, 2200));
const markdown =
  "# Fortaleza de Miravento\n\nErguida no divisor de águas, a fortaleza abriga o arquivo dos navegadores.\n\n## Ganchos de aventura\n- A cisterna secou depois do último inverno.\n- Uma galeria esquecida atravessa a montanha.";
const entity = {
  id: "example-castle-miravento",
  type: "castle",
  name: "Fortaleza de Miravento",
  lat: 25,
  lon: -20,
  desc: "Exemplo autoral, com relevo preservado, nota e mapa filho.",
  note: {
    id: "example-note",
    name: "Miravento.md",
    markdown,
    hash: createHash("sha256").update(markdown).digest("hex"),
    importedAt: "2026-10-22T00:00:00.000Z",
  },
};
entity.localMap = localMap(entity, world.definition.seed);
world.state.entities.push(entity);
checkpoint(world, "Cordilheira e fortaleza");
const js = (
  await build({
    entryPoints: ["src/publishing/export.js"],
    bundle: true,
    write: false,
    format: "esm",
    platform: "node",
    define: {
      __VIEWER_SOURCE__: JSON.stringify(
        (
          await build({
            entryPoints: ["src/publishing/viewer.js"],
            bundle: true,
            write: false,
            format: "iife",
            minify: true,
          })
        ).outputFiles[0].text,
      ),
      __VIEWER_CSS__: JSON.stringify(
        await readFile("src/publishing/viewer.css", "utf8"),
      ),
    },
  })
).outputFiles[0].text;
const { publish } = await import(
  "data:text/javascript;base64," + Buffer.from(js).toString("base64")
);
await mkdir("dist/examples", { recursive: true });
await writeFile("dist/examples/miravento.world.json", serialize(world));
await writeFile("dist/examples/miravento.html", publish(world, derive(world)));
await writeFile("dist/examples/Miravento.md", markdown);
console.log("Exemplo completo gerado em dist/examples/");
