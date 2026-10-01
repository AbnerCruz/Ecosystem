# Plano de importação de Lunet2D e Urbe (P1-3)

> **Autoridade:** normativa para as tarefas P1-4 a P1-8, subordinada a MANIFEST §11.2 e §44, NN-012, NN-013, NN-014, NN-023, ao [`README.md`](README.md) desta pasta e às decisões DEC-0002, DEC-0004, DEC-0008 e DEC-0009.
> **Estado:** **plano. Nada foi importado.** A execução depende da decisão de aprovação **DEC-0014** (pendente, visível no portal).
> Derivado dos inventários [`inventory-lunet2d.md`](inventory-lunet2d.md) e [`inventory-urbe.md`](inventory-urbe.md). Legenda: **FATO** verificado em ensaio (2026-10-01, diretório temporário); **PROPOSTA** do agente; **NÃO VERIFICADO** depende de comportamento do GitHub que só a primeira execução real confirma (§7.7).

## 1. Princípio: só decisões para o proprietário

Este plano **não** exige nenhuma ação manual do proprietário além de **decidir** (aprovar o plano, DEC-0014). Tudo o que a versão anterior pedia foi automatizado ou eliminado por desenho:

| Antes (ação do proprietário) | Agora | Como |
|------------------------------|-------|------|
| Congelar o desenvolvimento nas origens | **Eliminada** | A importação é **determinística e incremental** (FATO, §5): dá para reimportar quantas vezes for preciso e só o delta entra. O corte verifica que nada ficou para trás. |
| Fechar ou mergear os 10 PRs do Dependabot do Urbe | **Automatizada** | Um passo de corte, no repositório de origem (com o próprio `GITHUB_TOKEN` dele), remove o `dependabot.yml` da origem e fecha esses PRs com um comentário apontando para o monorepo (§7.6). |
| Criar token de publicação cross-repo | **Eliminada** | Nenhum workflow do monorepo escreve nas origens. As origens **puxam** o que precisam (§7.1) e publicam com o token delas. |
| Recriar os secrets de assinatura do Android do Urbe | **Eliminada** | O build assinado continua **no repositório do Urbe**, onde os secrets já estão. A chave nunca sai de lá. |
| Trocar a fonte do Pages do Urbe | **Eliminada** | A `main` da origem continua sendo a fonte do Pages: ela passa a ser um **espelho** de `apps/urbe`, então o Pages (modo atual) segue servindo o mesmo site na mesma URL (§7.4). |

**Limite honesto:** nenhuma automação consegue criar tokens, secrets ou alterar configurações de repositório (o GitHub reserva isso ao dono; confirmado: esta sessão recebe `403` nesses caminhos). O desenho evita precisar deles; se algum detalhe do GitHub se comportar diferente do esperado na primeira execução (§7.7), o agente **para e leva uma decisão ao portal**, em vez de pedir uma tarefa manual.

## 2. Princípios que o plano obedece

1. **Importar não é refatorar** (NN-013; ADD-0002 §17): dentro de `apps/<id>/` **nada** muda.
2. **Histórico completo** (NN-012; DEC-0002-A): `git filter-repo --to-subdirectory-filter`, merge de histórias não relacionadas, tags com prefixo, `commit-map` versionado.
3. **Dois PRs por produto**: **A) importação** (P1-4/P1-5) e **B) distribuição automatizada** (P1-6).
4. **A origem continua sendo o canal de distribuição** (DEC-0008-A, DEC-0009-A, **transitórias** até o Ecosystem declarar a distribuição) e **os repositórios de origem não são arquivados**.
5. Cada ajuste técnico inevitável é listado (§8); nenhum fica dentro de `apps/<id>/`.

## 3. Sequência

```text
DEC-0014 (aprovação) ─► P1-4 PR A Lunet2D ─► P1-5 PR A Urbe ─► P1-6 PR B (distribuição automatizada) por produto
                        ─► P1-7 architecture tests ─► P1-8 auditoria pós-migração ─► P1-9/P1-10 portal
```

Cada PR A é independente e reversível (revert do merge preserva o histórico).

## 4. Procedimento de importação (PR A)

Comandos executados no ensaio (§5). `<Repo>` = `Lunet2D` | `Urbe`; `<id>` = `lunet2d` | `urbe`.

