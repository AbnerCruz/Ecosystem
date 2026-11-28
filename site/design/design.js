// Vitrine da linguagem visual do Ecosystem. Lê tokens.json (copiado de docs/design no build do portal), aplica os
// valores como variáveis CSS e monta as amostras; nenhum valor é escrito à mão aqui.
(async function () {
  const root = document.documentElement;
  const $ = (s, el = document) => el.querySelector(s);
  const $$ = (s, el = document) => [...el.querySelectorAll(s)];

  let tokens;
  try {
    const res = await fetch("tokens.json", { cache: "no-cache" });
    if (!res.ok) throw new Error(res.status + "");
    tokens = await res.json();
  } catch (e) {
    document.body.classList.remove("loading");
    $("[data-version]").textContent = "não foi possível ler tokens.json (" + e.message + ")";
    return;
  }

  // ---------- tokens → CSS variables ----------
  const set = (k, v) => root.style.setProperty(k, v);
  const g = tokens.global;
  if (!g || !g.themes) { document.body.classList.remove("loading"); $("[data-version]").textContent = "Tokens globais v2 indisponíveis"; return; }
  const params = new URLSearchParams(location.search);
  const pref = matchMedia("(prefers-color-scheme: dark)");
  const themeChoice = ["system","light","dark"].includes(params.get("theme")) ? params.get("theme") : g.defaultMode;
  const mode = themeChoice === "system" ? (pref.matches ? "dark" : "light") : themeChoice;
  const theme = g.themes[mode];
  const accent = Object.hasOwn(theme.accents, params.get("accent")) ? params.get("accent") : g.defaultAccent;
  const density = Object.hasOwn(g.density, params.get("density")) ? params.get("density") : "comfortable";
  root.dataset.mode = mode; root.dataset.density = density; root.style.colorScheme = mode;
  const controls = {"theme-choice":themeChoice,"accent-choice":accent,"density-choice":density};
  Object.entries(controls).forEach(([id,value]) => { const el = document.getElementById(id); el.value=value; el.addEventListener("change",()=>{const q=new URLSearchParams(location.search);q.set(id.split("-")[0],el.value);location.search=q.toString();}); });
  pref.addEventListener("change",()=>{if(themeChoice==="system") location.reload();});
  const c = {ink:theme.neutral, violet:theme.accents[accent], blue:theme.status.info, green:theme.status.success, amber:theme.status.warning, red:theme.status.danger, white:"#FFFFFF",black:"#000000",overlay:theme.overlay};
  set("--demo-control",g.density[density].control+"px");
  set("--demo-space",g.density[density].space+"px");
  for (const [k, v] of Object.entries(c.ink)) set(`--ink-${k}`, v);
  for (const hue of ["violet", "blue", "green", "amber", "red"]) {
    for (const [k, v] of Object.entries(c[hue])) set(`--${hue}-${k.replace(/[A-Z]/g, m => "-" + m.toLowerCase())}`, v);
  }
  set("--white", c.white); set("--black", c.black);
  for (const [k, v] of Object.entries(c.overlay)) set(`--${k}`, v);
  for (const [k, v] of Object.entries(tokens.radius)) set(`--r-${k}`, v + "px");
  for (const [k, v] of Object.entries(tokens.space)) set(`--s-${k}`, v + "px");
  for (const [k, v] of Object.entries(tokens.motion.duration)) set(`--d-${k}`, v + "ms");
  for (const [k, v] of Object.entries(tokens.motion.easing)) set(`--ease-${k}`, `cubic-bezier(${v.join(",")})`);
  set("--press-scale", tokens.motion.pressScale);
  set("--icon-stroke", tokens.size.iconStroke);
  const shadow = s => s.split(",").map(p => p.trim().replace(/^(inset )?(-?\d+) (-?\d+) (\d+) (\d+) (#[0-9A-Fa-f]{8})$/, (m, inset, x, y, b, sp, col) =>
    `${inset || ""}${x}px ${y}px ${b}px ${sp}px #${col.slice(3)}${col.slice(1, 3)}`)).join(", ");
  set("--shadow-overlay", shadow(tokens.shadow.overlay));
  set("--shadow-highlight", shadow(tokens.shadow.highlight));
  for (const [k, t] of Object.entries(tokens.type)) {
    set(`--t-${k}-size`, t.size + "px"); set(`--t-${k}-line`, t.line + "px"); set(`--t-${k}-weight`, t.weight);
    set(`--t-${k}-tracking`, (t.tracking || 0) + "em");
  }
  $("[data-version]").textContent = `v${g.version} · ${mode==="dark"?"escuro":"claro"} · ${accent} · ${density}`;

  // ---------- contrast ----------
  const rgb = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16) / 255);
  const lin = v => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
  const L = h => { const [r, g, b] = rgb(h).map(lin); return 0.2126 * r + 0.7152 * g + 0.0722 * b; };
  const wcag = (a, b) => { const x = L(a), y = L(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  const apca = (txt, bg) => {
    const Y = h => { const [r, g, b] = rgb(h); return 0.2126729 * r ** 2.4 + 0.7151522 * g ** 2.4 + 0.072175 * b ** 2.4; };
    let t = Y(txt), b = Y(bg);
    t = t > 0.022 ? t : t + (0.022 - t) ** 1.414; b = b > 0.022 ? b : b + (0.022 - b) ** 1.414;
    if (b > t) { const s = (b ** 0.56 - t ** 0.57) * 1.14; return s < 0.1 ? 0 : (s - 0.027) * 100; }
    const s = (b ** 0.65 - t ** 0.62) * 1.14; return s > -0.1 ? 0 : -(s + 0.027) * 100;
  };

  // ---------- ink scale ----------
  const roles = { 1: "fundo do app", 2: "fundo sutil · barras", 3: "superfície · campos", 4: "elevada · hover · tonal", 5: "sobreposição · folhas",
    6: "divisória", 7: "borda", 8: "borda forte", 9: "desativado", 10: "texto terciário", 11: "texto secundário", 12: "texto principal" };
  const bg = c.ink["1"];
  $("[data-ink]").innerHTML = Object.entries(c.ink).map(([k, v]) => {
    const isText = +k >= 9;
    const fg = wcag(c.ink["1"],v) > wcag(c.ink["12"],v) ? c.ink["1"] : c.ink["12"];
    const metric = isText ? `${wcag(v, bg).toFixed(1)}:1 · Lc ${apca(v, bg).toFixed(0)}` : "";
    return `<div class="swatch"><div class="chipc" style="background:${v};color:${fg}">ink ${k}</div>
      <div class="meta"><b>${roles[k]}</b><span class="tnum">${v}</span>${metric ? `<span class="tnum">${metric}</span>` : ""}</div></div>`;
  }).join("");

  // ---------- accent and semantic scales ----------
  const names = { violet: "Acento · "+accent, blue: "Informação", green: "Sucesso", amber: "Aviso", red: "Erro" };
  $("[data-scales]").innerHTML = Object.keys(names).map(h => {
    const s = c[h];
    const cells = ["soft", "softStrong", "9", "10", "11", "12"].filter(k => s[k]).map(k => {
      const v = s[k]; const solid = k === "9" || k === "10";
      const text = solid ? c.white : (k === "11" || k === "12" ? c.ink["1"] : s["11"] || c.ink["12"]);
      const metric = solid ? `branco ${wcag(c.white, v).toFixed(1)}:1` : (k === "11" ? `${wcag(v, bg).toFixed(1)}:1` : "");
      return `<div class="cell" style="background:${v};color:${text}"><b>${k === "softStrong" ? "soft+" : k}</b><span class="tnum">${metric}</span></div>`;
    }).join("");
    return `<div class="scale"><span>${names[h]}</span>${cells}</div>`;
  }).join("");

  // ---------- type ----------
  const uses = { caption: "metadados, contadores", label: "rótulo de campo, cabeçalho", bodySmall: "texto denso, subtítulo", callout: "botão, título de linha",
    body: "texto corrido, campo", titleSmall: "barra superior", title: "título de página e folha", headline: "título de tela", display: "abertura, destaque", code: "editor" };
  $("[data-types]").innerHTML = Object.entries(tokens.type).map(([k, t]) =>
    `<div class="type-row"><div class="spec-t"><b>${k}</b>${t.size}/${t.line} · ${t.weight}${t.tracking ? ` · ${t.tracking * 100}%` : ""}</div>
      <div style="font-size:${t.size}px;line-height:${t.line}px;font-weight:${t.weight};letter-spacing:${t.tracking || 0}em;${k === "code" ? "font-family:'JetBrains Mono',monospace" : ""}">${k === "code" ? "total = itens.Sum();" : "Informação clara e legível"} <span class="t-caption muted" style="font-family:Inter">${uses[k] || ""}</span></div></div>`).join("");

  // ---------- space and radii ----------
  $("[data-spaces]").innerHTML = Object.entries(tokens.space).map(([k, v]) => `<div class="space-row"><span>${v}</span><i style="width:${v * 4}px"></i></div>`).join("");
  const rUse = { xs: "chip pequeno, caixa", s: "tile de ícone", m: "campo, botão", l: "cartão, bloco", xl: "folha, diálogo", pill: "chip, busca" };
  $("[data-radii]").innerHTML = Object.entries(tokens.radius).map(([k, v]) => `<div class="radius" style="border-radius:${Math.min(v, 44)}px"><b>${k} · ${v === 999 ? "pílula" : v}</b>${rUse[k] || ""}</div>`).join("");

  // ---------- icons (the Lunet line set) ----------
  const icons = {
    Voltar: "M15 18l-6-6 6-6", Mais: "M12 5v14 M5 12h14", Rodar: "M7 4.5v15l12-7.5z", Pausar: "M8 5v14 M16 5v14", Código: "M9 7l-5 5 5 5 M15 7l5 5-5 5",
    Jogo: "M6 9h12a3 3 0 0 1 3 3v2a3 3 0 0 1-5.2 2.1L14 14h-4l-1.8 2.1A3 3 0 0 1 3 14v-2a3 3 0 0 1 3-3z", Arte: "M12 3a9 9 0 1 0 0 18c1.2 0 1.8-.9 1.8-1.8 0-1.1-.8-1.4-.8-2.4 0-.9.7-1.6 1.6-1.6H17a4 4 0 0 0 4-4C21 6.6 17 3 12 3z",
    Mapa: "M3 6l6-3 6 3 6-3v15l-6 3-6-3-6 3z M9 3v15 M15 6v15", Som: "M9 18V6l11-2v12 M9 18a3 3 0 1 1-6 0a3 3 0 1 1 6 0z M20 16a3 3 0 1 1-6 0a3 3 0 1 1 6 0z",
    Notas: "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z M14 3v5h5 M9 13h6M9 17h4", Pasta: "M3.5 7.5a2 2 0 0 1 2-2h3.8l2 2h7.2a2 2 0 0 1 2 2v7.5a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2z",
    Buscar: "M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14z M20 20l-4-4", Editar: "M4 20h4l10.5-10.5-4-4L4 16z M13.5 6.5l4 4", Copiar: "M9 9h11v11H9z M5 15H4V4h11v1",
    Lixeira: "M4 7h16 M10 11v6 M14 11v6 M6 7l1 13h10l1-13 M9 7V4h6v3", Baixar: "M12 3v12 M7 10l5 5 5-5 M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5",
    Enviar: "M12 15V3 M7 8l5-5 5 5 M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5", Pulso: "M3 12h4l3-7 4 14 3-7h4", Problema: "M12 4l9 16H3z M12 10v4 M12 17h.01",
    Ok: "M5 12l5 5L20 7", Fechar: "M6 6l12 12 M18 6L6 18", Inspector: "M4 6h9 M17 6h3 M15 4v4 M4 12h3 M11 12h9 M9 10v4 M4 18h11 M19 18h1 M17 16v4",
    Desfazer: "M9 14L4 9l5-5 M4 9h10a6 6 0 0 1 0 12h-3", Selecionar: "M4 7V4h3 M10 4h4 M17 4h3v3 M20 10v4 M20 17v3h-3 M14 20h-4 M7 20H4v-3 M4 14v-4",
  };
  const svg = d => `<svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="${d}"${d === icons.Rodar ? ' class="fill"' : ""}/></svg>`;
  $("[data-icons]").innerHTML = Object.entries(icons).map(([n, d]) => `<div class="icon-cell"><div class="sizes-i">${svg(d)}${svg(d)}${svg(d)}</div><span>${n}</span></div>`).join("");

  // ---------- motion ----------
  const motionRows = [
    ["instant", "standard", "pressionar, troca de cor"], ["fast", "standard", "hover, fade, interruptor"], ["base", "standard", "painel, aba, expandir"],
    ["slow", "enter", "folha, troca de página"], ["slower", "enter", "transição grande (raro)"], ["fast", "exit", "saída: acelera, mais curta"],
  ];
  $("[data-motion]").innerHTML = motionRows.map(([d, e, use]) =>
    `<button type="button" class="mot" data-mot style="--md:var(--d-${d});--me:var(--ease-${e})"><b>${d} · ${tokens.motion.duration[d]} ms</b><span>${e} · ${use}</span><div class="lane"><span class="dot" style="transition:left var(--md) var(--me)"></span></div></button>`).join("");
  const play = el => el.classList.toggle("go");
  $$("[data-mot]").forEach(el => el.addEventListener("click", () => play(el)));
  $("[data-play-motion]").addEventListener("click", () => $$("[data-mot]").forEach(play));

  // ---------- interactions ----------
  const motionToggle = $("[data-motion-toggle]");
  motionToggle.addEventListener("click", () => {
    const on = document.body.classList.toggle("reduce-motion");
    motionToggle.setAttribute("aria-pressed", on);
    motionToggle.classList.toggle("selected", on);
  });

  $$("[data-segmented]").forEach(g => g.addEventListener("click", e => {
    const b = e.target.closest("button"); if (!b) return;
    $$("button", g).forEach(x => x.setAttribute("aria-checked", x === b));
  }));
  $$("[data-chips]").forEach(g => g.addEventListener("click", e => {
    const b = e.target.closest(".chip"); if (!b) return;
    $$(".chip", g).forEach(x => { x.classList.toggle("selected", x === b); x.setAttribute("aria-pressed", x === b); });
  }));
  $$("[data-opts]").forEach(g => g.addEventListener("click", e => {
    const b = e.target.closest(".opt"); if (!b) return;
    $$(".opt", g).forEach(x => { x.classList.toggle("selected", x === b); x.setAttribute("aria-pressed", x === b); });
  }));
  $$("[data-nav-demo]").forEach(g => g.addEventListener("click", e => {
    const b = e.target.closest("button"); if (!b) return;
    $$("button", g).forEach(x => x.classList.toggle("on", x === b));
  }));
  $$("[data-range]").forEach(r => {
    const out = $("[data-out]", r.closest(".slider"));
    const upd = () => { out.textContent = r.value + "%"; r.style.setProperty("--fill", r.value + "%"); };
    r.addEventListener("input", upd); upd();
  });
  $$("[data-validate-email]").forEach(f => {
    const input = $("input", f);
    const check = () => {const invalid=input.value.length > 0 && !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(input.value); f.classList.toggle("invalid",invalid);input.setAttribute("aria-invalid",String(invalid));};
    input.addEventListener("input", check); check();
  });
  $$("[data-loading]").forEach(b => b.addEventListener("click", () => {
    if (b.classList.contains("busy")) return;
    b.classList.add("busy"); b.setAttribute("aria-busy", "true");
    setTimeout(() => { b.classList.remove("busy"); b.removeAttribute("aria-busy"); }, 1600);
  }));

  // sheet, dialog, menu, toast
  const scrim = $("[data-scrim]"), sheet = $("[data-sheet]"), dialog = $("[data-dialog]"), toast = $("[data-toast-box]");
  let lastFocus = null;
  const open = el => { lastFocus = document.activeElement; scrim.hidden = false; el.hidden = false; ($("[data-close]", el) || el).focus(); };
  const close = () => { scrim.hidden = true; sheet.hidden = true; dialog.hidden = true; lastFocus?.focus(); };
  $("[data-open-sheet]").addEventListener("click", () => open(sheet));
  $("[data-open-dialog]").addEventListener("click", () => open(dialog));
  scrim.addEventListener("click", close);
  $$("[data-close]").forEach(b => b.addEventListener("click", close));
  document.addEventListener("keydown", e => { if (e.key === "Escape") { close(); menu.hidden = true; } });
  const menuBtn = $("[data-open-menu]"), menu = $("[data-menu]");
  menuBtn.addEventListener("click", e => { e.stopPropagation(); menu.hidden = !menu.hidden; menuBtn.setAttribute("aria-expanded", !menu.hidden); if (!menu.hidden) $("button", menu).focus(); });
  document.addEventListener("click", e => { if (!menu.hidden && !menu.contains(e.target)) { menu.hidden = true; menuBtn.setAttribute("aria-expanded", "false"); } });
  $$("button", menu).forEach(b => b.addEventListener("click", () => { menu.hidden = true; menuBtn.setAttribute("aria-expanded", "false"); menuBtn.focus(); }));
  let toastTimer;
  $("[data-toast]").addEventListener("click", () => { toast.hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => toast.hidden = true, 6000); });
  $("[data-close-toast]").addEventListener("click", () => { toast.hidden = true; });

  // checklist counter
  const list = $("[data-checklist]"), count = $("[data-checklist-count]");
  const recount = () => { const boxes = $$("input", list); count.textContent = `${boxes.filter(b => b.checked).length} de ${boxes.length}`; };
  list.addEventListener("change", recount); recount();

  // section indicator in the index
  const links = $$(".toc a");
  const io = new IntersectionObserver(entries => entries.forEach(en => {
    if (!en.isIntersecting) return;
    links.forEach(a => a.classList.toggle("on", a.getAttribute("href") === "#" + en.target.id));
  }), { rootMargin: "-40% 0px -55% 0px" });
  $$("main section[id]").forEach(s => io.observe(s));

  document.body.classList.remove("loading");
})();
