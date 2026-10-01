# Conhecendo o Assistente

O **Assistente** é uma inteligência artificial que trabalha **dentro das suas notas**. Ele não só conversa: ele **lê, procura, escreve, organiza e liga** notas, cria páginas e até muda a aparência do Urbe, sempre mostrando o que está fazendo.

## Onde fica

- A terceira aba, **Assistente**, embaixo (ou à esquerda, no computador).
- O botão **Assistente** no rodapé do editor abre o Assistente já sabendo qual nota está aberta e qual trecho está selecionado.

## Antes de tudo: conectar um modelo

O Assistente precisa de um "cérebro": um modelo de IA de algum provedor (OpenRouter, Anthropic, OpenAI, Google, ou um modelo rodando no seu próprio computador). Veja [[Tutorial/Assistente/Conectar um modelo|Conectar um modelo]].

## Uma conversa

1. Escreva o pedido, por exemplo: *"Resuma esta nota em tópicos"*.
2. O Assistente mostra cada passo: **Lendo…**, **Buscando…**, **Editando…**. Toque num passo para ver os detalhes.
3. Em tarefas grandes, ele monta um **plano** com a lista de etapas e vai marcando cada uma.
4. Quando ele quer mudar uma nota, aparece a **alteração proposta**, com o antes e o depois. Você decide: **Aprovar**, **Recusar** ou **Aprovar todas**. Veja [[Tutorial/Assistente/Agentes e modos|Agentes e modos]].
5. Depois de pronto, o botão **Desfazer** volta as notas para como estavam antes daquela resposta.

Se a resposta for cortada, use **Continuar**. Se der erro, **Tentar de novo**.

## Botões do topo

- **Nome do agente** (com a setinha): troca de agente.
- 🕐 **Conversas**: as conversas anteriores, para retomar ou excluir.
- **＋**: nova conversa.
- ⚙️ **Configurações do Assistente**: provedores, modelos, instruções, memória, agentes personalizados e limite de passos.

## Sugestões para começar

Numa conversa nova aparecem sugestões, como:

- *O que tem no meu vault? Dê uma visão geral.*
- *Resuma esta nota em tópicos.*
- *Revise o texto desta nota: clareza e erros.*
- *Encontre notas relacionadas e sugira `[[links]]` para esta.*
- *Encontre notas sem nenhuma ligação e sugira conexões.*
- *Crie uma nota-índice que ligue as notas por tema.*

## Memória

Diga *"lembre que eu prefiro respostas curtas"* e o Assistente guarda isso para as próximas conversas. A memória aparece (e pode ser apagada) nas configurações.

Veja também: [[Tutorial/Assistente/O que o Assistente sabe fazer|O que o Assistente sabe fazer]] · [[Tutorial/Assistente/Pedidos que funcionam bem|Pedidos que funcionam bem]]
