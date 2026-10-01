# Plano de importação de Lunet2D e Urbe (P1-3)

> **Autoridade:** normativa para as tarefas P1-4 a P1-8, subordinada a MANIFEST §11.2 e §44, NN-012, NN-013, NN-014, NN-023, ao [`README.md`](README.md) desta pasta e às decisões DEC-0002, DEC-0004, DEC-0008 e DEC-0009.
> **Estado:** **plano. Nada foi importado.** A importação só começa depois da revisão do proprietário (§10).
> Derivado dos inventários [`inventory-lunet2d.md`](inventory-lunet2d.md) e [`inventory-urbe.md`](inventory-urbe.md). Legenda: **FATO** verificado (ensaio de 2026-10-01 em diretório temporário); **PROPOSTA** do agente, dentro do que as decisões já tomadas permitem; **AÇÃO DO PROPRIETÁRIO** coisa que nenhum agente pode fazer.

## 1. Princípios que o plano obedece

1. **Importar não é refatorar** (NN-013; ADD-0002 §17): dentro de `apps/<id>/` **nada** muda na importação.
2. **Histórico completo** (NN-012; DEC-0002-A): `git filter-repo --to-subdirectory-filter` + merge de histórias não relacionadas, tags com prefixo, `commit-map` versionado.
3. **Dois PRs por produto**, em ordem: **A) importação** (P1-4/P1-5) e **B) restauração de pipelines** (P1-6). Misturá-los tornaria impossível provar que a importação não alterou o produto.
4. **Os repositórios de origem não são alterados** até a decisão do proprietário (DEC-0002; MANIFEST §44.3) e, por DEC-0008-A, **permanecem vivos como canal de distribuição**.
5. Cada ajuste técnico inevitável é listado (§8) e fica fora de `apps/<id>/`.

## 2. Sequência

```text
Pré-condições (§3, proprietário) ─► P1-4 PR A Lunet2D ─► P1-5 PR A Urbe ─► P1-6 PR B (pipelines) por produto
                                    ─► P1-7 architecture tests ─► P1-8 auditoria pós-migração ─► P1-9/P1-10 portal
```

Cada PR A é **independente** (um por produto) e pode ser revertido sem afetar o outro.

## 3. Pré-condições e ações do proprietário

