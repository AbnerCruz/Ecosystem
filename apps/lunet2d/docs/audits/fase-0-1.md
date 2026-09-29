# Auditoria parcial — Fases 0 e 1

Auditoria feita ao criar a rotina de desenvolvimento (não é o fechamento formal das fases).

- Versão testada em aparelho: v0.0.1-dev.8 (Fase 1), v0.0.1-dev.13 (Demo), v0.0.1-dev.19 (editor, sem relato de teste).

## Gates

- Fase 0 (CI gera APK instalável): **aprovado**, instalado em aparelho pelo usuário.
- Fase 1 (criar projeto, escrever C#, Run, mover sprite): **aprovado**, confirmado pelo usuário com captura de tela.

## Lacunas encontradas contra o spec

Fase 0: sem solução (`Lunet.slnx`, agora criada), sem `LICENSE.md` (decisão do autor), sem `release-notes.json` (agora gerado na release), pastas do §26 ainda inexistentes (criar quando houver conteúdo).

Fase 1: falta **Explorer**, **recuperação após crash** (§22), exportar ZIP sem teste em aparelho, opção "Tutorial" na criação de projeto (§33), painel de Documentation no workspace mínimo.

Adiantamentos: partes das Fases 2 e 3 foram implementadas antes de fechar a Fase 1. Estão marcadas no ROADMAP conforme o estado real de validação. A partir desta rotina, a ordem das fases passa a ser respeitada.

## Decisão

Fases 0 e 1 **não estão concluídas** até fechar as pendências acima e registrar a auditoria final.
