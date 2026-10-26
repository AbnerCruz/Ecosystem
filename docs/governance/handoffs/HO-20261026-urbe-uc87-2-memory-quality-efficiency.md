# Handoff — Urbe UC-87.2: qualidade, eficiência e contabilidade da memória

- Data: 2026-10-26
- Product: Urbe C# nativo, sem alteração no JS, Lunet, Hub ou Agent Runtime.
- Issue: UC-87.2 no apps/urbe/docs/csharp/ROADMAP.md.
- PR: https://github.com/AbnerCruz/Ecosystem/pull/798
- Branch: fix/urbe-memory-quality-budget-20261026
- Estado: **verificando**. Não afirmar release nem validação física; merge somente pelos gates governados.

## Normas consultadas

MANIFEST.md (NN-001, NN-003, NN-008, NN-009, NN-010, NN-011, NN-016, NN-017, NN-018, NN-019, NN-020, NN-022); AGENTS.md raiz e apps/urbe/AGENTS.md; ecosystem.json; apps/urbe/docs/csharp/ROADMAP.md e ASSISTANT-MEMORY.md; ADR-0033 e ADR-0039; docs/governance/integration-policy.json; DATA-CATALOG.md. A persistência de memória permanece da versão atual; campos aditivos de consumo são mudança em dados privados, crítica e sujeita à autorização normal.

## Defeitos originais com fixture C#

- Search usava substring para pontuar `rio` em `inventário`: falso positivo.
- PackPrompt ignorava integralmente memória longa por não caber: perda de decisão/fonte.
- SettlePast cortava conteúdo acima de 1200 caracteres aos primeiros 400: restrição de acessibilidade depois do caractere 1000 perdida no refinamento.
- ForPromptAdaptive calculava espaço independentemente do histórico efetivo no RunAsync: dupla alocação de janela; sistema, tools, anexos e saída não eram contabilizados no mesmo ponto.
- Provider usage não conservava cache-read/cache-create/reasoning; resumos não distinguiam status de custo com precisão.

## Correções implementadas

- Busca por termo e limite de palavra, fallback por frase, escopos e validade ainda aplicados por consulta; projeções lexicais fracas por registro e invalidação por texto/tipo/caminhos.
- Pedido curto de continuidade herda só o assunto substantivo recente; mudança explícita de tema usa assunto novo.
- Trechos de documentos longos com ID e fonte e busca paginada posterior; evidencia localizada em resultados anteriores, transcript original intacto.
- Orçamento de requisição em AssistantContextBudget.Build, passando pela camada existente AiAgentRun, com serialização wire OpenAI/Anthropic, limite estimado conservador ou tokenizer opcional, reserva de saída e recusa explícita ao exceder a janela. Sem chamada extra de IA nem processo de manutenção.
- AiUsage recebe campos opcionais de cache/saída de raciocínio e origem do custo; OpenAI/Anthropic parsers normalizam sem contar cached tokens duas vezes; UI distingue custos estimados/desconhecidos/parciais.
- Testes de regressão, isolamento, mudanças de estado, janela conjunta, compatibilidade JSON e medições de processamento local.

## Testes adicionados e medição

- MemoryQualityRegressionTests: três defeitos reproduzíveis antes da correção.
- MemoryQualityExtendedTests: continuidade, mudança de tema, substituição, isolamento, cache invalidado por edição/path/estado, orçamento e dados de uso; benchmark de 500 registros: tempo de primeira pesquisa, 12 consultas repetidas e edição+pesquisa; alocação por thread.
- Suites existentes: AssistantMemoryEngineTests, AssistantContextTests, AssistantAgentOracleTests, AssistantProvidersOracleTests; compilações Core/Client/Android/Windows.
- Primeira CI (run 40336503069): compilação Release Core bem-sucedida, 846 testes Core passaram e 15 falharam; 14 oráculos por campo costSource extra nos registros legados e 1 teste por cast Int64→Int32. Corrigidos em commits subsequentes. Não confundir essa rodada com CI verde final.
- CI após correções: https://github.com/AbnerCruz/Ecosystem/pull/798/checks — conferir resultado final e logs, corrigir falhas antes de promover PR.
- Tabela sem inventar números de desempenho em apps/urbe/docs/csharp/ASSISTANT-MEMORY-QUALITY-20261026.md. Diferenças de prompt/custo no modelo real ainda não medidas.

## O que deliberadamente não foi criado

Nenhuma consolidação automática, "sono", resumo pago por mensagem, novo agente, etapa/estado artificial, embeddings obrigatórios, migração do vault, banco extra, cache em disco ou cota arbitrária de memória. O armazenamento permanece local e a autoridade segue em AssistantStore.

## Limitações e próximos gates

1. Após CI final, comparar métricas de alocação/ticks nos logs do mesmo hardware com baseline instrumentado, sem atribuir economia de dinheiro a otimizações locais. Testes verificam o comportamento mas não o ganho de qualidade de um modelo real.
2. Contagem UTF-8/2 + overhead é **estimativa**; não existe tokenizer exato offline para todos os modelos. Em falha de contexto, a ferramenta deve recusar/mostrar erro. Anexos multimodais podem ter regras internas de tokens diferentes.
3. Validar em modelo real (continuidade, refinamentos longos, restrição, consumo de cache/custo) e aparelho Android (desempenho, uso de RAM, tela de consumo).
4. Integrador automático conforme política; o PR com dados privados aditivos demanda revisão governada. Não marcar [x] no roadmap nem publicar release sem gates.
