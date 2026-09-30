# Contrato `UrbeNative` v1

Contrato entre a página do Urbe (`src/`) e as cascas nativas: Electron (Windows) e Capacitor (Android).
Itens do ROADMAP: RM-F4-01, RM-F4-02, RM-F4-03 (REQ-062; segurança em REQ-056). Verificado por `tests/native-contract.mjs`,
`tests/desktop-main.mjs`, `tests/desktop-preload.mjs`, `tests/native-bridge.mjs` e `tests/native-android.mjs`.

## 1. Como a página descobre o que existe

`window.UrbeNative.contract` (fornecido pela casca) e `window.UrbeNativeContract` (sempre presente depois de
`src/native/bridge.js`, inclusive no navegador):

```js
{ version: 1,
  capabilities: ['fs', 'vault', ...],   // o que a casca oferece e FUNCIONA
  unsupported:  ['back', ...] }          // o que a casca NÃO oferece (declarado, não silencioso)
```

Regras: cada capacidade do universo abaixo aparece exatamente uma vez (em `capabilities` ou em `unsupported`);
uma capacidade declarada tem toda a sua API; uma declarada ausente não tem nenhuma parte dela.
A página consulta o contrato em vez de deduzir pela plataforma. Cascas antigas sem `contract` têm o contrato derivado
do formato do objeto pela ponte (`UrbeNativeContract`), com o mesmo resultado.

## 2. Capacidades

| Capacidade | API exigida | Contrato de comportamento |
|---|---|---|
| `fs` | `fs.stat/list/readBytes/writeBytes/mkdir/remove` (e `tree`, `readTexts` quando existirem) | Caminhos relativos à pasta do vault. `stat` de inexistente devolve `null`. `mkdir` é idempotente. `remove(rel, recursive)`. Nada sai da pasta: `..`, `.`, `\`, NUL e caminho que escape por atalho são recusados ou contidos; a raiz não é apagada. |
| `vault` | `vault()` | Devolve `{label, path}` (strings). |
| `openExternal` | `openExternal(url)` | Só `http:`, `https:`, `mailto:`, `tel:`. Qualquer outro esquema (`javascript:`, `file:`, `data:`, `vbscript:`, `intent:`, `app:`...) nunca é aberto (recusa ou ignora). |
| `saveFile` | `saveFile(name, bytes, mime)` | Salva bytes escolhendo/definindo o destino. Resultado `{where: string}`; se a casca oferece diálogo e a pessoa cancela, `{canceled: true}` sem gravar nada. O nome é saneado (sem separadores). |
| `print` | `printHtml(html, name)` | Imprime/gera PDF do HTML. Resultado `{saved: caminho}` (PDF gravado), `{printing: true}` (diálogo do sistema) ou `{canceled: true}`. O HTML é conteúdo do usuário: roda isolado (sem acesso ao app nem ao disco). |
| `update` | `update.check()`, `update.install()`, `update.onStatus(fn)` | `check()` devolve `{state}` com `state` em `none/checking/available/downloading/ready/error`. |
| `back` | `minimize()` + evento `urbeBack` no documento | Botão "voltar" do sistema: fecha o que está por cima; na cidade, minimiza. |
| `storageStatus` | `storage.status()` | Devolve `{sdk: number, ...}` com o estado de permissão de armazenamento. |

## 3. Matriz por plataforma

| Capacidade | Electron (Windows) | Android (Capacitor) | Web (PWA/navegador) |
|---|---|---|---|
| `fs` | declarada | declarada | ausente (File System Access do navegador ou armazenamento do app) |
| `vault` | declarada | declarada | ausente |
| `openExternal` | declarada | declarada | ausente (o navegador abre links) |
| `saveFile` | declarada (`dialog.showSaveDialog`, `fs:saveFile`) | declarada (MediaStore/Downloads/Urbe) | ausente (download do navegador) |
| `print` | declarada (PDF via `app://print`, sessão isolada) | declarada (WebView sem JS) | ausente (`window.print`) |
| `update` | declarada (electron-updater; só no app instalado) | declarada (GitHub Releases, instalação manual do APK) | ausente (service worker) |
| `back` | **ausente** | declarada | ausente |
| `storageStatus` | **ausente** | declarada | ausente |

