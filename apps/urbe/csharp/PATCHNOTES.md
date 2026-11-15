# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.53
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## Editor: revisão completa da escrita (beta.53)

Esta versão é dedicada ao editor. Cada problema abaixo foi reproduzido por teste antes de ser corrigido.

### Teclado
- **O teclado não fecha e reabre mais ao trocar de bloco** (tocar noutro bloco, setas, Backspace/Delete, Desfazer). A caixa antiga só sai depois que a nova recebe o foco.
- **Tocar numa linha traz o teclado de volta** mesmo depois de fechá-lo com Voltar (o pedido ao Android agora é explícito, como num campo de texto nativo).
- Marcar uma tarefa enquanto escreve não fecha mais o bloco nem o teclado.

### Toque e cursor
- **Corrigido: depois de digitar, o toque num bloco mais abaixo abria o bloco errado** (ou o mesmo de novo), e marcar uma tarefa abaixo mudava a linha de outro bloco.
- O toque põe o cursor na letra tocada em negrito, links, títulos, listas, tarefas, citações, código, fórmulas, tags e comentários, e a linha não pula ao virar caixa.
- O cursor continua visível ao escrever no fim da tela e quando o teclado abre.
- Tocar num link enquanto escreve põe o cursor no link (não sai da nota no meio da frase); um botão **Abrir** aparece para segui-lo.
- Reabrir a mesma nota (listas, busca, escrita do Assistente) não fecha mais o bloco em edição.

### Texto nunca perdido
- **Corrigido: digitar num bloco com texto da IA podia desfazer a letra e fechar o bloco.**
- Algumas edições perto de texto da IA impediam o salvamento ("marca de autoria protegida"); agora a nota salva, com os selos corretos.

### Escrever como num texto contínuo
- **Desfazer/Refazer valem para a nota inteira** no modo Visual, por palavra, e reabrem o bloco onde a mudança foi (Ctrl+Z, Ctrl+Y).
- Setas passam de um bloco para o outro; **Backspace no início junta ao bloco de cima** e Delete no fim junta o de baixo.
- **Seleção entre blocos**: Shift+setas, Shift+clique e Ctrl+A duas vezes (nota inteira).
- O bloco em edição mostra a Markdown **formatada** (negrito, itálico, código, links, tags), com as marcas esmaecidas.
- Trocar de bloco em notas longas ficou cerca de 2× mais leve: só o bloco que mudou é redesenhado.

### Formatação
- Negrito/itálico/código não incluem mais o espaço da seleção dentro das marcas (que deixava `**palavra **` com asteriscos aparecendo).
- Seleção de várias linhas recebe as marcas em cada linha; itálico no meio de palavra funciona; `*itálico*` é reconhecido pelo botão.
- No modo Fonte, Título/Lista/Tarefa/Citação vão para o início da linha (antes podiam cair no meio da palavra).

### Fórmulas
- **Matrizes não ficam mais com o parêntese por cima da última coluna** (ex.: `\begin{pmatrix}10 & 2\\3 & 40\end{pmatrix}`).
- Fórmulas em destaque mais largas que a tela são reduzidas para caber inteiras; só as muito largas rolam de lado.

### Ainda não resolvido
- No modo **Fonte**, digitar em notas muito longas continua mais lento que no Visual: o custo está no campo de texto do framework (refaz o texto inteiro a cada tecla). O Visual não tem esse custo.
- Tudo acima foi testado automaticamente; o comportamento no celular (teclado, toque) precisa da sua confirmação no aparelho.
