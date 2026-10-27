# UC-51 / UC-37 · Selo órfão e Undo/Redo nativos

**Origem:** [Issue #815](https://github.com/AbnerCruz/Ecosystem/issues/815), 26/10/2026.

## Comportamento esperado

O selo de autoria da IA é um identificador de conteúdo, não conteúdo autônomo. Ao esvaziar uma linha inteira que continha texto atribuído à IA, o selo que sobrou isolado nela é removido automaticamente. A quebra de linha permanece, sem colar parágrafos adjacentes. Linhas com texto continuam preservando o selo, mesmo que o usuário tente removê-lo manualmente. Marcas dentro de código literal não são normalizadas.

A exceção é delimitada no domínio (`NoteMarks`): operações que removem uma marca apenas podem passar pelo guarda de segurança caso removam a linha inteira que lhe deu suporte, sem substituir seu conteúdo por outro. A verificação de escrita do vault permanece ativa; remover apenas o selo, reatribuir outro modelo e falsificar marcas continuam proibidos.

A normalização visual ocorre no `VisualSurface` e a de Fonte no `EditorOverlay`; a autoridade persistida continua o Markdown da nota, sem arquivo paralelo de autoria ou migração.

## Barra de ferramentas

Os botões **Desfazer** e **Refazer** aparecem no começo da barra Markdown de forma acessível em Android e desktop, inclusive quando o arquivo `Personalização/editor.json` contém uma ordem antiga personalizada. Arquivos de código ou texto simples recebem os mesmos controles em sua barra. Os botões invocam as operações de edição nativas preexistentes; com o editor Visual sem um bloco ativo, executam a ação no editor de fonte e sincronizam o visual.

## Regressões e gates

Testes adicionados antes da implementação:

- `NoteMarksTests.AiAttributionDisappearsOnlyWhenItsEntireLineIsCleared`: exclusão de linha inteira permitida, remoção isolada e troca do selo proibidas, linhas com conteúdo e blocos de código preservados.
- `EditorAuthorshipProtectionTests.ClearingAnAuthoredLineRemovesOnlyItsOrphanBadgeInBothModes`: Fonte e Visual removem o selo órfão.
- `EditorAuthorshipProtectionTests.FormattingBarIncludesUndoRedoEvenWithPersonalizedButtons`: botões aparecem na barra padrão e na personalizada.

O CI .NET e a suíte headless são necessários para validar regressões; o resultado da execução deve ser consultado no PR. Validação de toque, Gboard, Undo/Redo, histórico e salvamento no Android físico permanece gate independente conforme NN-017. A integridade de um Markdown alterado fora do Urbe não é criptograficamente garantida.
