# Urbe nativo — 2.0.0-beta.25

24/10/2026 · Android Avalonia/.NET

- Notas em Fonte, busca de notas e comandos, Localizar e diálogos abrem sem focar campos no Android.
- Alternar Visual/Fonte não abre o IME nem restaura o foco editável anterior; formatação não reabre teclado sem toque.
- No modo Visual, bloco tocado e espaço vazio tocado ativam o teclado; criação interna de blocos não.
- O botão Voltar no editor encerra o foco, evitando teclado preso ao mudar de tela.
- Configurações → Diagnóstico de teclado e toques: registrar foco, IME/InputPane, viewport nativo, rolagem e toques; copiar relatório; desativado por padrão.
- Preservados os offsets de rolagem internos, a renderização pontual do Visual e o adjustResize nativo sem segunda compensação.
- Testes unitários da política de foco incluídos; a validação com IME do Android depende de teste no aparelho.
