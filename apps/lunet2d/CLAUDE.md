# Instruções para agentes (Claude Code e similares)

Este repositório implementa o Lunet2D. Antes de qualquer trabalho, siga `docs/DEVELOPMENT.md`:

1. Leia `ROADMAP.md` (estado atual) e rode `bash tools/roadmap-status.sh --pending`.
2. Releia as seções relevantes de `docs/SPEC.md` (a especificação integral, na íntegra).
3. Trabalhe na ordem das fases; não avance além da fase atual por conta própria.
4. Todo trabalho corresponde a um item do ROADMAP (acrescente o item antes, se faltar).
5. Ao concluir uma fase, faça a auditoria completa e registre em `docs/audits/fase-N.md`.
6. PR com CI verde é mergeado; confirme a release e passe ao usuário um roteiro de teste no aparelho.

Ferramentas úteis: `dotnet test --project tests/Lunet.Tests` (só precisa do .NET 10 SDK). O APK só compila no CI (o SDK do Android não está disponível no ambiente de agente).
