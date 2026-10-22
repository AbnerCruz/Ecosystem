export function drawLocal(canvas, entity) {
  const ctx = canvas.getContext("2d"),
    w = (canvas.width = 800),
    h = (canvas.height = 600),
    map = entity.localMap;
  ctx.fillStyle = "#15262b";
  ctx.fillRect(0, 0, w, h);
  ctx.strokeStyle = "#d9b980";
  ctx.lineWidth = 7;
  for (const [a, b] of map.edges) {
    const r = map.rooms[a],
      s = map.rooms[b];
    ctx.beginPath();
    ctx.moveTo((r.x + r.w / 2) * 8, (r.y + r.h / 2) * 6);
    ctx.lineTo((s.x + s.w / 2) * 8, (s.y + s.h / 2) * 6);
    ctx.stroke();
  }
  for (const r of map.rooms) {
    ctx.fillStyle = "#30423e";
    ctx.fillRect(r.x * 8, r.y * 6, r.w * 8, r.h * 6);
    ctx.strokeStyle = "#b8ae8d";
    ctx.lineWidth = 2;
    ctx.strokeRect(r.x * 8, r.y * 6, r.w * 8, r.h * 6);
    ctx.fillStyle = "#ece1c8";
    ctx.font = "16px Georgia";
    ctx.textAlign = "center";
    ctx.fillText(r.name, (r.x + r.w / 2) * 8, (r.y + r.h / 2) * 6);
  }
}
