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
- `timestamp`: hora real em que o handoff foi escrito (de preferência UTC); nunca no futuro — ele ordena os handoffs da mesma tarefa (`CHK-HANDOFFS`).
- `task_id`: ID do ROADMAP (`P1-1`) ou de Issue.
- `base_commit`: SHA de onde o agente partiu (BASE; obrigatório a partir de 2026-10-01T19:00Z). `commit`: resultado já conhecido ou `null` (derivado do commit que introduz o handoff); nunca o `base_commit`. A integração é o merge do `pr` (feito pelo integrador automático, ADR-0015); `verification[].tested_commit` registra o estado testado. O integrador só aceita PR com pelo menos um handoff em `review`, `verifying` ou `done`. Ver [`multi-agent.md`](multi-agent.md), ADR-0014 e ADR-0015.
- `state`: status machine de [`definition-of-done.md`](definition-of-done.md).
- `verification[].kind`: `automated` ou `human` — nunca misturar (NN-017).
- Proibido: segredos, tokens, raciocínio interno privado.

## 7. Regras entre agentes (MANIFEST §23)

- **Sem impersonação:** um agente nunca escreve como outro agente nem inventa revisão, aprovação ou decisão.
- **Concorrência:** paralelizar o independente, coordenar o que se sobrepõe, revalidar tudo na integração ([`multi-agent.md`](multi-agent.md), ADR-0014, ADR-0015): o integrador automático testa cada PR no estado combinado com a `main` atual e devolve ao autor conflito ou check vermelho; o autor reconcilia (merge da `main` na branch, resolução semântica) e envia de novo. `Check.cs -- --integration <branch>` confere localmente. Coordenar por escopo, branch e arquivos. Antes de começar, verificar handoffs e branches abertos; declarar a tarefa (`claimed`). Nunca sobrescrever trabalho de outro agente para resolver divergência — registrar `BLOCKER` e pedir coordenação.
- **Canonicalidade:** mensagens entre agentes comunicam estado; não substituem MANIFEST, SPEC, ADR, contratos, ROADMAP ou testes.

## 8. Estado vivo das tarefas (DEC-0003)

| Conceito | Autoridade |
|----------|-----------|
| Escopo e IDs das tarefas (`P<fase>-<n>`) | `ROADMAP.md` |
| **Estado vivo** (claimed, working, waiting, blocked, verifying, review) | **Issues do GitHub** |
| Resultado e evidência | handoff em `docs/governance/handoffs/` |

Convenção: uma Issue por tarefa em andamento, com o ID da tarefa no início do título e uma label `state:<estado>` da status machine de [`definition-of-done.md`](definition-of-done.md). Issue aberta = tarefa não encerrada; ao encerrar (`done`, `cancelled` ou `failed`) a Issue é fechada com link para o handoff. A caixa de seleção do ROADMAP (`[x]`/`[~]`/`[ ]`) registra **conclusão com evidência**, nunca o estado vivo. Nenhum dos dois pode contradizer o handoff.

## 8.1 Potencial de reutilização (ADR-0011)

Handoff de trabalho que **cria ou reestrutura** uma funcionalidade relevante pode (e, para funcionalidade nova, deve) trazer `reuse_assessment[]` com `subject`, `status` (`product-specific`, `possible-candidate`, `external-consumer-exists`), `rationale` e, no último caso, `consumers` e `extraction_review` (`pending`, `ADR-NNNN` ou `declined`). É o registro persistente (não a conversa) de que uma feature local foi considerada candidata; o portal o projeta e `CHK-HANDOFFS` o valida. Guia: [`docs/architecture/local-first.md`](../architecture/local-first.md).

## 9. Toda pendência do proprietário aparece no portal, com o objeto (DEC-0007)

O agente **nunca** pede uma decisão ou validação só no chat. Antes de pedir:

- **Decisão pendente:** entrada `status: pending` em [`decisions.json`](decisions.json) com `related` apontando para o(s) documento(s), ADR(s) ou artefato(s) — o **objeto** — que o proprietário precisa olhar. `CHK-DECISIONS` recusa pendência sem objeto.
- **Validação humana pendente:** verificação `kind: human`, `result: pending` em um handoff, com `object` (caminho do repositório ou URL do artefato/página a validar). `CHK-HANDOFFS` recusa validação pendente sem objeto.

O gerador do portal projeta ambas em seções próprias, com link para o objeto, e `CHK-PORTAL` recusa uma projeção que omita alguma pendência. O proprietário responde **clicando na alternativa no portal** (Issue pré-preenchida confirmada no GitHub, registrada pela automação — [`portal.md`](../architecture/portal.md) §3.2, ADR-0007) ou, se preferir, ao agente no chat; nos dois casos a decisão fica em `decisions.json` com a fonte persistida (NN-009). Uma **validação humana** é respondida do mesmo modo, com **Aprovar** ou **Reprovar** no portal (ADR-0008, ADD-0005): a automação grava o resultado na própria verificação do handoff e o agente depois atualiza o `state`. Um agente **nunca** responde por ele (decisão ou validação) nem cria a Issue de resposta. **Veredito do registro:** o workflow `decision` só termina **verde** quando o resultado desejado existe no repositório (registrado agora, ou já registrado com a mesma escolha — reprocessar o evento é idempotente); recusa, erro, falha de consistência ou de push terminam **vermelho**, com a causa em comentário na Issue (`decision-verdict.sh`, testado pelo self-test). "Decidida com consequências ainda a aplicar" (`consequencesApplied: false`) é um estado legítimo de **sucesso** do registrador; "não registrada" nunca é.
