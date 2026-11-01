# Planejamento adaptativo em camadas — beta.40

## Decisão técnica

Esta é uma evolução incremental da ferramenta nativa `update_plan` do Urbe C#, **não** um executor autônomo de DAG ou segundo runtime. O contrato simples anterior continua aceito. O modo estruturado é acionado pelo campo `goal`, proporcional à tarefa.

Confronto de referências: `AssistantAdaptivePlanTools` (estado atual), `AgentRuntime.Core/Tasks.cs` (teste e verificação independente), `TeamPlan.cs` (dependências acíclicas), Microsoft AutoGen Magentic-One (task/progress ledgers e replanejamento), GitHub Spec Kit (especificação/plano/tarefas e convergência) e LangGraph (checkpoints e reexecução). O contrato atual do host `IAssistantHost.SetPlan` permanece único; não há duplicação de dados do vault nem escrita em notas pelo planejador.

## Fluxo

1. **Diagnóstico:** descrever `goal` (pedido original), `current_state` (estado observado) e `target_state` (resultado testável). Investigar o suficiente e confrontar a estratégia com restrições; pedir aprovação só para decisões realmente críticas.
2. **Grafo:** decompor em `steps` com `id` estável, `step`, `status`, `acceptance`, `tests`, `depends_on` e detalhes opcionais. Não há limite arbitrário de profundidade. A ferramenta rejeita ID duplicado, dependência ausente e ciclo.
3. **Ciclo de cada nó:** antes de iniciar exigir `acceptance` e `tests`; ao concluir, registrar `test_evidence`, `implementation_evidence` e `verification_evidence`. Nenhum nó ativo ou concluído pode depender de outro ainda aberto. Independentes podem avançar em paralelo.
4. **Convergência:** após todos os nós concluídos, confrontar o resultado integrado com o plano (`final_plan_review`) e com o pedido original (`final_user_review`). Confrontos finais prematuros são recusados.
5. **Entrega:** distinguir claramente relatos de evidências produzidas pelas ferramentas, testes CI e validação humana; resultado completo no diálogo apenas quando houver provas pertinentes.

## Fronteira de confiança

Os campos de evidência de `update_plan` são **declarações textuais produzidas pelo modelo**. Eles validam completude estrutural do plano, e **não** autenticam testes, builds, artefatos, documentos, hashes, resultados de ferramentas, revisão humana ou validação no Android. O agente deve buscar verificações independentes pelas capacidades reais do Host e relatar qualquer limite. O resumo da ferramenta explicita isso; campos como `test_evidence` não viram recibos de execução.

Não foram adicionados estados canônicos alternativos à memória do agente, ao vault, ao `AgentRuntime.Core` ou ao workspace; a UI existente recebe `AiPlanStep` normalizados. O grafo estruturado ainda não tem armazenamento próprio durável nem executor de dependências: durante a execução o modelo deve manter e atualizar seus dados. Não confundir com a arquitetura futura proposta em `docs/architecture/agent-runtime.md`.

## TDD e regressões

`AssistantLayeredPlanTests` foi criado antes do código. Cobre compatibilidade antiga, visualização de baseline/target/dependências, IDs duplicados, referências inexistentes, ciclos, bloqueio de dependências, testes definidos antes da execução, exigência das três evidências e confronto global. Os checks reais vêm do workflow `urbe-checks`; sua execução e resultados devem ser registrados no PR e handoff, sem autoatribuição.

## Pendências para avaliação posterior

Verificação independente end-to-end por domínio, provas com IDs imutáveis de ferramentas, invalidar evidências quando arquivos mudam, checkpoints persistidos versionados e benchmark A/B contra o modo simples são extensões de arquitetura a estudar e aprovar separadamente. Sua ausência não deve ser mascarada pela UI. A solução evita expansão automática do escopo e não substitui CST-078, 079, 213, 217, 218 e 225 da Consolidação 2.5.
