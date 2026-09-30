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
| **Distribution Profile** | Descreve quais componentes formam determinada edição/distribuição. Nome e formato sujeitos a ADR. *(ADD-0002; conceito futuro, não implementado)* | `lunet-public` (conceitual) |
| **Connections** | Superfície de **UX** de um Product Shell para ver/ligar capabilities. Não é arquitetura paralela: é implementada sobre Capabilities. *(ADD-0002)* | Lunet2D → Connections |

As definições normativas de Product … Contract estão no `MANIFEST.md` §6. As de **Product Shell**, **Context**, **Distribution Profile** e **Connections** estão em [`docs/architecture/product-model.md`](docs/architecture/product-model.md) e [`docs/architecture/distribution.md`](docs/architecture/distribution.md) (MANIFEST §6 não foi alterado; ver [`manifest-changelog.md`](docs/governance/manifest-changelog.md)).

## 2. Linguagem (MANIFEST §4, NN-005)

- **C#** é o padrão para toda nova infraestrutura compartilhada (Hub, contracts, registry, IPC, SDKs, tools, workspaces, checks do repositório).
- O **Urbe** permanece em JavaScript; não há reescrita. Integração por adapters.
- Contratos que atravessam processos devem ser semanticamente independentes de linguagem (MANIFEST §4.3). Por isso os schemas da fundação são JSON Schema (ADR-0002).

## 3. Componentes e extração

O mapa canônico dos componentes é [`ecosystem.json`](ecosystem.json) (formato: [`docs/contracts/schemas/ecosystem.schema.json`](docs/contracts/schemas/ecosystem.schema.json)).

Nenhum componente compartilhado existe ainda. Para criar um, é obrigatório responder ao critério de MANIFEST §48 e às cinco perguntas de NN-022, registrar ADR e declarar em `ecosystem.json` `responsibility`, `contract`, `consumers`, `owners`, `compatibility` e `extractionReason` (verificado por `CHK-SHARED-DECLARATION`).

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

**Estado da fiscalização:** as regras acima são verificadas hoje sobre o **grafo declarado** em `ecosystem.json` (`CHK-BOUNDARIES`). A verificação sobre **referências reais de código** (csproj, package.json, imports) é planejada para a Fase 1, quando houver código importado (ver [`docs/governance/enforcement-matrix.json`](docs/governance/enforcement-matrix.json)).

Exceções exigem ADR e alteração explícita do check.

## 5. Autoridades (NN-001)

| Conceito | Autoridade canônica | Observações |
|----------|--------------------|-------------|
| Princípios e invariantes | `MANIFEST.md` | `AGENTS.md` reproduz; não substitui. |
| Componentes do ecossistema | `ecosystem.json` | Único manifest raiz (`CHK-SINGLE-AUTHORITY`). |
| Formato dos registros | `docs/contracts/schemas/*.schema.json` | O check valida contra eles; não duplica o formato. |
| Código, versão e histórico de Lunet2D/Urbe | repositórios de origem, enquanto `status = not-migrated` | Ver `source` e `version.authority` em `ecosystem.json`. |
| Mapeamento NN → fiscalização | `docs/governance/enforcement-matrix.json` | |
| Decisões pendentes/tomadas do proprietário | `docs/governance/decisions.json` | Decisão tomada aponta para ADR/SPEC/ROADMAP. |
| Decisões arquiteturais | `docs/adr/` | |
| Fases e escopo/IDs das tarefas | `ROADMAP.md` | Decidido em DEC-0003. |
| Estado vivo das tarefas (claimed, working, blocked…) | Issues do GitHub | DEC-0003; convenção em `docs/governance/communication.md` §8. |
| Registros de trabalho de agentes | `docs/governance/handoffs/` | |
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

Princípio: menor privilégio, deny-by-default quando aplicável, agentes não herdam todas as permissões do usuário. O permission model concreto ainda não existe (Fase 2).

## 8. Ainda NÃO decidido

Os itens abaixo são deliberadamente abertos. Nenhum agente deve tratá-los como decididos:

- tecnologia de UI e plataformas-alvo do Hub (Fase 3);
- protocolo e transporte de IPC (Fase 5);
- formato do `ComponentManifest` e do contrato de `Capability`, versionamento e permission model (Fase 2);
- estratégia de publicação do Urbe Web no ecossistema (decidida na auditoria/migração do Urbe, P1-10 — não presumir);
- formato dos registros canônicos de validação por build e das páginas `/testing/<componente>/<build>/` (P1-11);
- contrato de **Context** (Fases 2 e 5) e Host API do Product Shell (Fase 5);
- nome e formato do **Distribution Profile** e dos três eixos de disponibilidade: visibilidade, distribuição, modelo comercial (Fase 2; packaging na Fase 4);
- quais Services compartilhados (Identity, Catalog, Commerce, Entitlements, Downloads, Updates, Reviews, Creator Profiles, Notifications) existirão, se algum — só com consumidores reais, contrato e ADR (NN-020, NN-022);
- fonte canônica de catálogo, entitlements e identidade; política de edições e canais externos;
- como Product Shells serão estruturados no código de cada produto — depende dos inventários P1-1/P1-2 e não deve ser presumido;
- estrutura interna de `apps/`, `platform/`, `workspaces/` e `tools/` — os diretórios só serão criados quando tiverem conteúdo com função (ADR-0004).
