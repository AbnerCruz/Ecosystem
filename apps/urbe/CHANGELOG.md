# Urbe — Changelog

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
