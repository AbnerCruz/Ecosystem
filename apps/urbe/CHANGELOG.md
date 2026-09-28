# Urbe — Changelog

## v1.8.0-beta — A cidade como aquário: clima, luz, bichos e eventos (2026-09-28)

A cidade agora é interessante de olhar mesmo sem ninguém mexendo em nada (novo `src/world/life.js`).

### Luz
- A luz muda aos poucos: aurora, dia claro, sol dourado, crepúsculo e noite.
- As janelas acendem uma a uma, em horas um pouco diferentes para cada casa.
- Novo modo **Ciclo** (padrão): um dia inteiro a cada 24 minutos. Continuam existindo Dia, Tarde, Noite e Auto.

### Clima
- Sombras de nuvens passando, vento que muda de direção, folhas caindo das matas e rajadas sobre os campos.
- Chuva passageira (neve nas regiões frias), com marolas na água e respingos. Depois da chuva, de dia, pode vir um arco-íris.
- Neblina, principalmente de manhã.

### Água, fauna e flora
- Brilhos do sol e da lua na água, peixes pulando e marolas dos patos.
- Borboletas de dia e vaga-lumes à noite.
- Pombos bicando perto das ruas.
- Um cachorro acompanha um morador.

### Cidade
- Fumaça nas chaminés, que segue o vento e aparece mais de manhã, à noite e no frio.
- Lampiões nas ruas, que acendem ao escurecer.
- Moradores com guarda-chuva na chuva, lanterna à noite e balõezinhos (♪ ♥ ! …).

### Eventos
- Balão de ar quente, com a sombra passando pelo chão.
- Festa no bairro: balões e confete de dia, fogos de artifício à noite (peônia, anel e salgueiro).
- Barco à vela com esteira.
- Chuva de estrelas cadentes.
- Raposa ao anoitecer.
- Revoada migratória.
- Um aviso discreto no alto da tela conta o que está acontecendo, e dá para chamar qualquer evento pela paleta de comandos ("Cidade: …").

### Interações
- Pombos levantam voo quando alguém (ou o cachorro, ou a raposa) chega perto.
- Cervos se afastam de quem passa.
- Ovelhas e vacas fogem da raposa.
- A chuva apressa o passo dos moradores.

### Fluidez e opções
- Moradores atualizam a ~30 quadros por segundo (antes, 20) e animais a 25 (antes, 12).
- Os efeitos são desenhados sobre a imagem guardada da cidade, então não repintam chão e ruas.
- A qualidade se adapta ao aparelho: menos partículas se ele não acompanha. Num celular médio simulado, a cidade parada com tudo ligado roda a ~34 quadros por segundo.
- Novas chaves **Clima** e **Eventos** em Personalização → Cidade (e `cidade.clima`, `cidade.eventos` no tema.json).

## v1.7.2-beta — Abrir o app sem espera nem tela antiga (2026-09-28)

- **Abrir o app ficou rápido.**
  - Antes, o app lia a pasta arquivo por arquivo pela ponte com o Android: numa abertura comum eram ~560 chamadas, e tudo era lido duas vezes. Era o "Lendo 9 / 54" de toda vez.
  - Agora o lado nativo, no Android e no Windows, lista a pasta e lê todos os textos em **2 chamadas**, e o app trabalha com um espelho em memória.
  - Numa abertura comum, as chamadas caíram de ~560 para 5.
- O espelho continua em dia:
  - o que o app grava ou apaga vai ao disco e ao espelho juntos;
  - no Android, ao voltar para o app, a pasta é relida, para enxergar o que mudou por fora;
  - no computador, depois de abrir, o vigia da pasta assume.
- Um app instalado sem a leitura em lote continua funcionando arquivo a arquivo.
- A tela antiga "URBE / Suas cidades / Fundar uma cidade nova" não aparece mais na abertura. Ela vinha marcada como aberta no HTML e ficava visível enquanto o app carregava.

## v1.7.1-beta — Cidade leve no celular e digitação sem travar (2026-09-27)

### Cidade
- Moradores e bichos andando não repintam mais a cidade inteira. Chão, bairros e ruas ficam guardados numa imagem enquanto a câmera está parada, e só a animação é redesenhada.
  - Com a tela parada, os retângulos pintados por quadro caíram de ~1.570 para ~50.
- Ruas: os vizinhos de cada trecho são calculados uma vez por rede, não a cada quadro.
- Textura do chão: uma imagem pequena por bloco de 16×16 tiles, no lugar de centenas de retângulos e cálculos de ruído por quadro. O desenho é o mesmo.
- A largura dos rótulos é medida uma vez por texto.
- Num celular médio simulado (processador 6× mais lento), arrastar a cidade foi de 12 para ~30 quadros por segundo.

### Salvamento
- Digitar numa nota grava só aquela nota. Antes, cada pausa na digitação também gravava um diário com o vault inteiro (~100 KB). No Android isso passava pela ponte com o sistema e travava a digitação: era 1 MB a cada 5 segundos, agora são ~7 KB.
  - O diário continua protegendo as operações que mexem em vários arquivos de uma vez.
- O diário de recuperação é apagado depois de aplicado. Antes, podia ficar no disco e ser reaplicado por cima de edições mais novas.
- Windows: duas gravações do mesmo arquivo ao mesmo tempo não se atropelam mais. Antes, podiam falhar com "no such file" por usar o mesmo arquivo temporário. Agora ficam numa fila e cada uma usa um nome temporário único.

## v1.7.0-beta — Urbe instalável: Windows e Android, com pasta real e atualização (2026-09-27)

### Instalar de verdade
- **Windows**: instalador `Urbe-Setup-(versão).exe`, com atalho na área de trabalho e no menu Iniciar.
- **Android**: `Urbe-(versão).apk`.
- Os dois ficam na página de lançamentos do GitHub, e o site continua funcionando no navegador.

### Pasta de verdade
- As notas, o **Tutorial**, as páginas, os anexos e o mapa passam a ser **arquivos reais** em `Documentos/Urbe`, visíveis no explorador ou gerenciador de arquivos.
- No Windows dá para escolher outra pasta, inclusive uma do OneDrive, Dropbox ou Google Drive.
- No Android, o app pede o "acesso a todos os arquivos" (opcional) para enxergar também o que for copiado para a pasta por outro app ou pelo computador.
- **Mudanças feitas por fora** (Explorer, Obsidian, sincronizadores) entram no app: na hora no Windows, ao voltar para o app no Android.
  - Uma edição local ainda não salva nunca é atropelada.
- Proteção: o app só lê e grava dentro da pasta do Urbe (nem `..`, nem caminho absoluto, nem atalho para fora). A gravação é por troca atômica, então um corte de energia não deixa nota pela metade.

### Atualizações
- **Windows**: baixa a versão nova sozinho e oferece **Reiniciar e atualizar**.
- **Android**: avisa a versão nova e baixa o APK com **Baixar e instalar**.
- **Configurações → Procurar atualização** também funciona no app instalado.
- A cada versão nova na `main`, o GitHub Actions constrói o `.exe` e o `.apk` e publica o lançamento.

### No app instalado
- **Imprimir ou salvar PDF**:
  - no Windows, gera o PDF direto, com o tamanho de página e as margens do livro;
  - no Android, abre a impressão do sistema.
- **Exportações** (HTML, `.zip`) vão para onde você escolher no Windows e para `Downloads/Urbe` no Android.
- Links externos abrem no navegador.
- **Voltar do Android**:
  - fecha o que estiver aberto;
  - depois sai da nota;
  - na cidade, minimiza (nunca fecha o app de repente).

## v1.6.0-beta — Estúdio como ambiente único: entre direto e construa (2026-09-27)

### Entrar direto no ambiente
- **Nova página** abre **direto no estúdio**, com uma seção em layout livre (título e parágrafo) já selecionada. Não há mais a escolha de modelo antes.
- **Nome no topo renomeia o arquivo**: uma página "Nova página" passa a se chamar como o título que você der (sem sobrescrever outra).
- A tela **Páginas** mostra só as suas páginas; modelos, blocos e composições ficam dentro do editor.

### Uma biblioteca só (＋ → Inserir)
- Abas **Blocos**, **Composições**, **Meus blocos** e **Modelos**, com busca, e a última aba usada fica lembrada.
- **Composições** entram no layout livre selecionado (na peça atual) ou numa seção nova.
- **Modelos dentro do editor**:
  - **Substituir a página**, **Adicionar as seções ao fim** ou **Só o visual** (tema e formato);
  - variante **Completo**, **Simplificado** ou **Só a estrutura**;
  - **Desfazer** logo após carregar.

### Mais ferramentas
- Peças novas: **Tabela**, **Código** (com botão de copiar), **Fórmula** (LaTeX), **Selo** e **Incorporar** (só `https://`, em moldura isolada). São 19 peças ao todo.
- 17 composições novas, 24 ao todo:
  - cabeçalho, rodapé, capa com imagem, capa dividida, números, depoimentos;
  - planos e preços, perguntas, equipe, chamada, galeria, linha do tempo;
  - citação grande, barra lateral, contato, abertura de capítulo, página em duas colunas.
- Tamanhos aceitam `clamp()`, `min()`, `max()` e `calc()` (para textos que crescem com a tela). Continuam bloqueando qualquer tentativa de injeção.

## v1.5.0-beta — Layout livre: páginas e livros montados peça por peça (2026-09-27)

