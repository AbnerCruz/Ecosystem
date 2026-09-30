# Threat model — Urbe 1.8.2-beta (base da 2.0)

> Descoberta da issue #33 (AUD-013; REQ-010, 020, 021, 050–060). **[F]** fato, **[I]** inferência, **[P]** proposta.
> Ativos: notas do usuário (vault), chaves de provedores de IA, integridade do app instalado, privacidade do conteúdo enviado à IA.
> Modelo de ameaça: usuário único, local-first; adversários realistas = conteúdo malicioso importado/colado, plugin/terceiro instalado, outro app com acesso ao armazenamento, sincronizador externo, provedor de IA/CDN comprometido, release comprometido.

## 1. Trust boundaries
| Fronteira | De → Para | Confiança |
|---|---|---|
| B1 Vault (arquivos) | disco/sincronizador/outros apps ↔ app | **não confiável** (conteúdo, `.js`, `.css`, `.html`, `.page.json`, zip) |
| B2 Renderer ↔ processo principal (Electron IPC) | `UrbeNative` ↔ `main.js` | confiável só se origem `app://urbe` |
| B3 WebView ↔ Java (Android) | Capacitor/`UrbeAndroid` ↔ SO | confiável; caminhos vêm do JS |
| B4 App ↔ plugin | `new Function` no escopo global | plugin aprovado = **full-trust** |
| B5 App ↔ provedor de IA | HTTPS direto do navegador | provedor vê notas enviadas; chave sai do aparelho |
| B6 App ↔ conteúdo ativo (HTML/embeds/páginas) | iframes/`printHtml`/exports | não confiável |
| B7 App ↔ rede/CDN/updater | jsDelivr, Google Fonts, GitHub API/Release | parcialmente confiável |
| B8 App ↔ links externos | `openExternal`/`openUrl`/`target=_blank` | não confiável (destino) |

## 2. Electron (T-Electron)
- [F] `contextIsolation:true`, `sandbox:true`, `nodeIntegration:false`, preload mínimo (`main.js:46-49`, `preload.js:10-35`); guard de IPC `senderFrame.url.startsWith('app://urbe')` (`main.js:73`); `will-navigate` fora de `app://urbe` chama `external()` e cancela (`:57`); `setWindowOpenHandler` só `blob:app://urbe` e `app://urbe` (`:51-56`); `external()` só `http:`, `https:`, `mailto:` (`:45`).
- [F] Protocolo `app://urbe` **ignora o host** e serve qualquer arquivo sob `APP_DIR` (inclui `package.json`, `native/desktop/*.js`), com `path.relative` contra `..` (`main.js:62-69`). [I] `startsWith('app://urbe')` casa também `app://urbeXYZ` — risco baixo (só código do app).
- [F] **Permission handler** concede `clipboard-read`, `clipboard-sanitized-write`, `media`, `fullscreen`, `notifications` **sem checar a origem** (`main.js:146`). [I] `<iframe>` de embed https poderia receber câmera/microfone/notificações sem prompt.
- [F] `printToPdf` grava HTML em `os.tmpdir()/urbe-print-<Date.now()>.html`, `javascript:true`, `loadFile` (`main.js:98-111`); janela sem preload mas sem handlers de navegação. [I] recursos externos carregam em origem `file://`; nome previsível.
- [F] `URBE_TEST_USERDATA`/`URBE_TEST_VAULT` em produção desativam updater e single-instance (`main.js:15,24`).
- [F] `vault-fs.js`: `resolve()` rejeita `..`, `.`, `\0`, fora da raiz (`:9-16`); `inside()` com `realpath` cobre symlinks/junções (`:18-25`); raiz não apagável (`:46`); tmp+rename com fallback direto em `EPERM/EACCES/EBUSY` (`:49-58`); `readBytes` sem limite; `readTexts` ≤4 MB.
- [F] **Sem CSP** (grep de `Content-Security` vazio em `index.html`, protocolo e Capacitor).
- **Mitigação:** REQ-055 (permission handler por origem, allowlist e host no `app://`, `printToPDF` isolado, guard estrito, hooks de teste inertes), REQ-050, REQ-062.

