# ADR-0014 — Fluxo multiagente mínimo: base, resultado, integração e validação

## Status

Aceito — decisão do proprietário registrada em [ADD-0010](../governance/addenda/ADD-0010-concorrencia-multiagente.md) (2026-10-01): concorrência segura, sem lock global, com revalidação na integração. O **mecanismo** foi delegado ao agente e é o descrito abaixo. Guia operacional: [`docs/governance/multi-agent.md`](../governance/multi-agent.md). Não cria invariante nova nem altera o MANIFEST; aplica NN-001, NN-008, NN-011, NN-017 e NN-018 ao trabalho paralelo.

## Contexto

Dois agentes partiram do mesmo HEAD (`a4ad875`) e trabalharam em paralelo (Fase 3 no Ecosystem; RM-F2-03 no Urbe). Os trabalhos não se sobrepunham e os checks de cada branch passaram, mas o teste expôs lacunas: (1) nada obrigava a conferir, antes de integrar, se a `main` tinha mudado desde o início do trabalho; (2) o handoff do Urbe gravou como `commit` o commit de **partida** e o da Fase 3 gravou `null`, porque um arquivo não pode conhecer o hash do commit que o introduz; (3) "o agente terminou" se confundia com "a tarefa foi integrada"; (4) o CI do Urbe (`urbe-checks`) só roda em PR ou na `main`, então uma branch sem PR nunca o executou.

## Problema

Qual é o mínimo de processo e de estrutura que torna verificável, para trabalho concorrente: de onde cada agente partiu, qual é o seu resultado, se a base ficou obsoleta, se houve sobreposição e qual estado foi efetivamente testado e integrado — sem lock global e sem orquestrador?

## Opções

1. **Fluxo mínimo verificável:** PR como ponto de integração; `base_commit` no handoff; resultado e integração derivados de `branch`/`pr`; verificação de base obsoleta e sobreposição por `git` (`Check.cs -- --integration`); reconciliação explícita e checks no estado combinado.
2. **Lock/serialização:** um agente por vez, ou reserva de arquivos.
3. **Somente documentação**, sem campos nem verificação.

## Decisão

Opção 1.

- **Quatro identidades** (todas verificáveis, nenhuma autorreferente):
  - **BASE** — `base_commit` no handoff: SHA de onde o agente partiu. Obrigatório para handoffs a partir de 2026-10-01T19:00Z (`CHK-HANDOFFS`; a data original, 2026-10-02, estava errada — o dia real era 2026-10-01 — e foi corrigida com o ADR-0015); deve existir no histórico.
  - **WORK RESULT** — o commit da `branch` que introduz/atualiza o handoff (derivado). O campo `commit` só é preenchido quando o resultado já é conhecido ao gravar (ex.: numa reconciliação posterior); **nunca** é o commit de partida — `CHK-HANDOFFS` recusa `commit` igual a `base_commit`, inexistente ou que não descenda da base.
  - **INTEGRATION** — o merge do `pr` na `main` (derivado do PR; não gravado no handoff, que não pode conhecê-lo).
  - **VALIDATION** — `verification[].tested_commit` (opcional): o estado efetivamente testado (ex.: o estado combinado depois da reconciliação).
- **PR é o ponto de integração** para trabalho de agentes em branches: branch → commits → handoff → PR → CI → revisão/validação → merge. Mudança em `apps/<id>` sempre abre PR, para disparar o CI do Product (ex.: `urbe-checks`, que roda em `pull_request`); nenhum workflow foi alterado. Regras de merge de cada Product continuam valendo (o Urbe exige pedido explícito do proprietário).
- **Pré-integração obrigatória:** `dotnet run tests/consistency/Check.cs -- --integration <branch>` compara a branch com `origin/main`: FRESH (0), STALE (3: a `main` andou, reconciliar antes) ou STALE com sobreposição (4: os mesmos arquivos mudaram nos dois lados; arquivos de alto risco destacados). Com STALE, o agente faz merge da `main` na branch (sem reescrever histórico), resolve conflitos **semanticamente** e reroda os checks (Ecosystem e Products tocados) no estado combinado.
- **Estados sem máquina nova:** reusa a status machine dos handoffs. `review`/`verifying` = agente terminou, aguardando revisão, validação ou integração; `done` só quando o resultado está **integrado na `main`** e as verificações passaram. "Agente terminou" ≠ "tarefa concluída" ≠ "integrado".
- **Sem lock.** Trabalhos independentes correm em paralelo; sobreposição é resolvida na integração, com registro no handoff (conflito, causa, resolução, evidência).

## Consequências

- Um handoff novo diz de onde partiu e não pode afirmar um resultado falso; o resto é derivado de `branch`/`pr` e do git.
- A segunda branch a integrar sempre passa por reconciliação explícita e CI do estado combinado.
- O teste de concorrência fica reprodutível pelo self-test (`IntegrationTests`: mesmo HEAD, primeira integração, base obsoleta, sobreposição de alto risco, reconciliação preservando os dois trabalhos).
- Custo: um comando a mais antes de integrar e um campo a mais no handoff.
- Verificação e integração automáticas pedidas pelo proprietário (ADD-0011): [ADR-0015](0015-integrador-automatico.md) (integrador); integrar sem nenhum toque do proprietário depende de DEC-0023.
- Futuro (não implementado): comparar escopo esperado, paths prováveis e componentes de tarefas atribuídas ao mesmo tempo e sinalizar LOW/HIGH OVERLAP antes de começar.

## Alternativas rejeitadas

- **Lock/serialização (opção 2):** destrói o objetivo (paralelismo) e cria um ponto central sem necessidade (ADD-0010).
- **Somente documentação (opção 3):** repetiria a lacuna observada (handoff com commit errado passou nos checks).
- **Exigir o hash do resultado dentro do próprio commit:** autorreferência impossível.

## Referências

MANIFEST §23, §27; NN-001, NN-008, NN-011, NN-013, NN-017, NN-018; ADD-0010; ADR-0009, ADR-0011; [`multi-agent.md`](../governance/multi-agent.md); `apps/urbe/AGENTS.md`.
