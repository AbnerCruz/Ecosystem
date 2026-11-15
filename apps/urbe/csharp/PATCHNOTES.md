# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.55
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## O app é a sua pasta, 1:1 — e tudo responde na hora (beta.55)

- **O que está no aparelho é o que aparece no app.** Uma pasta apagada no aparelho (pelo gerenciador de arquivos ou por outro app) some do Urbe; uma pasta criada lá aparece, mesmo vazia. Ao voltar para o app, ele relê a pasta e mostra o que mudou fora dele: notas apagadas, criadas ou editadas e pastas. Não é sincronização: o app não guarda uma cópia, mostra a pasta.
- **Excluir, Lixeira, mover e salvar sem espera, no celular e no computador.** Cada gravação agora confere e relê só os arquivos que ela mexe. Antes, cada uma lia o vault inteiro duas vezes, e a demora crescia com o vault. Num vault de 2.000 notas no computador, mover 30 notas caiu de 2,1 s para 0,8 s. Restaurar, excluir de vez e esvaziar a Lixeira também ficaram imediatos, e a Lixeira com muitos itens abre rápido.
- **Excluir várias notas pergunta na hora.** Antes de mostrar a confirmação, o app lia o vault inteiro uma vez para cada nota escolhida, para avisar o que depende dela. Agora lê uma vez só para todas.
- **Uma mudança feita fora do app não trava mais o Urbe.** Antes, se qualquer arquivo do vault mudava fora do app (uma pasta apagada no aparelho, por exemplo), todas as gravações eram recusadas com "a pasta foi modificada fora do Urbe" até reabrir o vault. Agora só se recusa gravar por cima de um arquivo que mudou fora do app, que é o caso em que algo se perderia.
- **Multi-seleção para organizar o vault:**
  - cada toque marca ou desmarca a nota na hora, sem redesenhar a lista inteira;
  - a seleção não se perde quando o app salva ou relê a pasta;
  - o novo botão **Selecionar tudo** marca todas as notas mostradas (as pastas abertas ou todos os resultados de uma busca), para mover, agrupar ou excluir de uma vez;
  - a barra de seleção ficou mais clara: a contagem e **Selecionar tudo** em cima e, embaixo, todas as ações visíveis com nome (Mover, Nova pasta, Duplicar, Lixeira, Abrir, Renomear, Favoritar, Publicação). Antes eram só ícones, que ficavam cortados ou escondidos atrás do botão redondo flutuante;
  - enquanto você seleciona, o botão flutuante sai da frente;
  - cada pasta da árvore mostra quantas notas tem, contando as subpastas, para você ver onde o vault está crescendo.
- **Nova conversa (Assistente)** sempre responde: funciona mesmo com uma execução em andamento, que continua na conversa dela, e fecha a tela de Conversas ou de Configurações se ela estiver aberta.
- **Personalização**: tema, cor de destaque e os outros botões aplicam na hora e gravam logo depois. Se a gravação falhar, a aparência anterior volta e o motivo aparece. Antes, o botão esperava a gravação e, se ela falhasse, nada acontecia.
- **Texto da nota na largura inteira da tela.** Com o horário dos blocos ligado, cada bloco perdia um terço da largura para a coluna dos horários e o texto ficava espremido. Agora o horário fica pequeno no canto de cima de cada bloco, sem ocupar largura, e o texto usa a tela toda. Ao tocar para escrever, o bloco também não muda mais de largura.
- **Parágrafos sem recuo indevido.** Espaços digitados antes do texto de um parágrafo apareciam como recuo; agora ficam fora do desenho, como no Obsidian. O arquivo continua com o que você escreveu.
- **Texto maior no Assistente sobre a nota**: a conversa no popup usa 15 px, com a mesma formatação da conversa completa (antes eram 13 px, em texto simples), e o popup ficou mais alto.
- **Fórmulas largas** só diminuem até 85 % para caber; as maiores que isso rolam para o lado e continuam legíveis (antes podiam diminuir até 60 %).

Tudo foi testado automaticamente. No aparelho, falta confirmar:
- apagar uma pasta pelo gerenciador de arquivos e voltar ao app;
- excluir várias notas e usar a Lixeira;
- a multi-seleção com o dedo.