### Layout livre (bloco novo, no topo da lista do ＋)
- Uma **árvore de peças**: containers que guardam outras peças (quantos níveis quiser), títulos H1–H6, parágrafos em Markdown, imagens, botões, listas, citações, ícones, divisores, espaços, vídeos, notas do vault, HTML livre e quebra de página.
- **Containers responsivos** sem coordenadas fixas:
  - **empilhados**;
  - **lado a lado**, quebrando linha sozinho abaixo de uma largura mínima;
  - **grade**, com colunas fixas ou automáticas.
- **Estilo de cada peça**:
  - arranjo, distribuição e alinhamento;
  - largura, altura, proporção e ajuste da imagem;
  - espaçamento interno e externo;
  - fundo em cor, degradê ou imagem;
  - borda, cantos, sombra e opacidade;
  - fonte, tamanho, peso, altura da linha, espaço entre letras, alinhamento, maiúsculas e itálico;
  - classe e CSS da peça (`&` é a própria peça).
- **Estilo por tamanho de tela**: base (computador), tablet (até 900 px) e celular (até 600 px). O editor segue o tamanho da prévia. Cada campo mostra o valor herdado do computador, e há um botão para voltar ao estilo do computador.
- **Editor**:
  - **Camadas** em árvore;
  - tocar na prévia seleciona a peça, e **tocar de novo num texto** permite escrever direto nele;
  - adicionar dentro ou depois;
  - **7 composições prontas**: duas colunas, imagem + texto, grade de cartões, capa centralizada, cartão, caixa de destaque e página de livro;
  - subir, descer, sair e entrar num container, envolver, duplicar e apagar, com botões rotulados para o toque;
  - tudo com desfazer.
- **Converter em layout livre**: capa, folha de rosto, créditos, dedicatória, parte, capítulo (inclusive o que vem de uma nota), sobre o autor, colofão, texto, capa de site, citação, imagem e nota viram peças soltas, um título ou parágrafo por peça.
- **Separar em capítulos**: "Capítulos de uma pasta" vira um bloco de capítulo por nota.
- **No livro**, cada layout livre pode ser folha própria, página inteira (sem margem) ou continuação do texto.
- O Assistente recebe a descrição completa da árvore e sabe montar layouts livres.

### Modelos
- **Tela livre**: capa, imagem + texto e grade de cartões, tudo em peças soltas.
- O modelo **Livro** ganhou uma página em layout livre de exemplo.

### Arquivo `.page.json` enxuto
- O arquivo grava só o que difere do padrão. Um livro de exemplo caiu de 6,2 KB para 1,3 KB, e ele abre exatamente igual.

### Segurança
- Estilos das peças são validados: medidas, cores e opções.
- A URL de imagem de fundo é codificada e não sai do `url(...)`.
- No CSS escrito pela pessoa, `<` vira escape de CSS, e nada fecha a tag `<style>`.
- Duas seções livres nunca colidem: as classes levam o id da seção.

### Testes e Tutorial
- `tests/pages-free.mjs`:
  - árvore, validação e ids únicos;
  - CSS responsivo (lado a lado, grade, tablet e celular);
  - segurança;
  - classes por seção;
  - conversões;
  - modos no livro;
  - arquivo enxuto em ida e volta em todos os modelos;
  - composições.
- Nova nota **Tutorial/Páginas/Layout livre**; Blocos das páginas, Livros e Modelos apontam para ela.

## v1.4.0-beta — Estúdio de páginas: toques no celular, personalização e modelos vazio/simplificado (2026-09-27)

### Toques no celular
- **Campos que "não funcionavam"**: ao tocar num campo, o teclado abria e encolhia a tela, e o estúdio redesenhava o painel nesse redimensionamento. Isso destruía o campo tocado: o teclado fechava e o texto se perdia. Agora o painel só é redesenhado quando o layout muda de verdade (celular ↔ computador) e nunca enquanto um campo está em uso.
- Os grupos recolhíveis (Aparência da seção e outros) lembram se estão abertos. Antes eles se fechavam sozinhos a cada mudança.
- O título da página no topo ganhou uma área de toque maior (tinha 21 px de altura).

### Personalização
- **Tema**:
  - títulos: peso, MAIÚSCULAS ou versalete, espaço entre letras e tamanho;
  - altura da linha do texto;
  - botões: cheio, pílula, quadrado, contorno ou suave;
  - cartões: elevado, contorno, liso ou vidro;
  - links: sublinhados, sem sublinhado ou marca-texto;
  - cor das bordas;
  - **CSS próprio da página**.
- **Seção**:
  - cor de fundo e cor do texto próprias;
  - seção em caixa (cartão);
  - altura mínima: natural, meia tela ou tela cheia;
  - animação: do tema, nenhuma, aparecer, subir, aproximar, da esquerda ou da direita;
  - **classe CSS** e **CSS desta seção**, em que `&` é a própria seção.
- **Página**: código livre no `<head>`, para estatísticas, fontes próprias e meta tags.
- O CSS escrito pela pessoa não consegue fechar a tag `<style>`, e as classes são limpas.
- **Painéis organizados em grupos** que abrem e fecham:
  - Tema: Cores e fundo, Texto e títulos, Botões/cartões/links, Espaço e movimento, CSS próprio;
  - Seção: Aparência e Avançado;
  - Página: as opções de livro só aparecem no formato Livro.
- **Meus blocos**: qualquer seção pode ser salva como bloco reutilizável (menu ⋯ → Salvar como bloco reutilizável).
  - O bloco é gravado em `Páginas/Blocos/*.block.json`, com conteúdo e aparência.
  - Ele aparece no topo da lista do ＋ em qualquer página e também serve para "Trocar tipo de bloco".

### Modelos
- **Vazio**: o mínimo do mínimo, sem seções, sem barra, sem rodapé e sem animações.
- **Simples** (substitui "Em branco"): um título, um texto e um rodapé.
- Ao criar a partir de qualquer modelo com várias partes, inclusive os da pessoa, dá para escolher:
  - **Completo**: todas as partes, com textos de exemplo;
  - **Simplificado**: só as partes principais, listas com dois itens e um botão por grupo; num livro, sem as páginas opcionais;
  - **Só a estrutura**: os mesmos blocos e visual, sem os textos de exemplo.

### Testes e Tutorial
- Novos testes:
  - personalização aplicada e CSS seguro;
  - modelo Vazio;
  - variações de todos os modelos válidas;
  - "Só a estrutura" mantém os blocos e tira os textos.
- Tutorial atualizado: Modelos e temas, Blocos das páginas (Aparência, Avançado, Meus blocos) e O que são páginas.

## v1.3.3-beta — Primeiro acesso e pasta do aparelho mais confiáveis (2026-09-27)

- **Tutorial numa pasta nova**: a marca de "Tutorial já criado" também ficava no aparelho, pelo nome da cidade ("Urbe"). Por isso, uma pasta nova e vazia escolhida depois de apagar a antiga ficava sem Tutorial. Agora vale a marca que fica **dentro da pasta** (`.urbe/tutorial.json`). A do aparelho só é usada quando não dá para gravar na pasta.
- **Permissão da pasta expirada** (comum no Android): antes o app passava em silêncio a usar o armazenamento interno, e parecia que as notas tinham sumido. Agora ele pergunta: **Permitir acesso à pasta**, **Escolher outra pasta** ou **Usar o armazenamento do aplicativo**.
- **Pasta apagada, movida ou renomeada**: antes a abertura falhava e mostrava um menu sem explicação. Agora o app diz que a pasta não foi encontrada e oferece escolher outra, que pode ser nova e vazia, ou usar o armazenamento do aplicativo.
- **Qualquer outra falha ao abrir** mostra o erro com as opções **Tentar de novo**, **Escolher outra pasta** e **Usar o armazenamento do aplicativo**.
- Tutorial ("Onde ficam seus arquivos") e testes atualizados.

## v1.3.2-beta — A migração leva a cidade para terra firme (2026-09-27)

- Algumas cidades antigas ficaram inteiras **no meio do oceano** do mundo novo. A reorganização da 1.3.1 procurava espaço em volta do centro atual da cidade, que era o mar. Agora a migração leva a cidade para o **ponto de partida do mundo**, escolhido pelo gerador em terra boa, com rio por perto, e a câmera vai junto.
- "Organizar os bairros" e os bairros novos também conferem se o centro tem terra firme por perto. Se não tiver, usam o ponto de partida.

## v1.3.1-beta — Correção: cidades antigas travavam em "Sincronizando mundo" (2026-09-27)

- **Causa**: uma cidade criada antes do mundo novo tinha casas em lugares que agora são água ou montanha. Para cada ligação impossível entre essas casas, o traçado das ruas percorria até 5 janelas de 120 mil passos. No celular isso levava minutos, e a tela de carregamento parecia travada. Num computador, bastavam 6 casas na água para levar 18 s.
- **Ruas**: a busca agora desiste na hora quando a ligação é impossível:
  - ponta na água ou na montanha;
  - ponta numa ilha, detectada por uma inundação curta com as mesmas regras da busca.
  - Janelas maiores só são tentadas quando ajudam, e há um orçamento total de passos por ligação.
  - No mesmo caso das 6 casas na água, o tempo caiu de 18 s para 0,25 s. As cidades normais não mudam.
