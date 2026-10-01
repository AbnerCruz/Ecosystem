# ADD-0010 — Concorrência multiagente segura

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Contexto da mensagem:** dois agentes trabalharam ao mesmo tempo, a partir do mesmo HEAD da `main` (`a4ad875`), em tarefas diferentes — Fase 3 (branch `ccr-31501aad-6bwq7y`) e Urbe U-R1/RM-F2-03 (branch `ccr-5adec8cc-oy464b`) — como teste proposital. O texto integral é longo; abaixo estão **transcritas as passagens que decidem** e o que o agente interpretou delas. Registros: [ADR-0014](../../adr/0014-fluxo-multiagente-minimo.md) e [`multi-agent.md`](../multi-agent.md).

## Passagens decisórias (transcrição)

> «O objetivo não é impedir agentes de trabalharem ao mesmo tempo. O objetivo é permitir concorrência segura. A regra deve ser: PARALELIZAR O QUE É INDEPENDENTE · COORDENAR O QUE SE SOBREPÕE · REVALIDAR TUDO NA INTEGRAÇÃO.»

> «o mundo pode ter mudado enquanto um agente trabalhava. Portanto nenhum agente pode integrar trabalho importante presumindo que o estado lido no início ainda é o estado atual.»

> «antes de integrar, o agente deve verificar se o HEAD/base usado para iniciar o trabalho continua ancestral da "main" atual e se surgiram mudanças relevantes desde então. Se a base estiver velha: não integrar diretamente. Primeiro: fetch/re-read main · reconcile · rerun checks. Formalize isso no lugar apropriado. [...] Escolha o mecanismo mínimo e confiável. Não crie uma plataforma de locking distribuído para isso.»

> «Não resolva concorrência assim: "apenas um agente pode trabalhar por vez" [...] Também não crie lock global no repositório. O modelo desejado é: trabalhos independentes → paralelo; trabalhos conflitantes → isolamento + reconciliação explícita.»

> «dado um handoff, deve ser possível descobrir de forma confiável de qual estado o agente partiu e qual alteração representa seu resultado. Não obrigue um arquivo dentro de um commit a conhecer o próprio hash se isso cria autorreferência impossível. Prefira derivação estruturada.»

> «O modelo multiagente deve conseguir diferenciar: BASE · WORK RESULT · INTEGRATION · VALIDATION.»

> «É melhor: result_commit = derivado / desconhecido até integração do que: commit = hash incorreto.»

> «Um agente pode terminar sua execução sem a tarefa estar integrada. [...] agent finished ≠ task completed ≠ changes integrated.» — «Não crie uma segunda status machine se Issues/handoffs já resolverem isso.»

> «mudanças de Product devem abrir PR para disparar o CI completo específico do Product. Documente essa expectativa. Não altere workflows sem necessidade se PR já resolve o problema.»

> «Se: Agente A altera ROADMAP.md · Agente B altera ROADMAP.md — isso NÃO significa automaticamente que um deles está errado. Mas integração deve exigir: merge/rebase explícito → resolver semanticamente → não simplesmente "ours/theirs" → rerodar checks.»

> «O Urbe possui regra explícita: "PR não deve ser mergeado sem pedido explícito do proprietário." Respeite todas as regras atuais de merge do Ecosystem e dos Products. [...] Não interprete este prompt como autorização para ignorar uma regra normativa existente.»

> «Quando múltiplos agentes receberem tarefas, o sistema futuramente poderá comparar: scope esperado · paths prováveis · componentes · arquivos compartilhados e sinalizar: LOW OVERLAP → pode paralelizar ou HIGH OVERLAP → coordenar. Isso é futuro. Não construa um orquestrador complexo nesta tarefa.»

## Interpretação registrada pelo agente

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| paralelizar / coordenar / revalidar | Sem lock. Cada agente em branch própria; a integração é o único ponto de coordenação obrigatório. | Decisão do proprietário. |
| "escolha o mecanismo mínimo" | O agente escolhe o mecanismo (delegado): `--integration` no check de consistência, `base_commit` no handoff, PR como ponto de integração. Detalhes no ADR-0014. | Delegação do proprietário; mecanismo = proposta do agente aceita por delegação. |
| regra de merge do Urbe | PRs que tocam `apps/urbe` não são integrados sem pedido explícito do proprietário (`apps/urbe/AGENTS.md`). Um PR só do Ecosystem segue o fluxo branch → PR → CI → merge do MANIFEST §27, sem regra de aprovação própria. | Fato (regras vigentes). |
| "agent finished ≠ integrated" | Reusar a status machine existente (`review` ≠ `done`); `done` só depois de integrado e validado. Sem estados novos. | Inferência do agente, conforme a instrução de não duplicar sistemas. |
