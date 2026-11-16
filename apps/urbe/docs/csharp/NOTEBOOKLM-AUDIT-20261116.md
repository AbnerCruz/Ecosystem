# Urbe × NotebookLM — auditoria interna, matriz e plano (#956, 16/11/2026)

Fonte da ordem: issue #956. Normas preservadas: consolidação 2.6 §1.1/4.12/9, CST-155/170–174/209–210/229–246. Este registro é
evidência de estudo, não aceite.

## 1. Auditoria interna (código na branch do PR #952, sobre a `main`)

| Capacidade | Estado no código | Onde |
|---|---|---|
| Fontes reais (PDF, EPUB, PPTX, ODP, CBZ, texto, CSV, mídia, web, YouTube) | Existe; leitores nativos, citações por página/trecho/instante | `FilePreviewPanel`, `EpubStudyView`, `PptxReaderView`, `StudyBrowserPanel`, `SourceAnnotations` |
| Chat sobre a fonte aberta, com citação | Existe (CST-243) | `SourceContext`, `AssistantPanel` |
| Quiz | Existe: bloco ```` ```quiz ```` respondido por toque, curso com atividades | `QuizBlock`, `PublicationCourse` |
| Flashcards com repetição espaçada | **Não existe** | — |
| Sessão/caderno de estudo (coleção de fontes, objetivos, progresso) | **Não existe** (há "Estudo" = fonte + nota lado a lado) | `MainView.Study` |
| Mapa conceitual de estudo | **Não existe** (há grafos livres do vault e constelação) | `*.grafo.json` |
| Guia/resumo/roteiro derivado | Existe como nota editorial real (Adaptar, beta.56) | `EditorialNotes` |
| Slides | Existe: layout apresentação, exportação PPTX | `PublicationSlides`, `PublicationPptx` |
| Vídeo explicativo | Existe o motor principal: cenas, keyframes, timeline, MP4, legendas SRT | `VideoComposition`, `PublicationVideoPanel` |
| Áudio/podcast | Parcial: faixas de áudio no vídeo; sem diálogo de duas vozes | `VideoComposition.audio` |
| Infográfico vetorial | **Não existe** como produto | — |

## 2. Matriz de decisões

| Capacidade NotebookLM | Decisão | Racional |
|---|---|---|
| Video Overview (IA gera vídeo) | **Adaptar**: motion graphics editável é o principal; vídeo generativo só opcional e rotulado (CST-173) | Decisão normativa já aprovada |
| Audio Overview | **Reutilizar motor** de áudio do vídeo; roteiro em nota editorial | Sem segundo subsistema |
| Mind map | **Adaptar**: mapa de estudo próprio, reaproveitando o desenho dos grafos; nunca misturado com memória do agente | Domínios diferentes |
| Flashcards | **Implementar**: sintaxe `pergunta::resposta` na nota (a do plugin Spaced Repetition do Obsidian, conhecida pela comunidade); estado do aluno separado (arquivo privado do app) | Lacuna real — **entregue** |
| Quiz | **Reutilizar** `QuizBlock` | Já existe |
| Study guide/FAQ/resumo | **Reutilizar** Adaptar (nota editorial) | Já existe |
| Slides/infográficos | **Reutilizar** Publicações; infográfico vetorial a implementar depois | — |
| Notebook (coleção de fontes) | **Implementar** sessão de estudo como metadados de referências, sem cópias | Lacuna real |

## 3. Plano incremental

1. Flashcards: núcleo (leitura de cartões, agenda de revisão estilo SM-2/Anki, estado do aluno em arquivo privado do app, por vault e cartão, como as posições de leitura) → **Mais ações → Revisar cartões** (Mostrar resposta; Errei/Difícil/Bom/Fácil com quando volta). **Entregue (16/11/2026).**
2. Sessão de estudo: arquivo de metadados com referências por id do vault, objetivos e progresso; abrir/retomar.
3. Mapa de estudo derivado de títulos e links das fontes da sessão.
4. Podcast: roteiro de dois turnos em nota editorial → faixas no motor de áudio existente.
5. Infográfico vetorial em Publicações.

Riscos: estado do aluno é dado novo do usuário (persistência verificada, nunca na memória do agente); TTS no aparelho pode não existir em todos os Android.
