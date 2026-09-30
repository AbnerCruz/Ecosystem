# Governança

| Arquivo | Conteúdo | Formato |
|---------|----------|---------|
| [`communication.md`](communication.md) | Comunicação humano↔máquina e máquina↔máquina, categorias de mensagem, handoff. | normativo (abaixo do MANIFEST) |
| [`definition-of-done.md`](definition-of-done.md) | Definition of Done, status machine, `automated verified` × `human validated`. | normativo |
| [`enforcement-matrix.json`](enforcement-matrix.json) | `NN-XXX` → mecanismos de fiscalização (DOC/SCHEMA/TEST/ARCH/CI/REVIEW/ADR/DEVICE/RUNTIME). | [schema](../contracts/schemas/enforcement-matrix.schema.json) |
| [`decisions.json`](decisions.json) | Perguntas ao proprietário — "onde o ecossistema precisa de mim?" (MANIFEST §35). | [schema](../contracts/schemas/decisions.schema.json) |
| [`addenda/`](addenda/) | Texto integral de adendos/instruções do proprietário (MANIFEST §24, nível 1). | Markdown |
| [`handoffs/`](handoffs/) | Um handoff/resultado por arquivo. | [schema](../contracts/schemas/handoff.schema.json) |

Todos os arquivos JSON são validados por `dotnet run tests/consistency/Check.cs`.
