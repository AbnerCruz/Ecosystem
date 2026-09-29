# Instruções compartilhadas para agentes

Este repositório implementa o Lunet2D. Antes de qualquer trabalho, siga `docs/DEVELOPMENT.md`:

1. Leia `ROADMAP.md` (estado atual) e rode `bash tools/roadmap-status.sh --pending`.
2. Releia as seções relevantes de `docs/SPEC.md` (a especificação integral, na íntegra).
3. Trabalhe na ordem das fases; não avance além da fase atual por conta própria.
4. Todo trabalho corresponde a um item do ROADMAP (acrescente o item antes, se faltar).
5. Ao concluir uma fase, faça a auditoria completa e registre em `docs/audits/fase-N.md`.
6. PR com CI verde é mergeado; confirme a release e passe ao usuário um roteiro de teste no aparelho.

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
