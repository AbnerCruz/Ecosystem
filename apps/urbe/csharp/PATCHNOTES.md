# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.50
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## Publicações: Word, OpenDocument, PowerPoint e apresentação

No estúdio de Publicações, o menu ⋯ ganhou novas saídas, todas feitas a partir das notas como estão agora:

- **PDF de verdade**, salvo direto como arquivo (antes só pela impressão do sistema): páginas A4 com margens e números. Livros ganham capa, sumário com as páginas e cada capítulo começa numa página nova. Títulos não ficam sozinhos no fim da página, tabelas longas continuam com o cabeçalho repetido, e fórmulas e gráficos saem nítidos (vetor). Os links são clicáveis.

- **Documento Word (DOCX)** e **OpenDocument (ODT)**: títulos de verdade (aparecem na navegação e no sumário), listas e listas numeradas, tarefas, citações, código, tabelas, imagens do vault e links. Livros ganham sumário e cada nota começa numa página nova.
- **Apresentação PowerPoint (PPTX)** editável: cada nota, cada título # ou ## e cada linha `---` começa um slide. Texto, tópicos, tabelas e imagens são objetos do PowerPoint, não fotos. Texto que não cabe continua no slide seguinte, marcado “(cont.)”, em vez de ser cortado.
- **Apresentação HTML**: o layout Slides agora gera uma apresentação de verdade. Setas, espaço ou deslizar trocam de slide; **F** tela cheia; **O** visão geral; **N** notas do apresentador. Um slide cujo conteúdo não cabe é avisado, e a impressão em PDF sai com um slide por página.
- **Notas do apresentador**: escreva dentro de uma caixa `:::notas … :::`. No PowerPoint elas vão para a área de notas; nunca aparecem no slide.
- **Caixas** (`:::tipo título`) saem como caixas em todos os formatos (antes apareciam os `:::` crus).
- **Gráficos** (` ```grafico `) saem como desenho em todos os formatos: vetor no HTML, EPUB e ODT; no Word e no PowerPoint, imagem com o vetor original junto.
- **Criar publicações de onde você está**: com várias notas selecionadas no Explorer (botão de publicação na barra), com o resultado de uma busca, com todas as notas de uma tag (busca de comandos → “Nova publicação de uma tag”) ou com só um trecho de uma nota (menu ⋯ → “Criar publicação de um trecho”, escolhendo o título). A publicação sempre aponta para as notas; nada é copiado.
- Depois de salvar, o Urbe diz o que o formato não guarda igual (por exemplo, fórmulas vão como TeX editável e links entre notas viram texto).

## Questionários e cursos

- Novo bloco ` ```quiz ` (menu `/` → **Questionário**): escreva a pergunta com `?`, as alternativas como tarefas e marque com `[x]` as certas. Uma linha `>` traz a explicação. Na nota, você toca numa alternativa e em **Conferir** para ver se acertou e a explicação; a nota guarda só as perguntas.
- Novo layout de publicação **Curso**: cada nota é um módulo, cada `##` uma aula, e a propriedade `objetivos` aparece no começo do módulo. Sai como um arquivo HTML com o sumário do curso, uma aula por vez, questionários com correção na hora e o progresso (aulas concluídas, questões certas) guardado no navegador de quem estuda, nunca nas suas notas.
- Em PDF, Word, ODT e EPUB os questionários saem com as questões e o gabarito no fim.

## Cálculo nas notas

Novo bloco ` ```calculo ` (menu `/` → **Cálculo**): cada linha aparece com o resultado ao lado, e a nota guarda só as linhas.

- Contas com vírgula decimal, porcentagem, fatorial, raiz, π, e funções como média, mediana, desvio-padrão, mdc, mmc e combinação.
- Definições que valem para as linhas de baixo: `preco = 12,5`, `f(x) = x^2 + 1`.
- **Unidades com conversão**: `100 km/h em m/s`, `37 °C em °F`, `2 h + 30 min em min`. Somar metros com segundos dá erro, nunca um número errado.
- Matrizes (determinante, inversa, produto), `derivada de …`, `simplificar`, `expandir`, `resolver … = …` (exato até o 2º grau; acima disso aproximado, marcado com ≈) e `integral de …` (com `de a até b`, o valor).
- O Assistente ganhou a ferramenta **calcular**, com o mesmo motor: faz as contas em vez de estimar.

## Dados de planilhas nas notas

Novo bloco ` ```dados ` (menu `/` → **Dados**): mostra uma tabela feita de um CSV ou JSON do vault, ou da tabela de uma nota, depois de passos que você escreve (filtrar, ordenar, agrupar com soma e média, calcular uma coluna nova, limitar, resumo). A planilha nunca é alterada e a tabela se refaz quando o arquivo muda. Números e datas no formato brasileiro são reconhecidos, e planilhas salvas pelo Excel em português abrem com os acentos certos. O Assistente ganhou a ferramenta **consultar dados**, com os mesmos passos, para analisar planilhas sem lê-las inteiras.

Termine o bloco com `gráfico barras categoria total` (ou `linhas`, `pizza`, `dispersão`) para ver um **gráfico** em vez da tabela. Ele é desenhado dos dados, muda quando eles mudam e sai nítido em todas as publicações.

## Legendas das gravações

Uma transcrição de áudio, depois de revisada, vira **legendas**: no menu ⋯ da nota de transcrição, **Exportar legendas** salva SRT (players e editores de vídeo) ou WebVTT (páginas web). As legendas seguem os horários de cada trecho, têm no máximo duas linhas e saem com o texto que você corrigiu na nota.

## Onde uma nota é usada

No menu ⋯ da nota, **Onde esta nota é usada** lista o que depende dela: publicações (inclusive as que usam só um trecho), blocos de dados que leem a tabela dela, barras de progresso que contam as tarefas dela, notas que a incorporam ou ligam para ela. Toque num item para abri-lo. Ao mandar para a lixeira uma nota de que algo depende, o aviso diz o que vai ficar sem a fonte.

## O Assistente edita pela estrutura da nota

O Assistente ganhou a ferramenta **editar estrutura**: ele troca, acrescenta, apaga ou insere seções pelo título; muda propriedades; marca e acrescenta tarefas; altera células e acrescenta linhas em tabelas. Não precisa mais copiar o texto exato nem reescrever a nota inteira. Tudo passa pela mesma aprovação, pelo selo de autoria e pelo **Desfazer**. Se algo não existir ou for ambíguo, nada é alterado e ele recebe o motivo.

## O Assistente cita as suas notas

- O Assistente encontra nas suas notas os trechos que sustentam uma pergunta ou afirmação e responde citando a nota, a seção, as linhas e o texto exato. Se as notas não tratam do assunto, ele diz isso em vez de supor.
- A busca ignora acentos e maiúsculas e entende plurais e variações comuns do português. Ela mostra também as notas ligadas que tratam do mesmo assunto.
- Antes de reutilizar uma citação, o Assistente confere se o trecho continua igual, foi movido, foi alterado ou sumiu.
- Comentários %%…%% e a pasta Personalização nunca entram como evidência.

## Skills do Assistente

Crie uma pasta em `Personalização/Skills/` com um `SKILL.md` (nome, descrição e instruções, no formato aberto Agent Skills) para ensinar o Assistente a fazer um trabalho do seu jeito. Ele vê só o nome e a descrição e carrega o resto quando o pedido combina. Uma skill não dá permissões e nada nela é executado. O Urbe já vem com skills de apresentação, livro e cálculo.

## Segurança do Assistente

- Uma ação que precisa da sua aprovação nunca é aprovada sozinha: se a execução não tiver como perguntar a você, a ação não é feita.
- **Parar** agora interrompe também a geração de uma imagem em andamento (antes ela continuava e podia ser cobrada).

## O que ainda falta conferir no aparelho

Os testes automáticos (507 de interface e 1.428 do núcleo) passam. Os arquivos Word, ODT e PowerPoint foram abertos no LibreOffice, a apresentação HTML foi testada num navegador e o PDF foi lido de volta para conferir páginas, sumário e links. Ainda precisam ser conferidos no moto g32:

- salvar cada formato pelo seletor de arquivos do Android;
- os blocos de cálculo e de dados (com gráfico);
- a apresentação HTML.

Abrir os arquivos no Word e no PowerPoint também fica para conferir.
