# Urbe — Correções de qualidade e eficiência da memória (UC-87.2, 26/10/2026)

Status: implementação em PR #798, ainda sujeita a CI/revisão e validação com modelo real/Android. Não há novos ciclos de IA, consolidação em segundo plano, “sono” nem renovação de contexto por etapas. Não se altera o armazenamento das memórias.

## Defeitos reproduzíveis no C# anterior

1. **Busca por substring:** `AssistantMemoryEngine.Search` pontuava `text.Contains(term)` e `text.Contains(q)`, atribuindo correspondência à busca `rio` sobre `inventário`. Fixture: `MemoryQualityRegressionTests.ExactLexicalMatching_RioMustNotMatchInventario`.
2. **Memória longa omitida:** `PackPrompt` usava `if (formatted.Length > maxChars - used) continue`; uma decisão longa e relevante sumia sem ID/fonte. Fixture: memória de mais de 5000 caracteres, orçamento de 360 caracteres.
3. **Refinamento perdendo restrição:** `SettlePast/Settled` mantinha apenas os primeiros 400 caracteres do resultado da ferramenta anterior. Fixture: restrição de acessibilidade após o caractere 1000, novo pedido para preservá-la.
4. **Orçamento dividido:** `AssistantPanel.MemoryList` reservava espaço por conta própria com uma estimativa do histórico recente; `AssistantAgent.RunAsync` preparava outro histórico com teto separado, sem contabilizar esquemas de ferramentas e anexos junto do sistema e geração. A soma podia extrapolar a janela.
5. **Contabilidade parcial:** `AiUsage` representava apenas input/output/cost. O leitor de eventos Anthropic somava buckets cache-read e cache-create ao total sem preservá-los como categorias. O leitor OpenAI ignorava `prompt_tokens_details.cached_tokens`. Ausência de preço não distinguia o estágio conhecido/estimado.

As fixtures e casos em C# estão em `MemoryQualityRegressionTests`, `MemoryQualityExtendedTests` e nas suites de oráculos do Urbe. Os experimentos Python anteriores foram pistas, não medições de produção.

## Implementação

- Busca de palavra e frase com limites lexicais (não substring); projeção de normalização/termos com `ConditionalWeakTable<AiMemoryRecord,...>`: uma entrada mutável por registro vivo, validada por texto/tipo/caminhos atuais. Estados, confiança, temporalidade e escopo continuam aplicados dinamicamente por consulta; nenhum cache persistente.
- `ContinuityQuery`: para pedidos curtos padronizados, busca o último pedido substantivo recente; pedidos explícitos novos usam somente seu próprio tema. Não usa modelo, resumo, scheduler nem vetor externo.
- `PackPrompt`: quando uma memória relevante é longa demais para entrar inteira, preserva prefixo com tipo/fonte/ID, trecho compatível e aviso explícito para buscar o original pela ferramenta `recall_memory`. Não descarta conhecimento armazenado. Memórias fixadas continuam evidências, não instruções com prioridade sobre o usuário.
- `EvidenceExcerpt`: em refinamentos, mantém cabeçalho e pequeno trecho alinhado ao texto solicitado, ainda que depois dos primeiros 400 caracteres; identifica a chamada de ferramenta. O transcript integral nunca é sobrescrito. Fontes reais devem ser relidas quando a versão atual for necessária; o excerto não se apresenta como prova da versão atual.
- `AssistantContextBudget.Build`: somente no fluxo otimizado do cliente nativo, calcula requisição inteira (sistema, papel, ferramentas, histórico, argumentos/resultados, anexos, memória e reserva de saída). Consulta a memória *depois* de preparar o histórico. Constrói wire JSON OpenAI/Anthropic com os serializadores existentes, ou estimativa genérica para local. A conta conservadora é **estimada** por bytes UTF-8/2 + overhead de mensagens; existe ponto de extensão `CountInputTokens` para tokenizer do provedor. Se um payload não cabe, compacta a projeção sem alterar o histórico; se continua impossível, apresenta erro em vez de enviar fora da janela.
- O payload limitado preserva o formato dos registros originais. Descrições de ferramentas e mensagens do sistema não são removidas. `Prepare` mantém pareamento tool_use/tool_result, contexto recente e referências de evidência.
- `AiUsage` registra categorias opcionais `cacheReadInput`, `cacheCreationInput`, `reasoningOutput`, `costSource` com compatibilidade de leitura com JSON antigo. Cache tokens são subconjuntos de entrada, não somados de novo. Custos podem ser informados, estimados (tarifa simples sem cache), parciais ou desconhecidos. Dados de credenciais e conteúdo da conversa não entram em medições de performance.