No web não existe `window.UrbeNative`; `UrbeNativeContract` é `{version: 1, capabilities: [], unsupported: [todas]}`.

## 4. Canais IPC do Electron (`native/desktop/main.js`)

Todos passam por `isTrustedSender` (`native/desktop/guards.js`): somente o quadro principal, com URL de origem exatamente
`app://urbe` (igualdade de origem, não prefixo). Canais: `fs:stat`, `fs:list`, `fs:readBytes`, `fs:writeBytes`, `fs:mkdir`,
`fs:tree`, `fs:readTexts`, `fs:remove`, `fs:saveFile`, `vault:get`, `vault:pick`, `vault:reveal`, `shell:open`, `app:info`,
`print:html`, `update:check`, `update:install`, `update:last`. O `preload.js` roda com `sandbox: true`: só pode fazer `require('electron')`,
por isso é autossuficiente; ele não expõe `ipcRenderer` nem qualquer objeto do Node.

Segurança do Electron (RM-F3-09/10): permissões concedidas só a `app://urbe` (quadro principal) e só
`clipboard-read`, `clipboard-sanitized-write` e `fullscreen`. `media` e `notifications` foram removidas: o código do app não usa
`getUserMedia`, `Notification` nem captura de tela. `protocol.handle('app')` exige host exato `urbe` e serve somente
`index.html`, `manifest.webmanifest`, `*.png` da raiz, `src/**` e `vendor/**` (nunca `package.json`, `native/**`, dotfiles).
`printToPDF` usa `app://print/doc.html` (origem própria, servido da memória) numa sessão não persistente, sem preload, com navegação bloqueada;
nunca `file://`. As variáveis `URBE_TEST_*` só valem com `URBE_TEST_MODE=1` e app não empacotado.

## 5. Android (`native/android`)

`allowBackup="false"` com `dataExtractionRules` e `fullBackupContent` que excluem tudo. `printHtml` usa WebView sem JavaScript, sem acesso a
arquivos e com base `https://print.urbe.invalid/` (origem distinta de `https://localhost`). O `FileProvider` expõe só `cache/urbe-share/`.
`PathGuard` (caminho canônico dentro da base, sem `..`, sem atalho que escape, tamanho máximo de 4 MB por texto) é usado por todas as operações de arquivo do
`UrbeAndroidPlugin` (`readTexts`, `listTree` sem seguir atalhos, `saveFile`); `UrlGuard` valida `openUrl`.

Limite conhecido: as operações de vault feitas pelo plugin `Filesystem` do Capacitor (`stat`, `readdir`, `readFile`, `writeFile`, `mkdir`, `rmdir`, `deleteFile`) executam
código de terceiros que não passa pelo `PathGuard`. Mitigação no lado JS: `src/native/bridge.js` (`safeRel`) recusa `..`, `.`, `\`, `/` inicial, NUL e caminhos longos antes de
chamar o plugin. Atalhos (symlinks) dentro do vault não são detectados nessas operações; o risco depende de outro app criar o atalho em `Documentos/Urbe` (pasta compartilhada). Fechar isso
exigiria mover essas operações para o `UrbeAndroidPlugin` (decisão a registrar em ADR).

## 6. Como testar

- `node tests/native-contract.mjs` — a mesma conformidade contra Electron, Android e web.
- `node tests/desktop-main.mjs`, `node tests/desktop-preload.mjs` — guards e fiação do Electron (Electron simulado).
- `node tests/security/links.mjs` — allowlist de links nos três lugares.
- Java (sem Android SDK): `javac -d out native/android/app/src/main/java/app/urbe/{PathGuard,UrlGuard}.java native/android/app/src/test/java/app/urbe/GuardChecks.java && java -cp out app.urbe.GuardChecks`.
  Os JUnit4 (`PathGuardTest`, `UrlGuardTest`) chamam as mesmas verificações e rodam com `./gradlew testDebugUnitTest` (exige Android SDK).
