# ADR-0002 — Confiança de plugins: full-trust aprovado com UX honesta

- Status: Accepted
- Data: 2026-09-30
- Requisitos: REQ-020, REQ-032, REQ-051, REQ-052
- Decisores: Abner P. S. Cruz (proprietário)

## Contexto

Plugins são `.js` executados por `new Function` no escopo global do app (`plugins.js:97`) com acesso a `window`, `fetch`, `localStorage`, IndexedDB (chaves de IA) e `UrbeNative`. Há aprovação local por hash (SHA-256 com fallback FNV) e bloqueio de autoexecução quando o código muda. O diálogo não mostra o código nem o alcance real. O Assistente pode escrever plugins.

## Forças

- compatibilidade com plugins 1.x;
- honestidade sobre o poder real do plugin;
- custo/escopo de isolar código na 2.0;
- superfícies de IA e sincronizadores que alteram arquivos.

## Alternativas consideradas

### A — Full-trust aprovado com UX honesta

Mantém contrato e plugins existentes; exige mostrar código, hash e alcance e endurecer o hash.

### B — Isolamento por capabilities (Worker/iframe)

Segurança maior, mas quebra plugins 1.x e amplia muito o escopo.

### C — Híbrido: full-trust agora, manifesto de capabilities exibido

Mais trabalho de UX sem enforcement.

## Decisão

Adotar **A**. Antes de aprovar, o app mostra o código (ou diff), o hash SHA-256 e o alcance real (rede, chaves, filesystem, bridge). SHA-256 é obrigatório: sem `crypto.subtle`, o plugin não executa (sem fallback FNV). Documentação e testes descrevem plugins como código com o mesmo poder do app. A API `urbe.*` e o serviço `world.custom` passam a ter contrato versionado (REQ-032). Isolamento fica ADIADO (REQ-052), com gate de reavaliação: contrato de API versionado estável.

## Consequências positivas

- decisão explícita e coerente com o produto
- compatibilidade 1.x preservada

## Consequências negativas / trade-offs

- a superfície de risco permanece: um plugin aprovado tem poder total

## Migração

Sem migração de dados; plugins aprovados por FNV precisam ser reaprovados com SHA-256 (comunicar).

## Validação

testes de `customize.mjs` ampliados (hash, reaprovação, painel), teste da UI de aprovação, THREAT-MODEL §5 (G3).

## Relações

- documentos afetados: THREAT-MODEL, SPEC §8, tutorial (Personalização/Plugins)
