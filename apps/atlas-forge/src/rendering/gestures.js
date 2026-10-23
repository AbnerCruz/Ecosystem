/** Two-touch navigation; single-pointer tools belong to editor/viewer. */
export function bindPinch(canvas, renderer, cancelTool = () => {}) {
  const touches = new Map();
  let gesture,
    suppress = false;
  const measure = () => {
    const [a, b] = [...touches.values()];
    return {
      x: (a.x + b.x) / 2,
      y: (a.y + b.y) / 2,
      distance: Math.max(1, Math.hypot(a.x - b.x, a.y - b.y)),
    };
  };
  const update = (e) => {
    const rect = canvas.getBoundingClientRect();
    touches.set(e.pointerId, {
      x: e.clientX - rect.left,
      y: e.clientY - rect.top,
    });
  };
  canvas.addEventListener(
    "pointerdown",
    (e) => {
      if (e.pointerType !== "touch") return;
      update(e);
      canvas.setPointerCapture(e.pointerId);
      if (touches.size >= 2) {
        suppress = true;
        cancelTool();
        gesture = measure();
        e.stopImmediatePropagation();
        e.preventDefault();
      }
    },
    true,
  );
  canvas.addEventListener(
    "pointermove",
    (e) => {
      if (!touches.has(e.pointerId)) return;
      update(e);
      if (!suppress) return;
      e.stopImmediatePropagation();
      e.preventDefault();
      if (touches.size < 2) return;
      const next = measure();
      renderer.zoomAt(next.distance / gesture.distance, gesture.x, gesture.y);
      renderer.pan.x += next.x - gesture.x;
      renderer.pan.y += next.y - gesture.y;
      gesture = next;
      renderer.render();
    },
    true,
  );
  for (const type of ["pointerup", "pointercancel", "lostpointercapture"])
    canvas.addEventListener(
      type,
      (e) => {
        if (!touches.has(e.pointerId)) return;
        touches.delete(e.pointerId);
        if (suppress) e.stopImmediatePropagation();
        if (!touches.size) {
          suppress = false;
          gesture = null;
        } else if (touches.size >= 2) gesture = measure();
      },
      true,
    );
}
