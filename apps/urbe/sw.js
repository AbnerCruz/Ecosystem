const CACHE = 'urbe-shell-v0.36.1';
const APP_SHELL = [
  './',
  './index.html',
  './src/styles/base.css',
  './src/styles/shell.css',
  './src/styles/command-palette.css',
  './src/styles/editor-chrome.css',
  './src/styles/explorer-mobile.css',
  './src/styles/composition.css',
  './src/legacy/bootstrap.js',
  './src/core/core.js',
  './src/core/documents.js',
  './src/core/trash.js',
  './src/core/history.js',
  './src/composition/store.js',
  './src/composition/compiler.js',
  './src/composition/ui.js',
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
  './src/app.js',
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

self.addEventListener('install', event => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE);
    await cache.addAll(APP_SHELL);
    for (const url of OPTIONAL_EXTERNAL) {
      try { await cache.add(new Request(url, {mode: 'cors'})); }
      catch (err) { console.warn('Urbe: recurso opcional não pré-cacheado', url, err); }
    }
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
  // Dados de IA são dinâmicos: nunca servir saldo/modelos/uso do cache.
  if (url.hostname === 'openrouter.ai' || url.hostname.endsWith('.openrouter.ai')) {
    event.respondWith(fetch(event.request));
    return;
  }
  event.respondWith((async () => {
    const cached = await caches.match(event.request, {ignoreSearch: false});
    if (cached) return cached;
    try {
      const response = await fetch(event.request);
      if (response && (response.ok || response.type === 'opaque')) {
        const cache = await caches.open(CACHE);
        cache.put(event.request, response.clone()).catch(() => {});
      }
      return response;
    } catch (err) {
      if (event.request.mode === 'navigate') {
        const shell = await caches.match('./index.html');
        if (shell) return shell;
      }
      throw err;
    }
  })());
});
