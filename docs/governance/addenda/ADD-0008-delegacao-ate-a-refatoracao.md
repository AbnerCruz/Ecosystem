# ADD-0008 — Delegação da execução até a refatoração dos aplicativos

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Contexto da mensagem:** o proprietário acabara de responder DEC-0017 (alternativa A) pelo portal e respondia ao resumo do agente que perguntava pela release do Urbe e pelo início do P1-12.

## Mensagem do proprietário (transcrição integral)

> Pronto, se que manda chefe. Olha o que precisa ser feito e faça até finalmente eu poder refatorar os aplicativos para a nova estrutura

*(O texto foi mantido como enviado; "se que manda" foi lido como "você que manda".)*

## Interpretação registrada pelo agente (para conferência do proprietário)

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "Pronto" | DEC-0017 respondida no portal (alternativa A; Issue #25). | Fato (registro `responses/DEC-0017.md`). |
| "você que manda ... olha o que precisa ser feito e faça" | O agente decide e executa sozinho o **trabalho operacional** já previsto no ROADMAP e nas decisões existentes, sem perguntar a ordem. | Decisão do proprietário (delegação de execução). |
| limite da delegação | A delegação **não** cobre: responder decisões ou validações pendentes em nome do proprietário (MANIFEST §23.2), mudanças relevantes de produto ou de **dados do usuário** (NN-005, NN-011), nem publicar uma release de produto com trabalho ainda não lançado. Esses itens continuam indo ao portal. | Inferência do agente (limites que o MANIFEST impõe a qualquer delegação). |
| "até finalmente eu poder refatorar os aplicativos para a nova estrutura" | O objetivo é **encerrar a Fase 1** (passos 1–9 da sequência obrigatória, `docs/migration/README.md` §7) e deixar pronto o caminho do **passo 10** (extrair/modernizar), que é quando refatorar os produtos para a estrutura de Product Core, Product Shell, Tool, Workspace, Service, Library e Adapter (ADD-0002) passa a ser permitido, cada mudança estrutural com ADR. | Inferência do agente (alvo). |
