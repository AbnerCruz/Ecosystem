# Plataformas, distribuição e licença — Urbe 1.8.2-beta

> Descoberta da issue #33 (REQ-008, 017, 076–082). **[F]** fato, **[I]** inferência, **[P]** proposta.

## 1. Matriz de plataformas [F]
| Aspecto | Web/PWA | Windows (Electron) | Android (Capacitor) |
|---|---|---|---|
| Entrada | `index.html` + `sw.js`, `manifest.webmanifest` (standalone, ícones any/maskable) | `native/desktop/main.js`, `app://urbe/index.html` (`main.js:14,58`) | `native/www` (cópia sem SW), origem `https://localhost` |
| Storage | IDB `knowledge-city` (`kv`,`fs`,`blobs`, `app.js:1519-1526`) ou **pasta** via File System Access (Chrome/Edge desktop, `app.js:1910,2263`); `Disco.modo` `interno`/`pasta` | pasta real (`Documentos\Urbe`, configurável via `vault:pick`; `userData/config.json`); pode ser OneDrive/Dropbox | `Documents/Urbe` via Capacitor Filesystem (`bridge.js:226`) + plugin próprio; pede "acesso a todos os arquivos" (Android 11+) |
| Modelo | vault interno com várias cidades, migradas para vault único | **um** vault `Urbe` = a pasta (`bridge.js:119-129`) | idem |
| Mudanças externas | não | chokidar em tempo real (`main.js:31-41`) | relê ao voltar ao app (`visibilitychange`, throttle 3 s, `bridge.js:152`) |
| Atualização | SW rede-primeiro 4 s, `skipWaiting`; banner "Recarregar" (`app.js:2940+`) | `electron-updater` background, "Reiniciar e atualizar" (só empacotado, `main.js:117`) | "Baixar e instalar": abre `.apk` do GitHub no navegador; instalação manual |
| Distribuição | hospedagem estática (README) | `.exe` NSIS (`Urbe-Setup-<v>.exe`, x64, **não assinado**) + `latest.yml` no Release | `.apk` assinado com keystore de secret; `applicationId app.urbe`; `minSdk 24`, `target/compileSdk 36` |
| PDF | impressão do navegador | `printToPDF` (`main.js:98-111`) | `PrintManager` |
| Exportações | download do navegador | [F] sem `saveFile` em `preload.js`; segue Chromium | `Downloads/Urbe` via MediaStore |
| Links externos | `target=_blank` | `shell.openExternal` restrito | `openUrl` restrito |
| Voltar | — | — | `OnBackPressedCallback` → `urbeBack` → Esc/botões (`MainActivity.java`, `bridge.js:191-206`) |
| Instância única | — | `requestSingleInstanceLock` (`main.js:140`) | `launchMode=singleTask` |

- [F] `build.linux` (AppImage) existe em `package.json`, mas `app.yml` só constrói Windows e Android. [I] mac/Linux sem pipeline (REQ-078 ADIADO).
- [F] `tools/build-www.mjs` copia app para `native/www` sem `sw.js`; `bridge.js:144` desregistra SWs no nativo.

## 2. Contrato `UrbeNative` [F→P]
- [F] `window.UrbeNative` é exposto por `preload.js:10-35` (Electron: fs relativo ao vault, vault, updater, `openExternal`, `printHtml`) e por `bridge.js:207-` (`capacitorNative`) sobre Filesystem + UrbeAndroid; web sem ele (`bridge.js:12`).
- [P] REQ-076: contrato versionado (`UrbeNative.contract = {version, capabilities[]}`), teste de conformidade por adapter (web/Electron/Android), `capabilities` como verdade única para paridade (REQ-079).
- [F] Funções por adapter: Electron `fs:*` (stat, list, readBytes, writeBytes, mkdir, tree, readTexts, remove), `vault:get/pick/reveal`, `shell:open`, `app:info`, `print:html`, `update:check/install/last`; Android `getInfo`, `storageStatus`, `requestAllFiles`, `openUrl`, `saveFile`, `printHtml`, `listTree`, `readTexts`, `minimize`.

## 3. Build e release atuais [F]
- `package.json`: `version 1.8.2-beta`, `private:true`, `license:"UNLICENSED"`; scripts `desktop`, `desktop:build`, `www`, `android:sync`, `test`; `build.files` = `index.html`, manifest, `*.png`, `src/**`, `vendor/**`, `native/desktop/*.js`, `package.json`.
- `app.yml`: push em `main` com versão inédita publica Release (AUD-006, ver TEST-MATRIX §4); actions por tag; `contents: write` global.
- [F] Android `versionName/versionCode` derivam de `package.json` (`build.gradle:3-19`); Electron usa `app.getVersion()` e `${version}` no artefato.
- [F] `REPO='AbnerCruz/Urbe'` hardcoded em `bridge.js` e no bloco `publish`.

## 4. Licença e distribuição [F→C]
- [F] `package.json` `license:"UNLICENSED"`; **não há `LICENSE` na raiz**; repositório público (AUD-016).
- [F] Dependências embutidas: KaTeX 0.16.x MIT (`vendor/katex/LICENSE`, 20 fontes), JSZip 3.10.1 (dual MIT/GPLv3) e pako (MIT) em `src/legacy/bootstrap.js` com cabeçalhos preservados; PDF.js (Apache-2.0) por CDN em runtime; Electron/Chromium, Capacitor, chokidar, electron-updater em node_modules (avisos no instalador [I]). **Não há `THIRD-PARTY`.**
- **[C]** (decisão do proprietário, ADR-0003): licença **fonte-disponível/proprietária**. Consequências: `LICENSE` explícito de direitos reservados com permissões declaradas (leitura/uso pessoal), `THIRD-PARTY-NOTICES` (JSZip sob MIT), cabeçalho no README e `package.json` coerente (`SEE LICENSE IN LICENSE`). **[bloqueio residual]** o texto final da licença (permissões de uso/redistribuição/contribuição, CLA) depende do proprietário; até lá o gate G4 de distribuição pública fica **BLOQUEADO** (REQ-080), sem impedir o desenvolvimento.

## 5. Política de release e versionamento [P recomendada → ADR-0005]
- SemVer com sufixo de canal (`2.0.0-beta.N`, `2.0.0`).
- Fonte única de versão: `package.json` → script `tools/version.mjs` valida/sincroniza `core.js`, `index.html`, `sw.js`, CHANGELOG (REQ-065).
- Release: **tag `v<versão>` ou `workflow_dispatch` aprovado**, com `needs` nos testes e nos gates de dados; separado de push em `main` (REQ-066). Rollback: retirar Release/marcar como pre-release e republicar `latest.yml` anterior; para dados, restaurar backup de migração (REQ-038).
- Canais: `beta` (pre-release) e `stable` (após G6). Notas de release do CHANGELOG (`## v<versão>`).
- Assinatura Windows e verificação Android: ADIADAS (REQ-059); documentar SmartScreen/APK (REQ-082).

## 6. Riscos de plataforma
| Risco | Plataforma | REQ |
|---|---|---|
| `MANAGE_EXTERNAL_STORAGE` restringe distribuição (Play) | Android | REQ-077 (adiado) |
| Sem seletor de pasta/vaults múltiplos no app instalado | Android/Windows | OD-06 |
| Modo IDB interno é dado do navegador | Web | REQ-046 |
| Aprovação de plugin/estado local ligado ao aparelho | todas | REQ-044, REQ-047 |
| Ollama local (`http://localhost:11434`) pouco útil no WebView Android [I] | Android | documentar |
| Sem pipeline mac/Linux | — | REQ-078 (adiado) |