## 3. Android (T-Android)
- [F] `AndroidManifest.xml`: `allowBackup="true"` (linha 6), `requestLegacyExternalStorage` (7), permissões `INTERNET`, `MANAGE_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE` ≤29, `READ_EXTERNAL_STORAGE` ≤32; Activity `exported=true` (só MAIN/LAUNCHER); FileProvider `exported=false` com `external-path` e `cache-path` em `path="."` (raiz inteira).
- [F] `capacitor.config.json`: `androidScheme:"https"` (origem `https://localhost`), `allowMixedContent:false`.
- [F] Vault `Documents/Urbe` em armazenamento compartilhado (`UrbeAndroidPlugin.java:183-185`). [I] Outros apps com acesso leem/escrevem notas e `Personalização/plugins/*.js`; plugin só roda com hash aprovado localmente. `allowBackup=true` pode incluir localStorage/IndexedDB (chaves de IA) no backup.
- [F] `openUrl` só `https://`, `http://`, `mailto:`, `tel:` (`:89-104`); `saveFile` sanitiza `\/:*?"<>|` e usa MediaStore (`:106-141`); `readTexts` bloqueia `..` e exige `getCanonicalPath().startsWith(base)`, `isFile()`, ≤4 MB (`:221-247`); `listTree` prof. ≤32 sem checar symlinks.
- [F] Leitura/escrita geral pelo plugin `Filesystem` do Capacitor (`directory:DOCUMENTS`) **sem validação de caminho no Java**; defesa só em `checkName` no JS (`bridge.js:19`).
- [F] `printHtml`: WebView com `setJavaScriptEnabled(true)` e `loadDataWithBaseURL("https://localhost/", html, …)` (`:143-178,173`). [I] **mesma origem do app** → script no HTML impresso acessaria localStorage/IndexedDB (chaves de IA); a barreira é só o escape do motor de páginas.
- [F] `bridge.js:208-216`: overlay `blob:` em `<iframe sandbox="allow-scripts allow-popups allow-forms">` sem `allow-same-origin`.
- [F] Update (`bridge.js:244-263`): `fetch` anônimo de `api.github.com/repos/AbnerCruz/Urbe/releases/latest`, abre o `.apk` via `openUrl`, sem hash; defesa = assinatura do APK. Sem chave real (PR/manual) assina com chave **debug** (`build.gradle:38-43`); `minifyEnabled false`.
- **Mitigação:** REQ-056, REQ-053, REQ-062, REQ-059 (adiado).

## 4. HTML, páginas e embeds (T-HTML)
- [F] Prévia de anexo `.html`: `<iframe sandbox="">`, `referrerpolicy=no-referrer` (`app.js:549,2560,2916,3271`); Composição: `sandbox="allow-same-origin"` sem scripts (`composition/ui.js:15`); Studio: `allow-scripts allow-popups` (`studio.js:62`, origem opaca) e `allow-scripts allow-modals` na impressão (`:501`).
- [F] Bloco `embed`: só `https:` e `sandbox="allow-scripts allow-same-origin allow-popups allow-forms"` (`pages/free.js:179`). [I] Combinação anula o sandbox se o alvo for a origem do app; `safeUrl` só exclui esquemas; **não testado**.
- [F] `safeUrl` (`pages/engine.js:13-18`): `http(s)`, `mailto`, `tel`, relativos; `data:` só imagem base64; `cssSafe` escapa `<`; testes `pages.mjs:23-32`, `pages-free.mjs:29-32`.
- [F] Markdown do app: `escapeHTML` inicial e `v22Url` (`app.js:3622-3627`) bloqueia `javascript:`, `vbscript:`, `file:`, `data:` não-imagem.
- [F] Exports carregam Google Fonts e KaTeX de `cdn.jsdelivr.net` (`katex@0.16.47`, sem SRI, `engine.js:650`); PDF.js sem SRI (`sw.js:104-107`, `app.js:2894`).
- [F] `window.open(blobUrl,'_blank','noopener')` para PDF (`app.js:541,2555,2911`), tipo decidido pela extensão. [I] `.pdf` com conteúdo HTML poderia abrir na origem do app; risco baixo.
- **Mitigação:** REQ-057, REQ-050.

