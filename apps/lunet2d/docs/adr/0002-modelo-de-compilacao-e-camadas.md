# ADR 0002 — Jogo como assembly, referências em memória e camadas

## Contexto

O Run não pode passar pelo build Android completo. No Android os assemblies ficam dentro do APK, então caminhos de arquivo (`MetadataReference.CreateFromFile`) não existem.

## Decisão

- O jogo é compilado com Roslyn para um assembly em memória, carregado num `AssemblyLoadContext` coletável e executado por `GameHost` (Framework).
- As referências vêm dos assemblies já carregados no processo, lidos por `Assembly.TryGetRawMetadata` (`LoadedAssembliesReferenceProvider`). Só `System.*`, `netstandard` e `Lunet.Framework` são expostos: o jogo não enxerga APIs Android nem Roslyn.
- O Framework não depende de nada (nem da IDE, nem de Android); a saída gráfica é a interface `IGraphicsBackend` (OpenGL ES no Android, um coletor em memória nos testes).
- Regras de dependência (Framework, Core e Compiler sem referências a outros projetos Lunet; Runtime só ao Framework) são verificadas por `ArchitectureTests`.

## Consequências

- Todo o caminho editar → compilar → carregar → executar → tocar é testado no CI sem aparelho.
- `TryGetRawMetadata` no Mono do Android **não foi validado em aparelho**. Se falhar, a alternativa é embarcar os assemblies de referência como assets e usar `CreateFromStream`.
- Recarregar o jogo exige nome de assembly único por compilação no mesmo processo.
- Perder o contexto GL (voltar do segundo plano) reinicia o jogo do Preview.

## Alternativas

- Assets com assemblies de referência: previsível, mas aumenta o APK e duplica arquivos; fica como plano B.
- Build Android a cada Run: lento demais para iterar.
