# ARCHITECTURE.md — Arquitetura inicial

> **Autoridade:** abaixo de `MANIFEST.md` e dos ADRs aprovados (MANIFEST §24).
> Este documento registra apenas o que o manifesto já estabelece e o que foi decidido em ADR. Tudo o que ainda não foi decidido está listado explicitamente em §8 — **não** deve ser inferido.

## 1. Conceitos (MANIFEST §6)

| Conceito | Definição resumida | Exemplos |
|----------|--------------------|----------|
| **Product** | Aplicativo com domínio próprio e valor independente. | Lunet2D, Urbe, Hub |
| **Host** | Ambiente capaz de hospedar uma interface ou capability; oferece contexto e serviços sem exigir que a ferramenta o conheça. | Hub, Lunet2D |
| **Capability** | Contrato versionado que representa uma capacidade fornecida por um componente. | `sprite.edit`, `git.status`, `document.search` |
| **Tool** | Capability com operação de usuário independente e interface própria; abrível standalone quando o domínio permite. | Sprite Studio, Editor |
| **Service** | Capability operacional sem interface própria. | Agent Runtime, Git, Build |
| **Library** | Código interno sem lifecycle de usuário. | serialização, contracts |
| **Workspace** | Superfície de trabalho que organiza contexto, ferramentas e estado. | Agent Workspace, GitHub Workspace |
| **Adapter** | Traduz contratos ou conecta um sistema legado; se temporário, declara condição de remoção. | — |
| **Contract** | Descrição explícita, estável, versionada e testável de uma interação. | — |
| **Product Shell** | Superfície especializada e Host de um Product: representa seu domínio, navegação, biblioteca, projetos, integrações e capabilities, com experiência própria, sem possuir as Tools reutilizáveis que hospeda. Não é o Hub e não exige o Hub. *(ADD-0002; conceito, nada implementado)* | Lunet Product Shell, Urbe Product Shell |
| **Context** | Escopo atual em que uma operação, Tool, Workspace ou Agent trabalha, hierárquico. *(ADD-0002; só o conceito — contrato nas Fases 2/5)* | Ecosystem → Lunet2D → MeuJogo → Editor |
| **Distribution Profile** | Descreve quais componentes formam determinada edição/distribuição. Formato na Fase 2 (ADR-0012; nomes congelados em P2-9). *(ADD-0002; perfis reais: `current` — arranjo vigente — e `target` — direção decidida em DEC-0021-C, não operacional — em `docs/distribution/`)* | `lunet-public` (conceitual) |
| **Connections** | Superfície de **UX** de um Product Shell para ver/ligar capabilities. Não é arquitetura paralela: é implementada sobre Capabilities. *(ADD-0002)* | Lunet2D → Connections |

As definições normativas de Product … Contract estão no `MANIFEST.md` §6. As de **Product Shell**, **Context**, **Distribution Profile** e **Connections** estão em [`docs/architecture/product-model.md`](docs/architecture/product-model.md) e [`docs/architecture/distribution.md`](docs/architecture/distribution.md) (MANIFEST §6 não foi alterado; ver [`manifest-changelog.md`](docs/governance/manifest-changelog.md)).

## 2. Linguagem (MANIFEST §4, NN-005)

- **C#** é o padrão para toda nova infraestrutura compartilhada (Hub, contracts, registry, IPC, SDKs, tools, workspaces, checks do repositório).
- O **Urbe** em produção é JavaScript hoje; o proprietário decidiu a **reescrita completa em C#, com troca do produto só em paridade total** (DEC-0024-B, [ADR-0016](docs/adr/0016-migracao-do-urbe-para-csharp.md); programa em [`apps/urbe/docs/csharp/`](apps/urbe/docs/csharp/README.md)). Pilha de UI/hosts, transição e corte ainda não estão decididos. Até o corte, a integração com o resto do ecossistema continua por adapters; `ecosystem.json` descreve o produto distribuído (`language` só muda no corte).
- Contratos que atravessam processos devem ser semanticamente independentes de linguagem (MANIFEST §4.3). Por isso os schemas da fundação são JSON Schema (ADR-0002).

## 3. Componentes e extração

O mapa canônico dos componentes é [`ecosystem.json`](ecosystem.json) (formato: [`docs/contracts/schemas/ecosystem.schema.json`](docs/contracts/schemas/ecosystem.schema.json)).

Para criar um componente compartilhado (`platform/`, `workspaces/`, `tools/`, Service), é obrigatório responder ao critério de MANIFEST §48 e às cinco perguntas de NN-022, registrar ADR e declarar em `ecosystem.json` `responsibility`, `contract`, `consumers`, `owners`, `compatibility` e `extractionReason` (verificado por `CHK-SHARED-DECLARATION`). Quais componentes existem hoje, e em que `status`, é o que `ecosystem.json` diz — este documento não o replica.

