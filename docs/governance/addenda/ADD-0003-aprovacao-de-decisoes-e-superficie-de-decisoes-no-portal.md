# ADD-0003 — Aprovação das decisões pendentes e superfície de decisões no portal

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-09-30, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Registros de decisão:** DEC-0001, DEC-0002, DEC-0003, DEC-0004 (aprovadas) e DEC-0007 (nova) em [`../decisions.json`](../decisions.json).
>
> **Contexto da mensagem:** resposta ao resumo das pendências do agente, que listava DEC-0001…DEC-0004 **com a recomendação do agente para cada uma**, as validações humanas (P0-15, P0-30, gate da Fase 0) e a próxima tarefa (P1-1/P1-2).

## Mensagem do proprietário (transcrição integral)

> Aprova tudo, faz o merge. Quero sempre a decisão a ser tomada e o objeto no site para facilitar. De resto prossiga

## Interpretação registrada pelo agente (para conferência do proprietário)

Distingue o que o texto decide do que o agente inferiu (MANIFEST §53). O proprietário pode corrigir qualquer ponto abaixo; uma correção é uma nova decisão registrada.

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "Aprova tudo" | Aprova **a recomendação do agente** para cada decisão pendente apresentada: **DEC-0001** aceitar os ADRs 0001–0006 como estão; **DEC-0002** técnica A (`git filter-repo` para subdiretório + merge, com `commit-map` versionado); **DEC-0003** Issues do GitHub como autoridade do estado vivo das tarefas, ROADMAP como autoridade de escopo e IDs, handoffs como resultado e evidência; **DEC-0004** `AbnerCruz/Lunet2D` e `AbnerCruz/Urbe` são as origens canônicas. | Inferência do agente sobre "tudo". |
| "Aprova tudo" (revisões) | Aprova também a **revisão do alinhamento do ADD-0002** (P0-30) e o **gate da Fase 0**, que dependiam de revisão do proprietário. | Inferência do agente. |
| "Aprova tudo" (limite) | **Não** é tratada como a validação em aparelho do portal (**P0-15**: layout, toque e legibilidade no celular), porque NN-017 exige evidência de que a validação de fato ocorreu no dispositivo. P0-15 permanece pendente até o proprietário confirmar. | Decisão conservadora do agente. |
| "faz o merge" | Autoriza levar a branch `claude/new-session-vfwhkv` (estado atual) para a `main`. | Decisão do proprietário. |
| "Quero sempre a decisão a ser tomada e o objeto no site para facilitar" | Toda decisão pendente do proprietário **e** toda validação humana pendente devem aparecer no portal, sempre acompanhadas do **objeto** a ser decidido ou validado (documento, artefato, URL). É regra permanente. | Decisão do proprietário (DEC-0007). |
| "De resto prossiga" | Prosseguir com a próxima tarefa do ROADMAP: P1-1 e P1-2 (inventários somente leitura de Lunet2D e Urbe, com o mapa funcional). **Não** autoriza importar, migrar, refatorar nem extrair nada. | Decisão do proprietário. |

## Efeitos

- DEC-0001…DEC-0004 passam a `decided`, com este arquivo como registro; ADRs 0001–0006 passam a `Aceito`.
- DEC-0007 cria a exigência permanente de superfície de decisões e validações no portal, com objeto (ver ADR-0005, atualização posterior, e [`docs/architecture/portal.md`](../../architecture/portal.md)). O portal continua sendo **projeção somente leitura**; o Hub mantém o papel de control plane completo (MANIFEST §35). Responder a uma decisão continua sendo feito ao agente, que a registra em `decisions.json`.
