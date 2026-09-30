# Inventário de migração — Lunet2D (`lunet2d`)

> Tarefa: **P1-1** · Agente: `claude-code` · Data: 2026-09-30 · Commit de origem inventariado: `be30e62c35714b48e7c0be4139efa58d02644ee7` (`main`, "Merge pull request #25 from AbnerCruz/ccr-8e262421-r4rdz0")
> **Somente leitura.** Nada foi importado, refatorado, movido ou alterado no repositório de origem (NN-012, NN-013). O acesso ao repositório foi feito por clone completo (`git clone`, sem `--depth`) e, para o que não existe no git (releases, PRs, execuções de workflow), pela API do GitHub em modo de leitura.
> Convenção (MANIFEST §53): **FATO** = verificado nesta sessão, com comando ou fonte; **INFERÊNCIA** = dedução do agente; **PROPOSTA** = sugestão sujeita a decisão; **NÃO VERIFICADO** = não foi possível verificar, com o motivo.

## 1. Repositórios de origem

| Item | Valor | Tipo |
|------|-------|------|
| URL | https://github.com/AbnerCruz/Lunet2D | FATO (confirmada em DEC-0004) |
| Visibilidade | pública | FATO (`list_repos`) |
| Branch padrão | `main` | FATO |
| Licença | proprietária e provisória (`LICENSE.md`): "Todos os direitos reservados"; código pode ser lido mas não redistribuído, modificado ou explorado comercialmente sem autorização; jogos criados pertencem a quem os cria | FATO |
| Tamanho | 208 arquivos, 2,1 MB (sem `.git`), `.git` de 888 KB | FATO |
| Forks relevantes | não verificados | NÃO VERIFICADO (sem endpoint de forks disponível) |
| Idade | primeiro commit `e5dcc89` em 2026-09-28; último em 2026-09-30 | FATO |

## 2. Branches relevantes

FATO: `git ls-remote` e `git merge-base --is-ancestor <branch> origin/main`.

| Branch | Último commit | Situação | Preservar? |
|--------|---------------|----------|------------|
| `main` | 2026-09-30 | ativa; padrão | sim |
| `ccr-8e262421-r4rdz0` | 2026-09-30 | **incorporada à `main`** (0 commits à frente; 1 atrás). Origem de 23 dos 25 PRs | histórico já está na `main`; branch não precisa ser importada separadamente |
| `codex/autosave-journal` | 2026-09-29 | incorporada à `main` (0 à frente) | idem |
| `codex/phase-validation-guide` | 2026-09-29 | incorporada à `main` (0 à frente) | idem |

- **PRs:** 25 fechados, 0 abertos (API `list_pull_requests`). O campo `merged` **não** é retornado pela listagem, então o número de PRs efetivamente mergeados não foi afirmado; o histórico git da `main` contém 26 commits de merge (`git log --merges`). Pull requests, comentários e reviews **não fazem parte do histórico git** e não migram com a importação (NN-012): ver §12 (R-LUN-7).
- **Issues:** 0 (FATO, `list_issues`).
- Forks/branches de longa duração além das acima: nenhum encontrado (DEC-0004 já registra "nenhum indicado").

## 3. Histórico

| Item | Valor (FATO) |
|------|--------------|
| Commits na `main` | 84 (`git rev-list --count HEAD`) |
| Commits de merge | 26 |
| Primeiro commit | `e5dcc89` 2026-09-28 "docs: initialize Lunet repository" |
| Último commit | `be30e62` 2026-09-30 |
| Autores | `Claude <noreply@anthropic.com>` 50 · `Abner Cruz` 34 (git shortlog) |
| Agentes identificados | Claude (branch `ccr-*`), Codex (branches `codex/*`), conforme `AGENTS.md` e `AgentsChat.md` |

## 4. Tags e releases

FATO (`git tag`, `list_releases`, `get_release_by_tag`).

