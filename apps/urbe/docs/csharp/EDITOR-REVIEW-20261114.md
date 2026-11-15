# Editor — estudo, investigação e correções (14/11/2026)

Pedido do proprietário (14/11/2026): foco exclusivo no editor; estudar o framework, códigos de referência e editores
abertos, investigar e testar tudo (seleções, cliques, fluidez, coisas indesejáveis) e entregar uma release.
Branch `ccr-c717cbe5-9x21rb`, base `0dc7c75` (main com a beta.52). Autoridade de escopo: Numa 2.6 §4.3 (CST-015–026,
128, 129, 142–144, 147). Este registro é evidência de execução, não um segundo roadmap.

## 1. Método

1. **Auditoria interna** de `VisualSurface` (Visual, um bloco por vez), `EditorOverlay` (Fonte, guarda de autoria,
   salvamento), `MarkdownReader` (desenho dos blocos), `MathRenderer` (fórmulas) e dos testes existentes.
2. **Código-fonte do framework, versão exata usada** (Avalonia 12.1.3, clonado do repositório oficial):
   `TextBox.cs`, `Presenters/TextPresenter.cs`, `TextBoxTextInputMethodClient.cs`,
   `Android/Avalonia.Android/Platform/Input/{AndroidInputMethod,AvaloniaInputConnection,TextEditBuffer,EditCommand}.cs`,
   `Media/TextFormatting/{TextLayout,TextRunCache}.cs` e o template Fluent `Controls/TextBox.xaml`.
