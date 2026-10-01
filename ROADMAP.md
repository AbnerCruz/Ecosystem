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

**Fase 2 concluída** (gate aprovado em 2026-10-01). Próxima: **Fase 3 — Hub read-only** (tarefas P3-x a abrir no ROADMAP antes de implementar).

Em paralelo, como trabalho **interno dos próprios Products** e **fora do escopo da Fase 2** (infraestrutura compartilhada): refatorações em `apps/urbe/` e `apps/lunet2d/` — que **fazem parte deste repositório** e são o único lugar de desenvolvimento deles; `AbnerCruz/Urbe` e `AbnerCruz/Lunet2D` não são lugares de desenvolvimento (são espelhos de distribuição transitórios, DEC-0008/DEC-0009) — governadas pelo ADR-0009 e [`refactoring.md`](docs/architecture/refactoring.md) — sugestão: **U-R1** no Urbe (continuar a decomposição de `app.js` / Product Shell) e **L-R2** no Lunet2D (decompor `MainActivity` em Shell + painéis), conforme [`candidates.md`](docs/architecture/candidates.md), cada uma delegada em tarefa própria e conduzida pelos processos locais de cada Product. Nenhum subsistema é extraído sem evidência de segundo consumidor ([`local-first.md`](docs/architecture/local-first.md)).

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
- [x] P1-14 — Reconciliação de deriva de estado (auditoria externa pós-gate, sem tocar nos produtos): README sem estado, ARCHITECTURE atual × planejado × não decidido, Issue #6, matriz de enforcement, gates no portal; `CHK-STATE-CONSISTENCY` e `CHK-MIGRATION-HISTORY`; ADR-0010 `Proposto` e **DEC-0019** levada ao portal (*histórico: no momento desta tarefa a decisão foi criada; foi resolvida depois, em P1-15 — ADR-0010 `Aceito`, DEC-0019-A*). Evidência: handoff `HO-20261001-reconciliacao-de-deriva-de-estado`; mapa em [`state-drift.md`](docs/governance/state-drift.md).
- [x] P1-16 — Veredito do fluxo de decisões: um workflow `decision` só termina verde quando a decisão foi **registrada ou já estava registrada com a mesma escolha** (aplicador com resultado estruturado `outcome` + `decision-verdict.sh`; idempotência; sem gravação parcial); a recusa deixa o workflow vermelho e a Issue com a causa. Corrige também a redação "fora do Ecosystem" das refatorações dos Products. Evidência: handoff `HO-20261001-p2-12-distribuicao-atual-e-dec-0021`; self-tests do aplicador e do veredito.
- [x] P1-17 — **Fluxo multiagente mínimo** ([ADR-0014](docs/adr/0014-fluxo-multiagente-minimo.md), [ADD-0010](docs/governance/addenda/ADD-0010-concorrencia-multiagente.md), [`multi-agent.md`](docs/governance/multi-agent.md)): `base_commit` e identidades BASE/RESULT/INTEGRATION/VALIDATION nos handoffs, `Check.cs -- --integration` (base obsoleta e sobreposição), PR como ponto de integração; integração auditada do teste com dois agentes simultâneos (Fase 3 e Urbe RM-F2-03). Evidência: handoff `HO-20261001-integracao-multiagente`.
- [x] P1-15 — Reconciliação de DEC-0019 (clique do proprietário recusado pelo registrador por defeito do `CHK-STATE-CONSISTENCY`; corrigido e registrado pelo mecanismo canônico), remoção de `ecosystem.phase` e **princípio local-first / promoção por evidência** ([ADR-0011](docs/adr/0011-local-first-e-promocao-por-evidencia.md), [ADD-0009](docs/governance/addenda/ADD-0009-local-first-e-promocao-por-evidencia.md)). Evidência: handoff `HO-20261001-dec-0019-e-local-first`.

