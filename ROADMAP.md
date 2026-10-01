# ROADMAP.md — Ecosystem

> **Autoridade:** abaixo de `MANIFEST.md`, ADRs, `ARCHITECTURE.md` e SPECs (MANIFEST §24). Fases e gates derivam de MANIFEST §46.
> Este roadmap cobre o **Ecosystem** (fundação, plataforma, Hub). Os roadmaps próprios de Lunet2D e Urbe continuam válidos e não são substituídos (MANIFEST §47).

## Convenções

- IDs de tarefa: `P<fase>-<n>` — estáveis (NN-019); nunca reutilizar.
- `[x]` concluído com evidência · `[~]` verificado automaticamente, **aguardando validação humana/revisão** · `[ ]` aberto.
- Um item nunca é marcado `[x]` enquanto depender de validação humana (NN-017); `CHK-ROADMAP` fiscaliza parte disso.
- Evidência de cada item concluído: handoff em `docs/governance/handoffs/`.
- **Estado de um gate** (autoridade: este arquivo): uma linha `*Estado do gate:* **aprovado**`, `**aguardando**` ou `**não iniciado**` (padrão quando a linha não existe) logo depois de cada `**Gate:**`. `CHK-STATE-CONSISTENCY` exige que um gate `aprovado` não tenha itens da fase abertos (`[ ]` ou `[~]`) e que o portal projete os mesmos gates (`CHK-PORTAL`); a fase não é copiada em `ecosystem.json` (DEC-0019-A).
- **Estado vivo × caixa:** `[x]` exige que a Issue da tarefa (título começando pelo ID) esteja fechada e sem `state:` incompatível; `[~]` exige uma Issue aberta com `state:` ≠ `done` (DEC-0003; verificado quando há instantâneo das Issues).

## Próxima tarefa

**Fase 2 — Contracts e Registry: abrir o plano da fase** (tarefas `P2-x`, ainda não iniciada). Antes dela e em paralelo a ela, o proprietário pode **refatorar os aplicativos** (passo 10) pelas regras do ADR-0009 `Aceito` e do guia [`refactoring.md`](docs/architecture/refactoring.md); a ordem sugerida está em [`candidates.md`](docs/architecture/candidates.md).

*Por quê:* a Fase 1 está encerrada (gate aprovado em 2026-10-01); a Fase 2 é o que habilita extrações para o Ecosystem (contratos de Capability e Context, Distribution Profile, registry).

*Como ler o estado:* fase e gates = este arquivo (linha `*Estado do gate:*` de cada fase); tarefas em andamento = Issues (`state:<estado>`); resultado e evidência = handoffs; decisões e validações pendentes = portal.

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
- [x] P0-11 — Ratificação dos ADRs de fundação pelo proprietário (DEC-0001, aprovada em 2026-09-30 — ADD-0003).

Portal web (ADD-0001, ADR-0005, [`docs/architecture/portal.md`](docs/architecture/portal.md)):

- [x] P0-12 — `site/` com página simples, mobile-first, mostrando Ecosystem, Lunet2D, Urbe e Hub e links para a documentação canônica; dados não automatizados marcados como "não disponível".
- [x] P0-13 — Contrato da projeção `ecosystem-status/1`, gerador em C# e `CHK-PORTAL` contra divergência e dados canônicos escritos à mão.
- [x] P0-14 — Portal publicado por `pages.yml`; API confirmou `build_type=workflow`, artefato foi validado e smoke test confirmou o `site/index.html` na URL pública (run 36758554559).
- [x] P0-15 — Validação humana do portal no celular (layout, toque, legibilidade) — NN-017. **Aprovada pelo proprietário em 2026-10-01** ([ADD-0005](docs/governance/addenda/ADD-0005-aprovacao-p0-15-e-validacao-pelo-portal.md); handoff `HO-20261001-p0-15-aprovado`).

Alinhamento arquitetural pré-migração — Product Shells, distribuição independente e plataforma própria (ADD-0002, DEC-0006, ADR-0006). Somente conceito, documentação, governança e fiscalização; nenhum código, diretório ou Service criado; nenhum dado de Lunet2D/Urbe importado:

