# Falhas repetidas de ferramentas — AI-603

O `AgentRunner` incorpora a proteção funcional observada em `AssistantAgent.RunAsync`
do assistente nativo de referência: uma execução de ferramenta que falhou não é
repetida com argumentos idênticos no mesmo run. O modelo recebe `ToolResultBlock`
de erro com instrução de corrigir os argumentos ou mudar de estratégia.

- A identidade combina capability, versão e JSON dos argumentos. A ordem das
  propriedades, espaços e escapes de strings não contornam a proteção; a ordem de
  arrays, os tipos, maiúsculas/minúsculas e representações numéricas são preservados.
- Somente falhas efetivas (`ToolOutcome.IsError` ou exceção de ferramenta) entram
  na memória de falhas. Negação de permissão/aprovação e execução interrompida com
  efeito desconhecido continuam seguindo suas políticas próprias.
- Leituras bem-sucedidas não apagam falhas anteriores. Escrita/destruição
  bem-sucedida invalida a memória, pois pode ter corrigido o estado observado.
- Depois de `RunPolicy.NoProgressRepeats` supressões, o run bloqueia por
  `NoProgress`, inclusive se o modelo intercala ferramentas ou repete em lote.
  Isso limita execução improdutiva, não substitui orçamento ou verificação.
- A memória e a contagem são projeções do log existente, reconstruídas na retomada.
  Não existe armazenamento paralelo, loop novo, nova permissão ou retry automático.
- Metadados opcionais de `RuntimeEvent.Data`: `failedInvocationSha256` (digest da
  chamada que falhou), `repeatedFailedInvocation` e `invalidatesFailedInvocations`.
  Nenhum argumento em claro é acrescentado nesses campos. O digest não é
  criptografia de segredos; logs mantêm sua política de privacidade existente.
- Logs antigos continuam legíveis/retomáveis; falhas antigas sem esse metadado não
  são inferidas retrospectivamente. Novo run não herda falhas de outro run.

**Limites:** falha de leitura transitória sem mutação observada exige novos
argumentos ou novo run; alterações externas ao Host não são detectadas. A proteção
não garante idempotência, rollback ou ausência de cobrança de chamadas de modelo
já feitas. As garantias anteriores de retomada continuam em vigor.

A única dependência nova do Core é SHA-256 da biblioteca-base, para identificar a
chamada sem duplicar argumentos privados nos metadados. A allowlist de arquitetura
permite esse assembly; rede, arquivos, processos, SDKs e dependências de Products
continuam proibidos.

Evidência executável: `RepeatedToolFailureTests` e
`tests/integration/p6-lunet-host/Program.cs`, usando o mesmo Workspace/Runner e o
Host real de inspeção de texto. Esse piloto já era autorizado por ADR-0031;
**não é integração de IA no APK do Lunet nem segundo consumidor em produção**.
