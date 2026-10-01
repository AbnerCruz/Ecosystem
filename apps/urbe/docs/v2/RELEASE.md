# Política de release (REQ-006, REQ-019, REQ-065, REQ-066, REQ-081 · ADR-0005)

## Princípios
- **Integração ≠ publicação.** Merge em `main` nunca publica. Só uma tag `v<versão>` (ou `workflow_dispatch` sobre uma tag) publica (`.github/workflows/release.yml`).
- **Fonte única de versão:** `package.json`. `node tools/version.mjs sync` propaga para `package-lock.json`, `src/core/core.js`, `index.html` (`<title>`), `sw.js` (cache) e README; `node tools/version.mjs check` valida (roda no CI).
- **SemVer** com sufixo de canal: `2.0.0-beta.N` (beta) e `2.0.0` (estável). O `CHANGELOG.md` precisa ter o cabeçalho `## v<versão>` — dele saem as notas do Release.
- **Canais.** Hoje os apps instalados se atualizam por `releases/latest` (Android) e por `electron-updater` com `allowPrerelease` (Windows): por isso betas também são publicados como "latest". Separar `beta` e `stable` no updater é trabalho futuro (não faz parte da 2.0).

## Checklist de lançamento
1. Todos os itens do ROADMAP da versão em `[x]` (ou justificados), gates da fase verdes, `TRACEABILITY.md` regenerada.
2. Atualizar a versão em `package.json`; `node tools/version.mjs sync`; escrever a entrada do `CHANGELOG.md` (`## v<versão> — título (AAAA-MM-DD)`).
3. `npm run check` verde localmente e no CI do PR; `npm run test:e2e` verde; `node tools/perf/run.mjs` sem regressão além do budget (quando houver, `npm run perf:check`).
4. Integrar em `main` (merge do PR).
5. Criar a tag na `main`: `git tag v<versão> && git push origin v<versão>`.
6. O workflow `release.yml`: verifica tag = `package.json`, roda `npm run check`, constrói o instalador (Windows, NSIS) e o APK (Android, assinado com a chave do repositório — sem os secrets `ANDROID_KEYSTORE_*` o build falha) e publica o Release com as notas do CHANGELOG.
7. Conferir o Release, instalar em um aparelho de teste e abrir um vault de fixture.

## Rollback
- **Publicação ruim:** apagar o Release e a tag no GitHub (ou marcar o Release como pre-release) para que `releases/latest` volte a apontar para a versão anterior; o `latest.yml` anterior continua nos assets do Release antigo. Corrigir e publicar uma versão nova (nunca reutilizar tag).
- **Dados:** a migração 1.x→2.x cria backup restaurável em `.urbe/backup/<data>-1-2/` (comando `workspace.restoreBackup`); os arquivos 1.x nunca são reescritos (ADR-0004). Ver `MIGRATION.md`.

## Dry-run
- `tests/workflows.mjs` prova que push em `main` não publica e que o release exige tag, testes e build.
- Para ensaiar de ponta a ponta sem publicar: criar a tag em um fork/repositório de teste ou executar `app.yml` (`workflow_dispatch`), que só constrói.

## Segurança do pipeline (REQ-066)
- `permissions: contents: read` em todos os workflows; `contents: write` só no job `publicar`.
- Dependabot semanal (npm, GitHub Actions, Gradle). Fixar as actions por SHA de commit está pendente (RM-F0-15).
- O instalador do Windows não é assinado (ADIADO, REQ-059): o SmartScreen avisa na instalação; o APK é assinado com a chave própria.
