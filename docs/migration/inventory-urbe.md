# Inventário de migração — Urbe (`urbe`)

> Tarefa: **P1-2** · Agente: `claude-code` · Data: 2026-09-30 · Commit de origem inventariado: `662ca5b19b2e63bc8ca5ce17911a8a061521774c` (`main`, "Merge pull request #47 from AbnerCruz/claude/new-session-eh5rwa")
> **Somente leitura.** Nada foi importado, refatorado, movido, reescrito em C# ou alterado no repositório de origem (NN-005, NN-012, NN-013). Acesso por clone completo e, para o que não existe no git (releases, PRs, execuções de workflow), pela API do GitHub em modo de leitura.
> Convenção (MANIFEST §53): **FATO** = verificado nesta sessão, com comando ou fonte; **INFERÊNCIA** = dedução do agente; **PROPOSTA** = sugestão sujeita a decisão; **NÃO VERIFICADO** = não foi possível verificar, com o motivo.

## 1. Repositórios de origem

| Item | Valor | Tipo |
|------|-------|------|
| URL | https://github.com/AbnerCruz/Urbe | FATO (confirmada em DEC-0004) |
| Visibilidade | pública | FATO |
| Branch padrão | `main` | FATO |
| Licença | "Todos os direitos reservados" (`LICENSE`), com ADR-0003 do próprio Urbe ("licença de fonte disponível") | FATO |
| Tamanho | 460 arquivos, 5,4 MB (sem `.git`), `.git` de 3,6 MB | FATO |
| Forks relevantes | não verificados | NÃO VERIFICADO |
| Idade | primeiro commit `a8c770c` em 2026-09-09 ("Add files via upload"); último em 2026-09-30 | FATO |

## 2. Branches relevantes

FATO: `git ls-remote`, `git merge-base --is-ancestor`, `git rev-list --count`; 43 branches remotas no total (42 além da `main`; `git ls-remote --heads`).

