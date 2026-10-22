import { verifyNotes } from "../documents/markdown.js";
import { parseProject, serialize } from "./project.js";
const DB = "atlas-forge-projects";
function open() {
  return new Promise((resolve, reject) => {
    const r = indexedDB.open(DB, 1);
    r.onupgradeneeded = () => r.result.createObjectStore("projects");
    r.onsuccess = () => resolve(r.result);
    r.onerror = () => reject(r.error);
  });
}
export async function saveLocal(world) {
  const value = serialize(world),
    db = await open();
  try {
    await new Promise((resolve, reject) => {
      const t = db.transaction("projects", "readwrite");
      t.objectStore("projects").put(value, "last");
      t.oncomplete = resolve;
      t.onerror = () => reject(t.error);
      t.onabort = () => reject(t.error ?? new Error("Gravação cancelada"));
    });
  } finally {
    db.close();
  }
  const recovered = await loadLocal();
  if (serialize(recovered) !== value)
    throw new Error("Falha ao verificar a cópia local. Exporte JSON.");
}
export async function loadLocal() {
  const db = await open();
  try {
    const value = await new Promise((resolve, reject) => {
      const r = db.transaction("projects").objectStore("projects").get("last");
      r.onsuccess = () => resolve(r.result);
      r.onerror = () => reject(r.error);
    });
    return value ? await verifyNotes(parseProject(value)) : null;
  } finally {
    db.close();
  }
}
