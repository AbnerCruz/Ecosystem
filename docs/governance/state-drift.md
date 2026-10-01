# Deriva de estado: o que é fiscalizado e o que é revisão obrigatória

> **Autoridade:** operacional (ADR-0010). Explica como o Ecosystem impede que uma verdade antiga continue parecendo verdade depois que o sistema avançou.

## 1. Regra

**Quanto mais volátil a informação, menos lugares devem copiá-la à mão.** Estado vivo tem **uma** autoridade; qualquer outra superfície ou (a) aponta para ela, (b) a deriva por gerador, ou (c) é uma cópia **declarada e verificada** por check.

| Pergunta | Autoridade | Superfícies derivadas / cópias |
|----------|-----------|--------------------------------|
| Fase e estado de cada gate | `ROADMAP.md` (`*Estado do gate:*`) | `ecosystem.phase` (cópia verificada; DEC-0019), portal |
| Quais Products existem, onde vivem, `status` | `ecosystem.json` | portal (projeção), `CHK-SINGLE-AUTHORITY` |
| Tarefa em andamento | Issues (`state:<estado>`) | — |
| Tarefa concluída e evidência | `[x]` no ROADMAP + handoff | `CHK-ROADMAP`, `CHK-STATE-CONSISTENCY` |
| Decisão pendente/decidida | `decisions.json` | portal (`CHK-PORTAL`), seção "Não decidido" de `ARCHITECTURE.md` (`CHK-STATE-CONSISTENCY`) |
| Validação humana pendente | handoff (`verification`) | portal (`CHK-PORTAL`) |
| Estado de validação de um build | `docs/validation/**` | portal (`CHK-PORTAL`, `CHK-VALIDATION`) |
| Mecanismo de enforcement e seu estado | `enforcement-matrix.json` | `ARCHITECTURE.md` só aponta |

## 2. O que é fiscalizado automaticamente (`CHK-STATE-CONSISTENCY` e vizinhos)

Somente relações entre **fontes estruturadas** (JSON, linhas de formato fixo, IDs). Nunca grep de palavras em prosa.

1. Gate `aprovado` sem itens da fase abertos.
2. `ecosystem.phase` coerente com os gates e as tarefas iniciadas do ROADMAP.
3. `[x]`/`[~]` do ROADMAP × Issue da tarefa (quando há instantâneo das Issues; sem ele, "não verificado").
4. Seção "Não decidido" de `ARCHITECTURE.md` × `decisions.json` (por ID citado).
5. Mecanismo `planned` da matriz com `phase` de gate já aprovado.
6. Projeção do portal × decisões e validações (`CHK-PORTAL`); registro de validação × handoff (`CHK-VALIDATION`).

## 3. O que continua sendo revisão documental obrigatória

Sem estrutura confiável para checar, portanto **responsabilidade de quem muda o estado** (lista de verificação ao encerrar uma tarefa, fase ou gate):

- [ ] `README.md` não declara estado (só aponta). `ARCHITECTURE.md` descreve a arquitetura atual; "Planejado" e "Não decidido" separados (§8).
- [ ] Documentos de migração/inventário: o texto histórico é datado ("em 2026-09-30…") ou é marcado como evidência, não como estado atual.
- [ ] ADRs: status coerente com `decisions.json`; um ADR não é alterado para refletir o presente (ele é histórico; substitua-o por outro).
- [ ] Documentos de visão (`product-vision.md`) dizem que são visão, não estado.
- [ ] Ao fechar uma tarefa: Issue fechada com label `state:done` e link do handoff; `[x]` no ROADMAP; handoff `done`.

## 4. Causas-raiz tratadas (auditoria 2026-10-01)

| Sintoma | Causa | Tratamento |
|---------|-------|-----------|
| README com "não migrado" / "Fase 0" | estado volátil copiado em prosa, sem check | README sem estado, aponta para as autoridades |
| `ecosystem.phase = phase-0` | campo sem semântica definida e sem relação com o ROADMAP | coerência neutra + ADR-0010/DEC-0019 |
| Issue #6 `state:review` com P1-3 `[x]` | nenhum check relacionava ROADMAP e Issue; Issues das tarefas seguintes nem foram abertas | `CHK-STATE-CONSISTENCY` (3) e regra de abrir Issue para tarefa em andamento |
| `ARCHITECTURE.md` com "planejado para a Fase 1" | documento "inicial" nunca reclassificado; matriz com `planned` vencido | §8 em Planejado × Não decidido; checks 4 e 5 |