## 3.1 Local-first e promoção por evidência (ADR-0011)

Uma funcionalidade nova nasce **no Product que tem a necessidade**; vira componente compartilhado só por **promoção baseada em evidência** (segundo consumidor real → Extraction Review → NN-022 → ADR → contrato). Duplicação temporária é preferível a uma abstração compartilhada errada. O potencial de reutilização é registrado nos handoffs (`reuse_assessment`). Guia: [`docs/architecture/local-first.md`](docs/architecture/local-first.md); refatoração e extração: [`docs/architecture/refactoring.md`](docs/architecture/refactoring.md).

## 3.2 Contratos da Fase 2 (ADR-0012, `Aceito`)

- **ComponentManifest** = a entrada de componente de `ecosystem.json` (identidade = chave; `type`, `version`, `owners`, `status`) com, opcionalmente, `provides`, `requires` e `permissions.requests`. Sem estado de execução e sem campos de distribuição.
- **Capability** = contrato versionado (`docs/contracts/capabilities/<id>.json`: inputs, outputs, erros, permissões exigidas, lifecycle, compatibilidade semver). O provider é derivado dos `provides` dos manifests; um consumidor depende da capability e da faixa de versões, nunca de Product, Host, classe ou path.
- **Permissões:** catálogo `docs/contracts/permissions.json`; deny-by-default (o componente só tem o que solicitou; o consumidor precisa ter solicitado o que o contrato exige). Enforcement em runtime: Fase 5.
- **Context:** caminho `ecosystem → product → project → workspace → tool` (contrato inicial; sem IPC nem Host API).
- **Registry:** índice local derivado, dentro dos checks (`CHK-REGISTRY`, `-- --registry`): registrar, indexar, descobrir providers, validar compatibilidade. Nasce local (ADR-0011) e só vira componente próprio com consumidor real.
- **Distribution Profile:** entradas com `availability`, três eixos independentes (visibility, distribution, commercialModel; nomes congelados em P2-9) e `channels`; o Hub nunca é `bundled` em perfil com componente público (NN-023). O arranjo real (**SOURCE** = `apps/<id>` no Ecosystem; **DISTRIBUTION** = repositórios de origem, transitório) é o perfil `current` em `docs/distribution/current.profile.json` ([`distribution.md`](docs/architecture/distribution.md) §10). Direção decidida (DEC-0021-C): plataforma first-party como distribuição principal **futura e progressiva** — perfil `target`, não operacional.
- Nenhuma capability real existe ainda: só o vertical slice de exemplo (`docs/contracts/examples/`). Capabilities reais aparecem por **promoção baseada em evidência**.

## 4. Boundaries (MANIFEST §12)

Permitido:

```text
Product → Contract
Host    → Contract
Tool    → Contract
Adapter → Contract
```

Proibido:

```text
Urbe    → Lunet2D          (NN-002)
Lunet2D → Urbe             (NN-002)
Product → Product          (integração só por contract/capability/adapter)
*       → Hub (obrigatório) (NN-003)
Tool / componente compartilhado → Product/Host concreto (NN-007, MANIFEST §12)
*       → Portal (qualquer)   (ADD-0001: o portal nunca é dependência)
```

**Fiscalização:** o grafo **declarado** em `ecosystem.json` é verificado por `CHK-BOUNDARIES`; as **referências reais de código** dos produtos ativos (referência a outro Product ou ao Hub, `ProjectReference`/`file:`/`link:` para fora do produto) por `CHK-ARCH-REFS`. O mapeamento invariante → mecanismo e seu estado é [`docs/governance/enforcement-matrix.json`](docs/governance/enforcement-matrix.json) (a autoridade; não copiado aqui).

Exceções exigem ADR e alteração explícita do check.

## 5. Autoridades (NN-001)

