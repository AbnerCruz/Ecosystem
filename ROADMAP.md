# ROADMAP.md — Ecosystem

> **Autoridade:** abaixo de `MANIFEST.md`, ADRs, `ARCHITECTURE.md` e SPECs (MANIFEST §24). Fases e gates derivam de MANIFEST §46.
> Este roadmap cobre o **Ecosystem** (fundação, plataforma, Hub). Os roadmaps próprios de Lunet2D e Urbe continuam válidos e não são substituídos (MANIFEST §47).

## Convenções

- IDs de tarefa: `P<fase>-<n>` — estáveis (NN-019); nunca reutilizar.
- `[x]` concluído com evidência · `[~]` verificado automaticamente, **aguardando validação humana/revisão** · `[ ]` aberto.
- Um item nunca é marcado `[x]` enquanto depender de validação humana (NN-017); `CHK-ROADMAP` fiscaliza parte disso.
- Evidência de cada item concluído: handoff em `docs/governance/handoffs/`.

## Próxima tarefa

**P1-1 e P1-2 — inventário de Lunet2D e Urbe** (`docs/migration/inventory-<id>.md`, a partir do [template](docs/migration/inventory-template.md)).

*Por quê:* MANIFEST §44.1 e NN-012 exigem documentar origem, branches, tags, releases, workflows, Pages, configuração e estratégia de histórico **antes** de qualquer importação. O inventário é somente leitura, não depende de decisão pendente e produz os fatos que DEC-0002 e DEC-0004 precisam. Em paralelo, o proprietário deve responder às decisões pendentes em [`docs/governance/decisions.json`](docs/governance/decisions.json).

---

## Fase 0 — Constituição

Objetivo: monorepo com documentos normativos, `ecosystem.json`, processo de ADR, comunicação humano↔máquina e máquina↔máquina, boundaries e CI mínimo de consistência.

