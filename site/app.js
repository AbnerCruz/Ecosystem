// Portal do Ecosystem — renderiza a projeção data/ecosystem-status.json (ADR-0005) e, ao vivo, lê a API pública do
// GitHub para os indicadores de integração (mesmo princípio do espelho de distribuição: somente leitura, nunca gravado,
// falha = "não disponível"). Nenhum dado canônico é escrito aqui: tudo vem da projeção ou da fonte, com proveniência.
"use strict";

const $ = (id) => document.getElementById(id);
const NS = "http://www.w3.org/2000/svg";
const DAY = 86400000;

const STATUS_LABELS = {
  planned: "planejado",
  "not-migrated": "fora do monorepo",
  migrating: "em migração",
  active: "ativo",
  deprecated: "obsoleto",
  archived: "arquivado",
};

// Estados de validação de build: docs/governance/definition-of-done.md §4.
const VALIDATION = {
  UNKNOWN: { label: "sem registro", tone: "", icon: "circle" },
  IMPLEMENTED: { label: "implementado", tone: "info", icon: "code" },
  AUTOMATED_VERIFIED: { label: "verificado automaticamente", tone: "info", icon: "bot" },
  HUMAN_VALIDATION_PENDING: { label: "validação humana pendente", tone: "warn", icon: "clock" },
  VALIDATED: { label: "validado por humano", tone: "good", icon: "check" },
};

const GATES = {
  aprovado: { label: "gate aprovado", short: "aprovado", tone: "good", icon: "check" },
  aguardando: { label: "gate aguardando você", short: "aguardando você", tone: "warn", icon: "clock" },
  "não iniciado": { label: "gate não iniciado", short: "não iniciado", tone: "", icon: "circle" },
};

const CHECK_LABELS = { passing: "passando", failing: "falhando" };

const CLASS_LABELS = {
  constitution: "constituição",
  "control-plane": "controle do sistema",
  security: "segurança",
  "user-data": "dados do usuário",
  distribution: "distribuição",
  compatibility: "compatibilidade",
  architecture: "arquitetura",
  "product-strategy": "estratégia de produto",
};

// Ícones de traço (24×24), escritos aqui por serem apresentação, não dados.
const ICONS = {
  check: '<path d="M20 6 9 17l-5-5"/>',
  x: '<path d="M18 6 6 18M6 6l12 12"/>',
  circle: '<circle cx="12" cy="12" r="8"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  alert: '<path d="m21.7 18-8-14a2 2 0 0 0-3.4 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.7-3Z"/><path d="M12 9v4M12 17h.01"/>',
  download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>',
  external: '<path d="M7 17 17 7M7 7h10v10"/>',
  globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/>',
  merge: '<circle cx="18" cy="18" r="3"/><circle cx="6" cy="6" r="3"/><path d="M6 21V9a9 9 0 0 0 9 9"/>',
  pr: '<circle cx="18" cy="18" r="3"/><circle cx="6" cy="6" r="3"/><path d="M13 6h3a2 2 0 0 1 2 2v7M6 9v12"/>',
  user: '<circle cx="12" cy="8" r="4"/><path d="M20 21a8 8 0 0 0-16 0"/>',
  bot: '<rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 4v4M9 13v1M15 13v1"/>',
  code: '<path d="m16 18 6-6-6-6M8 6l-6 6 6 6"/>',
  book: '<path d="M4 19V5a2 2 0 0 1 2-2h14v15H6a2 2 0 0 0-2 2Zm0 0a2 2 0 0 0 2 2h14"/>',
  package: '<path d="m21 8-9-5-9 5v8l9 5 9-5Z"/><path d="m3 8 9 5 9-5M12 13v8"/>',
  refresh: '<path d="M21 12a9 9 0 0 1-15.5 6.2L3 16M3 12a9 9 0 0 1 15.5-6.2L21 8M21 3v5h-5M3 21v-5h5"/>',
  shield: '<path d="M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6Z"/><path d="m9 12 2 2 4-4"/>',
  life: '<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="4"/><path d="m5.6 5.6 3.6 3.6M14.8 14.8l3.6 3.6M14.8 9.2l3.6-3.6M5.6 18.4l3.6-3.6"/>',
  inbox: '<path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.5 5 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.5-7a2 2 0 0 0-1.8-1H7.3a2 2 0 0 0-1.8 1Z"/>',
  flag: '<path d="M4 21V4M4 4h12l-2 4 2 4H4"/>',
  layers: '<path d="m12 3 9 5-9 5-9-5Z"/><path d="m3 13 9 5 9-5"/>',
  activity: '<path d="M22 12h-4l-3 9L9 3l-3 9H2"/>',
  copy: '<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v1"/>',
  chev: '<path d="m9 18 6-6-6-6"/>',
  search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
  moon: '<path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5Z"/>',
  auto: '<circle cx="12" cy="12" r="9"/><path d="M12 3a9 9 0 0 0 0 18Z" fill="currentColor"/>',
  sparkle: '<path d="M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2 2M16 16l2 2M6 18l2-2M16 8l2-2"/>',
  hash: '<path d="M4 9h16M4 15h16M10 3 8 21M16 3l-2 18"/>',
};

// ---------------------------------------------------------------------------------------------- utilidades de DOM

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (v === null || v === undefined || v === false) continue;
    if (k === "class") node.className = v;
    else if (k.startsWith("on") && typeof v === "function") node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v === true ? "" : v);
  }
  append(node, children);
  return node;
}

function append(node, children) {
  for (const c of children.flat(Infinity)) {
    if (c === null || c === undefined || c === false) continue;
    node.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return node;
}

function icon(name, cls) {
  const s = document.createElementNS(NS, "svg");
  s.setAttribute("class", cls ? `i ${cls}` : "i");
  s.setAttribute("viewBox", "0 0 24 24");
  s.setAttribute("aria-hidden", "true");
  s.innerHTML = ICONS[name] || ICONS.circle; // constantes deste arquivo, nunca dados externos
  return s;
}

function svgEl(tag, attrs = {}) {
  const n = document.createElementNS(NS, tag);
  for (const [k, v] of Object.entries(attrs)) if (v !== null && v !== undefined) n.setAttribute(k, v);
  return n;
}

function link(href, text, cls, ext = true) {
  return el("a", { href, class: cls, rel: ext ? "noopener" : null, target: ext && /^https?:/.test(href) ? "_blank" : null }, text);
}

function pill(text, tone = "", iconName = null) {
  return el("span", { class: `pill ${tone}`.trim() }, iconName ? icon(iconName) : null, text);
}

function na(reason) {
  return el("span", { class: "na", title: reason || null }, "não disponível");
}

function fill(id, ...nodes) {
  const host = $(id);
  if (host) host.replaceChildren(...nodes.flat(Infinity).filter(Boolean));
  return host;
}

// ---------------------------------------------------------------------------------------------- formatação

const rtf = new Intl.RelativeTimeFormat("pt-BR", { numeric: "auto" });
function ago(date) {
  const t = date instanceof Date ? date.getTime() : Date.parse(date);
  if (!Number.isFinite(t)) return "";
  const s = Math.round((t - Date.now()) / 1000);
  const a = Math.abs(s);
  if (a < 45) return "agora";
  if (a < 3600) return rtf.format(Math.round(s / 60), "minute");
  if (a < 86400) return rtf.format(Math.round(s / 3600), "hour");
  if (a < 86400 * 30) return rtf.format(Math.round(s / 86400), "day");
  if (a < 86400 * 365) return rtf.format(Math.round(s / (86400 * 30)), "month");
  return rtf.format(Math.round(s / (86400 * 365)), "year");
}

function duration(ms) {
  if (!Number.isFinite(ms) || ms < 0) return "—";
  const m = Math.round(ms / 60000);
  if (m < 60) return `${Math.max(1, m)} min`;
  const h = Math.floor(m / 60);
  if (h < 48) return m % 60 && h < 10 ? `${h} h ${m % 60} min` : `${h} h`;
  return `${Math.round(h / 24)} d`;
}

const nf = new Intl.NumberFormat("pt-BR");
const nf1 = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1, minimumFractionDigits: 1 });
const bytes = (b) => (b >= 1048576 ? `${nf1.format(b / 1048576)} MB` : `${nf.format(Math.round(b / 1024))} KB`);
const pct = (x) => `${Math.round(x * 100)}%`;
const dayKey = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const shortDay = (d) => d.toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit" });
const longDay = (d) => d.toLocaleDateString("pt-BR", { weekday: "short", day: "numeric", month: "short" });
const plural = (n, one, many) => `${nf.format(n)} ${n === 1 ? one : many}`;

