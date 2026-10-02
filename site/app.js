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
      v.page ? el("small", { class: "src" }, link(v.page, `Página de validação do build ${v.build}`)) : null,
      el("small", { class: "src" }, v.source)));
}

function artifactRows(c) {
  return (c.artifacts || []).map((a) => {
    const size = a.sizeBytes ? ` · ${(a.sizeBytes / 1048576).toFixed(1)} MB` : "";
    return el("div", { class: "row" },
      el("dt", {}, a.kind === "apk" ? "APK" : a.kind === "windows-installer" ? "Instalador Windows" : "AppImage"),
      el("dd", {}, el("span", { class: "value" }, link(a.url, a.name + size)),
        el("small", { class: "src" }, a.sha256 ? `SHA-256 ${a.sha256}` : "SHA-256 não disponível")));
  });
}

// Espelho de distribuição: compara ao vivo (API pública do GitHub) a última sincronização da origem com a árvore esperada.
function mirrorRow(m) {
  const value = el("span", { class: "na" }, "conferindo…");
  const row = el("div", { class: "row" },
    el("dt", {}, "Espelho de distribuição"),
    el("dd", {}, value, el("small", { class: "src" }, m.source)));
  fetch(`https://api.github.com/repos/${m.origin}/commits?per_page=30`)
    .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
    .then((commits) => {
      const found = commits.map((c) => /^Ecosystem-Tree: ([0-9a-f]{40})$/m.exec(c.commit.message)).find(Boolean);
      if (found && found[1] === m.expectedTree) {
        value.replaceChildren(el("span", { class: "value" }, "em dia com o Ecosystem"));
      } else {
        value.replaceChildren(el("span", { class: "badge v-HUMAN_VALIDATION_PENDING" }, found ? "atrasado" : "sem sincronização registrada"),
          " ", link(m.workflowUrl, "Rodar a sincronização", "btn"));
      }
    })
    .catch(() => value.replaceChildren(el("span", { class: "na" }, "não foi possível conferir agora")));
  return row;
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
      ...artifactRows(c),
      c.mirror ? mirrorRow(c.mirror) : null,
      datum("CI (automático)", c.ci, (v) => CHECK_LABELS[v] || v),
      validationRow(c.validation)),
    actions.childElementCount ? actions : el("p", { class: "hint" }, "Nenhum artefato ou link disponível ainda."));
}

function objectLinks(objs) {
  return el("div", { class: "actions" },
    ...objs.map((o, i) => link(o.url, o.title, i === 0 ? "btn primary" : "btn")));
}

// Escolher uma alternativa abre a Issue já preenchida no GitHub; enviar a Issue confirma a escolha e uma
// automação a registra no repositório (ADR-0007). O portal em si nunca escreve nada.
function choiceLink(repo, alt) {
  const url = `${repo}/issues/new?title=${encodeURIComponent(alt.issueTitle)}&body=${encodeURIComponent(alt.issueBody)}`;
  return el("a", { href: url, class: "btn primary choose", target: "_blank", rel: "noopener" }, `Escolher ${alt.id}`);
}

// Aprovar ou reprovar uma validação humana segue o mesmo caminho (ADR-0008): Issue pré-preenchida, confirmada no GitHub.
function answerLink(repo, ans, label, cls) {
  const url = `${repo}/issues/new?title=${encodeURIComponent(ans.issueTitle)}&body=${encodeURIComponent(ans.issueBody)}`;
  return el("a", { href: url, class: `btn ${cls} choose`, target: "_blank", rel: "noopener" }, label);
}

function decisionCard(d, repo) {
  return el("article", { class: "card decision" },
    el("header", {},
      el("h3", {}, d.title),
      el("span", { class: `badge ${d.blocking ? "v-HUMAN_VALIDATION_PENDING" : ""}` }, d.blocking ? "bloqueia trabalho" : "não bloqueia")),
    el("p", { class: "meta" }, `${d.id} · ${d.component} · aberta em ${d.raisedAt}`),
    el("p", { class: "question" }, el("strong", {}, "Decisão a tomar: "), d.question),
    el("p", { class: "label" }, "Objeto a revisar"),
    objectLinks(d.objects),
    el("p", { class: "label" }, "Alternativas"),
    el("ol", { class: "alts" }, ...d.alternatives.map((a) => el("li", { class: "alt" },
      el("span", { class: "alt-id" }, a.id),
      el("div", { class: "alt-body" },
        el("strong", {}, a.option),
        el("p", {}, a.consequences),
        choiceLink(repo, a))))),
    d.recommendation ? el("p", { class: "rec" }, el("strong", {}, "Recomendação do agente: "), d.recommendation) : null,
    el("p", { class: "hint" }, "Ao escolher, o GitHub abre uma Issue já preenchida: toque em ", el("strong", {}, "Submit new issue"),
      " para confirmar. A decisão é registrada no repositório automaticamente e este portal é atualizado em seguida."),
    el("p", { class: "hint" }, link(d.record, "Registro das decisões")));
}

