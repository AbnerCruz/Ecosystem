// Thin DOM adapter for the C# Visual editor.
// No Markdown/domain/editor rules belong here.
export function readHtml(element) {
  return element?.innerHTML ?? "";
}

export function syncHtml(element, html, preserveFocus = true) {
  if (!element) return false;
  if (preserveFocus && document.activeElement === element) return false;

  const next = html ?? "";
  if (element.innerHTML === next) return false;

  element.innerHTML = next;
  return true;
}
