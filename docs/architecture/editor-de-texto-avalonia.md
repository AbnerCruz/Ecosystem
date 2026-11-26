# Editor de texto em Avalonia: estudo, bugs e forma de implementação (26/11/2026)

**Pedido do proprietário (26/11/2026):** o Lunet e o Numa usam Avalonia, e os dois tiveram problemas com o editor. Projetos futuros também terão editores. O pedido foi estudar a fundo: documentação, código-fonte e comunidade, confrontando com o Lunet, com o Numa e com os relatos do próprio proprietário. Em seguida, corrigir o Lunet.

Sintomas relatados no Lunet:
- bugs de cursor;
- teclado sem quebra de linha;
- teclado que não abre no editor nem na IA;
- teclado que fecha sozinho.

**O que este documento é:** referência técnica do Ecosystem para quem for construir ou corrigir um campo de texto em Avalonia no Android. Ele junta três coisas:
- o que se aprendeu lendo o código;
- os defeitos confirmados por teste;
- as regras de implementação.

**O que ele não é:** não é decisão de produto nem cria componente compartilhado. A correção mora no Lunet, e o reuso fica avaliado no fim, como pede a NN-022.

**Versões estudadas:**
- Avalonia 12.1.3, a mesma do Lunet e do Numa;
- AvaloniaEdit, no pacote 12.0.0 e no código do repositório oficial em `9eeba9b` (09/11/2026).

**Escopo da validação:** nada aqui foi testado num aparelho. Os testes reproduzem, linha a linha, a metade do caminho que pertence ao framework (seção 3). O comportamento próprio de cada teclado, como o Gboard e o da Samsung, só se confere no celular (NN-017).

---

## 1. Como o teclado do Android fala com um app Avalonia

Isto é o essencial para entender todos os bugs. As fontes estão no código do Avalonia 12.1.3, em `src/Android/Avalonia.Android/Platform/Input/` e em `src/Avalonia.Base/Input/TextInput/`.

1. **Quem recebe o teclado é o elemento focado.** O `TextInputMethodManager` pergunta ao elemento focado qual é o seu cliente de texto. Faz isso pelo evento `TextInputMethodClientRequested`, e o `TextBox` e o `TextArea` do AvaloniaEdit respondem com um `TextInputMethodClient`.
   - Junto, ele lê as opções do elemento pelas propriedades anexadas de `TextInputOptions`: `Multiline`, `ReturnKeyType`, `ShowSuggestions`, `ContentType` e as demais.
   - Se `InputMethod.IsInputMethodEnabled` for falso, ou se não houver cliente, chama `SetClient(null)`.
2. **`SetClient(null)` fecha o teclado.** O `AndroidInputMethod` chama `HideSoftInputFromWindow`. Isso acontece sempre que o foco sai de um campo, inclusive quando o campo é tirado da árvore visual: o Avalonia limpa o foco de um elemento desanexado.
3. **`SetClient(cliente)` abre o teclado só com `SHOW_IMPLICIT`.** O mesmo vale para o pedido feito no toque, `InputPaneActivationRequested`. A documentação do Android diz que um pedido implícito pode ser ignorado, e o Android de fato o ignora depois que a pessoa fechou o teclado com Voltar. O `EditText` nativo pede de forma explícita, com flags 0, quando é tocado.
4. **A tecla Enter depende de o campo ser declarado de várias linhas.** O `AndroidInputMethod.SetOptions` faz o mapeamento:
   - `ReturnKeyType.Default` sem `Multiline` vira **`IME_ACTION_DONE`**;
   - com `Multiline`, ou com `ReturnKeyType.Return`, vira `IME_FLAG_NO_ENTER_ACTION`.
   No caso Done, o `AvaloniaInputConnection.PerformEditorAction` **esconde o teclado primeiro e só depois manda Enter**.
   - **Nem o `TextBox` (com `AcceptsReturn`), nem o tema Fluent, nem o AvaloniaEdit declaram `Multiline`.** Todo campo de várias linhas sai, então, com Done: o teclado mostra ✓ no lugar de ↵, e o Enter fecha o teclado.
