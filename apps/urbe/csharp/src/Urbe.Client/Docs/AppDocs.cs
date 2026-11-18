using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The Tutorial of the native app (owner, UC-64): documentation of what the app does now, read in
/// <see cref="TutorialPanel"/> only. The 1.8.4 tutorial (apps/urbe/tutorial, <see cref="TutorialNotes"/>) stays the
/// immutable reference and is not shown. Kept as C# strings so no project file change is needed.
/// </summary>
public static partial class AppDocs
{
    /// <summary>The articles in reading order: "Comece aqui" first, then each section in turn.</summary>
    public static IReadOnlyList<CityNote> Load() =>
    [
        new(LegacyTutorial.Start, ComeceAqui),
        new("Tutorial/Escrever/Área de trabalho.md", AreaDeTrabalho),
        new("Tutorial/Escrever/Explorer.md", Explorer),
        new("Tutorial/Escrever/Editor.md", Editor),
        new("Tutorial/Escrever/Modelos.md", Modelos),
        new("Tutorial/Ferramentas/Publicações.md", Paginas),
        new("Tutorial/Ferramentas/Código e prévia ao vivo.md", Codigo),
        new("Tutorial/Ferramentas/Assistente.md", Assistente),
        new("Tutorial/Ferramentas/Plugins.md", Plugins),
        new("Tutorial/Ajustes/Personalização.md", Personalizacao),
        new("Tutorial/Ajustes/Backups, exportar e restaurar.md", Backups),
        new("Tutorial/Ajustes/Atalhos.md", Atalhos),
    ];

    private const string AreaDeTrabalho = """
        # Área de trabalho

        A Área de trabalho é a tela de base do Urbe. Cada **projeto** (uma pasta no topo da sua pasta de notas) é uma caixa de vidro na sua cor, com as notas e os arquivos mais recentes à vista; as notas soltas na raiz ficam em **Soltas**.

        ## Abrir e procurar

        - Toque numa caixa (ou em **Ver tudo**) para abrir o catálogo do projeto, com o caminho de volta no alto.
        - A busca procura em todos os projetos, ou só no aberto. Embaixo dela: tipos (Notas, Páginas, Imagens, PDF, Mídia), as tags mais usadas, a **ordem** (recentes, nome, criação) e **grade ou lista**. Cada caixa lembra a sua ordem e o seu modo.
        - Toque num cartão para abrir a nota; **segure** (ou clique com o botão direito) para ver onde ela está, quando foi criada e mudada, quantas palavras tem, as ligações e as tags, com Abrir, Renomear, Mover para, Duplicar, Favoritar e Mover para a lixeira.

        ## Organizar as caixas

        - O **⋯** de um projeto tem **Aparência da caixa** (cor, borda contínua, tracejada, dupla ou com brilho, e ícone) e **Mover caixa** para antes ou depois. Isso fica em `.urbe/area.json`, que só guarda a aparência: as pastas continuam sendo o que vale.
        - No computador, arraste um cartão até outra caixa para mover a nota para aquele projeto, e arraste o cabeçalho de uma caixa sobre outra para trocar a ordem.

        ## A barra lateral

        - No alto: **Favoritos** e **Recentes**, depois os **projetos** em árvore, com linhas que mostram os níveis.
        - No computador, arraste a borda da barra para mudar a largura, e o botão de **Pastas** recolhe e devolve a barra. No celular, ela abre como uma gaveta pelo botão **Pastas** ou deslizando da borda esquerda; tocar fora fecha.
        - Para renomear no lugar: segure a linha e toque em **Renomear** (ou F2 no computador). Enter confirma, Esc desiste.
        - Renomear ou mover uma nota, um arquivo ou uma pasta atualiza os links para ela em todas as notas (`[[Nome]]`, `[[Pasta/Nome]]`, `![[foto.png]]`, com `#seção` e `|apelido`), e também os links Markdown (`[texto](Pasta/Nome.md)`, `![](foto.png)`), do jeito que foram escritos: um caminho relativo à nota continua relativo, inclusive quando é a própria nota que muda de pasta. Um nome que mais de um arquivo do vault usa fica como está, porque não dá para saber qual era.

        ## Navegar no celular

        No celular não há abas embaixo: o **botão redondo no canto inferior direito** abre um leque com **Assistente** (para cima), **Notas** e **Grafos** (para a esquerda), cada um com o nome em cima. Toque no que quiser; Voltar ou um toque fora fecha o leque. Com a prática, **segure o botão e arraste** na direção do destino: ao soltar, ele abre, sem parar no leque (arrastar para longe desiste). Quando o Assistente espera uma resposta sua, o botão ganha um ponto. No computador, os destinos ficam na barra da esquerda.

        A **Constelação** (a estrela no alto da Área de trabalho) mostra as notas como um céu: veja [[Editor]].
        """;

    private const string ComeceAqui = """
        # Comece aqui

        Bem-vindo ao **Urbe**. Suas notas são arquivos Markdown (`.md`) numa pasta sua: você escreve, organiza em pastas e liga uma ideia à outra com `[[links]]`. O Urbe também produz páginas, sites e livros a partir delas.

        ## O primeiro passo: a pasta

        O Urbe guarda tudo em arquivos `.md` comuns, numa pasta do aparelho que você escolhe. Abra **Configurações** (a engrenagem no alto) → **Pasta no dispositivo** e escolha uma pasta, vazia ou com notas que você já tem. Sem uma pasta aberta, nada é gravado.

        ## A tela

        - **A Área de trabalho** ocupa a tela. Cada projeto (uma pasta no topo do vault) é uma caixa de vidro com a sua cor: dá para ver através dela as notas e os arquivos mais recentes, e tocar na caixa abre o catálogo do projeto. **+ Novo projeto** cria uma caixa nova. A busca no alto procura em todos os projetos, ou só no projeto aberto.
        - **O Explorer** é a árvore de pastas e notas: no computador fica ao lado da Área de trabalho; no celular abre pelo botão **Pastas**. No alto dele: o nome da sua pasta, com um ponto que mostra se está tudo salvo (verde), gravando (amarelo) ou com erro (vermelho); depois **Nova**, **Mais opções** e **Configurações**.
        - **Embaixo:** **Notas** e **Assistente**.
        - Ao abrir, o Urbe volta direto para a nota em que você estava.

        ## Por onde seguir

        - [[Área de trabalho]], [[Explorer]] e [[Editor]]: escrever e organizar.
        - [[Modelos]]: notas que começam prontas.
        - [[Código e prévia ao vivo]]: editar código do vault e ver um HTML como página enquanto escreve.
        - [[Atalhos]]: os atalhos de teclado.

        Na primeira vez, a **Configuração inicial** leva pela pasta, pelo tema e pelo Assistente; ela pode ser refeita em **Configurações → Configuração inicial**.

        As **Propriedades** da nota (o bloco `---` do começo) têm um formulário no alto da nota, em Visual e em Fonte: toque em **Propriedades** para abrir. **Aliases** são outros nomes da nota: um link `[[apelido]]` leva a ela, aparece nas conexões e funciona em `![[apelido]]`. **Tags** aceitam as do vault com um toque. Texto, número, data, sim/não e listas têm campo próprio, e **Adicionar propriedade** cria outras. O que o formulário não entende fica como estava.

        **Tags do projeto.** As tags escritas nas propriedades da **nota da pasta** (a nota com o nome da pasta, dentro dela: `Droerni/Droerni.md`) valem para tudo o que está nessa pasta e nas subpastas, sem ser escritas nas outras notas. Elas aparecem em **herdadas**, com a origem (`#worldbuilding` de Droerni), e contam na busca por tag, nos filtros da Área de trabalho, nas consultas das páginas e para o Assistente. Mover uma nota para outro projeto troca as herdadas na hora. Para uma nota não herdar uma tag, toque no × dela (o Urbe escreve `sem-tags: [worldbuilding]`); para não herdar nenhuma, use `herdar: false`. Fora do Urbe, as tags herdadas não aparecem nas outras notas.

        O **modo leitura** (o botão **Edição/Leitura** no rodapé do editor, ou **Mais ações → Modo leitura**) vale para Visual e Fonte: nada é alterado sem querer, a barra de formatação some e as propriedades só são mostradas. Links, comentários e cartões continuam funcionando.

        Para atualizar: **Configurações → Verificar atualização** procura uma versão mais nova do Urbe (só quando você pede). O arquivo abre no navegador: no Android, instale por cima do Urbe atual; no Windows, extraia o .zip por cima da pasta do Urbe. Suas notas ficam onde estão.

        Use a **busca** no alto do índice deste tutorial para achar qualquer assunto, e **Anterior / Próximo** para ler em sequência.
        """;