## 5. Plugins (T-Plugin)
- [F] Full-trust por design (`plugins.js:3-9`): `new Function('urbe','"use strict";…')` no escopo global (`:97`) com `window`, `fetch`, `localStorage`, IndexedDB (chaves de IA), `UrbeNative`; a `api` limpa comandos/botões/estilos/timers por cortesia.
- [F] Aprovação: `localStorage['urbe.plugins.v1']` chave `vault::path` `{hash,em}` (`:20-31,135`); SHA-256 (`crypto.subtle`) com **fallback FNV-1a 64 bits + tamanho** em contexto não seguro (`:29-30`); hash diferente → `mudou`, para o plugin (`:123`); nada roda sem aprovação (`:121`); modo seguro (`?seguro=1`/`urbe.modoSeguro`).
- [F] O diálogo (`customize/panel.js:266`) diz "vai rodar com acesso às suas notas" e **não mostra código nem capacidades**. [I] Subestima o alcance (rede, chaves, bridge).
- [F] `api.ia.ferramenta` cria ferramentas do Assistente com `access:'read'` por padrão, sem aprovação (`plugins.js:74-77`, `agent.js:25-27`); `api.notas.escrever` bloqueia `Personalização/plugins/` (`:60`) mas o código pode ignorar a `api`; `api.dados` usa localStorage sem cota.
- [F] O Assistente **pode escrever plugins** (`customize/ai-tools.js:5,34-38`; `validPath` aceita `.js`, `ai/tools.js`); com política `auto` sem aprovação; execução ainda exige o usuário ligar.
- [I] Na primeira aprovação o usuário aprova o que estiver no disco naquele instante (sincronizador pode ter alterado).
- [F] Testes: `customize.mjs:66-77`; nada testa isolamento de globais, fallback FNV ou painel.
- **[C]** Modelo mantido (ADR-0002): **REQ-051** (mostrar código/hash/alcance; SHA-256 obrigatório; docs) e **REQ-032** (contrato versionado). Isolamento **ADIADO (REQ-052)**.

## 6. IA (T-IA)
- [F] Chaves em texto puro no IDB `urbe-ai` (`store.js:3-6,22`); UI `<input type="password">` (`ai/ui.js:374`) e máscara `•••`+4 (`:343`); migração da chave legada (`store.js:36-40`). [I] qualquer código na origem (plugin, XSS) lê a chave (`app.js:3607-3610` cita isso).
- [F] Providers: OpenRouter, Anthropic, OpenAI, Gemini, Ollama, "compatível" com `baseUrl` livre (`providers.js:12-18`); `Authorization: Bearer` (`:102`); Anthropic `x-api-key` + `anthropic-dangerous-direct-browser-access:true` (`:160`); `HTTP-Referer` no OpenRouter; retry 408/409/425/429/5xx ≤4. [I] chave vai para qualquer URL digitada (esperado, sem allowlist).
- [F] Privacidade: notas lidas pelas ferramentas são enviadas ao provedor (README/tutorial).
- [F] Agentes (`ai/agent.js:19-30`): `access` `read|write|destructive|ui|memory|plan`; política `ask` (padrão), `auto`, `readonly`; `delete_note` sempre pede aprovação; desfazer com detecção de edição posterior; `validPath` bloqueia `..`, ponto inicial, caracteres proibidos; extensões `md/markdown/txt/html/js/mjs/css/json/yaml/csv` — a IA pode criar `.html`, `.js`, `.css` no vault, inclusive `Personalização/estilos/*.css` aplicados **sem aprovação de código** ([I] CSS `url()` pode requisitar recursos externos).
- [I] Injeção de prompt via nota importada/colada; mitigações atuais: aprovação de escrita (em `ask`), lixeira, desfazer.
- [F] UI escapa Markdown do modelo (`ai/ui.js:11`; teste `ai-agent.mjs:216`); SW nunca cacheia outras origens (`sw.js:136-140`).
- **Mitigação:** REQ-053, REQ-060; cripto nativa ADIADA (REQ-054).

