# ADD-0030 — Ordem do proprietário: a Área de trabalho, com o Explorer como barra lateral

**Data:** 2026-10-22. **Autoridade:** instrução explícita do proprietário nesta conversa, depois de usar a beta 2.0.0-beta.16, confirmada por ele: *"Pode registrar a área de trabalho, está decidido"*. **Registro:** DEC-0053. **Escopo:** o cliente C# nativo do Urbe (`apps/urbe/csharp`). **Substitui** o item 5 de ADD-0028 (DEC-0051), que faz do Explorer a base do app, e o item 4 de ADD-0029 (DEC-0052) na mesma parte. **Mantém** todo o resto de ADD-0028 (sem a simulação; dados da cidade preservados; abertura na última nota) e de ADD-0029 (conexões calmas), além de nativo sem web (ADD-0020), desempenho primeiro (ADD-0024) e experiência de usuário topo de linha (ADD-0026).

## O que o proprietário disse

> *"Também acho que o visual do explorador pode melhorar, na verdade o conceito pode mudar, deixar esse explorador atual como uma barra lateral, melhorar ele e deixar com estrutura de árvore, tá ligado? Fazer aquelas barras laterais modernas e muito mais sofisticadas que o explorador atual do Urbe. E no centro ser na verdade uma área de trabalho, como se fosse um Windows etc... Mas calma que visualmente o que imagino ainda é bem diferente. No Urbe antigo a gente tinha regiões porque eu queria mesmo diferenciar áreas, projeto tal é tal coisa, e projeto taltal é outra coisa. Aqui eu quero blocos. É só uma pasta? No fundo sim, mas a gente vai melhorar a navegação pelas pastas transformando em caixas. Imagine uma tela vazia onde o usuário clica em adicionar um novo projeto (o topo da hierarquia), aí esse projeto recebe na tela uma caixa com borda colorida, que pode receber bordas personalizadas. Mas na verdade visualmente é como se fosse uma caixa de vidro, porque a gente consegue ver o conteúdo que tem lá dentro. A caixa é um painel próprio de visualização com todas as anotações em grid — essa é a palavra certa, um grid, um catálogo com todas as notas, arquivos etc. Aí o usuário pode organizar, filtrar, pesquisar etc. (não somente na própria caixa como também na própria raiz). Aí ao clicar abre o editor/preview (a depender do caso) ou ao segurar ele abre pop-up com as informações, edição etc."*
>
> *"A gente tem que imaginar essa aba como se fosse um site, uma página HTML, onde cada projeto é um catálogo em grid. Enfim, essa é uma ideia inicial, eu tenho certeza que você consegue enxergar muito mais além do que eu."*

## Decisões do proprietário

1. **O Explorer vira uma barra lateral em árvore**, moderna e mais sofisticada que o Explorer atual.
2. **O centro do app é a Área de trabalho.** É ela a base do app, no lugar do Explorer. Na abertura, o app continua voltando para a última nota (UC-59).
3. **Projetos são caixas.**
   - Um projeto é o topo da hierarquia; no fundo, uma pasta do vault.
   - Na Área de trabalho, cada projeto é uma caixa com borda colorida, que aceita bordas personalizadas.
   - A caixa é **de vidro**: dá para ver o que tem dentro.
4. **Cada caixa é um catálogo em grid** com todas as notas e arquivos do projeto.
   - Dá para organizar, filtrar e pesquisar dentro da caixa e na raiz.
   - Tocar num item abre o editor ou a visualização, conforme o tipo.
   - Segurar um item abre um pop-up com as informações e a edição.
5. **A tela vazia convida a criar o primeiro projeto** ("adicionar um novo projeto").
6. **A referência visual é uma página, um site**: cada projeto é um catálogo em grid.
7. **Itens do ROADMAP C#:** UC-83 (barra lateral em árvore), UC-84 (Área de trabalho com projetos em caixas) e UC-85 (personalizar e organizar as caixas).
8. **O proprietário pediu ao agente que vá além da ideia inicial.** O desenho detalhado abaixo é **proposta do agente** dentro destas decisões. Cada parte é feita em fatias e validada no aparelho do proprietário (NN-017); o que ele mudar prevalece.

## Proposta do agente (dentro das decisões acima)

- **No celular**, a Área de trabalho ocupa a tela. A barra lateral é uma gaveta: abre pelo botão de menu ou deslizando da borda esquerda. **No computador e no tablet** (900 dp ou mais), a barra lateral fica fixa à esquerda, com largura ajustável e recolhível.
- **Barra lateral:**
  - seções Favoritos, Recentes e Projetos;
  - árvore com guias de recuo, setas que abrem e fecham, ícone por tipo e contagem por pasta;
  - renomear no lugar e arrastar para mover;
  - a pasta aberta na Área de trabalho fica marcada na árvore.
- **A caixa na raiz:**
  - cabeçalho com ícone, nome, número de itens, data da última mudança e menu;
  - os itens mais recentes em grid, de 6 a 9 conforme a largura, e "Ver tudo (N)";
  - subpastas aparecem como caixas menores dentro do grid;
  - a borda tem a cor do projeto. O vidro é uma superfície translúcida com um brilho suave e um contorno claro, leve para o celular, sem desfoque caro.
- **A caixa aberta** ocupa a Área de trabalho, com caminho de volta (Área de trabalho › Projeto › Subpasta).
  - Busca, filtro por tipo (notas, páginas, PDF, imagens, áudio, vídeo) e por tag.
  - Ordem: recentes, nome, criação ou manual. Grid ou lista.
- **Cartão do item:**
  - ícone do tipo, título, começo do texto ou miniatura, e data;
  - tocar abre;
  - segurar abre um painel de baixo (celular) ou um pop-up (computador) com caminho, datas, tamanho, ligações e tags, mais as ações renomear, mover para outro projeto, duplicar, favoritar, propriedades e lixeira.
- **Raiz:**
  - busca em todos os projetos, com os resultados agrupados por caixa;
  - "+ Novo projeto" cria a pasta no topo com nome e cor;
  - as notas soltas na raiz aparecem numa faixa discreta "Soltas";
  - arrastar caixas muda a ordem.
- **Calma e leve:**
  - nenhuma animação contínua; transições de 150 a 220 ms (UX.md);
  - só o que está na tela é montado;
  - miniaturas carregadas aos poucos;
  - tudo vem da biblioteca de notas já em memória (`NoteLibrary`).
- **Dados:**
  - As pastas e os arquivos continuam a única autoridade do conteúdo.
  - A aparência e a ordem das caixas (cor, estilo da borda, ícone, ordem, modo de exibição) ficam num arquivo novo do vault, `.urbe/area.json`. Ele declara que só guarda apresentação, indexada pelo caminho da pasta e atualizada quando a pasta é renomeada ou movida pelo app (NN-001).
  - Sem esse arquivo, as cores vêm de uma paleta estável pelo nome da pasta e a ordem é alfabética.
  - `.urbe/mapa.json` e `.urbe/mundo.json` continuam intocados.
- **Conexões (ADD-0029):** a constelação do vault (UC-72) pode ser um modo de ver a Área de trabalho, nunca a base.

## Limites que continuam valendo

- Nativo sem web (ADD-0020): a "página" é a referência visual, não HTML. A interface continua Avalonia.
- Nada da simulação volta (ADD-0028).
- Dados primeiro: gravação verificada, nada do usuário se perde, nenhuma pasta é criada, movida ou apagada sem ação da pessoa.
- Desempenho primeiro (ADD-0024): a Área de trabalho abre na hora, também com milhares de notas.