    private const string Explorer = """
        # Explorer

        O **Explorer** mostra a pasta como uma árvore de arquivos e pastas. No computador ele fica ao lado da Área de trabalho; no celular, abre pelo botão **Pastas** e fecha ao escolher uma nota.

        O Urbe é a sua pasta, um para um: o que está nela é o que aparece aqui. Uma pasta ou nota apagada, criada ou editada por outro app (o gerenciador de arquivos, uma sincronização) aparece assim quando você volta ao Urbe. Se a nota aberta for movida fora do app, o editor a acompanha; se for apagada, o Urbe avisa e, ao salvar, cria a nota de novo com o que você escreveu. Cada pasta mostra quantas notas tem, contando as subpastas.

        ## Navegar

        - **Buscar arquivos** filtra por nome e caminho.
        - Toque numa **pasta** para abrir ou fechar; toque numa **nota** para abrir no [[Editor]].
        - Outros arquivos (imagens, PDF…) abrem para visualizar.
        - O **⋯** de uma pasta: **Nova nota aqui**, **Nova subpasta**, **Renomear**, **Mover para** e **Mover para a lixeira**.

        ## Criar

        **Nova** (no alto do Explorer):

        - **Nova nota**, na pasta selecionada.
        - **A partir de um modelo**, quando há notas em `Modelos` (veja [[Modelos]]).
        - **Nova pasta**, na pasta selecionada.
        - **Nota rápida**, por voz, quando disponível. No Android também há o widget **Urbe: nota rápida** na tela inicial, que grava sem abrir o app: toque para começar e toque de novo (ou na notificação, que mostra o tempo) para terminar. Pausas para pensar não encerram a nota; ela só para sozinha depois de 45 segundos de silêncio ou aos 15 minutos, com aviso no último minuto. Dá para gravar a próxima enquanto a anterior ainda está sendo salva. No Windows, **Ctrl+Alt+N** (de qualquer programa) ou o ícone do Urbe na bandeja abrem a caixa da nota rápida: escreva, ou aperte **Win+H** para o ditado do Windows escrever o que você fala. As notas rápidas ficam em **Notas rápidas**, uma nota por mês com um título para cada dia; cada uma entra como tarefa com o horário, `- [ ] 08:11 — texto`, venha de você, do widget ou de um agente.
        - **Transcrever em nota** (Android 13 ou mais novo): no menu de um áudio do vault (uma aula, uma reunião; importe o arquivo ou grave com o gravador do celular), o Urbe transcreve no próprio aparelho, em trechos cortados nas pausas, e cria ao lado a nota **Nome — transcrição**, com o áudio, o texto e o horário de cada trecho, para revisar. O andamento aparece no alto, com **Cancelar**; parar não deixa nada. Pronta a nota, **Organizar com o Assistente** abre o Assistente com o pedido escrito, para você escolher o agente e enviar.
        - **Novo grafo**: um canvas livre (`Nome.grafo.json`) com cartões, notas e arquivos do vault, ligações (linha ou seta), formas, grupos e desenhos, com desfazer e refazer. **⋯ → Grafos** lista os grafos e gera a estrutura do vault (pastas, arquivos, notas e links). Renomear ou mover uma nota, um arquivo ou uma pasta pelo Urbe atualiza os cartões em todos os grafos; um cartão cuja nota foi excluída aparece como **não encontrada**. Um grafo incorporado numa nota (`![[Mapa]]`) não prende a rolagem: um dedo rola a nota, **dois dedos** movem e aproximam o grafo (no computador, Ctrl+roda), e tocar num cartão abre a nota, o arquivo ou a pasta. O Assistente também lê, cria e altera grafos (por exemplo, "faça um grafo da família real com as notas dos personagens"): a mudança aparece em texto para você aprovar.

        ## Selecionar e mover

        - **Segure** uma nota ou pasta para selecionar; depois, cada toque marca ou desmarca outra. Num arquivo (imagem, PDF, áudio), **Selecionar** no menu dele começa a seleção, e os arquivos entram junto com as notas.
        - A barra embaixo mostra quantos itens estão escolhidos e **Selecionar tudo** (todas as notas à vista, ou todos os resultados de uma busca). Embaixo, as ações: **Mover**, **Nova pasta** (agrupar), **Duplicar**, **Lixeira**, **Abrir**, **Renomear**, **Favoritar** e **Publicação**.
        - **Mover** leva notas e arquivos para a pasta escolhida e atualiza os links. Excluir manda as notas para a Lixeira; arquivos não têm Lixeira e são apagados de vez, e a pergunta avisa.
        - **Segure e arraste** sobre uma pasta para mover para ela. O arquivo muda de pasta no vault.
        - No Android, **Voltar** desfaz a seleção antes de fechar o Explorer.

        ## Mais opções

        O botão **⋯** do Explorer: **Buscar notas e comandos**, **Recentes**, **Favoritos**, **Importar arquivos**, **Exportar tudo (.zip)**, **Lixeira** e **Pasta do Urbe no dispositivo**.

        ## Lixeira

        O que você exclui vai para a lixeira: **Restaurar** devolve a nota, **Restaurar tudo** devolve todas de uma vez (cada uma para onde estava, recriando a pasta se for preciso), **Excluir de vez** apaga e **Esvaziar** limpa tudo. Se o lugar de uma nota já estiver ocupado, ela fica na Lixeira e o Urbe avisa: nada é sobrescrito.
        """;

