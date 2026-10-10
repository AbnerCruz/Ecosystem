# P6-5 R4 — Chat C# multi-turno sobre o Agent Workspace existente

O chat em terminal é uma superfície do Product Ecosystem AI, não um novo Runtime:
cada mensagem passa pelo mesmo EcosystemAiCli.RunAsync, que instancia o
AgentWorkspace.WorkspaceSession e seu AgentRunner existente.

Este incremento não substitui a UI gráfica mobile-first ou o aplicativo Android.
Entrega uma conversa real com escolha explícita da sessão e sem processo ocioso.

## Uso

Primeiro crie um projeto e uma sessão usando os comandos de cadastro da P6-5:

~~~bash
dotnet run --project src/EcosystemAi.Cli -- \
  --create-project --catalog /dados/privados/catalogo \
  --project /projeto/existente --project-name "Meu projeto"

dotnet run --project src/EcosystemAi.Cli -- \
  --create-session --catalog /dados/privados/catalogo \
  --project-id ID_PROJETO --session-title "Implementação"
~~~

Para conversar, use os IDs retornados, o endpoint e os valores de orçamento:

~~~bash
dotnet run --project src/EcosystemAi.Cli -- \
  --chat --catalog /dados/privados/catalogo \
  --project /projeto/existente --project-id ID_PROJETO --session-id ID_SESSAO \
  --endpoint https://PROVEDOR/chat/completions --model MODELO \
  --budget-cents 30 --max-call-cents 5 \
  --input-usd-per-million 1 --output-usd-per-million 3
~~~

A chave segue exclusivamente em ECOAI_API_KEY (variável de ambiente).
Endpoints HTTP não locais são recusados; HTTPS é exigido fora do loopback.
O chat mostra o projeto/sessão escolhidos e aguarda uma linha de texto.
Use /ajuda para comandos e /sair ou EOF para terminar. Entrada vazia,
comandos locais e mensagens maiores que 16 KiB não chamam o provider.

Quando existe histórico anterior, o chat adiciona --use-history ao run, que
reaproveita, com limites, as mensagens registradas na sessão. O contexto
anterior é dado não confiável, não concede capabilities adicionais. Quando a
sessão está vazia, o primeiro envio não tem contexto prévio. Os dados
continuam na mesma revisão de LocalProjectStore, junto aos receipts de runs.

**Custo e permissões:** limites de orçamento são **por execução/mensagem**
e não um teto acumulado do chat. A permissão de criar arquivos novos permanece
desativada por padrão e só é incluída mediante --allow-create.
Verificação independente, event log e custos continuam sendo os do Runtime.
Uma tarefa falha não é reenviada automaticamente: o usuário escolhe
manualmente a próxima ação. Ctrl+C cancela a execução atual.

**Privacidade:** conversas persistidas são texto claro em catálogo opt-in.
A opção de reenviar histórico significa compartilhar mensagens anteriores com
o modelo configurado. Não configure segredos no prompt.
O catálogo e o journal ficam fora do workspace acessível ao agente.

**Limites reais:** é chat textual no terminal C#, sem streaming token-a-token,
gestão visual de equipes/agentes ou Android instalado. A experiência gráfica,
secret store e tarefas da P6-5 ainda precisam ser implementadas e verificadas.

## Testes

R4InteractiveChatTests exercita o dispatch do mesmo runner por um callback
injetável (sem acesso real à rede no CI), histórico anterior somente quando
há mensagens, grants opt-in, saídas, limites, códigos de erro, caminho da sessão
e flags conflitantes. A suíte de contratos do Product continua responsável
pela execução HTTP mockada, budget, verificação e isolamento de capabilities.
