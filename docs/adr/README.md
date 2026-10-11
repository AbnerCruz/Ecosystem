# Architecture Decision Records

Decisões estruturais do Ecosystem (MANIFEST §29, NN-011). Processo definido em [ADR-0001](0001-registro-de-decisoes-arquiteturais.md).

## Quando um ADR é obrigatório

Protocolo IPC, formato de manifest, modelo de capability, linguagem/runtime excepcional, boundaries, packaging, transporte, modelo de plugins, versionamento, compatibilidade, migração de dados, mudança de arquitetura do Urbe ou do Lunet, escolha estrutural do Hub, reescrita estrutural (NN-005), extração de componente compartilhado relevante (NN-022) e qualquer exceção às invariantes que o manifesto permita via ADR (ex.: NN-015).

## Como criar

1. Copie [`TEMPLATE.md`](TEMPLATE.md) para `NNNN-titulo-em-kebab.md` com o próximo número livre (números nunca são reutilizados).
2. Preencha todas as seções obrigatórias: Status, Contexto, Problema, Opções, Decisão, Consequências, Alternativas rejeitadas.
3. Status inicial: `Proposto`. Se a decisão cabe ao proprietário (NN-011), registre também uma entrada `pending` em [`docs/governance/decisions.json`](../governance/decisions.json).
4. Adicione o ADR ao índice abaixo.
5. `dotnet run tests/consistency/Check.cs` (`CHK-ADR`) deve passar.

Um ADR `Aceito` só muda por outro ADR que o substitua (`Substituído por ADR-XXXX`). Agentes nunca marcam `Aceito` uma decisão que exige o proprietário sem registro da decisão dele (MANIFEST §23.2).

**ADRs de fundação (0001–0006):** criados pelo agente fundador sob MANIFEST §57 e **ratificados pelo proprietário em DEC-0001** (ADD-0003, 2026-09-30). Mudar qualquer um exige novo ADR que o substitua.

## Índice

| ADR | Título | Status |
|-----|--------|--------|
| [0001](0001-registro-de-decisoes-arquiteturais.md) | Registro de decisões arquiteturais | Aceito |
| [0002](0002-formato-dos-registros-canonicos.md) | Formato de `ecosystem.json` e dos registros de governança | Aceito |
| [0003](0003-checks-de-consistencia-em-csharp.md) | Checks de consistência em C# sem dependências | Aceito |
| [0004](0004-estrutura-inicial-do-repositorio.md) | Estrutura inicial do repositório | Aceito |
| [0005](0005-portal-web-github-pages.md) | Portal web via GitHub Pages como projeção | Aceito |
| [0006](0006-product-shell-context-distribuicao-independente.md) | Product Shell, Context e distribuição independente | Aceito |
| [0007](0007-resposta-a-decisoes-pelo-portal.md) | Resposta a decisões pelo portal | Aceito |
| [0008](0008-aprovacao-de-validacoes-pelo-portal.md) | Aprovação de validações humanas pelo portal | Aceito |
| [0009](0009-refatoracao-dos-produtos-pos-migracao.md) | Refatoração dos produtos depois da migração | Aceito |
| [0010](0010-semantica-de-ecosystem-phase-e-estado-derivado.md) | Semântica de ecosystem.phase e deriva de estado entre fontes | Aceito |
| [0011](0011-local-first-e-promocao-por-evidencia.md) | Local-first e promoção por evidência | Aceito |
| [0012](0012-contratos-da-fase-2.md) | Contratos da Fase 2 (manifest, capability, versões, Context, permissões, Registry, Distribution Profile) | Aceito (DEC-0020-B; nomes dos eixos congelados em P2-9 após DEC-0021-C) |
| [0013](0013-hub-read-only-fase-3.md) | Hub read-only (Fase 3): tecnologia de UI, plataforma-alvo e fontes de dados | Aceito (DEC-0022-A: Android nativo em C#) |
| [0014](0014-fluxo-multiagente-minimo.md) | Fluxo multiagente mínimo: base, resultado, integração e validação | Aceito (ADD-0010) |
| [0015](0015-integrador-automatico.md) | Integrador automático: estado combinado, fila serial, criticidade e autorização | Aceito (ADD-0011, ADD-0012) |
| [0016](0016-migracao-do-urbe-para-csharp.md) | Migração do Urbe para C# | Aceito (DEC-0024-B: reescrita completa com paridade; pilha e corte ainda por decidir) |
| [0017](0017-agent-runtime-execution-runtime-e-organizacoes.md) | Agent Runtime, Execution Runtime e organizações de agentes: fronteiras e sequência | Aceito (DEC-0026-C; escolha A revogada, ADD-0014) |
| [0018](0018-instalacao-e-launcher-do-hub.md) | Instalação e launcher do Hub | Aceito (DEC-0030-A) |

| [0019](0019-releases-diretas-dos-products.md) | Releases diretas dos Products no Ecosystem | Aceito (DEC-0031; ADD-0015) |
| [0020](0020-r2-workspace-changes-e-integracao-local.md) | R2: workspace, revisão e integração local | Proposto |
| [0021](0021-r3-sessao-local-do-agent-workspace.md) | R3: sessão local do Agent Workspace | Proposto |
| [0022](0022-tabletop-rpg-product-solo-first.md) | Tabletop RPG como Product C# solo-first | Aceito (DEC-0033; ADD-0016) |
| [0023](0023-capability-runtime-local-no-hub.md) | Capability Runtime experimental local no Hub | Proposto |
| [0024](0024-host-api-publica-product-shell.md) | Host API pública e neutra de transporte para Product Shells | Aceito (DEC-0034-A) |
| [0025](0025-urbe-pilha-ui-hosts-csharp.md) | Pilha de UI e hosts do Urbe em C# | Substituído (UI/hosts por ADR-0032, DEC-0043) |
| [0026](0026-urbe-transicao-cliente-csharp.md) | Transição do Urbe JavaScript para o cliente C# | Aceito (DEC-0036-C) |
| [0027](0027-primeiro-ipc-local-android.md) | Primeiro IPC local autenticado entre Hub e Lunet | Aceito (DEC-0037-A) |
| [0028](0028-text-inspect-core-compartilhado.md) | Núcleo compartilhado de `text.inspect` para o segundo Host | Aceito (DEC-0038-A) |
| [0029](0029-product-de-autoria-matematica-temporal.md) | Product independente de autoria matemática temporal | Aceito (DEC-0040-A) |
| [0030](0030-documento-canonico-de-autoria-matematica.md) | Documento canônico e autoria textual da matemática temporal | Aceito (DEC-0040-A) |
| [0031](0031-p6-4-piloto-headless-lunet.md) | Piloto headless do Host Lunet para P6-4 | Aceito (DEC-0041, piloto restrito) |
| [0032](0032-urbe-ui-nativa-sem-webview.md) | Urbe C#: interface nativa sem WebView, com a 1.8.4-beta como especificação | Aceito (DEC-0043) |
