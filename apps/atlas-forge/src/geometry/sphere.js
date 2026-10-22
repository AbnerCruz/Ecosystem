/** Spherical datum, metres. Cell centres avoid singular point samples at poles. */
export const RAD = Math.PI / 180;
export const wrap = (lon) => ((((lon + 180) % 360) + 360) % 360) - 180;
export const clamp = (n, a, b) => Math.max(a, Math.min(b, n));
export function vector(lat, lon) {
  const p = lat * RAD,
    l = lon * RAD;
  return [Math.cos(p) * Math.cos(l), Math.sin(p), Math.cos(p) * Math.sin(l)];
}
export function geographic([x, y, z]) {
  return {
    lat: Math.atan2(y, Math.hypot(x, z)) / RAD,
    lon: wrap(Math.atan2(z, x) / RAD),
  };
}
export function distance(a, b, radius = 6371000) {
  const p = ((b.lat - a.lat) * RAD) / 2,
    l = (wrap(b.lon - a.lon) * RAD) / 2;
  return (
    2 *
    radius *
    Math.asin(
      Math.sqrt(
        clamp(
          Math.sin(p) ** 2 +
            Math.cos(a.lat * RAD) * Math.cos(b.lat * RAD) * Math.sin(l) ** 2,
          0,
          1,
        ),
      ),
    )
  );
}
export function coord(i, grid) {
  return {
    lat: 90 - ((Math.floor(i / grid.width) + 0.5) * 180) / grid.height,
    lon: -180 + (((i % grid.width) + 0.5) * 360) / grid.width,
  };
}
export function index(lat, lon, grid) {
  return (
    clamp(Math.floor(((90 - lat) / 180) * grid.height), 0, grid.height - 1) *
      grid.width +
    Math.floor(((wrap(lon) + 180) / 360) * grid.width)
  );
}
export function area(row, grid, radius) {
  return (
    ((radius ** 2 * 2 * Math.PI) / grid.width) *
    (Math.sin((90 - (row * 180) / grid.height) * RAD) -
      Math.sin((90 - ((row + 1) * 180) / grid.height) * RAD))
  );
}
export function neighbors(i, grid) {
  const { width: w, height: h } = grid,
    x = i % w,
    y = Math.floor(i / w);
  // Eight spherical edges; crossing a polar edge reflects latitude and rotates longitude.
  const out = [];
  for (let dy = -1; dy <= 1; dy++)
    for (let dx = -1; dx <= 1; dx++) {
      if (!dx && !dy) continue;
      let nx = x + dx,
        ny = y + dy;
      if (ny < 0) {
        ny = 0;
        nx += w / 2;
      }
      if (ny >= h) {
        ny = h - 1;
        nx += w / 2;
      }
      out.push(ny * w + ((nx + w) % w));
    }
  return out;
}
export function orthographic(lat, lon, rotation = 0, centerLat = 12) {
  const p = lat * RAD,
    l = wrap(lon - rotation) * RAD,
    c = centerLat * RAD;
  return {
    x: Math.cos(p) * Math.sin(l),
    y: Math.cos(c) * Math.sin(p) - Math.sin(c) * Math.cos(p) * Math.cos(l),
    visible:
      Math.sin(c) * Math.sin(p) + Math.cos(c) * Math.cos(p) * Math.cos(l) >= 0,
  };
}
export function inverseOrtho(x, y, rotation = 0, centerLat = 12) {
  const r = Math.hypot(x, y);
  if (r > 1) return null;
  if (r < 1e-9) return { lat: centerLat, lon: wrap(rotation) };
  const c = Math.asin(r),
    p = centerLat * RAD;
  return {
    lat:
      Math.asin(
        Math.cos(c) * Math.sin(p) + (y * Math.sin(c) * Math.cos(p)) / r,
      ) / RAD,
    lon: wrap(
      rotation +
        Math.atan2(
          x * Math.sin(c),
          r * Math.cos(p) * Math.cos(c) - y * Math.sin(p) * Math.sin(c),
        ) /
          RAD,
    ),
  };
}