    private const string Editor = """
        # Editor

        O editor abre por cima da Área de trabalho e do Explorer. Ele salva sozinho e confere a gravação no arquivo; o rodapé mostra o estado.

        ## Visual e Fonte

        - **Visual** mostra a nota formatada; toque num trecho para editar aquele bloco.
        - **Fonte** mostra o Markdown puro. Numa nota longa (mais de 8.000 caracteres), o Fonte edita um bloco por vez, para a digitação continuar rápida; a busca na nota volta ao texto inteiro.
        - O modo inicial fica em **Personalização → Editor**.
        - **Horário de cada bloco** (Personalização → Editor) mostra, à direita de cada bloco, quando ele mudou pela última vez. Vem das **Versões anteriores**: um bloco só movido mantém o horário, um bloco dividido, juntado, colado ou desfeito ganha o da mudança, e um bloco mais antigo que as versões guardadas mostra o dia em que a nota foi criada, quando o Urbe o conhece.

        ## A barra

        Título 1, 2 e 3, **negrito**, _itálico_, `código`, lista, tarefa, citação, **Vincular nota**, link externo, linha horizontal, **∑** (fórmula) e **Caixa**. Um botão aceso indica a marca onde está o cursor; tocar nele de novo tira a marca.

        Na barra do alto: **Assistente**, os botões de plugins (como o Relógio), **Visual / Fonte** e **Mais ações**.

        ## O menu /

        Num bloco vazio, digite `/` para ver os blocos: títulos, lista, tarefa, citação, divisória, link para nota, link externo, fórmula, fórmula em destaque, bloco de código, comentário e caixa. Continue digitando para filtrar.

        ## Links

        - Digite `[[` e escolha a nota nas sugestões. As duas notas ficam ligadas: **Mais ações → Conexões** mostra a nota no meio de um céu calmo, com as que ela cita e as que a citam. Tocar num ponto abre aquela nota.
        - A **Constelação** (a estrela no alto da Área de trabalho, ou ⋯ do Explorer) mostra todas as notas como um céu: cada projeto é uma névoa na sua cor. Arraste para passear, aproxime com a pinça ou a roda; toque numa estrela para ver as ligações dela e toque de novo para abrir. As posições ficam em `.urbe/constelacao.json`.
        - Tocar num link abre a nota. Se ela ainda não existe, o Urbe pergunta se quer **criar nota**. Um link para um arquivo que não é nota (`[[foto.png]]`) abre o visualizador.
        - As sugestões de `[[` e `![[` trazem as notas e qualquer arquivo do vault: imagens, PDF, áudio, vídeo, código, páginas e grafos.
        - `[[Plano#Metas]]` leva direto ao título **Metas** da nota (aparece como “Plano › Metas”), `[[Plano#^meta]]` ao trecho que termina com ` ^meta`, e `[[#Metas]]` a um título da própria nota. O sumário também leva ao título no modo Visual.

        ## Incorporar

        `![[Nome]]` mostra no lugar o conteúdo, num cartão: a nota (`![[Plano]]`), uma imagem (`![[Imagens/capa.png]]`), PDF, áudio, vídeo, HTML, código ou texto, e um grafo (`![[Mapa.grafo.json]]`), que dá para navegar. Tocar no alto do cartão abre o original; tocar no corpo edita a linha.

        Para mostrar só uma parte da nota: `![[Plano#Orçamento]]` traz a seção desse título (até o próximo título do mesmo nível; `#Título#Subtítulo` desce mais um nível), `![[Plano#^meta]]` traz o parágrafo ou o item de lista que termina com ` ^meta`, e `![[Plano#L3-L8]]` traz as linhas 3 a 8. Os marcadores ` ^meta` não aparecem nos cartões. Um nome que não existe mostra “Não encontrei … no vault”.

        ## Progresso

        O botão **%** da barra insere uma barra de progresso na nota. Ela é um bloco de texto, legível em qualquer editor:

        - **Meta numérica**: `value: 54` e `total: 224` mostram 54/224 · 24,1% · faltam 170. Os botões **−** e **+** mudam o valor na própria nota (Desfazer volta). `value: $lidas` usa a propriedade `lidas` da nota, e os botões mudam a propriedade.
        - **Checklist desta nota** (`source: tasks`) conta as tarefas `- [ ]` e `- [x]` sozinho: marcar, desmarcar, acrescentar ou apagar uma tarefa atualiza a barra. Cada caixa conta uma vez, subtarefa também; tarefas dentro de código ou de comentários não contam. Sem tarefas, diz “Sem tarefas”, nunca 100%.
        - **Checklist de uma seção** (`section: Hoje`) conta só as tarefas sob esse título. `note:` com o id de outra nota conta as dela, mesmo se ela for renomeada ou movida.
        - **Ver pendentes** lista as tarefas que faltam; tocar numa leva até ela.

        A barra nunca marca uma tarefa por conta própria. O Assistente, ao ler a nota, recebe os mesmos números que você vê.

        ## Estudar com uma fonte ao lado

        **Estudo** (no menu radial ou no **+**) abre uma fonte e a nota juntas: em cima e embaixo no celular, lado a lado no computador. A divisória entre as duas se arrasta. A fonte pode ser um site, um vídeo do YouTube ou um arquivo do vault (PDF, livro, slides, áudio, vídeo).

        No navegador:

        - Digite um endereço ou uma pesquisa. **Voltar**, **Avançar** e **Recarregar** ficam no alto.
        - **Adicionar seleção à nota** cita o trecho selecionado com o endereço; **Perguntar à IA** leva o trecho ao Assistente.
        - Em **⋯**: **Modo leitura** mostra só o texto principal da página, no tema do Urbe (sem menus, anúncios nem a tela branca à noite); **Salvar página como nota** cria uma nota com esse texto, o endereço (`source`) e o dia (`captured`), sem sair da página. Também **Salvar referência na nota**, **Copiar endereço** e **Abrir no navegador do sistema**.
        - Num vídeo do YouTube, que toca no player oficial, **Anotar instante** cria na nota um link para o momento em que o vídeo está, como “Vídeo em 3:25”. O campo do instante serve para ajustar à mão.
        - Nada da página é guardado sem você pedir, e a página não tem acesso às suas notas.

        Num livro (EPUB), tocar no número de uma nota de rodapé mostra a nota embaixo da página, sem sair do capítulo; **Ir até a nota** leva até ela. Em **Aa**, **Ler por páginas** troca a rolagem por páginas do tamanho da tela: as setas viram a página e, no fim do capítulo, passam ao seguinte.

        ## Fórmulas

        Escreva LaTeX entre `$…$` no texto ou `$$…$$` em destaque, por exemplo $E = mc^2$. O botão **∑** (ou **Ctrl+M**) abre o editor de fórmulas, com **No texto** / **Destaque**, prévia, símbolos e modelos. Toque numa fórmula pronta para editar.

        ## Gráficos de funções

        Um bloco ```` ```grafico ```` desenha as funções escritas nele, uma por linha, com eixos e legenda (menu `/` → **Gráfico de função**):

        ```
        título: Ondas
        y = sin(x)
        f(x) = x^2/8 - 1
        x: -6..6
        y: -2..3
        ```

        Vale `+ - * / ^`, parênteses, `2x` e `3(x+1)`, `pi` e `e`, e as funções `sin` (ou `sen`), `cos`, `tan`, `sqrt` (ou `raiz`), `abs`, `ln`, `log`, `exp`, entre outras. Sem a linha `y:`, a altura se ajusta às curvas. Uma linha que não dá para ler aparece em vermelho embaixo do gráfico, e as outras continuam desenhadas. Toque no gráfico para editar o texto. Nas páginas, ele sai como imagem.

        ### Cálculo

        Um bloco ```` ```calculo ```` faz contas: cada linha aparece com o resultado ao lado, refeito sempre que a nota é lida (menu `/` → **Cálculo**). A nota guarda só as linhas.

        ```
        preco = 12,5
        qtd = 4
        preco * qtd
        f(x) = x^2 + 1
        f(3)
        100 km/h em m/s
        resolver x^2 - 5x + 6 = 0
        derivada de x^3 + 2x
        integral de sen(x) de 0 até pi
        ```

        - **Valores**: `+ - * / ^`, `%`, `5!`, `√`, `π`, vírgula decimal (`1,5`); funções como `sen`, `cos`, `ln`, `log`, `raiz`, `arred`, `media`, `mediana`, `desvio`, `soma`, `mdc`, `mmc`, `comb`. Separe argumentos com `;`: `media(3; 5; 8)`.
        - **Definições**: `a = 3` e `f(x) = …` valem para as linhas de baixo.
        - **Unidades**: escreva o número com a unidade e converta com `em` (`37 °C em °F`, `2 h + 30 min em min`). Somar metros com segundos dá erro, nunca um número errado.
        - **Matrizes e listas**: `[[1; 2]; [3; 4]]`, com `det`, `inv`, `transposta` e produto.
        - **Álgebra**: `derivada de …`, `simplificar …`, `expandir …`, `resolver … = …` (exato até o 2º grau; acima disso, aproximado com ≈) e `integral de …` (com `de a até b`, o valor).
        - Uma linha com `//` é comentário. Uma linha que não dá para calcular diz por quê, em vermelho, e as outras continuam. Nas publicações (HTML, EPUB, Word, ODT, PowerPoint) o bloco sai como tabela de linha e resultado. O Assistente usa o mesmo motor para fazer contas.

        ### Dados

        Um bloco ```` ```dados ```` mostra uma tabela feita de um arquivo do vault (CSV, JSON) ou da tabela de uma nota, depois de passos que você escreve, um por linha (menu `/` → **Dados**). A fonte nunca é alterada e a tabela é refeita sempre que a nota é lida.

        ```
        fonte: Planilhas/gastos.csv
        filtrar valor > 10 e categoria = "Mercado"
        agrupar por categoria: soma(valor) como total, contar
        ordenar por total desc
        ```

        - `fonte:` é o caminho no vault. Para a tabela de uma nota, `fonte: Livros.md`, ou `fonte: Livros.md#Lidos` para a tabela sob o título **Lidos**.
        - Passos: `filtrar` (= != > < >= <=, `contém`, `começa com`, com `e` e `ou`), `colunas a, b`, `ordenar por coluna desc`, `agrupar por coluna: soma(x), media(x), min(x), max(x), mediana(x), contar`, `calcular nova = preco * qtd`, `limitar 10`, `distintos coluna` e `resumo`.
        - Coluna com espaço vai entre aspas: `"valor total" > 10`. Números brasileiros (1.234,56; R$ 10) e datas (05/11/2026) são reconhecidos.
        - Um erro num passo aparece no lugar da tabela, dizendo o que corrigir. Tabelas grandes mostram as primeiras 300 linhas e o total.
        - Nas publicações o bloco sai como tabela com a fonte embaixo. O Assistente usa os mesmos passos para analisar planilhas.
        - **Gráfico**: termine com `gráfico barras coluna valor` (ou `linhas`, `pizza`, `dispersão`), com quantas colunas de valor quiser e, se quiser, `título: …`. O gráfico é desenhado dos dados e muda quando eles mudam; nas publicações sai como imagem nítida (vetor).

        ### Parâmetros e movimentos

        Uma linha como `a = 2 em 0..5` cria um **parâmetro**: um controle deslizante embaixo do gráfico. As curvas acompanham enquanto você arrasta, e a nota guarda o valor onde você soltar. Sem `em`, o controle vai de -10 a 10. Nomes juntos se multiplicam: `ax` é `a·x`.

        Uma curva **paramétrica** é escrita como `(x(t), y(t))`, com `t:` para o intervalo (sem ele, de 0 a 2π). Assim dá para desenhar movimentos, como um lançamento:

        ```
        título: Lançamento
        v = 10 em 0..30
        θ = pi/4
        g = 9,8
        (v cos(θ) t, v sin(θ) t - g t^2/2)
        t: 0..1,44
        y: 0..6
        ```

        ## Marcas do Urbe

        São texto comum no arquivo, então a nota continua legível em qualquer editor:

        ```
        %%um comentário seu%%
        palavra{"comentário sobre a palavra"}
        #Ideia: um parágrafo com o estilo da tag
        ```

        - `%%…%%` aparece separado no editor e fica fora das páginas e do HTML exportado.
        - `palavra{"…"}` mostra um 💬 sobre a palavra; toque nele para ler, escrever, **Editar** ou **Excluir** comentários.
        - `#Tag:` no começo do parágrafo dá a ele a cor da tag (Todo, Ideia, Feito, Aviso, Dúvida, Nota ou as suas, em **Personalização → Tags**).

        ## Caixas

        Um bloco entre `:::tipo Título` e `:::` vira uma caixa, como esta:

        :::dica Use caixas para fichas
        O tipo é uma palavra só (ficha, aviso, dica…). Caixas podem ficar umas dentro das outras.
        :::

        Insira pelo botão **Caixa** ou pelo menu `/`.

        ## Mais ações

        **Nova nota nesta pasta** (criada ao lado da aberta; Voltar retorna a esta), **Renomear**, **Mover para outra pasta**, **Mostrar nas notas**, **Versões anteriores** (o histórico, para restaurar), **Conexões** (as notas ligadas a ela, num desenho calmo), **Estrutura e backlinks** (os títulos da nota e as notas que apontam para ela), **Fixar em painel** e **Mover para a lixeira**.

        **Fixar em painel** deixa a seleção (ou a nota inteira) no topo, com rolagem própria, enquanto você escreve embaixo. O painel continua quando você abre outra nota.

        ## Abas e painéis

        Cada nota aberta vira uma aba. **Voltar / Avançar** percorrem o caminho; **Abrir em painel dividido** mostra duas notas lado a lado; **Localizar e substituir** procura no texto. Veja [[Atalhos]].
        """;