## 7. Vault, importação, links e updater
- **T-Vault/T-Import.** Desktop: `vault-fs.js` (testado `native-bridge.mjs:23-29`); Android: `Filesystem` + `checkName` (`bridge.js:19`) + `readTexts` canônico; Web: `getDirectoryHandle('..')` recusado; IDB: caminhos são chaves. [F] Importação por zip (`app.js:1718-1723`) usa nomes de entrada como caminho relativo **sem filtro `..`**, **sem limite de tamanho/contagem**; [I] zip-slip neutralizado pelas camadas inferiores, **não testado**. → REQ-058.
- **T-Links.** [F] Web `target="_blank" rel="noopener"`; nativo encaminha `http(s)`, `mailto`, `tel` ao sistema (`bridge.js:174-190`, `main.js:45`).
- **T-Updater.** [F] Electron: `electron-updater` GitHub provider, `allowPrerelease:true`, `autoDownload:true`, `autoInstallOnAppQuit:true`, 8 s após abrir e a cada 6 h (`main.js:116-128`); instalador **sem assinatura** (`CSC_IDENTITY_AUTO_DISCOVERY:'false'`); [I] integridade só pelo `sha512` do `latest.yml` no mesmo release — release comprometido serve ambos. → REQ-059 (adiado), REQ-066.
- **Modo seguro** desliga estilos e plugins (`customize.js:143,271`). **Telemetria:** nenhuma; rede só para provedor de IA, GitHub API (Android), jsDelivr e Google Fonts (exports).

## 8. Matriz ameaça → mitigação → REQ → teste
| ID | Ameaça | Severidade [I] | Mitigação | REQ | Teste/gate |
|---|---|---|---|---|---|
| T-1 | XSS/script injetado sem CSP | alta | CSP por plataforma | REQ-050 | teste de política + E2E (G3) |
| T-Plugin | plugin malicioso/alterado aprovado sem compreensão | alta | mostrar código/hash/alcance, SHA-256 | REQ-051, REQ-032 | teste UI + `customize.mjs` ampliado (G3) |
| T-IA | chave de IA exfiltrada; CSS/JS criados por IA | alta | allowBackup, política de ativos IA | REQ-053, REQ-060 | testes de export/manifest, política (G3) |
| T-Electron | permissões amplas, protocolo sem allowlist | média | REQ-055 | REQ-055 | teste de `main.js` (G3) |
| T-Android | `printHtml` na origem do app; backup | média/alta | REQ-056 | REQ-056 | teste Java/instrumentado (G3) |
| T-HTML | embed same-origin, CDN sem SRI | média | REQ-057 | REQ-057 | teste de sandbox/SRI (G3) |
| T-Import | zip-slip/bomb | média | limites e filtro | REQ-058 | testes de propriedade (G3) |
| T-Updater | release comprometido/instalador não assinado | média | ADIADO documentado | REQ-059 | — (documentado em REQ-082) |
| T-Vault | dados perdidos por versão desconhecida | alta (dados) | proteção forward | REQ-035 | fixtures (G1) |
| T-Links | links externos maliciosos | baixa | manter allowlist de esquemas | REQ-010 | teste de esquemas (G3) |

## 9. Não objetivos de segurança da 2.0 [C]
Isolamento de plugins (ADIADO), criptografia nativa de chaves (ADIADO), assinatura de instalador (ADIADO), sandbox de processo por plugin, proteção contra dispositivo comprometido/root.
