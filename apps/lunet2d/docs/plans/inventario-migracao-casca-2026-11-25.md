# Inventário de recursos e ordem de migração para a casca (LUNET-536), 25/11/2026

Ordem do proprietário: "não pare enquanto não estiver com todas as features devidamente implementadas, sem faltar nada". Fonte do inventário: o código de `src/Lunet.Android` (74 arquivos, cerca de 19 mil linhas) e o SPEC. Estado em 25/11: **casca** = já na interface nova (`Lunet.Client`); **rota** = abre a tela anterior pela rota (`MainActivity.Route`), nada se perde; **falta** = sem caminho na casca.

## Ciclo do programador (Onda 2)

| Recurso | Onde está hoje | Estado | Próximo passo |
|---|---|---|---|
| Editor de código com cores e números de linha | `CodeSurface` (AvaloniaEdit) | **casca** | — |
| Teclado da IDE (Teclas, Atalhos, Métodos, sugestões do Roslyn) | `LunetKeyboard` | **casca** | — |
| Diagnósticos no código (sublinhado e lista) | `EditorAssistant` (agora em `Lunet.Editor`), `CodeAnalyzer` | **casca** (M1) | — |
| Assinatura ao digitar `(` `,` | `EditorAssistant` | **casca** (M2) | — |
| Ir para definição, referências, renomear, formatar, quick fixes, estrutura, símbolos, buscar, operações de linha | `CommandPaletteView` | **casca** (M3) | — |
| Vários documentos abertos, histórico de posição | `MainActivity.EditorDocuments` | parcial (lista de arquivos) | **M4**: abas de documentos e retomada da posição. |
| Rodar, pausar, passo, velocidade | `GameSession`/`GameView` | **casca** | — |
| Preview isolado (processo separado) e GL | `IsolatedPreview`, `PreviewHost`, `Gles` | rota (Editor completo) | **M5**: preview GL embutido na aba Jogo (Android); rasterizador em CPU fica no desktop. |
| Profiler e Inspector | `MainActivity.Profiler`, `InspectorPanel` | rota | **M6**: painéis Inspector e Desempenho. |
| Console e Problemas | `SupportPanelView` | **casca** | — |
| IA no jogo (chat, modos Estudo/Fazer, diff, desfazer, modelos, chave) | `MainActivity.Chat`/`Ai`/`AiCompact`, `AiRunCoordinator`, `Lunet.Ai` | rota | **M7**: painel de IA na casca sobre o `Lunet.Ai` existente, com revisão por trecho no AvaloniaEdit. |
| IA global | `MainActivity.GlobalAi` | rota (IA, sem jogo) | Junto com M7. |
| Layouts da área de trabalho | `MainActivity.Layout`, `SplitterView` | rota | Substituído pelo layout adaptativo da casca (compacto/expandido); avaliar se ainda faz falta. |

## Ciclo do artista (Onda 3)

| Recurso | Onde está hoje | Estado | Próximo passo |
|---|---|---|---|
| Sprite Studio (desenho, frames, seleção, gestos, HSV) | `SpriteStudioView`, `MainActivity.Studio` | rota (Recursos/Studios) | **A1**: motor de canvas comum (gestos, zoom, undo por diferença, autosave) e Sprite v2 com camadas e clips. |
| Tile Studio (camadas, colisão, autotile) | `TileStudioView` | rota | **A2**: Tile v2 com IntGrid e regras. |
| Música (faixas, padrões, sintetizador, WAV) | `MusicStudioView`, `MainActivity.Music` | rota | **A3**: grade de padrões e piano roll no canvas comum. |
| Efeitos sonoros e áudio WAV | `MainActivity.AudioStudio`, `WaveAudioStudioView`, `AudioPreviewView` | rota | **A4**. |
| Partículas, Paleta, Dados | `ParticleStudioView`, `MainActivity.Palette`, `DataStudioView` | rota | **A5**. |
| Recursos do jogo e Biblioteca entre jogos | `MainActivity.Assets`, `AssetLibrary` | rota | **A6**: grade de miniaturas "usar neste jogo" em cada Studio. |
| Studios sem projeto (rascunhos globais) | `MainActivity.GlobalStudio` | rota (Studios) | Junto com A1–A5. |
| Abas Arte, Mapa e Som da casca | `WorkspaceView.AssetsView` | lista de arquivos | Abrem os Studios da casca quando A1–A3 existirem. |

## Projeto, aprendizado e publicação (Onda 4 e transversais)

| Recurso | Onde está hoje | Estado | Próximo passo |
|---|---|---|---|
| Criar, abrir, Home | `HomeView` | **casca** | Renomear, duplicar, apagar com lixeira, modelos de projeto. |
| Pasta de projetos e cópia compartilhada (SAF), revisões ZIP | `MainActivity.ProjectFolder`, `AndroidProjectFolder` | rota (Ajustes) | **P1**: ajustes de pasta e exportar/importar ZIP na casca. |
| Notas, esboços e mapas mentais | `MainActivity.Notes`/`MindMap`/`Attachments` | Notas em texto na casca; mapas por rota | **P2**: mapas mentais e anexos na aba Notas. |
| Aprender (trilhas, missões, manual offline) | `MainActivity.Learn`/`LearnPanel`, `DocumentationPanel`, `Lunet.Docs` | rota | **P3**: Aprender e Docs como painéis da casca. |
| Jogar (Launcher, saves por jogo) | `MainActivity.GameLauncher`, `GameplayActivity` | rota | **P4**: grade Jogar na casca; o jogo continua no processo `:gameplay`. |
| Gerar APK offline e assinatura | `MainActivity.GameBuild`, `AndroidGameApkSigner` | rota | **P5**: Gerar APK na casca com ícone do jogo e compartilhar. |
| Git (commit, histórico, remoto) | `GitPanel`, `Lunet.Git` | rota | **P6**. |
| Explorar referências | `MainActivity.Inspiration*` | rota | Congelado até o portão da Onda 2 (estudo de 24/11). |
| Aparência, primeiro uso, ajustes | `MainActivity.Appearance`/`Ide.ShowSettings` | rota | **P7**. |
| Conexão com o Ecosystem (IPC, capabilities) | `EcosystemHostClient`, `MainActivity.CapabilityHost`/`EcosystemIpc` | rota | **P8**, sem dependência obrigatória da plataforma de controle (NN-003). |
| Benchmark e demonstrações | `MainActivity.Benchmark`/`Demo` | ferramentas internas | Ficam enquanto a tela anterior existir. |

## Regra de saída

Uma linha só sai de **rota** quando a casca cobre o recurso com testes, capturas e emulador. Quando todas saírem, `MainActivity` e as rotas são apagadas, e o Lunet fica inteiro na casca.
