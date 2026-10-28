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
        new("Tutorial/Ferramentas/Páginas.md", Paginas),
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
        - [[Atalhos]]: os atalhos de teclado.

        Na primeira vez, a **Configuração inicial** leva pela pasta, pelo tema e pelo Assistente; ela pode ser refeita em **Configurações → Configuração inicial**.

        As **Propriedades** da nota (o bloco `---` do começo) têm um formulário no alto da nota, em Visual e em Fonte: toque em **Propriedades** para abrir. **Aliases** são outros nomes da nota: um link `[[apelido]]` leva a ela, aparece nas conexões e funciona em `![[apelido]]`. **Tags** aceitam as do vault com um toque. Texto, número, data, sim/não e listas têm campo próprio, e **Adicionar propriedade** cria outras. O que o formulário não entende fica como estava.

        O **modo leitura** (o botão **Edição/Leitura** no rodapé do editor, ou **Mais ações → Modo leitura**) vale para Visual e Fonte: nada é alterado sem querer, a barra de formatação some e as propriedades só são mostradas. Links, comentários e cartões continuam funcionando.

        Para atualizar: **Configurações → Verificar atualização** procura uma versão mais nova do Urbe (só quando você pede). O arquivo abre no navegador: no Android, instale por cima do Urbe atual; no Windows, extraia o .zip por cima da pasta do Urbe. Suas notas ficam onde estão.

        Use a **busca** no alto do índice deste tutorial para achar qualquer assunto, e **Anterior / Próximo** para ler em sequência.
        """;

    private const string Explorer = """
        # Explorer

        O **Explorer** mostra a pasta como uma árvore de arquivos e pastas. No computador ele fica ao lado da Área de trabalho; no celular, abre pelo botão **Pastas** e fecha ao escolher uma nota.

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
        - **Novo grafo**: um canvas livre (`Nome.grafo.json`) com cartões, notas e arquivos do vault, ligações (linha ou seta), formas, grupos e desenhos, com desfazer e refazer. **⋯ → Grafos** lista os grafos e gera a estrutura do vault (pastas, arquivos, notas e links). O Assistente também lê, cria e altera grafos (por exemplo, "faça um grafo da família real com as notas dos personagens"): a mudança aparece em texto para você aprovar.

        ## Selecionar e mover

        - **Segure** uma nota ou pasta para selecionar. A barra embaixo oferece **Abrir**, **Favoritar**, **Mover para pasta**, **Duplicar**, **Agrupar em nova pasta** e **Mover para a lixeira**.
        - **Segure e arraste** sobre uma pasta para mover para ela. O arquivo muda de pasta no vault.

        ## Mais opções

        O botão **⋯** do Explorer: **Buscar notas e comandos**, **Recentes**, **Favoritos**, **Importar arquivos**, **Exportar tudo (.zip)**, **Lixeira** e **Pasta do Urbe no dispositivo**.

        ## Lixeira

        O que você exclui vai para a lixeira: **Restaurar** devolve a nota, **Excluir de vez** apaga e **Esvaziar** limpa tudo.
        """;

    private const string Editor = """
        # Editor

        O editor abre por cima da Área de trabalho e do Explorer. Ele salva sozinho e confere a gravação no arquivo; o rodapé mostra o estado.

        ## Visual e Fonte

        - **Visual** mostra a nota formatada; toque num trecho para editar aquele bloco.
        - **Fonte** mostra o Markdown puro.
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

        ## Incorporar

        `![[Nome]]` mostra no lugar o conteúdo, num cartão: a nota (`![[Plano]]`), uma imagem (`![[Imagens/capa.png]]`), PDF, áudio, vídeo, HTML, código ou texto, e um grafo (`![[Mapa.grafo.json]]`), que dá para navegar. Tocar no alto do cartão abre o original; tocar no corpo edita a linha.

        Para mostrar só uma parte da nota: `![[Plano#Orçamento]]` traz a seção desse título (até o próximo título do mesmo nível; `#Título#Subtítulo` desce mais um nível), `![[Plano#^meta]]` traz o parágrafo ou o item de lista que termina com ` ^meta`, e `![[Plano#L3-L8]]` traz as linhas 3 a 8. Os marcadores ` ^meta` não aparecem nos cartões. Um nome que não existe mostra “Não encontrei … no vault”.

        ## Fórmulas

        Escreva LaTeX entre `$…$` no texto ou `$$…$$` em destaque, por exemplo $E = mc^2$. O botão **∑** (ou **Ctrl+M**) abre o editor de fórmulas, com **No texto** / **Destaque**, prévia, símbolos e modelos. Toque numa fórmula pronta para editar.

        ## Marcas do Urbe

        São texto comum no arquivo, então a nota continua legível em qualquer editor:

        ```
        %%um comentário seu%%
        palavra{"comentário sobre a palavra"}
        #Ideia: um parágrafo com o estilo da tag
        ```

        - `%%…%%` aparece separado no editor e fica fora das páginas e do HTML exportado.
        - `palavra{"…"}` mostra um 💬 sobre a palavra; toque nele para ler e escrever comentários.
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

        Páginas têm modelos próprios, no estúdio (veja [[Páginas]]).
        """;

    private const string Paginas = """
        # Páginas

        **Páginas** transforma notas em sites e páginas HTML: portfólios, currículos, livros… Abra em **Configurações → Páginas**.

        ## O estúdio

        - **Nova página** cria uma e abre o estúdio.
        - A página é feita de **blocos**: toque em **Adicionar bloco**, arraste para reordenar e use o menu de cada bloco para **Mover para cima / baixo**, **Duplicar**, **Salvar como bloco reutilizável**, **Trocar tipo de bloco** ou **Remover**.
        - Em **＋ → Modelos** há modelos prontos; aplicar um pode **substituir a página**, **adicionar as seções ao fim** ou trocar **só o visual**.
        - O estúdio salva sozinho e mostra **Salvo**.
        - **Criar página daqui**, no ⋯ de uma pasta, de um cartão de nota ou no **Mais ações** do editor: de uma pasta, uma capa e as notas dela; de uma nota, a própria nota.
        - Na prévia, títulos, rótulos e itens de uma linha se escrevem **no lugar**: toque, escreva e saia (Enter também termina). Tocar numa imagem abre a escolha de imagem. Textos com Markdown continuam no painel **Editar**.
        - O bloco **Notas** lista notas do seu vault e acompanha as mudanças: de uma **pasta** (com ou sem as subpastas), de uma **tag**, as **escolhidas**, as **recentes**, as que **ligam a uma nota**, as **ligadas por uma nota** ou as de uma **busca**.

        ## Prévia e comentários

        **Ver página** mostra a página dentro do Urbe, como ela será publicada. Os comentários das notas (`%%…%%` e `{"…"}`) não aparecem na página. Na prévia, o 💬 de uma palavra permite **comentar**, e o comentário é salvo no texto.

        ## Exportar

        No botão **Exportar** ou no **⋯** da página:

        - **Imprimir ou salvar PDF**
        - **Baixar HTML**: um arquivo único, que funciona em qualquer hospedagem
        - **Salvar HTML no vault**, ao lado da página
        - **Copiar HTML**

        No menu do estúdio também estão **Salvar como modelo** (com os textos ou só a estrutura), **Duplicar página** e **Editar JSON**. O arquivo da página é um `.page.json` na sua pasta.
        """;

    private const string Assistente = """
        # Assistente

        O **Assistente** (embaixo, ou no alto do editor) é um conjunto de agentes que pesquisam, escrevem e organizam a sua pasta, com um modelo de IA que você escolhe.

        ## Conectar um modelo

        Nas configurações do Assistente, **+ Adicionar provedor**: OpenRouter, Anthropic, OpenAI, Google Gemini, Ollama (local) ou um servidor compatível com OpenAI. Cole a chave, toque em **Testar** e escolha o modelo. As chaves ficam salvas só neste aparelho e vão direto para o provedor.

        ### IA no aparelho (sem internet)

        Em **Assistente → Configurações → IA no aparelho** você baixa uma vez um modelo (Gemma 4: o menor, ~3,3 GB, ou o maior, ~5,2 GB, que o Urbe recomenda conforme a memória do aparelho). Depois disso ele responde **sem internet e sem chave**, lendo e escrevendo as notas com as mesmas permissões. O download só começa quando você toca em **Baixar**, pode ser pausado e continuado, e o arquivo é conferido antes de ser usado; ele fica fora da sua pasta de notas. No celular, a primeira resposta demora mais (ele lê o contexto); as seguintes são mais rápidas. Imagens offline ainda não existem.

        ## Pedir

        Escreva em **Peça algo ao Assistente…**. Ele lê e busca as notas sozinho. Para mudar algo, ele **propõe**:

        - **Aprovar** grava a mudança; **Aprovar todas** vale para as próximas desta resposta.
        - **Editar** deixa você ajustar o texto proposto antes de aprovar; o que é gravado é a sua versão.
        - **Recusar** descarta.
        - Exclusões pedem **Excluir**, e plugins pedem **Ligar plugin**.
        - Depois de uma mudança, **Desfazer** volta atrás.

        Cada bloco que o Assistente cria ou reescreve (título, parágrafo, item de lista, citação, tabela ou código) ganha um selo pequeno com o nome do modelo, como `{ia:"modelo"}`, que aparece em verde no editor. O Urbe cuida dos selos sozinho:

        - Correções de ortografia, pontuação ou poucas palavras mantêm o autor original. Seu texto corrigido continua sendo seu.
        - Quando a IA reescreve um bloco, o selo antigo é trocado pelo do modelo atual, sem acumular.
        - No modo Visual você edita o texto sem ver o selo, e ele continua no bloco. Apagar o bloco leva o selo junto.
        - No modo Fonte o selo não pode ser apagado ou alterado sozinho.
        - Os agentes não veem os selos e não conseguem removê-los. Se você pedir, eles explicam que a autoria é mantida pelo aplicativo.

        **Enviar arquivos ao chat:** toque no único botão **+** ao lado da caixa de mensagem e escolha **Arquivo do dispositivo** ou **Nota do vault**. A busca de notas percorre o vault inteiro, sem limite artificial das primeiras 300 notas. Antes do envio, os anexos aparecem na bandeja compacta de contexto; cada cartão pode ser removido, e **Limpar** remove todos. Texto UTF-8 entra como contexto; imagens PNG/JPEG/WebP e PDFs vão como anexos multimodais. Até quatro anexos por mensagem, com limites por tipo. O Urbe não copia os anexos para o vault. Imagens e PDFs exigem um modelo multimodal compatível; só selecione arquivos que deseja enviar ao provedor de IA.

        **Perguntas e planejamento:** em tarefas complexas, o agente registra as etapas pelo plano e, se uma decisão importante estiver faltando, abre um cartão com uma pergunta por vez. Responda no cartão para continuar. O cartão mostra quantas dúvidas já foram esclarecidas, a compreensão que o agente declara, as dúvidas que ainda estão em aberto e o que mudou desde a pergunta anterior (as esclarecidas e as novas). São estimativas declaradas pelo agente, não medições.

        No chat, peça **"gere uma imagem"** sem trocar o modelo da conversa. Em **Configurações do Assistente → Geração de imagens**, selecione o provedor salvo e o modelo de imagem: OpenRouter lista modelos pela API específica de imagens; OpenAI e Gemini também podem ser escolhidos, e é possível informar o ID manual de um modelo compatível. **Automático** mantém a escolha anterior. O Assistente pede aprovação, guarda o arquivo na pasta **Imagens** sem sobrescrever arquivos e, se você quiser, coloca `![[Imagens/…]]` na nota. Sem uma chave válida, ele informa o que falta; não substitui a imagem por uma página.

        **Preços de imagens:** a lista de modelos mostra valores em dólares **por imagem, por megapixel ou por 1 milhão de tokens**, com fonte consultável. Para OpenRouter, a consulta utiliza o endpoint de preços do modelo escolhido; para modelos específicos OpenAI e Gemini, são exibidas referências públicas com data de verificação. Preço ausente aparece como **não informado**, nunca como gratuito. Os valores são referências ou mínimos, não previsão exata de cobrança: entrada, qualidade, resolução e rota podem alterar a fatura.

        **Prévia de imagem no chat:** quando `generate_image` termina, a imagem salva no vault aparece diretamente no cartão da conversa, com o botão **Abrir** para a visualização nativa em tamanho maior. Não é uma nova cópia nem uma URL externa. Nas conversas anteriores, imagens concluídas continuam à vista mesmo com os passos do agente recolhidos.

        ## Ajustar

        - **Instruções para todos os agentes**, **Memória** (fatos para lembrar), **Agentes personalizados** e **Limite de passos por resposta**.
        - **Modelo por agente:** em Configurações → Nomes dos agentes → **Configurar**, você pode selecionar um provedor/modelo de conversa específico para qualquer agente (inclusive os prontos). Sem escolha, o agente herda o modelo normal do Assistente. Trocar de agente passa a trocar também o modelo efetivo, sem mudar o padrão salvo.
        - **Instruções por blocos:** no mesmo painel, adicione quantos blocos titulados precisar (ex.: Tom, Fontes, Procedimento), edite e remova. Esses blocos se somam às instruções gerais e às instruções existentes do agente, sem ampliar as permissões. Os ajustes são guardados só na configuração local do Assistente.
        - **Conversas anteriores**: cada agente consulta as próprias conversas deste vault (o pedido, a resposta, os arquivos alterados e, se precisar, a conversa inteira), por exemplo quando você diz "continue aquilo de ontem". Ele nunca vê as conversas de outro agente nem o que ficou oculto.
        - **Nomes dos agentes**: cada agente pode ter um nome seu, como Jarvis. Numa conversa, chame-o por um nome novo ("Jarvis, resuma esta nota") e ele pergunta se deve passar a se chamar assim. O nome fica só neste aparelho.
        - **Pedir dentro da nota**: escreva numa linha `&Jarvis&: dê uma sugestão de nome` (ou `&&:` para o agente padrão). A linha vira um cartão com **Pedir a Jarvis** (ou Ctrl+Enter nela); só ao tocar o pedido vai, numa conversa própria "Na nota …", com a nota como contexto. Uma mudança na nota aparece no lugar da linha, como sugestão, com **Aceitar** e **Recusar**; uma resposta em palavras abre o Assistente, com **Pôr na nota** para guardá-la como citação logo abaixo da linha. Essas linhas não aparecem nas páginas.
        - **Personalização/AGENTS.md** na sua pasta: instruções suas, lidas a cada conversa. Separe por modelo:

        ```
        Geral: { escreva em português do Brasil }
        Claude 5: { seja breve; cite as notas }
        ```

        A parte **Geral** vale sempre; uma parte com nome vale quando todas as palavras do nome estão no modelo em uso. Títulos `## Claude 5` também funcionam.
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

        Plugins são programas: só ligue o que você entende ou que veio de alguém de confiança.
        """;

    private const string Personalizacao = """
        # Personalização

        **Configurações → Personalização** muda a aparência do Urbe. As escolhas ficam em `Personalização/tema.json`, na sua pasta.

        - **Aparência:** tema (escuro, meia-noite, floresta, oceano, vinho, claro, sépia, alto contraste), cor de destaque, cores, cantos, densidade e animações.
        - **Texto:** fonte e tamanho da interface e das notas, altura da linha.
        - **Editor:** largura do texto e o modo em que as notas abrem (Visual ou Texto).
        - **Estilos CSS:** guardados no tema; o app nativo não usa CSS, eles valem na versão web.
        - **Tags:** a cor de cada `#Tag:` (veja [[Editor]]). Fica em `Personalização/tags.json`.
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

        **A barra do editor e o menu /:** Configurações → **Barra do editor e menu /** abre `Personalização/editor.json`, com a lista `barra` (os botões, na ordem; `|` separa) e a lista `menu` (os itens do `/`). Apague o que não usa, troque a ordem e salve.

        No celular: toque para abrir e segure para selecionar ou arrastar. Se um botão não responde, **Configurações → Diagnóstico de toques** ajuda a descobrir o porquê.
        """;
}