- **Migração**: o mapa salvo agora guarda a versão do mundo. Ao abrir uma cidade salva com outro mundo, o Urbe reorganiza os bairros e as casas uma vez, no terreno novo, e avisa. Notas, pastas e ligações não mudam.
- **Bairros novos** (por exemplo, o do Tutorial criado ao abrir uma cidade antiga) contornam as casas soltas do mesmo nível, em vez de passar por cima delas.

## v1.3.0-beta — Livros: páginas pensadas para produzir livros (2026-09-27)

### Formato Livro
- Novo campo **Formato** (Site ou Livro) em Página → Estrutura. No formato Livro:
  - A tela mostra **folhas de papel** no tamanho escolhido.
  - Imprimir ou salvar em PDF usa as regras de livro:
    - **tamanho de página**: A5, 15,2 × 22,9 cm, bolso, A4 ou carta;
    - **margens** estreitas, normais ou largas, **espelhadas** entre a página da esquerda e a da direita, com a maior do lado da lombada.
  - Cada capítulo e cada parte **começam em página nova**, e há opção de começar sempre na página da direita.
  - **Números de página** no pé e **título do livro no alto**:
    - a capa sai sangrada, sem margem;
    - as páginas iniciais (folha de rosto, créditos, dedicatória, sumário) ficam sem número;
    - a abertura de capítulo e de parte fica sem o título no alto.
  - O texto sai **justificado com hifenização**, com **recuo na primeira linha** (cada um pode ser desligado), e com controle de viúvas e órfãs.
  - Numeração dos capítulos: "Capítulo 1", "Capítulo um", "1", "I" ou sem número.
  - Barra de navegação, rodapé e botões de site somem.
  - Um botão **Imprimir ou salvar PDF** aparece no HTML exportado.
- **10 blocos de livro**:
  - capa (clássica, moderna ou com imagem);
  - folha de rosto;
  - créditos e direitos (ISBN, ficha, equipe);
  - dedicatória ou epígrafe;
  - **sumário automático** com partes e capítulos;
  - parte;
  - **capítulo**, escrito na página ou puxado de uma nota, com epígrafe e **letra capitular**;
  - **capítulos de uma pasta**, em que cada nota vira um capítulo;
  - sobre o autor;
  - colofão.
- Numeração automática de partes (I, II…) e capítulos, contando também os que vêm de uma pasta. Um `[[link]]` para uma nota que é capítulo leva até ele.
- `***` vira ornamento de troca de cena (⁂). A primeira linha do capítulo sai em versalete.
- **Temas e fontes**:
  - temas **Livro clássico** (papel creme, EB Garamond e Cormorant Garamond) e **Livro moderno**;
  - fontes EB Garamond, Cormorant Garamond, Crimson Pro e Libre Baskerville.

### Modelos
- **Livro**: capa, folha de rosto, créditos, dedicatória, sumário, duas partes com capítulos de exemplo, sobre o autor e colofão.
- **Livro de uma pasta**: a mesma estrutura, com cada nota da pasta virando um capítulo. O nome digitado vai para a capa, a folha de rosto e o título no alto das páginas.

### Estúdio
- **Exportar → Imprimir ou salvar PDF**, em qualquer página. A impressão roda num quadro isolado, sem acesso ao app.

### Tutorial e testes
- Nova nota **Tutorial/Páginas/Livros**; "Modelos e temas", "Blocos das páginas" e "Estúdio" apontam para ela.
- Novo teste do livro:
  - numeração, inclusive dos capítulos de uma pasta;
  - sumário com links e wikilinks entre capítulos;
  - regras de página, margens espelhadas, números e cabeçalho;
  - todos os tamanhos e margens;
  - HTML injetado bloqueado e cabeçalho escapado no CSS;
  - blocos de livro no formato Site sem regras de impressão.

## v1.2.0-beta — Bairros organizados: hierarquia clara no chão e no mapa (2026-09-27)

A cidade deixava as pastas parecerem uma bagunça: bairros em losango, contornos sobrepostos, subpastas espalhadas, casas jogadas em lugares aleatórios e nomes empilhados. Agora ela segue regras de verdade.

### Forma e lugar dos bairros
- Os bairros são **arredondados e compactos**, do tamanho do que guardam. Eles crescem por prioridade a partir do centro, com um leve ondulado; antes cresciam em busca em largura e saíam em losango.
- Cada **subpasta** fica **dentro** do bairro pai, com 1 tile de margem até a borda dele.
- Bairros irmãos ficam separados por 1 tile, e bairros da raiz por 2. Nenhum contorno encosta em outro.
- Um bairro novo nasce no espaço livre mais perto do centro da cidade (ou do bairro pai) que tenha folga. Por isso a cidade cresce junta, sem ilhas soltas.
- Quando uma subpasta não cabe, o bairro pai cresce de forma arredondada em volta dele mesmo.
- Na **importação**, as subpastas reservam espaço primeiro, e as casas da pasta ocupam o resto. O tamanho de cada bairro leva em conta a árvore inteira de subpastas, também ao abrir uma pasta do aparelho.
- **Cores**: um bairro da raiz ganha a cor menos usada pelos vizinhos (paleta com 10 cores). As subpastas usam um tom mais claro da cor do bairro de cima, então a família de pastas se reconhece de longe.

### Casas agrupadas, sem grade
- As casas se **agrupam perto do centro do bairro** num arranjo orgânico: a ordem dos lotes segue a distância ao centro com um leve ondulado e um sorteio fixo por lote. Não há fileiras nem grade. Antes as casas caíam espalhadas em lugares sorteados.
- As notas soltas na raiz se agrupam do mesmo jeito em volta do centro, a 1 tile de qualquer bairro.

### Desenho
- O contorno é uma **curva suave**: a borda é traçada como polígono e arredondada, sem degraus.
- Cada bairro é preenchido de uma vez, sem emendas nem manchas de sobreposição.
- A hierarquia aparece na borda:
  - pasta na raiz: borda grossa com halo escuro;
  - subpasta: borda fina;
  - do 3º nível para baixo: borda tracejada.
- **Plaquinhas** no alto de cada bairro: um quadrado colorido para as pastas da raiz e uma bolinha para as subpastas.
  - Os nomes das subpastas só aparecem com zoom suficiente.
  - Uma plaquinha nunca fica sobre outra.
  - Os nomes de casas que colidiriam com uma plaquinha ou com outro nome ficam escondidos; a casa selecionada sempre mostra o dela.
- O **mapa** e o **minimapa** desenham a forma real de cada bairro, com a mesma hierarquia e os nomes, no lugar dos retângulos de antes.

### Organizar uma cidade que já existe
- Novo item **Configurações → Organizar os bairros** (comando `city.reorganize`).
  - Redesenha todos os bairros, dos maiores para os menores e cada subpasta dentro da sua, e reagrupa as casas perto do centro de cada bairro.
  - Notas, pastas e ligações não mudam.
- Novo serviço `city.layout` com `validate()`, que confere as regras: subpasta dentro da margem do pai, irmãos sem encostar, casas inteiras no próprio bairro e sem sobreposição.

## v1.1.0-beta — Mundo realista: placas tectônicas, relevo, clima e rios de verdade (2026-09-27)

O mundo deixou de ser ruído aleatório. Agora ele é simulado como um continente de verdade, numa grade de 512 × 512 células (cada uma com 4 × 4 tiles), em menos de um segundo ao abrir.

### Placas tectônicas e relevo
- 16 a 21 **placas** (continentais e oceânicas), cada uma com direção de movimento. As bordas são deformadas para não parecerem polígonos.
- O que acontece em cada borda depende do tipo de encontro:
  - colisão entre continentes: cordilheiras altas (tipo Himalaia);
  - continente sobre oceano: serra costeira e fossa no mar (tipo Andes);
  - oceano com oceano: arcos de ilhas;
  - afastamento: vales em rift e dorsais no fundo do mar.
- As cordilheiras têm cristas e picos, e o soerguimento é suavizado, sem listras.
- **Curva hipsométrica real**: muita planície, colinas e poucas serras altas. A plataforma continental é rasa e o mar fica fundo longe da costa. A borda do mapa afunda no oceano de forma orgânica.

### Clima
- A temperatura cai com a latitude (polos nas pontas do mapa) e com a altitude.
- O vento sopra em faixas de latitude (alísios e ventos de oeste) e leva umidade do mar. Ela chove na subida das serras e deixa **sombra de chuva** do outro lado.
- As células de Hadley deixam o equador úmido e formam faixas secas de deserto.
- Os biomas seguem o diagrama de Whittaker (temperatura × umidade).

### Rios e lagos
- **Drenagem de verdade** (priority-flood):
  - toda célula escoa para o mar;
  - depressões fechadas se enchem e viram lagos;
  - o fluxo acumulado da bacia decide onde há rio.
- Os rios nascem nas partes altas, **sempre descem**, recebem afluentes e ficam mais largos rio abaixo, até o mar ou um lago.
- Um microrrelevo só para a drenagem faz os rios serpentearem nas planícies.
- O traçado de cada tile é contínuo (segmentos entre células, com leve serpentear), e a costa e as margens dos lagos são orgânicas.

### Cidade
- A cidade começa num lugar escolhido pela geografia: clima temperado, relevo suave, um rio por perto e longe da costa e das serras.

