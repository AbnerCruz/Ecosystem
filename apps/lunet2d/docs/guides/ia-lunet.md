# IA do Lunet

A IA trabalha junto com você dentro do projeto: constrói, corrige, explica e cria arte, sem sair do editor.

## Configurar (uma vez)

Toque em **✦ IA** e escolha o provedor (OpenRouter, Anthropic, OpenAI, Google Gemini, DeepSeek, Groq ou um endpoint compatível), o modelo e a chave da API. A chave fica cifrada no Android Keystore deste aparelho; nunca vai para o projeto, o Git ou o APK do jogo. O provedor pode cobrar pelo uso.

- Cada provedor guarda a **sua** chave: trocar de provedor não apaga a outra ("✓ salva neste aparelho" aparece ao lado).
- **Ver modelos do provedor** lista os modelos que o provedor realmente oferece (os que aceitam ferramentas aparecem primeiro) — não precisa digitar o id de memória.
- **Testar conexão** faz um pedido mínimo e diz se a chave, o endereço e o modelo funcionam, e em quanto tempo responderam.
- **Pesquisar código de referência na internet** (ligado por padrão) e **Modo inicial do chat** (Auto, Ensinar ou Fazer).

## Usar

- **✦ IA** na barra superior abre o painel ao lado do código (folha inferior no celular, painel lateral em telas largas).
- Selecione um trecho e toque em **✦ Pedir à IA**.
- Em **Erros**, toque em **✦ Corrigir com a IA**; no **Console**, pergunte o que está acontecendo.
- **📎** anexa imagens e arquivos (do aparelho ou das suas Notas). Anexos são copiados para `Notes/`.

A IA recebe automaticamente o arquivo aberto, a linha do cursor, a seleção, os erros e o console.

## Ensinar e fazer

Acima da caixa de texto há três modos:

- **✦ Auto** (padrão): a IA entende pelo pedido. "Como faço para o personagem pular?", "me explica o SpriteBatch", "o que é GameTime?" → ela **explica** sem mexer no projeto. "Crie um inimigo", "adicione um placar", "como faço X? faz pra mim" → ela **implementa**.
- **📖 Ensinar**: sempre explica, em passos, no seu projeto (quais arquivos e classes mexer, trechos curtos comentados, por quê, como testar). Nenhum arquivo é alterado.
- **⚒ Fazer**: sempre implementa, compila e corrige.

Depois de uma explicação aparece **⚒ Fazer por mim**: a IA implementa exatamente o que explicou.

## Rapidez e organização do código

- Cada pedido já leva o **mapa do projeto** (pastas, imagens e sons, classes, campos, propriedades e métodos com linhas) e a **referência rápida da API**. A IA não gasta rodadas só para descobrir onde as coisas estão.
- A IA organiza o código como um projeto profissional: **um tipo por arquivo**, pastas por responsabilidade (`Scenes/`, `Entities/`, `Components/`, `Systems/`, `UI/`, `Data/`, `Util/`, ou as que seu projeto já usa), e reutiliza suas classes e variáveis pelos nomes reais. O `Game.cs` só orquestra.
- Ao compilar, ela recebe avisos quando um arquivo passa de ~350 linhas ou junta várias classes, e divide.
- Com a internet ligada, ela pesquisa código de referência (técnicas de plataforma, câmera, IA de inimigos, pathfinding…) e adapta para a API do Lunet. Consultas nunca levam chaves nem trechos do seu código; só endereços públicos são abertos.

### Validação no aparelho (DEVICE)

1. Configurações da IA: salve chaves em dois provedores, troque entre eles e confira que as duas continuam salvas; use **Ver modelos do provedor** e **Testar conexão** (com chave certa e errada).
2. No modo **Auto**, pergunte "Como faço para o personagem pular?": a resposta explica em passos, o cabeçalho diz "explicando (nada foi alterado)" e nenhum arquivo muda. Toque **Fazer por mim**: agora ela implementa.
3. Peça "crie um inimigo que persegue o jogador": confira que surgem arquivos separados (ex.: `Entities/Enemy.cs`) e que o `Game.cs` só os usa.
4. Compare o tempo até a primeira resposta com a versão anterior num pedido simples.
5. Peça uma técnica (ex.: "pulo com coyote time") e veja na linha do tempo a pesquisa e a leitura de páginas.

