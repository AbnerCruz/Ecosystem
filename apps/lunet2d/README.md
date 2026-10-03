# Lunet2D

O código canônico está em [`apps/lunet2d` do Ecosystem](https://github.com/AbnerCruz/Ecosystem/tree/main/apps/lunet2d).
APKs são publicados diretamente nas [Releases do Ecosystem](https://github.com/AbnerCruz/Ecosystem/releases), com tags `lunet2d-v…`.

Lunet é um ambiente completo e **code-first** para criar jogos 2D em C# no Android.
Um único produto, um único projeto em pasta comum e três camadas:

- **Lunet Framework**: APIs para construir o jogo inteiramente por código.
- **Lunet IDE**: ferramentas profissionais para escrever, compreender, executar, testar e depurar esse código.
- **Lunet Studio**: ferramentas visuais opcionais sobre os mesmos arquivos, sem esconder lógica ou tornar a UI obrigatória.

O código é a autoridade: **CODE IS THE SOURCE OF TRUTH / NO HIDDEN GAME LOGIC**.
Framework e código bastam; Studio acelera a produção. A disponibilidade e os
gates de cada recurso estão no [ROADMAP](ROADMAP.md), não nesta apresentação.

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

A política Visual ↔ Source e os limites estão no [ADR 0007](docs/adr/0007-framework-ide-studio-code-first.md) e no [SPEC §13](docs/SPEC.md).

O código dos jogos será C# com uma API própria, e os projetos serão pastas comuns. O framework será independente da IDE. Ferramentas visuais, plugins, IA e serviços online virão depois de validarmos compilação Roslyn e execução de código no Android. Os critérios e bloqueios atuais estão em [ROADMAP.md](ROADMAP.md).

## Licença

Código-fonte **proprietário**: veja [LICENSE.md](LICENSE.md). O código pode ser lido (inclusive dentro do app) para estudo, mas não pode ser redistribuído, modificado ou explorado comercialmente sem autorização expressa. Os jogos criados com o Lunet pertencem a quem os cria. A licença é provisória e pode ser revisada.
