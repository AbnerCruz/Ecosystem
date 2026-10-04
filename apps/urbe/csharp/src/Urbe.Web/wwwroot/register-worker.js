// Adapter de bootstrap PWA; não contém estado nem regras de produto.
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('service-worker.js').catch(error => console.error('Service worker:', error));
}
