# Urbe UC-87.3 — Aprendizado durável e recuperação lexical ampliada (27/10/2026)

**Estado:** correção em branch; CI, integração e validação Android ainda devem ser confirmados. Não confundir teste unitário com qualidade demonstrada de memória de longo prazo.

## Defeitos concretos no código anterior

- Um agente que apenas recebia "Prefiro respostas curtas" ou "Meu objetivo é concluir X" não criava memória durável, exceto se o modelo invocasse por conta própria uma ferramenta de gravação. "Lembre que" era capturado, mas a maioria das preferências e decisões declaradas passava despercebida.
- O modelo local tinha `recall_memory`, mas não tinha `learn_memory` no conjunto permitido, impedindo aprendizado estruturado por ferramenta em execuções locais.
- A consulta "nome do meu pet" podia não recuperar "Meu cachorro se chama Rex", por busca lexical literal.
- As ferramentas `remember` e `learn_memory` eram classificadas como `memory` de leitura, mesmo modificando a configuração persistente. A verificação de permissões ignorava essa escrita.

## Mudanças (sem novo armazenamento nem chamadas pagas)

1. `AssistantMemoryEngine.CaptureUserStatements` grava somente declarações afirmativas explícitas em primeira pessoa sobre preferências, objetivos e decisões, após conclusão bem-sucedida. O reconhecimento é deliberadamente conservador e ignora perguntas, negativas, blocos de código, trechos longos e possíveis segredos. Escopo `agent` no vault atual; fonte `conversation:<id>`; registros JSON v2 existentes, sem migração. A declaração explícita "lembre que" continua compartilhada no vault pelo fluxo preexistente. Sem extração da conversa completa, sem inferência de personalidade e sem consolidação automática.
2. A busca permanece lexical e offline, mas amplia um conjunto pequeno, auditável e testado de equivalências de alta precisão: pet/cachorro/cão, meta/objetivo, preferência(s) e carro/automóvel. Correspondência literal tem peso maior; termos genéricos de perguntas deixam de pontuar. **Isso não é embedding, recuperação semântica geral ou entendimento de paráfrases arbitrárias.** Preferências fixadas, scoping e exclusão de segredos permanecem.
3. Modelos executados no dispositivo passam a receber `learn_memory` quando suas permissões permitirem. No cliente C# nativo, a ferramenta legada `remember` deixa de ser oferecida ao modelo (mas seu contrato histórico permanece intacto nos oráculos de paridade); `learn_memory` recebe acesso `memory-write`, vetado em agentes `read` e política `readonly`, e solicita aprovação em política `ask`. O reconhecimento explícito de «lembre que» continua funcionando pelo fluxo automático existente. `recall_memory` continua apenas leitura.
4. Escritas novas continuam em `AssistantStore.SetConfigVerified`, com releitura byte-a-byte e sem alterar dados do vault.

## Pesquisa e decisões de engenharia

- **LongMemEval (ICLR 2025):** https://github.com/xiaowu0162/LongMemEval — avaliar extração, consultas entre sessões, conhecimento atualizado, dimensão temporal e capacidade de dizer "não sei"; separar indexação, recuperação e leitura.
- **Generative Agents (UIST 2023):** https://doi.org/10.1145/3586183.3606763 — recuperação dinâmica não é sinônimo de guardar tudo no prompt; importância e relevância precisam ser distinguidas.
- **Mem0 (código/documentação e comunidade):** https://github.com/mem0ai/mem0/blob/main/docs/core-concepts/memory-operations/update.mdx e https://github.com/mem0ai/mem0/issues/6515 — proveniência, IDs e atualização/deduplicação sob concorrência importam; não fazer atualizações destrutivas por correspondência superficial.
- **Letta (2026):** https://www.letta.com/blog/context-repositories/ — memória versionada e isolamento de escritas concorrentes são alternativas de longo prazo, não motivo para importar um segundo runtime e introduzir dependência para o Urbe.

## Verificações

- Testes **escritos antes das correções**, nos commits de regressão: `AssistantMemoryContinuityTests` e `AssistantDeviceMemoryToolsTests`.
- Cobrem preferências e objetivos entre sessões com roundtrip JSON, isolamento agente/vault, idempotência, recusa de extração ambígua ou sensível, sinônimo pet/cachorro, controles read-only e disponibilidade local.
- Verificar também `AssistantMemoryEngineTests`, `MemoryQualityRegressionTests`, `MemoryQualityExtendedTests`, `AssistantAgentOracleTests`, `Urbe.Client.Tests`, `npm run check` e consistency no CI.
- **Sem SDK .NET disponível no executor de edição:** evidência de compilação/testes depende dos jobs de GitHub Actions; não declarar verde antes dos logs.

## Concorrência, privacidade e rollout

- PRs #825 e #827 modificam a interface e o fluxo de anexos do Assistente. Esta correção limita as mudanças compartilhadas a duas linhas/pontos específicos em `AssistantPanel.cs`; não modifica as novas classes de extração nem sobrescreve branches alheias.
- Identidade dos vaults ainda baseada no nome (problema arquitetural anterior). Uma migração para ID persistente precisa de decisão e provas de não perda, fora deste patch.
- Ainda faltam telemetria acessível ao usuário de motivos de recuperação e avaliação comparativa com LongMemEval/LoCoMo; não declarar excelência sem medir.
- Reuse assessment (NN-022): mecanismo permanece local ao Urbe.Core, sem contrato duplicado em `platform/`.
- Teste no aparelho: memorizar preferência na conversa A, encerrar/reabrir, responder em conversa B com outro vocabulário; conferir mesma versão instalada; validar permissão e aprovações nos agentes readonly/ask/auto.
