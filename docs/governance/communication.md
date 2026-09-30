# Comunicação humano↔máquina e máquina↔máquina

> **Autoridade:** normativa, subordinada a MANIFEST §22–§24 e às invariantes NN-008, NN-009 e NN-010. Em caso de divergência, o manifesto prevalece.

## 1. Regra de persistência (NN-009)

Decisão material tomada em conversa (chat, reunião, interação com IA) só sustenta implementação duradoura depois de persistida:

| Tipo de decisão | Fonte canônica |
|-----------------|----------------|
| Estrutural/arquitetural | ADR em `docs/adr/` |
| Produto | SPEC/requirements/ROADMAP do componente |
| Resposta a uma pergunta ao proprietário | `docs/governance/decisions.json` (`status: decided`, `record` apontando para a fonte acima) |

"Eu lembro que o usuário queria isso" não é fonte de verdade. O handoff aponta para a fonte persistida, nunca para a conversa.

## 2. Categorias de mensagem (MANIFEST §22.2)

`QUESTION` · `PROPOSAL` · `DECISION_REQUIRED` · `DECISION` · `BLOCKER` · `RESULT` · `INCIDENT` · `HANDOFF`

Onde cada categoria é persistida:

| Categoria | Persistência |
|-----------|--------------|
| `DECISION_REQUIRED` | entrada `pending` em `decisions.json` |
| `DECISION` | entrada `decided` em `decisions.json` + ADR/SPEC/ROADMAP |
| `HANDOFF`, `RESULT`, `BLOCKER`, `INCIDENT` | arquivo em `docs/governance/handoffs/` (campo `category`) |
| `QUESTION`, `PROPOSAL` | Issue/PR ou ADR `Proposto`; viram `DECISION_REQUIRED` quando bloqueiam ou mudam direção |

## 3. Pedido de decisão ao proprietário (MANIFEST §22.3)

Toda entrada em `decisions.json` contém: contexto; decisão exata necessária (`question`); alternativas reais com consequências; consequências gerais; compatibilidade com o manifesto; recomendação técnica quando útil. O agente **não** transforma preferência própria em decisão — a recomendação é marcada como recomendação.

Uma decisão pendente é diferente de erro, tarefa, sugestão ou notificação (MANIFEST §35): é uma pergunta que bloqueia ou altera direção.

## 4. Autonomia (MANIFEST §22.4)

O proprietário não deve ser gargalo para decisões triviais. Agentes decidem sozinhos detalhes de implementação que estejam dentro de contratos aprovados e não mudem produto, boundary, compatibilidade, dados, decisão consolidada nem adicionem tecnologia estrutural. Todo o resto segue NN-011.

## 5. Separação de fato, inferência, proposta e decisão (MANIFEST §53)

Em handoffs, PRs e decisões, marque explicitamente o que é `FATO OBSERVADO` (verificado agora), `INFERÊNCIA`, `PROPOSTA` e `DECISÃO`. Não afirmar estado de código sem verificar; não reutilizar decisão antiga sem conferir sua validade.

## 6. Handoff (MANIFEST §23.1, NN-008)

Arquivo `docs/governance/handoffs/<message_id>.json`, validado por [`handoff.schema.json`](../contracts/schemas/handoff.schema.json).

Campos: `message_id`, `category`, `timestamp`, `agent`, `task_id`, `component`, `state`, `branch`, `commit`, `pr`, `files_changed`, `work_completed`, `verification`, `known_issues`, `blockers`, `next_actions`, `decisions_required`, `normative_sources` (documentos consultados diretamente — NN-010), `invariants` (NN-XXX considerados + evidência — MANIFEST §25) e `cost` (opcional).

- `message_id`: `HO-AAAAMMDD-slug`, igual ao nome do arquivo; nunca reutilizado.
- `task_id`: ID do ROADMAP (`P1-1`) ou de Issue.
- `commit`: SHA, ou `null` quando o handoff está no próprio commit que descreve.
- `state`: status machine de [`definition-of-done.md`](definition-of-done.md).
- `verification[].kind`: `automated` ou `human` — nunca misturar (NN-017).
- Proibido: segredos, tokens, raciocínio interno privado.

## 7. Regras entre agentes (MANIFEST §23)

- **Sem impersonação:** um agente nunca escreve como outro agente nem inventa revisão, aprovação ou decisão.
- **Concorrência:** coordenar por escopo, branch e arquivos. Antes de começar, verificar handoffs e branches abertos; declarar a tarefa (`claimed`). Nunca sobrescrever trabalho de outro agente para resolver divergência — registrar `BLOCKER` e pedir coordenação.
- **Canonicalidade:** mensagens entre agentes comunicam estado; não substituem MANIFEST, SPEC, ADR, contratos, ROADMAP ou testes.
