# Text Inspection Core

Library C# host-neutra que implementa a semântica pura de `text.inspect@1.0.0`.

Responsabilidade exclusiva:
- validar o limite do texto em memória;
- contar Unicode scalar values, palavras separadas por whitespace e linhas LF;
- respeitar cancelamento cooperativo.

A única fonte editável do algoritmo é:

`platform/text-inspection/src/Ecosystem.TextInspection/TextInspector.cs`

Hub e Lunet permanecem Products autocontidos. Eles compilam projeções determinísticas em `Generated/Ecosystem.TextInspection/TextInspector.cs`; essas projeções são artefatos derivados, marcados `GENERATED / DO NOT EDIT`, e nunca autoridade.

Comandos:

```bash
dotnet run platform/text-inspection/tools/Projection.cs -- --write
dotnet run platform/text-inspection/tools/Projection.cs -- --check
dotnet run platform/text-inspection/tools/Projection.cs -- --self-test
dotnet test --project platform/text-inspection/tests/Ecosystem.TextInspection.Tests/Ecosystem.TextInspection.Tests.csproj
```

`--check` falha se qualquer projeção estiver ausente ou divergir. `--self-test` prova drift, projeção ausente e regeneração determinística.

A Library não conhece JSON, Host API, Product, Binder, filesystem, rede, UI, Context, grants, lifecycle ou `ecosystem-local/0`.

Extração autorizada por ADR-0028 / DEC-0038-A após Extraction Review de NN-022.