function render(s) {
  const checks = s.ecosystem.checks;
  const meta = document.getElementById("meta");
  const approved = (s.ecosystem.gates || []).filter((g) => g.state === "aprovado").map((g) => g.phase);
  meta.replaceChildren(
    `Gates aprovados (ROADMAP): ${approved.length ? approved.map((n) => `Fase ${n}`).join(", ") : "nenhum"} · checks de consistência: `,
    checks.availability === "derived"
      ? (checks.url ? link(checks.url, CHECK_LABELS[checks.value] || checks.value) : CHECK_LABELS[checks.value] || checks.value)
      : el("span", { class: "na" }, "não disponível"));

  const products = s.components.filter((c) => c.type === "product");
  document.getElementById("apps").replaceChildren(...products.map(appCard));

  const dec = document.getElementById("decisions");
  dec.replaceChildren(...(s.pendingDecisions.length
    ? s.pendingDecisions.map((d) => decisionCard(d, s.source.repository))
    : [el("p", { class: "hint" }, "Nenhuma decisão pendente registrada.")]));

  const validationItem = (p) => el("li", {},
    el("span", { class: "badge v-HUMAN_VALIDATION_PENDING" }, VALIDATION_LABELS[p.state]),
    " ", el("strong", {}, `${p.component} · ${p.taskId}`), el("br"),
    p.check, el("p", { class: "label" }, "Objeto a validar"),
    objectLinks([p.object]),
    el("div", { class: "actions" },
      answerLink(s.source.repository, p.approve, "Aprovar", "primary"),
      answerLink(s.source.repository, p.reject, "Reprovar", "")),
    el("p", { class: "hint" }, "Ao responder, o GitHub abre uma Issue já preenchida: toque em ", el("strong", {}, "Submit new issue"),
      " para confirmar. Na reprovação, escreva o motivo depois de ", el("code", {}, "comment:"), "."),
    el("p", { class: "hint" }, link(p.record, "registro")));
  const critical = s.pendingValidations.filter((p) => p.critical !== false);
  const later = s.pendingValidations.filter((p) => p.critical === false);
  document.getElementById("validations").replaceChildren(...(critical.length
    ? critical.map(validationItem)
    : [el("li", { class: "hint" }, "Nenhuma validação crítica pendente.")]));
  document.getElementById("validations-later").replaceChildren(...(later.length
    ? later.map(validationItem)
    : [el("li", { class: "hint" }, "Nenhuma.")]));

  const approvals = s.pendingApprovals || { availability: "not-available", items: [] };
  document.getElementById("approvals").replaceChildren(...(approvals.availability !== "derived"
    ? [el("li", {}, el("span", { class: "na" }, "não disponível"))]
    : approvals.items.length
      ? approvals.items.map((a) => el("li", {}, el("strong", {}, `PR #${a.number}`), " · ", link(a.url, a.title),
          el("p", { class: "hint" }, "Leia no PR o que muda e a consequência; autorize com a label integrar, posta por você.")))
      : [el("li", { class: "hint" }, "Nenhuma mudança crítica aguardando você.")]));

  const dist = s.distribution;
  document.getElementById("distribution-hint").textContent = dist
    ? `Perfil ${dist.status} (${dist.profile}); decisões: ${dist.decisions.join(", ")}. Fonte = código em cada caminho abaixo, no Ecosystem; distribuição = canal listado. Derivado; nada aqui é alterado pelo portal.`
    : "Nenhum perfil de distribuição registrado.";
  document.getElementById("distribution").replaceChildren(...(dist ? dist.channels : []).map((ch) => el("li", {},
    el("strong", {}, ch.componentName), ` · fonte ${ch.sourcePath} · canal ${ch.kind} (${ch.role}) · `,
    ch.location ? link(ch.location, ch.location) : el("span", { class: "na" }, "sem localização"),
    ch.artifacts.length ? ` · ${ch.artifacts.join(", ")}` : "", ch.updateMechanism ? ` · atualização: ${ch.updateMechanism}` : "")));

  const reuse = document.getElementById("reuse");
  reuse.replaceChildren(...((s.reuseCandidates || []).length
    ? s.reuseCandidates.map((r) => el("li", {},
        el("strong", {}, r.subject), ` · ${r.component} · `,
        el("span", { class: "badge" }, r.status === "external-consumer-exists" ? "consumidor externo existe" : "possível candidato"),
        r.status === "external-consumer-exists" ? ` · consumidores: ${r.consumers.join(", ")} · Extraction Review: ${r.extractionReview}` : "",
        el("br"), r.rationale, " ", link(r.record, "handoff")))
    : [el("li", { class: "hint" }, "Nenhum candidato registrado ainda.")]));

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
