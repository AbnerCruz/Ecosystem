# UC-57 / Issue #805 — editor responsivo e chat com viewport sob controle do usuário

Data: 26/10/2026. Implementação: C# nativo Avalonia; PR #808. Sem WebView, motor JavaScript alternativo ou mudança no formato das notas.

## Problemas observados no código da main

1. `EditorScrolling.BandBottom=2/3` ordenava rolar assim que o caret entrava no terço inferior. `EditorOverlay` e `VisualSurface` também invocavam correção de caret quando o índice mudava, inclusive num toque, sem digitação. O editor da nota tinha `ScrollViewer.BringIntoViewOnFocusChange` no padrão `true`: foco e corretor próprio competiam com a pessoa.
2. Visual alterna `MarkdownReader` e `TextBox` por bloco; cada transição reconstruía blocos e tentava preservar âncora chamando layout síncrono. Nenhum segundo motor deve ser introduzido; reduzir reposicionamentos desnecessários e preservar a proteção de rolagem por gesto.
3. `EditorOverlay.OnTextChanged` calculava `LegacyEditorTools.Stats` sobre toda a nota em cada atualização; estatísticas em notas longas não precisam disputar o tempo de edição com o IME. A gravação/autosave, texto, alterações e histórico permanecem imediatos; só a atualização dos números é postergada ao repouso.
4. `AssistantPanel.Schedule` reconstruía a apresentação no streaming e `ScrollEnd` seguia a cauda se perto do fim. Não existia estado claro de navegação manual: um gesto podia ser seguido por novo movimento automático. `BringIntoViewOnFocusChange` também era padrão `true` no chat.
5. As bolhas de mensagens do usuário eram `SelectableTextBlock`, porém não traziam ferramentas de cópia, seleção parcial e edição/reenvio; as respostas do assistente já tinham ações de cópia e seleção.
6. Em Visual, seleção integral pelo campo do bloco incluía somente o bloco. Não existia ação explícita para selecionar o Markdown inteiro.

## Referências verificadas, sem importar dependências

- `AvaloniaUI/Avalonia/src/Avalonia.Controls/TextBox.cs`: `CaretIndex`, `SelectionStart/End`, `SelectAll`, `TextChanged` e seleção gerida pelo controle nativo.
- `AvaloniaUI/Avalonia/src/Avalonia.Controls/ScrollViewer.cs`: `ScrollChanged`, `Offset`, `BringIntoViewOnFocusChange` e distinção entre rolagem do host e interações do usuário.
- Documentação Avalonia: `https://docs.avaloniaui.net/docs/how-to/scrollviewer-how-to`; `https://docs.avaloniaui.net/controls/layout/containers/scrollviewer`; `https://docs.avaloniaui.net/api/avalonia/controls/textbox`.
- Código existente em `EditorOverlay`, `EditorScrolling`, `TouchTextScroll`, `VisualSurface`, `AssistantPanel` e `TextActions`; preservei as proteções de dedo/movimento de UC-73/UC-75/UC-76 e o autosave de UC-57.

## Mudanças técnicas

- Uma rolagem feita pelo leitor impede que o assistente ou corretor de cursor reposicione a tela. Acompanhar o fluxo do chat só ocorre enquanto o estado `followTail` estiver ativo, restaurado quando a pessoa volta manualmente ao final ou toca em `Ir à última mensagem`.
- Novo foco em texto não desloca automaticamente os `ScrollViewer` externos do chat/editor. A correção do caret é reservada à digitação real e quando a pessoa não está navegando. O limite passa de 2/3 para 94% da área visível para impedir saltos de linha desnecessários.
- O status detalhado de notas com mais de 4 Ki caracteres atualiza após 240 ms de pausa; em notas pequenas permanece síncrono para feedback instantâneo. Textos, clipboard, histórico e salvamento não mudam de autoridade.
- Mais ações → `Selecionar nota inteira` troca explicitamente para Fonte e aciona `SelectAll` nativo (inclusive alças da seleção Android); não confunde seleção integral de um bloco com a nota inteira.
- Ações nas mensagens enviadas: `Copiar`, `Selecionar trecho` (TextBox somente leitura, seleção nativa) e `Editar e reenviar` (recupera no compositor, pede confirmação se há rascunho, não modifica histórico e deixa o envio com o usuário). Essa semântica é deliberadamente auditável e não provoca replay, deleção silenciosa de execuções ou cobrança involuntária.

## Avaliação

`EditorInteractionStabilityTests` verifica seleção integral em Visual → Fonte, prioridade de leitura manual, política de followTail e fixture de digitação longa com ticks/alocação no runner. Suítes existentes `EditorTests`, `EditorRedesignUiTests`, `AssistantUiTests`, `EditorNavigationUiTests` e `StartupPerfTests` devem permanecer verdes.

**Limitações:** ticks de runner não são latência em Android e a ausência de streaming com modelo real não prova fluidez em dispositivo. Validar no aparelho: teclado Gboard, toque único, duplo, seleção com alças, blocos de 10+ KiB, rolagem durante resposta e retorno ao final. Não alegar porcentagem de melhoria sem mesmo benchmark antes/depois; não converter milissegundos locais em economia monetária.

## Gates de integração e release

A nova release só pode sair da `main` integrada, com versão canônica inédita, workflow de distribuição oficial, certificado e assets verificados. `User-data`, `distribution` e demais zonas críticas exigem revisão/autorização do proprietário, não merge manual ou tag forjada. Publicação e validação Android físico são etapas distintas; registrar a segunda como pendente.
