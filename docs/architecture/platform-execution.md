# Execução platform-first do Ecosystem (PLAT-001)

> **Escopo:** procedimento de seleção e delegação de trabalho. Não é nova autoridade de estado nem alteração das decisões arquiteturais. Autoridades: MANIFEST.md, AGENTS.md, ROADMAP.md, docs/governance/multi-agent.md, ADRs aceitos, Issues e handoffs. Issue de implementação: #323.

## Problema atacado

Um pedido genérico para continuar o Ecosystem não é licença para selecionar a tarefa local mais fácil no Lunet, Urbe ou outro Product. O monorepo contém Products independentes **e** uma plataforma. A falta de uma seleção explícita de escopo permite que a plataforma permaneça sem avanço apesar de muitos PRs de apps.

A política não pausa Products nem transforma o integrador automático em orquestrador de agentes. **Ela vale para a seleção de tarefas novas**; execução paralela só acontece quando uma sessão/agente foi de fato acionado.

## Algoritmo de seleção

1. **Escopo primeiro.** Product nomeado ou Issue explicitamente atribuída: siga aquele Product. Pedido genérico "continue o Ecosystem": escolha no roadmap **global** e nas Issues da plataforma.
2. **Releia estado vivo.** Fonte de fase/gate: ROADMAP.md; progresso do trabalho: Issues/PRs e handoffs; componentes: ecosystem.json; decisão pendente: portal derivado de decisions.json. Não deduza status do README, de chats anteriores ou da quantidade de commits em apps/*.
3. **Elimine duplicações e bloqueios.** Verifique se outra branch/PR já entrega o item. Bloqueado por validação humana ou decisão? Registre esse bloqueador e faça outra ação independente do núcleo. Não crie trabalho de Product para disfarçar espera.
4. **Priorize a menor entrega verificável que retire um bloqueio global.** O agente registra tarefa/Issue, escopo, dependências, branch, PR, handoff e testes. A prioridade nunca autoriza alterar boundary, permissão, roadmap de Product ou critério de aceite sem ADR/decisão.
5. **Revise reuso sem extração automática.** Funcionalidade que nasce em Product permanece local por padrão; registre reuse_assessment. Segundo consumidor real e ganho de complexidade demonstrado: Extraction Review, contrato e teste (NN-020/022).
6. **Trabalho simultâneo condicional.** Com dois ou mais agentes de fato disponíveis, a delegação deve reservar uma frente independente de plataforma quando houver item global acionável. Não interrompa tarefa já designada a um agente de Product, não simule agente em execução e não crie porcentagem arbitrária de PRs.
7. **Finalize com evidência.** Checks no estado combinado, handoff e PR via integrador; crítico aguarda autorização normal do proprietário. CI verde não substitui validação em aparelho.

## Trilha concreta de recuperação

**Distribuição / Fase 4:** o encerramento P4-9 com aprovação DEVICE para Urbe 1.8.4-beta tramita no PR #320. Não duplicar, marcar gate aprovado manualmente ou aproveitar aprovação de outros builds. Resolver falhas reais desse PR, se houver, e permitir a integração conforme a política crítica.

**Agent Workspace / Fase 6:** a Issue #144 continua sendo a autoridade do trabalho P6-4. O Host de teste R3 foi provado, mas **não** satisfaz a hospedagem real. A Host API da Fase 5 está pronta; o bloqueador descrito em ADR-0017 D7 e no plano agent-runtime §11.1 é o **marco autorizado do anfitrião** (Urbe UC-21 ou Lunet Fase 8).

Auditoria inicial da elegibilidade já registrada em [p6-4-host-eligibility.md](p6-4-host-eligibility.md). Próxima entrega global segura: aprofundar e testar **matriz de elegibilidade do Host real** (capabilities existentes, Context/grants, superfícies de sessão, testes de consumer real, lifecycle/isolamento, o que pode ser implementado sem antecipar IA do Product) comparando Hub, Urbe C# e Lunet, sem ligar UI nem modelo real. Hub pode ser examinado como Host técnico, mas **não** ser transformado em Agent Runtime nem substituir o gate que exige contexto de Urbe/Lunet (ADR-0017 D3). A matriz deve concluir: (a) integração já autorizada, com evidências; ou (b) mudança de sequencing necessária, com ADR e decisão do proprietário antes de qualquer código que altere o marco. Não inferir que esta diretiva de priorização aprovou a alternativa (b).

Enquanto isso, trabalho independente de plataforma é permitido: auditabilidade do Runtime/Workspace, conformance de contratos, regressões do integrador, preparativos de capacidades conforme o roadmap global — sempre com tarefa/Issue própria. **P6-5 não é concluído antes de P6-4.**

**Primeira Tool real / Fase 7:** realizar auditoria de um candidato reutilizável com dois consumidores **demonstráveis**, custos de duplicação e interfaces publicáveis. A auditoria pode começar sem código de extração; a extração só avança depois da decisão e dos testes exigidos pela NN-022. Não contar o experimento text.inspect da Fase 5 como prova automática de qualquer Tool nova.

## Critérios de aceite de roteamento

| Entrada | Ação correta | Desvio a rejeitar |
| --- | --- | --- |
| "Continue o Ecosystem" | Escolher Issue acionável do roadmap global e declarar etapa | Implementar feature isolada de Product sem ligação com gate |
| "Continue o Urbe" | Selecionar item do roadmap Urbe e respeitar seu gate | Abandonar a tarefa para avançar outra fase global |
| "Continue o Ecosystem" com P6-4 waiting | Expor #144 e atuar em desbloqueio legítimo ou tarefa independente | Marcar R3 concluído por Host simulado; fabricar integração |
| Ideia de Tool comum | Auditar consumidores, avaliar reuso, registrar handoff/ADR | Extrair só pela semelhança de interfaces |
| PR #320 já aberto | Acompanhar e reconciliar caso necessário | Criar segundo PR concorrente para o mesmo gate |

## Exclusões

Sem mudança de MANIFEST, ADR-0017, critérios de gate, permissões, estratégia de distribuição, versionamento dos Products ou mecanismo de integração. Nenhuma reserva de agente, scheduler, background job ou execução fora de sessões reais é criada. Mudanças em roadmap/código de Host real requerem tarefa e decisão/autorizações adequadas.
