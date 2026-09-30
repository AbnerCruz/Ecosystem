# ROADMAP.md — Ecosystem

> **Autoridade:** abaixo de `MANIFEST.md`, ADRs, `ARCHITECTURE.md` e SPECs (MANIFEST §24). Fases e gates derivam de MANIFEST §46.
> Este roadmap cobre o **Ecosystem** (fundação, plataforma, Hub). Os roadmaps próprios de Lunet2D e Urbe continuam válidos e não são substituídos (MANIFEST §47).

## Convenções

- IDs de tarefa: `P<fase>-<n>` — estáveis (NN-019); nunca reutilizar.
- `[x]` concluído com evidência · `[~]` verificado automaticamente, **aguardando validação humana/revisão** · `[ ]` aberto.
- Um item nunca é marcado `[x]` enquanto depender de validação humana (NN-017); `CHK-ROADMAP` fiscaliza parte disso.
- Evidência de cada item concluído: handoff em `docs/governance/handoffs/`.

## Próxima tarefa

**P1-1 e P1-2 — inventário de Lunet2D e Urbe** (`docs/migration/inventory-<id>.md`, a partir do [template](docs/migration/inventory-template.md)), agora **incluindo o mapa funcional e arquitetural** com classificação futura proposta (ADD-0002 §16). As tarefas de alinhamento P0-16…P0-29 precedem P1-1/P1-2 e estão concluídas; a migração **não** foi iniciada.

*Por quê:* MANIFEST §44.1 e NN-012 exigem documentar origem, branches, tags, releases, workflows, Pages, configuração e estratégia de histórico **antes** de qualquer importação. O inventário é somente leitura e produz os fatos de que o plano de importação (P1-3) precisa. DEC-0001…DEC-0004 foram aprovadas em 2026-09-30 ([ADD-0003](docs/governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md)); qualquer pendência nova do proprietário aparece no portal com seu objeto (DEC-0007).

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
- [~] P0-15 — Validação humana do portal no celular (layout, toque, legibilidade) — NN-017. **Objeto:** https://abnercruz.github.io/Ecosystem/

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

**Gate:** um agente novo consegue entrar no repositório e compreender corretamente o produto, a autoridade documental e o processo de trabalho sem depender de uma conversa anterior.
*Estado do gate:* **aprovado pelo proprietário em 2026-09-30** ([ADD-0003](docs/governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md)); a validação do portal no celular (P0-15) segue à parte e continua pendente.

## Fase 1 — Inventário e migração

Objetivo: auditar Lunet2D e Urbe, mapear históricos e workflows, importar preservando histórico, restaurar builds/testes/releases.

- [ ] P1-1 — Inventário do Lunet2D (`docs/migration/inventory-lunet2d.md`), **incluindo o mapa funcional e arquitetural com classificação futura proposta** (ADD-0002 §16).
- [ ] P1-2 — Inventário do Urbe (`docs/migration/inventory-urbe.md`), **incluindo o mapa funcional e arquitetural com classificação futura proposta** (ADD-0002 §16).
- [ ] P1-3 — Plano de importação por produto, a partir dos inventários P1-1/P1-2 (DEC-0002: técnica A; DEC-0004: origens confirmadas).
- [ ] P1-4 — Importação do Lunet2D preservando histórico (PR de escopo restrito, NN-013).
- [ ] P1-5 — Importação do Urbe preservando histórico (PR de escopo restrito, NN-013).
- [ ] P1-6 — Restaurar build/testes/workflows/releases de cada produto; pipelines seletivos por path (NN-014).
- [ ] P1-7 — Architecture tests sobre referências reais de código (NN-002, NN-003).
- [ ] P1-8 — Auditoria pós-migração: commits/tags da origem presentes (NN-012).
- [ ] P1-9 — Portal: versão, última release, APK, checksum e release notes de cada produto derivados das releases do GitHub (nunca digitados).
- [ ] P1-10 — Urbe Web: decidir, com base no inventário P1-2, como o Urbe Web é publicado no monorepo sem quebrar a publicação atual, e expor rota previsível no portal.
- [ ] P1-11 — Registros canônicos de validação por build (contrato + estados de `definition-of-done.md` §4) e páginas `/testing/<componente>/<build>/` geradas deles.
- [ ] P1-12 — Classificar candidatos (Product Core, Product Shell, Tool, Workspace, Service, Library, Adapter) a partir dos mapas funcionais **já validados pós-migração**: apenas proposta; nenhuma extração (passo 9 de [`docs/migration/README.md`](docs/migration/README.md) §7). Extração/modernização (passo 10) está fora da Fase 1 e exige ADR por extração.

Sequência obrigatória da Fase 1 (ADD-0002 §17): inventariar → importar → preservar histórico → restaurar build → restaurar testes → restaurar releases → validar produto → provar ausência de regressão conhecida → classificar candidatos → extrair/modernizar gradualmente. **Proibido** durante a importação: separar o Editor, extrair o Sprite Studio, reescrever o Agent Workspace, criar Store, criar Product Shell novo, transformar código em capabilities, reorganizar tudo em packages, reescrever o Urbe em C#, mudar a arquitetura interna porque a arquitetura futura é conhecida.

**Gate:**
1. Lunet2D e Urbe vivem no monorepo sem regressão conhecida e continuam possuindo lifecycles próprios.
2. A importação preservou intencionalmente a arquitetura funcional existente. Candidatos a Product Core, Product Shell, Tool, Workspace, Service, Library e Adapter foram inventariados, porém nenhuma extração estrutural foi realizada como efeito colateral da migração. *(Não pode ser marcado concluído antes da auditoria pós-migração, P1-8.)*
3. Cada Product foi empacotado, publicado e testado sem o Hub (NN-023).

## Fase 2 — Contracts e Registry

Objetivo: formalizar `ComponentManifest`, `Capability`, provider/consumer, versionamento e permission model; registry inicial; testes de boundary. Por ADR (ADD-0002): contrato de **Context**; nomes e formato dos eixos de disponibilidade (visibilidade, distribuição, modelo comercial); formato e validação do **Distribution Profile** (inclusive: nenhum perfil distribuível de um Product inclui o Hub — NN-023).

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
