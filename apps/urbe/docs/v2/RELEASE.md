# Política de release (REQ-006, REQ-019, REQ-065, REQ-066, REQ-081 · ADR-0005)

## Princípios
- **Integração ≠ publicação.** Merge em `main` nunca publica. Só uma tag `v<versão>` (ou `workflow_dispatch` sobre uma tag) publica (`.github/workflows/release.yml`).
- **Fonte única de versão:** `package.json`. `node tools/version.mjs sync` propaga para `package-lock.json`, `src/core/core.js`, `index.html` (`<title>`), `sw.js` (cache) e README; `node tools/version.mjs check` valida (roda no CI).
- **SemVer** com sufixo de canal: `2.0.0-beta.N` (beta) e `2.0.0` (estável). O `CHANGELOG.md` precisa ter o cabeçalho `## v<versão>` — dele saem as notas do Release.
- **Canais.** A versão-ponte `1.8.3-beta` ainda é entregue pelo canal legado `AbnerCruz/Urbe` para alcançar instalações existentes. Ela redireciona futuras atualizações para releases do Product no `AbnerCruz/Ecosystem`: Windows usa o provider GitHub com `tagNamePrefix: "urbe-v"`; Android lista releases e aceita somente `urbe-v<versão>` com `Urbe-<versão>.apk`. É proibido usar `releases/latest` global do monorepo, porque ele mistura Products. A URL/PWA antiga permanece independente até migração própria.

## Checklist de lançamento
1. Todos os itens do ROADMAP da versão em `[x]` (ou justificados), gates da fase verdes, `TRACEABILITY.md` regenerada.
2. Atualizar a versão em `package.json`; `node tools/version.mjs sync`; escrever a entrada do `CHANGELOG.md` (`## v<versão> — título (AAAA-MM-DD)`).
3. `npm run check` verde localmente e no CI do PR; `npm run test:e2e` verde; `node tools/perf/run.mjs` sem regressão além do budget (quando houver, `npm run perf:check`).
4. Integrar em `main` (merge do PR).
5. **Ponte:** publicar `v<versão>` no repositório legado para alcançar instalações anteriores. **Canal direto após a ponte:** no monorepo, publicar a tag `urbe-v<versão>` pelo workflow raiz `urbe-release.yml`; integração em `main` continua sem publicar.
6. O workflow de release verifica tag = `package.json`, roda `npm run check`, constrói o instalador (Windows, NSIS) e o APK (Android). No Ecosystem, o APK só pode ser publicado com os secrets `ANDROID_KEYSTORE_*` contendo a **mesma chave privada** histórica; ausência da chave falha fechado.
7. Conferir o Release, instalar em um aparelho de teste e abrir um vault de fixture.

## Rollback
- **Publicação ruim:** nunca reutilizar tag. Antes do corte, o canal legado continua disponível para recuperação. Depois do corte, remover/invalidar a release direta defeituosa e publicar uma versão superior corrigida; o updater do Urbe filtra apenas tags `urbe-v*`, sem depender do `latest` global do monorepo. O `latest.yml` de cada release Windows continua versionado nos assets.
- **Dados:** a migração 1.x→2.x cria backup restaurável em `.urbe/backup/<data>-1-2/` (comando `workspace.restoreBackup`); os arquivos 1.x nunca são reescritos (ADR-0004). Ver `MIGRATION.md`.

## Dry-run
- `tests/workflows.mjs` prova que push em `main` não publica e que o release exige tag, testes e build.
- Para ensaiar de ponta a ponta sem publicar: criar a tag em um fork/repositório de teste ou executar `app.yml` (`workflow_dispatch`), que só constrói.

## Segurança do pipeline (REQ-066)
- `permissions: contents: read` em todos os workflows; `contents: write` só no job `publicar`.
- Dependabot semanal (npm, GitHub Actions, Gradle). Fixar as actions por SHA de commit está pendente (RM-F0-15).
- O instalador do Windows não é assinado (ADIADO, REQ-059): o SmartScreen avisa na instalação; o APK é assinado com a chave própria.

## Ponte de distribuição P4-9

- `1.8.3-beta` é a ponte: deve ser publicada primeiro no canal legado assinado, antes de desligar o espelho.
- A ponte não muda dados nem exige launcher externo; muda apenas a origem de **futuras** atualizações instaladas.
- Primeiro release direto Android exige a mesma chave privada; chave de debug é proibida para corte.
- Só depois de release direto + atualização em aparelho + preservação do vault o Distribution Profile `current` pode mudar.
- O repositório/Pages legado permanece como recuperação; desativar sincronização automática não significa apagar histórico nem releases.
