# Estratégia de migração para o monorepo

> **Autoridade:** normativa para a Fase 1, subordinada a MANIFEST §11.2, §44 e às invariantes NN-012, NN-013 e NN-014.
> Nenhuma importação pode ocorrer antes de: inventário completo do produto (§2), DEC-0002 e DEC-0004 decididas, e plano de importação revisado.

## 1. Origens

| Componente | Repositório de origem | Confirmado | Destino |
|------------|----------------------|------------|---------|
| `lunet2d` | https://github.com/AbnerCruz/Lunet2D | não (DEC-0004) | `apps/lunet2d/` |
| `urbe` | https://github.com/AbnerCruz/Urbe | não (DEC-0004) | `apps/urbe/` |

Fonte de verdade: campo `source` em [`ecosystem.json`](../../ecosystem.json). Enquanto `status = not-migrated`, o repositório de origem é a autoridade de código, versão e histórico (NN-001).

## 2. Antes da migração — inventário obrigatório (MANIFEST §44.1)

Para cada produto, criar `docs/migration/inventory-<id>.md` a partir de [`inventory-template.md`](inventory-template.md), cobrindo: repositórios de origem, branches relevantes, tags, releases e artefatos, workflows, GitHub Pages, secrets/configuração necessária (**nomes apenas, nunca valores** — MANIFEST §30.2), dependências externas, arquivos normativos (SPEC, ROADMAP, AGENTS, ADRs), status atual e comandos de build/teste com o resultado atual (linha de base "antes").

## 3. Preservação do histórico (NN-012)

Técnica a decidir em **DEC-0002**. Opções avaliadas:

| | A — `git filter-repo --to-subdirectory-filter` + merge | B — `git subtree add` sem `--squash` |
|--|--|--|
| Commits preservados | todos | todos |
| SHAs originais | reescritos (commit-map versionado) | preservados |
| `git log`/`blame` por path em `apps/<id>/` | funcionam direto | não atravessam a fusão |
| Tags | reimportadas com prefixo `<id>/` | importação manual com prefixo |

Recomendação registrada em DEC-0002: **A**, com o `commit-map` gerado pelo filter-repo versionado em `docs/migration/` para rastrear SHAs antigos.

Em qualquer opção:

- a importação é feita a partir de um clone completo (`--mirror`/todas as branches relevantes), nunca de snapshot;
- tags relevantes recebem prefixo do produto para evitar colisão (`lunet2d/v0.x`, `urbe/v2.x`), preservando versões independentes (NN-014);
- os repositórios de origem permanecem intactos; arquivamento só por decisão do proprietário após §6.

## 4. Durante a migração — escopo restrito (NN-013, MANIFEST §44.2)

Proibido no PR de importação: reescrever Urbe ou Lunet; "limpar" código; alterar comportamento funcional; perder histórico ou release; misturar refatoração com importação.

Permitido apenas o mínimo técnico inevitável para funcionar no novo path (ex.: caminhos em workflows), **cada item listado e justificado** no PR e no handoff. Um PR por produto.

## 5. Depois da migração — prova (MANIFEST §44.3)

Cada produto precisa provar, comparando com a linha de base do inventário: build, testes, workflows, release, documentação, paths, dados, links e processo de desenvolvimento. Mais:

- auditoria de histórico: número de commits e conjunto de tags da origem presentes no monorepo (P1-8);
- `ecosystem.json` atualizado (`status: active`, `version.authority` apontando para o arquivo de versão no monorepo) no mesmo PR;
- CI seletivo: mudança em `apps/<id>/**` dispara apenas os pipelines daquele produto (NN-014);
- architecture tests sobre referências reais de código (P1-7).

Somente então o repositório antigo pode ser marcado como legado/arquivado, **quando o proprietário decidir**.