    private const string Modelos = """
        # Modelos

        Um modelo é uma nota comum dentro da pasta `Modelos`, ou uma nota com nome terminado em `.modelo.md` em qualquer pasta (por exemplo `Projetos/Droerni/Personagem.modelo.md`). Continua sendo Markdown.

        ## Criar um modelo

        Escreva a nota do modelo e use campos entre chaves duplas:

        ```
        # {{titulo}}
        Criado em {{data}}

        Cliente: {{cliente}}
        ```

        - `{{titulo}}` vira o nome da nota nova.
        - `{{data}}` vira a data de hoje.
        - Qualquer outro campo, como `{{cliente}}`, é perguntado depois do nome.
        - Texto entre colchetes, como `[Nome]`, **não** vira pergunta. Ao usar um modelo assim, o Urbe oferece **Converter em campos**, que troca cada `[Texto]` por `{{Texto}}`. Links `[[…]]`, tarefas `- [ ]` e código ficam como estão.

        ## Usar

        No [[Explorer]], **Nova → A partir de um modelo**: escolha o modelo, dê o nome, preencha os campos e a nota nasce pronta. Prefere escrever direto na nota? **Preencher no editor** cria a nota com o que você já respondeu e deixa o resto em branco para completar no editor.
        """;

    private const string Codigo = """
        # Código e prévia ao vivo

        O Urbe abre e edita os arquivos de código do vault como abre as notas: toque num arquivo no Explorer ou na Área de trabalho, e ele abre no editor. O que você escreve é salvo no próprio arquivo, com a mesma verificação das notas: nada é gravado por cima de uma mudança feita fora do Urbe, e um texto que não foi salvo nunca se perde.

        ## Linguagens

        - **Web:** HTML, CSS, SCSS, Less, JavaScript (js, mjs, cjs, jsx), TypeScript (ts, tsx), Vue e Svelte.
        - **Código:** C#, Python, Java, Kotlin, C e C++, Go, Rust, Ruby, PHP, Lua, Swift, Dart, R, SQL, shell (sh, bash, ps1, bat), Gradle e LaTeX.
        - **Dados:** JSON, YAML, TOML, INI, XML, CSV e TSV.

        Os demais arquivos (imagens, PDFs, áudio, vídeo) são abertos no visualizador.

        ## Destaques, marcadores e comentários

        No leitor de PDF, livro ou documento, selecione um trecho e escolha **Destacar**, **Comentar**, **Adicionar seleção à nota** ou **Marcador**. O trecho vai para uma nota de verdade, com o link de volta ao lugar exato da fonte e à versão do arquivo. O destino aparece antes: **esta nota**, **outra nota** (as recentes ou pela busca do nome), **uma nota nova** ou **Copiar com a referência**. Mandar o mesmo trecho de novo para a mesma nota não o duplica.
        - **⋯ → Anotações desta fonte** lista tudo o que as notas guardam sobre o arquivo: tipo, página ou capítulo, o trecho e a nota. Toque para **abrir na nota** (na linha certa) ou **ir à fonte**.
        - Se o arquivo mudou depois da anotação, ela aparece como **fonte mudou: revise**. O Urbe não procura uma frase parecida para colocar o destaque no lugar: você confere.
        - **Exportar anotações** salva um JSON no padrão W3C Web Annotation, com o trecho, o contexto antes e depois e a página. Nem o arquivo nem as notas mudam.

        ## Escrever código

        Num arquivo de código, o editor cuida da indentação, com o passo do próprio arquivo (tab, 2 ou 4 espaços):

        - **Enter** começa a linha nova na mesma indentação, e um nível adiante depois de `{`, `[`, `(` ou de `:` no fim da linha (Python). Entre `{` e `}`, abre o bloco com o `}` na linha de baixo.
        - **Tab** avança um nível as linhas selecionadas (ou põe um nível onde está o cursor); **Shift+Tab** volta um nível.
        - Um `}`, `]` ou `)` digitado numa linha só com espaços volta um nível.

        Funciona com o teclado do celular e com um teclado físico. Notas `.md` continuam com as teclas de Markdown (listas, tarefas).

        O código aparece **em cores**: palavras-chave, textos entre aspas, números, comentários, tags e atributos do HTML, propriedades do CSS e chaves do JSON, YAML e TOML. Enquanto você digita, o texto fica na cor normal; as cores voltam um instante depois da pausa.

        ## Prévia ao vivo de um HTML

        Num arquivo `.html`, o seletor do topo do editor passa a ser **Prévia | Código**. Em **Prévia**, a página aparece ao lado do código (numa tela larga) ou no lugar dele (no celular), e acompanha o que você escreve: cada mudança aparece um instante depois, sem salvar e sem atualizar.

        - A página usa os arquivos do vault a que o HTML se refere: folhas de estilo (`<link rel="stylesheet">`, com os `url()` delas), scripts (`<script src>`), módulos (`import` entre arquivos), imagens, áudio e vídeo. Caminhos relativos partem da pasta do HTML; os que começam com `/` partem da raiz do vault.
        - O que vale é o texto como está no editor, salvo ou não. Um CSS ou JS aberto no Urbe entra como está escrito.
        - `fetch()` e `XMLHttpRequest` de um arquivo de texto da pasta do HTML (um JSON, um CSV) recebem esse arquivo.
        - Endereços da internet (`https://…`) continuam como estão.
        - **Erros do código** (do JavaScript, de uma promessa rejeitada ou de `console.error`) aparecem num quadro vermelho dentro da própria página. O editor e os arquivos não são afetados por eles.

        A página roda isolada: ela não lê nem grava arquivos do vault e não tem acesso ao aplicativo.

        ## No computador

        No computador, **Abrir no navegador** mostra a mesma página no navegador. Ela é servida só para este computador e recarrega sozinha a cada mudança do código, como um Live Server, mesmo com o editor de volta em **Código**. Ela para quando você fecha o arquivo.
        """;

