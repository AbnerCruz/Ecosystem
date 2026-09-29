# Lunet2D

Lunet é um ambiente Android para criar jogos 2D em C#. Este repositório está no começo da implementação. O app permite criar um projeto, editar C#, apertar Run (Roslyn + Preview OpenGL ES) e mover um sprite com toque. A cadeia compilar → carregar → executar é testada no CI; **a execução no Android físico ainda não foi validada**.

## Construir

Requer .NET 10 SDK, workload `android`, Android SDK (API 36) e JDK compatível.

```sh
dotnet workload install android
dotnet build src/Lunet.Android/Lunet.Android.csproj -c Debug -f net10.0-android -p:AndroidPackageFormat=apk
```

Testes: `dotnet test --project tests/Lunet.Tests` (só precisa do .NET 10 SDK). O CI testa, constrói o APK e, na `main`, publica uma release de desenvolvimento em GitHub Releases.

## Direção

O código dos jogos será C# com uma API própria, e os projetos serão pastas comuns. O framework será independente da IDE. Ferramentas visuais, plugins, IA e serviços online virão depois de validarmos compilação Roslyn e execução de código no Android. Os critérios e bloqueios atuais estão em [ROADMAP.md](ROADMAP.md).

## Licença

Ainda não definida. Nenhuma licença de terceiros é presumida para este código.
