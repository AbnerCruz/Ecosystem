# Layout livre

O **layout livre** deixa você montar uma página (ou uma página de livro) **peça por peça**: cada título, parágrafo, imagem, botão ou caixa é uma peça solta, que você move, aninha e estiliza do seu jeito.

## Como começar

- Toda **Nova página** já abre com uma seção em layout livre.
- Toque em **＋** → **Blocos** → **Layout livre** (fica no topo da lista).
- Ou **＋** → **Composições** e escolha um conjunto pronto: ele entra no layout livre selecionado (ou numa seção nova).
- Ou carregue o modelo **Tela livre** (**＋** → **Modelos**), que já vem montado com peças soltas.
- Ou transforme qualquer bloco pronto: no menu **⋯** da seção, toque em **Converter em layout livre**. Um capítulo, por exemplo, vira um título e um parágrafo solto para cada parágrafo do texto.
- Em livros, **Capítulos de uma pasta** pode ser desmembrado: menu **⋯** → **Separar em capítulos**. Aí cada capítulo pode ser convertido e editado sozinho.

## As peças

| Peça | Para quê |
|---|---|
| **Container** | Uma caixa que guarda outras peças, inclusive outros containers |
| **Título** | Do H1 (maior) ao H6 |
| **Parágrafo** | Texto em Markdown |
| **Imagem** | Com legenda, texto alternativo e link |
| **Botão** | Principal, secundário ou discreto |
| **Lista**, **Citação**, **Ícone**, **Divisor**, **Espaço** | Os detalhes de sempre |
| **Vídeo**, **Nota do vault**, **HTML livre** | Conteúdo de fora ou do seu vault |
| **Tabela** | Tabela em Markdown, com cabeçalho e linhas |
| **Código** | Bloco de código com linguagem e botão de copiar |
| **Fórmula** | Fórmula em LaTeX, em destaque |
| **Selo** | Etiqueta curta (Novo, Grátis, Beta…) |
| **Incorporar** | Mapa, formulário ou qualquer página `https://` numa moldura segura |
| **Quebra de página** | Em livros, começa uma página nova naquele ponto |

## Containers: o segredo do celular

Um container arruma as peças de dentro de três jeitos:

- **Empilhados**: uma embaixo da outra.
- **Lado a lado**: em linha. Quando falta espaço (no celular, por exemplo), a linha **quebra sozinha**. A "largura mínima de cada filho" decide quando isso acontece.
- **Grade**: colunas iguais, com um número fixo de colunas ou automáticas.

Como não existe posição fixa em pixels, a página **se adapta a qualquer tela** sem ficar bagunçada.

## Estilo por tamanho de tela

No topo do editor fica **Estilo para: Computador · Tablet · Celular**, ligado ao botão de tamanho da prévia.

- Em **Computador**, o que você muda vale para todas as telas.
- Em **Tablet** ou **Celular**, o que você muda vale **só para aquele tamanho** (e os menores). Embaixo de cada campo aparece o valor que vem do computador.
- Exemplo: duas colunas lado a lado no computador e, no celular, **Arranjo: Empilhados**.
- **Voltar ao estilo do computador** apaga as mudanças daquele tamanho.

Cada peça pode ter: arranjo e alinhamento, tamanho (largura, altura, proporção), espaçamento interno e externo, fundo (cor, degradê ou imagem), borda, cantos, sombra, opacidade, fonte, tamanho e peso da letra, altura da linha, alinhamento do texto, maiúsculas e itálico. Em **Avançado** ficam uma classe CSS e o **CSS da peça** (`&` é a própria peça).

## Editar

- **Camadas** mostra a árvore de peças. Toque numa camada para selecionar.
- **Na prévia**, toque numa peça para selecioná-la. **Toque de novo num texto** para escrever direto nele.
- **Adicionar dentro / depois**: com um container selecionado, a peça entra nele; com outra peça, entra logo depois dela.
- **Composição pronta**: 24 conjuntos, como cabeçalho, rodapé, capa com imagem, capa dividida, números, depoimentos, planos e preços, perguntas, equipe, chamada, galeria, linha do tempo, citação grande, barra lateral, contato, abertura de capítulo, página de livro, página em duas colunas, capa centralizada, duas colunas, imagem + texto, grade de cartões, cartão e caixa de destaque.
- **Subir / Descer** trocam a ordem. **Sair** tira a peça do container. **Entrar** coloca a peça no container logo acima. **Envolver** cria um container em volta. Há também **Duplicar** e **Apagar**.
- Tudo pode ser desfeito.

## Em livros

No campo **No livro** de cada layout livre:

- **Folha própria**: começa numa página nova.
- **Página inteira**: sem margens, como uma capa.
- **Continua o texto**: segue logo depois do que vier antes.

Próximo: [[Tutorial/Páginas/Livros|Livros]] · Voltar: [[Tutorial/Páginas/Blocos das páginas|Blocos das páginas]]
