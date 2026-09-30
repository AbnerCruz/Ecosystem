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
| `claimed` | um agente/humano declarou que vai executar (Issue do GitHub — DEC-0003) |
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

## 4. Estados de validação de build

Aplicam-se a builds/artefatos candidatos (ex.: um APK do Lunet2D) e são o que o portal e, no futuro, o Hub apresentam (ADD-0001, ADR-0005):

| Estado | Significado | Pode vir só de CI? |
|--------|-------------|--------------------|
| `UNKNOWN` | não há registro canônico de validação | — |
| `IMPLEMENTED` | a mudança existe no build | sim |
| `AUTOMATED_VERIFIED` | verificações automáticas passaram para este build | sim |
| `HUMAN_VALIDATION_PENDING` | o requisito exige validação humana (§2) e ela ainda não aconteceu | — |
| `VALIDATED` | um humano validou, com evidência registrada | **nunca** |

CI verde leva no máximo a `AUTOMATED_VERIFIED`. Nenhuma superfície pode apresentar CI verde como "validado no aparelho" (NN-017). Os registros canônicos de validação por build ainda não existem (ROADMAP P1-11); até lá, produtos aparecem como `UNKNOWN`.
