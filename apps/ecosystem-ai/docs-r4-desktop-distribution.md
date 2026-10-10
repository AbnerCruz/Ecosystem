# P6-5 — Distribuição desktop independente (.NET 10 autossuficiente)

O Product **Ecosystem AI** dispõe de builds self-contained em três
runtimes, sem exigir instalação do SDK .NET nem de outro aplicativo no computador
destino. Este slice não inventa outro Runner, ProductStore ou Provider:
empacota exatamente `src/EcosystemAi.Cli` com o Runtime já existente.

| Destino | RID .NET | Pacote |
|---|---|---|
| Windows (64 bits) | `win-x64` | `.zip`, executável `EcosystemAi.Cli.exe` |
| Linux (64 bits) | `linux-x64` | `.tar.gz`, executável `EcosystemAi.Cli` |
| macOS Apple Silicon | `osx-arm64` | `.tar.gz`, executável `EcosystemAi.Cli` |

Não são pacotes Android e **não** podem ser executados no Android.
Distribuição Android exige Host específico e validação de dispositivo,
ainda pendentes. Builds de macOS feitas por cross-publish não substituem
teste manual com Gatekeeper ou assinatura/notarização. Não há assinatura
de executáveis: versões development podem ser bloqueadas pelo SO.

## Fluxo de CI / segurança

`.github/workflows/ecosystem-ai-release.yml` executa ambos os projetos
de testes C# antes de empacotar três runtimes. `dotnet publish` usa
`--self-contained true`, `PublishSingleFile=true` e
`PublishTrimmed=false` para evitar regressões de reflexão/serialização.
O runner Linux também invoca `--help` do executável publicado.

Em PR a workflow apenas compila e oferece pacotes como **artefatos de CI
com retenção limitada**, sem criar release. Na `main`, após integração
autorizada, ela publica uma prerelease versionada de desenvolvimento
(`ecosystem-ai-v<VERSION>-dev.<run>`) com os três pacotes e arquivos
`SHA256SUMS-<RID>.txt`. Arquivos da release não contêm
`ECOAI_API_KEY`, preços, chaves, históricos ou dados do usuário.
O token `GITHUB_TOKEN` é usado apenas no job de prerelease, nunca
durante build/test ou PR.

**Alteração crítica de distribuição:** o workflow dá a capacidade de
publicar binários por push na `main`. Antes de integrar o PR, o
proprietário precisa autorizar a mudança e os checks devem estar verdes.

## Iniciar após extrair

No diretório descompactado:

```sh
# Linux/macOS
./EcosystemAi.Cli --help
./EcosystemAi.Cli --web-ui --catalog ./dados-privados

# Windows PowerShell
./EcosystemAi.Cli.exe --help
./EcosystemAi.Cli.exe --web-ui --catalog ./dados-privados
```

A UI local inicia em `http://127.0.0.1:8765`, **somente no mesmo
dispositivo**. Sem `--web-tasks` não executa nenhum modelo. O modo
de execução real exige configuração explícita de endpoint, modelo,
orçamento, preço, chaves em variável de ambiente `ECOAI_API_KEY` e
limites de autorização. Não coloque chaves em linhas de comando,
`README`, scripts, commits ou tickets.

Os dados de projetos ficam em diretório escolhido pelo operador,
fora do executável distribuído. Não há migração de catálogo nesta
entrega nem serviço/background automático.

## Limites e gate

- A build Linux executa um smoke `--help` sem rede e sem token.
- As outras duas RIDs são *cross-published* e requerem validação em
  hardware real para declarar distribuição plenamente aprovada.
- Não são APK/instaladores assinados. A CI não garante compatibilidade
  com todos os sistemas operacionais nem com providers reais.
- Esta entrega aproxima o Product distribuível, mas **não fecha P6-5**.
