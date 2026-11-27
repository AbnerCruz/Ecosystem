# Conclusão integral do Lunet — relatório vivo (26/11/2026)

**Missão:** o documento "LUNET — GOAL de continuidade, consolidação e conclusão integral" (26/11). Este é o único relatório da missão. Ele guarda o estado recebido, os erros encontrados (com causa, correção e prova) e a matriz de estado.

**Rótulos de estado:** "Código" quer dizer só integrado à `main`. "Release" quer dizer publicado. "Aparelho" quer dizer que alguém conferiu num celular. Nada aqui foi testado num celular.

## H0 — Estado recebido

| Item | Fato verificado |
|---|---|
| `main` ao assumir | `c32c498` (merge do #1053, trabalho do agente GPT: estabilizar música e reabrir WAV sem jogo) |
| `VERSION` na `main` | `0.0.1-dev.1000609` |
| Releases | 1000606, 1000607, 1000608 e 1000609 publicadas com APK arm64. A 1000609 cobre o #1053. |
| PRs abertos do Lunet | Só o #1038 ("publicar E1–E3 como dev.1000593"). Está obsoleto: o E1–E3 já está na `main` (`cd258e55`) e foi publicado em releases posteriores. Não deve ser publicado com tag velha (§2 do GOAL). |
| Trabalho do GPT | Integrado: commits `8a62d80` e `90d1108`, merge `c32c498`. Nenhum commit sem PR encontrado no remoto. Não foi feito cherry-pick redundante. |
| Branch de trabalho | `ccr-0dc592a4-9d3w98`, avançada por fast-forward até a `main`. O force push foi recusado pela política, e não foi necessário. |

## Erros encontrados e corrigidos nesta missão

| # | Erro | Causa comprovada | Correção | Prova |
|---|---|---|---|---|
| E-01 | O build dos testes quebrou com CS0121 ("a chamada é ambígua") em `CameraTests` linhas 234 e 250, depois de acrescentar `Draw(Texture2D, RectangleF, Color)` | `new(40, 50)` sem tipo explícito converte para qualquer tipo de destino. Com a nova sobrecarga, `Draw(textura, new(x, y), cor)` passou a combinar com `Vector2` e com `RectangleF`. Jogos de usuários que escrevem assim também quebrariam. | `[OverloadResolutionPriority(-1)]` na nova sobrecarga, o mesmo recurso que o `SpriteBatch` já usa. O código antigo continua resolvendo para `Vector2`. | `RectangleDrawTests.DrawTextureRectangleColor_…` (chamada `Draw(tex, new(5, 6), cor)`), mais `CameraTests` verdes. O agente de teste reproduziu o erro antes da correção. |
| E-02 | Na aba Código, o botão **Rodar** cobria as teclas da barra de código | O botão flutuante ficava a 16 dp do fundo do editor, e a barra de código também fica no fundo do editor. Com um painel aberto, quando o editor recupera o foco, a barra volta e fica embaixo do botão. | `WorkspaceView.PlaceRunButton`: o botão sobe pela altura da barra e acompanha o redimensionamento dela. | Captura `celular-9a-objetivos` (antes e depois) e `RunButtonLayoutTests.Rodar_NeverCoversTheCodeBar` (retângulos sem interseção). |

| E-03 | Os Fixados da Home nova eram gravados em `settings.json` (`PinnedGameIds`), mas as telas antigas já guardavam fixados e recentes por GameId em `launcher-preferences.json`. Eram duas autoridades para o mesmo conceito, o que viola a NN-001. | Faltou auditar `LauncherPreferences` antes de criar o campo (erro do próprio agente, encontrado no H6). | O campo novo foi removido. A Home usa `LauncherPreferences`, o mesmo arquivo das telas antigas. "Continuar" usa `LastProjectId`, e abrir um jogo registra o recente. Se o arquivo estiver danificado, ele não é sobrescrito. | `HomeOrderTests` grava e lê `launcher-preferences.json`, e `Continue_PrefersTheGameOpenedLast` cobre o "Continuar" |

| E-04 | A Home quebrava com um jogo cujo `lunet.json` não tinha `name` (`ProjectException` em `HomeView.GameIdOf`) | A leitura do GameId para Fixados e Continuar só capturava erros de I/O e de JSON | `ProjectException` também é capturada. O jogo continua listado e só deixa de poder ser fixado. | A suíte completa (1.344 testes) reproduziu o erro em `ProjectFolderTests`, que agora passa |
| E-05 | Durante uma execução da IA, abrir outro jogo trocava a sessão no meio, mostrava "Parar" para outro jogo e, no fim, recarregava do disco o jogo aberto, o que perderia texto ainda não salvo | `EnsureSession` não considerava a execução em andamento, e o recarregamento final não conferia o jogo | A execução mantém a sessão do próprio jogo e avisa qual jogo está alterando. O recarregamento só acontece se o jogo da execução for o jogo aberto. Depois disso, a conversa passa para o jogo aberto. | `ARunKeepsItsGame_AndNeverReloadsAnotherGameWithUnsavedText` |

| E-06 | Na aba **Jogo** (também em Recursos e Notas), a barra de abas do workspace sumia, e o único caminho de volta ao código era o Voltar | O estado "teclado aberto" usava a barra de código mesmo quando a aba Código não estava visível. A barra, aberta uma vez em Código, escondia as abas em todas as outras. | A barra de código só conta quando o Código está na tela (`CodeVisible`), e o `Rebuild` recalcula o estado a cada troca de aba | Captura `celular-4-jogo` (antes e depois) e `WorkspaceTabsTests` |

| E-07 | `--screens`, o gerador de capturas, gravava progresso, Biblioteca, rascunhos dos Studios e ajustes no AppData **real** de quem o rodava. A contagem de "Minhas criações" subia a cada execução: 29, 31, 33. | A `MainWindow` sempre apontava para `AppData/Lunet`, e o gerador de capturas a reaproveitava | `MainWindow(projectsRoot, dataRoot)`: o gerador de capturas passa uma pasta temporária, e o app normal continua no AppData | Capturas `2l-studios` antes (33 itens) e depois (só os 2 do cenário) |
| E-08 | Em Jogar, no celular, aparecia uma capa por linha, embora o código previsse duas | A conta da largura descontava a margem de uma capa só, e a segunda quebrava a linha | A largura passa a considerar a margem das duas capas | Captura `2j-jogar` antes e depois |

| E-09 | O job `apk` do CI (jornada da casca no emulador Android 13) reprovou: "estado=Running quadros=0 → Nenhum quadro renderizado". O #1054 foi integrado mesmo assim. | Com a M5, o jogo roda no renderer GLES, mas `GameSession.FramesRendered` só contava quadros da CPU e ficava em 0 | O `PreviewRenderer` conta os quadros desenhados na thread GL (`Interlocked`), e a `GameSession` lê essa contagem quando o jogo está na GPU. O log do emulador também mostra "Failed to find ColorBuffer". Se a contagem continuar em 0, o problema é de renderização GLES dentro do `NativeControlHost` e será tratado a seguir. | Log do job `140048563727`; `GpuPreviewTests` lê a contagem da GPU; o CI do PR seguinte vai confirmar |

- **E-11:** Esc não fechava Jogar (nem as outras folhas) quando nada estava focado, e os atalhos F5, Ctrl+S e F1 também falhavam nesse caso. A causa: o atalho ficava na casca, e a tecla, sem foco, chega só à janela. Agora o atalho fica na janela. Achado pelo `BackSequenceTests`.

- **E-12:** ao sair do estúdio de música por outro destino com a prévia ainda renderizando, o som tocava por cima da tela seguinte. Agora a prévia para quando o estúdio sai da tela. Achado pelo `MusicLeaveTests`.
- **E-13 (perda de dados):** com um Studio aberto na página Studios, tocar em Jogar, Aprender ou Início na barra global descartava o desenho ou a música ainda não salvos. Voltar salvava; a barra não. Agora a troca de destino fecha o Studio pelo mesmo caminho do Voltar, que salva. Achado e provado pelo `StudioLeaveSavesTests`: música e sprite, por Jogar e por Início.

- **E-14 (dados na atualização):** a casca lia só os fixados por GameId. Quem atualiza de uma versão cuja Home salvava fixados e o último jogo por nome (preferências v1) os perdia na Home nova. A conversão para GameId (`UpgradeProjectReferences`) existia, mas só a tela anterior a chamava. Agora a casca a executa na primeira leitura; nomes sem jogo correspondente são mantidos. Achado e provado pelo `UpgradeDataTests`.

## Entregas (código na branch e depois na `main`; nada conferido num celular)

| Frente | O que mudou | Prova |
|---|---|---|
| Anexo B, parte A | `System.Drawing.Primitives` entrou na lista obrigatória do compilador do jogo, sem `System.Drawing.Common`. `Size` e `Point` funcionam com alias e não colidem com `Color` nem com `RectangleF`. | `SizeOverloadTests.TheGameCompilerSeesSizeAndPoint_…`, que usa o `GameCompiler` real |
| Anexo B, parte B | `RectangleF(Vector2, Vector2)`. Sobrecargas com `Size` em `CreateSolid`, `FromPixels`, `SetVirtualResolution` e `RenderTarget2D`, cada uma encaminhando para a escalar (mesma validação). | `RectangleDrawTests`, `SizeOverloadTests` |
| Anexo B, parte C | `Draw(RectangleF, Color)` e `Draw(Texture2D, RectangleF, Color)`. Produzem os mesmos vértices, UV, cor e lotes que `FillRect` e a sobrecarga completa, reaproveitam o pixel branco e não alocam no caminho aquecido. | `RectangleDrawTests` |
| H6 — Objetivos | Apoio **Objetivos** na IDE: mostra a missão ligada ao jogo pelo GameId, a lista de objetivos gravada no mesmo arquivo de progresso de Aprender, os critérios de aceite, a referência offline e uma pista no modo Estudo. A pista não escreve código nem envia nada sozinha. | `ObjectivesTests`, captura `9a-objetivos` |

| H3 — M5 | A aba Jogo no Android roda o jogo na GPU: o `PreviewHost` e o `PreviewRenderer` GLES das telas antigas, sem mudança, hospedados num `NativeControlHost`. A `GameSession` dirige a prévia: compilar, pausar, passo, reiniciar, teclas e parar. Uma falha atrasada de uma execução já parada é ignorada. O app pausa e retoma a superfície GL junto com a Activity. Inspector e Desempenho medem o próprio jogo na GPU. Não há chip de velocidade. | `GpuPreviewTests`, com uma prévia falsa; build do APK. A superfície GL real só roda no Android. |

| H4 — streaming | O painel de IA redesenhava toda a conversa (Markdown, código e diffs) a cada pedaço de texto ou passo recebido. Agora atualiza só o bloco ao vivo, no máximo a cada 100 ms. Quem subiu a rolagem para ler não é puxado de volta. | `AiStreamingRenderTests`: 200 atualizações ao vivo e zero reconstruções |

| UX §4.1 / §6.1 | A barra global agora é Início, Jogar, Aprender, Studios e Mais, nessa ordem, no celular e no desktop (antes eram seis destinos no desktop, e Biblioteca e Ajustes ficavam soltos). Mais reúne Lunet IA, **Documentação** (agora global, com o mesmo leitor offline), Biblioteca, Explorar (só onde existe a tela, sem link morto) e Ajustes. O título é "Área de trabalho". | `GlobalNavigationTests` (desktop e Android); capturas |

| UX §6.5 / §6.1 / §6.4 | Studios agrupados em Arte (Sprite, Mapa, Paleta), Áudio (Música, Efeito, Tom WAV) e Outros (Partículas, Tabela), todos tonais e com ícones distintos. O menu do jogo na Home tem Editar e Jogar, por um só caminho (`LunetShell.Play`). Abrir ou criar a missão em Aprender abre o jogo com os **Objetivos** ao lado do código. | Capturas `2l-studios` e `2j-jogar`; `LunetClientLearnTests` atualizado |

| Linguagem visual §5 | As bordas decorativas saíram dos cartões de cursos em Aprender, do cartão de alterações da IA e das miniaturas da Biblioteca; a profundidade agora vem do tom. Ajustes ganhou botão de voltar visível e o mesmo cabeçalho de subpágina de Aprender e Studios. | Capturas `2f-aprender` e `2k-ajustes` |

| UX §6.3 — decisão do teclado | **Divergência resolvida com a fonte.** O pedido original (PR #1040, 25/11) era um teclado próprio da IDE. Depois, o proprietário escreveu na sessão de 25/11: "A digitação do editor, ms extremamente alto, não da pra digitar, vamos abortar a ideia de ter um teclado nativo mas não abortar a ideia de ter varias abas". O PR #1047 aplicou isso: o teclado do sistema digita, e uma barra única tem as abas Teclas, Atalhos e Métodos. **Decisão vigente:** IME do sistema mais a barra de três abas. Nenhum caminho com dois teclados. A ponte de teclado e o Enter foram corrigidos no #1051. | Ordem citada; `CodeBar` e `EditorKeyboardTests` |
| UX §6.6 | O painel da IA do jogo usava uma cópia do formulário de configuração. Agora reaproveita o formulário único (`AiSettingsView`). | Teste `AiPanel_IsConfiguredOnceForTheWholeApp` atualizado |

| H4 — co-work | O painel de IA da casca não passava anexos, percepção de imagem, documentação nem pesquisa web, que a tela anterior do Android passava. Agora passa os quatro. Há catálogo de modelos com filtro, páginas e cache, e o id manual continua. A execução segura o processo com serviço em primeiro plano e notificação. | `AiCoworkShellTests` (3), `ModelCatalogViewTests` (2); build Android verde. Não foi testado no aparelho nenhum destes: notificação, permissão do Android 13, IA em segundo plano. |

## H5 — Matriz dos Studios (§6.5): criar → salvar → reabrir → publicar → usar no jogo

As provas são testes headless que já existiam (exceto onde marcado) e foram conferidos na suíte de 27/11 (1.348 testes verdes). "—" significa sem teste dedicado. Nada foi conferido num celular.

| Tipo | Criar / editar / salvar | Reabrir | Publicar na Biblioteca | Usar em outro jogo |
|---|---|---|---|---|
| Sprite | `SpriteStudio_DrawsFillsUndoesAnimatesAndSavesSourceAndSheetTogether`, `SpriteStudio_SelectsMovesFlipsDuplicatesAndErasesPixels` | `StandaloneStudio_WritesAndReopensEditableSpriteWithoutProject` | `PublishAndImport_CopyBothEditableSourceAndDerivedSpriteWithoutSharedDependency` | o mesmo teste e `Game_LoadsSpriteStudioExportFromProjectAssetsThroughHost` (o runtime carrega o PNG) |
| Mapa (Tile) | `TileStudio_CreatesAMapPaintsLayersUndoesAndSaves_AndEditsItsTileset`, `TileStudio_PaintsTerrainWithAutotileAndManagesLayers` | `Painting_FixesNeighbours_AndErasingReopensEdges` | `StudioPublishesAndForksAllFamiliesWithoutAnotherProjectAsSource` | `StandaloneLibrary_PublishesAndCopiesIntoTwoDifferentProjects` |
| Música | `MusicStudio_StartsWithASong_TogglesNotesPlaysAndSaves`, `MusicStudio_SetsNoteLengthInstrumentMixerAndArrangement` | `MusicStudio_InvalidDraftRemainsIntactAndCanBeClosed` (fonte inválida preservada, #1053) | `PublishingRenderedOutputUsesExactSourceAndDoesNotRewriteTheDraft` (sem WAV atrasado) | `StudioPublishesAndForksAllFamilies…` |
| Efeito (Sound) | `SoundStudio_GeneratesVariesPlaysAndSavesSourceAndWavTogether` | `SoundEffectReopenTests` (novo: reabre idêntico, mesmo WAV) | `StudioPublishesAndForksAllFamilies…` | `StudioPublishesAndForksAllFamilies…` |
| Tom (WAV) | `CreateEditAssetsPreviewExportAndReopen` e os testes de WAV do #1053 | os testes de reabrir WAV sem jogo, do #1053 | `StudioPublishesAndForksAllFamilies…` | `StudioPublishesAndForksAllFamilies…` |
| Partículas | `ParticleStudio_RunsTheRealEmitter_BurstsAndSavesTheEffect` | `ParticleStudioTests` (ida e volta) | `StudioPublishesAndForksAllFamilies…` | `Content_LoadsAStudioEffectAsAReadyEmitter` (o runtime usa o efeito) |
| Paleta | `Palette_PresetRampExtractRemove_SavesAndFeedsTheSpriteStudio` | `ProjectPaletteTests` (ida e volta) | `StudioPublishesAndForksAllFamilies…` | `StudioPublishesAndForksAllFamilies…` (importa em dois jogos) |
| Dados (Tabela) | `DataStudio_CreatesATableEditsRecordsAndColumnsAndSavesDataAndSchemaTogether` | `DataStudioTests` (ida e volta) | `StudioPublishesAndForksAllFamilies…` | `StudioPublishesAndForksAllFamilies…` (importa em dois jogos, com a tabela referenciada) |
| Sem jogo (rascunho global) | `Studios_WithoutAGame_CreateEditSaveInTheStudioFolder_AndPublish` | `StandaloneStudio_WritesAndReopensEditableSpriteWithoutProject` | idem | `StandaloneLibrary_PublishesAndCopiesIntoTwoDifferentProjects` |
| Segurança da Biblioteca | `UnsafePaths_AreRejectedWithoutPublishingProject`, `PublishRevision_RejectsSymlinkedAssetDirectory…`, `ExistingDestinationFiles_AreNotOverwrittenByStudioLibraryImport`, `InterruptedBundleRestoresPreviousBytesWhenReopened`, `InvalidPersistedMetadata_CannotCrashLibraryOrModifyTarget` | | | |

**Matriz completa em teste automatizado.** Todas as etapas têm prova para todos os tipos: `StudioPublishesAndForksAllFamilies…` publica cada família e a importa em dois jogos (Paleta e Dados inclusive). Só falta a conferência num celular.

## H9 — Jornadas UX01–UX14 (anexo A §8.1): estado em 27/11

As provas são testes headless da casca real e as capturas de `--screens`, em 412×915 e 1280×800. Nenhuma jornada foi conferida num celular.

| ID | Estado | Prova | O que falta |
|---|---|---|---|
| UX01 | Headless ok | `GlobalNavigationTests`, `LunetClientTests` (criar jogo vazio), captura `1-home-vazia` | Instalação limpa no aparelho |
| UX02 | Headless ok | `HomeOrderTests` (busca, Continuar, Fixados), `EditorSession_ReopensTheGameOnTheFileAndCaretYouLeft` | Aparelho |
| UX03 | Feito, exceto aparelho | `EditorKeyboardTests` (IME, Enter, desfazer); `MethodsJourneyTests`, numa jornada só: Métodos insere `Color.Red` com o tipo, volta para Teclas e entra em Recentes; a assinatura aparece ao digitar a chamada; o jogo compila e roda com o código inserido | Conferir no aparelho, com o teclado do sistema |
| UX04 | Headless ok | `AiStreamingRenderTests`: fechar e reabrir sem cancelar, sem reconstruir a conversa a cada pedaço de texto, sem cruzar jogos | Aparência compacta e expandida no aparelho |
| UX05 | Headless ok | `ObjectivesTests`, `LunetClientLearnTests` (missão → Objetivos → Referência) | Aparelho |
| UX06 | Headless ok | `Studios_WithoutAGame_…`, `StandaloneLibrary_PublishesAndCopiesIntoTwoDifferentProjects`, matriz H5 | Aparelho |
| UX07 | Parcial | Testes de música e WAV do #1053 (render fora da thread de UI, fonte inválida preservada); `MusicLeaveTests` (sair durante o render não toca nada depois, E-12); `StudioLeaveSavesTests` (trocar de destino salva, E-13) | Reproduzir no aparelho |
| UX08 | Parcial | `GpuPreviewTests` (falha de compilação não abre a GPU), `PlayView` com o caminho único `LunetShell.Play` | Partida em tela cheia no aparelho; volta à grade |
| UX09 | Feito, exceto aparelho | `GlobalNavigationTests` (Documentação global), Referência exata nos Objetivos; `ProblemDocsTests`: cada problema ao vivo tem um botão de Documentação, que abre a busca do nome citado pelo compilador sem sair do jogo, e Voltar retorna ao código | Conferir no aparelho |
| UX10 | Headless ok | `ProjectFolderTests` (conectar, gravar, recusar revisão alheia, restaurar, alternar) | Acesso revogado e cancelamento no seletor do aparelho |
| UX11 | **Não feito** | Só há capturas em 412×915 e 1280×800 | 320 e 360 dp, paisagem e passagem por ≥ 840 dp em todas as telas |
| UX12 | Feito, exceto aparelho | `AccessibilityAuditTests` em 11 destinos: todo controle tem nome para o leitor de tela e alvo mínimo de toque (interruptores subiram de 32 para 48 px). A fonte do sistema amplia a casca até a largura lógica de 320; testado com a fonte máxima. Captura `celular-2-home-fonte-grande` | Conferir no aparelho: TalkBack e Tamanho da fonte do Android |
| UX13 | Parcial | Streaming da IA sem reconstruir a conversa; `TypingLatencyTests` executado em 27/11 (ver §Latência) | Muitos projetos e recursos; medir no aparelho |
| UX14 | Feito, exceto aparelho | `BackSequenceTests`: Voltar e Esc fecham, nível por nível, as 11 folhas da Home (a IA vai de Ajustes para a conversa e depois fecha). No jogo, Voltar vai de painel para código e de código para Início; no Início, sai do app | Gesto de Voltar do Android no aparelho |

## Pendências conhecidas

- **UX §4.1, tela larga:** resolvido. A partir de 840 dp aparece uma barra lateral com os mesmos cinco destinos. A captura mostrou que as linhas de largura total ignoravam a barra e saíam cortadas; isso foi corrigido. (não declaradas como feitas)

- **M5, isolamento:** resolvido. Ajustes → Jogo → "Preview isolado" abre o jogo na Activity de processo separado (`IsolatedGpuPreview`). Na volta, a aba recolhe o console e acusa se o processo morreu (o arquivo marcador). Taxa de atualização alta também voltou a Ajustes.
- **M5, aparelho:** falta conferir no celular a superfície GL dentro da casca (toque, rotação, troca de aba, minimizar e voltar).

- **Anexo B, parte A:** falta conferir no Preview do Android e no APK exportado, com trimming, que o `System.Drawing.Primitives` carrega no aparelho. Isso depende do CI de APK e do celular.
- **#1038:** precisa ser fechado administrativamente, com prova de que as releases posteriores já contêm o E1–E3. Fechar PR de outro agente fica a cargo do proprietário ou do integrador.


## Latência de digitação (27/11/2026, runner Linux headless, não aparelho)

`TypingLatencyTests`: 189 teclas pelo caminho do teclado Android simulado (`AndroidImeSimulator`), com o documento renderizado a cada tecla.

| Cenário | p50 | p95 | máx |
|---|---|---|---|
| Arquivo de 3.000 linhas (~150 KB), 3 execuções | 19,1–20,5 ms | 28,4–33,4 ms | 31–42 ms |
| Arquivo de 30 linhas | 20,0 ms | 25,9 ms | 43,7 ms |
| Referência: `TextBox` vazio do Avalonia, mesmo runner | 10,9 ms | 14,1 ms | — |

Leitura dos números:
- A latência não cresce com o tamanho do arquivo: nenhuma tecla lê o documento inteiro, e o teclado recebe só uma janela de texto.
- Cerca de 11 ms por tecla é custo do próprio runner, que renderiza um quadro a cada tecla. O editor (AvaloniaEdit mais o cliente de IME) acrescenta cerca de 9 ms.
- **E-10:** a primeira execução falhou na compilação, não no teste; a segunda passou.
- Não há número do Moto G32, e nenhum é alegado. A medição no aparelho continua pendente.

## Matriz G01–G16 (Parte II do GOAL): estado em 27/11/2026

Legenda:
- **Automatizado:** teste no runner ou CI, sem aparelho.
- **Pendente:** ainda não executado.
- **Aparelho:** depende de celular, e nenhum foi usado.

| ID | Cenário | Estado | Evidência | Falta |
|---|---|---|---|---|
| G01 | Assumir o trabalho anterior e reconciliar com a `main` | Feito | §H0 deste relatório; #1054 integrado (3da594e); #1056 sobre a `main` | — |
| G02 | Música inválida, recuperar, tocar e trocar de tela durante a exportação | Automatizado | Testes do #1053 (fonte inválida preservada, render fora da UI); `MusicLeaveTests` (E-12); `StudioLeaveSavesTests` (E-13) | Aparelho: ANR e áudio real |
| G03 | WAV independente, editar, reabrir, publicar e copiar para o jogo | Automatizado | Matriz H5 (Studios) | Aparelho |
| G04 | Digitação, IME PT-BR, Métodos, assinatura, desfazer e rotação | Automatizado | `EditorKeyboardTests`, `AndroidImeSimulator`, `MethodsJourneyTests`, `TypingLatencyTests` (p50 ≈ 20 ms no runner, sem rebuild por tecla) | Aparelho: IME real, rotação, latência no Moto G32 |
| G05 | Código, Preview GLES, pausar, passo, Inspector e retorno | Automatizado/CI | `GpuPreviewTests`; jornada `apk` do CI no emulador com quadros reais (E-09) | Aparelho: toque, rotação, minimizar |
| G06 | IA Estudo e Fazer, revisão, arquivo alterado durante a execução, desfazer | Automatizado | `AiStreamingRenderTests` (não recarrega outro jogo com texto não salvo, E-05) e `AiCoworkShellTests` | Aparelho com provedor real |
| G07 | IA, minimizar, trocar de jogo, notificação e cancelar | Automatizado (parcial) | A execução mantém o jogo (E-05); hold liberado mesmo com falha (`AiCoworkShellTests`); serviço em primeiro plano compila | Aparelho: notificação, permissão, morte do processo |
| G08 | Sprite com camadas e animações, Tile, áudio, publicar e usar no jogo | Automatizado | Matriz H5 completa em testes | Aparelho |
| G09 | Aprender T01, objetivo, pista, Docs, Preview e reabrir | Automatizado | `ObjectivesTests`, `LunetClientLearnTests`, `ProblemDocsTests` | Aparelho |
| G10 | ZIP, importar, exportar por SAF, cancelar, revogar e recuperar | Automatizado (ZIP) | 44 testes rodados em 27/11. `ProjectZipImportTests` e `ProjectFolderSnapshotTests` (30) cobrem limite de entradas, caminho fora da pasta, duplicados, 512 MiB, ZIP corrompido e stream não posicionável. `ProjectManageTests`, `ProjectJourneyTests` e `ProjectFolderTests` (14) cobrem exportar, importar como jogo novo e salvar o aberto antes | Aparelho: SAF real, cancelar o seletor, revogar a permissão |
| G11 | SpriteBatch ordenado e legado; ciclo de vida do Context | Automatizado | `SortedBatchTests`, `SortedBatchEdgeTests`, `SortedBatchConsumerTests` (E1.6/E1.7) | Scene2D no modo ordenado |
| G12 | RectangleF(pos, size), Draw(rect, color), Size e Point | Automatizado | `RectangleDrawTests`, `SizeOverloadTests` (compilador do jogo) | Preview do Android e APK exportado, com trimming |
| G13 | Gerar o APK do jogo, instalar e rodar | Automatizado (geração) | 13 testes rodados em 27/11: assinatura (`AndroidGameApkSignerRegressionTests`), orientação (`GameApkOrientationTests`), alinhamento do ZIP (`OfflineApkZipAlignmentTests`), `ProjectJourneyTests` | Instalar e rodar o APK do jogo num aparelho |
| G14 | Atualizar o Lunet existente e conferir dados | Automatizado (parcial) | `UpgradeDataTests`: preferências v1 (fixados e último jogo por nome) chegam à Home nova (E-14 corrigido). Arquivos de IA, Notas e progresso mantêm os mesmos caminhos | Aparelho: instalar por cima da versão anterior |
| G15 | Matriz de migração e todas as rotas | Feito, exceto aparelho | Revisão rota a rota em §H8 abaixo: duas rotas ficam na tela anterior, com motivo; a lacuna de Notas encontrada foi portada (`NotesImportTests`) | Aparelho |
| G16 | PR integrado, release oficial, APK e SHA | Em andamento | #1056 no CI; a release 1000611 sai após a integração | Confirmar o APK e o SHA da release |

**Conclusão honesta:**
- **Ainda não validado no aparelho:** nada desta missão foi testado num celular. Cada item marcado como Aparelho continua aberto, conforme o NN-017.
- **Pendentes, sem prova automatizada:** G14 (atualização sobre uma instalação existente, só no aparelho). G13 tem prova da geração do APK, mas não da instalação.


## H8: rotas da tela anterior × casca (27/11/2026)

A tela anterior do Android (`MainActivity.Route`) atende 15 rotas. A casca chama só duas delas: `explore` e `play-game`.

| Rota | Na casca | Observação |
|---|---|---|
| studios | `StudiosView` | Sem jogo e dentro do jogo |
| learn | `LearnView` | |
| play | `PlayView` | |
| play-game | Tela anterior | Partida em tela cheia na Activity de jogo. A casca chama `PlayGame`; o caminho é único (`LunetShell.Play`) |
| library | `LibraryView` | |
| settings | `SettingsView` | |
| project-folder | `ProjectFolderView` | SAF no Android |
| ai-settings | `AiSettingsView` | Agora com catálogo de modelos e pesquisa web |
| ai | `AiPanelView` / `GlobalAiView` | Agora com anexos, imagens, documentação, web e segundo plano |
| git | `GitView` | |
| assets | `ResourcesPage` | Inclui importar |
| notes | Aba Notas (`DocumentEditor`) | **Lacuna encontrada e portada:** a casca não importava imagens nem arquivos para `Notes/`. Agora importa (64 MB, escrita atômica, sem sobrescrever), lista os arquivos e, com um toque, anexa à IA |
| apk | `ApkView` | |
| explore | Tela anterior | Congelado pelo proprietário (ADD-0036 §4); acesso e dados preservados, sem ampliar |
| editor | — | Rota vazia (volta ao editor) |

**Remoção da UI anterior:** não foi feita. O GOAL só permite remover depois da paridade e do teste no aparelho e no emulador. `play-game` e `explore` ainda usam a tela anterior, e nada foi testado num celular.
