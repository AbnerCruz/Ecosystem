# Ordens do proprietário — estado item por item

Quadro das ordens dadas em comentários e issues (PR #952, #954, #955, #956, #963, #902), conferido em 17/11/2026 contra o código do ramo do PR #952. Cada linha diz o estado real e a prova. **Feito** = código com teste no ramo. **Parcial** = parte feita, o resto dito. **Aberto** = não feito. **Aparelho** = depende do Android/Windows real: nada aqui conta como validado no aparelho (NN-017).

Este arquivo é a lista de tarefas das ordens; o andamento por CST continua em [CONSOLIDATION.md](CONSOLIDATION.md).

## Memória dos agentes (PR #952, 15/11/2026)

| Item da ordem | Estado | Prova |
|---|---|---|
| P0: `recall_memory`/`list_conversations` falhavam com inteiros (`offset`, `max_chars`, `limit`, `start`) | Feito; aparelho | `IntegerArgumentsTests` (todo parâmetro `integer` do catálogo pelo caminho do runtime) |
| P0: paginação sem lacunas, duplicações nem truncamentos | Feito (17/11) | `PagingThroughRecallReturnsEveryMemoryExactlyOnce` |
| P1: aprender preferências, decisões e procedimentos em várias conversas | Feito; aparelho | `MemoryLearningTests`, `MemoryPreferenceFormsTests` |
| P1: formas comuns de dizer (“Eu gosto…”, “Mudei de ideia:…”, amo, detesto, “adorei X, mas…”) | Feito (17/11) | `EveryCommonFormIsLearned` (7 formas que antes eram ignoradas) |
| P1: contradições resolvidas sem apagar (substituição datada e ligada) | Feito | `AStrongWordContradictsTheMildOneAcrossConversations`, `ForgettingTheReplacementDoesNotReviveTheObsoleteMemory` |
| P1: preferência específica não é contradição da geral (strogonoff de frango × strogonoff) | Feito | `PraiseThenCriticismLearnsThePraiseAndKeepsTheSpecificApart` |
| P1: episódios com pedido truncado | Feito (17/11) | `AnEpisodeKeepsTheWholeRequestAndEveryChangedFile` |
| P1: busca por tema devolvia fragmentos de execução | Feito | episódios depois do que a pessoa disse, na busca e no contexto (`WhatThePersonSaidReachesTheContextBeforeManyRunLogs`) |
| Observação: “o usuário só vê memórias episódicas no contexto” | Feito (17/11) | mesma prova: com 60 execuções, as preferências e procedimentos entram antes |
| P1: não vazar lembranças entre agentes | Feito | `Delegation_TheSpecialistKeepsItsModelPermissionsAndMemoryApart`, `Memory_PrivateAndSharedAreToldApartAndFiltered` |
| P1: grafo de memória navegável (UC-87/88.4) como projeção, com proveniência, conflitos, versões e navegação às fontes | Feito; aparelho | `MemoryGraphTests`, `MemoryGraph_OpensAsAProjectionAndWritesNothing` |
| P1: grafo com muitas lembranças (escala) e conforto no Android/tablet | Parcial | 2.000 lembranças viram grafo completo em menos de 1,5 s no computador (`TwoThousandMemoriesBuildQuicklyWithEveryOneOnTheGraph`); desenho e toque no aparelho pendentes |
| Observação: troca de modelo no export (`gpt-6-luna-pro` × `deepseek`) | Feito | `ConversationModelTests` (o registro acompanha o modelo efetivo) |
| Observação: memória achada pelo plural (“estrelas” → “Estrela”) | Feito | `MemoryPluralTests` |
| Sinônimo puro sem palavra em comum | Limite declarado | busca por palavras, sem embeddings; vazio orienta o agente a tentar os termos da pessoa (`ASynonymMissIsToldAndTheRetryFindsIt`) |
| Aceite: aprendizagem comprovada em conversas reais no Android | Aparelho | — |

## PDF/anexos (#954, #963 caso 01)

| Item | Estado | Prova |
|---|---|---|
| Alocação de 512 MiB por anexo PDF | Feito; aparelho | `BoundedReadTests` |
| Erro ambíguo “não encontrado ou acima do limite” | Feito | `Documents_AMissingPathSaysWhyAndSuggestsTheFile` |
| `list_notes` escondia PDFs e imagens | Feito | `ListNotesFilesTests` |
| PDF de 450 páginas vindo de Downloads, no aparelho | Aparelho | — |

## Auditoria dos 8 exports (#963)

| Caso | Estado | Prova |
|---|---|---|
| 02, 03, 05, 06 — contradições entre conversas | Feito | testes de memória acima |
| 04 — somente leitura × memória | Feito | `ReadonlyMemoryTests` |
| 07 — busca irrelevante (“receita” de jogo) | Parcial | orientação ao agente; eficiência só se mede em conversas reais |
| 08 — elogio seguido de crítica | Feito (17/11) | `PraiseThenCriticismLearnsThePraiseAndKeepsTheSpecificApart` |
| Versão `0.1.0` no export | Feito | prévia diz `…-previa` |
| Excluir a branch de diagnóstico | Aberto, de propósito | a ordem é excluir só **depois** da correção validada no aparelho |

## Chat e pop-up (#955)

| Item | Estado | Prova |
|---|---|---|
| Estudo de ChatGPT, Claude e outros, com comunidade | Feito | `CHAT-UX-STUDY-20261115.md` |
| Etapas do agente em palavras | Feito | testes de atividade do Assistente |
| Pop-up: auditoria visual, dois desenhos, painel acima da digitação | Feito; aparelho | capturas em 360, 900 e deitado (`PopupLook_Capture`) |
| Estado de erro no cartão compacto | Feito (17/11) | `NoteAgent_AFailingProviderIsToldInTheCompactCardWithoutLeavingTheNote` |
| Cópia exata de código e texto | Feito | `Chat_CodeAndTextAreCopiedExactlyWithoutSideEffects` |
| Conversa longa sem travar a interface | Parcial; aparelho | `Assistant_ALongConversationDoesNotHoldTheUiThread` |
| Aprovação do proprietário ao novo pop-up | Aparelho | — |

## NotebookLM/estudo (#956)

| Item | Estado | Prova |
|---|---|---|
| A. Sessões de estudo por referência, sem cópia | Feito | `StudySessionTests` |
| C. Flashcards (SM-2) e quiz | Feito | `FlashcardsTests`, `CourseTests` |
| Mapa da sessão | Feito | `StudyMapTests` |
| E. Podcast de duas vozes com roteiro em nota editorial | Feito; aparelho | `PodcastTests` |
| Infográfico vetorial (SVG + PNG) | Feito | `InfographicTests` |
| D. Produtos pelo motor de Publicações, vídeo por motion graphics | Feito | testes de Publicações e vídeo |
| Aceite 2: mesma fonte em 3 sessões e 2 Publicações, revisão detectada | Feito (17/11); aparelho | cada sessão guarda a versão estudada de cada nota e, ao abrir, diz o que mudou e de que tipo (texto, títulos e ligações, só formatação), com “Marcar como revistas”; publicações já diziam o que mudou desde a exportação (`EverySessionWithTheNoteSeesTheChangeAndItsKindUntilReviewed`, `StudySession_ReferencesSourcesAndOpensOneInEstudo`, `Publication_ReopeningSaysWhatChangedSinceTheExport`) |
| Aceite 4: editar relações do mapa de estudo sem tocar o grafo de memória | Parcial | mapas separados; edição das relações a conferir |
| Aceite 5: editar uma cena e re-renderizar o MP4 | Parcial | vídeo regenera inteiro; render focalizado a conferir |
| Aceites 1, 8 e 9 no aparelho | Aparelho | — |

## Estudo no celular (#902)

| Item | Estado |
|---|---|
| Painel vazio e seletor universal, PDF textual com citação | Feito (ver CST-232/233/239/240) |
| Retomada de leitura, destaques e anotações | Feito (CST-239 a 241) |
| OCR e figuras do livro | Feito (CST-242) |
| Navegador de estudo | Feito em parte (CST-235) |
| Provas no aparelho de toque, IME, rotação e tablet | Aparelho |