| Grupo | Branches | Situação | Preservar? |
|-------|----------|----------|------------|
| `main` | `main` | ativa | sim |
| Branches de sessão `claude/*` incorporadas | 27 (`abertura-rapida`, `agente-melhor`, `agentes-ia`, `ambiente-paginas`, `analise-geral-funcionamento-yj098x`, `app-desempenho`, `app-nativo`, `aquario-vivo`, `bairros-buraco`, `bairros-casas`, `celular-mundo`, `estudio-paginas`, `fix-abertura-mundo`, `fix-explorer-abas`, `fix-migracao-oceano`, `layout-livre`, `matematica`, `moradores`, `mundo-cheio`, `mundo-vivo`, `paginas-html`, `paginas-livro`, `primeiro-acesso`, `regioes-visual`, `ux-fase-1`, `ux-fase-2`, `ux-fase-3`) + `chore/urbe-2-foundation` | **ancestrais da `main`** (0 commits à frente) | histórico já está na `main`; não precisam ser importadas |
| `claude/beta-v1`, `claude/mundo-real`, `claude/fix-explorer-celular` | 3 | **1 commit à frente** cada, porém a auditoria do próprio Urbe (`docs/v2/BRANCH-AUDIT.md`) registra "só commits de merge de PR (#16/#17); conteúdo na `main`" e, para `fix-explorer-celular`, "commit duplicado na `main` (`0cf0da8`)" | **decisão do proprietário depende de aceitar a auditoria do Urbe**; o commit à frente existe só nessas refs — o plano de importação (P1-3) deve **preservá-las** até o proprietário decidir (NN-012) |
| `claude/new-session-eh5rwa` | 1 | branch de trabalho mais recente; já mergeada pelo PR #47 (`662ca5b`) | histórico na `main` |
| `dependabot/*` | 10 (npm: chokidar 5.0.0, electron 44.4.5, playwright 1.63.0; github-actions: download-artifact 8, setup-java 6, setup-node 7, upload-artifact 7, action-gh-release 3; gradle: google-services 4.5.0, gradle-wrapper 9.8.0) | **não mergeadas**, 1 commit à frente cada; cada uma tem **PR aberto** (#37–#46) | não importar: são propostas automáticas que o Dependabot refaz; ver R-URB-7 |

- **PRs abertos: 10**, todos do Dependabot (#37–#46, criados em 2026-09-30). Demais PRs: não listados individualmente (há PRs até o #47). **Não** fazem parte do histórico git (R-URB-7).
- **Issues:** 1 aberta: #33 "Urbe 2.0 — concluir descoberta antes da SPEC canônica" (FATO, `list_issues`). Issues não migram com o git.
- **Branch deletion:** `docs/v2/BRANCH-AUDIT.md` informa que a remoção das branches remotas **não foi executada** por falta de autorização do proprietário. Esta migração **não** apaga nenhuma branch de origem.

## 3. Histórico

| Item | Valor (FATO) |
|------|--------------|
| Commits na `main` | 382 (`git rev-list --count HEAD`) |
| Commits de merge | 33 |
| Primeiro / último | `a8c770c` 2026-09-09 / `662ca5b` 2026-09-30 |
| Autores | `Abner Cruz` 328 · `Claude` 54 |
| Commits na `main` **depois** da última release (`v1.8.2-beta`, `91de0f4`) | **36** — o trabalho da Urbe 2.0 (F0/F1) está mergeado e **não publicado** |

## 4. Tags e releases

FATO (`git tag`, `list_releases`, `get_latest_release`).

- **6 tags**, todas **leves**: `v1.7.0-beta`, `v1.7.1-beta`, `v1.7.2-beta`, `v1.8.0-beta`, `v1.8.1-beta`, `v1.8.2-beta`.
- **6 releases** GitHub com os mesmos nomes ("Urbe 1.8.2-beta"…), **todas `prerelease: false`** — de propósito: os apps instalados se atualizam por `releases/latest`, então betas também são "latest" (`docs/v2/RELEASE.md`). Publicadas entre 2026-09-27T20:44Z e 2026-09-28T11:14Z; autor `github-actions[bot]`.
- Última: **`v1.8.2-beta`** (alvo `91de0f4bc3626ffdf471b81d1b9b7e74ff0fceea`). Ativos:

| Ativo | Tamanho (bytes) | Digest GitHub (sha256) |
|-------|-----------------|-------------------------|
| `Urbe-1.8.2-beta.apk` | 4 263 697 | `61487f2bb0a43067e25aefa64c246d3769460a53920d74aa1e08e1cca6ea8d35` |
| `Urbe-Setup-1.8.2-beta.exe` | 94 753 312 | `77a1120ea62ebb1ce43a0e870e06015ae0c5ae43c0e1d701691327e6b1361c6c` |
| `Urbe-Setup-1.8.2-beta.exe.blockmap` | 100 670 | `039088541ff85e5c7cf632f4b87ef9d85dd53af212c80237e82e7fce29961687` |
| `latest.yml` (metadados do `electron-updater`) | 351 | `2ff8bdfa5edb7890dcfa80198cb2be0628644aac766912dcffb634c0eb340e0f` |

  O APK tem 2 downloads registrados. Não há `SHA256SUMS.txt` nem manifesto de release próprio (diferente do Lunet2D).

| Release de referência para o portal (P1-9) | |
|--------------------------------------------|--|
| Versão canônica (`package.json`) | `1.8.2-beta` |
| Última release | `v1.8.2-beta` |
| Artefatos | APK Android + instalador Windows (NSIS x64) |
| Checksum | somente o digest do GitHub por ativo (sem arquivo de checksums) |
| Web | ver §6 |

## 5. Workflows (GitHub Actions)

FATO: 4 workflows do repositório + 2 dinâmicos (Dependabot e Pages).

| Workflow | Gatilho | O que faz | Dependências de caminho / configuração |
|----------|---------|-----------|----------------------------------------|
| `structural-checks.yml` ("Urbe structural checks") | `push` em `main`, `pull_request` | `git ls-files -z '*.js' \| xargs node --check`; `npm run check` (Node 22). Última execução na `main`: run #350 **success** (https://github.com/AbnerCruz/Urbe/actions/runs/36754586806); 301 execuções no total | `git ls-files` **na raiz do repositório** (incluiria arquivos de outros produtos no monorepo); `npm run check` |
| `app.yml` ("Urbe instalável (validação de build)") | `pull_request` com filtro de caminhos (`native/**`, `src/native/**`, `package.json`, `package-lock.json`, `capacitor.config.json`, `tools/build-www.mjs`, os próprios workflows) e `workflow_dispatch` | confere a versão (`node tools/version.mjs check`) e chama `build-apps.yml` com `release: false`. **Nunca publica** (REQ-006) | caminhos relativos à raiz do produto |
| `build-apps.yml` (reutilizável) | `workflow_call` | job `windows` (windows-latest, `npm ci`, `electron-builder --win --x64 --publish never`) e job `android` (JDK 21 Temurin, `build-www` + `cap sync android`, `gradlew assembleRelease`); assina com secrets se existirem; sem secrets, APK de depuração (em `release: true`, **falha**) | `native/android`, `tools/build-www.mjs`, `dist/` |
| `release.yml` ("Publicar lançamento") | `push` de tag `v*` e `workflow_dispatch` (sobre tag) | verifica `tag == v<package.json>` e que a release **não** existe; `node --check` + `npm run check`; constrói; `softprops/action-gh-release@v2` com `make_latest: true` e notas do `CHANGELOG.md` (`## v<versão>`) | `package.json`, `CHANGELOG.md`; publica **no repositório onde roda**; o `latest` é o do repositório |
| `pages build and deployment` (dinâmico) | automático a cada push na `main` | publica o site a partir da `main` (**314 execuções**; última em 2026-09-30, `662ca5b`, success) | ver §6 |
| `Dependabot Updates` (dinâmico) | semanal | abre PRs de atualização | `.github/dependabot.yml` (npm `/`, github-actions `/`, gradle `/native/android`) |

Também versionados em `.github/`: `ISSUE_TEMPLATE/`, `pull_request_template.md`, `dependabot.yml`. As actions são fixadas por **tag**, não por SHA (pendência RM-F0-15 do próprio Urbe).

## 6. GitHub Pages — **o Urbe Web é publicado hoje a partir da `main`**

| Item | Situação |
|------|----------|
| Existência | **FATO:** o workflow dinâmico `pages build and deployment` existe (criado em 2026-09-09), com 314 execuções; a última roda sobre `main`/`662ca5b` e termina em success. |
| Modo | **INFERÊNCIA (alta confiança):** *deploy from a branch* (`main`, raiz). Razões: não existe workflow de Pages no repositório; o `index.html`, `manifest.webmanifest` e `sw.js` ficam na **raiz**; o README afirma que "publicar é só copiar a pasta para uma hospedagem estática". |
| URL pública | **INFERÊNCIA:** `https://abnercruz.github.io/Urbe/`. **NÃO VERIFICADO:** a API de configuração de Pages não está disponível nesta sessão e o domínio `github.io` é bloqueado pelo proxy do ambiente. |
| Comportamento | Cada merge na `main` **republica o site**. Isso é diferente do app instalado, em que "merge não publica" (REQ-006); a web é publicada por integração, os instaladores por tag. |
| Por que importa | O Urbe usa caminhos relativos (`"start_url": "./"`, `"scope": "./"`) e service worker por escopo de caminho. Depois da importação, a raiz do repositório será do Ecosystem e o Urbe estará em `apps/urbe/`: o modo *deploy from a branch* **não consegue** servir uma subpasta arbitrária, e o Pages do Ecosystem já usa o modo *GitHub Actions* (workflow `pages`). Ver R-URB-2 e DEC-0009. |

O navegador associa armazenamento (IndexedDB/OPFS) à **origem** (`https://abnercruz.github.io`), não ao caminho. **INFERÊNCIA (a validar em P1-10):** mover o Urbe Web para outro caminho da mesma origem preservaria os dados do navegador, mas não o escopo do service worker nem de PWAs já instaladas.

## 7. Secrets e configuração

- **Secrets declarados em workflow (somente nomes):** `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD` (em `build-apps.yml`; `release.yml` passa `secrets: inherit`). O `build.gradle` também lê as variáveis de ambiente `URBE_KEYSTORE`, `URBE_KEYSTORE_PASSWORD` e `URBE_KEY_PASSWORD`. **NÃO VERIFICADO:** se os secrets existem no repositório (exige permissão de administração). A própria documentação afirma que **sem a chave o APK não pode ser atualizado depois**.
- **Consequência para a migração (INFERÊNCIA):** secrets de repositório **não** se copiam com o git. Para restaurar a publicação no monorepo, o proprietário precisará recriar esses dois secrets no repositório do Ecosystem (ação do proprietário; nenhum agente tem como ler ou copiar o valor).
- **Chaves no repositório:** `.gitignore` bloqueia `*.keystore` e `*.jks`; nenhuma chave de assinatura versionada (FATO: `git ls-files`).
- **Chaves de IA de usuário:** ficam só no dispositivo, nunca no vault, export ou logs (AGENTS.md, ADR-0007). Não há segredo de serviço no repositório.
- **NÃO VERIFICADO:** proteções de branch, environments, webhooks (exigem administração).

## 8. Dependências externas

| Dependência | Detalhe (FATO) |
|-------------|----------------|
| Runtime | navegador (web/PWA); **Electron `^38.2.0`** (Windows); **Capacitor `^8.0.0`** (Android); Node 22 nos workflows; JDK 21 Temurin e Gradle para o APK |
| Dependências de produção | `chokidar ^4.0.3`, `electron-updater ^6.6.2` |
| Desenvolvimento | `@capacitor/android|cli|core|filesystem ^8.0.0`, `electron ^38.2.0`, `electron-builder ^26.0.12`, `playwright ^1.56.1`; lockfile `package-lock.json` versionado |
| Vendorizado | `vendor/katex` (KaTeX, fórmulas LaTeX); `THIRD-PARTY-NOTICES.md` |
| Serviços de terceiros em runtime | provedores de IA escolhidos pelo usuário (OpenRouter, Anthropic, OpenAI, Google Gemini e outros, com chave própria, só no aparelho); **`api.github.com/repos/AbnerCruz/Urbe/releases/latest`** para checar atualização no Android (ver R-URB-1) |
| Plataformas | web, Electron (Windows x64, NSIS; o `package.json` também declara alvo Linux AppImage, que **não** é construído pelo CI), Android (`applicationId app.urbe`); todos os 73 módulos declarados rodam em `web+electron+android` |
| Actions | `actions/checkout`, `setup-node`, `setup-java`, `upload-artifact`, `download-artifact`, `softprops/action-gh-release@v2` (por tag) |

## 9. Arquivos normativos

| Arquivo | Papel (FATO) |
|---------|--------------|
| `docs/v2/REQUIREMENTS.md` | Requirement Ledger: **REQ-001…REQ-109** (IDs estáveis) |
| `docs/v2/SPEC.md` | SPEC canônica da Urbe 2.0, **aprovada pelo proprietário em 2026-09-30** |
| `docs/v2/ROADMAP.md` | itens `RM-Fn-nn`, fases F0–F7 com gates G0–G7 |
| `docs/v2/TRACEABILITY.md` | **gerado** (`node tools/gen-traceability.mjs`); não editar à mão |
| `docs/v2/adr/0001…0009` | 9 ADRs próprios (runtime sem build e registro de módulos; plugins full-trust; licença fonte disponível; compatibilidade 1.x; release e versão única; identidade documental; credenciais de IA e CSP; comentários em sidecar; composições para páginas) |
| `docs/v2/discovery/*` | evidência do estado 1.x: `ARCHITECTURE-MAP`, `BOUNDARY-EXCEPTIONS`, `DATA-CATALOG`, `LEGACY-MAP`, `PLATFORMS`, `THREAT-MODEL`, `TEST-MATRIX`, `PERFORMANCE`… |
| `docs/v2/RELEASE.md`, `BRANCH-AUDIT.md`, `MIGRATION.md` (migração de **dados** 1.x→2.x, não de repositório), `PROGRAM.md` | política de release, auditoria de branches, etc. |
| `AGENTS.md` | contrato para agentes **do Urbe** (fontes de verdade em ordem; "nunca remova ou enfraqueça um REQ"; "release só por tag") |
| `AGENTSCHAT.md` | log de coordenação entre agentes (append-only, formato próprio) |
| `ARCHITECTURE.md`, `CONTRIBUTING.md`, `CHANGELOG.md` (74 KB), `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` | documentação geral |

**Observações de coerência interna (FATO, não corrigir aqui):** `README.md` diz que `app.yml` "publica o lançamento" a cada versão na `main`, mas o workflow real nunca publica (quem publica é `release.yml` por tag); `docs/v2/PROGRAM.md` diz "execução da 2.0 não iniciada", enquanto a SPEC está aprovada e há itens F0/F1 mergeados (PR #47). Derivas de documentação do próprio produto; a migração não deve "consertá-las" (NN-013).

**REQ-033 (ADIADO):** a SPEC do Urbe adia o "launcher multi-app" como ideia futura do proprietário. Consistente com o Ecosystem Hub ser externo ao Urbe (NN-003, NN-023); nenhuma dependência do Hub existe no Urbe.

## 10. Versão

FATO: autoridade única = **`package.json` → `"version": "1.8.2-beta"`** (ADR-0005). `node tools/version.mjs sync` propaga para `package-lock.json`, `src/core/core.js`, `index.html` (`<title>`), `sw.js` (nome do cache `urbe-shell-v1.8.2-beta`), `CHANGELOG.md` e README; `node tools/version.mjs check` valida (saída nesta sessão: **"OK: versão 1.8.2-beta coerente em package-lock, core, index, sw, CHANGELOG, README e app.js"**). O `versionCode` do Android é **derivado da versão** (`1.7.0-beta → 1070000`, `1.7.0-beta.3 → 1070003`, `1.7.0 → 1070099`, comentário em `native/android/app/build.gradle`), portanto **não** depende de número de execução do CI (ao contrário do Lunet2D).
Em `ecosystem.json`, `version.authority = source-repository` até a migração; após importar deve passar a `version-file` apontando para `apps/urbe/package.json`.

## 11. Linha de base (antes)

| Comando | Resultado | Evidência |
|---------|-----------|-----------|
| `npm run check` (Node 22.22.2, Linux, **sem** `npm ci`; commit `662ca5b`) | **56/56 arquivos de teste passaram; "todas as checagens passaram"** (3 min 34 s; o mais lento, `world-terrain.mjs`, 207 s) | executado nesta sessão |
| `node tools/version.mjs check` | OK (ver §10) | executado nesta sessão |
| CI `Urbe structural checks` na `main` (run #350) | success | https://github.com/AbnerCruz/Urbe/actions/runs/36754586806 |
| `npm run test:e2e` (Playwright) | **não executado** nesta sessão (exige instalar navegadores); NÃO VERIFICADO | — |
| Instalador Windows e APK | **não executáveis nesta sessão** (exigem runner Windows / Android SDK+Gradle); só o CI constrói | NÃO VERIFICADO localmente; `app.yml`/`release.yml` os validam |
| Validação humana | O produto mantém itens `[?]` (validação humana pendente) no seu ROADMAP (`AGENTS.md`); a migração não pode declarar "sem regressão" sem comparar com esse estado (NN-017) | — |

## 12. Mapa funcional e arquitetural (ADD-0002 §16)

> **PROPOSTA / INVENTÁRIO.** A classificação futura **não autoriza extrair, mover, reescrever nem refatorar nada** (NN-005, NN-013; AGENTS.md do próprio Urbe: "sem mover diretórios sem REQ que exija"). Não presume que existam módulos da visão de produto (`docs/architecture/product-vision.md`). O Urbe já possui **registro de módulos** (`src/modules.json`, **73 módulos** com `layer`, `phase`, `requires`, `uses`, `platforms`) verificado por `tools/check-modules.mjs` (ADR-0001); abaixo, os agrupamentos por pasta de `src/`. Dependências entre grupos: derivadas de `requires` em `src/modules.json` (FATO).

Camadas declaradas (contagem de módulos): `core` 10 · `persistence` 10 · `feature` 40 · `ui` 6 · `kit` 3 · `native` 1 · `vendor` 2 · `app` 1. Fases de carga: `boot` 8 · `core` 19 · `feature` 34 · `late` 11 · `app` 1. Plataformas: todos os 73 em `web+electron+android`.

### U-01 · Núcleo (`src/core/`) — 9 arquivos, 380 linhas
- **Responsabilidade atual:** eventos, comandos, documentos, índice de conhecimento, histórico, lixeira, agendador, atalhos, artefatos.
- **Dependências:** `ui` (1 aresta). **Dados que possui:** estruturas em memória; histórico/lixeira persistidos via U-02. **UI:** não. **Standalone?** só dentro do app (sem build; scripts carregados por registro de módulos).
- Candidato futuro: [x] **Product Core** *(PROPOSTA)*

### U-02 · Persistência (`src/persistence/`) — 10 arquivos, 639 linhas
- **Responsabilidade atual:** gravação do vault com journal; adaptadores `idb`/`fsa`/`router` (contrato em `docs/v2/contracts/persistence-adapter.md`); escritor único do `mapa.json`.
- **Dados que possui:** o **vault**: notas do usuário como arquivos Markdown (`Documentos/Urbe` nos apps; armazenamento do navegador ou pasta escolhida na web), `.urbe/` (mapa da cidade, lixeira, histórico, journal de recuperação), `Personalização/`, `Páginas/`, `Tutorial/`. **Dados do usuário são invariante** (REQ-007, ADR-0004: compatibilidade 1.x e proteção contra versão desconhecida).
- **Dependências:** `core`, `composition`. **Standalone?** não (parte do app). **Riscos:** qualquer mudança de caminho ou de formato toca dados do usuário — **fora do escopo da migração**.
- Candidato futuro: [x] **Product Core** · [ ] Adapter (adaptadores `idb`/`fsa`) *(PROPOSTA)*

### U-03 · Editor e Explorer (`src/editor/`, `src/explorer/`) — 8 + 3 arquivos, 273 + 234 linhas
- **Responsabilidade:** sessão de edição, abas, painel dividido, localizar/substituir, modo Visual (tabelas, callouts, tarefas, links); árvore de notas e suas operações.
- **Dependências:** `core`, `ui`, `persistence`, `composition`. **Dados:** os do vault via U-02. **UI:** sim. **Específico do Product?** sim (editor de Markdown do vault; não é o editor de código do Lunet).
- Candidato futuro: [x] **Product Core** · [ ] Tool (Editor) · [ ] Ainda indeterminado *(PROPOSTA)*

### U-04 · Mundo/Cidade (`src/world/`) — 11 arquivos, 1 327 linhas
- **Responsabilidade:** terreno procedural, arte pixel-art, worker de chunks, projeção, ruas, moradores e animais derivados das notas (a "cidade").
- **Dependências:** `core`, `persistence`, `app` (1 aresta). **Dados:** `mapa.json` via U-02. **UI:** sim (canvas). **Específico?** **totalmente** (identidade do Urbe).
- Candidato futuro: [x] **Product Core** *(PROPOSTA)*

### U-05 · Assistente de IA (`src/ai/`) — 5 arquivos, 1 168 linhas
- **Responsabilidade:** provedores, ferramentas, agentes, loop agêntico, interface do Assistente; os agentes leem/buscam/escrevem/organizam o vault com aprovação do usuário.
- **Dependências:** `core`, `explorer`, `persistence`, `ui`. **Dados:** configurações; chaves de IA só no aparelho (ADR-0007). **Externos:** provedores de IA (rede, com chave do usuário).
- **Relação com o Ecosystem:** MANIFEST §39 prevê Agent Runtime compartilhado com o Lunet2D. Este subsistema **não** deve ser reescrito às cegas; só será mapeado na Fase 6.
- Candidato futuro: [x] **Workspace** (Agent Workspace do Urbe) · [ ] Service (Agent Runtime) · [ ] Ainda indeterminado *(PROPOSTA)*

### U-06 · Páginas (`src/pages/`) — 5 arquivos, 1 713 linhas
- **Responsabilidade:** estúdio para montar sites e livros (blocos, layout livre, modelos, exportação); arquivos `*.page.json`.
- **Dependências:** `core`, `ai`, `explorer`, `math`, `native`, `ui`. **Dados:** `Páginas/` do vault.
- Candidato futuro: [x] **Product Core** · [ ] Workspace (Páginas) · [ ] Ainda indeterminado *(PROPOSTA)*

### U-07 · Matemática (`src/math/`) — 2 arquivos, 276 linhas (+ `vendor/katex`)
- **Responsabilidade:** fórmulas LaTeX (KaTeX embutido), editor visual. **Dependências:** `core`, `editor`.
- Candidato futuro: [x] **Library** · [ ] Tool *(PROPOSTA)*

### U-08 · Personalização e plugins (`src/customize/`) — 4 arquivos, 899 linhas
- **Responsabilidade:** temas, cores, fontes, estilos CSS, **plugins** com API em português (modelo *full-trust*, ADR-0002), ferramentas do Assistente.
- **Dependências:** `ai`, `core`, `editor`, `persistence`, `ui`, `world`. **Dados:** `Personalização/` do vault.
- Candidato futuro: [x] **Product Core** (modelo de plugins **próprio**; não confundir com o Plugin SDK do Ecosystem) · [ ] Ainda indeterminado *(PROPOSTA)*

### U-09 · Composições (`src/composition/`) — 3 arquivos, 43 linhas
- **Responsabilidade:** junta várias notas num documento pronto para imprimir (ADR-0009). **Dependências:** `core`, `persistence`, `ui`.
- Candidato futuro: [x] **Product Core** *(PROPOSTA)*

### U-10 · UI e estilos (`src/ui/`, `src/styles/`) — 8 + 11 arquivos, 417 + 1 910 linhas
- **Responsabilidade:** diálogos, ícones, paleta de comandos, busca, configurações; CSS.
- **Dependências:** `core`, `explorer`, `native`, `app` (1 aresta).
- Candidato futuro: [x] **Product Shell** *(PROPOSTA; parcialmente misturado com `src/app.js`)*

### U-11 · Casca e integração (`src/app.js`) — **5 259 linhas**
- **Responsabilidade:** "casca da cidade e integração (código histórico em camadas)" (README). Liga os demais grupos. É o maior arquivo do produto; a Urbe 2.0 (F2) tem itens próprios para extraí-lo **provando a substituta e apagando a antiga no mesmo item**.
- **Dependências:** `ai`, `core`, `editor`, `explorer`, `math`, `native`, `persistence`, `ui`, `world`. **Riscos:** há arestas que violam as camadas declaradas (`ui → app`, `world → app`, `core → ui`); o próprio Urbe mantém uma lista fechada e decrescente dessas exceções em `docs/v2/discovery/BOUNDARY-EXCEPTIONS.md`, cada uma com o item de ROADMAP que a remove.
- Candidato futuro: [x] **Product Shell** · [ ] Ainda indeterminado *(PROPOSTA; **não tocar** na migração)*

### U-12 · Ponte nativa (`src/native/bridge.js`) — 302 linhas · Electron (`native/desktop/`) · Capacitor (`native/android/`)
- **Responsabilidade:** `bridge.js` dá ao Urbe "handles" de pasta sobre o disco real (mesma interface do File System Access) e implementa a **checagem de atualização no Android** (ver R-URB-1); `native/desktop/` (`main.js`, `preload.js`, `guards.js`, `vault-fs.js`; 424 linhas) é a casca Electron com `electron-updater`; `native/android/` (`MainActivity`, `UrlGuard`, `PathGuard`, `UrbeAndroidPlugin`; Java, 792 linhas) é o projeto Capacitor com testes JVM de guardas.
- **Contrato:** `docs/v2/contracts/native.md`. **Dependências externas:** Electron, electron-updater, Capacitor.
- Candidato futuro: [x] **Adapter** (plataformas) · [ ] Product Shell (hosts nativos) *(PROPOSTA)*

### U-13 · Web/PWA (raiz: `index.html`, `manifest.webmanifest`, `sw.js`, ícones)
- **Responsabilidade:** superfície web e PWA (escopo `./`, cache `urbe-shell-<versão>`); **é o "Urbe Web"** publicado pelo Pages (§6). **Standalone?** sim, hospedável como arquivos estáticos.
- Candidato futuro: [x] **Product Shell** (web) *(PROPOSTA)*

### U-14 · Ferramentas, testes e tutorial
- `tools/` (22 arquivos): `check-all`, `check-modules`, `check-traceability`, `check-workflows`, `check-license`, `check-debt`, `version`, `build-www`, `build-tutorial`, `run-tests`, `run-e2e`, `perf/`, etc. `tests/` (74 arquivos; 56 arquivos de teste executados por `npm run check`; e2e em `tests/e2e/`). `tutorial/` (48 arquivos-fonte) + `src/tutorial/` (conteúdo gerado). **Riscos:** vários checks leem `.github/workflows/*.yml` **na raiz do produto** (R-URB-3).
- Candidato futuro: [x] **Product Core** (verificações do produto; não extrair) *(PROPOSTA)*

### Resumo do mapa

| Classificação proposta | Subsistemas |
|------------------------|-------------|
| Product Core | U-01 núcleo, U-02 persistência, U-03 editor/explorer, U-04 mundo, U-06 páginas, U-08 personalização, U-09 composições, U-14 |
| Product Shell | U-10 UI, U-11 casca `app.js`, U-13 web/PWA |
| Workspace | U-05 Assistente de IA |
| Library | U-07 matemática |
| Adapter | U-12 ponte nativa, adaptadores `idb`/`fsa` de U-02 |
| Service | nenhum identificado |
| Ainda indeterminado | fronteiras de U-03/U-06/U-08 e o que hoje vive em U-11 |

**Dependência do Hub ou de outro Product (NN-002, NN-003, NN-023):** **nenhuma.** `grep -rniE "ecosystem|lunet|hub"` em `src/`, `native/`, `tools/`, `tests/`, `.github/`, `index.html`, `sw.js` e nos manifestos não retorna referência ao Lunet2D, ao Hub ou ao Ecosystem (a única ocorrência textual é a chave `package-ecosystem` do `dependabot.yml`); o Urbe é empacotado e publicado sem o Hub hoje.

## 13. Riscos e ajustes técnicos inevitáveis previstos

Cada ajuste é **necessidade técnica para o produto funcionar em `apps/urbe/`** e deve constar, um a um, no plano de importação (P1-3) e no PR (NN-013). **Nenhum foi feito.**

| ID | Risco / ajuste previsto | Por quê (FATO) | Severidade |
|----|------------------------|----------------|------------|
| R-URB-1 | **Apps instalados se atualizam lendo `releases/latest` do repositório `AbnerCruz/Urbe`** | `src/native/bridge.js` tem `REPO='AbnerCruz/Urbe'` e consulta `api.github.com/repos/AbnerCruz/Urbe/releases/latest` (Android, pega o primeiro `.apk`); `package.json` configura `electron-updater` com `provider github, owner AbnerCruz, repo Urbe` e `native/desktop/main.js` liga `allowPrerelease` (Windows). **Se as releases passarem para o repositório do Ecosystem, as versões já instaladas deixam de encontrar atualizações**, e o "latest" do monorepo seria compartilhado com o Lunet2D e o Hub (uma release de outro produto poderia ser lida como a última do Urbe). | **Crítica** |
| R-URB-2 | **Urbe Web depende do Pages por branch** | Ver §6. Em `apps/urbe/` o site não é servido pelo modo atual; republicar exige decisão (DEC-0009) e pode alterar URL, escopo do service worker e PWAs instaladas. | **Alta** |
| R-URB-3 | **Checks do produto leem `.github/workflows/` na raiz do produto** | `tools/check-workflows.mjs` (`join(ROOT, '.github/workflows')`), `tests/workflows.mjs` (`../.github/workflows/`) e `tests/consistency.mjs` (lê `structural-checks.yml` e exige `git ls-files '*.js'`) **falharão** se os workflows forem movidos para a raiz do monorepo. Precisam ser adaptados (ajuste mínimo e documentado) — ou os workflows do produto precisam coexistir sob `apps/urbe/.github/` como referência. | Alta |
| R-URB-4 | **Workflows na raiz do repositório** | Os 4 workflows só executam a partir da raiz do repositório. No monorepo precisam ir para a raiz **com filtro por caminho** (`apps/urbe/**`), `working-directory` e escopo do `git ls-files`, para não rodarem a cada mudança de outro produto (NN-014). `release.yml` verifica `tag == package.json` e a existência da release no repositório que o roda. | Alta |
| R-URB-5 | **Secrets de assinatura do Android** | `ANDROID_KEYSTORE_BASE64` e `ANDROID_KEYSTORE_PASSWORD` precisam existir no repositório do Ecosystem (ação do proprietário). Sem eles o build de release falha por desenho. Trocar a chave impede a atualização de APKs instalados. | Alta |
| R-URB-6 | **Colisão de identificadores** | `AGENTS.md`, `CHANGELOG.md`, `docs/v2/adr/0001…0009`, `REQ-*`, `RM-*` coexistem com `docs/adr/0001…` e IDs do Ecosystem. Dentro de `apps/urbe/` os caminhos são únicos, mas referências por ID ("ADR-0001") ficam ambíguas (NN-019). **PROPOSTA (P1-3):** referências qualificadas (`urbe/ADR-0001`). | Média |
| R-URB-7 | **Objetos do GitHub não migram com o git** | 10 PRs abertos do Dependabot (#37–#46), 1 Issue aberta (#33), histórico de PRs/comentários/reviews. A configuração do Dependabot (`directory: /`) também precisa ser refeita para `/apps/urbe` se o monorepo a adotar. | Média |
| R-URB-8 | **`AGENTS.md` local e `AGENTSCHAT.md`** | O `AGENTS.md` do Urbe define "fontes de verdade em ordem" e um log de coordenação próprio; no monorepo, o local **complementa e não contradiz** o da raiz (MANIFEST §26) e `AGENTSCHAT.md` seria uma **segunda fonte de estado de agentes** ao lado de `docs/governance/handoffs/` (NN-001, NN-008). Ajuste de governança **posterior** à importação, como item próprio. | Média |
| R-URB-9 | **Trabalho não publicado à frente da última release** | 36 commits na `main` após `v1.8.2-beta` (Urbe 2.0 F0/F1). Restaurar "releases" (P1-6) deve partir do que está na `main`, sem publicar nada por efeito colateral da importação (AGENTS.md do Urbe: "release só por tag"). | Média |
| R-URB-10 | **Branches com 1 commit não incorporado** | `claude/beta-v1`, `claude/mundo-real`, `claude/fix-explorer-celular` (ver §2): preservar até o proprietário decidir (NN-012). | Baixa |
| R-URB-11 | **Builds só no CI** | Instalador Windows (NSIS) e APK só são construídos no GitHub Actions; a restauração de builds (P1-6) depende dos runners do GitHub. | Informativa |
| R-URB-12 | **Regra do Urbe contra "mover diretórios"** | `AGENTS.md`: "sem mover diretórios sem REQ que exija". A importação coloca o Urbe inteiro sob `apps/urbe/` **sem alterar sua estrutura interna**; é mudança de raiz, não de organização. Registrar explicitamente no plano para não conflitar. | Informativa |

**Sem risco encontrado:** sem referências absolutas ao caminho do repositório nos arquivos do produto; nenhuma dependência do Lunet2D, do Hub ou do Ecosystem; `package.json`, `package-lock.json`, `capacitor.config.json` e `.gitignore` ficam na raiz do produto (`apps/urbe/`) e **não** devem ser movidos para a raiz do monorepo.

## 14. Decisões necessárias

- **DEC-0008** — *Canal de releases e de atualização dos produtos após a migração* (R-URB-1, R-URB-5; bloqueia P1-3/P1-6). Pendente em `docs/governance/decisions.json` e visível no portal.
- **DEC-0009** — *Publicação do Urbe Web após a migração* (R-URB-2; bloqueia P1-10, não bloqueia P1-3). Pendente e visível no portal.
- Branches `claude/beta-v1`, `claude/mundo-real`, `claude/fix-explorer-celular`: preservar até decisão do proprietário (será perguntado no plano de importação, P1-3).
- Questões do plano de importação (P1-3) que o **agente** pode propor dentro de DEC-0002 e NN-013: formato das tags importadas (`urbe/v1.8.2-beta`), filtros de caminho dos workflows, adaptação mínima dos checks que leem `.github/workflows` (R-URB-3), referências qualificadas de ADR.
