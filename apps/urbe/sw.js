const CACHE = 'urbe-shell-v1.2.0-beta';
const APP_SHELL = [
  './',
  './index.html',
  './src/styles/base.css',
  './src/styles/shell.css',
  './src/styles/command-palette.css',
  './src/styles/editor-chrome.css',
  './src/styles/explorer-mobile.css',
  './src/styles/composition.css',
  './src/styles/theme.css',
  './src/ui/icons.js',
  './src/ui/dialogs.js',
  './src/ui/touch-debug.js',
  './src/ui/settings.js',
  './src/customize/customize.js',
  './src/customize/plugins.js',
  './src/customize/ai-tools.js',
  './src/customize/panel.js',
  './src/tutorial/content.js',
  './src/tutorial/tutorial.js',
  './src/styles/customize.css',
  './src/world/terrain.js',
  './src/world/pixel-art.js',
  './src/world/chunk-worker.js',
  './src/legacy/bootstrap.js',
  './src/core/core.js',
  './src/core/documents.js',
  './src/core/trash.js',
  './src/core/history.js',
  './src/composition/store.js',
  './src/composition/compiler.js',
  './src/composition/ui.js',
  './src/core/knowledge-index.js',
  './src/persistence/workspace.js',
  './src/world/projection.js',
  './src/world/system.js',
  './src/world/roads.js',
  './src/world/renderer.js',
  './src/world/touch.js',
  './src/core/scheduler.js',
  './src/explorer/model.js',
  './src/explorer/operations.js',
  './src/explorer/mobile-ui.js',
  './src/core/diagnostics.js',
  './src/editor/session.js',
  './src/editor/context.js',
  './src/editor/workspace.js',
  './src/editor/split.js',
  './src/editor/split-ui.js',
  './src/editor/find-ui.js',
  './src/editor/chrome.js',
  './src/core/keymap.js',
  './src/ui/command-palette.js',
  './src/ui/quick-open.js',
  './src/ai/providers.js',
  './src/ai/tools.js',
  './src/ai/store.js',
  './src/ai/agent.js',
  './src/ai/ui.js',
  './src/styles/agent.css',
  './src/styles/pages.css',
  './vendor/katex/katex.min.css',
  './vendor/katex/katex.min.js',
  './vendor/katex/fonts/KaTeX_AMS-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Caligraphic-Bold.woff2',
  './vendor/katex/fonts/KaTeX_Caligraphic-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Fraktur-Bold.woff2',
  './vendor/katex/fonts/KaTeX_Fraktur-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Main-Bold.woff2',
  './vendor/katex/fonts/KaTeX_Main-BoldItalic.woff2',
  './vendor/katex/fonts/KaTeX_Main-Italic.woff2',
  './vendor/katex/fonts/KaTeX_Main-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Math-BoldItalic.woff2',
  './vendor/katex/fonts/KaTeX_Math-Italic.woff2',
  './vendor/katex/fonts/KaTeX_SansSerif-Bold.woff2',
  './vendor/katex/fonts/KaTeX_SansSerif-Italic.woff2',
  './vendor/katex/fonts/KaTeX_SansSerif-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Script-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Size1-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Size2-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Size3-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Size4-Regular.woff2',
  './vendor/katex/fonts/KaTeX_Typewriter-Regular.woff2',
  './src/math/core.js',
  './src/math/editor.js',
  './src/styles/math.css',
  './src/pages/engine.js',
  './src/pages/templates.js',
  './src/pages/ai-tools.js',
  './src/pages/studio.js',
  './src/app.js',
  './src/editor/visual-tools.js',
  './src/ui/tips.js',
  './manifest.webmanifest',
  './icon-192.png',
  './icon-512.png',
  './icon-maskable-512.png',
  './apple-touch-icon.png'
];
const OPTIONAL_EXTERNAL = [
  'https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.min.mjs',
  'https://cdn.jsdelivr.net/npm/pdfjs-dist@6.3.289/build/pdf.worker.min.mjs'
];

const NETWORK_TIMEOUT = 4000;

self.addEventListener('install', event => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE);
    await cache.addAll(APP_SHELL);
    for (const url of OPTIONAL_EXTERNAL) {
      try { await cache.add(new Request(url, {mode: 'cors'})); }
      catch (err) { console.warn('Urbe: recurso opcional não pré-cacheado', url, err); }
    }
    // Versão nova assume sem esperar o botão “Recarregar”: antes o celular ficava
    // preso na versão antiga (com bugs já corrigidos) até alguém tocar no aviso.
    await self.skipWaiting();
  })());
});

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.filter(k => k.startsWith('urbe-shell-') && k !== CACHE).map(k => caches.delete(k)));
    await self.clients.claim();
  })());
});

self.addEventListener('message', event => {
  if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET') return;
  const url = new URL(event.request.url);
  // Outras origens (APIs de modelos, servidores locais) nunca passam pelo cache:
  // listas de modelos, saldos e respostas são dinâmicos. Só a PDF.js externa é guardada.
  if (url.origin !== self.location.origin && !OPTIONAL_EXTERNAL.includes(url.href)) return;
  // Rede primeiro: com internet o app é sempre a versão publicada; o cache só
  // serve sem conexão (ou quando a rede demora demais para responder).
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    // Navegações não aceitam RequestInit; revalida pela URL (sem usar cache HTTP velho).
    const same = url.origin === self.location.origin;
    const network = (event.request.mode === 'navigate' ? fetch(url.href, {cache: 'no-cache', credentials: 'same-origin'}) : fetch(event.request, same ? {cache: 'no-cache'} : undefined)).then(response => {
      // Navegação não pode receber resposta “redirecionada”; recria sem essa marca.
      if (response && response.redirected && event.request.mode === 'navigate') response = new Response(response.body, {status: response.status, statusText: response.statusText, headers: response.headers});
      if (response && (response.ok || response.type === 'opaque')) cache.put(event.request, response.clone()).catch(() => {});
      return response;
    });
    const cached = await cache.match(event.request) || (event.request.mode === 'navigate' ? await cache.match('./index.html') : undefined);
    if (!cached) return network;
    network.catch(() => {});
    let timer;
    const slow = new Promise(resolve => { timer = setTimeout(() => resolve(cached), NETWORK_TIMEOUT); });
    try { return await Promise.race([network, slow]); }
    catch (err) { return cached; }
    finally { clearTimeout(timer); }
  })());
});