| Conceito | Autoridade canônica | Observações |
|----------|--------------------|-------------|
| Princípios e invariantes | `MANIFEST.md` | `AGENTS.md` reproduz; não substitui. |
| Componentes do ecossistema | `ecosystem.json` | Único manifest raiz (`CHK-SINGLE-AUTHORITY`). |
| Formato dos registros | `docs/contracts/schemas/*.schema.json` | O check valida contra eles; não duplica o formato. |
| Contrato de uma capability / catálogo de permissões | `docs/contracts/capabilities/<id>.json` / `docs/contracts/permissions.json` | Provider **derivado** dos `provides` em `ecosystem.json` (uma autoridade); `CHK-REGISTRY`. |
| Código, versão e histórico de Lunet2D/Urbe | `apps/<id>/` deste repositório quando `status = active`; o repositório de origem enquanto `status = not-migrated` | Ver `source`, `path` e `version.authority` em `ecosystem.json`. Com o produto `active`, o repositório de origem é **espelho de distribuição** (releases e Urbe Web; DEC-0008/DEC-0009 transitórias, DEC-0014): nunca autoridade de código. |
| Distribuição atual dos Products (canais por onde builds, releases e web chegam ao usuário) | `docs/distribution/current.profile.json` | Perfil `current` (transitório: DEC-0008/DEC-0009 valem até a plataforma first-party existir, DEC-0021-C); a direção é o perfil `target.profile.json`. SOURCE = `path` em `ecosystem.json`; as localizações dos canais vêm de `ecosystem.json`, não são copiadas; `CHK-REGISTRY`. |
| Mapeamento NN → fiscalização | `docs/governance/enforcement-matrix.json` | |
| Decisões pendentes/tomadas do proprietário | `docs/governance/decisions.json` | Decisão tomada aponta para ADR/SPEC/ROADMAP. |
| Decisões arquiteturais | `docs/adr/` | |
| Fases e escopo/IDs das tarefas | `ROADMAP.md` | Decidido em DEC-0003. |
| Estado vivo das tarefas (claimed, working, blocked…) | Issues do GitHub | DEC-0003; convenção em `docs/governance/communication.md` §8. |
| Fase e estado de cada gate | `ROADMAP.md` (linha `*Estado do gate:*` de cada fase) | Não há campo de fase em `ecosystem.json` (DEC-0019-A, ADR-0010); o portal projeta os gates derivados do ROADMAP (`CHK-PORTAL`, `CHK-STATE-CONSISTENCY`). |
| Estado de validação de um build (`IMPLEMENTED` … `VALIDATED`) | `docs/validation/<componente>/<build>.json` | Contrato `validation-record.schema.json`; `CHK-VALIDATION`. Evidência humana vem dos handoffs e não é duplicada (NN-001). |
| Registros de trabalho de agentes | `docs/governance/handoffs/` | |
| Política de integração (rotina × crítico: o que o integrador leva à `main` sozinho e o que espera o proprietário) | `docs/governance/integration-policy.json` | Decidida pelo proprietário em ADD-0012; mecanismo no ADR-0015. Sempre lida da `main`; o status `ecosystem/integration`, as labels e os trailers `Integration-*` são derivados; `CHK-INTEGRATION`. |
| Alterações do `MANIFEST.md` | `docs/governance/manifest-changelog.md` | Registro exigido por MANIFEST §0; o texto do manifesto continua sendo o próprio `MANIFEST.md`. |
| Decisões/adendos do proprietário (texto integral) | `docs/governance/addenda/` | Nível 1 da hierarquia (MANIFEST §24); referenciados por `decisions.json`. |
| Estado exibido no portal web | **nenhuma** — `site/data/ecosystem-status.json` é projeção (`authority: false`) | Gerada no CI a partir das fontes acima; não versionada; validada por `CHK-PORTAL`. |

## 6. Runtime e GitHub (MANIFEST §14, §17, NN-015)

GitHub é registro de desenvolvimento e distribuição, **não** barramento de runtime. O protocolo de comunicação runtime (envelope versionado, command/event/request/response, discovery, permissões, erros, cancelamento, progresso, transporte desacoplado) será especificado por ADR **antes** da primeira dependência séria entre processos (Fase 5).

## 6.1 Superfícies humanas: GitHub, Portal e Hub (ADD-0001, ADR-0005)

| Superfície | Papel |
|------------|-------|
| GitHub | fonte técnica |
| Portal web (GitHub Pages, `site/`) | portal humano de desenvolvimento, distribuição, documentação, testes e recuperação |
| Ecosystem Hub | control plane completo |

"Estou com o celular e quero saber o estado do Ecosystem, baixar/testar um aplicativo ou recuperar o próprio Hub" → **Portal**. "Quero controlar profundamente o ecossistema, workspaces, capabilities, agentes, instalações, atualizações e integrações" → **Hub**.

O portal é projeção do estado canônico, nunca fonte de verdade; não é dependência de runtime de nenhum componente; não implementa nem duplica o Hub; e continua existindo depois do Hub como mecanismo de recuperação independente. Detalhes: [`docs/architecture/portal.md`](docs/architecture/portal.md).

## 6.2 Níveis de experiência e distribuição (ADD-0002, ADR-0006)

