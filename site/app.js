// Portal do Ecosystem — renderiza a projeção data/ecosystem-status.json (ADR-0005).
// Nenhum dado canônico é escrito aqui: tudo vem da projeção gerada a partir das fontes oficiais.
"use strict";

const STATUS_LABELS = {
  planned: "planejado",
  "not-migrated": "fora do monorepo (não migrado)",
  migrating: "em migração",
  active: "ativo",
  deprecated: "obsoleto",
  archived: "arquivado",
};

// Estados de validação de build: docs/governance/definition-of-done.md §4.
const VALIDATION_LABELS = {
  UNKNOWN: "sem registro",
  IMPLEMENTED: "implementado",
  AUTOMATED_VERIFIED: "verificado automaticamente",
  HUMAN_VALIDATION_PENDING: "validação humana pendente",
  VALIDATED: "validado por humano",
};

const CHECK_LABELS = { passing: "passando", failing: "falhando" };

function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined) continue;
    if (k === "class") node.className = v;
    else node.setAttribute(k, v);
  }
  for (const c of children.flat()) {
    if (c === null || c === undefined) continue;
    node.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return node;
}

function link(href, text, cls) {
  return el("a", { href, class: cls, rel: "noopener" }, text);
}

// Um dado projetado: valor derivado ou "não disponível", sempre com a fonte visível.
function datum(label, d, format = (v) => v) {
  const value = d.availability === "derived"
    ? el("span", { class: "value" }, d.url ? link(d.url, format(d.value)) : format(d.value))
    : el("span", { class: "na" }, "não disponível");
  return el("div", { class: "row" },
    el("dt", {}, label),
    el("dd", {}, value, el("small", { class: "src" }, d.source)));
}

function validationRow(v) {
  return el("div", { class: "row" },
    el("dt", {}, "Validação humana"),
    el("dd", {},
      el("span", { class: `badge v-${v.state}` }, VALIDATION_LABELS[v.state] || v.state),
      v.evidence ? el("small", { class: "src" }, v.evidence) : null,
      el("small", { class: "src" }, v.source)));
}

function appCard(c) {
  const actions = el("div", { class: "actions" });
  if (c.links.web) actions.append(link(c.links.web, "Abrir versão Web", "btn primary"));
  if (c.links.releases) actions.append(link(c.links.releases, "Ver releases", "btn"));
  if (c.links.repository) actions.append(link(c.links.repository, "Repositório", "btn"));

  return el("article", { class: "card" },
    el("header", {},
      el("h3", {}, c.name),
      el("span", { class: `status s-${c.status}` }, STATUS_LABELS[c.status] || c.status)),
    el("p", { class: "desc" }, c.description),
    el("dl", {},
      datum("Versão", c.version),
      datum("Última release", c.release),
      datum("CI (automático)", c.ci, (v) => CHECK_LABELS[v] || v),
      validationRow(c.validation)),
    actions.childElementCount ? actions : el("p", { class: "hint" }, "Nenhum artefato ou link disponível ainda."));
}

function render(s) {
  const phase = s.ecosystem.phase.replace(/^phase-/, "Fase ");
  const checks = s.ecosystem.checks;
  const meta = document.getElementById("meta");
  meta.replaceChildren(
    `${phase} · checks de consistência: `,
    checks.availability === "derived"
      ? (checks.url ? link(checks.url, CHECK_LABELS[checks.value] || checks.value) : CHECK_LABELS[checks.value] || checks.value)
      : el("span", { class: "na" }, "não disponível"));

  const products = s.components.filter((c) => c.type === "product");
  document.getElementById("apps").replaceChildren(...products.map(appCard));

  const val = document.getElementById("validations");
  val.replaceChildren(...(s.pendingValidations.length
    ? s.pendingValidations.map((p) => el("li", {},
        el("span", { class: "badge v-HUMAN_VALIDATION_PENDING" }, VALIDATION_LABELS[p.state]),
        " ", el("strong", {}, `${p.component} · ${p.taskId}`), el("br"),
        p.check, " — ", link(p.record, "registro")))
    : [el("li", { class: "hint" }, "Nenhuma validação humana pendente registrada.")]));

  const rec = document.getElementById("recovery");
  rec.replaceChildren(...s.components.filter((c) => c.type === "product").map((c) => el("li", {},
    el("strong", {}, c.name), ": ",
    c.links.releases ? link(c.links.releases, "releases") : el("span", { class: "na" }, "sem releases"),
    c.links.web ? [" · ", link(c.links.web, "Web")] : null,
    c.links.repository ? null : el("span", { class: "hint" }, ` (${STATUS_LABELS[c.status] || c.status})`))));

  document.getElementById("docs").replaceChildren(...s.docs.map((d) => el("li", {}, link(d.url, d.title))));

  const commit = s.source.commit;
  document.getElementById("footer").replaceChildren(
    "Projeção gerada em ", el("time", { datetime: s.generatedAt }, new Date(s.generatedAt).toLocaleString()),
    " a partir de ", link(commit ? `${s.source.repository}/commit/${commit}` : s.source.repository, commit ? commit.slice(0, 7) : s.source.ref),
    " · não é fonte de verdade.");
}

function renderError(err) {
  document.getElementById("meta").replaceChildren(el("span", { class: "na" }, "Projeção indisponível"));
  document.querySelector("main").prepend(el("p", { class: "notice error" },
    "Não foi possível carregar data/ecosystem-status.json (", String(err), "). ",
    "Localmente, gere-a com: dotnet run site/generator/GenerateStatus.cs"));
}

fetch("data/ecosystem-status.json", { cache: "no-store" })
  .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.json(); })
  .then(render)
  .catch(renderError);