- [x] P0-16 — Persistir a decisão do proprietário: `docs/governance/addenda/ADD-0002-…` (texto integral) e DEC-0006.
- [x] P0-17 — Atualizar `MANIFEST.md`: NN-023 (aditamento; nenhuma invariante alterada ou renumerada) e [`manifest-changelog.md`](docs/governance/manifest-changelog.md).
- [x] P0-18 — Atualizar `AGENTS.md` (NN-023 e ponteiro para os conceitos; `CHK-AGENTS-NN`).
- [x] P0-19 — Atualizar `ARCHITECTURE.md` (conceitos, §6.2, itens em aberto).
- [x] P0-20 — Enforcement de NN-023: matriz (mecanismos implementados e planejados por fase) e `CHK-BOUNDARIES`.
- [x] P0-21 — Atualizar este ROADMAP sem renumerar IDs.
- [x] P0-22 — Atualizar `docs/migration/inventory-template.md` com o mapa funcional e arquitetural.
- [x] P0-23 — Documentar Product Shell e os três níveis de experiência ([`product-model.md`](docs/architecture/product-model.md)).
- [x] P0-24 — Documentar Context como conceito, com requisitos e sem contrato ([`product-model.md`](docs/architecture/product-model.md) §4).
- [x] P0-25 — Documentar Distribution Profile e os três eixos de disponibilidade como conceito futuro ([`distribution.md`](docs/architecture/distribution.md) §2–§3).
- [x] P0-26 — Registrar a visão futura de Lunet2D e Urbe como Products completos ([`product-vision.md`](docs/architecture/product-vision.md)).
- [x] P0-27 — Registrar a independência entre Store e Library e Assets como categoria do catálogo ([`distribution.md`](docs/architecture/distribution.md) §5–§6).
- [x] P0-28 — Registrar a estratégia de distribuição first-party e Services compartilhados ([`distribution.md`](docs/architecture/distribution.md) §7–§8).
- [x] P0-29 — Atualizar o gate e a sequência obrigatória da migração ([`docs/migration/README.md`](docs/migration/README.md) §6–§8).
- [x] P0-30 — Ratificação do ADR-0006 e revisão do alinhamento pelo proprietário (aprovada em 2026-09-30 — ADD-0003).
- [x] P0-31 — Portal mostra decisões pendentes e validações humanas pendentes, sempre com o objeto (DEC-0007); `CHK-DECISIONS`, `CHK-HANDOFFS` e `CHK-PORTAL` fiscalizam.
- [x] P0-32 — Aplicar DEC-0001…DEC-0004: ADRs 0001–0006 `Aceito`, origens confirmadas em `ecosystem.json`, técnica de importação e convenção de Issues documentadas.
- [x] P0-33 — Registrar DEC-0008 e DEC-0009 (alternativa A, **transitórias**) e aplicá-las na migração (`docs/migration/README.md` §9), nos inventários e neste ROADMAP.
- [x] P0-34 — Responder decisões clicando no portal (DEC-0010, ADR-0007): projeção com título/corpo da Issue, página com botões por alternativa, workflow `decision.yml`, aplicador `apply-decision.cs`, `CHK-DECISION-FLOW` e testes do aplicador.
- [x] P0-35 — Mecanismo ratificado (ADR-0007 `Aceito`, DEC-0011) e validado ponta a ponta com cliques reais do proprietário no portal (Issues #4 e #5; commits do bot `420c85b` e `e363a1e`).
- [x] P0-36 — Acesso e visibilidade do portal e do repositório decididos (DEC-0012: manter público; [`access.md`](docs/architecture/access.md)).
- [x] P0-37 — Aprovar ou reprovar validações humanas pelo portal (ADD-0005, ADR-0008 `Aceito`, DEC-0015): projeção, botões, aplicador/workflow estendidos, `CHK-DECISION-FLOW` e self-test. Ratificado e validado de ponta a ponta com cliques reais do proprietário (DEC-0015, Issue #21; validação do P0-37, Issue #23; commits do bot `5c6a318` e `e2501e3`).

**Gate:** um agente novo consegue entrar no repositório e compreender corretamente o produto, a autoridade documental e o processo de trabalho sem depender de uma conversa anterior.
*Estado do gate:* **aprovado** — pelo proprietário em 2026-09-30 ([ADD-0003](docs/governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md)); a validação do portal no celular (P0-15) foi aprovada em 2026-10-01 ([ADD-0005](docs/governance/addenda/ADD-0005-aprovacao-p0-15-e-validacao-pelo-portal.md)).

## Fase 1 — Inventário e migração

Objetivo: auditar Lunet2D e Urbe, mapear históricos e workflows, importar preservando histórico, restaurar builds/testes/releases.

- [x] P1-1 — Inventário do Lunet2D ([`inventory-lunet2d.md`](docs/migration/inventory-lunet2d.md)), **incluindo o mapa funcional e arquitetural com classificação futura proposta** (ADD-0002 §16). Evidência: handoff `HO-20260930-inventario-lunet2d`. Linha de base: 286 testes aprovados; CI da `main` verde.
- [x] P1-2 — Inventário do Urbe ([`inventory-urbe.md`](docs/migration/inventory-urbe.md)), **incluindo o mapa funcional e arquitetural com classificação futura proposta** (ADD-0002 §16). Evidência: handoff `HO-20260930-inventario-urbe`. Linha de base: `npm run check` com 56/56 arquivos de teste; CI da `main` verde.
- [x] P1-3 — Plano de importação por produto ([`import-plan.md`](docs/migration/import-plan.md); ensaio executado; **aprovado pelo proprietário em DEC-0014-A**; sem ações manuais do proprietário, §1 do plano), a partir dos inventários P1-1/P1-2 (DEC-0002: técnica A; DEC-0004: origens confirmadas). DEC-0008/DEC-0009 decididas (A, transitórias; `docs/migration/README.md` §9). Deve tratar cada risco dos inventários e listar, um a um, os ajustes técnicos inevitáveis (NN-013).
- [x] P1-4 — Importação do Lunet2D preservando histórico (PR de escopo restrito, NN-013). Evidência: PR #9 (merge commit dd8d4b4), handoff `HO-20261001-importacao-lunet2d`: 84 commits, 25 tags, árvore idêntica, 286/286 testes.
- [x] P1-5 — Importação do Urbe preservando histórico (PR de escopo restrito, NN-013). Evidência: PR #10 (merge commit 2085270), handoff `HO-20261001-importacao-urbe`: 382 commits, 6 tags, árvore idêntica, 56/56 testes.
- [x] P1-6 — Restaurar build/testes/workflows/releases de cada produto; pipelines seletivos por path (NN-014). Concluído por espelho de distribuição (DEC-0014-A, DEC-0016-A, DEC-0017-A; [`import-plan.md`](docs/migration/import-plan.md) §7): CI por produto no monorepo; Lunet2D publicado pelo novo caminho (release `v0.0.1-dev.107`, validada em aparelho); Urbe cortado (espelho ativo, Urbe Web validado). R-LUN-1 e R-URB-5 eliminados pelo desenho (numeração e secrets continuam na origem); R-URB-3 tratado por DEC-0016-A. Risco residual aceito: o `release.yml` do Urbe será exercitado na primeira release do Urbe pelo novo caminho (handoff `HO-20261001-distribuicao-automatizada-parte-1`).
- [x] P1-7 — Architecture tests sobre referências reais de código (NN-002, NN-003): `CHK-ARCH-REFS` (handoff `HO-20261001-arch-refs-e-auditoria-pos-migracao`).
- [x] P1-8 — Auditoria pós-migração: commits/tags da origem presentes (NN-012). Evidência: [`audit-post-migration.md`](docs/migration/audit-post-migration.md) (84/84 e 382/382 commits, 25/25 e 6/6 tags, conteúdo idêntico).
- [x] P1-9 — Portal: versão, última release, APK, checksum e release notes de cada produto derivados das releases do GitHub (nunca digitados). Fontes hoje: Lunet2D publica `release-manifest.json`, `SHA256SUMS.txt` e notas em cada release; o Urbe publica APK, instalador Windows e `latest.yml` (checksum só pelo digest do GitHub). Por DEC-0008-A, lê as releases dos repositórios de origem de cada produto (transitório). **Concluído** (handoff `HO-20261001-portal-releases-e-artefatos`; conferência humana aprovada pelo proprietário, [ADD-0007](docs/governance/addenda/ADD-0007-validacoes-aprovadas.md)).
- [x] P1-10 — Urbe Web: publicado pelo espelho na mesma URL (`https://abnercruz.github.io/Urbe/`, DEC-0009-A), sem quebrar a publicação atual (validado pelo proprietário, ADD-0007) e com rota previsível no portal (`publicUrl` do componente `urbe` → "Abrir versão Web"). O modelo **definitivo** de publicação é declarado pelo próprio Ecosystem na Fase 2 (Distribution Profile), quando DEC-0009 é reavaliada.
- [x] P1-11 — Registros canônicos de validação por build (contrato + estados de `definition-of-done.md` §4) e páginas `/testing/<componente>/<build>/` geradas deles. Evidência: handoff `HO-20261001-registros-de-validacao-por-build`; `CHK-VALIDATION`.
- [x] P1-12 — Classificar candidatos (Product Core, Product Shell, Tool, Workspace, Service, Library, Adapter) a partir dos mapas funcionais **já validados pós-migração**: apenas proposta; nenhuma extração (passo 9 de [`docs/migration/README.md`](docs/migration/README.md) §7). Extração/modernização (passo 10) está fora da Fase 1 e exige ADR por extração. **Concluído:** [`candidates.md`](docs/architecture/candidates.md), revisão aprovada pelo proprietário (Issue #27, `VAL-HO-20261001-classificacao-de-candidatos-f2cc354957c9`); regras do passo 10 em ADR-0009 `Aceito` (DEC-0018-A).
- [x] P1-13 — Gate da Fase 1 **aprovado pelo proprietário** em 2026-10-01 (Issue #28, `VAL-HO-20261001-gate-da-fase-1-247456bb21aa`); evidências no handoff `HO-20261001-gate-da-fase-1`.
- [x] P1-14 — Reconciliação de deriva de estado (auditoria externa pós-gate, sem tocar nos produtos): README sem estado, ARCHITECTURE atual × planejado × não decidido, Issue #6, matriz de enforcement, gates no portal; `CHK-STATE-CONSISTENCY` e `CHK-MIGRATION-HISTORY`; ADR-0010 `Proposto` e **DEC-0019** pendente. Evidência: handoff `HO-20261001-reconciliacao-de-deriva-de-estado`; mapa em [`state-drift.md`](docs/governance/state-drift.md).

Sequência obrigatória da Fase 1 (ADD-0002 §17): inventariar → importar → preservar histórico → restaurar build → restaurar testes → restaurar releases → validar produto → provar ausência de regressão conhecida → classificar candidatos → extrair/modernizar gradualmente. **Proibido** durante a importação: separar o Editor, extrair o Sprite Studio, reescrever o Agent Workspace, criar Store, criar Product Shell novo, transformar código em capabilities, reorganizar tudo em packages, reescrever o Urbe em C#, mudar a arquitetura interna porque a arquitetura futura é conhecida.

**Gate:**
1. Lunet2D e Urbe vivem no monorepo sem regressão conhecida e continuam possuindo lifecycles próprios.
2. A importação preservou intencionalmente a arquitetura funcional existente. Candidatos a Product Core, Product Shell, Tool, Workspace, Service, Library e Adapter foram inventariados, porém nenhuma extração estrutural foi realizada como efeito colateral da migração. *(Não pode ser marcado concluído antes da auditoria pós-migração, P1-8.)*
3. Cada Product foi empacotado, publicado e testado sem o Hub (NN-023).

*Estado do gate:* **aprovado** — pelo proprietário em 2026-10-01 (P1-13; evidências no handoff `HO-20261001-gate-da-fase-1`): 1) 286/286 e 56/56 testes, CI e release por produto, auditoria P1-8; 2) `apps/<id>` idêntico às origens salvo dois ajustes documentados, candidatos classificados sem extração ([`candidates.md`](docs/architecture/candidates.md)); 3) Lunet2D publicado e validado em aparelho, Urbe Web validado, nenhuma referência ao Hub. O passo 10 (refatorar/extrair) segue as regras do ADR-0009 `Aceito` ([`refactoring.md`](docs/architecture/refactoring.md)).

## Fase 2 — Contracts e Registry

Objetivo: formalizar `ComponentManifest`, `Capability`, provider/consumer, versionamento e permission model; registry inicial; testes de boundary. **Reavaliar as decisões transitórias DEC-0008 e DEC-0009** (canal de releases e publicação do Urbe Web): quando o Ecosystem passar a declarar distribuição, a declaração dele substitui o que vale na migração. Por ADR (ADD-0002): contrato de **Context**; nomes e formato dos eixos de disponibilidade (visibilidade, distribuição, modelo comercial); formato e validação do **Distribution Profile** (inclusive: nenhum perfil distribuível de um Product inclui o Hub — NN-023).

**Gate:** um componente pode declarar uma capability, outro pode descobri-la e a compatibilidade pode ser validada sem dependência direta entre produtos.

## Fase 3 — Hub read-only

Objetivo: Hub inicial em C#; lê `ecosystem.json`; mostra Lunet2D e Urbe; integra leitura do GitHub; Past / Now / Next; CI, releases, branches, PRs e tarefas.

- [ ] P3-1 — Portal lista as releases do Hub para instalação e recuperação (o portal continua existindo como mecanismo independente).

**Gate:** no celular, o proprietário abre o Hub e compreende o estado atual dos dois produtos sem abrir GitHub manualmente. *(requer validação humana em aparelho)*

## Fase 4 — Launcher e Updates

Objetivo: detectar versões, listar releases, baixar artefatos, validar integridade, conduzir instalação/atualização respeitando o modelo de segurança da plataforma, abrir produto instalado. Packaging e distribuição de cada Product **sem** o Hub (NN-023): o Hub é conveniência do proprietário, não requisito de distribuição; o Hub pode permanecer privado.

**Gate:** o usuário utiliza o Hub como entrada para instalar/atualizar/abrir Lunet2D e Urbe dentro dos limites da plataforma, **e** cada Product é instalado, atualizado e usado em seu domínio essencial sem o Hub presente (NN-023). *(requer validação humana em aparelho — DEVICE)*

## Fase 5 — Capability Runtime

Objetivo: IPC; command/event/request; capability discovery local; lifecycle; permissions; Host API; Tool hosting. Por ADR (ADD-0002): Host API de Product Shell; Context em discovery e permissões; Connections implementada sobre o Capability Registry (UX, sem registro paralelo).

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