## O que ela sabe

- **O próprio Lunet:** pesquisa a referência da API do Framework, os guias offline e a lista do que existe e do que ainda não existe. Se você pedir algo que o Lunet não tem, ela diz e propõe a melhor alternativa.
- **O seu projeto:** lê e busca arquivos, conhece a paleta, o tamanho predominante dos sprites, os nomes, as classes e as suas notas de estilo.

## Coerência artística e contextual

Antes de criar arte ou mecânicas, a IA lê o estilo do projeto e segue a paleta, os tamanhos, o contorno, os nomes e o estilo de código já usados. Ao criar um sprite, ela é avisada se uma cor ou tamanho foge do padrão. Escreva suas preferências em uma nota com "estilo" no nome (por exemplo `Notes/estilo-visual.md`): a IA segue essa nota.

## Desenho → sprite

1. Em **Notas e esboços**, importe a foto do desenho (⇩) ou desenhe um esboço.
2. Abra a imagem e toque em **✦ Virar sprite** (ou anexe a imagem na conversa).
3. A IA enxerga a imagem como uma grade de pixels (fundo do papel removido), redesenha limpa no estilo do projeto e salva `Assets/<nome>.sprite.json` (editável no Sprite Studio) e `Assets/<nome>.png` (para o jogo), com o código de uso.

## Conversa persistente e execução fora do chat

- A conversa é privada do projeto e fica salva desde o envio do pedido, mesmo enquanto a resposta ainda está sendo gerada. Fechar ou reabrir o chat, navegar pelo editor e voltar aos projetos não apaga o histórico.
- O trabalho prossegue em um serviço Android em primeiro plano quando você minimiza o Lunet ou bloqueia a tela. A notificação indica a atividade; ao reabrir o chat, os últimos passos aparecem. O botão de enviar vira **Parar** durante uma execução. Há apenas uma tarefa em andamento por vez.
- Forçar parada ou o Android matar o processo pode interromper o trabalho. Ao abrir novamente, revise os arquivos alterados antes de repetir a solicitação; a execução não é retomada automaticamente.
- `Content.LoadTexture("Textures/Cells.png")` procura `Content/Textures/Cells.png`. Para `Assets/heroi.png`, use `Content.LoadTexture("Assets/heroi.png")`. A forma antiga `Content.LoadTexture("Content/Textures/Cells.png")` tem fallback de compatibilidade. A IA verifica existência e assinatura de PNG ao compilar, mas não afirma que o jogo rodou sem executar o Preview.

### Testes em Android 13

1. Inicie uma tarefa longa, saia do chat para o editor, retorne e confira pedido, passos e resposta; repita entrando em outro projeto.
2. Minimize e bloqueie a tela durante a execução. Verifique o serviço e o retorno ao histórico; tente cancelar outra tarefa.
3. Faça force-stop apenas com uma tarefa de teste e confira se o histórico permaneceu e a execução foi marcada como interrompida.
4. Crie `Content/Textures/Cells.png`, carregue a textura em `Game.cs` e execute o Preview. Remova o PNG e confira que o verificador acusa falta do arquivo.

## Segurança e revisão

- Escrever no projeto é opcional (configurações da IA).
- Toda alteração entra num checkpoint: o cartão de mudanças mostra os arquivos, o diff, **Manter** ou **Desfazer tudo**.
- Quando altera código, a IA compila e verifica texturas referenciadas literalmente. Somente o Preview comprova a execução.
- A IA só acessa arquivos dentro do projeto aberto.

## Limites atuais

- A IA vê imagens pela grade de pixels (até 64×64 células), não como uma foto em alta resolução.
- Arquivos binários que não são imagem (PDF, por exemplo) ficam guardados em `Notes/`, mas a IA não os lê.