    private const string Paginas = """
        # Publicações

        **Publicações** transforma notas em produtos para outras pessoas: artigo, livro com sumário, apresentação, curso ou vídeo. A publicação não copia o texto: ela aponta para as notas. Editar a nota muda a publicação, e editar pelo estúdio é editar a própria nota.

        ## Criar

        - **Criar publicação desta nota** (no **Mais ações** do editor ou no menu de um cartão), **Criar publicação de um trecho** (só um título da nota e o que vem embaixo dele, ou o parágrafo selecionado) ou, na busca de comandos, **Nova publicação de uma tag** e **Nova publicação de uma busca** (você vê as notas encontradas antes de criar).
        - **Capa:** num artigo ou livro, o botão de livro ao lado de uma nota a torna a capa. Ela vai para o início, fica numa página só dela e sai do sumário; no EPUB, a primeira imagem dela é a capa que a estante mostra. O texto da capa é o da nota: título, subtítulo e autoria você escreve nela.
        - **Nova publicação no estilo de outra** (na busca de comandos) leva o layout, o tema, a cor, a letra, o público e os formatos de uma publicação que já existe; nenhuma nota vem junto.
        - O projeto fica em `Publicações/Nome.publication.json`, com a ordem das notas, o layout e o tema. O texto continua nas notas.

        ## O estúdio

        - **Adicionar nota** põe mais uma nota na composição; as setas mudam a ordem e a lixeira tira a nota da publicação (a nota fica no vault).
        - Tocar no nome de uma nota abre a nota para editar.
        - **Adaptar** (o lápis em cada nota) cria um **resumo, roteiro, fala, tradução ou adaptação** como nota de verdade, ao lado da original, começando com o texto dela. A publicação passa a mostrar a adaptação e a original não muda. Se a original mudar depois, o estúdio avisa, com **Abrir a origem** e **Marcar como revisada**.
        - **Configurar composição**: layout (artigo, livro com sumário, apresentação com uma nota por slide, curso, vídeo com uma nota por cena), tema, objetivo e público.
        - **Desfazer** e **Refazer** valem para a composição. O estúdio salva sozinho.
        - **Verificar publicação** lista o que corrigir antes de exportar, como notas que não existem mais.

        ## Exportar

        Em **⋯** do estúdio: HTML, **PDF**, livro **EPUB**, documento **Word (DOCX)** e **OpenDocument (ODT)**, apresentação **PowerPoint (PPTX)**, **pacote ZIP**, **vídeo MP4** com legendas (SRT e WebVTT) e **Imprimir ou salvar como PDF**. Cada exportação é feita na hora, a partir das notas como estão.

        - As imagens das notas (inclusive `![[foto.png]]`) vão **dentro** do arquivo, que abre sozinho, sem internet. Uma imagem que falta é dita no lugar dela.
        - O **pacote ZIP** leva a publicação, os áudios e vídeos em `media/` e um manifesto que confere cada arquivo.
        - Antes de salvar, o Urbe **abre o arquivo gerado e confere** se ele está inteiro; se não estiver, nada é salvo e você vê o motivo.
        - O que um formato não guarda como no Urbe (uma fórmula que vira texto, um gráfico que vira imagem) é dito ao salvar. Dados que parecem pessoais são mostrados antes de compartilhar, para você decidir.
        - O estúdio diz o que mudou nas notas desde a última exportação.

        ## Vídeo

        - Tocar numa publicação no Explorer abre o estúdio. A prévia começa na primeira cena já formada, e tocar numa cena leva ao momento em que ela está pronta.
        - Tocar num objeto da prévia seleciona ele. Arrastar muda o lugar; **Maior** e **Menor** mudam o tamanho; **Quadro-chave aqui** guarda como ele está neste instante. Para animar, marque um quadro-chave, mude o tempo e mova o objeto: o Urbe faz o caminho entre os dois.
        - **Narrar o roteiro** usa a voz do aparelho para ler o texto de cada cena e põe a fala no começo dela. Se a fala for mais longa que a cena, a cena cresce para caber, e as legendas acompanham a fala.
        - O Assistente que monta um vídeo confere o que fez: mede cada cena (texto cortado, objeto fora do quadro, sobreposição, contraste e legenda cobrindo algo), olha os quadros reais quando o modelo enxerga imagens, e corrige só a cena com problema.
        - **Ajustar áudios do vídeo** muda o volume, a entrada e a saída de cada trilha (a música entra em 1 s e sai em 2 s por padrão).
        - Antes de gerar o MP4, o Urbe confere se há espaço livre suficiente e, se não houver, diz quanto falta e não começa. Durante a exportação ele mostra quanto tempo ainda falta, e **Cancelar** não deixa arquivo pela metade.

        ## Páginas antigas

        Os arquivos `.page.json` do antigo editor de Páginas continuam no vault, sem mudança: dá para vê-los e exportá-los, mas eles não são mais editados. No menu de uma página, **Converter em publicação** leva os textos dela para notas de verdade, aponta para as notas que ela mostrava e diz o que não tem equivalente; a página antiga fica como está.
        """;

