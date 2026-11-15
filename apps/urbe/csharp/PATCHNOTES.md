# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.56
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## Progresso nas notas (beta.56)

- **− e + na barra de progresso.** Uma meta numérica (por exemplo, 54 de 224 páginas) muda com um toque, direto na nota, sem abrir a configuração. O valor para na meta e no zero, e Desfazer volta. Se o valor vem de uma propriedade da nota (`value: $lidas`), os botões mudam a propriedade.
- **Checklist de uma seção.** Ao inserir um progresso (botão **%**), dá para contar só as tarefas sob um título da nota, não a nota inteira.
- **Ver pendentes.** A barra de uma checklist mostra quantas tarefas faltam e lista quais são; tocar numa leva até ela.
- **O Assistente vê os mesmos números.** Ao ler uma nota, ele recebe o progresso calculado pelo app, em vez de contar as caixas por conta própria.
- **Anotar instante do YouTube sem digitar o segundo.** No navegador de estudo, **Anotar instante** agora lê o tempo do player oficial e cria a ligação para aquele momento (o rótulo aparece como "Vídeo em 3:25"). O campo do instante continua lá para ajustar à mão, e é usado quando o player ainda não carregou o vídeo.
- **Modo leitura no navegador de estudo.** Em ⋯, mostra só o texto principal da página, no tema do Urbe: sem menus, anúncios e sem a tela branca de noite. Os links continuam funcionando, e dá para selecionar um trecho e adicioná-lo à nota.
- **Salvar página como nota.** Em ⋯, cria uma nota com o texto da página, o endereço e o dia, sem sair da página. Se já existir uma nota com esse nome, o app pergunta outro, nunca grava por cima.
- **Notas de rodapé no livro sem perder o lugar.** Num EPUB, tocar no número de uma nota de rodapé ou de fim mostra o texto dela embaixo da página, no capítulo onde você está. **Ir até a nota** leva até ela; **Fechar** continua a leitura. Vale para livros que marcam as notas como notas (EPUB 3 e a maioria dos livros recentes). Nos outros, o link leva até a nota como antes.
- **Adaptar uma nota para a publicação, sem mexer na pesquisa.** No estúdio de Publicações, o novo botão de lápis em cada nota cria um **resumo, roteiro, fala, tradução ou adaptação** como uma nota de verdade, ao lado da original e começando com o texto dela. A publicação passa a mostrar a adaptação; a nota original não muda. A adaptação diz de onde veio (com um link que funciona em qualquer editor de Markdown) e, se a original mudar depois, o estúdio avisa, com **Abrir a origem** e **Marcar como revisada**.
- **Blocos de código dentro de blocos de código.** Um exemplo entre `~~~` com crases dentro, ou um bloco de quatro crases, aparecia quebrado no editor e na página; agora segue a regra do Markdown (só a mesma marca, do mesmo tamanho ou maior, fecha o bloco).

## O que continua pendente

- **Plano de consolidação 2.6:** os demais itens seguem para as próximas versões.
- **No aparelho:** conferir os botões − / + e Ver pendentes com o dedo, o Anotar instante num vídeo do YouTube, o Modo leitura e Salvar página como nota em sites reais, as notas de rodapé em livros reais, Adaptar no estúdio, no celular e no computador.