### Testes
- Novos testes do mundo físico:
  - placas;
  - montanhas junto às bordas de placa;
  - relevo e fração de terra;
  - todo trecho de rio desce até o mar ou um lago;
  - a vazão cresce rio abaixo e há confluências;
  - clima por latitude e altitude;
  - outras sementes.
- O Tutorial explica como o mundo é formado (Cidade → Como a cidade funciona).

## v1.0.0-beta — Primeira beta: personalização, Tutorial e casa limpa (2026-09-27)

### Personalização completa
- **Tela de Personalização** (Configurações → Personalização) com 8 abas: Aparência, Texto, Editor, Cidade, Texturas, Estilos CSS, Plugins e Arquivo. Tudo aparece na hora, com prévia ao vivo enquanto você arrasta cores e controles.
- **Temas**: 8 prontos (Escuro, Meia-noite, Floresta, Oceano, Vinho, Claro, Sépia e Alto contraste), cor de destaque, as 14 cores uma a uma e temas salvos em `Personalização/temas/`.
  - O texto sobre o destaque fica preto ou branco, o que der mais contraste.
  - Trocar o fundo decide sozinho se o tema é claro ou escuro.
  - O CSS do app passou a usar variáveis, então os temas claros funcionam em todas as telas.
- **Texto e forma**: 7 famílias de fonte (ou qualquer fonte instalada), tamanhos, altura da linha, cantos, densidade, efeito de vidro e animações.
- **Editor**: largura do texto, modo inicial (Visual ou Texto) e corretor ortográfico.
- **Cidade**:
  - liga e desliga moradores, animais e nomes das casas e dos bairros;
  - hora do dia: tarde dourada, noite com janelas acesas, ou automática pelo relógio;
  - cor de cada bioma.
- **Texturas próprias**: pacotes JSON com o chão de cada bioma e o desenho de cada tipo de construção, em grade de pixels ou imagem.
  - Editor de pixels por toque, com lápis, balde, conta-gotas e desfazer.
  - Importar imagem do aparelho.
- **Estilos CSS** ligáveis um a um, mais um CSS rápido.
- **Modo seguro** (na tela ou com `?seguro=1`): desliga estilos e plugins sem apagar nada.
- **Plugins** em `Personalização/plugins/`:
  - API em português: comandos, botões na cidade, notas, eventos, estilos, desenho sobre o mapa, ferramentas para o Assistente, dados e temporizadores. Tudo é desfeito ao desligar.
  - Segurança: nada roda sem o usuário ligar neste aparelho. A aprovação guarda o SHA-256 do código, e qualquer mudança no arquivo pede nova revisão. Erros ficam isolados.
  - 4 modelos para começar.
- Tudo mora em `Personalização/tema.json` (validado, com erros apontados por caminho) e vai junto com o vault. O Assistente ganhou `appearance_schema`, `set_appearance` e `plugin_guide`.

### Pasta Tutorial
- **46 notas** em linguagem simples, cobrindo: cidade, mapa, construções, bairros e vida na cidade; notas, aba Notas, editor, Markdown, links, tags, busca, comandos, abas e painel dividido; lixeira, versões e tipos de arquivo; fórmulas e uma galeria delas; páginas, blocos, modelos, estúdio e composições; o Assistente (conexão, agentes, modos, ferramentas e bons pedidos); toda a personalização com as referências da API de plugins e do `tema.json`; arquivos, backup e recuperação; instalação, gestos, atalhos e diagnóstico de toques; solução de problemas e glossário. Inclui uma página de exemplo.
- Criada sozinha na primeira abertura, uma vez por cidade (se você apagar, não volta), com um bairro para cada assunto. Numa cidade vazia, abre em "Comece aqui".
- **Configurações → Tutorial** abre ou restaura. Restaurar recria o que falta e pergunta antes de substituir notas que você mudou.
- As notas vêm de `tutorial/*.md`. `tools/build-tutorial.mjs` confere se todo link aponta para uma nota que existe, se não há tags acidentais e se tudo é alcançável a partir de "Comece aqui".

### Editor
- O modo Visual mostra e salva **sem perdas**: tabelas (com alinhamento), citações de várias linhas, **callouts** (`> [!tip]`, 12 tipos), **listas aninhadas**, a linguagem dos blocos de código e links com rótulo `[[caminho|rótulo]]`. Antes, tabelas viravam parágrafos soltos ao editar, e rótulos e sublistas se perdiam.
- `[[...]]`, `**` e `_` dentro de trechos de código são mostrados como texto.
- **Versões anteriores** no menu ⋯ da nota: lista por data com prévia e restaura (até 40 versões por nota).
- **Ctrl+\** pergunta qual nota abrir ao lado (antes abria a mesma nota).
- **Localizar e substituir** passa para o modo Fonte sozinho.

### Correções encontradas na varredura
- **Excluir no painel da casa** mandava a nota para lugar nenhum: o desenho sumia, a nota continuava e a casa voltava. Agora vai para a Lixeira.
- **Pastas criadas pelo Assistente, por importação ou por plugins** viram bairros na cidade. Antes, só as criadas pelo botão viravam; os bairros filhos fazem o pai crescer quando não cabem.
- **Tags e links dentro de código** (exemplos de `#tag`, CSS como `#renderedPreview`, `[[link]]` entre crases) não contam mais como tags e ligações.
- **Lixeira**: excluir de vez e esvaziar (antes só dava para restaurar).
- **Backup**: "Exportar tudo (.zip)" e "Importar arquivos" voltaram a ser alcançáveis (menu ⋯ da aba Notas e Configurações).
- **Diagnóstico do workspace** mostra o resultado na tela.
- **Aba Notas**: Ctrl/⌘+clique seleciona várias no computador.
- As Configurações não abrem mais a tela antiga de cidades, que não tinha como fechar.

### Casa limpa
- Saíram ~250 KB de imagens PNG embutidas do visual antigo (`app.js` e `index.html`). A prévia do painel da casa e a "casa fantasma" ao posicionar usam a arte atual.
- O CI confere a sintaxe de **todos** os `.js` e roda **todos** os testes de `tests/`.
  - Antes, dois módulos do núcleo estavam fora da lista.
  - Novo teste de consistência: todo arquivo está no `index.html` e no service worker, e há uma versão só em todo lugar.
- README novo, ARCHITECTURE atualizado e `.gitignore`.

## v0.48.0 — Moradores de verdade (2026-09-27)

- **Moradores em pixel-art**: no lugar dos retângulos, cada morador agora é um bonequinho no mesmo estilo do mundo, com contorno, sombra e animação de passos. Tem quatro quadros de caminhada e vira para frente, para trás e para os lados.
- **Cada um é diferente**: tom de pele, cabelo, roupa (calça ou vestido), chapéu de palha, boné, capuz ou careca, e o que carrega (cesto, saco, balde ou cajado). A aparência é fixa por casa, então o mesmo morador é sempre reconhecível.
  - A guilda da casa influencia a roupa: lavradores de chapéu de palha e cesto, escribas de capuz azul, guardas de vermelho com cajado, e assim por diante.
- **Vida na rua**:
  - Cada morador anda no seu ritmo, mais devagar que antes.
  - Sai pela porta de casa e entra na casa do vizinho. Fica lá um pouco e depois volta.
  - Anda do seu lado da rua, então quem vai e quem vem não se atravessa.
  - Quando dois se cruzam, às vezes param para conversar, com um balão de "…".
  - Em algumas casas, alguém fica à toa ao lado da porta, dando uns passos e olhando em volta, mesmo quando a nota ainda não tem ligações.
- Animação mais fluida (quadro a cada 50 ms), e os moradores aparecem a partir de 38% de zoom.

## v0.47.0 — Mundo cheio de vida (2026-09-27)

- **De longe, floresta continua floresta**: cada chunk ganha uma versão com as copas das árvores pintadas de cima (luz, sombra, neve), gerada no worker em meia resolução. Abaixo de 50% de zoom o mapa usa essa versão, então não fica mais um chão liso. Também é mais leve que desenhar milhares de árvores uma a uma. Bairros continuam sem árvores, como de perto.
- **Chão muito mais rico** (18 tipos de detalhe, em ~28% dos tiles, até dois por tile):
  - Campo e prado: canteiros de flores em manchas, capim alto, arbustos (alguns floridos) e pedrinhas.
  - Floresta: cogumelos, samambaias, troncos caídos e tocos.
  - Colinas e montanhas: rochas e matacões.
  - Pântano: juncos e poças.
  - Rios e lagos: vitórias-régias na margem.
  - Praia: conchas e madeira trazida pelo mar.
  - Savana, estepe e deserto: capim seco e arbustos secos.
  - Tundra: manchas de neve.
- **Fauna**: rebanhos de ovelhas e vacas pastando e andando pelos campos, cervos nas florestas, patos nadando em rios e lagos, e bandos de pássaros cruzando o céu com sombra no chão.
  - Os animais evitam ruas, casas e bairros.
  - Só existem perto do que está na tela.
  - Usam o mesmo relógio leve dos moradores, com custo desprezível.

## v0.46.0 — Mundo mais bonito, mapa explorável e toques no celular (2026-09-27)