- [x] P0-1 — `MANIFEST.md` incluído sem alterações.
- [x] P0-2 — `AGENTS.md` com protocolo e reprodução operacional de todas as `NN-XXX` (`CHK-AGENTS-NN`).
- [x] P0-3 — `ARCHITECTURE.md` inicial sem decisões inventadas.
- [x] P0-4 — `ecosystem.json` inicial + schema (ADR-0002).
- [x] P0-5 — Processo de ADR (ADR-0001).
- [x] P0-6 — Comunicação humano↔máquina e máquina↔máquina, handoff schema, registro de decisões.
- [x] P0-7 — Matriz de enforcement legível por máquina, validada contra o manifesto.
- [x] P0-8 — Checks de consistência em C# com self-test (ADR-0003).
- [x] P0-9 — CI mínimo de consistência no GitHub Actions (primeira execução verde: run #1, commit 912366d).
- [x] P0-10 — Estratégia de migração documentada antes de qualquer importação.
- [~] P0-11 — Ratificação dos ADRs de fundação pelo proprietário (DEC-0001).

Portal web (ADD-0001, ADR-0005, [`docs/architecture/portal.md`](docs/architecture/portal.md)):

- [x] P0-12 — `site/` com página simples, mobile-first, mostrando Ecosystem, Lunet2D, Urbe e Hub e links para a documentação canônica; dados não automatizados marcados como "não disponível".
- [x] P0-13 — Contrato da projeção `ecosystem-status/1`, gerador em C# e `CHK-PORTAL` contra divergência e dados canônicos escritos à mão.
- [x] P0-14 — Portal publicado por `pages.yml`; API confirmou `build_type=workflow`, artefato foi validado e smoke test confirmou o `site/index.html` na URL pública (run 36758554559).
- [~] P0-15 — Validação humana do portal no celular (layout, toque, legibilidade) — NN-017.

**Gate:** um agente novo consegue entrar no repositório e compreender corretamente o produto, a autoridade documental e o processo de trabalho sem depender de uma conversa anterior.
*Estado do gate:* aguardando revisão humana ou de agente independente (não pode ser autodeclarado pelo agente fundador).

## Fase 1 — Inventário e migração

Objetivo: auditar Lunet2D e Urbe, mapear históricos e workflows, importar preservando histórico, restaurar builds/testes/releases.

- [ ] P1-1 — Inventário do Lunet2D (`docs/migration/inventory-lunet2d.md`).
- [ ] P1-2 — Inventário do Urbe (`docs/migration/inventory-urbe.md`).
- [ ] P1-3 — Plano de importação por produto, após DEC-0002 e DEC-0004.
- [ ] P1-4 — Importação do Lunet2D preservando histórico (PR de escopo restrito, NN-013).
- [ ] P1-5 — Importação do Urbe preservando histórico (PR de escopo restrito, NN-013).
- [ ] P1-6 — Restaurar build/testes/workflows/releases de cada produto; pipelines seletivos por path (NN-014).
- [ ] P1-7 — Architecture tests sobre referências reais de código (NN-002, NN-003).
- [ ] P1-8 — Auditoria pós-migração: commits/tags da origem presentes (NN-012).
- [ ] P1-9 — Portal: versão, última release, APK, checksum e release notes de cada produto derivados das releases do GitHub (nunca digitados).
- [ ] P1-10 — Urbe Web: decidir, com base no inventário P1-2, como o Urbe Web é publicado no monorepo sem quebrar a publicação atual, e expor rota previsível no portal.
- [ ] P1-11 — Registros canônicos de validação por build (contrato + estados de `definition-of-done.md` §4) e páginas `/testing/<componente>/<build>/` geradas deles.

**Gate:** Lunet2D e Urbe vivem no monorepo sem regressão conhecida e continuam possuindo lifecycles próprios.

## Fase 2 — Contracts e Registry

Objetivo: formalizar `ComponentManifest`, `Capability`, provider/consumer, versionamento e permission model; registry inicial; testes de boundary.

**Gate:** um componente pode declarar uma capability, outro pode descobri-la e a compatibilidade pode ser validada sem dependência direta entre produtos.

## Fase 3 — Hub read-only

Objetivo: Hub inicial em C#; lê `ecosystem.json`; mostra Lunet2D e Urbe; integra leitura do GitHub; Past / Now / Next; CI, releases, branches, PRs e tarefas.

- [ ] P3-1 — Portal lista as releases do Hub para instalação e recuperação (o portal continua existindo como mecanismo independente).

**Gate:** no celular, o proprietário abre o Hub e compreende o estado atual dos dois produtos sem abrir GitHub manualmente. *(requer validação humana em aparelho)*

## Fase 4 — Launcher e Updates

Objetivo: detectar versões, listar releases, baixar artefatos, validar integridade, conduzir instalação/atualização respeitando o modelo de segurança da plataforma, abrir produto instalado.

**Gate:** o usuário utiliza o Hub como entrada para instalar/atualizar/abrir Lunet2D e Urbe dentro dos limites da plataforma. *(requer validação humana em aparelho)*

## Fase 5 — Capability Runtime

Objetivo: IPC; command/event/request; capability discovery local; lifecycle; permissions; Host API; Tool hosting.

**Gate:** uma Tool simples funciona standalone no Hub e embutida em outro Host sem código específico para aquele Host.

## Fase 6 — Agent Workspace compartilhado

Objetivo: mapear IA do Urbe e Agentic Workspace do Lunet; definir Agent Runtime comum; implementar/adaptar contratos; preservar integrações específicas. Sem reescrita cega (MANIFEST §39).

**Gate:** o mesmo modelo conceitual de Agent Workspace opera com contexto do Urbe ou do Lunet por tools/capabilities diferentes.

## Fase 7 — Primeira Tool real compartilhada

Objetivo: extrair uma ferramenta com boundary claro (ex.: Sprite Studio ou Editor), após auditoria.

**Gate:** a ferramenta funciona standalone e integrada sem duplicação de implementação.

---

## Revisões periódicas

- Dívida e custo de integração da plataforma (NN-020): ao fim de cada fase.