    private const string Assistente = """
        # Assistente

        O **Assistente** (no botão redondo do canto inferior direito no celular, no botão flutuante no computador, ou no alto do editor) é um conjunto de agentes que pesquisam, escrevem e organizam a sua pasta, com um modelo de IA que você escolhe.

        ## Conectar um modelo

        Nas configurações do Assistente, **+ Adicionar provedor**: OpenRouter, Anthropic, OpenAI, Google Gemini, Ollama (local) ou um servidor compatível com OpenAI. Cole a chave, toque em **Testar** e escolha o modelo. As chaves ficam salvas só neste aparelho e vão direto para o provedor.

        ## Pedir

        Escreva em **Peça algo ao Assistente…**. Ele lê e busca as notas sozinho. Para mudar algo, ele **propõe**:

        - **Aprovar** grava a mudança; **Aprovar todas** vale para as próximas desta resposta.
        - **Editar** deixa você ajustar o texto proposto antes de aprovar; o que é gravado é a sua versão.
        - **Recusar** descarta.
        - Exclusões pedem **Excluir**, e plugins pedem **Ligar plugin**.
        - Mudar `Personalização/AGENTS.md` (as instruções de todos os agentes) sempre pede a sua aprovação, mesmo com o agente em automático ou depois de **Aprovar todas**.
        - Uma página da internet não manda no agente: o que ela disser é tratado como conteúdo. O Urbe recusa enviar para fora um trecho das suas notas copiado numa pesquisa ou num endereço, e abre normalmente um endereço que você guardou numa nota.
        - Depois de uma mudança, **Desfazer** volta atrás.

        Cada bloco que o Assistente cria ou reescreve (título, parágrafo, item de lista, citação, tabela ou código) ganha um selo pequeno com o nome do modelo, como `{ia:"modelo"}`, que aparece em verde no editor. O Urbe cuida dos selos sozinho:

        - Correções de ortografia, pontuação ou poucas palavras mantêm o autor original. Seu texto corrigido continua sendo seu.
        - Quando a IA reescreve um bloco, o selo antigo é trocado pelo do modelo atual, sem acumular.
        - No modo Visual você edita o texto sem ver o selo, e ele continua no bloco. Apagar o bloco leva o selo junto.
        - No modo Fonte o selo não pode ser apagado ou alterado sozinho.
        - Os agentes não veem os selos e não conseguem removê-los. Se você pedir, eles explicam que a autoria é mantida pelo aplicativo.

        **Enviar arquivos ao chat:** toque no único botão **+** ao lado da caixa de mensagem e escolha **Arquivo do dispositivo** ou **Nota do vault**. A busca de notas percorre o vault inteiro, sem limite artificial das primeiras 300 notas. Antes do envio, os anexos aparecem na bandeja compacta de contexto; cada cartão pode ser removido, e **Limpar** remove todos. Texto UTF-8 entra como contexto; imagens PNG/JPEG/WebP e PDFs vão como anexos multimodais. Uma mensagem leva até 20 anexos, somando até 30 MB, o que os provedores aceitam juntos. Fotos grandes ou deitadas vão de pé e reduzidas (2000 px, JPEG), em vez de serem recusadas. PDFs vão até 20 MB e textos até 384 KB por arquivo, tanto do aparelho quanto do vault. O Urbe não copia os anexos para o vault. Imagens e PDFs exigem um modelo multimodal compatível; só selecione arquivos que deseja enviar ao provedor de IA.

        **Perguntas e planejamento:** em tarefas complexas, o agente registra as etapas pelo plano e, se uma decisão importante estiver faltando, abre um cartão com uma pergunta por vez. Responda no cartão para continuar. O cartão mostra quantas dúvidas já foram esclarecidas, a compreensão que o agente declara, as dúvidas que ainda estão em aberto e o que mudou desde a pergunta anterior (as esclarecidas e as novas). São estimativas declaradas pelo agente, não medições.

        No chat, peça **"gere uma imagem"** sem trocar o modelo da conversa. Em **Configurações do Assistente → Geração de imagens**, selecione o provedor salvo e o modelo de imagem: OpenRouter lista modelos pela API específica de imagens; OpenAI e Gemini também podem ser escolhidos, e é possível informar o ID manual de um modelo compatível. **Automático** mantém a escolha anterior. O Assistente pede aprovação, guarda o arquivo na pasta **Imagens** sem sobrescrever arquivos e, se você quiser, coloca `![[Imagens/…]]` na nota. Sem uma chave válida, ele informa o que falta; não substitui a imagem por uma página.

        **Quanto levou:** embaixo de cada resposta aparece quanto tempo ela levou e onde: o modelo (rede e geração), as ferramentas e a espera pela sua aprovação, além das chamadas, falhas, edições e de quantos arquivos o agente conferiu relendo. Em **Custos e consumo → Desempenho por agente** ficam os números juntos de cada agente (mediana, p90, concluídas, falhas por execução e edições conferidas). São só tempos e contagens, nunca o conteúdo; ficam neste aparelho, em cada conversa, e **Medir execuções** desliga.

        **Preços de imagens:** a lista de modelos mostra valores em dólares **por imagem, por megapixel ou por 1 milhão de tokens**, com fonte consultável. Para OpenRouter, a consulta utiliza o endpoint de preços do modelo escolhido; para modelos específicos OpenAI e Gemini, são exibidas referências públicas com data de verificação. Preço ausente aparece como **não informado**, nunca como gratuito. Os valores são referências ou mínimos, não previsão exata de cobrança: entrada, qualidade, resolução e rota podem alterar a fatura.

        **Despertador, agenda e e-mail:** em Assistente → Conexões → Aparelho você liga, um por um, o que os agentes podem fazer no aparelho. Tudo começa desligado. Com o despertador ligado, peça “me acorde às 6h45 de segunda a sexta” e o relógio do aparelho abre com o alarme pronto; dá para perguntar qual é o próximo despertador. Com a agenda ligada, o agente cria eventos (o app de agenda abre para você salvar) e consulta os da semana; no Android, o sistema pergunta uma vez se o Urbe pode ler a agenda. Com o e-mail ligado, o agente prepara a mensagem no seu app de e-mail e quem envia é você; o Urbe não lê sua caixa de entrada. Cada ação que muda algo pede sua aprovação antes. No computador, o e-mail e o evento abrem nos apps do sistema; despertador e consulta da agenda só existem no celular.

        **Prévia de imagem no chat:** quando `generate_image` termina, a imagem salva no vault aparece diretamente no cartão da conversa, com o botão **Abrir** para a visualização nativa em tamanho maior. Não é uma nova cópia nem uma URL externa. Nas conversas anteriores, imagens concluídas continuam à vista mesmo com os passos do agente recolhidos.

        **De onde veio a imagem:** a imagem gerada guarda dentro do próprio arquivo (PNG ou JPEG) que foi feita por IA, com o modelo, a descrição usada e a data, no padrão de metadados IPTC que outros programas também leem. Ao abrir a imagem, o Urbe mostra “gerada por IA” e o modelo; a descrição vira o texto alternativo. Isso vai junto quando você move a imagem ou a usa numa publicação.

        ## Skills

        Uma **skill** ensina o Assistente a fazer um tipo de trabalho do seu jeito (o formato aberto Agent Skills). Crie uma pasta em `Personalização/Skills/` com um arquivo `SKILL.md`:

        ```
        ---
        name: revisar-livro
        description: Revisar capítulos com o meu checklist editorial.
        ---
        # Revisão
        Leia checklist.md e aponte cada problema com a frase.
        ```

        - O Assistente vê só o **nome e a descrição** de cada skill e carrega as instruções quando o pedido combina com ela; arquivos da pasta (como `checklist.md`) são lidos quando a skill manda.
        - O `name` usa letras minúsculas, números e hífens, igual ao nome da pasta. Uma skill com problema no cabeçalho não aparece.
        - Uma skill **não dá permissões** e **nada nela é executado**: scripts são só texto. As aprovações continuam as mesmas.
        - O Urbe já traz skills de apresentação, livro e cálculo; uma sua com o mesmo nome toma o lugar da do app.

        ## Ajustar

        - **Instruções para todos os agentes**, **Memória** (fatos para lembrar), **Agentes personalizados** e **Limite de passos por resposta**.
        - **Modelo por agente:** em Configurações → Nomes dos agentes → **Configurar**, você pode selecionar um provedor/modelo de conversa específico para qualquer agente (inclusive os prontos). Sem escolha, o agente herda o modelo normal do Assistente. Trocar de agente passa a trocar também o modelo efetivo, sem mudar o padrão salvo.
        - **Instruções por blocos:** no mesmo painel, adicione quantos blocos titulados precisar (ex.: Tom, Fontes, Procedimento), edite e remova. Esses blocos se somam às instruções gerais e às instruções existentes do agente, sem ampliar as permissões. Os ajustes são guardados só na configuração local do Assistente.
        - **A conversa continua**: ao voltar ao Assistente ou a um agente pouco depois (até duas horas) ou com algo pendente (uma mensagem esperando, um formulário não enviado), a conversa em andamento reaparece. Parada há mais tempo, começa uma nova, e a anterior fica em **Conversas**. **Nova conversa** recomeça na hora.
        - **Fontes de uma pesquisa**: o painel de fontes diz se o agente leu a página inteira, só uma parte (**Leitura parcial**) ou só viu o resultado da busca.
        - **Exportar conversa ou memória**: uma chave sua que tenha ido parar no chat sai como “[chave removida]”.
        - **Conversas anteriores**: cada agente consulta as próprias conversas deste vault (o pedido, a resposta, os arquivos alterados e, se precisar, a conversa inteira), por exemplo quando você diz "continue aquilo de ontem". Ele nunca vê as conversas de outro agente nem o que ficou oculto.
        - **Permissões do agente** (somente leitura, com aprovação, automático): mudar para uma mais restrita enquanto ele trabalha já vale a partir da próxima ação; mudar para uma mais livre vale a partir da próxima resposta. O Urbe avisa qual dos dois aconteceu.
        - **Consultar um especialista**: um agente pode pedir ajuda a outro agente seu, que trabalha na própria conversa, com o próprio modelo e permissões. Se o especialista precisar perguntar algo a você, pedir um formulário ou uma aprovação, o pedido aparece na conversa em que você está; a resposta vai para ele, e o trabalho continua sem trocar de conversa. Numa consulta, o especialista nunca tem menos aprovações que o agente que pediu: se ele está em automático e o que pediu não, as mudanças dele pedem a sua aprovação na conversa que pediu.
        - **Nomes dos agentes**: cada agente pode ter um nome seu, como Jarvis. Numa conversa, chame-o por um nome novo ("Jarvis, resuma esta nota") e ele pergunta se deve passar a se chamar assim. O nome fica só neste aparelho.
        - **Pedir dentro da nota**: escreva numa linha `&Jarvis&: dê uma sugestão de nome` (ou `&&:` para o agente padrão). A linha vira um cartão com **Pedir a Jarvis** (ou Ctrl+Enter nela); só ao tocar o pedido vai, numa conversa própria "Na nota …", com a nota como contexto. Uma mudança na nota aparece no lugar da linha, como sugestão, com **Aceitar** e **Recusar**; uma resposta em palavras abre o Assistente, com **Pôr na nota** para guardá-la como citação logo abaixo da linha. Essas linhas não aparecem nas páginas.
        - **Personalização/AGENTS.md** na sua pasta: instruções suas, lidas a cada conversa. Separe por modelo:

        ```
        Geral: { escreva em português do Brasil }
        Claude 5: { seja breve; cite as notas }
        ```

        A parte **Geral** vale sempre; uma parte com nome vale quando todas as palavras do nome estão no modelo em uso. Títulos `## Claude 5` também funcionam.

        **Quando instruções divergem**, vale a mais específica: o que você pede agora; depois o papel e os blocos do agente; depois o `AGENTS.md` (a parte do modelo antes da Geral); por último as orientações do Urbe. Segurança, permissões, aprovações e selos de autoria valem sempre, mesmo que uma instrução peça o contrário.

        ## Servidores MCP

        Em **Configurações → Pesquisa → Servidores MCP**, adicione um servidor pelo endereço (e a chave, se ele pedir). O Urbe mostra as ferramentas que o servidor oferece; cada uma começa **desligada**. No computador também dá para adicionar um **servidor local** (um programa, com aviso antes). Libere cada ferramenta como **Leitura** (o Assistente usa sem perguntar, como uma pesquisa na web) ou **Escrita** (pergunta antes de cada uso). O que o servidor responde é tratado como dado de terceiros, não como ordem.
        """;

