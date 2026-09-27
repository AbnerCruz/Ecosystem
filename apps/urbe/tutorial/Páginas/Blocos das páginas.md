# Blocos das páginas

> [!tip] Quer liberdade total?
> O bloco **Layout livre** monta a seção peça por peça, com estilo próprio para computador, tablet e celular. Veja [[Tutorial/Páginas/Layout livre|Layout livre]].

Cada seção de uma página usa um destes blocos. Para trocar o tipo de uma seção já criada, use **Trocar tipo de bloco…** no menu dela.

| Bloco | Para quê |
|---|---|
| **Capa** | Abertura com título grande, texto, botões e imagem |
| **Texto** | Texto livre em Markdown (títulos, listas, tabelas, código, fórmulas, links) |
| **Nota** | Mostra uma nota sua e se atualiza sozinho quando ela muda |
| **Coleção de notas** | Cartões ou lista de várias notas (de uma pasta, de uma tag, escolhidas ou recentes), com resumo e, se quiser, o texto completo |
| **Destaques** | Grade de benefícios com ícone, título e texto |
| **Cartões** | Projetos, posts ou produtos, com imagem, etiqueta e link |
| **Galeria** | Grade de imagens; ao tocar, a imagem abre grande |
| **Imagem** | Uma imagem com legenda |
| **Vídeo** | YouTube, Vimeo ou arquivo de vídeo |
| **Citação** | Uma frase em destaque com o autor |
| **Depoimentos** | Vários depoimentos em cartões |
| **Números** | Números grandes com rótulo (métricas, conquistas) |
| **Linha do tempo** | Eventos em ordem: história, carreira, roteiro |
| **Perguntas** | Perguntas frequentes que abrem e fecham |
| **Chamada** | Faixa com convite para uma ação e botões |
| **Colunas** | De 2 a 4 colunas de texto |
| **Planos** | Tabela de preços com itens e botão |
| **Contato** | E-mail, telefone e links (redes, portfólio) |
| **Contagem regressiva** | Contagem até uma data |
| **Código** | Bloco de código com botão de copiar |
| **Sumário** | Lista automática das seções da página |
| **Divisor** | Separador ou espaço |
| **HTML livre** | Seu próprio HTML, CSS e JavaScript (use com cuidado) |

Há ainda os **blocos de livro** (capa, folha de rosto, créditos, dedicatória, sumário, parte, capítulo, capítulos de uma pasta, sobre o autor e colofão), explicados em [[Tutorial/Páginas/Livros|Livros]].

## Aparência de cada seção

Além do conteúdo, toda seção tem **Aparência da seção**:

- **Fundo**: nenhum, superfície, cor principal, degradê, imagem ou invertido.
- **Espaço vertical** e **largura** do conteúdo.
- **Alinhamento**: à esquerda ou centralizado.
- **Âncora** e **mostrar no menu**: cria um item na barra de navegação que leva até a seção.
- **Ocultar seção**: esconde a seção sem apagar.
- **Cor de fundo própria** e **cor do texto própria**, só para aquela seção.
- **Seção em caixa**: o conteúdo vira um cartão com borda e cantos.
- **Altura mínima**: natural, meia tela ou tela cheia.
- **Animação**: a do tema, nenhuma, aparecer, subir, aproximar, da esquerda ou da direita.

Em **Avançado** ficam a âncora, o item do menu, uma **classe CSS** e o **CSS desta seção**. Nele, `&` é a própria seção: `& h2{color:tomato}` pinta só os títulos dela. Se você escrever só propriedades (sem chaves), elas valem para a seção inteira.

## Meus blocos

Montou uma seção do jeito que gosta? No menu **⋯** dela, toque em **Salvar como bloco reutilizável**. O bloco (conteúdo e aparência) vai para `Páginas/Blocos/` e aparece em **Meus blocos**, no topo da lista do **＋**, em qualquer página. Para apagar um bloco seu, apague o arquivo `.block.json` na aba Notas.

Próximo: [[Tutorial/Páginas/Modelos e temas|Modelos e temas]]