- **25 tags**, todas **leves** (`git for-each-ref refs/tags --format='%(objecttype)'` → 25 `commit`, nenhuma anotada): `v0.0.1-dev.8 … v0.0.1-dev.103`.
- **25 releases** GitHub, **todas `prerelease: true`** ("Lunet 0.0.1-dev.N (development)"), criadas por `github-actions[bot]`; o conjunto de tags é idêntico ao conjunto de releases. Primeira: dev.8 (2026-09-29); última: **dev.103** (2026-09-30T11:39:39Z, alvo `be30e62…`, igual ao HEAD da `main`).
- Número da tag = `run_number` do workflow `CI` (há lacunas porque runs sem release também incrementam).
- Ativos de cada release (dev.103): `Lunet-0.0.1-dev.103-arm64.apk` (29 646 075 bytes; digest GitHub `sha256:79e68417bc4bf09545a533e33b5a9d6277abab9957357e32b270fab0a6222c59`), `release-manifest.json`, `release-notes.json`, `release-notes.md`, `SHA256SUMS.txt`. Contagem de downloads do APK: 0.
- As notas de cada release declaram: "**Não** passou por teste em aparelho físico: veja o ROADMAP" (NN-017: validação humana pendente é o estado declarado pelo próprio produto).

| Release de referência para o portal (P1-9) | |
|--------------------------------------------|--|
| Versão canônica (arquivo `VERSION`) | `0.0.1` |
| Última release | `v0.0.1-dev.103` |
| APK | `Lunet-0.0.1-dev.103-arm64.apk`, ABI `arm64-v8a`, minSdk 29 (`release-manifest.json` é gerado por `tools/make-release-files.sh`) |
| Checksum | `SHA256SUMS.txt` na release (e digest do ativo via API) |
| Notas | `release-notes.md` / `release-notes.json` |

## 5. Workflows (GitHub Actions)

FATO: um único workflow, `.github/workflows/ci.yml` ("CI", id 369761041, ativo). Última execução na `main`: run #103, **success** (https://github.com/AbnerCruz/Lunet2D/actions/runs/36709671677).

| Job | Gatilho / condição | O que faz | Dependências de caminho / configuração |
|-----|--------------------|-----------|----------------------------------------|
| `test` | `push`, `pull_request`, `workflow_dispatch` (concorrência por `github.ref`) | `dotnet test --project tests/Lunet.Tests` com .NET 10 | `tests/Lunet.Tests`; `global.json` (runner Microsoft.Testing.Platform) |
| `apk` (needs `test`) | idem | `dotnet workload install android`; `dotnet build src/Lunet.Android/Lunet.Android.csproj -c Debug -p:AndroidSdkDirectory="$ANDROID_HOME" -p:GITHUB_RUN_NUMBER=<run_number>`; coleta `*-Signed.apk`; **exige** o fingerprint SHA-256 do certificado de desenvolvimento estável; publica artifact `lunet-apk` | `src/Lunet.Android/`; `tools/lunet-dev.keystore`; fingerprint esperado está **fixo no workflow** |
| `release` (needs `apk`) | somente `push` na `main`; `permissions: contents: write` | `tag="v$(cat VERSION)-dev.<run_number>"`; `tools/make-release-files.sh`; `gh release create "$tag" … --target <sha> --prerelease` com `github.token` | **`VERSION`**, `CHANGELOG.md`, `tools/make-release-files.sh`; cria tag e release **no repositório onde o workflow roda** |

Não há workflow de Pages, de dependabot nem de segurança (FATO: `list_workflows` → 1).

## 6. GitHub Pages

FATO: não há workflow `pages build and deployment` nem execuções de Pages para o repositório. **NÃO VERIFICADO:** configuração de Pages nas Settings (a API de configuração não está disponível nesta sessão). Sem evidência de site público do Lunet2D.

## 7. Secrets e configuração

