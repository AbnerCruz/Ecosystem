# Lunet2D — viabilidade de expansão para desktop, navegador e iOS

**Data:** 2026-11-14 · **Tipo:** estudo (Fase A, LUNET-492) · **Estado:** proposta; a escolha da tecnologia de interface depende da decisão DEC-0062 (ver [ADR 0012](../adr/0012-expansao-multiplataforma.md)).
**Origem:** avaliação independente trazida pelo proprietário em 14/11/2026, **conferida contra o código desta branch** (base `938aacd`). Onde a avaliação citava o commit `f1872a7` da `main`, vale o que está abaixo.
**Não autoriza:** alterar o aplicativo Android, trocar a interface existente, reescrever o Framework ou prometer execução de C# local no iOS.

Convenção (NN-009, MANIFEST §53): **FATO** = verificado no repositório; **EXTERNO** = alegação de documentação de terceiros, ainda não reverificada nesta sessão; **INFERÊNCIA** = conclusão nossa; **PROPOSTA** = o que sugerimos.

## 1. O que a avaliação afirma e o que o código confirma

| Afirmação da avaliação | Resultado | Evidência |
|---|---|---|
| Framework, Runtime, Compiler, Core usam `net10.0`; só o app Android usa `net10.0-android` | **FATO** | `src/*/*.csproj`; teste `PlatformPortabilityTests.OnlyTheAndroidHostTargetsAPlatformFramework` |
| Existe `IGraphicsBackend` e `IAudioBackend` separando o Framework da plataforma | **FATO** | `src/Lunet.Framework/Graphics/IGraphicsBackend.cs`, `Audio/IAudioBackend.cs` |
| O backend gráfico concreto é OpenGL ES 3.0 no Android | **FATO** | `src/Lunet.Android/Gles/GlesBackend.cs` (o Framework não contém tipo `Gles*`) |
| A interface está presa ao Android | **FATO** | `MainActivity.*`, `LunetUi`, `SpriteStudioView` etc. usam `Android.Views/Widget/Graphics` |
| Roslyn compila e `AssemblyLoadContext` carrega o jogo | **FATO** | `Lunet.Compiler` (Roslyn), `Lunet.Runtime/GameLoader.cs` |
| As bibliotecas portáteis não dependem de APIs Android | **FATO** (novo, agora fiscalizado) | `PlatformPortabilityTests` — 22 casos; nenhum `using Android.*`, `Java.*`, `UIKit`… nas 10 bibliotecas |
| Contratos separados também para entrada, armazenamento, segredos, build, área de transferência e ciclo de vida | **FATO: não existem** como interfaces | Hoje entrada e ciclo de vida são tratados no host Android (`PreviewHost`, `CanvasGestures`), segredos em `AndroidKeystoreVault`, build em `OfflineGameApkBuilder`. É trabalho novo, não ponto de partida. |
| iOS não permite gerar/executar código dinamicamente no aparelho (exige AOT) | **EXTERNO** | Documentação .NET/Apple citada pela avaliação; **confirmar no spike** antes de qualquer promessa. |
| Avalonia roda em Windows/Linux/macOS/Android/iOS/WebAssembly | **EXTERNO** | Documentação oficial do Avalonia, citada pela avaliação. O Ecosystem já usa Avalonia em outro Product C# (experiência interna, sem criar dependência entre Products, NN-002). |

Dois pontos que a avaliação não destacou e que importam:

1. **`Lunet.Studio` referencia `Lunet.Framework`** (achado C3 do diagnóstico de 14/11). Para o Studio rodar fora do Android, seu modelo de documento (sprites, tilemaps, áudio) deve ficar independente da UI. Os modelos novos desta entrega (`SpriteSelection`, `SpriteFloating`, `VectorIcon`) já nascem sem Android e com testes.
2. **O Preview isolado e o empacotamento de APK do jogo são Android por natureza.** Em desktop, "exportar" passa a ser publicar executável (ou WebAssembly); isso é um backend de build novo (`IBuildBackend`), não uma adaptação.