## Matriz comparativa e critério de evidência

| Caso sintético | Antes (código original) | Depois (critério de teste) |
| --- | --- | --- |
| Busca “rio” em “inventário” | Correspondência falsa por substring | Sem correspondência |
| Memória relevante de 5000+ caracteres | Ignorada quando ultrapassa orçamento | Trecho e ID/fonte recuperáveis |
| Refinar com restrição no caractere 1000+ | Restrição ausente do resumo dos primeiros 400 | Trecho recuperado, original intacto |
| “Pode seguir” depois de formulário | Consulta sem tema específico | Último pedido substantivo, escopo mantido |
| Troca explícita para geografia | Pode reter tema anterior em memória fixada | Consulta temática do novo pedido |
| Decisão supersedida | Estado deve ser filtrado | Antiga não recuperada; nova recuperada |
| Mudança de texto/caminho/estado do registro | Trabalho lexical repetitivo | Projeção invalidada e ranking atualizado |
| Requisição próxima do limite | Duas estimativas independentes | Soma do payload + reserva respeita limite estimado |
| Cache e custo desconhecido | Categorias indisponíveis | Categorias preservadas; parcial/unknown sem “grátis” |

**Medição de processamento local:** fixture de 500 registros mede primeira construção, 12 consultas repetidas e edição/consulta, com `Stopwatch.ElapsedTicks` e `GC.GetAllocatedBytesForCurrentThread()`; números concretos devem ser coletados dos logs da execução. Teste de equivalência de resultado considera a mudança intencional de match por palavra; não converter ticks, bytes ou caracteres em economia financeira. Nenhuma melhoria real da resposta ou custo de API é afirmada sem teste com modelos e dados de faturamento.

## Compatibilidade, segurança e riscos

- O provider pode não oferecer tokenizer local exato; o limite conservador serve de guarda prática, **não** garantia matemática para todos os tokenizadores multimodais. Falhas de estimativa devem resultar em erro de contexto, não perda de dados.
- Valor de cache da Anthropic pode ser categoria disjunta dentro do total; OpenAI informa `prompt_tokens` total com cached subset. Não somar duas vezes.
- Tokens/custos registrados são os efetivamente fornecidos pelos provedores, sem inventar tarifa de leitura de cache, preço de imagem nem atribuir custo zero a dados ausentes.
- Persistência das memórias permanece inalterada; uso da conversa recebe apenas campos opcionais. A alteração de contrato aditivo deve respeitar gate de dados da integração, testes de roundtrip e backup existentes.
- Sem testes com provedor real/Android não se afirma melhoria de qualidade final nem estabilidade física. Release não é disparada por este PR.

## Verificações e pendências

Testes Core: `MemoryQualityRegressionTests`, `MemoryQualityExtendedTests`, `AssistantContextTests`, `AssistantMemoryEngineTests`, oráculos `AssistantAgentOracleTests` e `AssistantProvidersOracleTests`. Testes de UI e consistency em GitHub Actions da PR. Verificar compilação Android e Windows. Em aparelho: context window pequena, chats longos, cache real OpenAI/Anthropic, tela de custo, retorno de anexos e tempo/memória nos modelos locais. Sem execução com modelo real, benchmarks de qualidade (LongMemEval/FrontierMemory) continuam pendentes.
