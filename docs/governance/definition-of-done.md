# Definition of Done e status machine

> **Autoridade:** normativa, subordinada a MANIFEST §19, §28 e às invariantes NN-017 e NN-018.

## 1. Status machine de tarefas

Estados (MANIFEST §19): `planned` → `claimed` → `working` → `verifying` → `review` → `done`, com `waiting`, `blocked`, `failed` e `cancelled` como desvios.

```text
planned ─► claimed ─► working ─► verifying ─► review ─► done
                        │  ▲         │           │
                        ▼  │         ▼           ▼
                  waiting/blocked   failed    working (mudanças pedidas)
qualquer estado ─► cancelled
```

| Estado | Significado |
|--------|-------------|
| `planned` | existe no ROADMAP/Issue; ninguém assumiu |
| `claimed` | um agente/humano declarou que vai executar (handoff ou Issue) |
| `working` | em execução |
| `waiting` | aguardando algo externo não bloqueante (CI, outra tarefa) |
| `blocked` | impedido; `blockers` obrigatório |
| `verifying` | executando verificações |
| `review` | aguardando revisão humana/independente ou validação humana |
| `done` | Definition of Done satisfeita, com evidência |
| `failed` | tentativa encerrada sem sucesso; motivo registrado |
| `cancelled` | não será feita; motivo registrado |

## 2. Dois níveis de verificação (NN-017)

- **`automated verified`** — verificações automáticas passaram (checks, testes, CI).
- **`human validated`** — um humano validou o que só pode ser validado por humano: dispositivo real, Android lifecycle, toque, layout, performance percebida, comportamento visual, instalação/atualização.

Uma tarefa que depende de validação humana fica em `review` até ela acontecer; no ROADMAP aparece como `[~]`, nunca `[x]`.

## 3. Definition of Done (MANIFEST §28, NN-018)

Quando aplicável, "done" exige:

- [ ] implementação;
- [ ] integração;
- [ ] testes;
- [ ] verificação (`dotnet run tests/consistency/Check.cs` no mínimo);
- [ ] documentação atualizada no mesmo conjunto de mudanças;
- [ ] boundaries respeitados;
- [ ] contratos atualizados;
- [ ] compatibilidade analisada;
- [ ] CI verde;
- [ ] ausência de regressão conhecida;
- [ ] evidência do resultado (handoff com `verification[].evidence`);
- [ ] validação humana quando o comportamento depende de dispositivo ou experiência visual.

`CHK-HANDOFFS` recusa handoff `done` sem verificação, com verificação pendente/falha, sem evidência ou com bloqueios abertos.
