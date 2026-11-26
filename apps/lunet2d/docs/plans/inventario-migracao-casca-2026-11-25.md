# Inventário de recursos e ordem de migração para a casca (LUNET-536), 25/11/2026

Ordem do proprietário: "não pare enquanto não estiver com todas as features devidamente implementadas, sem faltar nada". Fonte do inventário: o código de `src/Lunet.Android` (74 arquivos, cerca de 19 mil linhas) e o SPEC. Estado em 25/11: **casca** = já na interface nova (`Lunet.Client`); **rota** = abre a tela anterior pela rota (`MainActivity.Route`), nada se perde; **falta** = sem caminho na casca.

## Ciclo do programador (Onda 2)

| Recurso | Onde está hoje | Estado | Próximo passo |
|---|---|---|---|
| Editor de código com cores e números de linha | `CodeSurface` (AvaloniaEdit) | **casca** | — |
| Barra de código sobre o teclado do sistema (Teclas, Atalhos, Métodos, sugestões do Roslyn) | `CodeBar` (o teclado próprio saiu em 25/11 por latência) | **casca** | — |
| Diagnósticos no código (sublinhado e lista) | `EditorAssistant` (agora em `Lunet.Editor`), `CodeAnalyzer` | **casca** (M1) | — |
| Assinatura ao digitar `(` `,` | `EditorAssistant` | **casca** (M2) | — |
| Ir para definição, referências, renomear, formatar, quick fixes, estrutura, símbolos, buscar, operações de linha | `CommandPaletteView` | **casca** (M3) | — |
| Vários documentos abertos, histórico de posição | `DocumentEditor` + `EditorDocuments` (sessão em `.lunet/editor-session.json`) | **casca** (M4) | — |
| Rodar, pausar, passo, velocidade | `GameSession`/`GameView` | **casca** | — |
| Preview isolado (processo separado) e GL | `IsolatedPreview`, `PreviewHost`, `Gles` | rota (Editor completo) | **M5**: preview GL embutido na aba Jogo (Android); rasterizador em CPU fica no desktop. |
| Profiler e Inspector | `InspectorView` + `GameInspection`, `PerformanceView` + `FrameStats` | **casca** (M6) | Próximo: medir também a GPU quando a prévia GL embutida (M5) existir. |
| Console e Problemas | `SupportPanelView` | **casca** | — |
| IA no jogo (chat, modos Estudo/Fazer, diff, desfazer, chave) | `AiPanelView` sobre `Lunet.Ai` | **casca** (M7) | Próximos: anexos e imagens, pesquisa na web, catálogo de modelos, revisão trecho a trecho dentro do editor, execução em segundo plano com notificação (`AiRunCoordinator`). |
| IA global | `GlobalAiView` sobre `WorkspaceAiChat` (mesmo `ai/workspace-history.json`); Configurar IA em `AiSettingsView` | **casca** (26/11) | — |
| Layouts da área de trabalho | substituído pelo layout adaptativo da casca (compacto/expandido, código e jogo lado a lado) | **casca** | — |

## Ciclo do artista (Onda 3)

| Recurso | Onde está hoje | Estado | Próximo passo |
|---|---|---|---|
| Sprite Studio (desenho, frames, seleção, gestos, HSV) | casca: `Views/SpriteStudioView` (lápis, borracha, balde, conta-gotas, quadros, prévia); tela anterior mantém seleção, HSV e gestos | **casca** (1ª fatia) | **A1**: motor de canvas comum (gestos, zoom, undo por diferença, autosave) e Sprite v2 com camadas e clips. |
| Tile Studio (camadas, colisão, autotile) | `Views/TileStudioView` (camadas, terreno/autotile, tileset) | **casca** | — |
| Música (faixas, padrões, sintetizador, WAV) | `Views/MusicStudioView` | **casca** | — |
| Efeitos sonoros e áudio WAV | `Views/SoundStudioView`, `Views/WaveEditorView` | **casca** | — |
| Partículas, Paleta, Dados | `Views/ParticleStudioView`, `Views/PaletteView`, `Views/DataView` | **casca** | — |
| Recursos do jogo e Biblioteca entre jogos | Biblioteca: `LibraryView` (mesma `FilesDir/Library`); Recursos do jogo: `MainActivity.Assets` | Biblioteca **casca**; Recursos do jogo rota | **A6**: Recursos do jogo na casca e "usar neste jogo" dentro de cada Studio. |
| Studios sem projeto (rascunhos globais) | `Views/StudiosView` (mesma `StudioWorkspace`) | **casca** | — |
| Abas Arte, Mapa e Som da casca | `WorkspaceView.AssetsView` | lista de arquivos | Abrem os Studios da casca quando A1–A3 existirem. |

## Projeto, aprendizado e publicação (Onda 4 e transversais)

| Recurso | Onde está hoje | Estado | Próximo passo |
|---|---|---|---|
| Criar, abrir, Home | `HomeView` + `ProjectStore.Manage` | **casca** | Feito: modelos e orientação ao criar, renomear, duplicar com identidade nova, lixeira com desfazer. Próximo: miniatura do jogo na capa, fixar no início. |
| Pasta de projetos e cópia compartilhada (SAF), revisões ZIP | `HomeView.ExportZip`/`ImportZip`; Ajustes → Pasta de projetos em `ProjectFolderView` sobre `ProjectFolderSync` (Core) e o seletor do sistema na cabeça Android | **casca** (26/11) | — |
| Notas, esboços e mapas mentais | `MainActivity.Notes`/`MindMap`/`Attachments` | Notas em texto na casca; mapas por rota | **P2**: mapas mentais e anexos na aba Notas. |
| Aprender (trilhas, missões, manual offline) | `DocsView` + `DocsLibrary`; `LearnView` sobre `LearningCatalog`/`LearningProgressStore` (mesmo `learning/progress.json`) | **casca** (P3) | Próximo: pista da IA por objetivo (modo Estudar) dentro do desafio. |
| Jogar (Launcher, saves por jogo) | `PlayView` na casca; preparo e `GameplayActivity` pela rota `play-game` | **casca** (P4) | Próximo: "jogado em" e saves por jogo na grade. |
| Gerar APK offline e assinatura | `Views/ApkView` + `PackageGameApk` da cabeça | **casca** | — |
| Git (commit, histórico, remoto) | `Views/GitView` | **casca** | — |
| Explorar referências | `MainActivity.Inspiration*` | rota | **Congelado pelo proprietário** até o portão da Onda 2 (ADD-0036 §4); não reabrir sem decisão dele. |
| Aparência, primeiro uso, ajustes | `SettingsView` (mesmo `settings.json`); pasta e IA por janela própria | **casca** (P7) | Próximo: tema claro nos tokens da casca. |
| Conexão com o Ecosystem (IPC, capabilities) | `Views/ConnectionsView` (Registry do Host, text.inspect local) + `EcosystemConnection` da cabeça Android (pareamento opcional, NN-003) | **casca** (26/11) | — |
| Benchmark e demonstrações | `MainActivity.Benchmark`/`Demo` | ferramentas internas | Ficam enquanto a tela anterior existir. |

## Regra de saída

Uma linha só sai de **rota** quando a casca cobre o recurso com testes, capturas e emulador. Quando todas saírem, `MainActivity` e as rotas são apagadas, e o Lunet fica inteiro na casca.
