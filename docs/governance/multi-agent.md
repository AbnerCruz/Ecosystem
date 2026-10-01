# Trabalho multiagente: paralelizar, coordenar, revalidar

> **Autoridade:** normativa, subordinada a MANIFEST §23 e §27, ao [ADD-0010](addenda/ADD-0010-concorrencia-multiagente.md), ao [ADD-0011](addenda/ADD-0011-colaboracao-multiagente-sem-verificacao-manual.md), ao [ADR-0014](../adr/0014-fluxo-multiagente-minimo.md) e ao [ADR-0015](../adr/0015-integrador-automatico.md). Complementa [`communication.md`](communication.md) §6–§8.

**Premissa:** o mundo pode ter mudado enquanto um agente trabalhava. Nenhum agente integra trabalho presumindo que a `main` lida no início ainda é a `main` atual.

## 1. Fluxo

```text
main (BASE) ──► branch do agente ──► commits ──► handoff (review) ──► PR pronto ──► CI do PR
                                                                                      │
integrador (§5): main ATUAL + PR ──► conflito? ── sim ──► volta ao autor: merge da main na branch,
                      │                                    resolução semântica, push (reavaliado sozinho)
                     não
                      │
       checks do Ecosystem + CI dos Products tocados no estado combinado; handoff; política
                      │
       verde e permitido ──► main = commit combinado testado (INTEGRATION) ──► sync da origem, se apps/<id>
```

1. **Começar:** `git fetch`; registrar no handoff `base_commit` = SHA da `main` de onde partiu; declarar a tarefa (Issue com `state:claimed`/`working` quando aplicável, `communication.md` §8).
2. **Trabalhar** numa branch própria. Ler handoffs e branches abertas; se outra tarefa ativa tocar os mesmos arquivos, coordenar (§3).
3. **Terminar a execução:** handoff em `review` (ou `verifying`/`blocked`), nunca `done` antes da integração. `commit` = `null` (o resultado é derivado do commit que introduz o handoff) ou um SHA já existente — nunca o `base_commit`.
4. **Abrir PR** contra a `main`. Mudança em `apps/<id>` **sempre** abre PR: é o PR que dispara o CI do Product (`urbe-checks`, `lunet2d-ci` rodam em `pull_request` e na `main`, não em branches soltas). O PR usa o template (escopo, invariantes, verificação, handoff, base).
5. **Antes de integrar** (o integrador faz 5–7 sozinho, §5; o comando continua útil para conferir localmente): `dotnet run tests/consistency/Check.cs -- --integration <branch>`:
   - `FRESH` (0): a `main` atual é ancestral da branch;
   - `STALE` (3): a `main` andou — faça merge da `main` na branch (sem rebase/force-push em branch de outro agente), rerode os checks;
   - `STALE` + sobreposição (4): os dois lados mudaram os mesmos arquivos — reconciliação semântica obrigatória (§3).
6. **Checks no estado combinado:** `Check.cs` e `--self-test`; para cada Product tocado, o CI do Product no PR (e localmente, quando possível: `npm ci && npm run check` em `apps/urbe`). O que vale é **A + B juntos**, não A e B separados. Registrar em `verification[].tested_commit` o estado testado.
7. **Integrar** respeitando as regras de merge vigentes — quem integra é o integrador (§5), conforme o `mergePolicy`: no Urbe, só com pedido explícito do proprietário (`apps/urbe/AGENTS.md`), dado pela label `integrar`; validação humana pendente continua pendente (NN-017).
8. **Estado final:** o handoff pode entrar na `main` já como `done` **no próprio PR que integra o trabalho** (na `main`, `done` ⇒ integrado; enquanto o PR não é integrado, o `done` da branch é só proposta — o integrador só integra o que passou). Se ainda faltar validação humana, fica em `review`. ROADMAP/Issue atualizados no mesmo PR; se mudou `apps/<id>`, quem estiver presente dispara `sync-from-ecosystem.yml` na origem (DEC-0017-A); senão, o agendamento da origem leva a mudança e o portal mostra o atraso.

## 2. Quatro identidades (ADR-0014)

| Identidade | O que é | Onde está |
|------------|---------|-----------|
| BASE | estado de onde o agente partiu | `base_commit` no handoff (verificado no histórico) |
| WORK RESULT | commit produzido pelo agente | derivado: commit da `branch` que introduz/atualiza o handoff; `commit` só quando já conhecido |
| INTEGRATION | merge final na `main` | derivado do `pr` (merge commit) |
| VALIDATION | estado efetivamente testado | `verification[].tested_commit` (opcional), CI do PR |

