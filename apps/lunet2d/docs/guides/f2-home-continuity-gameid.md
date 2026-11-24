# F2 · Home e continuidade de projetos por GameId — LUNET-510

**Base auditada:** `main@9200ab7cdc479367c5c60d343ed189ebef6d4bc6` · release oficial `0.0.1-dev.1000585` · primeiro incremento da F2 aprovado pelo plano de 23/11/2026.

## Estudo e auditoria antes da intervenção

Fontes internas examinadas: `MainActivity.Home.cs`, `MainActivity.cs` (OpenProject, Run, StopPreview, OnBackPressed), `LauncherPreferences.cs`, `ProjectStore.cs`, `ProjectManifest.cs`, `AutosaveJournal.cs`, `docs/DEVELOPMENT.md`, `ROADMAP.md`, `docs/SPEC.md`, os estudos completos e a prévia aprovada da F2. Referências oficiais: [Android — salvar estados de Views](https://developer.android.com/topic/libraries/architecture/views/saving-states-views) e [Android — Document provider / Storage Access Framework](https://developer.android.com/training/data-storage/shared/documents-files). A documentação Android distingue estado transitório da tela de dados persistidos e restringe árvores SAF em versões recentes do sistema.

**Achados comprovados no código de partida:**

1. A Home já apresentava cartões de jogos, busca e preferências persistentes, mas `launcher-preferences.json` V1 guardava `LastProject` e `PinnedProjects` exclusivamente por **nome de pasta**. Renomear/mover a pasta invalidava o atalho, apesar de `lunet.json` ter `gameId`.
2. Ao existir um último projeto, a Home oferecia só o botão Continuar, escondendo a ação de criar novo jogo naquele local. A ação Jogar era interna ao editor, não tinha entrada própria na continuidade.
3. `OpenProject` possuía recuperação de alterações não salvas via `AutosaveJournal`; abrir e compilar automaticamente antes dessa decisão poderia executar uma versão incorreta do jogo. O novo atalho Jogar não pode ignorar a recuperação.
4. `StopPreview` restaurava o editor mesmo quando a pessoa pretendia apenas jogar a partir da Home. `OnBackPressed` devolvia toda origem ao Início, não necessariamente à seção de onde a edição saiu.
5. O armazenamento em pasta livre via SAF é **outro contrato**: usar `ACTION_OPEN_DOCUMENT_TREE` sem staging e migração verificável quebraria a execução Roslyn/Git/ZIP. Este incremento **não finge** ter migrado projetos nem criado integração SAF.

## Decisões implementadas

- **Identidade:** `LauncherProjectCatalog.Scan` lê somente `gameId` existente no manifesto local. Sem inventar ID quando a chave está ausente; links simbólicos, JSON danificado ou IDs ambíguos são ignorados, sem editar projetos. A resolução exige uma correspondência única. O nome da pasta é um rótulo mutável, não a identidade.
- **Compatibilidade:** `LauncherPreferences` aceita V1; a Home transforma, de modo atômico, nomes que correspondem a projetos reais em atalhos por GameId (V2). Nomes antigos sem correspondência ficam preservados na preferência para eventual recuperação, **sem abrir qualquer projeto ao acaso**. `Game.cs`, `lunet.json` e assets não são alterados pela migração.
- **Continuidade:** o último jogo realmente aberto e a fila dos oito IDs recentes sobrevivem ao reinício, inclusive se a pasta foi renomeada. Fixação e limpeza na exclusão utilizam GameId. Quando não se consegue resolver o último ID, a Home informa a indisponibilidade e oferece Projetos.
- **UX:** as ações **Editar**, **Jogar** e **Criar projeto** são distintas; os projetos fixados e recentes não disputam a mesma seção. Os cartões permitem jogar diretamente, sem menu secundário.
- **Execução segura:** o atalho Jogar verifica a presença de buffers recuperáveis; nesses casos não compila o jogo automaticamente e oferece abrir o editor para decidir a recuperação. Ao encerrar Preview iniciado na Home, volta à Home; erro de compilação mostra o editor e diagnósticos, sem esconder a falha.
- **Retorno:** o botão de voltar do editor e o Android Back devolvem à seção de origem registrada no launcher (Projetos, Aprender, Studios, Explorar ou Início), em vez de mandar tudo para Início.

## Testes e critérios de aceite

`LauncherContinuityTests.cs` cobre V1→V2, renomeação de pasta, MRU limitado, ID duplicado/ausente, manifesto danificado e exclusão que não afeta outros fixados. `LauncherPreferencesTests.cs` original continua sendo executado. O harness Android 13 tem as telas adicionais `home`, `home-resume` e `home-missing`, capturadas junto às telas legadas e com cheque do processo ativo.

Validação manual ainda obrigatória no moto g32 Android 13: atualizar sem apagar dados; pin/editar/jogar/voltar; renomear/mover pasta mantendo o `gameId`; abrir ZIP de projeto legado; recusar recuperar um buffer e observar comportamento de Jogar; Back após Preview e mudança de orientação. Sem esse retorno, não marcar toda a F2 como concluída.

## Contrapontos e próximos incrementos

A varredura lê manifestos locais para mapear IDs e deliberadamente não altera formatos de projetos. Com milhares de projetos, será necessário medir a latência e eventualmente empregar cache invalidável por revisão, não um índice concorrente silencioso. IDs duplicados permanecem ambíguos: o usuário pode abrir manualmente, mas não é permitido escolher arbitrariamente o “último”.

**F2 restante:** hierarquia adaptativa mais ampla e preservação refinada de scroll após todas as origens; primeiro uso; seletor de pasta `ACTION_OPEN_DOCUMENT_TREE` e permissões persistíveis; contrato de staging Roslyn/Git; migração recuperável; personalização de tipografia/temas; testes de acessibilidade e aparelho físico. Nada disso está marcado como entregue neste incremento.