```text
LEVEL 1  Ecosystem Hub       universo geral do proprietário/ecossistema
LEVEL 2  Product Shell       universo especializado de um Product
LEVEL 3  Workspace / Tool    atividade concreta do usuário
```

Uma Tool reutilizável também pode ser aberta diretamente do Hub (`Ecosystem → Sprite Studio`): o Product Shell hospeda Tools, não as possui.

- **Hub ≠ Product Shell.** O Hub não é requisito para distribuir um Product: **NN-023** (complementa NN-003). O Hub pode permanecer privado/interno.
- **Arquitetura ≠ distribuição.** Existir no monorepo/Registry não significa estar incluído, instalado, visível, público, gratuito ou utilizável; isso é papel do Distribution Profile (futuro).
- **Visibilidade, forma de distribuição e modelo comercial** são três eixos independentes que a arquitetura precisa suportar; nomes e formato ficam para a Fase 2 (ADR).
- **Connections = UX; Capabilities = mecanismo.** Não existe segundo registro ou protocolo.
- **First-party by default; external distribution by choice.** Plataformas externas não são autoridade arquitetural. Nada de commerce, contas, identidade ou marketplace está implementado.
- **Lunet2D e Urbe** são Products completos, cada um com Product Shell próprio e experiência própria; visão em [`product-vision.md`](docs/architecture/product-vision.md) (não é estado atual).

Nada disso autoriza criar código, diretórios, Services, Shells, Store ou schemas agora (NN-020, NN-022, NN-013). Respostas curtas às perguntas-chave: [`docs/architecture/faq.md`](docs/architecture/faq.md).

## 7. Permissões (MANIFEST §30, NN-016)

Princípio: menor privilégio, deny-by-default quando aplicável, agentes não herdam todas as permissões do usuário. O modelo inicial de permissões (declaração, deny-by-default) está em §3.2; o enforcement em runtime é da Fase 5 (ver §8).

## 8. Planejado e em aberto

Este documento descreve a arquitetura **atual** nas seções anteriores. Aqui ficam só duas coisas, separadas. Nada de histórico: o que já foi decidido ou entregue está nos ADRs, em `decisions.json` e nos handoffs.

### 8.1 Planejado (a fase em que vira concreto está no ROADMAP)

- capabilities reais, por **promoção baseada em evidência** (ADR-0011) — nenhuma existe ainda;
- Host API do Product Shell e contrato de Context em runtime (Fase 5); IPC e transporte (Fase 5), por ADR **antes** da primeira dependência séria entre processos;
- enforcement de permissões em runtime (Fase 5);
- packaging e distribuição pelo Distribution Profile (Fase 4);
- plataforma first-party como distribuição principal dos Products (DEC-0021-C): **progressiva** — até ela existir valem DEC-0008/DEC-0009 (repositórios de origem); depois, cada Product migra em item próprio, com ponte de atualização, redirecionamento e recuperação, e os repositórios antigos viram canal alternativo, sem serem apagados. Nenhum Service de distribuição antes de haver consumidor real (NN-020, NN-022);
- Hub read-only **Android nativo em C#** (`net10.0-android`), com a leitura de dados em biblioteca independente de UI (Fase 3; ADR-0013, DEC-0022-A);
- estrutura de `platform/`, `workspaces/`, `tools/` e `services/`: os diretórios só nascem quando tiverem conteúdo com função (ADR-0004); `apps/` existe e contém Lunet2D e Urbe.
- Agent Runtime (Service), Execution Runtime, Agent Workspace, Product de IA e organizações de agentes (diretiva ADD-0013): plano, fronteiras e contratos **propostos** em [`docs/architecture/agent-runtime.md`](docs/architecture/agent-runtime.md); o status do ADR-0017 e a sequência do primeiro slice são do ADR e da DEC-0026 (autoridades; não copiados aqui). Nada implementado.

### 8.2 Não decidido (nenhum agente deve tratar como decidido)

- quando e como a plataforma first-party será construída (fase, Services, identidade e catálogo) — a direção está decidida (perfil `target`), o plano não;
- quais Services compartilhados (Identity, Catalog, Commerce, Entitlements, Downloads, Updates, Reviews, Creator Profiles, Notifications) existirão, se algum — só com consumidores reais, contrato e ADR (NN-020, NN-022);
- fonte canônica de catálogo, entitlements e identidade; política de edições e canais externos;
- estrutura **final** de cada Product Shell no código: há classificação proposta e aprovada como proposta ([`docs/architecture/candidates.md`](docs/architecture/candidates.md)) e regras de refatoração aceitas (ADR-0009), mas cada mudança estrutural é decidida na própria refatoração.
