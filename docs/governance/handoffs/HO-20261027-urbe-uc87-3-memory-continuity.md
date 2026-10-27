# Handoff — Urbe UC-87.3: aprendizado e recuperação de memórias

- Data: 2026-10-27
- PR: https://github.com/AbnerCruz/Ecosystem/pull/829
- Branch: `fix/urbe-uc87-memory-recall-20261027`
- Escopo: Urbe C# (AssistantMemoryEngine, AssistantMemoryTools, AssistantTools, AssistantPanel e testes).
- Estado inicial: **em validação**. Não presumir CI verde, integração ou validação no dispositivo.

## Evidências e diagnóstico

1. `AssistantPanel.StartTurnAsync` persistia somente `lembre que` ou episódios com arquivos alterados; preferências, metas e decisões declaradas explicitamente em frases comuns não criavam registro.
2. `AssistantPanel.LocalToolNames` omitia `learn_memory`; modelo local só podia recuperar.
3. `AssistantMemoryEngine.Search` dependia do vocabulário lexical, não entendia `pet` frente a `cachorro`.
4. `remember` e `learn_memory` tinham acesso de leitura (`memory`) embora persistissem alterações em config.json.

## Pesquisa

LongMemEval/LongMemEval-V2, Generative Agents, documentação Mem0, issue concorrência #6515, contexto versionado Letta. Links e limites científicos na documentação do patch: `apps/urbe/docs/csharp/ASSISTANT-MEMORY-CONTINUITY-20261027.md`.

## Implementação

- `CaptureUserStatements` com padrão restrito de primeira pessoa, origem `user`, fonte de conversa, escopo agent/vault, limite de comprimento e filtros anti-credenciais; opera **somente em execução terminada com sucesso**, sem chamadas extras de IA.
- Índice lexical existente estendido por grupos curtos e explícitos de equivalência, peso inferior a match literal; sem alegar semântica geral.
- `learn_memory` disponível para modelo local, sujeito a política e escopo; `learn_memory` classificada como `memory-write`; a ferramenta legada `remember` fica fora das ferramentas expostas pelo cliente nativo, preservando o contrato da suíte de oráculos preexistente. Escrever memória com política readonly fica bloqueado no cliente nativo.
- Formato de dados original `memoryRecords` mantido. `SetConfigVerified` conserva verificação de persistência.

## Testes e gates

Testes adicionados **antes** da implementação: `AssistantMemoryContinuityTests`, `AssistantDeviceMemoryToolsTests`. Casos: roundtrip persistido, dedup, isolamento agente/vault, negativos e segredos, sinônimo pet/cachorro, permissão readonly/ask, modelos locais.

Executar `urbe-checks`, `consistency`, CI Android/Windows e comparar compatibilidade com oráculos `AssistantAgentOracleTests`. CI do PR: https://github.com/AbnerCruz/Ecosystem/pull/829/checks

Android DEVICE pendente: aprendizado sem frase mágica; reinício; recall em nova conversa; permissões de ferramenta; leitura parcial; coexistência de anexos; nenhum vazamento entre agentes.

## Normas, impacto e coordenação

MANIFEST 0.1 (NN-001, 008, 009, 010, 011, 016, 017, 018, 019, 020, 022), AGENTS.md raiz e apps/urbe/AGENTS.md, apps/urbe/docs/csharp/ROADMAP.md (UC-87), ASSISTANT-MEMORY.md, DEC-0051/ADR-0034 e diretrizes locais. Reuse assessment: código exclusivo Urbe.Core, nenhuma plataforma compartilhada.

PRs paralelos #825 e #827 também modificam AssistantPanel; este PR não usa as branches deles, não faz merge manual, não muda partes de extração/anexos. O integrador precisa validar o estado combinado.

**Release:** #825 já propõe a versão beta.35 e toca o csproj de release. Não competir pelo mesmo arquivo nem rebaixar versionCode: após CI e integração governada, publicar próximo número livre pelo workflow oficial `urbe-csharp-mobile-release.yml`. Sem release fabricada e sem marcar DEVICE como aprovado.
