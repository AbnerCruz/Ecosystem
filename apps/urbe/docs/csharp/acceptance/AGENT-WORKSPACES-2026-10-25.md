# UC-81 — verificação dos espaços por agente (25/10/2026)

Base: `831a52a8`, checkout limpo em main antes do trabalho. Issue #756.
Branch: `feat/urbe-agent-workspaces`. ADR-0041 registra produto, dados e falhas.

## Implementação e compatibilidade

- Lista de agentes com nomes; trocar abre o chat correspondente. Histórico filtrado por AgentId e Vault, continuidade sem prazo artificial de seis horas.
- Modelos de texto, imagem, instruções e permissões individuais. Permissões somente nas configurações; ausentes ou inválidas viram readonly. Exclusões/plugins continuam sujeitos às aprovações existentes.
- Somente Assistente em instalação nova. Demais perfis são templates de criação com ID novo. Preservam-se identidades já referenciadas por históricos e personalizações.
- Assistente legado sem escolha explícita de modelo também recebe o padrão local; escolhas anteriores, inclusive modelos de conversas antigas e IDs de provedores coincidentes, são preservadas.
- Provedor `device` / IA no aparelho, usando `ILocalTextRuntime.Recommended.Id`; catálogo real Gemma 4 E2B/E4B. Não baixa nem executa pesos automaticamente. Nenhum modelo de imagem em configuração nova.
- Migração aditiva em config.json, backup dos bytes originais verificado e gravação lida de volta. Ilegível/versão futura/falha de backup impedem alterações. Repetição idempotente. Conversas não são regravadas pela migração.
- Históricos legados mantêm o AgentId final gravado. O chat antigo podia mudar de agente: não há evidência por mensagem para repartir conteúdo. A interface explica essa limitação.
- Modelos, chaves, memória e instruções anteriores permanecem. Política global/de conversa antiga é preservada como metadado, mas não vira concessão individual de escrita. Uma escolha visual global explicitamente salva é preservada para agentes existentes; não se infere um novo provedor de imagens.
- Mudanças colaterais necessárias: componente FlatDisclosure aceita ocultar a seta só no Assistente; testes de escrita configuram concessão explícita; UI contextual continua abrindo a conversa diretamente. Sem alteração de vault, dependências, assinatura, versão ou release.

## Comandos executados e resultados

SDK local: .NET 10.0.402. Comandos no diretório `apps/urbe/csharp` (SDK no PATH):

```sh
dotnet build tests/Urbe.Core.Tests/Urbe.Core.Tests.csproj -m:1 -v minimal
dotnet tests/Urbe.Core.Tests/bin/Debug/net10.0/Urbe.Core.Tests.dll -parallelMode none -class 'Urbe.Core.Tests.Assistant*' -class 'Urbe.Core.Tests.Agent*' -class 'Urbe.Core.Tests.LocalProviderTests' -reporter verbose

dotnet build tests/Urbe.Client.Tests/Urbe.Client.Tests.csproj -m:1 -v minimal
URBE_SNAPSHOT_DIR=/tmp/urbe-agent-snapshots dotnet tests/Urbe.Client.Tests/bin/Debug/net10.0/Urbe.Client.Tests.dll -parallelMode none -method '*AgentWorkspaces*' -method '*Assistant*' -method '*NoteAgent*' -method '*CodeBlocks*' -method '*ModelPrice*' -reporter verbose
```

Builds: zero warnings, zero erros. Core: **220 passaram**, zero falhas, 5.283s.
UI: **19 passaram**, zero falhas, 26.915s. Nove casos novos Core e um novo UI;
incluem falha de backup/gravação com retry, versão futura, migração, defaults,
round-trip de configuração, histórico por agente/vault, seleção e reinicialização.
Regressões cobrem aprovações, desfazer, plugins, comandos em notas, modelo local
simulado, clipboard, expansão lazy e ausência de setas nas etapas.

Na raiz:

```sh
dotnet run tests/consistency/Check.cs
git diff --check
```

Resultado: **22 checks passaram**, e `git diff --check` não apontou problemas. A revisão do diff verificou que nenhuma
conversa é reatribuída, nenhum grant nasce de política global, nenhum provedor
substitui uma seleção individual ausente, e o backup não é sobrescrito.

Capturas headless inspecionadas: `native-agent-workspaces.png` (412×900),
`native-assistant-experience-activity.png`, e capturas existentes de chat/desktop.
O teste usa o caminho de snapshots já consumido pelo CI; imagens locais são
intermediárias, não comprovação de Android físico.

## Limites

Tentativas locais das suítes completas Core/UI foram interrompidas sem resultado
conclusivo; a evidência acima é das suítes direcionadas. CI completo e integração
ficam separados. Não houve build de APK/Windows, instalação, atualização real,
inferência com pesos locais ou teste com provedor real. DEVICE ainda necessário:
IME/toque, retomada do app, migração de instalação existente e consumo de memória
do modelo local no dispositivo. Não se declara release nem gate humano aprovado.

## Arquivos alterados

- `apps/urbe/AGENTSCHAT.md`
- `apps/urbe/csharp/src/Urbe.Client/AssistantPanel.Device.cs`
- `apps/urbe/csharp/src/Urbe.Client/AssistantPanel.Views.cs`
- `apps/urbe/csharp/src/Urbe.Client/AssistantPanel.cs`
- `apps/urbe/csharp/src/Urbe.Client/Ui/FlatDisclosure.cs`
- `apps/urbe/csharp/src/Urbe.Core/Assistant/AgentNames.cs`
- `apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantAgent.cs`
- `apps/urbe/csharp/src/Urbe.Core/Assistant/AssistantStore.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/AgentWorkspaceUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/AssistantCompactUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/AssistantContextualUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/AssistantExperienceUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/AssistantPluginUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/AssistantUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Client.Tests/DeviceAiUiTests.cs`
- `apps/urbe/csharp/tests/Urbe.Core.Tests/AgentNamesTests.cs`
- `apps/urbe/csharp/tests/Urbe.Core.Tests/AgentWorkspaceTests.cs`
- `apps/urbe/csharp/tests/Urbe.Core.Tests/AssistantStoreTests.cs`
- `apps/urbe/docs/csharp/ASSISTANT-EXPERIENCE.md`
- `apps/urbe/docs/csharp/ROADMAP.md`
- `apps/urbe/docs/csharp/acceptance/AGENT-WORKSPACES-2026-10-25.md`
- `apps/urbe/docs/v2/discovery/DATA-CATALOG.md`
- `docs/adr/0041-urbe-espacos-por-agente.md`
- `docs/adr/README.md`
- `docs/governance/handoffs/HO-20261025-urbe-agent-workspaces.json`
