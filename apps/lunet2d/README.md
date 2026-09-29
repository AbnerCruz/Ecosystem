# Lunet2D

Lunet é um ambiente Android para criar jogos 2D em C#. O projeto está na Fase 3 (IDE). O app permite criar um projeto, editar C#, apertar Run (Roslyn + Preview OpenGL ES) e mover um sprite com toque. A cadeia compilar → carregar → executar é testada no CI; as Fases 0–2 foram validadas em aparelho conforme as auditorias. Os recursos recentes da Fase 3 ainda exigem validação em aparelho.

## Planejamento

A especificação completa está em [`docs/SPEC.md`](docs/SPEC.md), o estado e o que falta em [`ROADMAP.md`](ROADMAP.md) (`bash tools/roadmap-status.sh --pending` resume por fase) e a rotina de trabalho em [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md).

Agentes de desenvolvimento: leia [`AGENTS.md`](AGENTS.md) e o registro de continuidade [`AgentsChat.md`](AgentsChat.md).

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

Código-fonte **proprietário**: veja [LICENSE.md](LICENSE.md). O código pode ser lido (inclusive dentro do app) para estudo, mas não pode ser redistribuído, modificado ou explorado comercialmente sem autorização expressa. Os jogos criados com o Lunet pertencem a quem os cria. A licença é provisória e pode ser revisada.