    private const string Plugins = """
        # Plugins

        Plugins acrescentam ferramentas ao Urbe. Eles ficam em **Personalização → Plugins**.

        ## Relógio

        O plugin nativo **Relógio** mostra **Agora**, um **Cronômetro** e um **Temporizador** (com tempos prontos) num painel sobre as notas e o editor. Ligado, ele ganha um botão no alto do Explorer e na barra do editor.

        ## Plugins da sua pasta

        - Plugins `.plugin.json` ficam em `Personalização/plugins`. **Novo plugin** cria um a partir de um modelo; o Assistente também cria plugins.
        - Antes de **Ligar**, o Urbe mostra o que o plugin pode fazer. Ele só faz o que declara.
        - Se o arquivo mudar, o plugin desliga até você revisar (**Revisei, ligar**).
        - Os plugins `.js` da versão 1.8.4 não rodam no app nativo; aqui você só os vê e aprova.
        - **Modo seguro** desliga todos os plugins e estilos sem apagar nada.
        - Com a permissão **agentes**, um plugin pode pedir trabalho a um dos seus agentes, só nas notas ou pastas que indicar. A cada pedido você vê o plugin, o agente, o pedido e onde ele pode gravar, e confirma; fora desse lugar o agente não grava nada.

        Plugins são programas: só ligue o que você entende ou que veio de alguém de confiança.
        """;