```bash
git clone --mirror https://github.com/AbnerCruz/<Repo> <Repo>.git && cd <Repo>.git
git for-each-ref --format='delete %(refname)' refs/pull | git update-ref --stdin     # PRs do GitHub não são histórico
git filter-repo --force --to-subdirectory-filter apps/<id> --tag-rename ":<id>/"      # commit-map em filter-repo/commit-map
# no monorepo, branch migration/<id>
git fetch f-<id> refs/heads/main:refs/remotes/f-<id>/main "refs/tags/<id>/*:refs/tags/<id>/*"
git merge --allow-unrelated-histories --no-ff -m "Importar <Repo> (histórico preservado)" f-<id>/main
```

- **Fusão do PR por *merge commit*** (nunca *squash*/*rebase*: reescreveriam o histórico e invalidariam o `commit-map`).
- O PR A altera **apenas**: histórico importado, `ecosystem.json` (§6) e `docs/migration/commit-map-<id>.txt`. **Nenhum arquivo dentro de `apps/<id>/`.**
- **Corte sem congelamento:** a importação roda de novo imediatamente antes do corte; se a origem ganhou commits, entram só eles (§5, T1b). O PR A registra o `HEAD` da origem importado e a verificação de que a origem não avançou depois.
- **Tags:** as sessões de agente não conseguem empurrar tags (o proxy responde 403; FATO em 2026-10-01). O PR A traz `docs/migration/tags-<id>.txt` (tag → SHA) e o workflow manual `restore-tags.yml`, que recria as tags com o `GITHUB_TOKEN` do repositório depois do merge, sem mover nenhuma existente. Zero ação do proprietário.
- Branches: ver §9.

## 5. Ensaios executados (FATO, 2026-10-01, em `/tmp`, sem tocar no Ecosystem nem nas origens)

**Importação (T0):**

| Verificação | Lunet2D | Urbe |
|-------------|---------|------|
| Commits em `main` na origem → após filtro | 84 → **84** | 382 → **382** |
| Tags (prefixadas) | 25 → **25** (`lunet2d/v0.0.1-dev.N`) | 6 → **6** (`urbe/v1.7.0-beta`…) |
| Commits no monorepo de teste (base 20) | +84 | +382 → **488** no total (inclui 2 merges), igual ao esperado |
| Hash da árvore `apps/<id>` × árvore da `main` de origem | `db9f6cd5…` = `db9f6cd5…` **idêntico** | `8f4f53db…` = `8f4f53db…` **idêntico** |
| Testes **no novo caminho** | `dotnet test` em `apps/lunet2d`: **286/286** | `npm run check` em `apps/urbe`: **56/56** |
| Checks do Ecosystem | só falha `CHK-SINGLE-AUTHORITY` (esperado) → **16/16** depois de atualizar `ecosystem.json` | idem |

**Determinismo e incremental (T1):** duas execuções independentes de `filter-repo` sobre o mesmo repositório produzem **o mesmo SHA** (`1fe8e82f…`, igual ao da importação anterior). Uma origem com +1 commit produz uma `main` filtrada cujo **pai é exatamente a `main` já importada**; o merge incremental traz **1** commit. Logo, o congelamento é desnecessário.

**Espelho da origem (T2, Urbe):** com `git archive` + `tar` (sem `rsync`, que nem existe no ambiente de teste), excluindo `.git` e `.github`:

| Verificação | Resultado |
|-------------|-----------|
| Espelhar logo após a importação | **0 arquivos alterados** (a árvore importada é idêntica à da origem fora de `.github`) |
| Mudança + remoção de arquivo no monorepo | o espelho propaga **só** essas duas alterações, em um commit do bot |
| Sincronizar de novo o mesmo commit | **idempotente** (0 alterações) |
| `.github/` original da origem | **intacto** (diff vazio) |
| Checagens originais do Urbe no espelho | `tools/version.mjs check` OK; `tests/workflows.mjs` OK; `tests/consistency.mjs` OK |
| Commit humano na origem | **detectado** como deriva (último autor ≠ bot) |

Outros achados: o filtro leva `.github/` do produto para `apps/<id>/.github/` (**workflows inertes**, §7.5); o hash da árvore idêntico é o critério verificável de "migração sem alteração" (NN-013); os testes do Lunet2D acharam a raiz certa no novo caminho.

## 6. `ecosystem.json` no PR A

```json
"status": "active",
"version": { "authority": "version-file", "file": "apps/lunet2d/VERSION" }   // Urbe: "apps/urbe/package.json"
```

`source` permanece como proveniência. Nenhum campo de release/publicação é adicionado: por DEC-0008 e DEC-0009 (transitórias) quem declarará isso é o próprio Ecosystem (Fases 2 e 4).

## 7. Distribuição automatizada (PR B, P1-6)

### 7.1 Desenho: a origem é um **espelho de distribuição** que puxa do monorepo

O monorepo é a **única autoridade do código** (NN-001). O repositório de origem de cada produto passa a ser uma **projeção** dele, com a fonte declarada no commit (`Ecosystem-Commit: <sha>`), e continua construindo e publicando **com os pipelines e secrets que já tem**.

- **Sincronização puxada** (workflow novo na origem, `sync-from-ecosystem.yml`, agendado a cada 10 min e com `workflow_dispatch`; modelo e script em [`.github/origin-sync/`](../../.github/origin-sync/), componente `origin-sync`. **O script não é copiado para a origem**: o workflow o busca do Ecosystem a cada execução, mantendo uma só autoridade, NN-001): lê o monorepo (público, **sem token**), detecta mudança em `apps/<id>/**`, **espelha a árvore** (excluindo `.git` e `.github`) em um commit do `github-actions[bot]` na `main` da origem e dispara o pipeline de release da própria origem por `workflow_dispatch` (permitido ao `GITHUB_TOKEN`).
- **Deriva:** se a origem recebe um commit **não-bot que toque fora de `.github/`**, o workflow para e abre uma Issue na origem (a origem deixou de ser o lugar de desenvolver).
- **Keepalive:** o commit de sincronização conta como atividade e evita que o GitHub desative o agendamento após 60 dias sem atividade.
- Nenhum workflow do monorepo escreve nas origens: **nenhum token cross-repo** existe.

**Ensaio do script (FATO, 2026-10-01, origem simulada com clones locais e `gh` falso):** primeira execução com espelho idêntico registra só a âncora (commit com `Ecosystem-Commit` e `Ecosystem-Tree`, sem disparar release); segunda execução não faz nada; mudança + remoção + nova pasta no `apps/urbe` do Ecosystem são espelhadas; uma tag `urbe/vX` pendente gera commit espelhado da árvore daquela tag, tag `vX` na origem e `gh workflow run release.yml --ref vX`, e depois a `main` é sincronizada; commit humano na origem é detectado como deriva (Issue aberta, exit 1, nada empurrado). Mesmo ensaio no Lunet2D (`ci-dispatch`): espelho idêntico, âncora sem disparo.

**Tolerância à reinstalação (ADD-0006):** o proprietário aceita reinstalar os apps beta durante a Fase 1. O desenho **não depende** disso (chave, `versionCode` e `releases/latest` ficam na origem), mas uma falha de continuidade de atualização deixa de ser bloqueio.

### 7.2 Lunet2D
- A origem mantém o `ci.yml` (test → apk → release). Como o `run_number` é o **da própria origem**, o `versionCode` e as tags `v0.0.1-dev.N` **continuam a sequência naturalmente** (a última é `dev.103`): R-LUN-1 e R-LUN-2 deixam de existir, sem offset.
- **Edição necessária na origem (única):** o job `release` só roda em `push`, e pushes do `GITHUB_TOKEN` não disparam workflows. A condição passa a aceitar também `workflow_dispatch` (`(github.event_name == 'push' || github.event_name == 'workflow_dispatch') && github.ref == 'refs/heads/main'`). É mudança de infraestrutura na origem, feita **depois** da importação (não entra no histórico importado), listada em §8.
- O fingerprint e o keystore de desenvolvimento seguem onde estão (R-LUN-4).
- **Latência:** release típica **10–15 min** depois do merge (agendamento + CI), contra ~3 min hoje. Está explicitado em DEC-0014.

### 7.3 Urbe (releases)
- A origem mantém **intactos** `release.yml`, `build-apps.yml`, `app.yml` e `structural-checks.yml`. Disparo de release: o monorepo recebe a tag `urbe/v<versão>` (como hoje se cria `v<versão>`); o sync detecta, espelha o código **daquela** revisão, cria a tag `v<versão>` **no commit espelhado** e dispara `release.yml` sobre essa tag. Assim `verificar` (tag = `package.json`), testes, build, assinatura (secrets existentes) e `make_latest` rodam como hoje e **a tag aponta para o código realmente construído** (resolve a lacuna de proveniência da versão anterior do plano).
- Os apps instalados continuam lendo `releases/latest` de `AbnerCruz/Urbe`, sem nenhuma mudança (R-URB-1 eliminada).

### 7.4 Urbe Web (DEC-0009-A)
- O Pages da origem continua no modo atual (*deploy from a branch*, `main`). Como a `main` da origem é o espelho de `apps/urbe`, **o mesmo conteúdo continua sendo servido, na mesma URL, com o mesmo escopo de service worker e PWAs instalados**. Nenhuma configuração muda.
- **NÃO VERIFICADO:** que um push do `GITHUB_TOKEN` dispare a construção do Pages (§7.7). Plano B automático: pedir a construção pela API de Pages no mesmo workflow.

### 7.5 Workflows inertes e checks (R-URB-3)
Os workflows importados ficam **inertes** em `apps/<id>/.github/` (o GitHub só executa os da raiz do repositório); os testes do Urbe passam porque validam essas cópias. O monorepo ganha **workflows ativos na raiz** apenas de **CI** por produto (`lunet2d-ci.yml`, `urbe-checks.yml`: testes e build **sem assinatura**, com filtro `apps/<id>/**`, NN-014) e Dependabot na raiz (`/apps/urbe`, `/apps/urbe/native/android`, `/apps/lunet2d`, `/`). **PROPOSTA:** uma verificação no Ecosystem das regras dos workflows reais de origem (REQ-006/066 do Urbe), já que os testes do Urbe passam a ver só as cópias inertes.

### 7.6 Passo de corte (uma vez por produto, depois do PR A)
Feito por mim com as credenciais de sessão já anexadas, ou pelo próprio `sync` no primeiro disparo: (1) instalar `sync-from-ecosystem.yml` na origem; (2) Lunet2D: ajustar a condição do job `release` (§7.2); (3) Urbe: remover `.github/dependabot.yml` da origem e fechar os PRs do Dependabot (#37–#46) com comentário apontando para o monorepo; (4) primeira sincronização, que deve **não alterar nada** (T2: 0 arquivos), provando que o espelho coincide com a origem.

### 7.6.1 Execução real (FATO, 2026-10-01)

- **Lunet2D:** PR #26 em `AbnerCruz/Lunet2D` (workflow de sincronização + `workflow_dispatch` no job `release`) com CI verde e fundido. A primeira execução real do espelho (`workflow_dispatch`, execução 36811164722) criou **só o commit de âncora** `4d6d5ba` do `github-actions[bot]`, com `Ecosystem-Tree: db9f6cd5…` (a árvore do produto, igual à da origem): a recusa por divergência não foi acionada e **nenhum** release foi disparado. Prova que o `GITHUB_TOKEN` da origem consegue empurrar na `main` e que o script roda no runner.
- **Urbe:** PR #48 em `AbnerCruz/Urbe` **falhou no CI do próprio produto**: `tests/workflows.mjs` (REQ-006/REQ-066) só admite `contents: write` em `release.yml` e exige `permissions: contents: read` no topo. O espelho precisa de escrita para empurrar. Não há como contornar sem alterar a checagem do produto; levado ao proprietário em **DEC-0016** (ajuste mínimo vs. publicar do monorepo). Até lá o PR #48 fica aberto e o Urbe só está importado.

### 7.7 O que só a primeira execução real confirma
Comportamentos do GitHub **NÃO VERIFICADOS** nesta sessão (sem acesso a configurações; `docs.github.com` bloqueado): (a) push do `GITHUB_TOKEN` dispara a construção do Pages (se não, plano B da API); (b) o intervalo real do agendamento (mínimo 5 min; pode atrasar); (c) `workflow_dispatch` do `GITHUB_TOKEN` sobre o `ci.yml`/`release.yml` da origem (comportamento documentado, mas a ser provado). **Se algum falhar, o agente para, registra o achado e leva uma decisão ao portal.**

## 8. Ajustes técnicos inevitáveis (NN-013), consolidados

| # | Ajuste | PR / momento | Dentro de `apps/<id>/`? |
|---|--------|--------------|------------------------|
| 1 | `ecosystem.json`: status e autoridade de versão (§6) | A | não |
| 2 | `docs/migration/commit-map-<id>.txt`, `docs/migration/tags-<id>.txt` e `.github/workflows/restore-tags.yml` (tags, §4) | A | não |
| 3 | CI por produto na raiz com filtro de caminho; Dependabot na raiz (§7.5) | B | não |
| 4 | `sync-from-ecosystem.yml` em cada origem (§7.1) | corte | não (é na origem) |
| 5 | Lunet2D: condição do job `release` aceita `workflow_dispatch` (§7.2) | corte | não (é na origem) |
| 6 | Urbe: remover `dependabot.yml` da origem e fechar os PRs do Dependabot (§7.6) | corte | não (é na origem) |
| 7 | Verificação dos workflows reais de origem (§7.5) | B | não |
| 8 | Urbe (condicionado a DEC-0016-A): `tools/check-workflows.mjs` e `tests/workflows.mjs` admitem `sync-from-ecosystem.yml` com `contents: write` só em nível de job | B | **sim** (2 arquivos do produto) |
| 9 | Lunet2D: aviso de espelho no topo de `apps/lunet2d/README.md` (documentação; feito depois da importação, em PR separado, e também para exercitar o espelho ponta a ponta) | B | **sim** (1 arquivo do produto) |

**Nada** dentro de `apps/<id>/` é alterado na Fase 1. Dívidas preexistentes (token Git em texto claro, `xunit.v3 *`, ações por tag, README e PROGRAM do Urbe desatualizados) **não** são corrigidas.

## 9. Branches, Issues e PRs de origem

- **Lunet2D:** as 3 branches estão incorporadas à `main`: **não importar**.
- **Urbe:** `claude/beta-v1` e `claude/mundo-real` contêm só merges de PR (#16/#17); `claude/fix-explorer-celular` tem 1 commit com **patch idêntico** ao `0cf0da8` da `main` (`git patch-id`). **PROPOSTA:** importá-las como `archive/urbe/<nome>` (custo desprezível, preserva tudo); as 27 branches `claude/*`/`chore/*` já são ancestrais da `main` e as 10 `dependabot/*` são propostas automáticas: **não importar**.
- **Issues e PRs não fazem parte do histórico git**: `filter-repo` não os leva (R-LUN-7, R-URB-7). Hoje (FATO, 2026-10-01): **Lunet2D** 0 Issues e 25 PRs fechados; **Urbe** 1 Issue aberta (#33) e **10 PRs abertos do Dependabot** (#37–#46). A origem permanece viva (DEC-0008-A): **nada é apagado**; os PRs do Dependabot são fechados pelo passo de corte (§7.6). Onde ficam as Issues **novas** de cada produto: **DEC-0013-A (decidida)** — nos repositórios de origem; o Ecosystem fica só com as Issues do próprio Ecosystem e da migração. A Issue #33 do Urbe permanece onde está.

## 10. Decisão de aprovação e critérios de aceite

**DEC-0014** (no portal): aprovar o plano automatizado (recomendado), aprová-lo na variante com pipeline no monorepo (exige que você crie token e secrets e troque o Pages) ou pedir mudanças.

**Aceite do PR A de cada produto (evidência no handoff):** commits e tags presentes (contagens do §5 e `commit-map`); hash da árvore `apps/<id>` idêntico ao da `main` de origem; testes do produto no novo caminho iguais à linha de base (286 / 56); `ecosystem.json` atualizado e os checks do Ecosystem verdes; **nenhum** arquivo dentro de `apps/<id>/` editado; fusão por *merge commit*; origem sem commits novos depois do corte.
**Aceite do PR B e do corte:** primeira sincronização sem alterações; CI por produto só dispara pelos caminhos do produto; uma release de ensaio construída pela origem a partir do espelho **sem afetar `releases/latest` real** (ou com a próxima release real); instalação e atualização em aparelho (NN-017, DEVICE) — essa validação, que só uma pessoa faz, entra no portal com o objeto.

## 11. O que este plano não faz

Não extrai Editor, Sprite Studio ou Agent Workspace; não cria Store, Product Shell nem capabilities; não reescreve o Urbe; não altera updaters, formatos de dados ou arquitetura interna; não muda o código de `apps/<id>/` (ADD-0002 §17).
