# Lunet — Reconciliação definitiva da experiência de uso

**Missão de implementação para o agente · 26/11/2026**
**Componente:** `AbnerCruz/Ecosystem/apps/lunet2d`
**Base consultada:** [`main`, f22eb24586f822d1104573a8b2b3aa55cb63a624](https://github.com/AbnerCruz/Ecosystem/commit/f22eb24586f822d1104573a8b2b3aa55cb63a624), merge do [#1052](https://github.com/AbnerCruz/Ecosystem/pull/1052) (IA sem jogo, configurar IA, pasta de projetos e conexões na casca). Comparação com o snapshot de 23/11 `94230eab`: 232 commits à frente, segundo a API do GitHub. Revalidar a base ao assumir a tarefa.
**Natureza:** especificação de trabalho e critérios de aceite, não relato de implementação. Não é novo design system, nem reescrita do motor. A presente missão é restaurar a organização aprovada e reconciliá-la com as decisões posteriores, corrigindo as regressões efetivas no Lunet.

## 0. Ordem do proprietário

Estude. Estude os arquivos antigos, o estudo de UX de 26/11, a implementação atual, a experiência real do Lunet e as decisões registradas no Ecosystem. A organização demonstrada no protótipo de 23/11 foi aprovada; o acabamento da implementação posterior foi rejeitado. A linguagem visual comum do Ecosystem de 26/11 deve permanecer. Recupere o que funcionava na organização anterior, aplique a linguagem visual vigente e preserve os avanços técnicos e funcionais do produto. Não confunda trocar cores com projetar uma experiência de uso.

A tarefa termina com a interface funcional e integrada, não com outra lista de sugestões, capturas de um mockup, uma nova versão de HTML ou uma promessa de migração. Faça as correções no cliente real, com testes, evidências e release conforme as regras vigentes. Não restaure telas antigas por nostalgia. Não faça um segundo aplicativo ou um segundo editor. Não remova recursos para tornar a tela minimalista: organize-os segundo sua finalidade.

## 1. Hierarquia das fontes e leitura obrigatória

### 1.1 Autoridades vigentes — leia antes de editar

1. [`MANIFEST.md`](https://github.com/AbnerCruz/Ecosystem/blob/main/MANIFEST.md), NN, [`AGENTS.md`](https://github.com/AbnerCruz/Ecosystem/blob/main/AGENTS.md), `ecosystem.json`, decisões e adendos aplicáveis; [`protocolo de estudo e desenvolvimento`](https://github.com/AbnerCruz/Ecosystem/blob/main/docs/governance/protocolo-estudo-desenvolvimento.md).
2. [`ADD-0037 / DEC-0069`](https://github.com/AbnerCruz/Ecosystem/blob/main/docs/governance/addenda/ADD-0037-linguagem-visual.md), [`linguagem-visual.md`](https://github.com/AbnerCruz/Ecosystem/blob/main/docs/design/linguagem-visual.md), [`tokens.json`](https://github.com/AbnerCruz/Ecosystem/blob/main/docs/design/tokens.json), [`estudo visual de 26/11`](https://github.com/AbnerCruz/Ecosystem/blob/main/docs/design/estudo-visual-2026-11-26.md), [vitrine oficial](https://abnercruz.github.io/Ecosystem/design/). O adendo explicitamente **não modifica as decisões de produto nem os dados do usuário**.
3. [`ADD-0036 / DEC-0068`](https://github.com/AbnerCruz/Ecosystem/blob/main/docs/governance/addenda/ADD-0036-lunet-casca-avalonia.md), [`ADR 0013`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/adr/0013-casca-unica-avalonia.md), [`plano da casca Avalonia`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/plans/lunet-casca-avalonia-2026-11-25.md), [`inventário de migração`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/plans/inventario-migracao-casca-2026-11-25.md), [`estudo de design de 25/11`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/research/design-produto-2026-11-25.md) e [`estudo do teclado`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/research/teclado-nativo-ide-2026-11-25.md).
4. [`AGENTS.md local`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/AGENTS.md), [`SPEC`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/SPEC.md), [`ROADMAP`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/ROADMAP.md), [`DEVELOPMENT`](https://github.com/AbnerCruz/Ecosystem/blob/main/apps/lunet2d/docs/DEVELOPMENT.md), `AgentsChat.md`, `OWNER-ORDERS-STATUS.md` se existente, handoffs, PRs e CI. Audite o estado efetivo, não copie o status de um checklist antigo. Leia ADD-0035/DEC-0065 para integração e validação; ADD-0027/ADR-0033 para IA; ADRs code-first e armazenamento pertinentes.

### 1.2 Materiais aprovados de 23/11 — referência de arquitetura da informação

- `Lunet-estudos-completos.md`: auditoria Android, produto, editor, Studios, Aprender, IA, Launcher e documentação editorial. 65.845 bytes.
- `lunet_previa_interativa.html`: protótipo original responsivo. 79.114 bytes. As páginas são `home`, `launcher`, `learn`, `editor`, `studios`, `docs`, `explore`. A IA é transversal.
- `Lunet-plano-de-desenvolvimento.md`: fases F1–F8, contratos comuns de navegação/dados e apêndice de fontes. 68.906 bytes.
- `Captura mobile do protótipo` e `estudo em PDF`. Links de biblioteca pessoal podem não estar acessíveis ao agente externo. Se não abrir, não invente conteúdo nem bloqueie a execução: os contratos relevantes foram consolidados neste documento. Peça os originais somente se uma dúvida visual específica não puder ser resolvida pelas fontes e regras vigentes.
- Histórico: [#1024](https://github.com/AbnerCruz/Ecosystem/pull/1024) recuperou F1; [#1023](https://github.com/AbnerCruz/Ecosystem/pull/1023) trata de T01; [#1042](https://github.com/AbnerCruz/Ecosystem/pull/1042) trata da casca única e design; [#1052](https://github.com/AbnerCruz/Ecosystem/pull/1052) é evolução posterior. Não reverta o conjunto desses PRs para recuperar aparência.

### 1.3 Precedência e fronteiras

- **26/11 rege aparência, componentes, tipografia, movimento, cores e acessibilidade.** A linguagem de produto de 23/11 rege a organização quando não contradiz decisões posteriores explícitas. O plano de 25/11 rege a arquitetura do cliente. O código atual prova o que de fato existe, não tem autoridade para revogar uma ordem do proprietário.
- O HTML é referência de composição, navegação, hierarquia e retorno. Não copiar slogan, dados de amostra, gradientes, menta, cores, cards, CSS, simuladores, contadores fictícios ou execução simulada. Não converter HTML em WebView. Não preservar pixels antigos em oposição ao design system novo.
- A casca única `Lunet.Client` em Avalonia é definitiva. `Lunet.Android` e `Lunet.Desktop` são heads; `io.lunet.studio` mantém identidade e atualização por cima. `MainActivity.*` é ponte de migração, não destino para UI nova. Framework, Host, Runtime, Compiler, Studio e Core continuam com as responsabilidades próprias. Nenhuma dependência de outro Product, nenhum novo pacote compartilhado sem NN-022.
- O documento de consolidação arquitetural do Framework de 24/11 (imports, contexto, ordenação) não é autorização para refazer APIs gráficas nesta missão. Não desfazer melhorias do Framework, nem deslocar escopo para novos jogos, cursos, engines, funcionalidades de IA ou estúdios inéditos. Corrigir bugs e lacunas de integração que impeçam o fluxo existente.
- O nome do produto é Lunet, não “Lunet Novo”. O usuário deseja ferramentas profissionais no celular, não um painel de demonstração de funcionalidades.

## 2. Diagnóstico confrontado com a main

As constatações abaixo são de inspeção estática da base indicada, não testes executados no APK. Elas definem pontos de investigação e testes de regressão. Se algum já tiver sido corrigido ao iniciar, prove a correção e preserve-a.

| Evidência no código consultado | Consequência / ação |
|---|---|
| `HomeView.Build` (53–97) monta título “Seus jogos”, contagem, grade, lixeira, FAB; `BuildNav` (99–124) tem Jogos/Aprender/Jogar/Studios/Mais no Android e adiciona Biblioteca e Ajustes separadamente quando `OpenRoute == null`. | Home é quase só catálogo de jogos; recuperar continuidade, busca, fixados/recentes e distinção entre área de trabalho e jogar. A topologia de destinos não deve depender de uma ponte legada existir. |
| `HomeView.ShowMore` (153–187) oferece IA, Biblioteca, Explorar e Ajustes. Não há Documentação nesse menu. Explorar depende de `OpenRoute`. | Tornar Docs acessível globalmente; manter Explorar como destino existente sem ampliar catálogo durante esta missão; mesmas rotas conceituais nas plataformas, indisponibilidades reais explícitas. |
| `HomeView.ShowLearn/ShowGlobalAi/ShowPlay/ShowStudios/ShowLibrary` (403–469) recriam views e substituem `_overlay`; `ShowStudios` publica via `ShowLibrary().ShowPublishStudio`. | Risco de perder posição, seleção, conversa visível e contexto de origem; testar reentrada e publicação/cancelamento. Usar navegação de destinos com histórico, não pilha de overlays tratada como páginas sem contrato de retorno. |
| `ShellState` mantém dois níveis, aba e painel, tab history; `ExpandedMinWidth = 840`. `WorkspaceView.Tabs` apresenta quatro itens: Código, Jogo, Recursos (Art/Map/Sound) e Notas. | Preservar agrupamento mobile em Recursos, que atende limite de navegação; manter as seis vistas conceituais do ADR 0013 acessíveis. Não transformar cada função em aba global. |
| `WorkspaceView` guarda `_code`, `_notes`, `_game` e `_support`; `SetKeyboardOpen` oculta barra e FAB, preserva folha quando se digita dentro dela; `ReloadFromDisk` protege documento dirty. | Boa fundação: manter instâncias pesadas e foco, não reconstruir editor por toque/resize/IA. Verificar caminhos de `Rebuild`, Back e salvamento antes de alterar. |
| `SupportPanelView.More` possui IA, Comandos, Documentação, Inspector, Desempenho, Console, Problemas, Git, APK e Conexões; IA e Docs são instâncias conservadas. | Não eliminar ferramentas; separar apoios frequentes de operações administrativas. Acrescentar acesso direto à IA e à missão sem depender de um menu profundo; painel compacto não modal e expansão voluntária. |
| `LearnView.OpenChallengeGame` (319–326) fecha Aprender, abre jogo; se há referência, abre Docs e navega para a página. Não há `SupportPanel` de missão no enum consultado. | Lacuna estrutural: referência não é objetivo. Reintroduzir contexto persistente de missão, checklist e retorno ao curso no mesmo editor. |
| `StudiosView` oferece armazenamento independente e caminhos de Sprite, Tile, Música, Efeitos, Partículas, Dados, Paleta e WAV. | Preservar capacidade existente, reconciliar apresentação e fluxo global/projeto. Verificar lifecycle e crash de áudio no código real; não confundir galeria de cartões com estúdio concluído. |
| `LunetShell` possui storage e hooks para pastas, áudio, jogo, settings, IA; `HomeRoutes` ainda declara ponte. | Reaproveitar integrações; rotas restantes são dívida identificável, não licença para duplicar UI. |
| `DocumentEditor` usa `CodeSurface`, `NotesSurface`, `CodeBar`, análise assíncrona, estado por documento. O `CodeBar` da main documenta ordem de 25/11: digitação pelo teclado do sistema, três abas sobre ele; Métodos substitui o teclado enquanto aberto. O ROADMAP e o estudo D2 ainda descrevem teclado próprio integral. | Existe divergência entre história, código e docs. Não trocar silenciosamente a política de teclado como consequência desta missão. Ver §6.3. Preservar AvaloniaEdit, Roslyn, undo, seleção, atalhos e catálogo. |
| `LunetTokens` declara Inter, JetBrains Mono, escala e movimento; ADD-0037 exige adoção por **todos** os controles, não apenas os tokens. | Conferir estilos e superfícies reais de cada tela, inclusive Fluent defaults e rotas legadas. Verificar contraste de pares efetivamente usados e transparências compostas. |

Referências de código fixadas: [HomeView](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/HomeView.cs), [ShellState](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Shell/ShellState.cs), [LunetShell](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/LunetShell.cs), [WorkspaceView](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/WorkspaceView.cs), [SupportPanelView](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/SupportPanelView.cs), [LearnView](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/LearnView.cs), [StudiosView](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/StudiosView.cs), [DocumentEditor](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/DocumentEditor.cs), [CodeBar](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Views/CodeBar.cs), [LunetTokens](https://github.com/AbnerCruz/Ecosystem/blob/f22eb24586f822d1104573a8b2b3aa55cb63a624/apps/lunet2d/src/Lunet.Client/Ui/LunetTokens.cs).

## 3. O que conservar, atualizar ou descartar do estudo antigo

| Direção de 23/11 | Tratamento em 26/11 |
|---|---|
| Home organiza, Launcher roda, IDE edita, Studios produzem, Aprender orienta. | Conservar integralmente a separação semântica. |
| Documentação por tarefa, símbolo e conceito; Explorar independente; IA global e contextual. | Conservar. Não empilhar como cartões de propaganda na Home. |
| Editor como destino acessível quando um projeto está ativo. | Adaptar ao nível Workspace do ADR 0013; entrar por projeto, nunca criar editor sem projeto. |
| Teclado contextual, autocompletar, signature help, undo e métodos por namespace. | Conservar capacidades. Aplicar a decisão de teclado mais recente, não D2 de modo automático. |
| Modo escuro, menta/roxo, tipografia e estilo do HTML. | Descartar valores do protótipo; usar exclusivamente Ecosystem v1.0. |
| Onboarding e preferências de aparência, inclusive tema claro. | Conservar pasta, acesso a dados e preferências de leitura; não reintroduzir tema claro como default nem inventar paletas. Um tema alternativo depende de decisão do design system; manter dados/preferências anteriores sem apagar. |
| `MainActivity.*`, EditText e teclado de Views nativas. | Substituídos pela casca Avalonia e AvaloniaEdit; usar código histórico apenas para recuperar capacidade e contratos. |
| F1: resgatar commits #1018/#962. | Histórico. Verificar equivalente atual; não repetir merge histórico. |
| F2–F8: plano amplo para desenvolvimento. | Reconciliar por comportamento observável. Não reexecutar todas as fases, nem declarar que estão completas só porque código foi criado. O recorte atual é UX de produto e continuidade. |
| Fluxo de jogos, assets, biblioteca, arquivos reais, progresso, IA, saves. | Invariantes. Sem duplicação de dados, migração desnecessária ou regressão funcional. |

## 4. Arquitetura da informação — contrato de produto

**Princípio:** cada destino resolve um objetivo, não representa uma coleção aleatória de features. A navegação global não é a navegação de um projeto. Um fluxo não modal de apoio não é uma nova página global. A interface deve permitir saber onde estou, o que estou fazendo, o que está ativo e como voltar.

### 4.1 Mapa de navegação proposto para execução

A organização abaixo reconcilia o protótipo com o ADR 0013 e o limite de 3–5 destinos mobile. Rótulos e agrupamentos são especificações de implementação desta missão; preservar decisões explícitas mais recentes se houver incompatibilidade comprovada. Não criar um ADR concorrente apenas para renomear um item.

```text
LUNET (global, sem jogo obrigatório)
  Área de trabalho  → criar / importar / continuar / organizar jogos
  Jogar            → biblioteca jogável → partida dedicada → volta à grade
  Aprender         → trilhas → curso → missão → workspace do jogo + objetivos
  Studios          → ferramenta → rascunho pessoal → salvar/publicar
  Mais             → Biblioteca | Documentação | Explorar | Lunet IA | Ajustes

WORKSPACE (um jogo identificado por GameId)
  Código           → arquivos, abas de documentos, editor
  Jogo             → preview, rodar/parar, inspeção e execução
  Recursos         → Arte | Mapa | Som | outros assets existentes
  Notas            → notas, esboços, mapas mentais existentes
  Apoios           → Console | Problemas | Objetivos (quando missão ativa)
                      IA | Docs | Comandos | Inspector | Desempenho
  Operações        → Git / Gerar APK / Conexões / ações do jogo
```

No celular, barra global com **Área de trabalho, Jogar, Aprender, Studios, Mais** (usar rótulo curto “Projetos” ou “Início” apenas se necessário para caber, sem confundir com Jogar). Não usar seis ou sete destinos no telefone; em tela larga, rail lateral com destinos globais e os secundários rotulados. Mesma identidade de rota, não dois produtos. Documentação e IA também têm acessos contextuais dentro da IDE. Biblioteca integra Studios, mas continua acessível sem abrir um Studio. Ajustes é configuração, não uma página de criação.

No workspace, barra de até quatro destinos (Código, Jogo, Recursos, Notas), preservando vistas Arte/Mapa/Som via Recursos, conforme agrupamento existente. Em largura >= 840 dp, aproveitar editor e jogo lado a lado quando útil, com apoios em coluna, preservando o comportamento do ADR. Acesso direto para IA e objetivos não precisa se tornar aba de destino: são apoios ao trabalho. Navegação global desaparece no workspace; Voltar restaura a origem. Não duplicar botões de navegação em cards, barras e rodapés.

### 4.2 Wireframes sem estilo (estrutura, não desenho de pixels)

```text
HOME / COMPACTO                      WORKSPACE / COMPACTO
┌─────────────────────────┐         ┌─────────────────────────┐
│ Área de trabalho        │         │ ‹ Nome do jogo  estado ⋯│
│ Buscar projetos         │         │ Game.cs | Player.cs    │
│ Continuar edição       │         │                         │
│ [último projeto real]   │         │     código / conteúdo  │
│ Fixados (se houver)     │         │                         │
│ Recentes / todos        │         │ [IA compacta se aberta] │
│ lista ou grade          │         │ Console Problemas ...  │
│             Novo jogo  │         │       Rodar (primária)  │
│ Home Jogar Apr. Stud. ⋯ │         │ Código Jogo Rec. Notas  │
└─────────────────────────┘         └─────────────────────────┘

ESTÚDIO / COMPACTO                  MISSÃO / WORKSPACE
┌─────────────────────────┐         ┌─────────────────────────┐
│ ‹ Studios               │         │ ‹ Jogo · missão         │
│ Ferramentas             │         │ Game.cs                 │
│ Sprite  Tilemap         │         │ código / preview        │
│ Música  Efeitos         │         │                         │
│ Paleta  Partículas ...  │         │ Objetivos  2/4   ˄      │
│ Minhas criações         │         │ [painel expansível]     │
│ Rascunhos / Publicados  │         │ Docs / Pista contextual │
│ Biblioteca              │         │ Código Jogo Rec. Notas  │
└─────────────────────────┘         └─────────────────────────┘
```

Os esquemas mostram relações, não impõem alturas ou posições exatas. Não colocar todas as ações sugeridas simultaneamente se a tela ficar poluída. A ação Rodar tem um único local canônico conforme estado e tamanho; não duplicar FAB e botão de topo. Quando teclado aberto, barra inferior e ações conflitantes cedem espaço. A barra de apoio deve mostrar estado sem obrigar três toques para erros/objetivos. Definir o acesso direto à IA sem sacrificar a ação primária.

## 5. Sistema visual — a única estética autorizada

Leia §0–14 de `docs/design/linguagem-visual.md` e a vitrine. Os números abaixo resumem a versão encontrada; `tokens.json` sempre vence. Não criar tokens paralelos dentro de páginas.

| Papel | Valor v1.0 |
|---|---|
| Fundo | ink.1 `#0B0B0F` |
| Barras / fundo sutil | ink.2 `#111116` |
| Campos / superfícies | ink.3 `#17171E` |
| Elevado | ink.4 `#1D1D26` |
| Overlay | ink.5 `#24242F` |
| Texto principal / secundário / terciário | `#EDEDF3` / `#B4B4C6` / `#9494A9` |
| Ação Lunet | violeta.9 `#6E56F8`; branco no botão sólido |
| Informação, link, seleção informativa | azul; azul.11 `#7CB3FF` em texto/ícone |
| Seleção comum | soft do acento + texto/forma; não acento sólido em todo item |
| Sucesso / aviso / erro | tokens semânticos, nunca apenas cor |
| Tipografia | Inter embutida; JetBrains Mono no código; máximo de três tamanhos de interface por tela, usando escala normativa |
| Espaço | escala de 4; margem 16 no celular, 24 tablet, 32 desktop; 8–12 intragrupo, 24–32 entre grupos |
| Controles | toque 48 dp; topo 56; navegação inferior 64; ícones 20/24; indicador 56×32 |
| Movimento | 90/150/220/300 ms, transformação/opacidade, reduzir movimento respeitado |

- [ ] `LunetTokens`, `LunetUi`, `LunetIcons`, `LunetApp` e estilos de controle são a implementação local única da linguagem. Conferir correspondência integral com a versão declarada, inclusive formato alpha CSS versus Avalonia. Não adicionar dependência em Urbe/Numa/Hub.
- [ ] Campos sem borda normal, fundo de tom distinto, label externo persistente, foco de 2 px, erros acionáveis. Verificar `TextBox`, `ComboBox`, `ToggleSwitch`, slider, menu, tab, tooltip, diálogo, scroll, editor, inputs de IA e configurações. Controles Fluent sem personalização não passam.
- [ ] Uma ação principal sólida por tela; secundárias tonais/fantasma; remoção vermelha só onde pertinente. Listas planas, cartões apenas para conteúdo heterogêneo, nunca cartão dentro de cartão. Máximo quatro níveis de elevação, sem sombra em superfícies planas, sem vidro no Android.
- [ ] Folha para apoio curto: alça, fechar visível, scrim quando modal, altura <=90%, uma por vez. Não usar folha para navegação global longa. IA compacta é **não modal** e não bloqueia código; confirmação crítica usa diálogo apropriado.
- [ ] Revisar estados real/vazio/carregando/erro/pressionado/foco/desativado/selecionado, acessibilidade, nomes dos ícones, feedback e preferências de movimento. Contraste WCAG AA e APCA conforme §11; calcular pares reais e alpha composto, não apenas listar cores. Se tokens normativos e exigências entrarem em conflito, documentar o par e propor solução sob governança; não alterar `tokens.json` unilateralmente.
- [ ] Não usar preto puro no cromo, branco puro em texto comum, borda decorativa onipresente, gradiente de botão, destaques de menta, glow ornamental, texto de marketing ou informação técnica na Home. O palco do jogo, arte e conteúdo editável conservam suas cores próprias.

## 6. Plano executável (checkboxes)

### 6.0 — Auditoria, base e proteção contra regressão

- [ ] Registrar SHA de `origin/main`, branch, última release/tag/APK, PRs em andamento, arquivos em disputa, estado de LUNET-530/531/534/536/537 e IDs correlatos F1–F8. Identificar também trabalho recente de áudio, IA e teclado. Não sobrescrever mudanças concorrentes.
- [ ] Ler documentos da §1 e executar protocolo de auditoria: Framework/execução primeiro (somente para conhecer os contratos), IDE/UX em seguida, Studios/UX por último. Traçar usuário → view → estado → armazenamento/runtime → retorno. Inventariar controles, rotas, features e ponte Android por destino.
- [ ] Usar os estudos anteriores e suas fontes como pesquisa válida; verificar referências que mudaram de tecnologia ou decisão, investigar lacunas de implementação atuais. Comparar ao menos uma referência relevante de código/docs e comunidade para cada mecanismo novo. Não repetir 232 commits de estudo já útil nem criar estudo novo sem ligação a teste.
- [ ] Gerar baseline com a casca real: `dotnet run --project src/Lunet.Desktop -- --screens <pasta>` a partir de `apps/lunet2d`, além dos testes headless existentes. Inventário captura → estado → entrada → arquivo. Registrar bugs reproduzidos e separar observados de inferidos.
- [ ] Criar testes de regressão antes da alteração. Inspecionar `LunetClientTests`, testes de shell, `ProjectJourneyTests`, testes de Studio, Learning, Docs, IA e Android; usar IDs existentes ou adicionar uma tarefa rastreável sem duplicar ROADMAP. Separar correção de UI de bugs de domínio quando isso reduz risco.

### 6.1 — Home, shell e continuidade

**Arquivos iniciais:** `LunetShell.cs`, `Shell/ShellState.cs`, `Views/HomeView.cs`, `Views/SettingsView.cs`, `Views/ProjectFolderView.cs`, `MainActivity.Route.cs`, `ShellActivity.cs`, Host `WorkspaceSession` e stores atuais.

- [ ] Consolidar modelo de navegação da §4. Rotas de primeiro nível persistentes com back stack ou mecanismo equivalente já existente; folha é overlay de apoio e não substitui uma rota de página sem restaurar origem. Evitar segundo estado canônico. Guardar estado transitório por destino (posição, filtro, seleção) e identidade de jogo por GameId; reconciliar nomes de pasta apenas quando a API atual exigir.
- [ ] Home: título funcional, busca, ação de retomar último jogo editado, fixados quando existentes, recentes/todos, grade ou lista coerente com dados reais. Não duplicar o mesmo jogo em três blocos enormes; se houver seção de continuidade, mantenha compacta e omita se vazia. Não preencher com demos. Estado inicial vazio e ação Novo jogo clara. Novo projeto padrão vazio preserva template atual e escolhas explícitas; não criar assets de demonstração involuntários.
- [ ] Tocar em jogo na Home **edita**; menu contextual mantém ações reais (jogar, renomear, duplicar, exportar/importar, mover/remover e demais disponíveis) com confirmação/undo adequados. Exibir erro recuperável quando pasta inacessível ou último jogo ausente. Não exibir caminho técnico no cabeçalho; Ajustes é autoridade de configuração de armazenamento.
- [ ] Navegação global estável mobile/desktop. A presença de `OpenRoute` não decide se o conceito existe. Quando recurso depende de plataforma, explicar limitação no lugar certo, sem link morto. Migrar/reconciliar Explorar/Docs global e demais rotas necessárias na casca usando serviços existentes, sem ampliar catálogo de Explorar congelado na onda 0.
- [ ] Retornar a mesma posição, busca, seleção, rolagem e contexto de origem ao sair de workspace, partida, IA, biblioteca e Studios. Trocar aba global de forma direta, não acumular telas. Back/Esc: fecha teclado/painel transitório primeiro; depois subpágina; depois origem; nunca sai sem tratar alteração não salva. Mudança de orientação não reinicia navegação.
- [ ] Preservar armazenamento, SAF, identidade, importação/exportação ZIP, migração e configurações já existentes. Não inventar migração de dados para resolver layout. Teste instalação limpa e atualização com jogos, notas, biblioteca, progresso e configurações existentes. Cancelamento e acesso revogado mantêm dados intactos.

**Aceite:** a pessoa consegue abrir o Lunet sem projeto, reconhecer os destinos, criar/retomar um jogo, voltar ao mesmo estado e abrir Docs, IA, Studio e Jogar sem menus redundantes. Em 320–412 dp, nenhum rótulo ou ação essencial fica cortado.

### 6.2 — Workspace, código, jogo e recursos

**Arquivos:** `WorkspaceView.cs`, `DocumentEditor.cs`, `TextSurface.cs`, `GameView.cs`, `ResourcesPage.cs`, `SupportPanelView.cs`, `ShellState.cs`, `GameSession.cs`, `ProjectFiles.cs`, `CommandPaletteView.cs`.

- [ ] Conservar AvaloniaEdit, números de linha, realce, seleção, linha atual, tabs/documentos, Explorer, busca, undo/redo, compilação, diagnósticos, jump-to e assinatura. Não substituir por `TextBox` nem implementar novo editor para redesenhar a UI.
- [ ] Topo compacto com voltar, nome/estado real e ação principal contextual; agrupar secundárias em ⋯. Não incluir botões duplicados para Rodar/Parar; em mobile garantir zona de toque conveniente. Um botão de painéis pode coexistir com acesso contextual à IA e indicadores acionáveis de erros. D3/D4 de 25/11 cedem medidas de tokens a 26/11 (ex.: indicador 56×32).
- [ ] Explorer e arquivos acessíveis sem sair do projeto; em compacto, painel sob demanda, em largo coluna. Abas com nome, alteração não salva, fechar e recuperação de arquivo. Nenhuma mudança de painel reconstrói documento desnecessariamente. Preservar caret, seleção, viewport, undo, foco e estado dirty entre vistas, rotação, IA e Preview.
- [ ] `Recursos` é entrada agrupada para Arte/Mapa/Som e outros assets reais, com divisão por tipos e acesso aos editores corretos. Não criar outro catálogo ou copiar a fonte silenciosamente. `Notas` conserva conteúdo e mapas mentais existentes.
- [ ] Apoios: Console/Problemas acessíveis diretamente por área de estado/atalho, sem tela modal; abrir erro vai à linha. Comandos, Docs, inspector e desempenho sob demanda; Git, APK e Conexões como operações do jogo. Manter alcance a todas as ações existentes e atalhos.
- [ ] Separar Preview da partida pelo Launcher; preservar pausa/passo/velocidade, jogo lado a lado quando largo, entrada/orientação, áudio, lifecycle, saves. Ao falhar build, abrir problema com diagnóstico, não descartar código. Ao sair, parar recursos de sessão quando devido.
- [ ] Verificar `Rebuild`, `ReloadFromDisk`, `SaveAll`, `SetKeyboardOpen`, eventos de jogo e suporte; controlar reparenting e subscriptions. A UI não pode tomar foco da IA ao abrir teclado nem sobrescrever texto dirty após retorno. Renderização incremental/virtualizada quando necessário, nunca um rebuild inteiro por evento de log.

### 6.3 — Teclado, completude e entrada móvel

**Arquivos:** `CodeBar.cs`, `CodeInputClient.cs`, `DocumentEditor.cs`, `TextSurface.cs`, `Ui/CodeInput.cs`, `Shell/CodeKeys.cs`, `MethodCatalog.cs`, `KeyboardLayout.cs`, `KeyboardFit.cs`, settings e head Android.

O pedido original do proprietário em 25/11 era um teclado integral próprio da IDE com Teclas, Atalhos e Métodos. Porém a main consultada registra uma ordem posterior no comentário de `CodeBar`: digitação pelo teclado do sistema, mantendo três abas. O `DocumentEditor` chama `SetSystemKeyboard`, enquanto ROADMAP e D2 não foram totalmente reconciliados. Não assumir que o código comprova a autorização posterior; localizar a ordem, PR e handoff exatos. A missão atual é organização e acabamento, não uma autorização autônoma para trocar política de entrada.

- [ ] Confirmar a última decisão explícita e registrar sua referência. Se teclado integral for de fato a decisão vigente, restaurar/completar as três abas dentro da única superfície de teclado e a opção de sistema/teclado físico conforme norma. Se a decisão posterior pelo IME estiver vigente, conservar IME + barra única com as abas. Se não puder comprová-la, manter comportamento executável atual e marcar a divergência no relatório, sem interromper os demais itens. Não fazer um híbrido improvisado de dois teclados ativos.
- [ ] Em ambos os caminhos, Teclas, Atalhos e Métodos são partes de uma experiência de entrada, não barras duplicadas em lugares diferentes. Teclas dão acesso aos símbolos C#; Atalhos a edições estruturais; Métodos é lista rolável e pesquisável: Recentes, Mais usados, depois namespaces/classes/membros em ordem alfabética. Usar catálogo real de API + símbolos do jogo, não texto decorativo.
- [ ] Autocompletar e signature help coerentes com revisão do texto e cursor, debounce/análise assíncrona, cancelar resultados velhos. Inserção de método respeita caret, seleção, parâmetros, parênteses e desfazer em um passo. Não tratar sugestões antigas como atuais.
- [ ] Garantir código e caret visíveis com teclado; evitar barras sobrepostas; respeitar insets, fonte do sistema e teclado físico. Prosa, notas, busca, inputs de IA e configurações usam teclado de sistema adequado. Back fecha o teclado/página de Métodos sem sair do projeto. Verificar digitação PT-BR, acentos, repetição e navegação de foco.
- [ ] Acabamento conforme Ecosystem v1.0 e decisões vigentes: rótulos acessíveis, alvos generosos, seleção clara, feedback sem reconstrução e sem animar layout quadro a quadro. Háptico por hook se autorizado/suportado; respeitar preferência do sistema. Testes de toque e teclado em ambas orientações.

### 6.4 — Aprender integrado à IDE

**Arquivos:** `LearnView.cs`, `ShellState.cs`, `WorkspaceView.cs`, `SupportPanelView.cs`, persistência Learning/Project, `DocsView.cs` e IA.

- [ ] Conservar trilhas, curso, missão, progressos e associações existentes. Não criar cursos novos nem fornecer jogo solucionado. Curso indisponível continua explicitamente indisponível. A missão é pedagógica: o usuário implementa, não recebe resultado pronto.
- [ ] Criar contexto ativo de missão ligado a ID estável da missão + GameId. Ao abrir/retomar de Aprender, selecionar o jogo real e mostrar objetivos sem exigir que usuário volte a catálogo. Ao entrar diretamente em jogo já associado, oferecer retomar missão sem forçar contexto indevido; múltiplas missões exigem seleção inequívoca. Evitar nova autoridade de progresso; usar store existente.
- [ ] Apoio `Objetivos` com título curto, progresso real, checklist dos objetivos, critérios observáveis e ação de referência exata. Em compacto, resumo acessível/folha não bloqueante quando recolhida; em largo, coluna. Objetivos pode coexistir com editor/preview conforme espaço; Docs, Console, Problemas e IA continuam acessíveis.
- [ ] Ao alternar missão/Docs/Preview, preservar código e checklist; ajuda IA em modo Estudo por padrão quando decisão vigente assim prevê, sem editar o exercício sem escolha explícita. Referência exata offline e retorno à missão.
- [ ] Voltar restaura curso, missão e posição de lista de onde veio. Verificar retomada após fechar app, renomear jogo, jogo ausente, progresso existente, múltiplos jogos, erro ao criar e storage revogado. Marcar progresso somente por ação ou verificação real definida pelo curso; nada fictício.

### 6.5 — Studios, biblioteca e recursos do jogo

**Arquivos:** `StudiosView.cs`, `LibraryView.cs`, `ResourcesPage.cs`, `WorkspaceView.cs`, `SpriteStudioView.cs`, `TileStudioView.cs`, `MusicStudioView.cs`, `SoundStudioView.cs`, `WaveEditorView.cs`, `PaletteView.cs`, `ParticleStudioView.cs`, `DataView.cs`, `ZoomCanvas.cs`; storage `StudioAssetWorkspace`, `ReusableAssetLibrary` e backends atuais.

- [ ] Home → Studios abre sem jogo. Galeria apresenta ferramentas por tarefa (Arte: Sprite/animação, Tilemaps, Paletas; Áudio: Música, efeitos, edição WAV quando existente; Outros: Partículas e Dados). Agrupamentos são orientação, não novos módulos/estúdios. Mostrar rascunhos reais recentes e acesso à biblioteca; não lotar página de caixas aninhadas nem inserir seletor obrigatório de projeto.
- [ ] Studio individual: topo com voltar, nome/estado de salvamento; conteúdo central (canvas, sequenciador, waveform ou tabela); ferramentas contextuais próximas ao dedo, inspector/propriedades sob demanda, ações primárias claras. A forma segue o domínio: não impor layout de Sprite a Música ou Dados. Zoom, pan, edição, undo e preview conservam funções presentes. Tela vazia explica o primeiro passo.
- [ ] Reutilizar mesma implementação nativa para global e projeto, com storage/ownership explícito. Exibir contexto (rascunho pessoal ou recurso do jogo) sem confusão. Salvar no rascunho, publicar para Biblioteca e copiar/importar para jogo são operações diferentes; nunca escolher jogo de forma implícita.
- [ ] Publicar fonte e derivados coerentes da revisão escolhida; não publicar WAV atrasado de composição anterior; preservar revisão anterior em falha. Testar colisão de nomes, cancelamento, storage inválido, cópia independente/fork, dependências, alterações e reabertura. Voltar de publicação/cancelamento restaura Studio, documento, seleção, zoom quando pertinente e posição da biblioteca. Não resetar a galeria inteira por voltar do editor.
- [ ] Para cada tipo existente, comprovar matriz global → criar → salvar → reabrir → publicar → usar cópia no jogo → editar → reabrir jogo. Registrar fonte, derivado e identidade de revisão. Se tipo não suporta operação, estado explícito, não botão fictício. A matriz não autoriza criar ferramentas novas.
- [ ] Áudio/Música: revisar incidente atual de crash, PRs/relatório e lifecycle de geração/preview/Stop/Dispose. Se a correção já entrou, proteger por teste; caso contrário reproduzir, corrigir ou encaminhar com causa/trace, sem esconder funcionalidade. Não bloquear thread de UI com render de WAV, não tocar callback de view descartada, não permitir que render antigo substitua novo. Exercitar iniciar, parar, mudar faixa, sair/voltar, repetir e fechar durante geração. Conferir armazenamento e derivado publicado. A causa do crash não foi demonstrada nesta auditoria; não inventá-la.

### 6.6 — IA global e co-work

**Arquivos:** `GlobalAiView.cs`, `AiPanelView.cs`, `SupportPanelView.cs`, `LunetShell.cs`, AiHost/runtime, settings, histórico, checkpoints e testes existentes.

- [ ] IA global é acessível sem jogo em Mais e por atalho de área de trabalho quando útil; configurar provedor, credenciais e default apenas em Ajustes/IA global. No projeto, conversa contextual e seletor de modelo local quando permitido; não duplicar formulário de configuração. Mostrar link para configurar quando falta provedor, sem mensagem enganosa de execução.
- [ ] Co-work: ação direta abre compositor compacto não modal sobre/ao lado do código, com última mensagem, estado da execução, contexto e botão expandir para conversa completa. Fechar visualização não cancela execução se runtime suporta continuação; cancelamento é ação explícita. Histórico persiste no escopo correto. Não reabrir painel grande automaticamente a cada ferramenta.
- [ ] Execução apresenta etapas/eventos reais, agrupa detalhes expansíveis, carrega histórico sob demanda, suporta rolagem independente e não arrasta o editor. Perguntas, aprovações/diffs/erros e opções de desfazer visíveis. Não simular cadeia de pensamento privada nem inventar atividade; mostrar progresso e dados reais disponíveis. Fluxo Estudo vs Fazer segue permissões existentes e decisão do produto, não mera cor/badge.
- [ ] Proteger contexto por GameId e revisão, sessão global separada da sessão de projeto, edição concorrente e projeto fechado. Nenhuma ação de ferramenta pode sobrescrever documento dirty sem reconciliação. Preservar runtime e integrações canônicas; sem segundo agente/assistente. Verificar execução longa, scroll, fechamento/reabertura, IME do chat e alternância entre projeto e Home.

### 6.7 — Documentação, Launcher e Explorar

**Documentação** — `DocsView.cs`, `Shell/DocsLibrary.cs`, guias e referência gerada:

- [ ] Entrada global em Mais e contextual via símbolo/erro/objetivo. Mesmo leitor e conteúdo offline. Organizar em Primeiros passos, Guias por tarefa, Conceitos, API; busca por nome/assinatura/termo, resultado com contexto. Não misturar changelog, IDs de desenvolvimento ou esboço de roadmap com manual de produto.
- [ ] Página com título, pré-requisitos, sequência, código copiável, resultado esperado, erros comuns e links relacionados. Back volta para busca/curso/cursor de origem. Referências de código devem compilar contra a versão atual; não reintroduzir exemplos antigos inválidos (`Graphics.Clear`, `Content.Load<T>`, `time.Delta`) que o estudo de 23/11 denunciou; confrontar API de hoje. Usar a suíte/documentação atual em vez de reescrever toda a referência sem necessidade.

**Jogar** — `PlayView.cs`, `GameSession`, runtime/preview e head:

- [ ] Biblioteca jogável separada da lista de projetos de edição. Toque na capa inicia partida real da revisão atual; ação Editar distinta. Estados preparando/compilando/erro/jogando, diagnóstico e escolha explícita da última revisão válida se oferecida. Não vender preview simulado como partida; usar pipeline atual, cache válido e execução offline.
- [ ] Voltar da partida à mesma grade/posição; preservar saves, orientação, áudio, processo e isolamento já implementado. Não exigir gerar/instalar APK a cada jogo. `Gerar APK` segue distribuição do jogo no workspace, não é fluxo de Jogar. Testar falha de compilação e assets alterados.

**Explorar** — preservar catálogo offline, filtros e notas existentes:

- [ ] Entrada global em Mais; tratar eventual rota Android legada sem remover conteúdo. Reconciliar aparência, navegação, retorno e estados com casca. Não expandir o catálogo, criar curadoria nova ou furar congelamento do plano de 25/11. Vinculação explícita ao jogo quando a feature atual existir. Preservar favoritos e notas.

## 7. Referências técnicas para as decisões de UX

Os estudos de 23, 25 e 26/11 já têm bibliografias. Não use a lista como pretexto para copiar outra interface ou recomeçar a pesquisa do zero. Use documentação e código para resolver mecanismos ainda duvidosos e registre evidência e limite.

| Tema | Referências concretas e uso |
|---|---|
| Shell adaptativo | [Android adaptive navigation](https://developer.android.com/develop/ui/compose/layouts/adaptive), [Avalonia docs](https://docs.avaloniaui.net/), ADR 0013. Rotas semânticas, largura e foco; Compose como referência de interação, não toolkit. |
| Aparência | [Linear redesign](https://linear.app/blog/how-we-redesigned-the-linear-ui), [Radix escala](https://www.radix-ui.com/colors/docs/palette-composition/understanding-the-scale), [Material tonal surfaces](https://material.io/blog/tone-based-surface-color-m3), [WCAG](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), [Lucide](https://lucide.dev/guide/design/icon-design-guide), estudo de 26/11. As normas locais decidem valores, não a estética de cada fonte. |
| Campos e folhas | [NN/g placeholders](https://www.nngroup.com/articles/form-design-placeholders/), [NN/g bottom sheets](https://www.nngroup.com/articles/bottom-sheet/). Label persistente, fechar explícito, conteúdo longo em página. |
| Editor | [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit), [VS Code workbench](https://github.com/microsoft/vscode/tree/main/src/vs/workbench), [Acode](https://github.com/Acode-Foundation/Acode), [Sora Editor](https://github.com/Rosemoe/sora-editor). Foco, símbolos, interação touch, virtualização, teclas; não trocar editor por benchmark sem ganho. |
| Studios | [Aseprite](https://github.com/aseprite/aseprite), [LDtk](https://github.com/deepnight/ldtk), [BeepBox](https://github.com/johnnesky/beepbox), [Godot](https://docs.godotengine.org/en/stable/). Canvas e ferramentas orientadas ao domínio, fontes/derivados; não importar código sem verificar licença. |
| Aprender/Docs | [Diátaxis](https://diataxis.fr/), [Godot docs](https://docs.godotengine.org/en/stable/), [MonoGame docs](https://docs.monogame.net/). Organização por intenção e tarefas completas, não índice alfabético como entrada única. |
| Performance e acessibilidade | [Android vitals](https://developer.android.com/topic/performance/vitals/render), [Android accessibility](https://developer.android.com/guide/topics/ui/accessibility/apps), estudos internos e testes reais. |

Fontes externas são as referências dos estudos anteriores e guias para verificação pelo executor; nesta reconciliação não foi realizada nova revisão independente de todas as páginas/comunidades citadas. Registre a investigação complementar só quando houver decisão de implementação ainda não fundamentada.

## 8. Critérios de aceite verificáveis — não confundir estética com checklist verde

### 8.1 Jornadas de integração

| ID | Sequência obrigatória | Oráculo |
|---|---|---|
| UX01 | Instalação limpa → Home → novo jogo vazio → editor → voltar | Destinos legíveis, ação única, nenhum demo involuntário; volta à origem. |
| UX02 | Home com jogos → buscar/filtrar → abrir → editar → voltar → retomar | Identidade e estado preservados, cursor/seleção/scroll, sem dados perdidos. |
| UX03 | Código → Métodos → inserir chamada → undo → assinatura → rodar | Uma edição/undo, símbolos reais, caret preservado, diagnóstico acionável. |
| UX04 | Código → IA compacta → execução → expandir → fechar → reabrir | Trabalho/contexto preservados; sem modal desnecessário; eventos reais; sem cruzar sessões. |
| UX05 | Aprender → missão → editor → Objetivos → Docs → Preview → voltar | Mesmo GameId, objetivos sempre recuperáveis, progresso real, volta ao curso. |
| UX06 | Studios sem jogo → criar/salvar → publicar/cancelar → copiar para jogo | Fonte global independente, derivado correto, retorno ao documento/galeria, cópia não altera origem. |
| UX07 | Música → tocar/parar → alterar → trocar tela durante render → voltar | Sem crash, áudio obsoleto descartado, sem callback em view morta; persistência íntegra. |
| UX08 | Jogar → partida → voltar → editar; falha de compilação | Volta à grade e ao estado; distinção entre Preview/Launcher/APK; erro útil. |
| UX09 | Docs global → busca; Docs contextual por erro/símbolo/missão | Mesmo leitor offline; link exato e histórico/retorno. |
| UX10 | Home → Ajustes → pasta/revogação/cancelar; app atualizado | Nada perdido; autoridade de storage única; estado anterior recuperável. |
| UX11 | Abrir cada destino e painel em 320/360/412 dp, retrato/paisagem e >=840 dp | Sem clipping, sobreposição, alvo inacessível ou tela duplicada; estado preservado. |
| UX12 | Fonte maior, foco, leitor de tela onde suportado, teclado físico/IME e reduzir movimento | Nome/ordem de foco, contraste, conteúdo alcançável; limitações reais registradas. |
| UX13 | Histórico IA/log longo, muitos projetos/assets, trocar abas repetidamente | UI responsiva, sem alocação/rebuild injustificado, sem perda de estado. |
| UX14 | Global → Biblioteca/Explorar/Docs; Studio e jogo com Back/Esc sucessivo | Uma pilha coerente; sem link morto nem overlay preso; nenhuma ação duplicada. |

### 8.2 Evidência visual exigida

- [ ] Capturas **do cliente real**, antes/depois, 412×915 e 1280×800 conforme ADR, mais compactos 320/360 dp, landscape e fonte ampliada onde harness permitir. Usar dados de fixture reais sem incluir no app de produção. Não usar o protótipo HTML como evidência da implementação.
- [ ] Capturar no mínimo: Home vazia e povoada, menu Mais, busca/retomada, projeto código com teclado fechado/aberto/Métodos, Preview e erro de build, Recursos, Notas, Console/Problemas, IA compacta/expandida/erro, Aprender catálogo/missão/objetivos dentro da IDE, Docs global/contextual, galeria de Studios, cada Studio existente em estado editável, publicação/biblioteca, Jogar e Ajustes. Cada imagem identificada por SHA, resolução, fixture e ação.
- [ ] Fazer revisão visual humana/de agente independente **sobre capturas reais**, com checklist §13 da linguagem visual por tela. Olhar alinhamento, densidade, espaço desperdiçado, hierarquia, contraste, ação primária, estados e coerência entre destinos. Não aprovar uma imagem só por passar teste de token. Corrigir defeitos observáveis antes de publicar. Isto é revisão técnica da evidência, não gate de validação pelo proprietário.
- [ ] Medir latência de ações críticas (referência <100 ms para feedback), digitação/caret, troca de painéis e primeiro quadro; custo de render, GC/alocações e listas longas. Separar build Debug/Release, headless/emulador/aparelho; comparar mesma fixture. Meta histórica cold start <1,5 s no moto g32 não é resultado medido; registrar evidências reais e regressões. Não atribuir números a teste inexistente.

## 9. Ordem de integração e definição de pronto

A sequência reduz retrabalho: primeiro contrato e fundação, depois jornada do desenvolvedor, depois jornadas dependentes, por último acabamento transversal. O executor pode integrar fatias independentes conforme governança, sem fragmentar semântica ou deixar o produto permanentemente em estado intermediário.

- [ ] **U0**: inventário/diagnóstico, testes de navegação e baseline visual; consolidar decisões e conflitos documentais.
- [ ] **U1**: casca, destinos globais, Home, persistência de navegação e componentes/tokens; provas UX01/02/09/10/14.
- [ ] **U2**: workspace, teclado conforme decisão vigente, painéis, Preview e continuidade; provas UX03/11/12/13.
- [ ] **U3**: objetivos de Aprender e IA co-work; provas UX04/05. Usar a mesma infraestrutura de apoios, não criar cascas concorrentes.
- [ ] **U4**: Studios, Biblioteca, recursos, áudio e retorno, provas UX06/07. Preservar/validar correções concorrentes de música.
- [ ] **U5**: Launcher/Docs/Explorar, reconciliação das rotas restantes e revisão visual final de todas as superfícies afetadas; UX08/09/14. Nenhuma feature existente deve desaparecer enquanto se migra.
- [ ] **U6**: regressão integrada, documentação, rastreabilidade, PR, CI, merge e release oficial conforme autorização e regras vigentes. Revalidar base combinada com main, não só a branch isolada.

**Entrega técnica:** usar TDD, `dotnet test` da suíte vigente do Lunet, testes do cliente/headless, smoke Android/emulador e checks oficiais; executar consistency e gates de arquitetura, segurança, integrador, conforme AGENTS. Quando ferramenta/SDK não estiver disponível, indicar comando, motivo e resultado real de CI; não substituir por uma declaração de sucesso. Registrar em `AgentsChat.md`, ROADMAP e handoff canônico resultados, SHA, arquivos, evidências, known_issues e revisão das decisões obsoletas (especialmente teclado, status F1–F8 e medidas D3/D4). Persistir a arquitetura da informação específica do Lunet em fonte de produto existente (SPEC ou documento de UX vinculado a SPEC), sem copiar o design system nem alterar governança global desnecessariamente.

**Integração:** ADD-0035/DEC-0065 e ADD-0034 conforme texto vigente: PR pronto e estado combinado verde integram pelo mecanismo oficial, sem label manual como requisito novo. Não fazer merge forçado, manipular assinaturas ou alterar portão de integração. Se código do Lunet mudar, gerar nova release própria incrementando o número no estágio vigente (estágio pertence ao proprietário). Confirmar tag, SHA e APK publicado pelo workflow oficial `.github/workflows/lunet2d-release.yml`; não publicar APK manual divergente. Não afirmar que capturas, CI ou emulador são teste em dispositivo físico, nem criar gate de aprovação humana que ADD-0035 aboliu. Informar limitações de validação em `known_issues` e fornecer roteiro opcional para uso real.

**Relatório final esperado do agente:** link de PR e release/APK, commit integrado, tabela UX01–UX14 com resultado e evidência, capturas antes/depois, lista de regressões corrigidas, arquivos alterados, conflitos documentais reconciliados, limitações/known_issues e indicação de qualquer recurso que não pôde ser validado. Marcar checkbox somente com prova. Não declarar “tudo concluído” com falhas, rotas sem funcionamento ou evidência ausente.

## 10. Limites da auditoria que originou esta missão

Foram recuperados os três arquivos originais em Markdown/HTML, inspecionados seus contratos de produto e páginas, consultadas as normas e estudos de 25/26 no GitHub e examinados trechos de código da casca atual. O HEAD foi confirmado pela API e os links de código foram fixados à base encontrada. Não houve checkout completo, compilação, execução do APK, teste no aparelho, inspeção de todas as capturas atuais ou investigação integral de todos os arquivos e PRs concorrentes. A matriz diferencia lacunas observadas no código de riscos a provar. Esta especificação não atesta o estado de cada feature nem a causa do crash musical. Os agrupamentos de navegação, painel de objetivos e ordem de trabalho são propostas de implementação desta missão fundamentadas no material anterior; não constituem mudança de tokens ou decisão de arquitetura global. A divergência de teclado requer fonte da última ordem, não um palpite.

---

**Regra de encerramento:** o Lunet deve parecer e funcionar como um produto único. A estética vem do Ecosystem; a organização vem das tarefas reais de quem cria jogos. A interface deve desaparecer diante do trabalho, não esconder o trabalho diante da interface.