function median(xs) {
  if (!xs.length) return NaN;
  const a = [...xs].sort((x, y) => x - y);
  const m = Math.floor(a.length / 2);
  return a.length % 2 ? a[m] : (a[m - 1] + a[m]) / 2;
}

function quantile(xs, q) {
  if (!xs.length) return NaN;
  const a = [...xs].sort((x, y) => x - y);
  return a[Math.min(a.length - 1, Math.floor(q * a.length))];
}

// ---------------------------------------------------------------------------------------------- interação: tooltip e toast

const tip = {
  show(evt, title, rows) {
    const t = $("tooltip");
    t.replaceChildren(el("div", { class: "tt-title" }, title),
      ...rows.map((r) => el("div", { class: "tt-row" },
        r.key ? el("span", { class: "tt-key", style: `background:${r.key}` }) : null, el("b", {}, r.value), el("span", {}, r.label))));
    t.hidden = false;
    const rect = evt.currentTarget && evt.type === "focus" ? evt.currentTarget.getBoundingClientRect() : null;
    const x = rect ? rect.left + rect.width / 2 : evt.clientX;
    const y = rect ? rect.top : evt.clientY;
    const w = t.offsetWidth;
    const h = t.offsetHeight;
    t.style.left = `${Math.max(8, Math.min(window.innerWidth - w - 8, x - w / 2))}px`;
    t.style.top = `${Math.max(8, y - h - 12)}px`;
  },
  hide() { $("tooltip").hidden = true; },
};

let toastTimer;
function toast(msg) {
  const t = $("toast");
  t.textContent = msg;
  t.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => t.classList.remove("show"), 1800);
}

function copyButton(text, what) {
  return el("button", {
    class: "copy", type: "button", "aria-label": `Copiar ${what}`, title: `Copiar ${what}`,
    onclick: () => navigator.clipboard?.writeText(text).then(() => toast(`${what} copiado`), () => toast("Não foi possível copiar")),
  }, icon("copy"));
}

// ---------------------------------------------------------------------------------------------- GitHub ao vivo (somente leitura)

// Cache curto por sessão: poupa o limite da API pública (60 req/h por IP). Conveniência local, nunca estado do Ecosystem.
async function gh(path, map = (x) => x, ttl = 300000) {
  const key = `eco-gh:${path}`;
  try {
    const c = JSON.parse(sessionStorage.getItem(key) || "null");
    if (c && Date.now() - c.t < ttl) return c.v;
  } catch (e) { /* armazenamento indisponível: segue sem cache */ }
  let r;
  try { r = await fetch(`https://api.github.com/${path}`, { headers: { Accept: "application/vnd.github+json" } }); }
  catch (e) { throw new Error("sem conexão com a API pública do GitHub"); }
  if (!r.ok) throw new Error(r.status === 403 || r.status === 429 ? "limite da API pública do GitHub atingido; tente em alguns minutos" : `GitHub respondeu ${r.status}`);
  const v = map(await r.json());
  try { sessionStorage.setItem(key, JSON.stringify({ t: Date.now(), v })); } catch (e) { /* idem */ }
  return v;
}