Sequência obrigatória da Fase 1 (ADD-0002 §17): inventariar → importar → preservar histórico → restaurar build → restaurar testes → restaurar releases → validar produto → provar ausência de regressão conhecida → classificar candidatos → extrair/modernizar gradualmente. **Proibido** durante a importação: separar o Editor, extrair o Sprite Studio, reescrever o Agent Workspace, criar Store, criar Product Shell novo, transformar código em capabilities, reorganizar tudo em packages, reescrever o Urbe em C#, mudar a arquitetura interna porque a arquitetura futura é conhecida.

**Gate:**
1. Lunet2D e Urbe vivem no monorepo sem regressão conhecida e continuam possuindo lifecycles próprios.
2. A importação preservou intencionalmente a arquitetura funcional existente. Candidatos a Product Core, Product Shell, Tool, Workspace, Service, Library e Adapter foram inventariados, porém nenhuma extração estrutural foi realizada como efeito colateral da migração. *(Não pode ser marcado concluído antes da auditoria pós-migração, P1-8.)*
3. Cada Product foi empacotado, publicado e testado sem o Hub (NN-023).

*Estado do gate:* **aprovado** — pelo proprietário em 2026-10-01 (P1-13; evidências no handoff `HO-20261001-gate-da-fase-1`): 1) 286/286 e 56/56 testes, CI e release por produto, auditoria P1-8; 2) `apps/<id>` idêntico às origens salvo dois ajustes documentados, candidatos classificados sem extração ([`candidates.md`](docs/architecture/candidates.md)); 3) Lunet2D publicado e validado em aparelho, Urbe Web validado, nenhuma referência ao Hub. O passo 10 (refatorar/extrair) segue as regras do ADR-0009 `Aceito` ([`refactoring.md`](docs/architecture/refactoring.md)).

## Fase 2 — Contracts e Registry

Objetivo: formalizar `ComponentManifest`, `Capability`, provider/consumer, versionamento, permission model, `Context`, `Distribution Profile` e Registry inicial; criar testes de boundary. **Reavaliar as decisões transitórias DEC-0008 e DEC-0009** (canal de releases e publicação do Urbe Web): quando o Ecosystem passar a declarar distribuição, a declaração dele substitui o que vale na migração (P2-12). Por ADR (ADD-0002): contrato de **Context**; nomes e formato dos eixos de disponibilidade (visibilidade, distribuição, modelo comercial); formato e validação do **Distribution Profile** (inclusive: nenhum perfil distribuível de um Product inclui o Hub — NN-023). **Princípio da fase (ADR-0011):** os contratos nascem **dentro** do Ecosystem e **nenhum** Product é refatorado nem tem subsistema extraído; capabilities reais só aparecem por promoção baseada em evidência. Contratos: [ADR-0012](docs/adr/0012-contratos-da-fase-2.md) (`Proposto`, DEC-0020). Evidência da fase: handoff `HO-20261001-fase-2-aberta-e-vertical-slice`.

Cada tarefa traz: *saída verificável* · *depende de* · *evidência* · *gate* (critério do gate que ela sustenta: **G-a** declarar capability · **G-b** descobrir · **G-c** validar compatibilidade sem dependência direta entre Products).

- [x] P2-1 — **ComponentManifest e identidade**: o manifest é a entrada de `ecosystem.json` (chave = ID estável, `type`, `version`, `owners`, `provides`, `requires`, `permissions`), sem estado de execução e sem campos de distribuição.
  - Saída: `ecosystem.schema.json` estendido; ADR-0012 (manifest). Depende de: —. Evidência: `CHK-SCHEMA`; fixtures do slice validam contra `#/properties/components`. Gate: G-a.
- [x] P2-2 — **Capability contract**: formato versionado (ID, versões, inputs, outputs, erros, permissões exigidas, lifecycle, política de compatibilidade); provider derivado dos manifests.
  - Saída: `capability-contract.schema.json`; `capability.test` (fixture). Depende de: P2-1. Evidência: `CHK-SCHEMA` sobre `docs/contracts/**/capabilities/*.json`. Gate: G-a.
- [x] P2-3 — **Provider / Consumer**: `provides` e `requires` com validação (capability conhecida, versão com contrato, consumidor satisfeito).
  - Saída: `Registry.Validate`. Depende de: P2-1, P2-2. Evidência: `CHK-REGISTRY`, casos positivo e negativos do slice. Gate: G-a, G-c.
