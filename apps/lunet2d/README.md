# Lunet2D

Lunet é um ambiente Android para criar jogos 2D em C#. Este repositório está no começo da implementação. O APK atual, quando compilado, é uma fundação: permite criar um projeto local, editar um arquivo C# e exportar a pasta do projeto em ZIP. Ainda **não** compila nem executa o jogo no telefone.

## Construir

Requer .NET 10 SDK, workload `android`, Android SDK (API 36) e JDK compatível.

```sh
dotnet workload install android
dotnet build src/Lunet.Android/Lunet.Android.csproj -c Debug -f net10.0-android -p:AndroidPackageFormat=apk
```

O fluxo de CI constrói o APK e o disponibiliza como artifact da execução. Não há release até um teste de instalação e abertura em Android.

## Direção

O código dos jogos será C# com uma API própria, e os projetos serão pastas comuns. O framework será independente da IDE. Ferramentas visuais, plugins, IA e serviços online virão depois de validarmos compilação Roslyn e execução de código no Android. Os critérios e bloqueios atuais estão em [ROADMAP.md](ROADMAP.md).

## Licença

Ainda não definida. Nenhuma licença de terceiros é presumida para este código.
