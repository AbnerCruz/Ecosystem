# Urbe — Changelog

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