- [x] P2-4 — **Versionamento e regras de compatibilidade**: semver `MAJOR.MINOR.PATCH`; faixas exata, `^`, `~` e comparadores; sem pré-lançamento nem resolução de grafo.
  - Saída: `SemVer`, `VersionRange`. Depende de: P2-2. Evidência: self-test `registry:` (7 casos) e `CAP_INCOMPATIBLE` no slice. Gate: G-c.
- [x] P2-5 — **Permission model inicial**: catálogo `permissions.json`, `permissions.requests`, deny-by-default (consumidor precisa solicitar o que o contrato exige). Enforcement em runtime fica para a Fase 5.
  - Saída: `permissions-catalog.schema.json` + catálogo. Depende de: P2-2. Evidência: `PERMISSION_MISSING`/`PERMISSION_UNKNOWN` no slice. Gate: G-c.
- [x] P2-6 — **Contrato de Context**: caminho `ecosystem → product → project → workspace → tool`, ordem estrita, `product` existente; exemplos Lunet (`MeuJogo`/`editor`) e Urbe (`MeuVault`/`Nota`). Sem IPC nem Host API (Fase 5).
  - Saída: `context.schema.json` + regras. Depende de: P2-1. Evidência: `CHK-REGISTRY` valida os exemplos; self-test "Context fora de ordem". Gate: —.
- [x] P2-7 — **Registry inicial e discovery**: índice local/estático derivado (registrar, indexar capabilities, descobrir providers, validar), dentro dos checks (local-first); CLI `--registry`.
  - Saída: `Registry`, `RegistryCli`. Depende de: P2-3, P2-4. Evidência: `-- --registry --discover capability.test "^1.0.0"` → `provider-a@1.0.0`. Gate: G-b.
- [x] P2-8 — **Distribution Profile**: formato com `availability` e três eixos independentes (nomes congelados depois em P2-9); NN-023: Hub nunca `bundled` em perfil com componente público.
  - Saída: `distribution-profile.schema.json` + exemplo `lunet-public`. Depende de: P2-1. Evidência: `CHK-REGISTRY`; self-test "Hub bundled". Gate: —.
- [x] P2-9 — **Congelar os nomes dos eixos** (visibility, distribution, commercialModel, availability). Os contratos estruturais do ADR-0012 foram aceitos em DEC-0020-B; os nomes ficaram provisórios até a distribuição real ser modelada (P2-12) e o destino decidido (**DEC-0021-C**, 2026-10-01). Congelados como estão no schema, incluindo `external-channel` e o `status` `target`; mudar um nome exige ADR e migração dos perfis.
  - Saída: ADR-0012 `Aceito`; schema sem marcações de provisório; perfil `target` ([`target.profile.json`](docs/distribution/target.profile.json)). Depende de: DEC-0021 (registrada: C), P2-12. Evidência: handoff `HO-20261001-gate-da-fase-2`; `CHK-REGISTRY` + 2 casos de self-test do perfil `target`. Gate: —.
- [x] P2-10 — **Architecture checks da fase**: `CHK-REGISTRY` (provider/consumer conhecidos, compatibilidade, permissões), Tool/Service/Library sem Host concreto (`TOOL_KNOWS_HOST`), Product → Product (`PRODUCT_DEPENDS_ON_PRODUCT`), Hub não bundled; matriz de enforcement atualizada.
  - Saída: `CHK-REGISTRY`. Depende de: P2-3..P2-8. Evidência: self-test com mutações reais (5 casos do check + slice). Gate: G-c.
- [x] P2-11 — **Vertical slice**: A provê `capability.test` v1; B exige `^1.0.0` (compatível, descoberto); C exige `^2.0.0` (falha); mais negativos de permissão, capability desconhecida, sem provider, Tool que conhece Host, Product → Product. Fixtures; nenhuma Tool real extraída.
  - Saída: `docs/contracts/examples/registry-slice/`. Depende de: P2-1..P2-7. Evidência: `CHK-REGISTRY` verde e o negativo falhando como esperado. Gate: G-a, G-b, G-c.