3. **Código do renderizador de matemática**: CSharpMath (`Display/Typesetter.cs` — `MakeTable`, `MakeRowWithColumns`;
   `Displays/InnerDisplay.cs`, `Displays/ListDisplay.cs`; `Extensions.CollectionWidth`) e as issues do projeto
   (#32, correção itálica; #239, medição de equações de várias linhas).
4. **Modelos de editor de referência** (conceitos, sem importar código): o "live preview" de editores de Markdown em
   documento único (CodeMirror 6/Obsidian: o texto é a fonte e só a decoração muda), o modelo de blocos (Notion:
   Backspace no início junta ao bloco de cima, setas atravessam blocos), o histórico agrupado por palavra de editores
   de texto (VS Code, ProseMirror `history`).
5. **Medição**: perfis com `dotnet-trace` (formato speedscope) sobre digitação e troca de bloco numa nota de 14,5 KB,
   em build Release no runner headless.
6. **Testes que tentam quebrar**: um teste de *fuzz* (`EditorFuzzUiTests`) faz sequências aleatórias de toques,
   digitação, Backspace, seleção, setas (com e sem Shift) numa nota com selo de IA, código, fórmulas, listas e
   tarefas, e confere a cada passo: digitar nunca fecha o bloco, nunca some uma letra, Fonte e Visual concordam, e a
   gravação aceita o resultado. 150 sequências passam localmente (10 por padrão no CI; `URBE_FUZZ_SEEDS=n`).

## 2. Achados e correções (todos reproduzidos por teste antes de corrigir)

| # | Achado (causa confirmada) | Efeito para a pessoa | Correção | Prova |
|---|---|---|---|---|
| 1 | Depois de digitar num bloco, os blocos abaixo continuavam com as posições do último desenho. `Edit` procurava o bloco tocado por uma posição velha. | O primeiro toque num bloco abaixo, depois de digitar, abria o bloco de cima ou o mesmo bloco de novo: o toque "não pegava" e o teclado não vinha para aquela linha (CST-015/016). Marcar uma tarefa abaixo mudava a linha de outro bloco. **Existe na beta.52** (testes vermelhos na main). | As posições acompanham cada tecla (`ShiftBlocksAfter`). | `ASingleTapOnAnotherBlockWhileWritingEditsIt`, `ATaskTickedBelowAfterTypingChangesItsOwnLine` |
| 2 | O Visual recompunha os selos `{ia:…}` a partir do bloco como foi aberto; o `EditorOverlay` conferia cada mudança contra a nota atual. Quando discordavam, o overlay chamava `SetText(current)`. | A letra digitada era desfeita e o bloco fechava (teclado sumia) — "interrompe a escrita e perde o texto" (CST-144) em notas com texto da IA. | O Visual relê os selos a partir da nota atual quando as leituras discordam; o overlay nunca desfaz texto digitado na caixa (seus selos são só os que `AiAuthorship` compõe). | `TypingInBlocksJoinedWithAuthoredTextKeepsWriting`, fuzz |
| 3 | A guarda de gravação (`MainView.SaveNoteAsync`) compara com o último texto salvo; selos compostos bloco a bloco em várias gravações podiam sair diferentes. | A nota não salvava ("A marca de autoria da IA é protegida…") depois de certas edições no Visual. | Antes de gravar, os selos são relidos do último texto salvo (`EditorOverlay.SealsForSave`); o texto da pessoa não muda e nenhum selo é inventado. | fuzz (assert de gravação) |
| 4 | Cada desenho do Visual tirava a caixa em edição da árvore e a punha numa célula nova. O Avalonia limpa o foco de um elemento desanexado e o cliente de IME perde o presenter (`TextBox.OnDetachedFromVisualTree` → `SetPresenter(null)`). | Marcar uma tarefa (ou qualquer redesenho) enquanto escrevia fechava o bloco e o teclado. | A célula da caixa é o mesmo controle de um desenho ao outro e nunca sai da árvore. | `TickingATaskWhileWritingKeepsWriting` |
| 5 | Sair de um bloco redesenhava a nota inteira (`Children.Clear()` + `MarkdownReader` de todos os blocos), e recolocar os filhos invalidava a medida de todo o texto. | Lentidão ao trocar de bloco em notas longas (CST-129, "não está fluido"). | Desenhos de blocos sem mudança são reaproveitados e a lista de filhos é reconciliada (só entra/sai o que mudou). Troca de bloco numa nota de 14,5 KB: ~42 ms → ~20 ms de trabalho do editor (Release, runner). | `LeavingABlockRedrawsOnlyThatBlock`, perfil |
| 6 | Tocar num link de outro bloco enquanto se escreve seguia o link. | Sair da nota no meio de uma frase ao tentar pôr o cursor. | Enquanto um bloco é escrito, o link é lugar para o cursor; um chip **Abrir** sob o cursor segue o link. Lendo (sem bloco aberto), o toque segue o link como antes. | `ALinkTappedWhileWritingPlacesTheCaret…`, `ALinkTappedWhileReadingIsFollowed`, `EditorLinksTests` |
| 7 | Reabrir a mesma nota (listas, busca, atualizações, escrita do Assistente) refazia tudo. | O bloco em edição fechava sem motivo. | Mesmo texto: nada acontece. Texto mudado em outro lugar: rolagem mantida e o mesmo bloco volta a ser escrito, cursor no lugar. | `OpeningTheSameNoteAgain…`, `ANoteChangedElsewhere…` |
| 8 | Desfazer no Visual só conhecia a caixa aberta. | Depois de sair de um bloco, Desfazer não fazia nada ou se misturava ao histórico da Fonte. | Histórico da nota inteira (`EditHistory`: diferenças, digitação agrupada por palavra), Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z e a barra; o bloco da mudança desfeita volta a ser escrito. | `EditHistoryTests`, `UndoInVisualGoesBackAcrossBlocks…` |
| 9 | Um documento em blocos sem teclado entre blocos. | Setas, Backspace e Delete paravam na borda do bloco. | ↑/← no início vão ao bloco de cima (↑ mantém a coluna), ↓/→ no fim ao de baixo; Backspace no início e Delete no fim juntam blocos de texto (a Markdown muda exatamente esse caractere, como na Fonte); bloco vazio some e volta ao fim do de cima; em código/fórmula/tabela a tecla só leva o cursor ao vizinho. | `BackspaceAtTheStart…`, `DeleteAtTheEnd…`, `ArrowsCrossBlocks…`, `Backspace…CodeBlock…` |
| 10 | Seleção presa a um bloco. | Não dava para selecionar texto de dois blocos no Visual. | Shift+setas na borda, Shift+clique e Ctrl+A duas vezes (nota inteira) — a caixa passa a conter os blocos selecionados. | `ShiftArrowsSelectAcrossBlocks`, `ShiftClickSelects…`, `CtrlATwice…` |
| 11 | Ao entrar num bloco, a Markdown aparecia crua. | "Salto" visual: negrito, links e títulos viravam texto plano com marcas. | O bloco em edição desenha a própria Markdown formatada (`MarkdownTextPresenter` + `MarkdownSyntax`): negrito, itálico, código, links, tags, fórmulas e comentários com seu estilo, marcas esmaecidas. Todos os caracteres continuam no texto (cursor, seleção e IME iguais). | `MarkdownSyntaxTests`, `TheBlockBeingWrittenShowsItsMarkdownFormatted` |
| 12 | CSharpMath mede uma linha de matriz a partir da primeira célula desenhada (`CollectionWidth` = direita − esquerda dos filhos), não do início da linha. | Em `\begin{pmatrix}10 & 2\\3 & 40\end{pmatrix}` o parêntese direito (ou o que vem depois) era desenhado sobre a última coluna (CST-142). | Depois da composição, cada linha passa para onde está sua primeira célula e as células voltam o mesmo tanto (nada muda na página); larguras ficam corretas e delimitadores e o resto da linha são reposicionados (`MathRenderer.TableWidths`). | `TheClosingDelimiterOfAMatrixIsDrawnAfterItsLastColumn` (vermelho antes), `WhatFollowsAMatrixIsNotDrawnOverIt` |
| 13 | Fórmula em destaque mais larga que a coluna. | Cortada à direita (só rolando de lado). | Reduzida para caber (até 60%, legível); só o que ainda não cabe rola (`FormulaFit`). | `FormulasOnANarrowScreenAreDrawnWhole` |

| 14 | Ao trocar de bloco, a caixa velha saía da árvore antes da nova receber o foco. O Avalonia limpa o foco do elemento removido (`FocusManager.ClearFocusOnElementRemoved`) e o gerenciador de texto fica sem cliente: no Android, `AndroidInputMethod.SetClient(null)` chama `HideSoftInputFromWindow`, e a caixa nova chama `ShowSoftInput` de novo. | **Teclado fechando e abrindo sozinho** a cada troca de bloco (toque, setas, Backspace/Delete, Desfazer), e às vezes não reabrindo. | A caixa velha fica na árvore, invisível, até a nova tomar o foco diretamente dela (`HandOver`/`Retire`); os painéis movem filhos no lugar (`Move`) em vez de tirar e recolocar. Mantida uma caixa nova por bloco: o IME recebe estado limpo (o `TextEditBuffer` do Avalonia guardaria a região de composição da caixa anterior). | `MovingBetweenBlocksNeverLeavesTheKeyboardWithoutATextField` |
| 15 | O app pedia o teclado com `SHOW_IMPLICIT`, que o Android ignora depois que a pessoa fechou o teclado (Voltar). Tocar no bloco já em edição só passava pelo pedido implícito do Avalonia. | **Teclado não abrindo** ao tocar numa linha depois de fechá-lo. | Toque direto é pedido explícito (flags 0, como o `EditText`), também no bloco em edição e na Fonte. | `AFingerOnTheBlockBeingWrittenAsksForTheKeyboard` (headless; o efeito no Android depende de DEVICE) |
| 16 | Formatação: `**palavra **` (espaço da seleção dentro das marcas) não é lido como negrito; seleção de várias linhas quebrava as marcas; `_` dentro de palavra não é itálico; `*x*` não era reconhecido como itálico (o botão embrulhava de novo); na Fonte, Título inseria `## ` no meio da palavra (comportamento da 1.8.4). | **Formatação errada**, asteriscos/sublinhados crus na nota. | Espaços ficam fora das marcas, um par por linha (marcas de lista fora), `*` dentro de palavra, `*x*` e `__x__` reconhecidos, e Fonte usa as mesmas ações do Visual (título/lista/tarefa/citação no início da linha). | `EditorFormattingTests`, `Fonte_ToolbarFormatsTheLineAndTheSelection` |

Cursor: além dos 12 casos, o toque cai na letra certa com fórmula inline, tags, comentário escondido, lista aninhada,
comentário de palavra, selo de IA e ênfase aninhada (20 casos); o cursor continua visível digitando novas linhas no
fim da tela e quando o teclado abre (`TheCaretStaysInSight…`, `WhenTheKeyboardOpens…`). Com fórmula inline ou
comentário de palavra antes do ponto tocado, o texto depois pode descer uma linha ao virar caixa (a fonte é maior que
o desenho); o cursor fica na letra certa.

Precisão do toque (sem mudança de código, agora coberta): tocar numa letra de negrito, link wiki com apelido, link
Markdown, título, item de lista, tarefa, citação, código inline, riscado ou parágrafo quebrado põe o cursor naquela
letra, e a linha não se move ao virar caixa (`ATapPutsTheCaretOnTheLetterTapped…`, 12 casos).

## 3. Medições (runner headless, Release; não são latência de aparelho)

| Medida | Antes | Depois |
|---|---|---|
| Trabalho do editor por troca de bloco (perfil, 60 trocas, nota de 14,5 KB) | ~42 ms | ~20 ms |
| Tecla no Visual (com quadro renderizado) | ~15 ms | ~15 ms (só o bloco é refeito) |
| Tecla na Fonte, nota de 14,5 KB | ~43 ms | ~43 ms (ver limite 1) |

## 4. Limites conhecidos (não resolvidos aqui, com o motivo)

1. **Fonte em notas longas.** O perfil mostra ~95% do custo por tecla dentro do `TextBox` do Avalonia
   (`TextPresenter.CreateTextLayout` refaz o layout do texto inteiro a cada tecla, várias vezes; no Android o IME
   ainda recebe o texto todo como `ExtractedText`). O Visual não tem esse custo porque cada caixa contém um bloco.
   Resolver exige um controle de edição próprio virtualizado com cliente de IME — decisão de arquitetura (NN-011),
   não tomada aqui.
2. **IME do Android ao trocar de caixa.** No código do Avalonia, comandos de composição ainda na fila de uma conexão
   antiga são aplicados ao cliente atual (`TextEditBuffer` lê `Client` dinamicamente). Não há como reproduzir sem o
   aparelho; os casos de troca indevida de caixa (achados 1 e 4) foram removidos, o que reduz a exposição.
3. **Selo dentro de crases no meio da linha.** Se um bloco de código for puxado para o meio de uma linha (``` depois
   de texto), os selos seguintes são lidos como código e `AiAuthorship` acrescenta um selo a cada leitura; a gravação
   pode recusar. Backspace/Delete não juntam mais blocos de código (achado 9), mas uma seleção apagada ainda pode
   produzir esse texto. Correção pertence a `AiAuthorship` (UC-51).
4. **Correção itálica** de glifos (CSharpMath #32) continua ausente fora de matrizes.

## 5. Validação no aparelho (NN-017) — pendente

Nada disto foi tocado num celular. Conferir no Android (moto g32) e no Windows: toque após digitar (achado 1),
escrita em nota com texto da IA (2, 3), marcar tarefa escrevendo (4), fluidez ao trocar de bloco em nota longa (5),
links e chip Abrir (6), Desfazer/Refazer (8), Backspace/setas com teclado físico e Gboard (9), seleção entre blocos
(10), aparência do bloco formatado e IME com palavra em composição (11), matrizes e fórmulas largas (12, 13),
teclado que não fecha ao trocar de bloco (14), teclado que volta ao tocar depois de fechado com Voltar (15),
formatação pela barra nos dois modos (16).
