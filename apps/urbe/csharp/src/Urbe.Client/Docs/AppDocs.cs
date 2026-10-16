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
        new("Tutorial/Cidade/A cidade.md", ACidade),
        new("Tutorial/Cidade/Construir o mundo.md", ConstruirOMundo),
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

    private const string ComeceAqui = """
        # Comece aqui

        Bem-vindo ao **Urbe**. Aqui suas notas viram uma cidade: cada nota é uma **casa**, cada pasta é um **bairro** e cada `[[link]]` entre notas abre uma **rua**. Quanto mais você escreve e liga ideias, mais a cidade cresce e ganha vida.

        ## O primeiro passo: a pasta

        O Urbe guarda tudo em arquivos `.md` comuns, numa pasta do aparelho que você escolhe. Abra **Configurações** (a engrenagem no alto) → **Pasta no dispositivo** e escolha uma pasta, vazia ou com notas que você já tem. Sem uma pasta aberta, nada é gravado.

        ## A tela

        - **A cidade** ocupa a tela toda. Arraste para andar, faça pinça (ou use a roda do mouse) para aproximar.
        - **No alto:** o nome da sua pasta, com um ponto que mostra se está tudo salvo (verde), gravando (amarelo) ou com erro (vermelho); depois **Buscar notas**, **Mapa da cidade** e **Configurações**.
        - **Embaixo:** **Explorer** (arquivos e pastas), **Cidade**, **Assistente** e o botão **+**, que constrói no mundo.

        ## Por onde seguir

        - [[A cidade]]: o que você vê e como a vida acontece.
        - [[Construir o mundo]]: construções, regiões e decorações.
        - [[Explorer]] e [[Editor]]: escrever e organizar.
        - [[Atalhos]]: os atalhos de teclado.

        Use a **busca** no alto do índice deste tutorial para achar qualquer assunto, e **Anterior / Próximo** para ler em sequência.
        """;

    private const string ACidade = """
        # A cidade

        A cidade é o seu vault desenhado num mundo procedural, com terreno, rios, florestas e montanhas.

        ## Casas, bairros e ruas

        - **Casas** são notas e outros arquivos. O tipo do arquivo define a construção: uma nota `.md` é uma casa; `.html`, `.js`, `.css`, `.json`, `.csv` e `.txt` têm construções próprias.
        - **Bairros** são pastas. Subpastas viram bairros dentro de bairros.
        - **Ruas** ligam notas que têm `[[links]]` entre si. Escreva um link e a rua aparece.

        ## Andar e olhar

        - **Arraste** para andar e faça **pinça** ou use a **roda do mouse** para o zoom.
        - **Mapa da cidade** (no alto) mostra o mundo inteiro, com os bairros e a área que você está vendo.
        - **Buscar notas** encontra uma nota, leva a câmera até a casa dela e abre a nota.
        - **Configurações → Organizar os bairros** redesenha pastas e subpastas e reagrupa as casas. Dá para voltar: **Ctrl+K → Desfazer reorganização**.

        ## Tocar no mundo

        - **Tocar numa casa** abre o cartão do arquivo: **Abrir e editar**, **Copiar**, **Visual** (o visual da construção), **Mover** (para outra pasta) e **Excluir** (vai para a lixeira).
        - **Segurar uma casa** e arrastar leva a casa para outro lugar. Se ela mudar de bairro, o arquivo muda de pasta junto.
        - **Tocar num bairro** mostra o contorno com alças; arraste-as e toque em **Aplicar** para mudar o formato.
        - **Segurar um bairro** abre nome, cor e descrição da pasta.
        - **Tocar numa decoração** abre as opções dela (veja [[Construir o mundo]]).

        ## A vida

        - **Moradores** andam pelas ruas entre notas ligadas e às vezes param para conversar.
        - **Ofícios:** perto de um **poço**, de uma **roça** ou de uma **pilha de lenha**, alguém da casa mais próxima sai para buscar água, cuidar da roça ou cortar lenha, e depois volta para casa.
        - **Crianças** brincam perto da porta de algumas casas, correm e voltam.
        - **Animais**, **clima** (nuvens, vento, chuva, neve, neblina) e **eventos** (balão, festa, barcos, raposa, revoadas, estrelas cadentes).
        - **Relógio:** a luz segue um ciclo de dia e noite. Em **Personalização → Cidade → Luz** você escolhe Ciclo, Dia, Tarde, Noite ou Auto (o relógio do aparelho).

        Tudo isso pode ser ligado ou desligado em **Personalização → Cidade → Vida na cidade**.
        """;

    private const string ConstruirOMundo = """
        # Construir o mundo

        O botão **+** da cidade constrói direto no mapa. Tudo vai para a sua pasta; sem uma pasta aberta, o Urbe avisa e não constrói.

        ## Construção

        Um arquivo onde você tocar no mapa.

        1. **+ → Construção** e escolha o tipo: Nota `.md`, Texto `.txt`, Página `.html`, Script `.js`, Estilo `.css`, Dados `.json` ou Tabela `.csv`. O tipo define a construção.
        2. Toque no mapa para posicionar e confirme.
        3. Dê um nome e toque em **Construir**.

        ## Região

        Uma pasta desenhada no mapa. **+ → Região**, **arraste** no mapa para delimitar a área e dê o nome. Casas soltas que ficarem dentro dela entram na pasta.

        ## Decoração

        **+ → Decoração** e escolha: poço, roça, lenha, banco, fonte, canteiros, arbusto, lampião, cerca, barris, caixotes, carroça, placa, varal, árvore frutífera, pedras, espantalho…

        - Toque no mapa para posicionar e em **Colocar**.
        - **Pincel:** arraste para pintar uma decoração em cada quadrado do caminho (bom para cercas e canteiros). Toque de novo para voltar a um toque por vez.
        - **Tocar numa decoração** colocada: **Mover**, **Variação** (cada uma tem as suas, como telhado de palha, trigo ou abóboras), **Girar**, **Espelhar**, **Colocar mais desta** e **Remover decoração**.
        - A última variação escolhida vale para as próximas do mesmo tipo.

        Poço, roça e lenha também dão **ofícios** aos moradores (veja [[A cidade]]). As decorações ficam em `.urbe/mundo.json`.

        ## Importar

        **+ → Importar** traz **arquivos** ou uma **pasta inteira** do aparelho (notas, pastas ou `.zip`). Veja [[Backups, exportar e restaurar]].

        ## Nota rápida

        Quando o aparelho permite, **+ → Nota rápida** escuta o que você fala e escreve na nota do dia, em `Notas rápidas`. No Android também há o widget **Urbe: nota rápida** na tela inicial.
        """;

    private const string Explorer = """
        # Explorer

        O **Explorer** (embaixo, à esquerda) mostra a pasta como arquivos e pastas. Em telas largas ele fica num painel ao lado da cidade.

        ## Navegar

        - **Buscar arquivos** filtra por nome e caminho.
        - Toque numa **pasta** para abrir ou fechar; toque numa **nota** para abrir no [[Editor]].
        - Outros arquivos (imagens, PDF…) abrem para visualizar.
        - O **⋯** de uma pasta: **Nova nota aqui**, **Nova subpasta**, **Editar nome, cor e descrição**, **Mostrar na cidade**, **Mover para** e **Mover para a lixeira**.

        ## Criar

        **Nova** (no alto do Explorer):

        - **Nova nota**, na pasta selecionada.
        - **A partir de um modelo**, quando há notas em `Modelos` (veja [[Modelos]]).
        - **Nova pasta**, que vira um bairro na cidade.
        - **Nota rápida**, por voz, quando disponível.

        ## Selecionar e mover

        - **Segure** uma nota ou pasta para selecionar. A barra embaixo oferece **Abrir**, **Favoritar**, **Mover para pasta**, **Duplicar**, **Agrupar em nova pasta** e **Mover para a lixeira**.
        - **Segure e arraste** sobre uma pasta para mover para ela. A casa muda de bairro junto.

        ## Mais opções

        O botão **⋯** do Explorer: **Recentes**, **Favoritos**, **Importar arquivos**, **Exportar tudo (.zip)**, **Lixeira** e **Pasta do Urbe no dispositivo**.

        ## Lixeira

        O que você exclui vai para a lixeira: **Restaurar** devolve a nota, **Excluir de vez** apaga e **Esvaziar** limpa tudo.
        """;

    private const string Editor = """
        # Editor

        O editor abre por cima da cidade. Ele salva sozinho e confere a gravação no arquivo; o rodapé mostra o estado.

        ## Visual e Fonte

        - **Visual** mostra a nota formatada; toque num trecho para editar aquele bloco.
        - **Fonte** mostra o Markdown puro.
        - O modo inicial fica em **Personalização → Editor**.

        ## A barra

        Título 1, 2 e 3, **negrito**, _itálico_, `código`, lista, tarefa, citação, **Vincular nota**, link externo, linha horizontal, **∑** (fórmula) e **Caixa**. Um botão aceso indica a marca onde está o cursor; tocar nele de novo tira a marca.

        Na barra do alto: **Assistente**, os botões de plugins (como o Relógio), **Visual / Fonte** e **Mais ações**.

        ## O menu /

        Num bloco vazio, digite `/` para ver os blocos: títulos, lista, tarefa, citação, divisória, link para nota, link externo, fórmula, fórmula em destaque, bloco de código, comentário e caixa. Continue digitando para filtrar.

        ## Links e ruas

        - Digite `[[` e escolha a nota nas sugestões. O link vira uma **rua** na cidade.
        - Tocar num link abre a nota. Se ela ainda não existe, o Urbe pergunta se quer **criar nota**.

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

        **Renomear**, **Mover para outra pasta**, **Mostrar nas notas**, **Versões anteriores** (o histórico, para restaurar), **Estrutura e backlinks** (os títulos da nota e as notas que apontam para ela), **Fixar em painel** e **Mover para a lixeira**.

        **Fixar em painel** deixa a seleção (ou a nota inteira) no topo, com rolagem própria, enquanto você escreve embaixo. O painel continua quando você abre outra nota.

        ## Abas e painéis

        Cada nota aberta vira uma aba. **Voltar / Avançar** percorrem o caminho; **Abrir em painel dividido** mostra duas notas lado a lado; **Localizar e substituir** procura no texto. Veja [[Atalhos]].
        """;

    private const string Modelos = """
        # Modelos

        Um modelo é uma nota comum dentro da pasta `Modelos`. Nada de formato especial.

        ## Criar um modelo

        Crie a pasta `Modelos` e escreva notas nela. Use campos entre chaves duplas:

        ```
        # {{titulo}}
        Criado em {{data}}

        Cliente: {{cliente}}
        ```

        - `{{titulo}}` vira o nome da nota nova.
        - `{{data}}` vira a data de hoje.
        - Qualquer outro campo, como `{{cliente}}`, é perguntado depois do nome.

        ## Usar

        No [[Explorer]], **Nova → A partir de um modelo**: escolha o modelo, dê o nome, preencha os campos e a nota nasce pronta.

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

        ## Prévia e comentários

        **Ver página** mostra a página dentro do Urbe, como ela será publicada. Os comentários das notas (`%%…%%` e `{"…"}`) não aparecem na página. Na prévia, o 💬 de uma palavra permite **comentar**, e o comentário é salvo no texto.

        ## Exportar

        No botão **Exportar** ou no **⋯** da página:

        - **Imprimir ou salvar PDF**
        - **Baixar HTML**: um arquivo único, que funciona em qualquer hospedagem
        - **Salvar HTML no vault**, ao lado da página
        - **Copiar HTML**

        No menu do estúdio também estão **Salvar como modelo**, **Duplicar página** e **Editar JSON**. O arquivo da página é um `.page.json` na sua pasta.
        """;

    private const string Assistente = """
        # Assistente

        O **Assistente** (embaixo, ou no alto do editor) é um conjunto de agentes que pesquisam, escrevem e organizam a sua pasta, com um modelo de IA que você escolhe.

        ## Conectar um modelo

        Nas configurações do Assistente, **+ Adicionar provedor**: OpenRouter, Anthropic, OpenAI, Google Gemini, Ollama (local) ou um servidor compatível com OpenAI. Cole a chave, toque em **Testar** e escolha o modelo. As chaves ficam salvas só neste aparelho e vão direto para o provedor.

        ## Pedir

        Escreva em **Peça algo ao Assistente…**. Ele lê e busca as notas sozinho. Para mudar algo, ele **propõe**:

        - **Aprovar** grava a mudança; **Aprovar todas** vale para as próximas desta resposta.
        - **Editar** deixa você ajustar o texto proposto antes de aprovar; o que é gravado é a sua versão.
        - **Recusar** descarta.
        - Exclusões pedem **Excluir**, e plugins pedem **Ligar plugin**.
        - Depois de uma mudança, **Desfazer** volta atrás.

        O texto escrito pelo Assistente ganha no fim uma marca pequena com o nome do modelo, como `{ia:"modelo"}`, que aparece em verde no editor.

        ## Ajustar

        - **Instruções para todos os agentes**, **Memória** (fatos para lembrar), **Agentes personalizados** e **Limite de passos por resposta**.
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

        O plugin nativo **Relógio** mostra **Agora**, um **Cronômetro** e um **Temporizador** (com tempos prontos) num painel sobre a cidade e o editor. Ligado, ele ganha um botão no alto da cidade e na barra do editor.

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
        - **Cidade:** moradores, animais, clima, eventos, nomes das casas e dos bairros; **Muralha** e **Contornos** dos bairros; cores do chão; a **Luz** (hora do dia).
        - **Texturas:** pacotes de texturas para o chão e as construções.
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

        **Configurações → Importar arquivos** (ou **+ → Importar**) traz **arquivos** ou uma **pasta inteira**: notas, pastas ou `.zip`. O Urbe confere os arquivos antes. Se o `.zip` for um export ou backup do Urbe, ele oferece restaurar; o que for substituído é guardado antes em backup.

        ## Backups automáticos

        Antes de migrar, reorganizar a cidade ou importar, o Urbe guarda um backup em `.urbe/backup`. Para ver e restaurar: **Ctrl+K → Backups do vault**.

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
        | Ctrl+Shift+M | Voltar para a cidade |
        | Ctrl+M | Fórmula no texto |
        | Ctrl+Shift+M (no editor) | Fórmula em destaque |
        | Esc | Fechar a busca ou terminar de editar o bloco |

        **Abrir comandos** (Ctrl+K) lista tudo o que o Urbe faz: organizar e desfazer a organização dos bairros, backups, painel dividido, restaurar versão, plugins e mais.

        No celular: arraste para andar, pinça para o zoom, toque para abrir e segure para mover ou selecionar. Se um botão não responde, **Configurações → Diagnóstico de toques** ajuda a descobrir o porquê.
        """;
}
