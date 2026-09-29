# Auditoria da Fase 1 — Vertical Slice

- Data: 2026-09-29
- Versões testadas em aparelho: v0.0.1-dev.8 (gate), v0.0.1-dev.63 (Explorer, recuperação, ZIP)

## 1. Gate (§28)

"No telefone é possível criar projeto, escrever C#, apertar Run e mover sprite com toque": **aprovado** (captura de tela do usuário na dev.8).

## 2. Itens

| Item | Estado |
| --- | --- |
| Criar projeto em pasta comum com `lunet.json` | validado |
| Editar C# com salvamento atômico | validado |
| Roslyn no Android, diagnósticos com arquivo/linha/coluna | validado |
| Framework mínimo, OpenGL ES, sprite, toque, Preview (Run/Stop/Restart/Pause/Step) | validado |
| Explorer | validado (criar/renomear/excluir pastas) |
| Recuperação após crash | fechar pelos recentes salva o texto (salvamento ao pausar, validado); a recuperação de encerramento sem pausa é coberta por testes automáticos |
| Exportar ZIP | validado na dev.63 (o botão estava fora da tela; corrigido) |
| Workspace mínimo do §33 | Explorer, Editor, Preview e Problems/Console validados; Documentation → Fase 3; opção "Tutorial" → Fase 11 |

## 3. Lacunas do spec

Painel de documentação (§33) e opção Tutorial: movidos para as Fases 3 e 11. Snapshot do projeto inteiro (§22): Fase 13.

## 4–5. Princípios e qualidade

Offline first, sem conta, projetos como arquivos: ok. **Bug grave encontrado depois do gate:** perda de código ao voltar do Preview (corrigido com `EditorSession`, ver auditoria da Fase 2). Bug de layout: menu escondido na barra do editor (corrigido).

## 6–9. Arquitetura, regressão, documentação, riscos

`ArchitectureTests` verdes; suíte completa verde; riscos 1–3 do §32 validados.

## Decisão

Fase 1 **concluída**.
