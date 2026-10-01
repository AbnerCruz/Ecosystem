# Estratégia de migração para o monorepo

> **Autoridade:** normativa para a Fase 1, subordinada a MANIFEST §11.2, §44 e às invariantes NN-012, NN-013 e NN-014.
> Esta é a **estratégia**. Ela vale para qualquer importação futura (nenhuma importação pode ocorrer antes de: inventário completo do produto, §2, e plano de importação revisado). O estado de cada produto (`status`) é o de [`ecosystem.json`](../../ecosystem.json); o resultado da migração de Lunet2D e Urbe está em [`audit-post-migration.md`](audit-post-migration.md) e no [plano](import-plan.md).

## 1. Origens

| Componente | Repositório de origem | Confirmado | Destino |
|------------|----------------------|------------|---------|
| `lunet2d` | https://github.com/AbnerCruz/Lunet2D | sim (DEC-0004) | `apps/lunet2d/` |
| `urbe` | https://github.com/AbnerCruz/Urbe | sim (DEC-0004) | `apps/urbe/` |

Fonte de verdade: campo `source` em [`ecosystem.json`](../../ecosystem.json). Enquanto `status = not-migrated`, o repositório de origem é a autoridade de código, versão e histórico (NN-001); com `status = active`, a autoridade é `apps/<id>/` e a origem é espelho de distribuição (DEC-0014-A).

## 2. Antes da migração — inventário obrigatório (MANIFEST §44.1)

Para cada produto, criar `docs/migration/inventory-<id>.md` a partir de [`inventory-template.md`](inventory-template.md), cobrindo: repositórios de origem, branches relevantes, tags, releases e artefatos, workflows, GitHub Pages, secrets/configuração necessária (**nomes apenas, nunca valores** — MANIFEST §30.2), dependências externas, arquivos normativos (SPEC, ROADMAP, AGENTS, ADRs), status atual e comandos de build/teste com o resultado atual (linha de base "antes").

**Inventários concluídos (2026-09-30):** [`inventory-lunet2d.md`](inventory-lunet2d.md) (P1-1) e [`inventory-urbe.md`](inventory-urbe.md) (P1-2). Os riscos deles alimentaram o [plano de importação](import-plan.md) (P1-3).

**Mapa funcional e arquitetural (ADD-0002 §16).** O inventário também registra, para cada subsistema importante, nome, responsabilidade atual, arquivos/diretórios, dependências, dados que possui, UI, se funciona standalone hoje, se é específico do Product e dependências externas e riscos. Acrescenta uma **classificação futura, explicitamente PROPOSTA** (Product Core, Product Shell, Tool, Workspace, Service, Library, Adapter ou ainda indeterminado). A classificação é insumo de inventário: **não autoriza extrair nada** (§7).

## 3. Preservação do histórico (NN-012)

**Decidido em DEC-0002: técnica A** (`git filter-repo --to-subdirectory-filter apps/<id>` + `merge --allow-unrelated-histories`, com `commit-map` versionado). Opções avaliadas:

| | A — `git filter-repo --to-subdirectory-filter` + merge | B — `git subtree add` sem `--squash` |
|--|--|--|
| Commits preservados | todos | todos |
| SHAs originais | reescritos (commit-map versionado) | preservados |
| `git log`/`blame` por path em `apps/<id>/` | funcionam direto | não atravessam a fusão |
| Tags | reimportadas com prefixo `<id>/` | importação manual com prefixo |

Decisão: **A**, com o `commit-map` gerado pelo filter-repo versionado em `docs/migration/` para rastrear SHAs antigos.

Em qualquer opção:

- a importação é feita a partir de um clone completo (`--mirror`/todas as branches relevantes), nunca de snapshot;
- tags relevantes recebem prefixo do produto para evitar colisão (`lunet2d/v0.x`, `urbe/v2.x`), preservando versões independentes (NN-014);
- os repositórios de origem permanecem intactos; arquivamento só por decisão do proprietário após §6.

## 4. Durante a migração — escopo restrito (NN-013, MANIFEST §44.2)

Proibido no PR de importação: reescrever Urbe ou Lunet; "limpar" código; alterar comportamento funcional; perder histórico ou release; misturar refatoração com importação. Proibido também **aproveitar a importação** para a arquitetura futura já conhecida (ADD-0002 §17): separar o Editor; extrair o Sprite Studio; reescrever o Agent Workspace; criar Store; criar um Product Shell novo; transformar código em capabilities; reorganizar tudo em packages; reescrever o Urbe em C#; mudar a arquitetura interna apenas porque a arquitetura futura é conhecida.

Permitido apenas o mínimo técnico inevitável para funcionar no novo path (ex.: caminhos em workflows), **cada item listado e justificado** no PR e no handoff. Um PR por produto.

## 5. Depois da migração — prova (MANIFEST §44.3)

Cada produto precisa provar, comparando com a linha de base do inventário: build, testes, workflows, release, documentação, paths, dados, links e processo de desenvolvimento. Mais:

- auditoria de histórico: número de commits e conjunto de tags da origem presentes no monorepo (P1-8);
- `ecosystem.json` atualizado (`status: active`, `version.authority` apontando para o arquivo de versão no monorepo) no mesmo PR;
- CI seletivo: mudança em `apps/<id>/**` dispara apenas os pipelines daquele produto (NN-014);
- architecture tests sobre referências reais de código (P1-7).

