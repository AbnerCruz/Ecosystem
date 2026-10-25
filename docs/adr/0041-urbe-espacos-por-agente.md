# ADR-0041 — Espaços de conversa e configuração individual por agente

## Status

Proposto. Direção explicitamente solicitada pelo proprietário em 25/10/2026. Implementação
sujeita a testes e integração crítica; não autoriza release nem certifica DEVICE.

## Contexto

UC-81, issue #756. O seletor anterior alterava AgentId dentro da conversa e o
histórico listava todos os agentes. Manter esse comportamento não permite
continuidade independente. Evoluir o domínio existente, sem novo runtime/storage.

## Problema

Separar continuidade e configuração sem apagar dados, inventar autoria de mensagens
legadas ou converter permissões globais em grants individuais.

## Opções

1. Manter chat compartilhado e filtrar apenas a aparência.
2. Repartir mensagens legadas por autoria inferida.
3. Reutilizar identidades/arquivos atuais, com configuração aditiva e migração segura.

## Decisão

Opção 3, conforme o pedido explícito atual. Sem nova infraestrutura.

Identidade e instruções permanecem em AiAgentDef/CustomAgents; modelos e blocos
continuam em AgentModels/AgentInstructionBlocks. Acrescentar AgentPolicies e
AgentImages por ID estável em config.json, com agentWorkspaceVersion=1. Conversas
mantêm AgentId e seus arquivos atuais; filtro usa AgentId e Vault. Trocar de
agente nunca reatribui uma conversa. Configuração individual é autoridade da
política de execução, nunca o campo Policy de um chat antigo. Sem configuração
individual explícita: readonly. Não inferir concessão de escrita a partir de
política global ou histórica. AllowTool/NeedsApproval continuam fiscalizando
capacidades, exclusões e plugins; opt-out global de rede permanece intacto.

Apenas Assistente (geral) está ativo numa instalação nova. Os demais perfis são
templates copiados com identidade nova quando escolhidos. Perfis antigos com
histórico, nome ou configuração individual são preservados como agentes
existentes: não são novos padrões automáticos. IDs desconhecidos referenciados
por históricos ficam acessíveis como agentes preservados somente leitura.

Em instalação nova, ou com Assistente legado ainda sem escolha explícita de modelo, usar o ID de modelo recomendado pelo ILocalTextRuntime
fornecido pelo head (catálogo real Urbe.LocalAi.LocalModelCatalog: Gemma 4 E2B/E4B),
via provedor device. Não baixar pesos automaticamente: uso offline depende de
instalação verificada e suporte do dispositivo. Sem runtime não inventar modelo.
Nenhum modelo visual por padrão; geração exige seleção explícita por agente.
Escolhas globais anteriores são preservadas como legado, e uma escolha visual
explicitamente salva é copiada para os agentes existentes, sem criar acesso por
seleção automática de outro serviço. Modelo escolhido cujo provedor foi removido
não deve cair silenciosamente em outro provedor.

## Migração e falhas

Antes de gravar, preservar os bytes originais em
config-before-agent-workspaces.json e verificar sua leitura exata. Depois gravar
a configuração e ler de volta. Migração é idempotente pelo marcador; backup não
é sobrescrito. JSON ilegível ou versão futura impede alterações. Não regravar
conversa alguma durante migração. Campos de modelo/política legados continuam
no arquivo; políticas de chat não se tornam grants de agente.

O formato antigo não prova a autoria individual de cada mensagem: uma conversa
podia trocar de agente. Preservar o AgentId gravado e indicar no histórico essa
limitação, sem dividir, mesclar ou adivinhar autores. A restauração técnica
consiste em repor o backup privado como config.json com o aplicativo fechado;
conversas não precisam ser restauradas porque não foram migradas.

## Consequências

Core testa defaults, separação por agente/vault, idempotência, backup, versão
futura, falha de escrita e round-trip de modelos/permissões/imagens/instruções.
UI testa seleção, retomada e reinicialização com persistência real. Os testes
anteriores de escrita devem configurar permissões explicitamente. Sem novas
dependências e sem alteração do vault, providers HTTP ou regras de aprovação.
NN-001/009/010/011/016/017/018/019/022; ADR-0032/0033/0039.

## Alternativas rejeitadas

Chat compartilhado não atende a continuidade por agente. Divisão por autoria inferida
não tem evidência nos dados e poderia misturar ou perder conteúdo. Nova base de dados
ou novo runtime não demonstram necessidade neste incremento.

## Referências

Issue #756; UC-81; MANIFEST; ADR-0032/0033/0039; DATA-CATALOG.
