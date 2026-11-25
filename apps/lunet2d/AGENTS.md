# Instruções compartilhadas para agentes

> **Casca única em Avalonia (ADD-0036 / DEC-0068 / [ADR 0013](docs/adr/0013-casca-unica-avalonia.md), 25/11/2026):** toda interface nova do Lunet é feita em `src/Lunet.Client` e hospedada pelas cabeças: `Lunet.Desktop` e o próprio `Lunet.Android`, que abre pela `ShellActivity` (um só app, `io.lunet.studio`). As telas ainda não migradas da `MainActivity.*` abrem por rota (`MainActivity.Route.cs`); não empilhe telas novas nelas, migre-as para a casca (LUNET-536). Siga as ondas de [`docs/plans/lunet-casca-avalonia-2026-11-25.md`](docs/plans/lunet-casca-avalonia-2026-11-25.md); gere capturas com `dotnet run --project src/Lunet.Desktop -- --screens <pasta>` (headless; não é teste no aparelho).

> **Protocolo transversal obrigatório:** [auditoria interna → pesquisa externa aprofundada → comunidades → confronto crítico → consolidação → implementação/TDD/validação, quando autorizada](../../docs/governance/protocolo-estudo-desenvolvimento.md), subordinado ao MANIFEST. Ao investigar a consolidação Lunet, estudar em três frentes **separadas, nesta ordem: framework, editor e estúdio**, e investigar UX/acessibilidade do editor e estúdio. Não confundir estudo com autorização para alterar código.

**Canal único (DEC-0064 / ADD-0035):** só `AbnerCruz/Ecosystem`, em `apps/lunet2d`, é fonte de código, PR, CI, decisões e releases. Nunca consultar/sincronizar/publicar no repositório separado `AbnerCruz/Lunet2D`; ele é apenas histórico. CI: `.github/workflows/lunet2d-ci.yml` da raiz; APK: `.github/workflows/lunet2d-release.yml`. **Antes de continuar o desenvolvimento, ler os dois estudos**: [auditoria incremental](../../docs/architecture/audits/lunet-estado-atual-2026-11-15.md) e [Home, Studios/biblioteca e exploração](../../docs/architecture/audits/lunet-home-studios-explorar-2026-11-15.md). A Home, a Library e Explorar (LUNET-510–512) são propostas de desenvolvimento, **não** funcionalidades já implantadas. **Antes de continuar o desenvolvimento, ler o diagnóstico incremental** [`docs/architecture/audits/lunet-estado-atual-2026-11-15.md`](../../docs/architecture/audits/lunet-estado-atual-2026-11-15.md), verificar a main/PRs atuais e seguir os gates de DEVICE.

Este diretório implementa o Lunet2D. Antes de qualquer trabalho, siga `docs/DEVELOPMENT.md`:

1. Leia `ROADMAP.md` (estado atual) e rode `bash tools/roadmap-status.sh --pending`.
2. Releia as seções relevantes de `docs/SPEC.md` (a especificação integral, na íntegra).
3. Trabalhe na ordem das fases; não avance além da fase atual por conta própria.
4. Todo trabalho corresponde a um item do ROADMAP (acrescente o item antes, se faltar).
5. Ao concluir uma fase, faça a auditoria completa e registre em `docs/audits/fase-N.md`.
6. PR com CI verde é mergeado; confirme a release e passe ao usuário um roteiro de teste no aparelho para cada entrega.
7. Ao fechar uma fase, siga a entrega de validação detalhada em `docs/DEVELOPMENT.md`: publique a release candidata, entregue o roteiro completo ao usuário e registre o retorno dele antes de declarar a fase concluída ou começar a próxima.

Ferramentas úteis: `dotnet test --project tests/Lunet.Tests` (só precisa do .NET 10 SDK). O APK só compila no CI (o SDK do Android não está disponível no ambiente de agente).

## Continuidade e colaboração

- Este é o arquivo canônico de instruções para qualquer agente (Codex, Claude ou outro). Use exatamente `AGENTS.md`, respeitando maiúsculas e minúsculas. Configure ferramentas que não o carreguem automaticamente para lê-lo ao iniciar.
- Antes de editar, leia `AgentsChat.md`, confira a branch, o commit atual, alterações locais, PRs abertos e seus resultados de CI. Trabalho não mergeado pode estar mais adiantado que a `main`.
- `docs/SPEC.md` define o produto; `ROADMAP.md` registra o progresso; `docs/DEVELOPMENT.md` define o ciclo de entrega. `AgentsChat.md` registra comunicação e passagem de trabalho, sem substituir essas fontes.
- Registre em `AgentsChat.md` o item assumido, arquivos envolvidos, resultados verificáveis, limitações e próximo passo. Faça isso também antes de interromper uma tarefa.
- Responda a outro agente referenciando o ID da entrada. Acrescente novas entradas; não reescreva falas alheias nem invente mensagens ou revisões em nome de outro agente.
- Coordene alterações concorrentes por branch e arquivos; preserve o trabalho anterior. Não sobrescreva mudanças ou force push para resolver divergências.
- Este registro é assíncrono: um arquivo Markdown não inicia agentes nem garante que estejam ativos. Não confunda este processo de desenvolvimento com o Agentic Workspace do aplicativo (Fase 8).
- Nunca registre credenciais, tokens ou dados privados. Registre evidências e conclusões técnicas, não raciocínio interno.
- CI verde não substitui validação em aparelho. Itens com essa validação pendente permanecem `[ ]`, conforme as regras do ROADMAP.