- **Biomas maiores**: continentes, clima e umidade em escalas 2–3× maiores e com menos detalhe miúdo. As regiões ficam coerentes (planícies, florestas, cordilheiras e lagos grandes) em vez de retalhos; o trecho médio de um bioma passou de 17,7 para 33 tiles. A área em volta da cidade continua amigável para construir.
- **Transições suaves**:
  - Entre biomas de terra, as texturas se misturam gradualmente (ecótono) em vez de trocar numa escadinha da grade.
  - Margens de rios, lagos e mar são curvas, com a espuma seguindo a costa.
  - O sombreamento do relevo também transiciona.
  - As árvores avançam alguns tiles sobre o bioma vizinho, e bosques e campos se mesclam.
- **Mapa refeito**:
  - Arrastar para explorar, pinça ou roda para zoom, toque duplo para aproximar.
  - Um toque marca o lugar e mostra o bioma com “Ir até lá”, então arrastar nunca teleporta sem querer.
  - Botões fechar, +/− e “voltar para onde estou”, em tela cheia no celular.
  - Terreno com relevo sombreado desenhado em duas passadas (rápida e nítida), com pastas, ruas, casas e onde você está.
- **Configurações de verdade** (botão de ajustes na Cidade, que antes abria só o Assistente): cidade/vault, pasta no dispositivo, Assistente, Páginas, diagnóstico de toques e “Procurar atualização”, com a versão.
- **Diagnóstico de toques** (Configurações): cada toque deixa um ponto verde (o botão recebeu) ou vermelho (não recebeu), e “Copiar relatório” gera um texto com o aparelho e o que aconteceu em cada toque, para achar botões que falham num celular específico.
- **Todos os controles** usam `touch-action: manipulation`, sem espera de zoom por toque duplo.
- **Matemática**: não existe mais “nota matemática” separada; fórmulas funcionam em qualquer nota.

## v0.45.0 — Matemática nas notas (fase 1) (2026-09-27)

- **LaTeX em qualquer nota `.md`**, sem formato novo:
  - Fórmulas no texto com `$…$` ou `\(…\)`; em destaque com `$$…$$` ou `\[…\]`.
  - Blocos de código e `\$` ficam como estão, e preços como “R$ 10” não viram fórmula.
  - Renderização com KaTeX embutido no app (`vendor/katex`), que funciona offline.
- **Editor visual**:
  - Fórmulas são blocos que não se desmontam ao editar o texto em volta.
  - Tocar ou clicar numa fórmula abre um painel com prévia ao vivo, erros explicados, autocompletar de comandos, símbolos (Básico, Grego, Cálculo, Conjuntos, Setas) e 18 modelos (fração, integral, somatório, limite, derivadas, matrizes, sistema, por partes, alinhado…).
  - No celular, o painel sobe como folha inferior. **Tab** pula para a próxima `{}` a preencher.
  - Digitar `$x^2$` converte na hora.
  - Botão **∑** na barra (ou **Ctrl+M**; **Ctrl+Shift+M** para destaque) e itens “Fórmula” e “Fórmula em destaque” no menu `/`.
  - O Markdown volta exatamente como estava: cada fórmula guarda seus delimitadores, e abrir e sair não altera o arquivo.
- **Modo Fonte**:
  - Com o cursor dentro de uma fórmula, aparece uma barra com a prévia (ou o erro), o autocompletar (`\fr` → `\frac{}{}`, Tab/Enter) e os símbolos.
  - No celular, a barra fica logo acima do teclado. Em notas com `tipo: matematica` no cabeçalho, ela aparece sempre.
- **Nota matemática**: Notas → Nova → “Nova nota matemática” (ou a paleta de comandos) cria a nota com `tipo: matematica` e um exemplo.
- **Fórmulas também no Assistente e nas Páginas** (o HTML exportado leva o CSS do KaTeX). O Assistente foi orientado a escrever matemática em LaTeX.

## v0.44.0 — Páginas: estúdio de sites e páginas HTML (2026-09-27)

- **Páginas são arquivos do vault** (`.page.json`): um JSON com meta, tema, layout e seções. Você edita no estúdio visual, o Assistente cria e edita pelo JSON, e dá para abrir como texto. Exporta um único HTML independente, com o mesmo resultado da prévia.
- **23 blocos**:
  - Estrutura: capa, chamada, contato, contagem regressiva, sumário, divisor.
  - Conteúdo: texto em Markdown (tabelas, tarefas, callouts, código, [[links]]), destaques, cartões, citação, depoimentos, números, linha do tempo, perguntas, colunas, planos, código com botão copiar.
  - Mídia: imagem, galeria, vídeo (YouTube/Vimeo/arquivo).
  - Notas: nota e coleção de notas, que puxam o conteúdo real e se atualizam sozinhos. Os [[links]] viram âncoras dentro da página.
  - Avançado: HTML livre.
- **8 temas** (Aurora, Papel, Grafite, Oceano, Floresta, Entardecer, Neon, Lavanda), cada um com modo claro, escuro ou automático. Dá para ajustar cores, 12 fontes, arredondamento, escala do texto, espaçamento, largura, sombras, fundo (degradê, aurora, pontos, grade) e animações ao rolar.
- **Página publicada**: barra de navegação com menu no celular, botão claro/escuro, voltar ao topo e barra de progresso de leitura. É responsiva e respeita “reduzir movimento”.
- **Estúdio**:
  - Prévia ao vivo sem piscar, em celular, tablet ou computador.
  - Toque ou clique numa parte da prévia para editar.
  - Seções com arraste para reordenar, ocultar, duplicar e trocar de tipo.
  - Inspetor gerado a partir de cada bloco, com listas editáveis e imagens reduzidas no aparelho.
  - Editor de JSON com validação ao vivo.
  - Desfazer/refazer e salvamento automático.
  - Exportar: baixar HTML, abrir em nova aba, salvar no vault ou copiar.
- **Celular**: barra de ferramentas embaixo (Seções, Editar, +, Tema, Página) e folha inferior arrastável.
- **Computador**: painéis laterais e atalhos (`/` adicionar, ↑↓ selecionar, Alt+↑↓ mover, Ctrl+D, Delete, Ctrl+Z/Shift+Z, Ctrl+S, Ctrl+J, Ctrl+E, Ctrl+P, 1/2/3, T, ?).
- **Modelos**: em branco, landing de produto, portfólio, artigo de uma nota, site de uma pasta, currículo, evento, documentação e links (bio). “Salvar como modelo” guarda os seus (`.template.json`).
- **Assistente**:
  - `page_schema` descreve o formato.
  - `write_page` cria ou edita a página: valida antes de gravar, mostra o diff na aprovação e pode ser desfeito.
  - O botão ✦ do estúdio abre o Assistente já com a página como contexto.
  - Se a página mudar por fora enquanto está aberta, o estúdio atualiza (com Desfazer).
- **Onde achar**: Notas → Nova → Nova página; Notas → ⋯ → Páginas; paleta de comandos (“Páginas”, “Nova página”). Abrir um `.page.json` por qualquer caminho leva ao estúdio.
- **Correção**: arquivos `.json` (e outros não-Markdown) ganhavam a extensão duplicada no sync com a cidade (`X.page.json` → `X.page.json.json`).

## v0.43.0 — Agente mais minucioso (2026-09-27)

- Leitura sem cortes: `read_note` entrega a nota inteira sempre que ela cabe no orçamento, que acompanha o contexto do modelo. Antes o modelo pedia 200 linhas, recebia metade e respondia com o que tinha. Notas enormes vêm em partes, com um aviso claro de LEITURA PARCIAL.
- Se o modelo responde sem terminar de ler, ele é lembrado uma vez de ler o restante e revisar. A primeira resposta fica recolhida como “Resposta preliminar”.
- Novas ferramentas:
  - `grep_notes`: acha as linhas exatas em todas as notas, com caminho e número da linha. Ignora acentos e maiúsculas, aceita regex e mostra contexto.
  - `read_notes`: lê várias notas numa só chamada.
  - `update_plan`: mostra o plano do agente como uma checklist ao vivo.
- Leituras independentes pedidas no mesmo passo rodam em paralelo, e cada resultado aparece assim que fica pronto.
- Instruções do agente reforçam o trabalho minucioso: ler por inteiro, seguir links e backlinks, planejar tarefas longas.
- Os detalhes de cada ferramenta mostram o resultado inteiro, com rolagem.
- Notas: a lixeira não fazia nada com uma **pasta** selecionada (só notas eram consideradas, em silêncio) e o contador dizia “1 nota”. Agora excluir pasta funciona: tudo dentro dela, inclusive subpastas, vai para a Lixeira (restaurável) e a região sai da cidade. O contador mostra “1 pasta · 2 notas”, e as ações que só valem para notas ficam apagadas quando só há pastas selecionadas.

## v0.42.2 — Atualizações chegam sozinhas; Assistente não entra em loop (2026-09-27)

- O celular continuava rodando a versão antiga depois de uma correção publicada: o service worker servia tudo do cache e a versão nova só entrava ao tocar em “Recarregar”. Por isso Excluir e os outros botões de Notas seguiam quebrados mesmo após a v0.42.1.
- O service worker agora busca na rede primeiro (o cache fica para uso sem internet ou rede muito lenta, acima de 4 s) e assume assim que é instalado. Quem ainda está numa versão antiga recebe esta sozinho ao abrir o app (uma recarga automática, uma única vez).
- A troca de service worker não recarrega mais a página no meio da edição; o aviso “Nova versão” só aparece se a página aberta estiver realmente desatualizada.
- Assistente: o modelo às vezes estraga emoji no caminho (“Teste/� Gue.md”) e a leitura falhava. Agora a nota é encontrada comparando só letras e números (ou pelo título).
- Assistente: a mesma chamada que já falhou não é executada de novo; o modelo recebe um aviso para mudar de estratégia e, se insistir, o agente para com “Parei: o agente repetia uma ação que falhava” (em vez de uma fila de “falhou”).
- Assistente: modelos que encerravam só com raciocínio depois de usar ferramentas (ex.: gpt-oss) deixavam a pergunta sem resposta. Agora são lembrados uma vez de responder em texto.