- **Secrets declarados em workflow:** nenhum além do `github.token` automático (FATO).
- **Configuração sensível versionada de propósito:** `tools/lunet-dev.keystore`, descrito no `.gitignore` e no ADR 0004 como **chave pública de desenvolvimento** ("chaves de release nunca entram no repositório"); a senha e o alias de assinatura de desenvolvimento aparecem em `src/Lunet.Android/Lunet.Android.csproj`. Valores **não reproduzidos** aqui (MANIFEST §30.2). INFERÊNCIA: como é chave de desenvolvimento pública por decisão do produto, não é vazamento; mas **qualquer** release futura para usuários finais exigirá chave própria fora do repositório (Fase 9/12 do ROADMAP do Lunet).
- **Token de Git do usuário:** `GitAccount.Token` é gravado como **JSON em texto claro** no armazenamento privado do app (`src/Lunet.Core/GitAccount.cs`, FATO). Não é segredo do repositório, mas é um ponto de atenção de MANIFEST §30.2 ("armazenamento seguro apropriado à plataforma"). Não alterar durante a migração; registrar como dívida (§12, R-LUN-8).
- **NÃO VERIFICADO:** proteções de branch, environments, webhooks, variáveis de repositório (exigem permissão de administração).

## 8. Dependências externas

| Dependência | Detalhe (FATO) |
|-------------|----------------|
| Runtime/SDK | .NET 10 SDK (`global.json` exige runner Microsoft.Testing.Platform); workload `android`; Android SDK API 36; JDK compatível (README) |
| Pacotes NuGet | `Microsoft.CodeAnalysis.CSharp` **5.9.0** (Lunet.Compiler); `xunit.v3` com versão **flutuante `*`** (Lunet.Tests) — **risco de reprodutibilidade** (R-LUN-9) |
| Framework | `Lunet.Framework` **não** tem `PackageReference` (garantido por teste) |
| Serviços | GitHub (Git HTTPS via `Lunet.Git`, opcional, com token do usuário); nenhum backend próprio |
| Plataforma alvo | `net10.0-android`, `RuntimeIdentifier android-arm64`, `SupportedOSPlatformVersion 29`, `ApplicationId io.lunet.studio` |
| Actions usadas | `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`, `actions/download-artifact@v4` (por tag, não por SHA) |

## 9. Arquivos normativos