## 2. Gargalos reais, em ordem de risco

1. **Compilar e carregar jogos por plataforma** (alto no iOS, médio no navegador). Desktop reutiliza Roslyn + `AssemblyLoadContext` como hoje. Navegador exige Roslyn em WebAssembly ou compilação remota. iOS: **INFERÊNCIA** — preview instantâneo local provavelmente impossível; o caminho é build remoto em macOS com AOT, aceitando perder o Preview instantâneo ou substituí-lo por um interpretador/hot reload limitado. Só o spike dirá.
2. **Interface** (alto): ~80 AlertDialogs e Views imperativas em `Lunet.Android`. Uma casca compartilhada só compensa se reaproveitar a arquitetura de informação já consolidada (workbench, Studios) sem reimplementar tudo duas vezes.
3. **Backend gráfico** (médio): `IGraphicsBackend` é boa fronteira. Desktop pode usar OpenGL; navegador WebGL 2; iOS Metal. Cada um é um backend novo com testes de conformidade comuns (o `RecordingBackend` já existe nos testes e serve de oráculo).
4. **Armazenamento, segredos, entrada, ciclo de vida** (médio): hoje implícitos no host Android; precisam virar contratos pequenos e testados.

## 3. Proposta de fases (sem alterar o app existente até o gate de cada uma)

| Fase | Item | Critério de saída |
|---|---|---|
| A | **LUNET-492 Fundação**: inventário (este documento), fiscalização de portabilidade (`PlatformPortabilityTests`), contratos de plataforma propostos (`IInputBackend`, `IStorageBackend`, `ISecretsBackend`, `IBuildBackend`, `IClipboardBackend`, `IPlatformLifecycle`) **somente como ADR/contrato**, sem código de produto | Bibliotecas portáteis comprovadamente sem Android (**feito**); contratos revisados |
| A′ | **LUNET-482 Spike da casca** (Avalonia, já previsto no diagnóstico): editor de sprite + canvas + preview GL embutido, medindo partida, rolagem e latência de toque contra o Android nativo | Relatório com números; decisão DEC-0062 |
| B | **LUNET-493 Desktop** (Windows/Linux/macOS): host Avalonia, Roslyn local, Preview, exportação | Criar, editar, compilar, executar e exportar um jogo completo |
| C | **LUNET-494 Navegador**: WebGL 2, armazenamento web, compilação compatível | Desenvolver e executar um projeto pelo navegador |
| D | **LUNET-495 iOS**: Metal, armazenamento Apple, build remoto, AOT | Um jogo real em iPhone/iPad (exige macOS/Xcode e DEVICE) |
| E | **LUNET-496 Unificação**: componentes visuais compartilhados, formatos de projeto idênticos | Mesmos contratos e formatos em todos os hosts |

Prioridade sugerida: Desktop > Navegador > iOS; Android continua plataforma de referência e recebe melhorias normalmente (inclusive esta entrega).

## 4. O que **não** fazer agora

- Não reescrever o Framework, o Compiler nem o Runtime (NN-005).
- Não substituir a interface Android antes do spike e da decisão (NN-011; SPEC §2 ainda diz "Android only" — conflito C1 do diagnóstico).
- Não prometer C# local no iOS (EXTERNO não verificado).
- Não criar projeto `Lunet.Platform`/`shared` sem consumidor real (NN-004, NN-020, NN-022): contratos só são extraídos quando o segundo host (desktop) os exigir de fato.
- Não pôr a plataforma de controle do Ecosystem no caminho: nenhum host novo depende dela (NN-003, NN-023).

## 5. Verificação desta fase

`dotnet test --project tests/Lunet.Tests` — inclui `PlatformPortabilityTests` (portabilidade das 10 bibliotecas: TFM simples, sem `using` de plataforma, costuras gráfica/áudio presentes, só `Lunet.Android` com TFM de plataforma). Nenhum hosts novo foi criado; nada de desktop, navegador ou iOS foi executado.