function repoPath(url) {
  const m = /github\.com\/([^/]+\/[^/#?]+)/.exec(url || "");
  return m ? m[1] : null;
}

// Integrações na main: commits do integrador ("Integrar PR #N", com o trailer Integration-Criticality) e merges
// feitos pelo proprietário pelo botão do GitHub. Nada é inferido além do que o próprio commit declara.
function parseIntegration(c) {
  const msg = c.message || "";
  const first = msg.split("\n")[0];
  let m = /^Integrar PR #(\d+): (.*)$/.exec(first);
  if (m) {
    const crit = /^Integration-Criticality: (\w+)(?: (\[.*\]))?/m.exec(msg);
    let classes = [];
    try { classes = crit && crit[2] ? JSON.parse(crit[2]) : []; } catch (e) { classes = []; }
    const critical = crit ? crit[1] === "critical" : false;
    return { pr: +m[1], title: m[2], date: c.date, sha: c.sha, mode: critical ? "owner" : "auto", how: critical ? "authorized" : "routine", classes };
  }
  m = /^Merge pull request #(\d+) from /.exec(first);
  if (m) {
    const title = msg.split("\n").slice(2).join(" ").trim();
    return { pr: +m[1], title: title || `PR #${m[1]}`, date: c.date, sha: c.sha, mode: "owner", how: "button", classes: [] };
  }
  return null;
}

async function loadLive(s) {
  const repo = repoPath(s.source.repository);
  if (!repo) throw new Error("repositório da projeção não reconhecido");
  const ref = s.source.ref && s.source.ref !== "HEAD" ? s.source.ref : null;
  const since = new Date(Date.now() - 14 * DAY);
  since.setHours(0, 0, 0, 0);
  const slimCommits = (arr) => arr.map((c) => ({ sha: c.sha, message: c.commit.message, date: c.commit.committer.date }));
  const slimPulls = (arr) => arr.map((p) => ({
    number: p.number, title: p.title, url: p.html_url, draft: !!p.draft, user: p.user && p.user.login,
    head: p.head && p.head.ref, fork: !!(p.head && p.base && p.head.repo && p.base.repo && p.head.repo.full_name !== p.base.repo.full_name),
    created: p.created_at, merged: p.merged_at, closed: p.closed_at, labels: (p.labels || []).map((l) => l.name),
  }));
  const slimRuns = (o) => (o.workflow_runs || []).map((r) => ({
    name: r.name, status: r.status, conclusion: r.conclusion, created: r.created_at, url: r.html_url, event: r.event,
  }));

  const commitsPath = (page) => `repos/${repo}/commits?per_page=100&page=${page}&since=${since.toISOString()}${ref ? `&sha=${encodeURIComponent(ref)}` : ""}`;
  const [c1, open, closed, runs] = await Promise.all([
    gh(commitsPath(1), slimCommits),
    gh(`repos/${repo}/pulls?state=open&per_page=50`, slimPulls),
    gh(`repos/${repo}/pulls?state=closed&sort=updated&direction=desc&per_page=60`, slimPulls),
    gh(`repos/${repo}/actions/runs?per_page=60${ref ? `&branch=${encodeURIComponent(ref)}` : ""}`, slimRuns).catch(() => null),
  ]);
  let commits = c1;
  let partial = false;
  for (let page = 2; page <= 3 && commits.length === (page - 1) * 100; page++) {
    try { commits = commits.concat(await gh(commitsPath(page), slimCommits)); }
    catch (e) { partial = true; break; } // sem esconder: os números passam a dizer "parcial"
  }
  if (commits.length >= 300) partial = true;
  const integrations = commits.map(parseIntegration).filter(Boolean)
    .sort((a, b) => Date.parse(b.date) - Date.parse(a.date));
  return { repo, since, integrations, open, closed, runs, partial, at: new Date() };
}

// ---------------------------------------------------------------------------------------------- estado da página

const state = { s: null, live: null, liveError: null, palette: [] };

function products(s) { return s.components.filter((c) => c.type === "product"); }

function criticalApprovals(s, live) {
  if (live) {
    return live.open.filter((p) => p.labels.includes("critico") && p.labels.includes("pronto-para-integrar"))
      .map((p) => ({ number: p.number, title: p.title, url: p.url, live: true }));
  }
  const a = s.pendingApprovals;
  return a && a.availability === "derived" ? a.items : null;
}

function inboxCount(s, live) {
  const approvals = criticalApprovals(s, live) || [];
  const validations = s.pendingValidations.filter((p) => p.critical !== false);
  return s.pendingDecisions.length + approvals.length + validations.length;
}

// ---------------------------------------------------------------------------------------------- hero

function renderHero() {
  const { s, live } = state;
  const n = inboxCount(s, live);
  const unknown = criticalApprovals(s, live) === null; // sem dados ao vivo nem instantâneo: não afirmar "tudo em dia"
  $("hero-title").textContent = n
    ? `${n} ${n === 1 ? "item precisa" : "itens precisam"} de você`
    : unknown ? "Nenhuma decisão ou validação esperando você." : "Tudo em dia. Nada precisa de você.";

  const prods = products(s);
  const active = prods.filter((c) => c.status === "active").length;
  const gates = s.ecosystem.gates || [];
  const focus = gates.find((g) => g.state !== "aprovado");
  const parts = [`${plural(active, "Product ativo", "Products ativos")}`];
  if (focus) parts.push(`Fase ${focus.phase} em foco`);
  if (live) {
    const recent = live.integrations;
    const auto = recent.filter((i) => i.mode === "auto").length;
    if (recent.length) parts.push(`${plural(recent.length, "integração", "integrações")} em 14 dias, ${pct(auto / recent.length)} sem você`);
  }
  $("hero-lede").textContent = n
    ? `Abaixo, cada item mostra o objeto a revisar e o que acontece quando você responde. ${parts.join(" · ")}.`
    : `Trabalho rotineiro entra sozinho; só o crítico chega até você.${unknown ? " Mudanças críticas prontas: não foi possível conferir agora." : ""} ${parts.join(" · ")}.`;

  const checks = s.ecosystem.checks;
  const checksChip = checks.availability === "derived"
    ? el(checks.url ? "a" : "span", { class: "chip", href: checks.url, target: checks.url ? "_blank" : null, rel: "noopener" },
        el("span", { class: `dot ${checks.value === "passing" ? "good" : "bad"}` }), `Checks de consistência: ${CHECK_LABELS[checks.value] || checks.value}`)
    : el("span", { class: "chip", title: checks.source }, el("span", { class: "dot warn" }), "Checks: não disponível");
  const commit = s.source.commit;
  const projChip = el("a", { class: "chip", href: commit ? `${s.source.repository}/commit/${commit}` : s.source.repository, target: "_blank", rel: "noopener" },
    icon("layers"), `Projeção ${ago(s.generatedAt)}`, commit ? el("span", { class: "mono faint" }, commit.slice(0, 7)) : null);
  const liveChip = state.live
    ? el("span", { class: "chip", title: "Lido agora da API pública do GitHub pelo seu navegador" }, el("span", { class: "dot live" }), "Ao vivo: GitHub")
    : state.liveError
      ? el("span", { class: "chip", title: state.liveError }, el("span", { class: "dot warn" }), "Ao vivo: indisponível")
      : el("span", { class: "chip" }, el("span", { class: "dot" }), "Ao vivo: carregando…");
  fill("hero-meta", checksChip, projChip, liveChip);
}

// ---------------------------------------------------------------------------------------------- precisa de você

function objectLinks(objs) {
  return el("div", { class: "actions" },
    ...objs.map((o, i) => link(o.url, [icon(i === 0 ? "external" : "book"), el("span", {}, o.title)], i === 0 ? "btn primary obj" : "btn obj")));
}

// Escolher uma alternativa abre a Issue já preenchida no GitHub; enviar a Issue confirma a escolha e uma
// automação a registra no repositório (ADR-0007). O portal em si nunca escreve nada.
function choiceLink(repo, alt) {
  const url = `${repo}/issues/new?title=${encodeURIComponent(alt.issueTitle)}&body=${encodeURIComponent(alt.issueBody)}`;
  return el("a", { href: url, class: "btn primary", target: "_blank", rel: "noopener" }, `Escolher ${alt.id}`);
}

// Aprovar ou reprovar uma validação humana segue o mesmo caminho (ADR-0008): Issue pré-preenchida, confirmada no GitHub.
function answerLink(repo, ans, label, cls, iconName) {
  const url = `${repo}/issues/new?title=${encodeURIComponent(ans.issueTitle)}&body=${encodeURIComponent(ans.issueBody)}`;
  return el("a", { href: url, class: `btn ${cls}`, target: "_blank", rel: "noopener" }, icon(iconName), label);
}

function decisionCard(d, repo) {
  return el("article", { class: "card task critical reveal" },
    el("div", { class: "kind" }, pill("Decisão", "bad", "flag"), d.blocking ? pill("bloqueia trabalho", "warn", "alert") : pill("não bloqueia"),
      el("span", { class: "faint small" }, `${d.id} · ${d.component} · aberta ${ago(d.raisedAt)}`)),
    el("h3", {}, d.title),
    el("p", { class: "q" }, d.question),
    el("p", { class: "label" }, "Objeto a revisar"),
    objectLinks(d.objects),
    el("p", { class: "label" }, "Alternativas"),
    el("ol", { class: "alts" }, ...d.alternatives.map((a) => el("li", { class: "alt" },
      el("span", { class: "alt-id" }, a.id),
      el("div", { class: "alt-body" }, el("strong", {}, a.option), el("p", {}, a.consequences), choiceLink(repo, a))))),
    d.recommendation ? el("p", { class: "rec" }, el("strong", {}, "Recomendação do agente: "), d.recommendation) : null,
    el("p", { class: "hint" }, "Ao escolher, o GitHub abre uma Issue já preenchida: toque em ", el("strong", {}, "Submit new issue"),
      " para confirmar. A decisão é registrada no repositório automaticamente e este portal é atualizado em seguida. ",
      link(d.record, "Registro das decisões")));
}

function approvalCard(a) {
  return el("article", { class: "card task critical reveal" },
    el("div", { class: "kind" }, pill("Mudança crítica pronta", "bad", "shield"), a.live ? pill("ao vivo", "info", "activity") : null),
    el("h3", {}, `PR #${a.number} · ${a.title}`),
    el("p", { class: "q muted" }, "Já testada no estado combinado com a main. O PR explica o que muda e a consequência; para autorizar, você mesmo põe a label ",
      el("code", {}, "integrar"), ". Se a main ou o PR mudarem antes, o integrador testa de novo e pede de novo."),
    el("div", { class: "actions" }, link(a.url, [icon("external"), "Abrir o PR"], "btn primary")));
}

function validationCard(p, repo) {
  return el("article", { class: `card task ${p.critical !== false ? "critical" : ""} reveal` },
    el("div", { class: "kind" }, pill("Validação humana", p.critical !== false ? "bad" : "warn", "user"),
      el("span", { class: "faint small" }, `${p.component} · ${p.taskId}`)),
    el("h3", {}, p.check),
    el("p", { class: "label" }, "Objeto a validar"),
    objectLinks([p.object]),
    el("div", { class: "actions", style: "margin-top:10px" },
      answerLink(repo, p.approve, "Aprovar", "primary", "check"),
      answerLink(repo, p.reject, "Reprovar", "", "x")),
    el("p", { class: "hint" }, "Ao responder, o GitHub abre uma Issue já preenchida: toque em ", el("strong", {}, "Submit new issue"),
      " para confirmar. Na reprovação, escreva o motivo depois de ", el("code", {}, "comment:"), ". ", link(p.record, "Registro")));
}

function renderInbox() {
  const { s, live } = state;
  const repo = s.source.repository;
  const approvals = criticalApprovals(s, live);
  const critical = s.pendingValidations.filter((p) => p.critical !== false);
  const later = s.pendingValidations.filter((p) => p.critical === false);
  const items = [
    ...s.pendingDecisions.map((d) => decisionCard(d, repo)),
    ...(approvals || []).map(approvalCard),
    ...critical.map((p) => validationCard(p, repo)),
  ];
  const nodes = [];
  if (items.length) nodes.push(el("div", { class: "inbox-list" }, items));
  else {
    nodes.push(el("div", { class: "inbox-clear reveal" },
      el("span", { class: "badge-ic" }, icon("check")),
      el("div", {},
        el("strong", {}, approvals === null ? "Nenhuma decisão ou validação crítica esperando você." : "Nenhuma decisão, aprovação ou validação crítica esperando você."),
        el("span", { class: "muted small" }, approvals === null
          ? "Mudanças críticas prontas: não disponível agora (sem dados ao vivo nem instantâneo na projeção)."
          : "Quando algo crítico precisar de você, aparece aqui com o objeto a revisar."))));
  }
  if (later.length) {
    nodes.push(el("details", { class: "later" },
      el("summary", {}, icon("chev", "chev"), `Sem pressa · ${plural(later.length, "validação não crítica", "validações não críticas")}`),
      el("p", { class: "hint", style: "margin:0 0 10px" }, "Continuam pendentes e visíveis (CI verde não substitui a validação humana), mas não bloqueiam nada."),
      el("div", { class: "inbox-list" }, later.map((p) => validationCard(p, repo)))));
  }
  fill("inbox-body", nodes);
}

// ---------------------------------------------------------------------------------------------- indicadores

function tile(labelIcon, label, value, sub, viz, opts = {}) {
  return el("article", { class: `card tile ${opts.loading ? "loading" : ""} reveal`, "aria-label": opts.aria || null },
    el("div", { class: "t-label" }, icon(labelIcon), label),
    el("div", { class: "t-value" }, value),
    sub ? el("div", { class: "t-sub" }, sub) : null,
    viz ? el("div", { class: "t-viz" }, viz) : null);
}

function splitBar(a, b) {
  const total = a + b || 1;
  const svg = svgEl("svg", { class: "spark", viewBox: "0 0 100 10", preserveAspectRatio: "none", height: "10", role: "img",
    "aria-label": `${a} sozinhas, ${b} com você` });
  const wa = (a / total) * 100;
  if (a) svg.append(svgEl("rect", { x: 0, y: 0, width: Math.max(0, wa - (b ? 1 : 0)), height: 10, rx: 2, class: "s1" }));
  if (b) svg.append(svgEl("rect", { x: Math.min(100, wa + (a ? 1 : 0)), y: 0, width: Math.max(0, 100 - wa - (a ? 1 : 0)), height: 10, rx: 2, class: "s2" }));
  svg.style.height = "10px";
  return svg;
}

function runStrip(runs) {
  const done = runs.filter((r) => r.status === "completed" && (r.conclusion === "success" || r.conclusion === "failure")).slice(0, 30).reverse();
  const w = 100 / Math.max(done.length, 1);
  const svg = svgEl("svg", { class: "spark", viewBox: "0 0 100 12", preserveAspectRatio: "none", role: "img",
    "aria-label": `${done.filter((r) => r.conclusion === "success").length} de ${done.length} execuções verdes` });
  done.forEach((r, i) => svg.append(svgEl("rect", { x: i * w + 0.4, y: 0, width: Math.max(0.6, w - 0.8), height: 12, rx: 1,
    class: r.conclusion === "success" ? "ok" : "ko" })));
  svg.style.height = "12px";
  return svg;
}

function gatesMeter(gates) {
  return el("div", { class: "meter", role: "img", "aria-label": `${gates.filter((g) => g.state === "aprovado").length} de ${gates.length} gates aprovados` },
    gates.map((g) => el("span", { class: g.state === "aprovado" ? "on" : g.state === "aguardando" ? "warn" : "" })));
}

function todayLine(ints) {
  const k = dayKey(new Date());
  const t = ints.filter((i) => dayKey(new Date(i.date)) === k);
  return t.length ? ` · hoje ${t.filter((i) => i.mode === "auto").length} de ${t.length}` : "";
}

function renderKpis() {
  const { s, live, liveError } = state;
  const gates = s.ecosystem.gates || [];
  const approved = gates.filter((g) => g.state === "aprovado").length;
  const prods = products(s);
  const withRelease = prods.filter((c) => c.release && c.release.availability === "derived").length;
  const validated = prods.filter((c) => c.validation && c.validation.state === "VALIDATED").length;

  const tiles = [];
  if (live) {
    const ints = live.integrations;
    const auto = ints.filter((i) => i.mode === "auto").length;
    tiles.push(tile("sparkle", "Integradas sem você", ints.length ? pct(auto / ints.length) : "—",
      ints.length ? `${nf.format(auto)} de ${plural(ints.length, "integração", "integrações")} em 14 dias${live.partial ? " (parcial)" : ""}${todayLine(ints)}` : "Nenhuma integração em 14 dias",
      ints.length ? splitBar(auto, ints.length - auto) : null));

    const sinceT = live.since.getTime();
    const leads = live.closed.filter((p) => p.merged && Date.parse(p.merged) >= sinceT)
      .map((p) => Date.parse(p.merged) - Date.parse(p.created));
    tiles.push(tile("clock", "Do PR à main", leads.length ? duration(median(leads)) : "—",
      leads.length ? `mediana de ${plural(leads.length, "PR", "PRs")} · p90 ${duration(quantile(leads, 0.9))}` : "Sem PRs integrados em 14 dias"));

    const q = queueStates(live.open);
    tiles.push(tile("pr", "Fila agora", nf.format(q.owner + q.back + q.queued),
      [q.owner ? `${q.owner} esperando você` : null, q.back ? `${q.back} devolvidos ao autor` : null, `${q.queued} em teste ou na fila`,
       q.ignored ? `${q.ignored} fora da fila (Dependabot, rascunho ou fork)` : null].filter(Boolean).join(" · ")));

    if (live.runs && live.runs.length) {
      const done = live.runs.filter((r) => r.status === "completed" && (r.conclusion === "success" || r.conclusion === "failure"));
      const ok = done.filter((r) => r.conclusion === "success").length;
      const lastFail = done.find((r) => r.conclusion === "failure");
      tiles.push(tile("activity", "CI da main", done.length ? pct(ok / done.length) : "—",
        done.length ? `${nf.format(ok)} de ${plural(done.length, "execução verde", "execuções verdes")}${lastFail ? ` · última falha ${ago(lastFail.created)}` : ""}` : "Sem execuções concluídas",
        done.length ? runStrip(live.runs) : null));
    } else {
      tiles.push(tile("activity", "CI da main", na("execuções do GitHub Actions não lidas"), "Execuções não disponíveis agora"));
    }
  } else {
    const loading = !liveError;
    for (const [ic, label] of [["sparkle", "Integradas sem você"], ["clock", "Do PR à main"], ["pr", "Fila agora"], ["activity", "CI da main"]]) {
      tiles.push(tile(ic, label, loading ? "000" : na(liveError), loading ? "lendo o GitHub…" : "dados ao vivo indisponíveis", null, { loading }));
    }
  }
  tiles.push(tile("flag", "Gates aprovados", el("span", {}, nf.format(approved), el("small", {}, `/ ${gates.length}`)),
    gates.find((g) => g.state !== "aprovado") ? `próximo: Fase ${gates.find((g) => g.state !== "aprovado").phase}` : "todas as fases aprovadas",
    gatesMeter(gates)));
  tiles.push(tile("package", "Products ativos", nf.format(prods.filter((c) => c.status === "active").length),
    `${withRelease} com release · ${validated} validados por humano`));
  fill("kpis", tiles);

  $("live-note").replaceChildren(live
    ? el("span", { class: "live-note" }, el("span", { class: "dot live" }), `Ao vivo · lido ${ago(live.at)}`)
    : liveError ? el("span", { class: "live-note", title: liveError }, el("span", { class: "dot warn" }), "Dados ao vivo indisponíveis") : "");
}

// ---------------------------------------------------------------------------------------------- Apps

function releaseParts(d) {
  if (!d || d.availability !== "derived" || !d.value) return null;
  const m = /^(\S+)\s*\((\d{4}-\d{2}-\d{2})(?:,\s*([^)]+))?\)/.exec(d.value);
  return m ? { tag: m[1], date: m[2], note: m[3] || null } : { tag: d.value, date: null, note: null };
}

const ARTIFACT_LABELS = { apk: "APK", "windows-installer": "Windows", appimage: "AppImage" };

function factDatum(label, d, render = (v) => v) {
  return el("div", { class: "fact" }, el("dt", {}, label),
    el("dd", {}, d && d.availability === "derived"
      ? (d.url ? link(d.url, render(d.value)) : render(d.value))
      : na(d && d.source)));
}

function mirrorFact(m) {
  const dd = el("dd", {}, el("span", { class: "faint" }, "conferindo…"));
  gh(`repos/${m.origin}/commits?per_page=30`, (arr) => arr.map((c) => c.commit.message))
    .then((msgs) => {
      const found = msgs.map((msg) => /^Ecosystem-Tree: ([0-9a-f]{40})$/m.exec(msg)).find(Boolean);
      if (found && found[1] === m.expectedTree) dd.replaceChildren(pill("em dia com o Ecosystem", "good", "check"));
      else dd.replaceChildren(pill(found ? "atrasado" : "sem sincronização registrada", "warn", "alert"), " ",
        link(m.workflowUrl, "Rodar a sincronização", "small"));
    })
    .catch((e) => dd.replaceChildren(na(String(e.message || e))));
  return el("div", { class: "fact wide" }, el("dt", {}, "Espelho de distribuição"), dd);
}

function appCard(c, i) {
  const v = VALIDATION[c.validation.state] || VALIDATION.UNKNOWN;
  const rel = releaseParts(c.release);
  const artifacts = c.artifacts || [];

  const primary = [];
  if (c.links.web) primary.push(link(c.links.web, [icon("globe"), "Abrir versão Web"], "btn primary"));
  artifacts.forEach((a, k) => primary.push(link(a.url,
    [icon("download"), `Baixar ${ARTIFACT_LABELS[a.kind] || a.kind}`, a.sizeBytes ? el("span", { class: "meta" }, bytes(a.sizeBytes)) : null],
    !c.links.web && k === 0 ? "btn primary" : "btn")));
  const secondary = [];
  if (c.validation.page) secondary.push(link(c.validation.page, [icon("shield"), "Roteiro de teste"], "btn ghost sm"));
  if (c.links.releases) secondary.push(link(c.links.releases, [icon("package"), "Releases"], "btn ghost sm"));
  if (c.links.repository) secondary.push(link(c.links.repository, [icon("code"), "Código"], "btn ghost sm"));

  const provenance = [
    el("li", {}, el("strong", {}, "Versão: "), c.version.source),
    el("li", {}, el("strong", {}, "Release: "), c.release.source),
    el("li", {}, el("strong", {}, "CI: "), c.ci.source),
    el("li", {}, el("strong", {}, "Validação: "), c.validation.source, c.validation.evidence ? [el("br"), c.validation.evidence] : null),
    ...artifacts.map((a) => el("li", {}, el("strong", {}, `${a.name}`),
      el("div", { class: "hash" }, a.sha256 ? [el("code", {}, `SHA-256 ${a.sha256}`), copyButton(a.sha256, "SHA-256")] : el("span", { class: "na" }, "SHA-256 não disponível")))),
    c.mirror ? el("li", {}, el("strong", {}, "Espelho: "), c.mirror.source) : null,
  ];

  return el("article", { class: "card app-card reveal", id: `app-${c.id}` },
    el("div", { class: "app-top" },
      el("span", { class: `glyph s${(i % 3) + 1}`, "aria-hidden": "true" }, c.name.charAt(0)),
      el("div", { class: "head" },
        el("div", { class: "title" }, el("h3", {}, c.name), pill(STATUS_LABELS[c.status] || c.status, c.status === "active" ? "good" : "warn")),
        el("p", { class: "desc" }, c.description))),
    el("dl", { class: "facts" },
      factDatum("Versão", c.version),
      el("div", { class: "fact" }, el("dt", {}, "Última release"),
        el("dd", {}, rel
          ? [c.release.url ? link(c.release.url, rel.tag) : rel.tag,
             el("span", { class: "sub" }, [rel.date ? `publicada ${ago(`${rel.date}T12:00:00Z`)}` : null, rel.note].filter(Boolean).join(" · "))]
          : na(c.release.source))),
      factDatum("CI", c.ci, (x) => CHECK_LABELS[x] || x),
      el("div", { class: "fact" }, el("dt", {}, "Validação humana"),
        el("dd", {}, pill(v.label, v.tone, v.icon), c.validation.build ? el("span", { class: "sub" }, `build ${c.validation.build}`) : null)),
      c.mirror ? mirrorFact(c.mirror) : null),
    primary.length ? el("div", { class: "actions" }, primary) : el("p", { class: "muted small" }, "Nenhum artefato ou link disponível ainda."),
    secondary.length ? el("div", { class: "actions" }, secondary) : null,
    el("details", { class: "prov" }, el("summary", {}, icon("chev", "chev"), "Fontes e checksums"), el("ul", {}, provenance)));
}

function renderApps() {
  fill("apps-grid", products(state.s).map(appCard));
}

// ---------------------------------------------------------------------------------------------- Atividade (gráfico + listas)

function queueStates(open) {
  const out = { owner: 0, back: 0, queued: 0, ignored: 0 };
  for (const p of open) {
    const st = prState(p);
    if (st.key === "owner") out.owner++;
    else if (st.key === "back") out.back++;
    else if (st.key === "ignored") out.ignored++;
    else out.queued++;
  }
  return out;
}

function prState(p) {
  if (p.labels.includes("precisa-reconciliar")) return { key: "back", label: "Voltou ao autor", tone: "bad", icon: "refresh", cls: "back" };
  if (p.labels.includes("critico") && p.labels.includes("pronto-para-integrar")) return { key: "owner", label: "Esperando você", tone: "warn", icon: "user", cls: "owner" };
  if (p.labels.includes("critico")) return { key: "crit", label: "Crítico, em teste", tone: "warn", icon: "shield", cls: "owner" };
  // Mesmas regras de elegibilidade da fila (IntegrationQueue.Plan): rascunho, fork e Dependabot ficam fora.
  if (p.draft || p.fork || (p.head || "").startsWith("dependabot/"))
    return { key: "ignored", label: p.draft ? "Rascunho, fora da fila" : p.fork ? "Fork, fora da fila" : "Dependabot, fora da fila", tone: "", icon: "pr", cls: "" };
  return { key: "queued", label: "Na fila do integrador", tone: "info", icon: "pr", cls: "auto" };
}

function buildDays(live) {
  const days = [];
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  for (let k = 13; k >= 0; k--) {
    const d = new Date(today.getTime() - k * DAY);
    days.push({ date: d, key: dayKey(d), auto: 0, owner: 0 });
  }
  const byKey = new Map(days.map((d) => [d.key, d]));
  for (const it of live.integrations) {
    const d = byKey.get(dayKey(new Date(it.date)));
    if (d) d[it.mode]++;
  }
  return days;
}

function niceMax(v) {
  if (v <= 4) return 4;
  const pow = 10 ** Math.floor(Math.log10(v));
  for (const m of [1, 2, 5, 10]) if (m * pow >= v && (m * pow) % 2 === 0) return m * pow;
  return 10 * pow;
}

function colPath(x, y, w, h, r) {
  if (h <= 0) return "";
  const rr = Math.min(r, h, w / 2);
  return `M${x},${y + h}V${y + rr}Q${x},${y} ${x + rr},${y}H${x + w - rr}Q${x + w},${y} ${x + w},${y + rr}V${y + h}Z`;
}

function drawChart(days) {
  const host = $("chart");
  const W = Math.max(280, host.clientWidth || 600);
  const H = Math.round(Math.min(240, Math.max(170, W * 0.36)));
  const m = { l: 30, r: 6, t: 10, b: 26 };
  const iw = W - m.l - m.r;
  const ih = H - m.t - m.b;
  const max = niceMax(Math.max(...days.map((d) => d.auto + d.owner), 1));
  const band = iw / days.length;
  const cw = Math.min(24, Math.max(6, band * 0.62));
  const y = (v) => m.t + ih - (v / max) * ih;

  const svg = svgEl("svg", { width: W, height: H, viewBox: `0 0 ${W} ${H}`, role: "img",
    "aria-label": `Integrações por dia nos últimos 14 dias: ${days.reduce((a, d) => a + d.auto + d.owner, 0)} no total` });
  for (const t of [0, max / 2, max]) {
    svg.append(svgEl("line", { x1: m.l, x2: W - m.r, y1: Math.round(y(t)) + 0.5, y2: Math.round(y(t)) + 0.5, class: t === 0 ? "baseline" : "gridline" }));
    const lbl = svgEl("text", { x: m.l - 8, y: y(t) + 4, "text-anchor": "end", class: "tick" });
    lbl.textContent = nf.format(t);
    svg.append(lbl);
  }
  const step = W < 420 ? 3 : 2;
  days.forEach((d, i) => {
    const cx = m.l + band * i + band / 2;
    const x = cx - cw / 2;
    const g = svgEl("g", { class: "col" });
    const band0 = svgEl("rect", { x: m.l + band * i, y: m.t, width: band, height: ih, class: "hover-band", opacity: 0 });
    g.append(band0);
    const hA = (d.auto / max) * ih;
    const hO = (d.owner / max) * ih;
    if (d.auto) g.append(svgEl("path", { d: d.owner ? `M${x},${y(0)}V${y(0) - hA}H${x + cw}V${y(0)}Z` : colPath(x, y(0) - hA, cw, hA, 4), class: "s1" }));
    if (d.owner) {
      const gap = d.auto ? 2 : 0;
      const hh = Math.max(0, hO - gap);
      g.append(svgEl("path", { d: colPath(x, y(0) - hA - gap - hh, cw, hh, 4), class: "s2" }));
    }
    if ((days.length - 1 - i) % step === 0) {
      const t = svgEl("text", { x: cx, y: H - 8, "text-anchor": "middle", class: "tick" });
      t.textContent = i === days.length - 1 ? "hoje" : shortDay(d.date);
      g.append(t);
    }
    const hit = svgEl("rect", { x: m.l + band * i, y: m.t, width: band, height: ih + 6, class: "hit", tabindex: 0,
      "aria-label": `${longDay(d.date)}: ${d.auto} sozinhas, ${d.owner} com você` });
    const show = (e) => {
      band0.setAttribute("opacity", 1);
      tip.show(e, longDay(d.date), [
        { key: "var(--series-1)", value: nf.format(d.auto), label: "sozinhas (rotina)" },
        { key: "var(--series-2)", value: nf.format(d.owner), label: "com você (crítico)" },
        { value: nf.format(d.auto + d.owner), label: "total" },
      ]);
    };
    const hide = () => { band0.setAttribute("opacity", 0); tip.hide(); };
    hit.addEventListener("pointermove", show);
    hit.addEventListener("pointerleave", hide);
    hit.addEventListener("focus", show);
    hit.addEventListener("blur", hide);
    g.append(hit);
    svg.append(g);
  });
  host.replaceChildren(svg);
}

function renderActivity() {
  const { live, liveError } = state;
  fill("chart-legend",
    el("span", {}, el("i", { style: "background:var(--series-1)" }), "Sozinhas (rotina)"),
    el("span", {}, el("i", { style: "background:var(--series-2)" }), "Com você (crítico)"));
  if (!live) {
    const msg = liveError ? [na(liveError), el("span", { class: "muted small" }, ` — ${liveError}`)] : el("span", { class: "faint" }, "Lendo o GitHub…");
    fill("chart", el("p", { class: "empty" }, msg));
    fill("queue", el("li", { class: "empty" }, liveError ? "Fila não disponível agora." : "Lendo o GitHub…"));
    fill("recent", el("li", { class: "empty" }, liveError ? "Integrações não disponíveis agora." : "Lendo o GitHub…"));
    fill("chart-table");
    return;
  }

  const days = buildDays(live);
  drawChart(days);
  const total = days.reduce((a, d) => a + d.auto + d.owner, 0);
  $("chart-sub").textContent = `Últimos 14 dias · ${plural(total, "integração", "integrações")}${live.partial ? " · parcial: parte do histórico não pôde ser lida" : ""}`;
  fill("chart-table", el("table", { class: "data-table" },
    el("thead", {}, el("tr", {}, el("th", {}, "Dia"), el("th", { class: "n" }, "Sozinhas"), el("th", { class: "n" }, "Com você"), el("th", { class: "n" }, "Total"))),
    el("tbody", {}, days.slice().reverse().map((d) => el("tr", {},
      el("td", {}, longDay(d.date)), el("td", { class: "n" }, d.auto), el("td", { class: "n" }, d.owner), el("td", { class: "n" }, d.auto + d.owner))))));

  const order = { owner: 0, back: 1, crit: 2, queued: 3 };
  const open = live.open.filter((p) => prState(p).key !== "ignored")
    .sort((a, b) => order[prState(a).key] - order[prState(b).key] || Date.parse(a.created) - Date.parse(b.created));
  const ignored = live.open.filter((p) => prState(p).key === "ignored");
  const ignoredNote = ignored.length ? [el("li", { class: "empty" },
    `${plural(ignored.length, "PR fica", "PRs ficam")} fora da fila por regra do integrador (Dependabot, rascunho ou fork): `,
    ignored.slice(0, 12).map((p, k) => [k ? ", " : "", link(p.url, `#${p.number}`)]))] : [];
  fill("queue", open.length
    ? open.slice(0, 8).map((p) => {
        const st = prState(p);
        return el("li", {},
          el("span", { class: `ic ${st.cls}` }, icon(st.icon)),
          el("div", { class: "t" }, link(p.url, `#${p.number} · ${p.title}`),
            el("span", {}, `${st.label} · aberto ${ago(p.created)}${p.user ? ` · ${p.user}` : ""}`)),
          pill(st.key === "owner" ? "você" : st.key === "back" ? "autor" : "auto", st.tone));
      }).concat(open.length > 8 ? [el("li", { class: "empty" }, `e mais ${open.length - 8}…`)] : [], ignoredNote)
    : [el("li", { class: "empty" }, "Fila vazia: nenhum PR esperando integração."), ...ignoredNote]);

  const lead = new Map(live.closed.filter((p) => p.merged).map((p) => [p.number, Date.parse(p.merged) - Date.parse(p.created)]));
  const repo = state.s.source.repository;
  fill("recent", live.integrations.length
    ? live.integrations.slice(0, 10).map((it) => {
        const how = it.how === "routine" ? "Sozinha · rotina"
          : it.how === "authorized" ? `Com você · crítica${it.classes.length ? ` (${it.classes.map((c) => CLASS_LABELS[c] || c).join(", ")})` : ""}`
          : "Com você · merge pelo botão";
        const lt = lead.get(it.pr);
        return el("li", {},
          el("span", { class: `ic ${it.mode === "auto" ? "auto" : "owner"}` }, icon(it.mode === "auto" ? "merge" : "user")),
          el("div", { class: "t" }, link(`${repo}/pull/${it.pr}`, `#${it.pr} · ${it.title}`),
            el("span", {}, [how, lt !== undefined ? `do PR à main em ${duration(lt)}` : null].filter(Boolean).join(" · "),
              " · ", link(`${repo}/commit/${it.sha}`, it.sha.slice(0, 7), "mono"))),
          el("span", { class: "when", title: new Date(it.date).toLocaleString("pt-BR") }, ago(it.date)));
      })
    : el("li", { class: "empty" }, "Nenhuma integração nos últimos 14 dias."));
}

// ---------------------------------------------------------------------------------------------- Roadmap

function renderRoadmap() {
  const { s } = state;
  const gates = s.ecosystem.gates || [];
  // Nome e progresso de cada fase vêm da projeção (derivados do ROADMAP pelo gerador; CHK-PORTAL compara), quando presentes.
  const detail = new Map(gates.filter((g) => Number.isInteger(g.done)).map((g) => [g.phase, g]));
  const focus = gates.find((g) => g.state !== "aprovado");
  const doc = s.docs.find((d) => d.path === "ROADMAP.md");
  const rl = $("roadmap-link");
  if (doc) rl.href = doc.url; else rl.remove();
  if (doc) { rl.target = "_blank"; rl.rel = "noopener"; }
  $("roadmap-sub").textContent = `Fases e gates derivados do ROADMAP (a autoridade): ${gates.filter((g) => g.state === "aprovado").length} de ${gates.length} gates aprovados.`;

  fill("phases", gates.map((g) => {
    const st = GATES[g.state] || { label: g.state, short: g.state, tone: "", icon: "circle" };
    const d = detail.get(g.phase);
    const total = d ? (d.done || 0) + (d.inProgress || 0) + (d.todo || 0) : 0;
    const isFocus = focus && focus.phase === g.phase;
    return el("li", { class: `phase ${g.state === "aprovado" ? "done" : ""} ${isFocus ? "current" : ""}`.trim(),
      "aria-current": isFocus ? "step" : null },
      el("span", { class: "n" }, `Fase ${g.phase}${isFocus ? " · em foco" : ""}`),
      d && d.name ? el("span", { class: "nm" }, d.name) : null,
      el("span", { class: "gate", title: st.label }, el("span", { class: "faint small" }, "Gate "), pill(st.short, st.tone, st.icon)),
      total ? el("span", { class: "bar-track", role: "img", "aria-label": `${d.done} de ${total} itens concluídos` },
        el("b", { style: `flex:${d.done || 0}` }), d.inProgress ? el("i", { style: `flex:${d.inProgress}` }) : null,
        el("span", { style: `flex:${d.todo || 0}` })) : null,
      total ? el("span", { class: "cnt" }, `${d.done}/${total} itens${d.inProgress ? ` · ${d.inProgress} em andamento` : ""}`) : null);
  }));
}

// ---------------------------------------------------------------------------------------------- Distribuição, recuperação, reuso, docs

const CHANNEL_KIND = { "github-release": "Release no GitHub", "github-pages": "Web (GitHub Pages)" };

function renderDistribution() {
  const { s } = state;
  const dist = s.distribution;
  $("distribution-hint").textContent = dist
    ? `Perfil ${dist.name || dist.profile} (${dist.status}). Fonte = o código em cada caminho, neste repositório; distribuição = o canal. Se o Hub estiver ausente ou quebrado, tudo continua acessível por aqui.`
    : "Nenhum perfil de distribuição registrado. Se o Hub estiver ausente ou quebrado, os produtos continuam acessíveis por aqui.";
  fill("channels", dist && dist.channels.length
    ? dist.channels.map((ch) => el("div", { class: "rowi" },
        el("div", { class: "t" }, el("strong", {}, `${ch.componentName} · ${CHANNEL_KIND[ch.kind] || ch.kind}`),
          el("span", { class: "muted" }, [`fonte ${ch.sourcePath}`, ch.role === "primary" ? "canal principal" : ch.role,
            ch.artifacts.length ? ch.artifacts.join(", ") : null, ch.updateMechanism && ch.updateMechanism !== "none" ? `atualiza por ${ch.updateMechanism}` : "sem atualização automática"].filter(Boolean).join(" · "))),
        ch.location ? link(ch.location, [icon("external"), "Abrir"], "btn sm") : na("sem localização")))
    : el("p", { class: "empty" }, "Nenhum canal registrado."));

  fill("recovery", products(s).map((c) => el("div", { class: "rowi" },
    el("div", { class: "t" }, el("strong", {}, c.name),
      el("span", { class: "muted" }, c.links.releases || c.links.web ? "Releases e artefatos acessíveis sem o Hub." : `Sem releases ainda (${STATUS_LABELS[c.status] || c.status}).`)),
    el("div", { class: "actions" },
      c.links.web ? link(c.links.web, [icon("globe"), "Web"], "btn sm") : null,
      c.links.releases ? link(c.links.releases, [icon("package"), "Releases"], "btn sm") : null,
      !c.links.web && !c.links.releases ? na("sem releases projetadas") : null))));

  const reuse = s.reuseCandidates || [];
  fill("reuse", reuse.length
    ? reuse.map((r) => el("div", { class: "rowi" },
        el("div", { class: "t" }, el("strong", { class: "mono" }, r.subject), el("span", { class: "faint small" }, ` · ${r.component}`),
          el("span", { class: "muted" }, r.rationale)),
        el("div", { class: "actions" },
          pill(r.status === "external-consumer-exists" ? "consumidor externo existe" : "possível candidato", r.status === "external-consumer-exists" ? "warn" : "info"),
          link(r.record, "handoff", "btn ghost sm"))))
    : el("p", { class: "empty" }, "Nenhum candidato registrado ainda."));
}

function renderDocs() {
  fill("docs-grid", state.s.docs.map((d) => el("a", { class: "doc", href: d.url, target: "_blank", rel: "noopener" },
    el("span", { class: "ic" }, icon("book")),
    el("span", { class: "t" }, el("strong", {}, d.title), el("span", {}, d.path)),
    icon("external", "go"))));
}

function renderFooter() {
  const { s } = state;
  const commit = s.source.commit;
  const portalDoc = s.docs.find((d) => d.path && d.path.endsWith("portal.md"));
  fill("footer",
    el("span", {}, "Projeção gerada ", el("time", { datetime: s.generatedAt, title: new Date(s.generatedAt).toLocaleString("pt-BR") }, ago(s.generatedAt)),
      " a partir de ", link(commit ? `${s.source.repository}/commit/${commit}` : s.source.repository, commit ? commit.slice(0, 7) : s.source.ref, "mono")),
    el("span", {}, "Indicadores ao vivo: API pública do GitHub, lidos pelo seu navegador e nunca gravados"),
    el("span", {}, "Não é fonte de verdade"),
    portalDoc ? link(portalDoc.url, "Como este portal funciona") : null);
}

// ---------------------------------------------------------------------------------------------- busca / paleta de comandos

function buildPalette() {
  const { s, live } = state;
  const items = [];
  const sec = (label, hash, ic) => items.push({ group: "Ir para", label, icon: ic, href: hash });
  sec("Precisa de você", "#inbox", "inbox");
  sec("Indicadores", "#pulse", "activity");
  sec("Apps", "#apps", "package");
  sec("Atividade de integração", "#activity", "merge");
  sec("Roadmap", "#roadmap", "flag");
  sec("Distribuição e recuperação", "#distribution", "life");
  sec("Documentação", "#docs", "book");
  for (const c of products(s)) {
    if (c.links.web) items.push({ group: c.name, label: `Abrir ${c.name} Web`, icon: "globe", href: c.links.web });
    for (const a of c.artifacts || []) items.push({ group: c.name, label: `Baixar ${c.name} · ${ARTIFACT_LABELS[a.kind] || a.kind}`, sub: a.name, icon: "download", href: a.url });
    if (c.validation.page) items.push({ group: c.name, label: `Roteiro de teste do ${c.name}`, sub: c.validation.build, icon: "shield", href: c.validation.page });
    if (c.links.releases) items.push({ group: c.name, label: `Releases do ${c.name}`, icon: "package", href: c.links.releases });
    if (c.links.repository) items.push({ group: c.name, label: `Código do ${c.name}`, icon: "code", href: c.links.repository });
    items.push({ group: c.name, label: `Ver ${c.name} no portal`, icon: "chev", href: `#app-${c.id}` });
  }
  for (const d of s.pendingDecisions) items.push({ group: "Precisa de você", label: d.title, sub: d.id, icon: "flag", href: "#inbox" });
  for (const p of s.pendingValidations) items.push({ group: "Precisa de você", label: p.check, sub: p.taskId, icon: "user", href: "#inbox" });
  if (live) {
    for (const p of live.open) items.push({ group: "PRs abertos", label: `#${p.number} · ${p.title}`, sub: prState(p).label, icon: "pr", href: p.url });
    for (const it of live.integrations.slice(0, 15)) items.push({ group: "Integrados recentemente", label: `#${it.pr} · ${it.title}`, sub: ago(it.date), icon: "merge", href: `${s.source.repository}/pull/${it.pr}` });
  }
  for (const d of s.docs) items.push({ group: "Documentação", label: d.title, sub: d.path, icon: "book", href: d.url });
  state.palette = items;
}

const norm = (t) => String(t || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
let palSel = 0;
let palShown = [];

function renderPalette() {
  const q = norm($("pal-q").value).split(/\s+/).filter(Boolean);
  palShown = state.palette.filter((it) => {
    const hay = norm(`${it.label} ${it.sub || ""} ${it.group}`);
    return q.every((w) => hay.includes(w));
  }).slice(0, 60);
  palSel = Math.min(palSel, Math.max(0, palShown.length - 1));
  const list = $("pal-list");
  if (!palShown.length) { list.replaceChildren(el("li", { class: "pal-empty" }, "Nada encontrado.")); return; }
  const nodes = [];
  let group = null;
  palShown.forEach((it, i) => {
    if (it.group !== group) { group = it.group; nodes.push(el("li", { class: "pal-group", role: "presentation" }, group)); }
    nodes.push(el("li", { class: "pal-item", role: "option", id: `pal-${i}`, "aria-selected": i === palSel ? "true" : "false",
      onclick: () => go(it), onpointermove: () => { if (palSel !== i) { palSel = i; mark(); } } },
      icon(it.icon), el("span", {}, it.label), it.sub ? el("span", { class: "sub" }, it.sub) : null));
  });
  list.replaceChildren(...nodes);
  $("pal-q").setAttribute("aria-activedescendant", `pal-${palSel}`);
}

function mark() {
  document.querySelectorAll(".pal-item").forEach((n) => n.setAttribute("aria-selected", n.id === `pal-${palSel}` ? "true" : "false"));
  const cur = $(`pal-${palSel}`);
  if (cur) cur.scrollIntoView({ block: "nearest" });
  $("pal-q").setAttribute("aria-activedescendant", `pal-${palSel}`);
}

function go(it) {
  closePalette();
  if (it.href.startsWith("#")) {
    const t = document.querySelector(it.href);
    if (t) { t.scrollIntoView({ behavior: "smooth", block: "start" }); history.replaceState(null, "", it.href); }
  } else window.open(it.href, "_blank", "noopener");
}

function openPalette() {
  const d = $("palette");
  if (d.open) return;
  buildPalette();
  $("pal-q").value = "";
  palSel = 0;
  renderPalette();
  if (typeof d.showModal === "function") d.showModal(); else d.setAttribute("open", "");
  $("pal-q").focus();
}

function closePalette() {
  const d = $("palette");
  if (!d.open) return;
  if (typeof d.close === "function") d.close(); else d.removeAttribute("open");
}

function setupPalette() {
  const isMac = /Mac|iPhone|iPad/.test(navigator.platform || navigator.userAgent);
  $("kbd-hint").textContent = isMac ? "⌘K" : "Ctrl K";
  $("open-palette").addEventListener("click", openPalette);
  $("pal-q").addEventListener("input", () => { palSel = 0; renderPalette(); });
  $("pal-q").addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); palSel = Math.min(palShown.length - 1, palSel + 1); mark(); }
    else if (e.key === "ArrowUp") { e.preventDefault(); palSel = Math.max(0, palSel - 1); mark(); }
    else if (e.key === "Enter" && palShown[palSel]) { e.preventDefault(); go(palShown[palSel]); }
    else if (e.key === "Escape") { e.preventDefault(); closePalette(); } // campo de busca limparia o texto em vez de fechar
  });
  $("palette").addEventListener("click", (e) => { if (e.target === $("palette")) closePalette(); });
  document.addEventListener("keydown", (e) => {
    const typing = /^(INPUT|TEXTAREA|SELECT)$/.test(document.activeElement && document.activeElement.tagName);
    if ((e.key === "k" || e.key === "K") && (e.metaKey || e.ctrlKey)) { e.preventDefault(); $("palette").open ? closePalette() : openPalette(); }
    else if (e.key === "/" && !typing && !$("palette").open) { e.preventDefault(); openPalette(); }
  });
}