| Arquivo | Papel (FATO) |
|---------|--------------|
| `docs/SPEC.md` (32 KB) | especificação integral do produto, 36 seções; "prompt de implementação integral" |
| `ROADMAP.md` (28 KB) | estado e fases 0–15 + Transversal + Aceitação final; `bash tools/roadmap-status.sh` → **67/201 itens concluídos**; Fases 0–2 completas (11/11, 12/12, 34/34), **Fase 3 (IDE) 4/31** |
| `docs/DEVELOPMENT.md` | rotina de desenvolvimento e protocolo de fechamento de fase |
| `docs/adr/0001…0006` | 6 ADRs próprios do Lunet (fundação Android; compilação e camadas; editor sobre EditText; assinatura estável; Git em C# puro; Preview rápido e isolado) |
| `docs/audits/` | `fase-0`, `fase-1`, `fase-2`, `fase-3`, `fase-3-roteiro`, `TEMPLATE` |
| `AGENTS.md` | "arquivo canônico de instruções para qualquer agente" **do Lunet2D** |
| `AgentsChat.md` | registro assíncrono de comunicação/handoff entre agentes (formato próprio) |
| `CHANGELOG.md`, `README.md`, `LICENSE.md` | changelog (seção única "0.0.1 — em desenvolvimento"), visão geral, licença |
| `docs/api/lunet-framework.json`, `docs/guides/*.md` | documentação da API e guias; **empacotados como assets do APK** (`AndroidAsset` no csproj) |

## 10. Versão

FATO: autoridade única = arquivo **`VERSION`** (`0.0.1`), lido por `Directory.Build.props` (`LunetVersion`). O **número de build** é derivado do CI: tag `v<VERSION>-dev.<run_number>` e `ApplicationVersion` (versionCode Android) = `GITHUB_RUN_NUMBER` (fallback 1 localmente). Em `ecosystem.json`, `version.authority = source-repository` até a migração; após importar, deve passar a `version-file` apontando para `apps/lunet2d/VERSION` (NN-001, NN-014).

## 11. Linha de base (antes)

| Comando | Resultado | Evidência |
|---------|-----------|-----------|
| `dotnet test --project tests/Lunet.Tests` (no commit `be30e62`, .NET SDK 10.0.112, Linux x64) | **286 testes, 286 aprovados, 0 falhas, 0 ignorados** (5 s de execução, 16 s totais) | executado nesta sessão |
| CI `test` + `apk` + `release` na `main` (run #103) | **success**; publicou `v0.0.1-dev.103` | https://github.com/AbnerCruz/Lunet2D/actions/runs/36709671677 |
| Build do APK localmente | **não executável nesta sessão** (sem workload/SDK Android); só o CI gera o APK, como o próprio `AGENTS.md` declara | NÃO VERIFICADO localmente; verificado pelo CI |
| Validação em aparelho | **pendente** para a Fase 3 (README: "os recursos recentes da Fase 3 ainda exigem validação em aparelho") | NN-017 |

## 12. Mapa funcional e arquitetural (ADD-0002 §16)

> **PROPOSTA / INVENTÁRIO.** A classificação futura **não autoriza extrair, mover nem refatorar nada** (NN-013). Descreve o que existe hoje; não presume que existam módulos da visão de produto (`docs/architecture/product-vision.md`). Dependências entre projetos conferidas nos `.csproj` e no teste `ArchitectureTests` do próprio Lunet (FATO).

```text
Android ──► Core, Compiler, Docs, Git, Editor, Framework, Runtime
Runtime ──► Framework          Editor ──► Compiler
Framework, Core, Compiler, Docs, Git ──► (nenhum projeto Lunet)
Tests   ──► todos exceto Android
```

Tamanhos: arquivos `.cs` e linhas (FATO, `find … | wc -l`).

### S-01 · Lunet.Framework — framework 2D do jogo
- **Responsabilidade atual:** API pública que os jogos usam: `Game`/`GameHost`, passo fixo, `SpriteBatch`, `Texture2D`, `SpriteFont`, `RenderTarget2D`, `Shader`, estados gráficos, entrada (toque, gestos, gamepad, teclas, controles virtuais), áudio (`AudioMixer`, `SoundEffect`, `Music`), conteúdo (`ContentManager`, PNG próprio, `Localization`), `Save`, utilidades, geometria, inspeção (atributos).
- **Arquivos:** `src/Lunet.Framework/` — 53 arquivos, 5 343 linhas (Graphics, Input, Audio, Content, Storage, Inspection, Utilities).
- **Dependências:** nenhum projeto Lunet e **nenhum PackageReference** (teste `FrameworkHasNoPackageReferences`).
- **Dados que possui:** nenhum dado persistido próprio (tipos e contratos de runtime; salvamento do jogo via `ISaveStore`).
- **UI:** nenhuma. **Standalone hoje?** sim como biblioteca `net10.0` (independente da IDE, conforme SPEC e ADR 0002). **Específico do Product?** sim (domínio de jogos 2D).
- **Dependências externas:** nenhuma. **Riscos:** API pública documentada em `docs/api/lunet-framework.json` precisa ficar sincronizada (teste de cobertura existe).
- Candidato futuro: [x] **Product Core** · [ ] Library · [ ] Ainda indeterminado *(PROPOSTA)*

### S-02 · Lunet.Runtime — carregamento e execução do jogo
- **Responsabilidade atual:** `GameLoader` (carrega o jogo compilado em contexto descartável; exceções do jogo não derrubam o app), `ObjectInspector` (inspeção por reflexão do jogo em execução).
- **Arquivos:** `src/Lunet.Runtime/` — 3 arquivos, 414 linhas. **Dependências:** `Lunet.Framework`. **Dados:** nenhum. **UI:** não (a UI do Inspector está em Lunet.Android, S-08).
- **Standalone?** sim (testado em desktop). **Específico do Product?** sim. **Externas:** nenhuma. **Riscos:** acoplamento ao formato de assembly do jogo (ADR 0002).
- Candidato futuro: [x] **Product Core** · [ ] Service *(PROPOSTA)*

### S-03 · Lunet.Compiler — compilação Roslyn
- **Responsabilidade atual:** compilar C# do jogo em memória com diagnósticos (arquivo/linha/coluna), compilação incremental, `ChangeClassifier` (só corpos × reinício necessário).
- **Arquivos:** `src/Lunet.Compiler/` — 7 arquivos, 444 linhas. **Dependências:** nenhum projeto Lunet; pacote `Microsoft.CodeAnalysis.CSharp 5.9.0`.
- **Dados:** nenhum. **UI:** não. **Standalone?** sim. **Específico?** domínio C#/Roslyn, parcialmente genérico. **Riscos:** referências do assembly em memória no Android (ADR 0002); versão do Roslyn fixa.
- Candidato futuro: [x] **Product Core** · [ ] Service (Compiler/Build) *(PROPOSTA)*

### S-04 · Lunet.Core — sistema de projetos e estado do app
- **Responsabilidade atual:** `LunetProject`, `ProjectStore`, `ProjectManifest` (`lunet.json`), `ProjectTemplates` (modelos inicial, "Demo: Coletor de moedas", "Laboratório"; 455 linhas), `AutosaveJournal` (`.lunet/autosave`), `AtomicFile`, `ProjectSearch`, `WorkspaceLayout`, `EditorSettings`, `EditorSession`, `GitAccount`, `LogExport`, `WavGenerator`.
- **Arquivos:** `src/Lunet.Core/` — 13 arquivos, 1 365 linhas. **Dependências:** nenhuma.
- **Dados que possui:** projetos como **pastas comuns** (SPEC §5) no armazenamento privado do app; `lunet.json`; `.lunet/autosave`; configurações do editor; layouts de workspace; conta Git.
- **UI:** não (estado). **Standalone?** sim como biblioteca. **Específico?** misto: projeto/manifesto = domínio Lunet; `WorkspaceLayout`, `EditorSettings`, `EditorSession` = estado de UI/shell.
- **Riscos:** mistura de "Project System" com estado de UI no mesmo projeto; token Git em texto claro (R-LUN-8).
- Candidato futuro: [x] **Product Core** (Project System) · [x] **Product Shell** (estado de UI: layout/sessão/configurações) · [ ] Ainda indeterminado para a divisão exata *(PROPOSTA)*

### S-05 · Lunet.Editor — serviços do editor de código
- **Responsabilidade atual:** serviços independentes da view (ADR 0003): `PieceTable`, `UndoHistory`, localizar/substituir, multi-cursor, operações de linha, indentação, `CodeFormatter`, `SyntaxHighlighter`, `CodeAnalyzer` (estrutura, refatoração, renomear, correções rápidas), atalhos.
- **Arquivos:** `src/Lunet.Editor/` — 14 arquivos, 1 844 linhas. **Dependências:** `Lunet.Compiler` (apenas; teste `Editor_DependsOnlyOnCompiler`).
- **Dados:** nenhum. **UI:** não (as views estão em Lunet.Android, S-08: `CodeEditText`, `MinimapView`, `EditorAssistant`). **Standalone?** sim, testado em desktop. **Específico?** C#-específico; não conhece o Host Android.
- **Riscos:** a parte de view está no projeto Android e é acoplada a `EditText`.
- Candidato futuro: [x] **Tool** (Editor; MANIFEST §37 já aponta `Editor.Core/CSharp/Host/SDK`) *(PROPOSTA — não extrair; ver §17 do ADD-0002)*

### S-06 · Lunet.Git — cliente Git em C# puro
- **Responsabilidade atual:** status, diff, commit, histórico, ramos, reverter, mesclar, buscar, pull, push (HTTPS com token), clonar; leitura de objetos/packs, inflate, `.gitignore`, config.
- **Arquivos:** `src/Lunet.Git/` — 13 arquivos, 2 522 linhas. **Dependências:** nenhum projeto Lunet. **Dados:** repositórios Git nas pastas dos projetos. **UI:** não (painel em `GitPanel.cs`, S-08).
- **Standalone?** sim (ADR 0005). **Específico?** não: genérico. **Externas:** servidor Git HTTPS do usuário. **Riscos:** segurança do token (R-LUN-8); escopo de protocolo próprio.
- Candidato futuro: [x] **Service** (Git) · [ ] Library *(PROPOSTA)*

### S-07 · Lunet.Docs — documentação offline
- **Responsabilidade atual:** gerar modelo de API a partir de XML docs, navegador de documentação (namespaces, tipos, membros, busca), `MarkdownLite`, assinaturas.
- **Arquivos:** `src/Lunet.Docs/` — 7 arquivos, 977 linhas. **Dependências:** nenhuma. **Dados:** o conteúdo que exibe (`docs/api/lunet-framework.json`, `docs/guides/*.md`) é embarcado como assets do APK (`AndroidAsset` no csproj de S-08). **UI:** não (painel em `DocumentationPanel.cs`, S-08).
- **Standalone?** sim. **Específico?** o conteúdo é do Lunet; o mecanismo é genérico. 
- Candidato futuro: [x] **Library** · [ ] Tool (Documentation Workspace) · [ ] Ainda indeterminado *(PROPOSTA)*

### S-08 · Lunet.Android — app Android (host/IDE/Preview)
- **Responsabilidade atual:** aplicativo: `MainActivity` (1 114 linhas) + partes `Ide`, `Layout`, `Isolated`, `Benchmark`; painéis Git, Inspector, Documentação, referência rápida; `PreviewHost`/`IsolatedPreview` (Preview em **processo separado**, ADR 0006); backend gráfico OpenGL ES (`GlesBackend`, `PreviewRenderer`); `AndroidAudioBackend`; háptica; views do editor (`CodeEditText`, `MinimapView`, `EditorAssistant`); `SplitterView`; `BuildInfo`.
- **Arquivos:** `src/Lunet.Android/` — 22 arquivos, 5 381 linhas (FATO: não há pasta `Resources` nem layouts `.axml`/`.xml` no projeto; as views são construídas em código).
- **Dependências:** todos os outros projetos `src/`. **Dados:** usa os dados de S-04; assets embarcados (docs). **UI:** toda a UI do produto hoje.
- **Standalone?** é o aplicativo; **não compila fora do CI** (workload Android). **Específico?** sim.
- **Riscos:** `MainActivity` concentra IDE/layout/preview (código histórico); qualquer separação de "Product Shell" × Tools dependeria de refatoração futura (fora da migração).
- Candidato futuro: [x] **Product Shell** (host da IDE e do Preview) · [ ] Adapter (backends Android: GLES/áudio/háptica) · [ ] Ainda indeterminado (a fronteira entre Shell e Tools neste projeto) *(PROPOSTA)*

### S-09 · Lunet.Tests — testes e regras de arquitetura
- **Conteúdo:** 28 arquivos, 5 463 linhas; 286 testes. Inclui `ArchitectureTests` (regras de dependência entre projetos, ausência de pacotes no Framework, ausência de ciclos, presença de SPEC/DEVELOPMENT/AGENTS/ROADMAP, cobertura de fases 0–15 no ROADMAP e no SPEC, análise sintática de todos os `.cs` do Android).
- **Dependências:** projetos de `src/` exceto Android; `xunit.v3 *`. **Standalone?** sim (`dotnet test`).
- **Riscos:** os testes localizam a raiz **subindo até achar `ROADMAP.md`**; dentro de `apps/lunet2d/` o ancestral mais próximo é o do Lunet (comportamento esperado, mas **precisa ser provado após a importação**).
- Candidato futuro: [x] **Product Core** (testes do produto; não extrair) *(PROPOSTA)*

### S-10 · Ferramentas e pipeline de release
- **`tools/make-release-files.sh`** (gera manifest, notas e checksums), **`tools/roadmap-status.sh`**, **`tools/lunet-dev.keystore`**, `.github/workflows/ci.yml`. **Dados:** nenhum. **Standalone?** os scripts dependem de `VERSION`, `CHANGELOG.md`, `ROADMAP.md` na raiz do produto.
- Candidato futuro: [x] **Ainda indeterminado** (pipeline do produto; relacionado a Packaging/Launcher, Fase 4) *(PROPOSTA)*

### S-11 · Documentação e governança próprias
- SPEC, ROADMAP, DEVELOPMENT, ADRs 0001–0006, auditorias, AGENTS.md, AgentsChat.md, CHANGELOG. **Dados:** documentos. **Riscos:** identificadores **locais** ("ADR 0001", "Fase 3", itens de ROADMAP) **colidem em nome** com os do Ecosystem (ex.: `ADR-0001`); ver R-LUN-5.
- Candidato futuro: [x] **Ainda indeterminado**: fica no produto; o relacionamento com a governança do Ecosystem é decisão da migração, não de extração *(PROPOSTA)*

### Resumo do mapa

| Classificação proposta | Subsistemas |
|------------------------|-------------|
| Product Core | S-01 Framework, S-02 Runtime, S-03 Compiler, S-04 Core (parte projetos), S-09 Tests |
| Product Shell | S-08 Android (IDE/Preview host), S-04 Core (parte estado de UI) |
| Tool | S-05 Editor |
| Service | S-06 Git |
| Library | S-07 Docs |
| Adapter | backends Android dentro de S-08 (GLES, áudio, háptica) |
| Ainda indeterminado | fronteira Shell × Tools em S-08; S-10 pipeline; S-11 governança |

**Dependência do Hub ou de outro Product (NN-002, NN-003, NN-023):** **nenhuma.** Nenhum projeto referencia o Hub, o Urbe ou o Ecosystem (FATO: grafo de `ProjectReference` acima; `grep -rniE "ecosystem|hub|urbe"` em `.cs`, `.csproj`, `.props`, `.yml` e `.sh` não retorna nenhuma ocorrência). O produto é empacotado e publicado sem o Hub hoje.

## 13. Riscos e ajustes técnicos inevitáveis previstos

Cada ajuste abaixo seria **necessidade técnica para o produto funcionar em `apps/lunet2d/`** e deve constar, um a um, no plano de importação (P1-3) e no PR (NN-013). **Nenhum foi feito.**

| ID | Risco / ajuste previsto | Por quê (FATO) | Severidade |
|----|------------------------|----------------|------------|
| R-LUN-1 | **`versionCode` do Android** = `GITHUB_RUN_NUMBER` | Em um workflow novo no monorepo o `run_number` reinicia; o Android **recusa** atualizar um APK instalado por outro de `versionCode` menor. Os testadores já têm builds até a 103. Pipeline novo precisa manter `versionCode` monotônico (ex.: deslocamento fixo). | **Alta** |
| R-LUN-2 | **Tags e releases no repositório errado** | `gh release create "v$(cat VERSION)-dev.N"` cria tag e release no repositório onde roda; no Ecosystem colidiria com os de outros produtos e sem prefixo. DEC-0002 já prevê prefixo `<id>/` para tags importadas; o **formato das novas tags** (`lunet2d/v…`) precisa ser decidido no plano. Relacionado a DEC-0008. | Alta |
| R-LUN-3 | **Workflow na raiz do repositório** | `.github/workflows/ci.yml` só executa na raiz do repositório; no monorepo precisa ir para a raiz **com filtro por caminho** (`apps/lunet2d/**`) e `working-directory`, para não rodar a cada mudança do Urbe (NN-014). | Média |
| R-LUN-4 | **Fingerprint de assinatura fixo e keystore** | O workflow exige o fingerprint do keystore de desenvolvimento (caminho relativo `tools/…`). Deve continuar válido em `apps/lunet2d/tools/…`; se a chave mudar, **nenhum** APK instalado se atualiza (ADR 0004). | Média |
| R-LUN-5 | **Colisão de identificadores de documentos** | `AGENTS.md`, `ROADMAP.md`, `docs/adr/0001…0006`, "Fase N" existem no Lunet e no Ecosystem. Dentro de `apps/lunet2d/` os caminhos são únicos, mas as **referências por ID** ("ADR-0001") ficam ambíguas (NN-019). **PROPOSTA (P1-3):** referências qualificadas `lunet2d/ADR-0001`. | Média |
| R-LUN-6 | **`AGENTS.md` local "canônico"** e `AgentsChat.md` | Declara-se "arquivo canônico de instruções para qualquer agente". No monorepo, o `AGENTS.md` local **complementa e não contradiz** a raiz (MANIFEST §26) e `AgentsChat.md` seria uma **segunda fonte de estado de agentes** ao lado de `docs/governance/handoffs/` (NN-001, NN-008). Ajuste de governança **posterior** à importação, como item próprio. | Média |
| R-LUN-7 | **PRs não migram** | 25 PRs fechados (sem abertos) e seus comentários vivem só no GitHub; o repositório de origem não pode ser apagado sem perder essa rastreabilidade (MANIFEST §44.3). | Baixa |
| R-LUN-8 | **Token Git em texto claro** | Ver §7. Dívida de segurança preexistente; **não** corrigir na migração (NN-013). | Média (preexistente) |
| R-LUN-9 | **`xunit.v3` com versão `*`** | Build não reproduzível: a mesma árvore pode compilar com outra versão do xUnit. Não corrigir na migração; registrar. | Baixa |
| R-LUN-10 | **Build Android só no CI** | Não é possível restaurar/validar o APK fora do GitHub Actions (nem nesta sessão). A validação de "build restaurado" (Fase 1, P1-6) depende do runner do GitHub. | Informativa |
| R-LUN-11 | **Validação humana pendente herdada** | O produto declara recursos da Fase 3 sem validação em aparelho. A migração não pode marcar "sem regressão" sem comparar contra esse estado (NN-017). | Informativa |

**Sem risco encontrado:** nenhuma referência absoluta a caminhos do repositório nos `.csproj` (caminhos relativos `..\..\docs\…`, `..\..\tools\…` continuam válidos sob `apps/lunet2d/`); `Directory.Build.props`, `global.json` e `Lunet.slnx` ficam na raiz do produto e **não** devem ser movidos para a raiz do monorepo (seriam aplicados ao Urbe e ao Hub; `Directory.Build.props` de um produto não deve se aplicar a outro).

## 14. Decisões necessárias

- **DEC-0008** — *Canal de releases e de atualização dos produtos após a migração* (afeta R-LUN-1, R-LUN-2; bloqueia P1-3/P1-6). Registrada como pendente em `docs/governance/decisions.json` e visível no portal.
- Questões do plano de importação (P1-3) que o **agente** pode propor sem decisão do proprietário, por estarem dentro de DEC-0002 e NN-013: formato das tags importadas (`lunet2d/v0.0.1-dev.N`), deslocamento do `versionCode`, filtros de caminho do workflow, referências qualificadas de ADR. Serão submetidas à revisão do plano.
- Nenhum outro repositório, fork ou branch de longa duração precisa ser preservado (DEC-0004).
