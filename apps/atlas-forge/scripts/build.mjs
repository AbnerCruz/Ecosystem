import { build } from "esbuild";
import { readFile, mkdir, writeFile } from "node:fs/promises";
const bundle = async (file) =>
  (
    await build({
      entryPoints: [file],
      bundle: true,
      write: false,
      format: "iife",
      target: "es2022",
      minify: true,
      legalComments: "none",
    })
  ).outputFiles[0].text;
const worker = await bundle("src/app/worker.js"),
  viewer = await bundle("src/publishing/viewer.js"),
  viewerCSS = await readFile("src/publishing/viewer.css", "utf8");
const result = await build({
  entryPoints: ["src/app/editor.js"],
  bundle: true,
  write: false,
  format: "iife",
  target: "es2022",
  minify: true,
  legalComments: "none",
  define: {
    __WORKER_SOURCE__: JSON.stringify(worker),
    __VIEWER_SOURCE__: JSON.stringify(viewer),
    __VIEWER_CSS__: JSON.stringify(viewerCSS),
  },
});
const css = await readFile("src/app/styles.css", "utf8"),
  html = await readFile("index.html", "utf8"),
  js = result.outputFiles[0].text.replace(/<\/script/gi, "<\\/script");
const output = html
  .replace("</head>", `<style>${css}</style></head>`)
  .replace("</body>", `<script>${js}</script></body>`);
await mkdir("dist", { recursive: true });
await writeFile("dist/index.html", output);
await writeFile("dist/atlas-forge.html", output);
console.log(
  `Editor offline: dist/atlas-forge.html (${(Buffer.byteLength(output) / 1024).toFixed(0)} KiB)`,
);
