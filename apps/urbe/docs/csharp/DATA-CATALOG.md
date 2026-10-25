# Catálogo de dados — Assistente nativo do Urbe

Autoridade de implementação: `Urbe.Core/Assistant/AssistantStore.cs`. Este catálogo
descreve a evolução do cliente C#; o catálogo em `docs/v2/discovery/` permanece
congelado como evidência histórica do JavaScript e não recebe formatos nativos.

## Urbe C# — espaços por agente (25/10/2026, ADR-0041)

Configuração privada atual `AssistantStore/config.json`: campos aditivos
`agentWorkspaceVersion:1`, `agentPolicies:{id:policy}`, `agentImages:{id:{providerId,model}}`
e, após a correção de permissões, `agentToolScopes:{id:"read"|"write"|"all"}`.
O novo campo é opcional, aditivo e não exige migração: sem entrada vale o teto
legado `AiAgentDef.Tools`; a política individual continua deny-by-default.
Mesmo ownership Core, fora do vault. Preserva `AgentModels`, instruções, credenciais,
memórias e escolhas legadas. Backup dos bytes originais verificado em
`config-before-agent-workspaces.json`; gravação lida de volta; ilegível/versão
futura/falha de backup bloqueiam migração. Marcador torna repetição idempotente.
`conversa-*.json` não é migrado nem reatribuído: AgentId/Vault filtram o histórico.
Excluir um agente exige confirmação: seus `conversa-*.json` legíveis, com
`AgentId` correspondente, são removidos em todos os vaults, com verificação
de leitura/listagem após cada exclusão. Se o armazenamento falhar silenciosamente,
a operação é abortada com erro visível e os dados de configuração do agente
permanecem disponíveis para nova tentativa. Arquivos de outros agentes e
conteúdo dos vaults não são afetados. JSON de conversa ilegível não pode ter
sua propriedade inferida e não é apagado por suposição.

Fixtures/testes: `AgentWorkspaceTests` e `AgentWorkspaceUiTests`.
