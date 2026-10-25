# Catálogo de dados — Assistente nativo do Urbe

Autoridade de implementação: `Urbe.Core/Assistant/AssistantStore.cs`. Este catálogo
descreve a evolução do cliente C#; o catálogo em `docs/v2/discovery/` permanece
congelado como evidência histórica do JavaScript e não recebe formatos nativos.

## Urbe C# — espaços por agente (25/10/2026, ADR-0041)

Configuração privada atual `AssistantStore/config.json`: campos aditivos
`agentWorkspaceVersion:1`, `agentPolicies:{id:policy}`, `agentImages:{id:{providerId,model}}`.
Mesmo ownership Core, fora do vault. Preserva `AgentModels`, instruções, credenciais,
memórias e escolhas legadas. Backup dos bytes originais verificado em
`config-before-agent-workspaces.json`; gravação lida de volta; ilegível/versão
futura/falha de backup bloqueiam migração. Marcador torna repetição idempotente.
`conversa-*.json` não é migrado nem reatribuído: AgentId/Vault filtram o histórico.
Fixtures/testes: `AgentWorkspaceTests` e `AgentWorkspaceUiTests`.
