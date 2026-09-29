# ADR 0003 — Editor de código sobre EditText, com serviços independentes da view

## Contexto

O spec pede um editor com documentos grandes, undo/redo robusto, IME Android, realce, diagnósticos e autocompletar. Uma view de texto própria (desenho em Canvas, `InputConnection`, seleção, IME) é o maior risco de qualidade em teclado móvel: composição de texto, correção automática, acentuação e teclados de terceiros dependem do `EditText`.

## Decisão

- A entrada de texto continua no `EditText` do Android (IME, seleção e clipboard nativos). `CodeEditText` acrescenta realce por spans, números de linha desenhados em `OnDraw`, indentação automática e histórico próprio.
- Toda a lógica que não depende de view fica em `Lunet.Editor` (net10.0, testada em desktop): `SyntaxHighlighter`, `CodeAnalyzer` (autocompletar, dica, definição, referências e diagnósticos ao vivo via Roslyn), `IndentationService`, `UndoHistory` e `FindReplace`.
- A análise roda fora da thread de UI, após 250 ms sem digitação, com número de versão para descartar resultados velhos.
- Autocompletar aparece como chips acima do teclado, não como popup, para não competir com a barra de sugestões do teclado nem cobrir o texto.

## Consequências

- IME e acentuação funcionam como no resto do Android desde o primeiro dia.
- O realce reaplica todos os spans a cada pausa: aceitável para arquivos de milhares de linhas, mas não escala para dezenas de milhares. Se a medição em aparelho mostrar gargalo, o próximo passo é uma view virtualizada com `Lunet.Editor` inalterado.
- Não há multi-cursor, folding nem minimapa; ficam para uma view própria.
- A estrutura de dados do texto é a do `Editable` (não é piece table/rope). O `UndoHistory` guarda só diffs.

## Alternativas

- View própria com piece table e `InputConnection` customizada: melhor para arquivos enormes e multi-cursor, muito mais risco com IME. Adiada até haver dados de desempenho.
- `WebView` com Monaco/CodeMirror: bom editor, mas contradiz "não usar WebView como base" do ADR 0001 e traz dependência pesada offline.