5. **A conexão não guarda texto próprio.** O `TextEditBuffer` lê `SurroundingText` e `Selection` do cliente a cada comando. Ele edita sempre do mesmo jeito: define a seleção pelo cliente, manda a tecla Delete e digita pelo `TopLevel.TextInput`.
   - O teclado compõe a palavra (`setComposingText`), confirma (`commitText`), apaga antes do cursor (`deleteSurroundingText`) e move o cursor (`setSelection`).
   - Tudo passa por `Selection`, que é **um índice dentro de `SurroundingText`**. Se o cliente errar esse índice, ou ignorar uma seleção nova, o texto sai errado.
6. **Composição fora da caixa.** A região da palavra em composição fica no `TextEditBuffer` da conexão, não no cliente. Ela só é zerada quando o teclado chama `finishComposingText` ou quando a conexão é recriada (`RestartInput`).

---

## 2. Bugs encontrados: causa, efeito e prova

**Legenda:**
- **[A]** defeito do AvaloniaEdit;
- **[V]** comportamento do Avalonia;
- **[L]** defeito do próprio Lunet.

"Prova" é o teste em `apps/lunet2d/tests/Lunet.Tests/EditorKeyboardTests.cs`.

| # | Causa confirmada no código | Efeito para a pessoa (relato) | Prova |
|---|---|---|---|
| A1 | `TextAreaTextInputMethodClient.Selection` devolve `Caret.Position.Column`, que começa em 1, como se fosse um índice que começa em 0 (`TextArea.cs`, classe interna). | O teclado "vê" o cursor uma letra à direita. A sugestão, a capitalização e a correção trabalham sobre a letra errada (**bugs de cursor**). | `AvaloniaEditsOwnBridge…`: o texto antes do cursor sai `"var x"` quando o cursor está depois de `"var "`. |
| A2 | O `set` de `Selection` faz `return` quando não há seleção (`selection.StartPosition.Line == 0` numa `EmptySelection`). Toda vez que o teclado diz "selecione esta palavra para trocá-la", nada acontece, e o Delete seguinte apaga a letra **depois** do cursor. | Palavra composta pelo Gboard sai com **letra repetida e letra apagada**. Digitar "ye" antes de `x` em `var x = 1;` produz `var yye = 1;`. Arrastar o cursor pela barra de espaço do Gboard não move o cursor. | `AvaloniaEditsOwnBridge…`: o resultado `var yye = 1;` fica registrado no teste. |
| A3 | O cliente do AvaloniaEdit nunca chama `InputPaneActivationRequested`. Tocar no editor que já tem o foco não pede o teclado. | **Teclado que não abre** ao tocar de novo no código depois de fechá-lo com Voltar. | `ATapOnTheFocusedEditorAsksForTheKeyboardAgain` |
| A4 | O `TextArea` não declara `TextInputOptions.Multiline`. | **Enter fecha o teclado** (Done) e a linha nova vem junto, ou o teclado mostra ✓ em vez de ↵: **"teclado sem quebra de linha" e "teclado fechando sozinho"**. | `TheCodeEditorIsAMultiLineFieldForTheKeyboard…` e o caso "Done" em `AvaloniaEditsOwnBridge…` |
| A5 | O cliente só conhece a linha do cursor e não implementa `ExecuteContextMenuAction`. | O recortar, copiar e colar da barra do Gboard não chegavam ao editor. | Coberto pelo cliente novo (seção 4). |
| V1 | O `TextBox` com `AcceptsReturn` também não declara `Multiline` (item 4 da seção 1). | Na **mensagem da IA** e nas **notas**, Enter fechava o teclado. O Numa já tinha visto isso e corrigido campo a campo (`EditorInput.ConfigureMultiline`, 14/11/2026). | `TheAiMessage…EnterIsALineBreak`, `EveryMultiLineFieldOfTheApp…` |
| V2 | Pedidos de teclado só implícitos (item 3 da seção 1). Comunidade: [Avalonia #15695](https://www.github.com/AvaloniaUI/Avalonia/issues/15695) e a correção [PR #15603](https://www.github.com/AvaloniaUI/Avalonia/pull/15603). Nela, um mantenedor diz que não há API para abrir o painel de entrada a partir do controle. | **Teclado que não abre** no editor nem na IA depois de fechado com Voltar. | `ATapOnAFieldAsksForTheKeyboard…` (o pedido explícito é feito) |
| V3 | Um campo focado que sai da árvore perde o foco, e isso fecha o teclado (itens 1 e 2 da seção 1). | Qualquer redesenho que move o campo fecha o teclado. | Ver L1–L3 |
| L1 | O `DocumentEditor.Rebuild` tirava o editor da árvore e o recolocava num layout novo. Isso acontecia ao aparecer a barra de código (**primeiro toque no código**), ao abrir a busca, ao abrir o menu de arquivo, ao abrir um arquivo e ao conectar um teclado físico. | **Teclado que fecha sozinho logo ao abrir** (o primeiro toque mostra a barra e derruba o teclado). | `TheFirstTouchOnTheCode_BringsTheCodeBarWithoutMovingTheEditor` (zero desanexações) |
| L2 | `WorkspaceView.SetKeyboardOpen` **escondia a folha de apoio quando o teclado abria**, e a IA mora nessa folha. O campo de mensagem ficava invisível, perdia o foco, e o teclado fechava. | **"Teclado que não abre na IA"**: ele abria e fechava no mesmo instante. | `TheAiMessage_KeepsItsSheetFocusAndKeyboard…` |
| L3 | O compositor da IA tirava e recolocava o campo de mensagem a cada troca de modo (Estudar/Fazer), envio ou atualização. Os botões de modo e de enviar pegavam o foco. | O teclado fechava ao escolher Fazer ou ao enviar. | Mesmo teste: Fazer não tira o foco |
| L4 | `WorkspaceView.Rebuild`, que roda a cada mudança de estado do jogo, desanexa todo o espaço de trabalho. | Rodar, parar ou falhar o jogo enquanto se escreve (código e jogo lado a lado) fechava o teclado. | `AGameStateChangeWhileWriting_KeepsTheFocusOnTheCode` |

**Relatos anteriores que o estudo explica:**
- **Numa, 14/11:** o teclado fechava e abria a cada troca de bloco (achado 14 do Numa). É o V3, resolvido lá por troca de foco direta.
- **Numa:** o teclado não reabria depois de Voltar (achado 15). É o V2, resolvido lá com pedido explícito.
- **Numa:** "interrompe a escrita e perde o texto" (CST-144). É um caso do V3 combinado com regravação do texto.
- **Lunet, 25/11:** "a digitação do editor, ms extremamente alto". O teclado próprio desenhado em Avalonia foi abortado. Com o teclado do sistema, os itens A1–A4 e L1–L3 viraram os sintomas relatados agora.

---

## 3. Método: o simulador do caminho Android

Testar teclado sem aparelho costuma ser "chamar `TextInput` e ver o texto". Isso não exercita o que quebra. Por isso:

- `apps/lunet2d/tests/Lunet.Tests/AndroidImeSimulator.cs` é uma **transcrição linha a linha** do `TextEditBuffer`, do `EditCommand` e do `AvaloniaInputConnection` do Avalonia 12.1.3. Ele implementa `SetComposingText`, `CommitText`, `DeleteSurroundingText`, `SetSelection`, `FinishComposing` e Enter.
- O Delete e a digitação passam pela janela headless, como as teclas do Android passam pela `TopLevel`.
- Ele também calcula a ação de Enter que o `AndroidInputMethod.SetOptions` daria ao campo focado, Done ou quebra de linha.
- **Teste de regressão do próprio framework:** o mesmo simulador, aplicado ao cliente original do AvaloniaEdit, **reproduz os bugs** (A1, A2, A4). Se uma versão futura do AvaloniaEdit corrigir esses bugs, esse teste falha, avisando que a ponte do Lunet pode sair.
- **Limite:** o simulador não decide como o Gboard decide, por exemplo quando compor ou quando confirmar. Prova que o framework e o app fazem a sua parte certa. Continua pendente conferir no aparelho com o Gboard e o teclado da Samsung (seção 7).

---

## 4. Forma de implementação (regras para todo campo de texto Avalonia no Android)

**R1. Declare todo campo de várias linhas.**
```csharp
TextInputOptions.SetMultiline(campo, true);
TextInputOptions.SetReturnKeyType(campo, TextInputReturnKeyType.Return);
```
- Faça isso **por estilo global**, e não campo a campo, para nenhum campo novo nascer com o bug. Seletor: `TextBox` com `AcceptsReturn=true`; no Lunet é `CodeInput.MultilineTextBoxes()`.
- Campo de uma linha continua com Done, o que é certo: o Enter confirma e fecha.

**R2. Em código, desligue sugestões.** Use `TextInputOptions.SetShowSuggestions(editor, false)`, que o Avalonia traduz em `TYPE_TEXT_FLAG_NO_SUGGESTIONS` mais `VISIBLE_PASSWORD`.
- O teclado deixa de compor palavras e de autocorrigir sobre o código.
- Isso elimina a classe inteira de bugs de composição (item 6 da seção 1, [Avalonia #16956](https://github.com/AvaloniaUI/Avalonia/issues/16956)). Editores de código no celular fazem o mesmo.
- Acentos continuam no toque longo.
- Em prosa (notas e IA), as sugestões ficam ligadas.

**R3. Com AvaloniaEdit, use uma ponte de teclado correta.** No Lunet é `CodeInputClient`, instalado por um handler de `TextInputMethodClientRequestedEvent` na instância. Esse handler roda depois do handler de classe do AvaloniaEdit e substitui o cliente. A ponte precisa:
- devolver `Selection` como índice em `SurroundingText`, começando em 0;
- mover o cursor no `set` mesmo sem seleção;
- usar como `SurroundingText` uma **janela de linhas inteiras em volta do cursor**, cerca de 1 KiB de cada lado.
  - A janela fica **parada enquanto o cursor está dentro dela**: as pontas são `TextAnchor`s que acompanham as edições.
  - Assim os índices de um comando do teclado não mudam no meio do comando.
  - Mandar o arquivo inteiro custaria uma cópia do texto a cada consulta, e o Android consulta várias vezes por tecla. O Numa mediu esse custo no `TextBox` (limite 1 do estudo de 14/11).
- pedir o teclado num toque (`InputPaneActivationRequested`);
- executar Recortar, Copiar, Colar e Selecionar tudo (`ExecuteContextMenuAction`);
- pedir `RequestReset()` ao carregar outro texto, para o teclado esquecer a composição antiga.

**R4. Peça o teclado explicitamente no toque.**
- O app chama `ShowSoftInput(view, 0)` no Android depois que o foco assentou no campo tocado. O Avalonia não expõe essa chamada.
- Só vale para toque **no próprio campo**. Uma tecla da barra de código ou o ⌄ "fechar teclado" não reabrem o teclado.
- O Numa faz o mesmo (`MainActivity.ShowKeyboard`).

**R5. Nunca tire da árvore um campo que pode estar focado.**
- Monte o quadro uma vez e reconstrua só o que está em volta: a lista de arquivos, a barra de busca e o menu.
- Troque visibilidade e conteúdo dos vizinhos, nunca o pai do campo.
- Quando um redesenho inevitável move o campo, devolva o foco a ele logo depois. A regra vale para o mesmo lugar: mesma aba e mesmo painel. Ir a outra aba é a pessoa saindo, e aí o teclado sai junto.

**R6. Não esconda o contêiner do campo que o teclado veio servir.** Ao abrir o teclado, abas e folhas podem sair da frente. A folha que contém o campo focado fica.

**R7. Botões usados durante a escrita não pegam foco.** Exemplos: enviar, modo, teclas da barra de código. Use `Focusable = false` ou um toque tratado num `Border`.

**R8. Teste pelo caminho do Android, não por `TextInput` direto.** Use o simulador da seção 3, com toque headless (`TouchBegin`/`TouchEnd`), porque o mouse segue outro caminho de foco no Avalonia.

---

## 5. Confronto: Lunet e Numa

| Regra | Lunet (depois desta correção) | Numa (Urbe C#), hoje |
|---|---|---|
| R1 | Estilo global: todo `TextBox` com `AcceptsReturn`. | Campo a campo (`EditorInput.ConfigureMultiline`): Fonte, Visual, IA e IA compacta. **Ficam sem a regra:** OCR (`OcrStudyView`), respostas a perguntas do agente e instruções e prompts de agentes (`AssistantPanel.Views`). Nesses campos, o Enter ainda fecha o teclado. |
| R2 | Código sem sugestões. | Não se aplica: é prosa, com `TextBox`. |
| R3 | `CodeInputClient` no lugar do cliente do AvaloniaEdit. | Não se aplica: não usa AvaloniaEdit. O `TextBox` do Avalonia tem cliente correto, mas manda o texto inteiro ao teclado, o que pesa em notas longas (limite 1 do Numa). |
| R4 | Pedido explícito no toque, para qualquer campo. | Pedido explícito no toque, no editor (achado 15). |
| R5/R6 | Editor e mensagem da IA não saem da árvore; a folha da IA fica com o teclado; o foco volta depois de redesenho. | Troca de bloco sem tirar a caixa velha antes (achado 14). |

**Recomendação ao Numa:** aplicar o R1 por estilo global, o que fecha os campos que faltam. Fica registrada aqui. Este trabalho não toca no Urbe, porque o pedido foi corrigir o Lunet. O Urbe tem roadmap próprio.

---

## 6. Avaliação de reuso (NN-022)

**Classificação:** `possible-candidate`. Dois Products sofreram do mesmo defeito do framework, e projetos futuros também vão sofrer. Mesmo assim, **nada é extraído agora**:
- o Numa não usa AvaloniaEdit, então a ponte do R3 tem um consumidor só;
- o R1 é uma linha de estilo, e um pacote compartilhado custaria mais versionamento do que remove.

O que se compartilha é **este documento e as regras R1–R8**. Quando um segundo Product usar AvaloniaEdit no celular, abre-se a Extraction Review da ponte e do simulador.

---

## 7. Não testado no aparelho (NN-017 / ADD-0035)

Os testes headless provam a metade do framework e a do app. Conferir no celular, com Gboard e com o teclado da Samsung:
- Enter no código, nas notas e na IA quebra a linha sem fechar o teclado;
- o primeiro toque no código abre o teclado e a barra de código, e o teclado fica;
- depois de fechar o teclado com Voltar, um toque no código ou na IA o abre de novo;
- digitar palavras e acentos no código sem letra repetida ou apagada;
- arrastar o cursor pela barra de espaço;
- recortar, copiar e colar pela barra do teclado;
- a IA com o teclado aberto: a folha continua visível acima do teclado;
- trocar Estudar/Fazer e enviar sem o teclado fechar.

---

## 8. Fontes

**Código** (Avalonia 12.1.3, tag `12.1.3`; AvaloniaEdit `master` em `9eeba9b` e pacote 12.0.0):
- [`AndroidInputMethod.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Android/Avalonia.Android/Platform/Input/AndroidInputMethod.cs)
- [`AvaloniaInputConnection.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Android/Avalonia.Android/Platform/Input/AvaloniaInputConnection.cs)
- [`TextEditBuffer.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Android/Avalonia.Android/Platform/Input/TextEditBuffer.cs)
- [`EditCommand.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Android/Avalonia.Android/Platform/Input/EditCommand.cs)
- [`InputMethodManager.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Input/TextInput/InputMethodManager.cs)
- [`TextInputOptions.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Input/TextInput/TextInputOptions.cs)
- [`TextBoxTextInputMethodClient.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Controls/TextBoxTextInputMethodClient.cs)
- [`FocusManager.cs`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Input/FocusManager.cs)
- [`AvaloniaEdit/Editing/TextArea.cs`](https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Editing/TextArea.cs) (classe `TextAreaTextInputMethodClient`)
- [`EmptySelection.cs`](https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Editing/EmptySelection.cs)

**Documentação:**
- [Avalonia — Text Input and IME](https://docs.avaloniaui.net/docs/input-interaction/text-input)
- [Avalonia — Input Pane](https://docs.avaloniaui.net/docs/services/input-pane)
- [API `TextInputMethodClient`](https://api-docs.avaloniaui.net/docs/T_Avalonia_Input_TextInput_TextInputMethodClient)
- [Android — `InputMethodManager.showSoftInput`](https://developer.android.com/reference/android/view/inputmethod/InputMethodManager#showSoftInput(android.view.View,%20int))
- [Android — `EditorInfo.IME_FLAG_NO_ENTER_ACTION`](https://developer.android.com/reference/android/view/inputmethod/EditorInfo#IME_FLAG_NO_ENTER_ACTION)
- [Android — `InputType.TYPE_TEXT_FLAG_MULTI_LINE`](https://developer.android.com/reference/android/text/InputType#TYPE_TEXT_FLAG_MULTI_LINE)

**Comunidade:**
- [Avalonia #15695](https://www.github.com/AvaloniaUI/Avalonia/issues/15695): o teclado não reaparece num TextBox já focado.
- [PR #15603](https://www.github.com/AvaloniaUI/Avalonia/pull/15603): correção e comentário do mantenedor sobre não haver API para abrir o painel.
- [Avalonia #15694](https://www.github.com/AvaloniaUI/Avalonia/issues/15694): alças de seleção em campo vazio.
- [Avalonia #16956](https://github.com/AvaloniaUI/Avalonia/issues/16956): composição de sílabas.
- [Discussão #14274](https://github.com/AvaloniaUI/Avalonia/discussions/14274): `stateAlwaysHidden` não basta.
- [Issues do AvaloniaEdit](https://www.github.com/AvaloniaUI/AvaloniaEdit/issues): #352 (IME de chinês que para depois de rolar) e #374 (falha no Android).
- Os mesmos sintomas em outros frameworks mostram que a causa é o contrato do Android, não um app:
  - [Obsidian — teclado liga e desliga ao editar](https://forum.obsidian.md/t/android-app-when-editing-soft-keyboard-switches-on-and-off-characters-are-inserted-inside-wiki-links-and-before-headings/51556);
  - [Obsidian — teclado não abre em nota nova](https://forum.obsidian.md/t/android-soft-keyboard-doesnt-open-when-creating-a-new-note/119357);
  - [Unity — mudar de campo esconde o teclado](https://issuetracker.unity3d.com/issues/moving-focus-from-one-text-field-to-another-hides-keyboard-sometimes-on-android);
  - [Zotero — teclado some ao digitar](https://forums.zotero.org/discussion/127954/android-keyboard-vanishing-while-typing-in-a-text-annotation).

**Relatos do proprietário e estudos anteriores:**
- [Numa — editor, 14/11/2026](../../apps/urbe/docs/csharp/EDITOR-REVIEW-20261114.md), achados 14, 15 e 16.
- [Numa — estabilidade de interação, 26/10/2026](../../apps/urbe/docs/csharp/EDITOR-INTERACTION-STABILITY-20261026.md).
- [Lunet — teclado nativo, 25/11/2026](../../apps/lunet2d/docs/research/teclado-nativo-ide-2026-11-25.md).
- [Lunet — casca Android, 25/11/2026](../../apps/lunet2d/docs/research/casca-android-avalonia-2026-11-25.md).
