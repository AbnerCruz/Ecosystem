export function escape(value) {
  return String(value).replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
}
/** Deliberately small, safe Markdown subset. Raw HTML and links never become active. */
export function markdown(source) {
  const lines = String(source).split("\n");
  let code = false;
  return (
    lines
      .map((line) => {
        if (line.startsWith("```")) {
          code = !code;
          return code ? "<pre><code>" : "</code></pre>";
        }
        let s = escape(line);
        if (code) return `${s}\n`;
        s = s
          .replace(/`([^`]+)`/g, "<code>$1</code>")
          .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
          .replace(/\*([^*]+)\*/g, "<em>$1</em>");
        const heading = /^(#{1,3}) (.*)$/.exec(s);
        return heading
          ? `<h${heading[1].length}>${heading[2]}</h${heading[1].length}>`
          : s.startsWith("- ")
            ? `<p>• ${s.slice(2)}</p>`
            : `<p>${s || "<br>"}</p>`;
      })
      .join("") + (code ? "</code></pre>" : "")
  );
}
export async function noteFromFile(file) {
  if (!/\.md$/i.test(file.name) || file.size > 1024 * 1024)
    throw new Error("Selecione um arquivo .md de até 1 MiB.");
  const source = await file.text();
  return {
    id: crypto.randomUUID(),
    name: file.name,
    markdown: source,
    hash: await digest(source),
    importedAt: new Date().toISOString(),
  };
}

export async function digest(source) {
  const hash = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(source),
  );
  return Array.from(new Uint8Array(hash), (x) =>
    x.toString(16).padStart(2, "0"),
  ).join("");
}
/** Verify imported/stored note snapshots before committing a project to the editor. */
export async function verifyNotes(world) {
  const checked = new Map();
  const states = [
    world.state,
    ...world.timeline.checkpoints.map((c) => c.state),
  ];
  for (const state of states)
    for (const entity of state.entities) {
      const note = entity.note;
      if (!note?.hash || checked.get(note.hash) === note.markdown) continue;
      if ((await digest(note.markdown)) !== note.hash)
        throw new Error(
          `Integridade da nota inválida: ${note.name}. O mundo anterior foi preservado.`,
        );
      checked.set(note.hash, note.markdown);
    }
  return world;
}