- [x] P2-12 — **Reavaliar DEC-0008/DEC-0009** com o modelo declarativo pronto: descrever o arranjo atual como dado (SOURCE = `apps/<id>`; DISTRIBUTION = repositórios de origem, transitório) e levar ao portal a decisão sobre a distribuição definitiva (releases e Urbe Web) com alternativas e consequências reais. **Não** altera canais, releases, endpoints de atualização, Urbe Web, Pages nem repositórios antigos; qualquer mudança é item próprio depois da decisão.
  - Saída: `docs/distribution/current.profile.json` (perfil `current`, validado por `CHK-REGISTRY`), [`distribution.md`](docs/architecture/distribution.md) §10 e **DEC-0021** pendente no portal (não bloqueante). Depende de: P2-8, DEC-0020 (registrada). Evidência: `CHK-REGISTRY` + 4 casos de self-test do perfil; handoff `HO-20261001-p2-12-distribuicao-atual-e-dec-0021`. Gate: —.
- [x] P2-13 — **Portal projeta components/capabilities/providers/consumers/profiles** (somente leitura, derivado), quando houver capability real ou perfil real; sem grande UI antes disso. Condição atendida pelo **perfil real** (P2-12): o portal mostra a seção "Distribuição atual dos Products" (fonte = `path` do Ecosystem; canal; localização derivada de `ecosystem.json`). Nenhuma capability real existe: `capabilities` é derivada de `provides`/`requires`, está vazia e o portal não exibe seção para ela (nada é inventado nem mostrado como operacional).
  - Saída: campos `distribution` e `capabilities` da projeção + seção do portal + comparações em `CHK-PORTAL` (2 casos de self-test). Depende de: P2-7, P2-12. Evidência: handoff `HO-20261001-p2-12-distribuicao-atual-e-dec-0021`. Gate: —.
- [x] P2-14 — **Gate da Fase 2** — **aprovado pelo proprietário** em 2026-10-01 (Issue #32, `VAL-HO-20261001-gate-da-fase-2-388a8354a4eb`): evidência objetiva (slice executável + checks) e aprovação do proprietário.
  - Saída: handoff do gate. Depende de: P2-1..P2-13, DEC-0020 (B), DEC-0021 (C). Evidência: handoff `HO-20261001-gate-da-fase-2` (evidência pronta; validação humana pendente no portal). **Não** é autoaprovável: exige a validação humana do proprietário. Gate: G-a, G-b, G-c.

**Gate:** um componente pode declarar uma capability, outro pode descobri-la e a compatibilidade pode ser validada sem dependência direta entre produtos. *(Evidência executável: `dotnet run tests/consistency/Check.cs` — `CHK-REGISTRY` — e `-- --registry --file docs/contracts/examples/registry-slice/positive.json`.)*
*Estado do gate:* **aprovado** — pelo proprietário em 2026-10-01 (P2-14, Issue #32; evidências no handoff `HO-20261001-gate-da-fase-2`): capability declarada, descoberta pelo Registry e compatibilidade validada (positivo e negativos) sem dependência entre Products; contratos do ADR-0012 aceitos; distribuição atual e direção (DEC-0021-C) descritas como dados.

## Fase 3 — Hub read-only

Objetivo: Hub inicial em C#; lê `ecosystem.json`; mostra Lunet2D e Urbe; integra leitura do GitHub; Past / Now / Next; CI, releases, branches, PRs e tarefas.

- [ ] P3-1 — Portal lista as releases do Hub para instalação e recuperação (o portal continua existindo como mecanismo independente).

**Gate:** no celular, o proprietário abre o Hub e compreende o estado atual dos dois produtos sem abrir GitHub manualmente. *(requer validação humana em aparelho)*

## Fase 4 — Launcher e Updates

Direção de distribuição decidida (DEC-0021-C): plataforma first-party como distribuição principal futura e progressiva; cada Product migra em item próprio (versão-ponte, redirecionamento, recuperação), repositórios antigos como canal alternativo. Esta fase é a candidata natural a planejá-la; nenhum item existe ainda.

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