## v0.42.1 — Correções no celular: Notas e abas (2026-09-27)

- Notas: tocar numa pasta a abria e fechava na mesma hora (a ação rodava no `pointerup`, a lista era redesenhada e o `click` seguinte caía na linha nova). Agora a ação acontece só no toque confirmado; o toque longo seleciona sem abrir a nota.
- Notas: a barra da seleção rolava para o lado e escondia Duplicar, Excluir e Limpar. Virou uma barra de ícones que cabe na tela, com a nova ação Mover para pasta.
- Editor: havia duas barras de abas empilhadas (a antiga ficava atrás da nova), criando uma faixa vazia e dividindo os toques. Ficou só uma, e o × de fechar ganhou área de toque de 32 px.

## v0.42.0 — Assistente agêntico, independente de modelo (2026-09-26)

- O chat antigo (um pedido ao OpenRouter com as notas coladas no prompt) deu lugar a agentes que trabalham no vault usando ferramentas.
- Provedores (`src/ai/providers.js`): OpenRouter, Anthropic (API nativa), OpenAI, Google Gemini, Ollama local e qualquer servidor compatível com OpenAI (LM Studio, vLLM, Groq…). Streaming SSE com chamadas de ferramenta nos dois protocolos, novas tentativas em 429/5xx, cancelamento e mensagens de erro claras (chave recusada, sem créditos, modelo sem ferramentas, contexto estourado).
- Ferramentas (`src/ai/tools.js`): visão geral do vault, listar, buscar, ler com números de linha, links e backlinks, tags, contexto do editor (nota aberta e seleção), criar, editar trecho exato, reescrever, acrescentar em seção, renomear/mover atualizando os [[links]], excluir para a lixeira, criar pasta, abrir nota e memorizar fatos.
- Loop agêntico (`src/ai/agent.js`): vários passos até concluir, com limite configurável; políticas “Pede aprovação”, “Edita sozinho” e “Só leitura” (exclusões sempre pedem confirmação); recusas voltam ao modelo para ele ajustar o plano; subagente Pesquisador para pesquisas amplas em contexto próprio; compactação do contexto preservando pares de ferramenta; proteção contra instruções escondidas em notas.
- Agentes: Assistente, Pesquisador, Escritor e Organizador, mais agentes personalizados com instruções e permissões próprias.
- Interface (`src/ai/ui.js`): cada ação vira um cartão com status; aprovação mostra o diff da mudança; parar a qualquer momento; desfazer todas as alterações de uma resposta (sem atropelar edições feitas depois); [[links]] da resposta abrem a nota; histórico de conversas; troca de agente, provedor e modelo (lista com preço, contexto e aviso de modelos sem ferramentas); consumo de tokens e custo; memória e instruções editáveis. Conversas e chaves ficam só neste aparelho (IndexedDB); a configuração antiga do OpenRouter é importada.
- Correção: abrir uma nota no editor Visual e sair dela reescrevia o arquivo (reformatação do Markdown, com linhas em branco entre itens de lista). Agora só grava quando há mudança real, e listas são serializadas sem linhas em branco.
- O service worker não intercepta mais requisições a outras origens (APIs de modelos), evitando respostas antigas do cache.

## v0.41.0 — Mundo vivo (2026-09-26)

- Novo gerador de mundo (`src/world/terrain.js`): continentes, cordilheiras, rios que descem ao mar, praias, e biomas derivados de elevação, temperatura (latitude e altitude) e umidade — campo, prado, floresta, mata fechada, pântano, taiga, tundra, neve, colinas, montanha, deserto, savana e estepe. Determinístico e infinito; o início é sempre um vale temperado.
- Arte pixel medieval gerada em código (`src/world/pixel-art.js`) com paleta única: texturas por bioma com bordas orgânicas, relevo sombreado, espuma na costa e profundidade na água, flores/pedras/juncos, árvores por espécie (carvalho, bétula, pinheiro, salgueiro, acácia, palmeira, cacto), com neve no frio.
- Construções em enxaimel com telhados, chaminés e janelas iluminadas; o material muda com o bioma (pedra e neve no frio, adobe no deserto, azul no litoral) e o tipo de arquivo vira um ofício (salão, oficina, tinturaria, torre, mercado).
- Regras de terreno: não se constrói em água, pântano, montanha ou neve eterna; ruas atravessam rios e lagos com pontes e custam mais em matas e colinas. O posicionamento explica o motivo (“Não dá: Rio”). A barra mostra o bioma sob a câmera, e novas casas nascem perto de onde você está explorando.
- Mapa grande mostra a região com biomas e relevo, na proporção do quadro.
- Chão gerado num Web Worker: explorar e saltar pelo mapa mantém 60 fps mesmo com CPU 4× mais lenta.
- Cidades existentes continuam válidas: casas antigas sobre terreno agora proibido ganham um lote aterrado.

## v0.40.0 — Redesenho, fase 3: a cidade (2026-09-26)

- Chão sem a “grade”: os tiles de grama e água traziam uma faixa escura herdada da folha de sprites; agora só a área limpa é usada, com contraste suavizado.
- Ruas desenhadas como caminhos contínuos que se conectam aos vizinhos (antes, ruas horizontais apareciam como blocos soltos).
- Nomes das casas e das pastas em pílulas com a fonte da interface; notas Markdown sem a extensão; a seleção virou um halo arredondado na cor de destaque.
- A cidade abre enquadrando as notas quando há casas fora da tela; novo comando “Enquadrar todas as notas”.
- Toda nota vira casa na hora, venha de onde vier (Notas, busca, lixeira), sem duplicar a casa criada pela própria cidade.
- Ruas agora são refeitas ao abrir o vault (antes sumiam depois de recarregar) e quando links mudam fora do editor.

## v0.39.0 — Redesenho, fase 2: escrever e encontrar (2026-09-26)

