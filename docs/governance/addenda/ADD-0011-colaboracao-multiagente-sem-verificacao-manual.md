# ADD-0011 — Merge do trabalho multiagente e colaboração sem verificação manual

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Contexto da mensagem:** resposta ao relatório da integração multiagente (ADR-0014): PRs #33, #34 e #36 integrados; PR #35 (Urbe RM-F2-03) pronto, aguardando pedido explícito do proprietário (`apps/urbe/AGENTS.md`); DEC-0022 já respondida pelo proprietário no portal (A).

## Mensagem do proprietário (transcrição integral)

> Pode fazer o merge e aprovar tudo. Mas eu precisar pegar um agente (no caso você) para verificar esse trabalho multiagente é o insustentável, o sistema deve permitir essa colaboração sem erros

## Correção do proprietário (transcrição integral)

Enviada 21 segundos depois da mensagem acima (18:55:21 → 18:55:42 UTC), como correção dela. O agente só a recebeu ao terminar o trabalho em andamento, às 19:31 UTC — depois de DEC-0023 já estar no portal.

> Automaticamente*

## Interpretação registrada pelo agente (para conferência do proprietário)

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "Pode fazer o merge" | Pedido explícito de merge do PR #35 (Urbe RM-F2-03), exigido por `apps/urbe/AGENTS.md`. Executado depois de reconciliar com a `main` atual e com o CI do estado combinado verde. | Decisão do proprietário. |
| "aprovar tudo" | Aprova o trabalho multiagente já integrado (PRs #33, #34, #36) e o PR #35. No momento não havia outra pendência: DEC-0022 já tinha sido respondida pelo proprietário no portal (A) e não havia validação humana pendente. | Decisão do proprietário (o escopo é inferência do agente). |
| "precisar pegar um agente para verificar esse trabalho multiagente é insustentável; o sistema deve permitir essa colaboração sem erros" | Requisito: a verificação e a reconciliação do trabalho concorrente (base obsoleta, estado combinado, CI dos Products, handoff) devem ser **automáticas**, sem que o proprietário precise acionar um agente. | Decisão do proprietário (requisito). |
| "Automaticamente*" | Correção de uma palavra da mensagem anterior (o asterisco marca correção); reforça o requisito: a colaboração deve acontecer **automaticamente**. Como foi escrita antes de a pergunta de DEC-0023 existir, **não** foi tratada como resposta a ela. Implementado já (ADR-0015): toda a verificação e a devolução ao autor são automáticas; integrar sem nenhum toque depende de DEC-0023. | Decisão do proprietário (requisito); o alcance é inferência do agente. |
| até onde vai a automação | Se o próprio sistema também faz o **merge** na `main` sem revisão humana por PR, ou se só verifica e deixa o merge como um clique do proprietário, é uma escolha com consequências reais (merge na `main` publica o Urbe Web e as releases de desenvolvimento do Lunet2D depois da sincronização). Levada ao portal como **DEC-0023**. | Inferência do agente; decisão pendente. |