// ---------------------------------------------------------------------------------------------- tema e navegação

function setupTheme() {
  const btn = $("theme");
  const modes = ["auto", "light", "dark"];
  const labels = { auto: "Tema: automático", light: "Tema: claro", dark: "Tema: escuro" };
  const iconOf = { auto: "auto", light: "sun", dark: "moon" };
  const current = () => document.documentElement.dataset.theme || "auto";
  const paint = () => { const m = current(); btn.replaceChildren(icon(iconOf[m])); btn.setAttribute("aria-label", labels[m]); btn.title = labels[m]; };
  btn.addEventListener("click", () => {
    const next = modes[(modes.indexOf(current()) + 1) % modes.length];
    if (next === "auto") delete document.documentElement.dataset.theme; else document.documentElement.dataset.theme = next;
    try { if (next === "auto") localStorage.removeItem("eco-theme"); else localStorage.setItem("eco-theme", next); } catch (e) { /* sem armazenamento: vale só nesta visita */ }
    paint();
    toast(labels[next]);
  });
  paint();
}

function setupNav() {
  const links = [...document.querySelectorAll("[data-nav] a")];
  if (!("IntersectionObserver" in window)) return;
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue;
      const id = `#${e.target.id}`;
      links.forEach((a) => a.setAttribute("aria-current", a.getAttribute("href") === id ? "true" : "false"));
    }
  }, { rootMargin: "-45% 0px -50% 0px" });
  for (const id of ["inbox", "pulse", "apps", "activity", "roadmap", "distribution", "docs"]) {
    const n = $(id);
    if (n) io.observe(n);
  }
}

