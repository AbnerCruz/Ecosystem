# Text Inspection Core

Library C# host-neutra que implementa o núcleo de `text.inspect@1.0.0`.

Responsabilidade exclusiva:
- validar o limite do texto em memória;
- contar Unicode scalar values, palavras separadas por whitespace e linhas LF;
- respeitar cancelamento cooperativo.

Não conhece JSON, Host API, Product, Binder, filesystem, rede, UI, Context, grants ou lifecycle. Hub e Lunet adaptam seus próprios Hosts ao contrato público em `docs/contracts/capabilities/text.inspect.json`.

Extração autorizada por ADR-0028 / DEC-0038-A após Extraction Review de NN-022.