- Menu “/” no editor Visual: Título 1–3, Lista, Tarefa, Citação, Divisória, Link para nota e Link externo, com filtro por digitação e teclado (↑ ↓ Enter Esc).
- Bolha de formatação sobre o texto selecionado: negrito, itálico, código, transformar em link de nota, link externo, título e citação.
- Busca global reescrita (botão de busca e Ctrl/⌘+P): acha trechos de palavras e ignora acentos (“pao” acha “Pão”), ordena título antes de conteúdo, mostra o trecho com o termo destacado, lista recentes quando vazia e cria a nota direto da busca.
- Dicas de primeiro uso: ao voltar à cidade sem ligações, mostra como ligar notas com [[; avisa quando a primeira rua é construída.
- Correções no editor Visual: o clique não desfaz mais a seleção de texto; a divisória (---) funciona no modo Visual; blocos criados ao sair de uma lista não perdem o conteúdo ao salvar.

## v0.38.0 — Redesenho, fase 1: casca escura focada (2026-09-26)

- Sistema visual único (`src/styles/theme.css`): paleta escura neutra com um acento, tipografia do sistema (monoespaçada só em código), escala de espaçamento e raios, sombras suaves. Os tokens antigos passam a apontar para a nova paleta.
- Ícones SVG consistentes (`src/ui/icons.js`) no lugar de glifos Unicode que variavam por aparelho.
- Diálogos próprios (`src/ui/dialogs.js`): folhas inferiores no celular e cartões centrais no desktop, com validação e teclado (Enter/Esc). Nenhum `prompt()`/`confirm()`/`alert()` nativo restante.
- Navegação: dock com Cidade, Notas e Assistente (vira trilho lateral no desktop); barra superior flutuante com busca, mapa e configurações; o botão + só aparece na cidade.
- Notas: cabeçalho enxuto (Nova, ⋯), busca, estados vazios explicativos, Recentes/Favoritos/Composições/Lixeira no menu ⋯, restauração direta da lixeira.
- Editor: voltar, título, Visual/Fonte e menu ⋯ com semântica clara; barra de formatação legível (H1–H3, B, I, ícones); abas só com duas ou mais notas; texto em coluna confortável.
- Primeiro acesso: cartão explica a cidade e cria a primeira nota; notas criadas pela cidade abrem direto no editor.
- Removidas regras legadas que forçavam cantos retos, fundos azulados e aviso “N arquivo(s) editável(is)” ao abrir.

## v0.37.1 — Correções de integridade do editor e do save (2026-09-26)

- Editor Visual: o texto digitado agora chega ao DocumentStore; antes a reconciliação do save descartava a edição e a nota voltava vazia ao recarregar.
- O aviso “Esta nota está vazia.” passou a ser só visual (CSS) e não é mais gravado nem misturado ao texto digitado.
- Excluir uma nota aberta no editor não a recria mais ao fechar/desfocar o editor.
- Operações pendentes no debounce do save são gravadas ao fechar o app ou ir para segundo plano.
- A primeira instalação do service worker não recarrega mais a página (o que descartava o que foi criado nos primeiros instantes).
- Abrir um documento por qualquer comando fecha o Explorer móvel; a paleta não lista comandos duplicados.

## v0.37.0 — Vault único e integração móvel (2026-09-26)

- O app abre diretamente um vault `Urbe`; cidades legadas são copiadas para `Cidades/<nome>` sem apagar as origens, com marcador de migração retomável.
- O carregamento real agora alimenta DocumentStore, composição e persistência; a gravação usa o conteúdo canônico e persiste IDs de documentos.
- Recuperação do journal reaplica arquivos, exclusões e metadados físicos antes de removê-lo; links e tags acompanham edições.
- Explorer móvel ganha criar nota/pasta, escolher pasta física, retomar composições e tolerância de gesto; o menu “+” expõe região e construção.
- Links da nota visual abrem com toque; operações de mover, duplicar, excluir e restaurar alinham mundo e documentos por ID.
- Composição reconhece frontmatter, listas, código e tabelas; estilos de bloco sobrevivem à inserção anterior, fonte/alinhamento refletem controles; CSS e HTML são editáveis e imagens locais podem ser embutidas na exportação.
- Removidos scripts duplicados, atualizado cache offline e incluídas regressões de integração no CI.


## v0.35.1 — Workspace Integrity (2026-09-26)

- Removed Burgo completely from runtime, navigation, cache, architecture and tests.
- Added crash-safe workspace journal with authoritative recovery after interrupted writes.
- Added bounded persistent document revision history.
- Enforced stable document identity across rename/move and legacy UI actions.
- Routed legacy note deletion through the recoverable trash.
- Replaced permanent shell polling with event/MutationObserver-driven updates.
- Made the final physical save state derive Markdown from DocumentStore rather than world buildings.
- Routed legacy road semantics through KnowledgeIndex instead of parsing building content.
- Added workspace diagnostics for persistence, documents, knowledge and aquarium projection.
- Rewrote architecture invariants around a single canonical workspace authority.

## v0.34.0 — Runtime Migration Complete (2026-09-26)

- Moved aquarium frame scheduling out of the monolith into AquariumRenderer + shared Scheduler.
- Replaced the monolithic pointer gesture block with a reusable mobile-first TouchController.
- Canvas drawing functions remain a compatibility rendering adapter while canonical state lives outside app.js.
- Burgo and pedestrian simulation ticks share the scheduler instead of independent perpetual timers.
- Added runtime adapter tests and offline cache coverage.
- The legacy app is now a compatibility/UI adapter over canonical workspace, persistence, knowledge, aquarium and simulation services rather than the data authority.

## v0.33.0 — Aquarium World Core (2026-09-26)

- Added AquariumWorld as a document-driven runtime projection.
- Added incremental RoadGraph derived from the canonical KnowledgeIndex.
- Wiki-link changes now invalidate semantic edges instead of defining workspace state through road tiles.
- Added a shared visibility-aware scheduler foundation for later timer consolidation.
- Added BurgoProjection as an optional downstream consumer of documents; it never writes note content.
- Legacy road routing is now gated by the canonical semantic road graph while tile routing is migrated.
- Added automated tests for aquarium projection, semantic roads and Burgo isolation.

## v0.32.0 — Persistence Core and World Projection (2026-09-26)

- Added a canonical workspace persistence service with pluggable storage adapters.
- Vault loading now hydrates canonical documents before the aquarium is projected.
- Markdown save state is derived from DocumentStore instead of buildings.
- Added a WorldProjection service that owns spatial metadata without owning note content.
- Spatial state remains isolated in .urbe/mapa.json while Markdown remains clean.
- The legacy FSA/IndexedDB layer now acts as a persistence adapter during migration.
- Added world enable/disable and spatial move commands.
- Verified that documents can load, change and save with the aquarium disabled.
- Added automated persistence/world separation tests.

## v0.31.0 — Mobile-first Explorer Core (2026-09-26)

- Added a document-first Explorer tree independent from aquarium buildings and regions.
- Added canonical multi-selection, expanded folders, favorites and recent documents.
- Added canonical rename, move, duplicate and delete commands.
- Added a mobile-first full-screen Explorer with 44px+ touch targets, long-press selection, optional haptic feedback and a thumb-reachable bottom action bar.
- Large screens derive from the mobile surface as a side panel instead of using a separate desktop-first implementation.
- Routed the file-browser shortcut through the canonical Explorer.
- Added compatibility projection for rename/move/delete while WorldSystem is still legacy.
- Added automated Explorer tests for hierarchy, multi-selection, favorites, recent files and file operations.

## v0.30.0 — Editor Workspace (2026-09-26)

- Added document back/forward navigation independent from aquarium navigation.
- Added local editor session persistence for open tabs, active document and navigation history.
- Added find/replace services and a responsive editor find bar.
- Added split-view state with open, close, swap and bounded ratio commands.
- Added a safe secondary split pane that renders canonical document content without creating a second competing editor state.
- Added shortcuts for find, navigation and split view through the shared keymap.
- Added automated tests for navigation, find/replace, persistence and split state.

## v0.29.0 — Editor Core (2026-09-26)

- Added an editor session independent from the aquarium/runtime.
- Added logical multi-document tabs with activation, closing and pin-ready state.
- Added bounded document history with canonical undo/redo.
- Added reusable editor context with Markdown outline, outgoing links and backlinks.
- Connected the legacy editor to canonical editor sessions without replacing the current editing surface.
- Added visible document tabs and a responsive outline/backlinks context panel.
- Added `editor.undo`, `editor.redo`, `editor.closeTab` and `editor.contextPanel` commands.
- Added automated editor tests for tabs, history, outline and backlinks.

## v0.28.0 — Canonical documents and knowledge index (2026-09-26)

- Added a canonical `DocumentStore` independent from city buildings and rendering.
- Documents now expose stable path, content, properties, tags and wiki-links.
- Added an in-memory knowledge index for local search, tags, outgoing links and backlinks.
- Opening an existing vault projects its Markdown files into the canonical document model.
- Legacy editor changes synchronize into canonical documents while compatibility remains.
- Added `document.open`, `document.update`, `document.remove` and `workspace.search` commands.
- Added indexed Quick Open on `Ctrl/Cmd+P`.
- Added automated tests for documents, wiki-links, backlinks, tags, search and reindexing.

## v0.27.0 — Workspace Core foundation (2026-09-26)

- Reframed the runtime around a local-first workspace core; the city is an integrated spatial aquarium rather than the authority over documents.
- Added `EventBus`, `CommandRegistry`, `StateStore` and service registry in `src/core/core.js`.
- Added the first compatibility bridge for navigation, document creation, folder creation and workspace saving.
- Added behavioral core tests and CI syntax coverage.
- Fixed the v0.26 shell extraction: restored valid `shell.css` content and loaded it from `index.html`.
- Added the core asset to the offline application shell.
- Added a unified keyboard shortcut registry and searchable Command Palette, sharing the same canonical commands used by the runtime.
- Added responsive Command Palette UI for desktop and mobile.

## v0.26.0 — 2026-09-26

Fundação estrutural para desmontar o monólito sem alterar o produto.

- O `index.html` caiu de ~771 KB para ~115 KB: CSS e JavaScript foram extraídos para `src/`, mantendo a ordem original de execução e cascata.
- O runtime principal agora vive em `src/app.js`; estilos foram separados em base e shell.
- O service worker foi versionado e passou a pré-cachear todos os novos assets, preservando instalação e uso offline.
- Adicionado `ARCHITECTURE.md` com princípios, fronteiras-alvo e ordem segura de decomposição por domínio.
- Adicionada verificação automática no GitHub Actions: sintaxe JavaScript e invariantes estruturais/PWA.
- `window.URBE` e `window.Burgo` continuam como contratos de diagnóstico para a próxima etapa.
- Regra de manutenção: novas funcionalidades não devem voltar ao padrão de patches de versão sobrepostos no fim do runtime.

## v0.25.0 — 2026-09-10

Fase 2 do Burgo: os moradores saem de casa.

- **Pedestres nas estradas.** Cada nota ligada a outra manda gente andar entre
  as duas, com a cor da guilda do bairro. Nota órfã não recebe ninguém — a rua
  vazia é o diagnóstico.
- **Andam por estrada, não por atalho.** O caminho é uma busca em largura sobre
  a malha viária que já está desenhada. O A* do terreno não servia: ele recusa
  justamente o vão de acesso da casa, que é por onde a pessoa sai.
- **Densidade pelo número de wiki-links** e teto de andarilhos proporcional ao
  ânimo do burgo: vila desanimada tem menos gente na rua.
- **Orçamento de CPU como restrição de projeto**: teto de 18 caminhantes, só os
  visíveis na tela, rotas em cache, no máximo quatro rotas novas por ciclo,
  animação a 12 quadros e nada de desenhar abaixo de certo zoom. Quadro completo
  medido em 2,9 ms. A animação pausa quando o mapa não está à vista.
- Nada disso vai para o disco: o vaivém é leitura do vault, não estado de jogo.

Ajuste da v0.24
- **Piso de ânimo em 15%.** Ausência longa desanima o burgo, mas não o mata:
  voltar precisa ser retomada, não ressurreição.

Interno
- `window.URBE` expõe leitura de estado para o harness de testes — o app inteiro
  vive dentro de uma IIFE e não havia como verificá-lo de fora.

## v0.24.0 — 2026-09-10

Primeira camada de simulação: **o Burgo**. Um sim idle medieval que roda
sobre a cidade, com três regras de projeto inegociáveis.

Regras
- A simulação **nunca escreve nas suas notas**. O vault é a geografia e a
  economia do reino: é lido, jamais alterado.
- O estado do burgo mora no IndexedDB, por vault. Não entra no mapa.json e
  não gera gravação em disco a cada tique.
- Nada de pontos inventados. O vigor de um morador é a data de edição da nota
  dele; a vila prospera quando você escreve e definha quando você abandona.

O que existe
- Relógio do reino: um dia a cada 15 minutos reais. Ao voltar, todos os dias
  de ausência são resolvidos, sem teto — 180 dias fora resolveram 17.280 dias
  do reino em pouco mais de 3 segundos de boot.
- Moradores: cada nota ganha um habitante com nome gerado, ofício, nível e
  ânimo. Nota nova traz morador; nota apagada, despedida na crônica.
- Guildas por bairro: a pasta define o ofício (Lavradores, Lenhadores,
  Pedreiros, Ferreiros, Escribas, Mercadores, Curandeiros, Guardas). Nota na
  raiz tira o ofício do próprio nome, para um vault plano não virar uma vila
  de clones.
- Economia: trigo, madeira, pedra, moedas e inspiração. Cada morador come; o
  celeiro pode secar. Nota sem edição há meses derruba a produção dela.
- Inspiração: gerada pelo que você escreve de verdade no editor.
- Crônica: registro em prosa do que aconteceu — colheitas, fome, telhados que
  cedem em notas esquecidas, feira semanal com a nota mais viva.
- Painel próprio no dock (5º destino) com recursos, moradores e crônica.
  Tocar num morador abre a nota em que ele mora.
- `window.Burgo` expõe o estado para inspeção no console.

## v0.23.0 — 2026-09-09

Redesenho da casca do aplicativo. O motor não mudou: tudo abaixo delega para
as funções que já existiam.

Navegação
- Dock inferior com quatro destinos — Mundo, Arquivos, Assistente, Vaults —
  dentro da zona de alcance do polegar. A barra lateral esquerda saiu.
- Uma única ação primária visível (botão ＋ flutuante), com menu de criação
  por pressão longa: arquivo, pasta, desenhar pasta no mapa, importar.
- Barra superior flutuante com o nome do vault, o ponto de estado da gravação
  e os dois atalhos raros (mapa e configurações). O minimapa vivo, o HUD de
  contadores e o selo de sincronização saíram da tela: os números aparecem no
  mapa grande, sob demanda.
- Nenhuma ferramenta fica ativa sem saída à vista: ao entrar em "desenhar
  região" ou "construir", uma faixa explica o passo e oferece Cancelar.

Superfícies
- Diálogos viraram bottom sheets com alça, largura total e botões de 50px:
  nova região, novo arquivo, configurações, contexto da IA e menu de ações.
- A ficha do arquivo virou folha inferior, com a ação principal em destaque.
- O toast virou snackbar acima do dock, sem cobrir conteúdo.
- Painéis-destino convivem com o dock; só o editor entra em modo imersivo.

Editor
- Cabeçalho: voltar (←), nome do arquivo, controle segmentado Visual/Fonte e
  menu de ações (⋮). O ícone críptico de alternância e o texto de gravação
  saíram do cabeçalho — o estado de salvamento foi para o rodapé.
- A barra de formatação voltou ao modo visual, agora funcional: títulos,
  citação e listas trocam o tipo do bloco preservando negrito, links e
  wiki-links; negrito e itálico agem sobre a seleção.
- Corpo do texto em 15px com entrelinha 1.65.

Bug encontrado no caminho
- A v0.20 desligou o action sheet com `display:none!important` e nunca o
  religou. Desde então "Mover" no Explorador, o seletor de destino e o de
  importação (pasta ou arquivos) abriam invisíveis: o toque não fazia nada.
  Religado.

Toque e acessibilidade
- Todos os alvos com no mínimo 44px; espaçamento em múltiplos de 4.
- Áreas seguras (notch e barra de gestos) respeitadas no topo e no rodapé.

## v0.22.0 — 2026-09-09

Correções sobre a v0.21, todas aplicadas como um bloco de patch no fim do
script (mesma convenção das versões anteriores) mais um bloco de CSS.

Bugs corrigidos
- Abrir um arquivo pelo Explorador deixava o painel do Explorador (tela cheia,
  z-index 65) por cima do editor (z-index 50): o editor abria invisível e
  nenhum botão do cabeçalho recebia toque. Agora o Explorador fecha ao abrir.
- A barra de abas do editor era injetada sem linha de grid e caía no rodapé,
  sobre a barra de status. Ganhou linha própria e some quando há um só arquivo.
- A toolbar Markdown continuava visível no modo visual e seus botões escreviam
  no textarea escondido, injetando o texto cru no arquivo.
- O texto "Salvando..." mudava a largura do cabeçalho entre o toque e o soltar
  do dedo, e o botão de alternar fonte/visual escapava do dedo.
- O template `# ` era renderizado como parágrafo, então o primeiro texto
  digitado saía como `#Texto` em vez de virar título.
- Os botões de mover e excluir do Explorador ficavam sempre visíveis: o
  atributo `hidden` perdia para o `display:grid` da classe.
- Dois links no mesmo parágrafo quebravam o HTML — o regex de itálico casava
  de um `target="_blank"` ao outro.
- Links `javascript:`, `vbscript:` e `data:` não-imagem passavam direto para o
  preview; nomes de nota não eram escapados no autocompletar em modo fonte.
- `mapa.json` era regravado a cada 120 ms de digitação porque o campo `salvo`
  sempre mudava. Agora só grava quando o conteúdo do mapa muda de verdade.
- Ao abrir um vault, o snapshot de binários era zerado e não semeado: todas as
  imagens e PDFs eram relidos e reescritos no primeiro sync.
- HUD e selo de sincronização colidiam com o minimapa.
- O seletor de ordenação de modelos da IA estava escondido no celular.

Desempenho
- Abrir um vault de 120 notas com wiki-links: de 103 s para 6,2 s.
  - `routeAStar` usava `open.sort()` a cada passo; agora usa heap binário.
  - `roadComponentFrom` refazia um BFS da malha inteira a cada consulta; agora
    os rótulos de componente são reaproveitados enquanto a malha não cresce.
  - `tileBlockedByBuilding`, `isHouseAccessGap` e `loteValido` varriam
    `world.buildings` inteiro; passaram a usar o índice por chunk.
  - `ehAgua` ganhou memória para o ruído do terreno.

## v0.21.0 — 2026-09-09

- Refeito o menu principal para linguagem visual pixel art, com versão visível, lista de vaults, criação pelo botão `+`, exclusão segura e indicador de carregamento.
- Corrigida a exclusão do vault atual para interromper a sincronização antes da remoção.
- Persistência local reorganizada: armazenamento interno automático e pasta escolhida pelo usuário lembrada quando o navegador oferece File System Access.
- HUD lateral compactado, recolhível, sem textos redundantes e com acesso aos vaults por ícone de porta.
- Seleção virou o estado padrão quando nenhuma ferramenta lateral está ativa.
- Regiões: clique abre edição de forma; pressionar/segurar abre configurações; criação começa com campos vazios; nomes vazios e duplicados recebem numeração automática.
- Construção mobile-first em duas etapas: posicionar, confirmar/cancelar e só então configurar o arquivo.
- Cópias passam pelo mesmo fluxo de posicionamento e configuração.
- Arquivos editáveis de primeira classe: `.md`, `.txt`, `.html`, `.js`, `.css`, `.json`, `.yaml/.yml` e `.csv`; sem extensão usa `.md`.
- Nomes duplicados seguem `arquivo (n).ext`.
- Construções recebem aparência diferenciada conforme o tipo de arquivo.
- Explorador redesenhado no estilo visual do jogo, usa o nome do vault na raiz e recebeu pesquisa integrada.
- Criação no Explorador usa o mesmo pipeline do Mundo e não abre o editor automaticamente.
- Importação permite escolher arquivos ou uma pasta completa quando a API do navegador permite.
- Multiseleção refeita; pressionar/segurar um item inicia a ação de mover sem depender de drag-and-drop.
- Editor passou a adaptar ferramentas conforme a extensão, ganhou abas múltiplas, ferramentas de código e preview HTML isolado.
- Troca de visualização não dispara salvamento por si só; o salvamento só ocorre quando o conteúdo realmente muda.
- Template Markdown vazio reduzido a `# `; outros tipos nascem vazios.
- Assistente de IA redesenhado com contexto Local / Selecionado / Global, seletor de arquivos, histórico expansível e interface minimalista.
- Configurações globais da IA foram separadas das orientações/memórias locais de cada arquivo.
- Seletor de modelos recebeu ordenação e preço visível; saldo disponível pode ser sincronizado com o provedor configurado.
- Área local de IA recebeu seção de artefatos para revisão/aprovação antes de entrada no mundo.
- Limite de zoom-out ampliado e conflito com o handler legado de roda corrigido.
- Geração visual do terreno recebeu variação de biomas de baixa frequência sem alterar a topologia persistida.
- Mapa foi redesenhado para representar terreno, água, estradas, regiões, construções e viewport em vez de blocos abstratos.
- Fluxos de abertura/salvamento/importação foram unificados para evitar divergência entre Mundo, Explorador e Editor.