    private const string Personalizacao = """
        # Personalização

        **Configurações → Personalização** muda a aparência do Urbe. As escolhas ficam em `Personalização/tema.json`, na sua pasta.

        - **Aparência:** tema (escuro, meia-noite, floresta, oceano, vinho, claro, sépia, alto contraste), cor de destaque, cores, cantos, densidade e animações.
        - **Texto:** fonte e tamanho da interface e das notas, altura da linha.
        - **Editor:** largura do texto e o modo em que as notas abrem (Visual ou Texto).
        - **Estilos CSS:** guardados no tema; o app nativo não usa CSS, eles valem na versão web.
        - **Tags:** a cor de cada `#Tag:` e, se quiser, um ícone antes do nome (✅, 💡, ⚠️…; toque no escolhido de novo para tirar). Veja [[Editor]]. Fica em `Personalização/tags.json`.
        - **Plugins:** veja [[Plugins]].
        - **Arquivo:** o `tema.json` para editar à mão, e **Restaurar padrão**.

        Na pasta Personalização também fica o `AGENTS.md` do [[Assistente]].
        """;

    private const string Backups = """
        # Backups, exportar e restaurar

        Suas notas são arquivos comuns na pasta que você escolheu. O Urbe confere cada gravação e guarda cópias antes de mudanças grandes.

        ## Exportar

        **Configurações → Exportar tudo (.zip)** faz uma cópia de segurança de todas as notas. Guarde o `.zip` em outro lugar.

        ## Importar e restaurar

        **Configurações → Importar arquivos** (ou **⋯ → Importar arquivos** no Explorer) traz **arquivos** ou uma **pasta inteira**: notas, pastas ou `.zip`. Imagens, PDFs, áudios e vídeos vêm junto com as notas (um arquivo que já existe na pasta fica como está). O Urbe confere os arquivos antes. Se o `.zip` for um export ou backup do Urbe, ele oferece restaurar; o que for substituído é guardado antes em backup.

        ## Backups automáticos

        Antes de migrar ou importar, o Urbe guarda um backup em `.urbe/backup`. Para ver e restaurar: **Ctrl+K → Backups do vault**.

        ## Quando a pasta muda fora do Urbe

        Outro aplicativo ou uma sincronização pode mudar os arquivos enquanto o Urbe está aberto. O Urbe nunca grava por cima do que mudou lá: ao salvar, ele lê a pasta de novo, e as mudanças de fora aparecem. Se a própria nota que você está escrevendo foi mudada lá fora, as duas versões ficam: a de fora continua na nota, e a sua é salva ao lado como **Nome (conflito data hora)**, que segue aberta no editor para você juntar o que quiser.

        ## Texto não salvo

        O Urbe salva sozinho logo depois de uma pausa na escrita. Se o app for fechado de repente antes disso (o sistema encerrou o app, a bateria acabou), o que você digitou fica guardado à parte, só neste aparelho. Ao abrir a nota de novo, o Urbe pergunta: **Recuperar** põe o texto de volta (e dá para desfazer), **Descartar** fica com a nota como estava. Ele avisa se a nota também mudou nesse meio-tempo.

        ## Versões e lixeira

        - **Versões anteriores**, no **Mais ações** do [[Editor]], restaura um texto anterior da nota.
        - A **Lixeira** do [[Explorer]] devolve o que foi excluído.
        """;

    private const string Atalhos = """
        # Atalhos

        No computador (Ctrl; Cmd no Mac):

        | Atalho | O que faz |
        | --- | --- |
        | Ctrl+K | Abrir comandos |
        | Ctrl+P | Buscar notas |
        | Ctrl+N | Nova nota |
        | Ctrl+S | Salvar |
        | Ctrl+F | Localizar e substituir |
        | Ctrl+W | Fechar aba |
        | Alt+← / Alt+→ | Voltar / Avançar |
        | Ctrl+Shift+F | Abrir o Explorer |
        | Ctrl+M | Fórmula no texto |
        | Ctrl+Shift+M (no editor) | Fórmula em destaque |
        | Esc | Fechar a busca ou terminar de editar o bloco |

        **Abrir comandos** (Ctrl+K) lista tudo o que o Urbe faz: backups, painel dividido, restaurar versão, plugins e mais.

        **Seus atalhos:** Configurações → Atalhos lista todos e **Editar meus atalhos** abre `Personalização/atalhos.json`, por exemplo `"Mod+Shift+K": "ui.constellation"` (Mod é Ctrl). Um atalho seu vale por cima do padrão; `""` desliga um padrão. Os nomes dos comandos (como `ui.workspace`, `ui.sidebar.toggle`) aparecem no próprio arquivo de exemplo.

        **A barra de formatação, pelo toque:** em **Personalização → Editor → Barra de formatação**, cada botão tem ↑ e ↓ para mudar de lugar e × para sair da barra; os que estão fora aparecem com +, e **Separador** cria um grupo. **Padrão** volta à barra original. Desfazer e Refazer ficam sempre no começo. A escolha vai para o mesmo `Personalização/editor.json`, sem mexer no menu `/`.

        **A barra do editor e o menu /:** Configurações → **Barra do editor e menu /** abre `Personalização/editor.json`, com a lista `barra` (os botões, na ordem; `|` separa) e a lista `menu` (os itens do `/`). Apague o que não usa, troque a ordem e salve.

        No celular: toque para abrir e segure para selecionar ou arrastar. Se um botão não responde, **Configurações → Diagnóstico de toques** ajuda a descobrir o porquê.
        """;
}
