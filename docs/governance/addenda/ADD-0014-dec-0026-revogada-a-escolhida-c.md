# ADD-0014 — DEC-0026: a escolha A é revogada e a alternativa C é a decisão

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-03, em conversa com o agente `claude-code`, persistido aqui conforme NN-009.
> **Contexto da mensagem:** a DEC-0026 (quando e onde nasce o primeiro slice do Agent Runtime; aceite do ADR-0017) foi respondida no portal com a alternativa **A**. O proprietário diz que foi um erro e que tentou enviar a substituta.
> **FATO (verificado no repositório e na API do GitHub):**
> - Issue #73, aberta pelo proprietário em 2026-10-03T02:24:04Z, escolheu **A**; o registrador gravou `DEC-0026 = A` (commit `df88852`, `docs/governance/responses/DEC-0026.md`).
> - Issue #74, aberta pelo proprietário 65 segundos depois (02:25:13Z), escolheu **C**. O registrador **recusou de propósito**: "DEC-0026 já está decidida com outra alternativa; o registro não é sobrescrito por esta Issue" (ADR-0007: uma decisão registrada não é sobrescrita por outra Issue). A recusa é o comportamento correto do mecanismo, não uma falha do proprietário.
> - Nenhuma consequência da A havia sido aplicada (`consequencesApplied: false`; nenhum código, diretório ou componente foi criado).
> **Registro do histórico:** a escolha A e as Issues #73 e #74 **permanecem** como histórico; este adendo não as apaga nem as reescreve.
> **Efeito:** [`decisions.json`](../decisions.json) (DEC-0026 passa a C, `record` neste adendo), [ADR-0017](../../adr/0017-agent-runtime-execution-runtime-e-organizacoes.md) `Aceito`, [`ROADMAP.md`](../../../ROADMAP.md) e **DEC-0027** (id e nome do Product de IA). Registro e aplicação entram no mesmo PR, que é crítico e só entra com a autorização do proprietário.

## Mensagem do proprietário (transcrição integral)

> Tomei uma decisão errado no ecosystem e quero revogar ela, tentei enviar a substituta mas não foi validada.
>
> Eu quero a opção C para DEC-0026

## Interpretação (INFERÊNCIA, com a base)

"A decisão errada" é a escolha A da DEC-0026 (a única decisão respondida pelo proprietário nesse intervalo; a substituta é a Issue #74, alternativa C, recusada pelo registrador). A frase final é explícita: **C para a DEC-0026**. Se a interpretação estiver errada, o proprietário corrige com um novo adendo.

## Alternativa C (texto da decisão, como estava no portal)

> **C — Antecipar o R1 dentro de um Product novo de IA** (nome de exibição provisório "Ecosystem AI"), com fronteira de extração desde o primeiro dia (Core sem referência a Product, UI ou provedor, provada por testes de arquitetura; mesmo precedente do Hub.Core) e promoção a Service por Extraction Review quando houver um segundo consumidor real.
>
> Respeita o ADR-0011, dá ao R1 um consumidor real imediato (o próprio Product) e deixa a promoção barata. Cria um Product (o id é estável, NN-019): antes de qualquer código, uma decisão curta de id e nome será aberta no portal, e a entrada em `ecosystem.json` é mudança crítica. Se Urbe em C# e Lunet2D não adotarem, o Core continua local ao Product (resultado válido). Aceita o ADR-0017.

## Consequências (aplicadas no mesmo PR)

1. **DEC-0026 = C**, com este adendo como registro; a escolha A fica explicitamente revogada, com o histórico preservado.
2. **ADR-0017 passa a `Aceito`** (DEC-0026-C): fronteiras D1–D9, e a sequência D7 com o R1 nascendo **dentro do Product de IA**.
3. **Nada é implementado ainda.** O R1 só começa depois da **DEC-0027** (id e nome do Product, aberta no portal por este PR) e das aprovações críticas que ele exigir (entrada em `ecosystem.json`).
4. ROADMAP: P6-1 concluído; P6-2 passa a incluir a criação do Product; dependências ajustadas.
5. Os mecanismos canônicos não foram alterados: a recusa do registrador continua valendo. Reverter uma decisão registrada passa por **registro explícito** (como este adendo), nunca por Issue que sobrescreve em silêncio.