| # | Ação | Quando | Observação |
|---|------|--------|------------|
| 1 | **Congelar o desenvolvimento** nos repositórios de origem durante a janela de importação (nenhum commit novo em `main`) | antes de cada PR A | Commits depois do congelamento não entram na importação. O Urbe tem 10 PRs do Dependabot abertos e a sessão de agentes pode estar ativa; o plano usa o `HEAD` da `main` no momento da importação e o registra. |
| 2 | Decidir o que fazer com os **10 PRs abertos do Dependabot** (#37–#46) do Urbe | antes do PR A do Urbe | PRs não migram (R-URB-7). Fechá-los ou mergeá-los antes evita perda; o Dependabot será reconfigurado na raiz em P1-6. |
| 3 | Criar o **token de publicação** (acesso a *contents* e *releases* **somente** de `AbnerCruz/Lunet2D` e `AbnerCruz/Urbe`) e guardá-lo como secret do repositório Ecosystem | antes do PR B | Nome proposto: `ORIGIN_RELEASES_TOKEN`. Necessário para DEC-0008-A (releases) e DEC-0009-A (site). Nenhum agente cria nem lê esse valor. |
| 4 | Recriar os secrets de assinatura do Android do Urbe no repositório Ecosystem: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD` | antes do PR B do Urbe | R-URB-5. **Trocar a chave impede a atualização dos APKs instalados.** |
| 5 | Mudar a **fonte do Pages** de `AbnerCruz/Urbe` para a branch `gh-pages` (uma vez) | durante o PR B do Urbe | DEC-0009-A (§7.3). A URL não muda. |

## 4. Procedimento de importação (PR A)

Comandos **reproduzíveis**, executados no ensaio (§5). `<Repo>` = `Lunet2D` | `Urbe`; `<id>` = `lunet2d` | `urbe`.

```bash
# 1. clone espelho e remoção de refs que não são histórico (PRs do GitHub)
git clone --mirror https://github.com/AbnerCruz/<Repo> <Repo>.git && cd <Repo>.git
git for-each-ref --format='delete %(refname)' refs/pull | git update-ref --stdin

# 2. reescrever o histórico para apps/<id>/ e prefixar as tags
git filter-repo --force --to-subdirectory-filter apps/<id> --tag-rename ":<id>/"
#    -> filter-repo/commit-map (SHA antigo -> novo) deve ser versionado em docs/migration/commit-map-<id>.txt

# 3. no monorepo, em uma branch migration/<id>
git remote add f-<id> <caminho>/<Repo>.git
git fetch f-<id> refs/heads/main:refs/remotes/f-<id>/main "refs/tags/<id>/*:refs/tags/<id>/*"
git merge --allow-unrelated-histories --no-ff -m "Importar <Repo> (histórico preservado)" f-<id>/main
```

Regras:

- O PR é fundido com **"Create a merge commit"**: *squash* e *rebase* **reescrevem** o histórico e invalidam o `commit-map`.
- O PR A altera **apenas** (a) o histórico importado, (b) `ecosystem.json` (§6) e (c) `docs/migration/commit-map-<id>.txt` e a evidência da auditoria. **Nenhum arquivo dentro de `apps/<id>/` é editado.**
- Branches de origem: ver §9.

## 5. Ensaio executado (FATO, 2026-10-01, em `/tmp`, sem tocar no Ecosystem nem nas origens)

| Verificação | Lunet2D | Urbe |
|-------------|---------|------|
| Commits em `main` na origem → após filtro | 84 → **84** | 382 → **382** |
| Tags na origem → após filtro (prefixadas) | 25 → **25** (`lunet2d/v0.0.1-dev.N`) | 6 → **6** (`urbe/v1.7.0-beta`…`urbe/v1.8.2-beta`) |
| Commits no monorepo de teste (base 20) | +84 (+1 merge) | +382 (+1 merge) = **488** no total, igual ao esperado |
| Hash da árvore `apps/<id>` no monorepo × árvore da `main` de origem | `db9f6cd5…` = `db9f6cd5…` **idêntico** | `8f4f53db…` = `8f4f53db…` **idêntico** |
| Testes **no novo caminho** | `dotnet test --project tests/Lunet.Tests` em `apps/lunet2d`: **286/286** | `npm run check` em `apps/urbe`: **56/56**, "todas as checagens passaram" |
| Checks do Ecosystem com os produtos importados | só falha `CHK-SINGLE-AUTHORITY` (esperado: `ecosystem.json` ainda diz `not-migrated`) | idem |
| Checks do Ecosystem depois de atualizar `ecosystem.json` (§6) | **16/16** passam (inclui `CHK-BOUNDARIES` e `CHK-SECRETS`, sem falsos positivos) | idem |
| Refs descartadas | 25 `refs/pull/*` | refs `refs/pull/*` |

Consequências do ensaio:

- **O hash da árvore idêntico é o critério verificável de "migração sem alteração"** (NN-013): o PR A deve reproduzi-lo.
- O risco de os testes do Lunet localizarem a raiz errada (R-LUN, "provar depois da importação") **não se confirmou**: o ancestral com `ROADMAP.md` mais próximo é o do Lunet.
- O Urbe passa 56/56 porque o filtro leva a pasta `.github/` do produto para `apps/urbe/.github/`: **esses workflows ficam inertes** (o GitHub só executa os da raiz do repositório). Isso muda a estratégia de R-URB-3 (§7.4).
- `git log -- apps/<id>` mostra menos commits que o total (58 e 336) porque a simplificação de merges do `git log` omite merges sem diferença; os commits estão todos no histórico.

## 6. `ecosystem.json` no PR A

Para cada produto, no mesmo PR (NN-001, NN-021):

```json
"status": "active",
"version": { "authority": "version-file", "file": "apps/lunet2d/VERSION" }   // Urbe: "apps/urbe/package.json"
```

`source` permanece como proveniência. Não se adiciona nenhum campo de release/publicação: por DEC-0008 e DEC-0009 (transitórias) quem passará a declarar isso é o próprio Ecosystem (Fases 2 e 4).

## 7. Restauração de pipelines (PR B, P1-6)

Todos os arquivos novos ficam **fora de `apps/<id>/`**. Workflows na raiz: `lunet2d-ci.yml`, `urbe-checks.yml`, `urbe-apps.yml`, `urbe-release.yml`, `urbe-web.yml` (nomes propostos), cada um com **filtro de caminho** (`apps/<id>/**` e o próprio workflow) e `working-directory` (NN-014). Os workflows de origem não são editados.

### 7.1 Lunet2D (`lunet2d-ci.yml`)
- Jobs `test`, `apk`, `release` equivalentes aos de origem, com `working-directory: apps/lunet2d`.
- **Numeração (PROPOSTA, resolve R-LUN-1 e R-LUN-2 sem editar código):** `N = 103 + github.run_number`; o workflow passa `-p:GITHUB_RUN_NUMBER=N` (o csproj já lê essa propriedade), então `versionCode = N` e a tag `v0.0.1-dev.N` **continuam a sequência** (a última é `dev.103`; `run_number` reinicia em um workflow novo e colidiria com tags existentes e faria o Android recusar a atualização).
- **Release (DEC-0008-A):** `gh release create "v$(cat VERSION)-dev.N" … --repo AbnerCruz/Lunet2D --prerelease` com `ORIGIN_RELEASES_TOKEN`. O alvo da tag no repositório de origem é o `HEAD` congelado da `main` de origem (os SHAs do monorepo não existem lá); **a proveniência verdadeira vai em `release-manifest.json` e nas notas** (campo `commit` = SHA do monorepo, com link). Isso é consequência direta de DEC-0008-A e está sinalizado para a revisão (§10).
- O fingerprint do certificado esperado e o caminho `tools/lunet-dev.keystore` continuam válidos (R-LUN-4).

### 7.2 Urbe — checks e releases
- `urbe-checks.yml`: `node --check` e `npm run check` **escopados a `apps/urbe`** (`git ls-files` com `-- apps/urbe`).
- `urbe-apps.yml` (validação de build, nunca publica) e `urbe-release.yml`: gatilho em tags `urbe/v*`; verifica `urbe/v<package.json>`; roda os checks; constrói; **publica em `AbnerCruz/Urbe`** com a tag **sem prefixo** `v<versão>` e `make_latest: true` (os apps instalados leem `releases/latest` desse repositório — R-URB-1). Mesmos secrets de assinatura (§3).
- Dependabot na raiz do monorepo: `npm` e `gradle` em `/apps/urbe`, `/apps/urbe/native/android`; `nuget` em `/apps/lunet2d`; `github-actions` em `/`.

### 7.3 Urbe Web (DEC-0009-A) — `urbe-web.yml`
- Monta o site com o mesmo conjunto que o Urbe já usa (`tools/build-www.mjs` copia `index.html`, `manifest.webmanifest`, ícones, `src/` e `vendor/`) **mais `sw.js`**, e publica no branch **`gh-pages`** de `AbnerCruz/Urbe`, com o SHA do monorepo na mensagem do commit. É artefato **gerado**: declara sua fonte (NN-001).
- O Pages de origem hoje serve a raiz inteira da `main`; a ação 5 do §3 troca a fonte para `gh-pages`. **Ordem:** publicar `gh-pages`, conferir, só então trocar a fonte (sem indisponibilidade). URL, escopo do service worker e PWAs instalados **não mudam**.
- **INFERÊNCIA a validar em P1-6:** que o conjunto acima é suficiente para o site funcionar (comparar com o que o Pages serve hoje).

### 7.4 Checks do Urbe que leem `.github/workflows` (R-URB-3)
Os testes `tests/workflows.mjs`, `tests/consistency.mjs` e `tools/check-workflows.mjs` validam as **cópias inertes** em `apps/urbe/.github/workflows/`, não os workflows ativos da raiz. Consequência: depois da migração **ninguém verifica automaticamente** que "merge em `main` não publica" e que "release exige tag e testes" (REQ-006/066) nos workflows reais. **PROPOSTA:** no PR B, criar uma verificação no Ecosystem para os workflows ativos de cada produto (mesmas regras), em vez de editar os testes do Urbe. As cópias inertes ficam intactas; sua remoção é refatoração posterior (NN-013).

## 8. Ajustes técnicos inevitáveis (NN-013), consolidados

| # | Ajuste | PR | Dentro de `apps/<id>/`? |
|---|--------|----|------------------------|
| 1 | `ecosystem.json`: status e autoridade de versão (§6) | A | não |
| 2 | `docs/migration/commit-map-<id>.txt` | A | não |
| 3 | Workflows na raiz com filtro de caminho (§7) | B | não |
| 4 | Numeração `N = 103 + run_number` do Lunet2D, via propriedade já existente | B | não (no workflow) |
| 5 | Publicação cross-repo de releases e do site (DEC-0008-A, DEC-0009-A) | B | não |
| 6 | Dependabot na raiz | B | não |
| 7 | Verificação dos workflows ativos (§7.4) | B | não |

**Nada** dentro de `apps/<id>/` é alterado na Fase 1. Dívidas preexistentes (token Git em texto claro, `xunit.v3 *`, ações por tag, README e PROGRAM do Urbe desatualizados) **não** são corrigidas (R-LUN-8, R-LUN-9; inventários).

## 9. Branches e PRs de origem

- **Lunet2D:** as 3 branches estão incorporadas à `main` (0 commits à frente): **não importar**.
- **Urbe:** `claude/beta-v1` e `claude/mundo-real` contêm só merges dos PRs #16/#17; `claude/fix-explorer-celular` tem 1 commit cujo **patch é idêntico** ao `0cf0da8` da `main` (verificado por `git patch-id`). **PROPOSTA:** importá-las como `archive/urbe/<nome>` (custo desprezível e preserva tudo, NN-012); as 27 branches `claude/*` e `chore/*` já são ancestrais da `main` e as 10 `dependabot/*` são propostas automáticas: **não importar**.
- PRs, Issues e comentários **não migram** (R-LUN-7, R-URB-7): o repositório de origem permanece vivo (DEC-0008-A), o que os preserva.

## 10. Revisão do proprietário e critérios de aceite

**Para o proprietário revisar este plano**, em especial: (1) a janela de congelamento (§3.1); (2) a proveniência das releases do Lunet2D, em que o alvo da tag no repositório de origem não é o commit construído (§7.1); (3) a troca da fonte do Pages do Urbe para `gh-pages` (§3.5, §7.3); (4) a numeração `103 + run_number` (§7.1); (5) a importação das 3 branches como `archive/urbe/*` (§9).

**Aceite do PR A de cada produto (evidência no handoff):** commits e tags da origem presentes (contagens do §5 e `commit-map`); hash da árvore `apps/<id>` idêntico ao da `main` de origem; testes do produto no novo caminho iguais à linha de base (286 / 56); `ecosystem.json` atualizado e os 16 checks do Ecosystem verdes; nenhum arquivo dentro de `apps/<id>/` editado; fusão por *merge commit*.
**Aceite do PR B:** workflows só disparam pelos caminhos do produto; build e testes restaurados no CI; releases publicadas em modo de ensaio sem afetar `releases/latest` real; validação humana de instalação/atualização em aparelho quando aplicável (NN-017, DEVICE).

## 11. O que este plano não faz

Não extrai Editor, Sprite Studio ou Agent Workspace; não cria Store, Product Shell nem capabilities; não reescreve o Urbe; não altera updaters, formatos de dados ou arquitetura interna; não muda o código de `apps/<id>/` (ADD-0002 §17).