// ---------------------------------------------------------------------------------------------- ciclo

function renderAll() {
  renderHero();
  renderInbox();
  renderKpis();
  renderActivity();
}

function render(s) {
  state.s = s;
  renderHero();
  renderInbox();
  renderKpis();
  renderApps();
  renderActivity();
  renderRoadmap();
  renderDistribution();
  renderDocs();
  renderFooter();

  loadLive(s)
    .then((live) => { state.live = live; state.liveError = null; })
    .catch((e) => { state.live = null; state.liveError = String(e && e.message ? e.message : e); })
    .finally(renderAll);

  let lastW = $("chart").clientWidth;
  if ("ResizeObserver" in window) {
    new ResizeObserver(() => {
      const w = $("chart").clientWidth;
      if (state.live && Math.abs(w - lastW) > 8) { lastW = w; drawChart(buildDays(state.live)); }
    }).observe($("chart"));
  }
  // O tempo relativo envelhece: atualiza os rótulos sem refazer leituras.
  setInterval(() => { renderHero(); renderFooter(); }, 60000);
}

function renderError(err) {
  $("hero-title").textContent = "Projeção indisponível";
  $("hero-lede").textContent = "Não foi possível carregar o estado do Ecosystem.";
  fill("inbox-body", el("div", { class: "callout error" }, icon("alert"),
    el("p", {}, "Não foi possível carregar data/ecosystem-status.json (", String(err), "). Localmente, gere-a com: ",
      el("code", {}, "dotnet run site/generator/GenerateStatus.cs"))));
}

setupTheme();
setupPalette();
setupNav();
fetch("data/ecosystem-status.json", { cache: "no-store" })
  .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
  .then(render)
  .catch(renderError);