## 3. Sobreposição

Mudança no mesmo arquivo por dois agentes **não** significa que um deles está errado; significa que a integração exige reconciliação explícita: merge, leitura dos dois lados, resolução **semântica** (nunca `--ours`/`--theirs` às cegas em arquivo normativo), checks de novo, e registro no handoff (conflito, causa, resolução, evidência).

Arquivos de **alto risco** de conflito semântico (destacados por `--integration`): `ROADMAP.md`, `ARCHITECTURE.md`, `AGENTS.md` (inclusive os locais), `MANIFEST.md`, `ecosystem.json`, `docs/governance/decisions.json`, `docs/governance/enforcement-matrix.json`, `docs/adr/README.md`, `docs/contracts/schemas/*`, `site/generator/*`, `.github/workflows/*`, `tests/consistency/Check.cs`. Numeração (ADR-NNNN, DEC-NNNN, P*n*-*m*) é recurso compartilhado: quem integra depois renumera o próprio item se colidir.

## 4. Estados (sem máquina nova)

A status machine dos handoffs ([`definition-of-done.md`](definition-of-done.md)) já distingue: `working` → `verifying`/`review` (agente terminou; aguardando revisão, validação humana ou integração) → `done` (integrado na `main` **e** verificado). `blocked` quando depende de decisão. Validação humana pendente aparece no portal (`HUMAN_VALIDATION_PENDING`). Não há lock nem reserva de arquivos.

## 5. Integrador automático (ADR-0015)

Ninguém precisa acionar um agente para verificar ou integrar trabalho concorrente ([ADD-0011](addenda/ADD-0011-colaboracao-multiagente-sem-verificacao-manual.md)). O integrador (`.github/workflows/integrate.yml`, componente `integrator`) faz sozinho os passos 5–7 do §1, a cada evento de PR, a cada mudança da `main` e de hora em hora:

1. escolhe **um** PR por vez (fila serial; rascunhos, forks e Dependabot ficam fora);
2. monta o estado combinado **main atual + PR** (`integration/pr-N`); conflito ⇒ devolve ao autor;
3. confere o handoff (pelo menos um em `review`, `verifying` ou `done`; NN-008);
4. roda no estado combinado os checks do Ecosystem e o CI de cada Product tocado;
5. aplica a política: a mais restritiva entre o piso `ecosystem.mergePolicy` e o `mergePolicy` dos componentes tocados — `automatic` integra; `owner-authorization` marca `pronto-para-integrar` e espera a label **`integrar`** do proprietário;
6. integra avançando a `main` para **exatamente** o commit testado, sem force; se a `main` mudou no meio, reavalia.

| No PR | Significa | Quem age |
|-------|-----------|----------|
| status `ecosystem/integration` amarelo (`testando`) | avaliação em andamento | ninguém |
| verde `pronto` + label `pronto-para-integrar` | verde contra a `main` atual; falta autorização | proprietário: label `integrar` |
| verde `integrado` | está na `main` (o PR aparece como mergeado) | ninguém |
| vermelho `conflito`/`falhou`/`bloqueado` + label `precisa-reconciliar` | conflito, check vermelho ou handoff ausente/incompleto | agente autor: corrigir e enviar; o integrador reavalia sozinho |

**Deveres do agente:** trabalhar em branch própria (PR em rascunho enquanto trabalha, se precisar do CI do Product); ao terminar, handoff em `review` (ou `done`, se só faltar a integração) e PR pronto para revisão; atender ao comentário do integrador. **Nunca** integrar à mão nem adicionar a label `integrar` — ela é a autorização do proprietário (MANIFEST §23.2). Enquanto DEC-0023 estiver pendente, o piso é `owner-authorization`: tudo é verificado automaticamente e cada PR verde espera um toque do proprietário.

Proteções que continuam valendo: `--integration` (base obsoleta e sobreposição) para quem quiser conferir antes; o índice de ADRs com `merge=union` validado por `CHK-ADR`; `CHK-INTEGRATION` sobre o próprio integrador.

## 6. Futuro (não implementado)

Quando vários agentes receberem tarefas ao mesmo tempo, o sistema poderá comparar escopo esperado, paths prováveis, componentes e arquivos de alto risco e sinalizar `LOW OVERLAP` (paralelizar) ou `HIGH OVERLAP` (coordenar) antes de começar. Hoje a coordenação acontece na integração.
