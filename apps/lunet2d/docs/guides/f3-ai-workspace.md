# IA global e co-work no projeto

A Home tem chat geral e configurações de provedor, chave e modelo padrão. No editor, o seletor grava um modelo por GameId e configuração de provedor. Use **Usar padrão** para retirar essa preferência de um jogo. O catálogo distingue sugestões, modelos listados pelo provedor e capacidade de ferramentas conhecida. Há pesquisa por nome, ID ou alias, atualização manual e entrada por ID quando necessário.

## Estudo e Fazer

Toda abertura de ajuda de Aprender usa **Estudo**. Um novo pedido só ganha ferramentas de escrita após a escolha explícita de **Fazer** e com escrita global habilitada. Depois de enviar, o seletor volta a Estudo. O botão **Projeto** permite excluir todos os arquivos, diagnósticos, anexos, console e histórico daquele pedido, inclusive das ferramentas. **Conferir contexto** mostra o GameId, arquivo, trecho, revisão e referências selecionadas. A documentação offline pode ser consultada mesmo com o projeto excluído. O chat geral não tem acesso a games.

Os dois chats guardam a conversa e o rascunho em armazenamento privado. Durante a resposta, atualizam o texto em curso sem remontar as mensagens anteriores. A conversa do jogo também registra as ferramentas e seus resultados. Se o processo morrer, texto parcial e alterações permanecem disponíveis. Uma execução interrompida não repete automaticamente uma edição. Uma nova solicitação inicia uma nova execução.

No **Estudo**, a IA lê e explica a lógica existente, distingue defeitos de preferências e oferece ajuda progressiva. Se a pessoa pedir código, pode apresentar a correção ou exemplo necessário; não há bloqueio pedagógico artificial. No **Fazer**, preserva escolhas válidas e altera apenas o necessário. Mudanças maiores são justificadas, não impostas. Convenções de organização são recomendações, não requisitos para jogos pequenos. As ferramentas de escrita continuam indisponíveis no Estudo.

Caso de regressão: um movimento com `Vector2.Normalize` deve ser analisado em termos de vetor nulo, ultrapassagem do alvo e estado do destino; a normalização não precisa ser substituída por uma divisão explícita apenas por preferência matemática.

## Revisões

Em **Fazer**, as ferramentas reaproveitam a leitura de arquivos, documentação, sprites e compilação do Lunet. Para substituir código, o arquivo precisa corresponder à versão lida. O diff mostra os bytes gravados pela IA e sinaliza divergência posterior. O desfazer confere todos os caminhos e hashes antes de restaurar. Checkpoints legados sem hash comprovado continuam legíveis e podem recusar desfazer. Caminhos que atravessam links simbólicos são recusados.

## Evidência e compatibilidade

- O Lunet usa o runtime canônico e o protocolo Chat Completions. A publicação de texto por SSE suporta frames de conteúdo e chamadas de ferramenta fragmentadas. Nos provedores sem preço na resposta, o Lunet liquida somente a reserva interna e omite o custo do provedor no evento. Os outros consumidores podem continuar exigindo preço confirmado.
- O catálogo usa a paginação por cursor da Anthropic e por token do Gemini. Modelos informados pelo OpenRouter usam metadados explícitos de ferramentas. Capacidade não declarada continua desconhecida, e o provedor pode recusar um modelo sem compatibilidade.
- A bateria controlada usa respostas sem credenciais reais para testar streaming, falhas, permissões, modelos e histórico. Isso não mede a latência pública dos provedores. Se um provedor exigir um dialeto próprio fora de Chat Completions, é preciso um endpoint compatível para usar o chat.
- As referências comparadas incluem [modo estudo](https://help.openai.com/en/articles/11780217-using-study-mode-in-chatgpt), [co-work no editor](https://docs.github.com/en/copilot/how-tos/chat-with-copilot/chat-in-ide), [relato de interrupção após ferramentas](https://github.com/openai/codex/issues/24850), [OpenRouter](https://openrouter.ai/docs/api/api-reference/models/get-models), [Anthropic](https://platform.claude.com/docs/en/api/models/list), [Gemini](https://ai.google.dev/api/models) e [OpenAI](https://developers.openai.com/api/reference/resources/models/methods/list).
