import http from "node:http";
import { readFile } from "node:fs/promises";
const port = Number(process.env.PORT || 4173);
http
  .createServer(async (req, res) => {
    if (
      !["/", "/index.html", "/atlas-forge.html"].includes(
        req.url?.split("?")[0],
      )
    ) {
      res.writeHead(404);
      res.end();
      return;
    }
    try {
      res.setHeader("Content-Type", "text/html; charset=utf-8");
      res.setHeader("Cache-Control", "no-store");
      res.end(await readFile("dist/index.html"));
    } catch {
      res.writeHead(503);
      res.end("Execute npm run build primeiro.");
    }
  })
  .listen(port, "0.0.0.0", () =>
    console.log(`Atlas Forge em http://localhost:${port}`),
  );