Somente então o repositório antigo pode ser marcado como legado/arquivado, **quando o proprietário decidir**.

## 6. Migração não é refatoração, extração nem redesenho (NN-013)

Os quatro trabalhos abaixo são itens **separados**, com PRs e handoffs separados, salvo necessidade técnica inevitável explicitamente documentada:

```text
MIGRATION   mover o projeto preservando comportamento e histórico
REFACTOR    reorganizar código sem mudar comportamento
EXTRACTION  tornar algo reutilizável (Tool, Workspace, Service, Library…)
REDESIGN    mudar arquitetura, UX ou modelo de dados
```

Nesta fase só existe MIGRATION. REFACTOR, EXTRACTION e REDESIGN começam depois do gate da Fase 1 e exigem inventário classificado, consumidor real, contrato e ADR (NN-020, NN-022).

## 7. Sequência obrigatória (ADD-0002 §17)

```text
1. INVENTARIAR                          (P1-1, P1-2: incluem o mapa funcional e arquitetural)
2. IMPORTAR                             (P1-4, P1-5)
3. PRESERVAR HISTÓRICO                  (NN-012, §3)
4. RESTAURAR BUILD                      (P1-6)
5. RESTAURAR TESTES                     (P1-6)
6. RESTAURAR RELEASES                   (P1-6, NN-014, NN-023)
7. VALIDAR PRODUTO                      (inclui validação humana quando aplicável — NN-017)
8. PROVAR AUSÊNCIA DE REGRESSÃO CONHECIDA   (P1-8)
9. CLASSIFICAR CANDIDATOS               (P1-12: proposta, sem extração)
10. EXTRAIR/MODERNIZAR GRADUALMENTE     (fora da Fase 1; cada extração com ADR)
```

Nenhum passo pode ser pulado nem antecipado. Os passos 9 e 10 não fazem parte da importação.

## 8. Gates da Fase 1

1. Gate atual (ROADMAP): Lunet2D e Urbe vivem no monorepo sem regressão conhecida e continuam possuindo lifecycles próprios.
2. **Gate adicional (ADD-0002 §18):** a importação preservou intencionalmente a arquitetura funcional existente. Candidatos a Product Core, Product Shell, Tool, Workspace, Service, Library e Adapter foram inventariados, porém nenhuma extração estrutural foi realizada como efeito colateral da migração.

O gate adicional **não pode ser marcado como concluído antes da auditoria pós-migração** (P1-8).

## 9. Canal de releases e publicação durante a migração — TRANSITÓRIO (DEC-0008 e DEC-0009, alternativa A)

> Decisões do proprietário ([ADD-0004](../governance/addenda/ADD-0004-decisoes-0008-0009-e-resposta-pelo-portal.md)), **válidas para a migração (Fase 1)**. Depois, o próprio Ecosystem declara como cada produto é distribuído e publicado (Distribution Profile, Registry e Launcher — Fases 2 e 4) e substitui esta seção. Por isso **nenhum campo novo** foi adicionado a `ecosystem.json` por causa delas.

**Releases (DEC-0008-A).** O pipeline do monorepo constrói e **publica as releases de cada produto nos repositórios de origem** (`AbnerCruz/Lunet2D`, `AbnerCruz/Urbe`):

- `releases/latest`, tags e releases continuam **por produto**, no formato atual (`v0.0.1-dev.N`, `v1.8.2-beta`…), em cada repositório de origem. Os apps instalados do Urbe continuam atualizando **sem nenhuma mudança de código** (R-URB-1 deixa de ser bloqueio); a mudança de updater é proibida na migração (NN-013).
- As tags **importadas** para o monorepo seguem DEC-0002 (prefixo `<id>/`); isso não afeta as tags publicadas nos repositórios de origem.
- Os repositórios de origem **permanecem vivos** como canal de distribuição: **não arquivar** (um repositório arquivado não recebe releases) e **sem desenvolvimento novo**: depois da importação, o código só evolui no monorepo (NN-001).
- **Mecanismo (decidido em DEC-0014-A):** o monorepo **não** publica nas origens (isso exigiria token cross-repo e recriar os secrets de assinatura). Cada origem **puxa** `apps/<id>` (`.github/origin-sync/`, [`import-plan.md`](import-plan.md) §7) e continua construindo e publicando com seus próprios pipelines e secrets; o resultado para o usuário é o descrito acima (releases por produto, na origem). Disparo da sincronização: DEC-0017-A.
- O `versionCode` do Android do Lunet2D hoje é o `run_number` do CI do repositório de origem (R-LUN-1): no desenho proposto a origem continua gerando o número, então **a sequência continua sozinha** (sem offset).

**Urbe Web (DEC-0009-A).** O Urbe Web **continua sendo servido por `AbnerCruz/Urbe`** (Pages): o pipeline do monorepo publica o **site estático** gerado de `apps/urbe/` no Pages desse repositório. URL, escopo do service worker e PWAs instalados **não mudam**. O mecanismo exato é detalhado em [`import-plan.md`](import-plan.md) §7.4 (proposta: a `main` da origem passa a ser um espelho de `apps/urbe`, sem trocar a fonte do Pages); o conteúdo publicado é artefato **gerado** e declara sua fonte no commit (NN-001).

**O que esta seção não decide:** filtros de caminho dos workflows e a adaptação mínima dos checks do Urbe que leem `.github/workflows` (R-URB-3). São itens do plano P1-3.

