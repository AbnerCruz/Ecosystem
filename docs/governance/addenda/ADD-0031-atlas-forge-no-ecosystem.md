# ADD-0031 — Ordem do proprietário: incorporar Atlas Forge ao Ecosystem

**Data:** 2026-10-22. **Autoridade:** instrução explícita do proprietário nesta conversa, após a entrega do editor offline, fontes/exemplos, resultados de testes e PR #658. **Registro:** DEC-0054. **Escopo:** incorporação do Atlas Forge como aplicação independente `atlas-forge`, em `apps/atlas-forge`, conforme opção A apresentada no ADR-0035.

## O que o proprietário disse

Depois de receber a entrega funcional, os links para os artefatos e o PR aberto, o proprietário respondeu:

> Pode por no ecosystem meu rei

## Decisão e aplicação

A resposta aprova a incorporação proposta: código no diretório canônico do aplicativo, registro em `ecosystem.json`, documentação local e CI seletivo, preservando a implementação JS/Canvas e seu funcionamento independente/offline. A exceção de stack é local ao editor solicitado, não uma mudança do padrão C# da plataforma.

O registro não transforma Atlas Forge em Tool global multi-host ou capability pública. Não há consumidor externo integrado que autorize extração compartilhada; não se altera outro Product, não se reintroduz simulação em aplicativo de notas e não se publica release de produção como consequência automática.

A integração segue o integrador e as verificações vigentes. Este arquivo registra a instrução real do proprietário; não inventa clique no portal, evento de label, resultado de CI, validação Android ou merge que ainda não ocorreu.

## Evidência e limites

Objeto apresentado e aprovado: PR [#658](https://github.com/AbnerCruz/Ecosystem/pull/658), editor offline e pacote de revisão da Alpha 0.2. O escopo completo e os limites permanecem no mandato e no roadmap do aplicativo. Corpus real da Alpha e Android físico continuam pendentes.
