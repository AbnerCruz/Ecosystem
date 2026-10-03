# Consistency checks

Verificações da fundação do Ecosystem (ADR-0003). Componente `consistency-checks` em `ecosystem.json`.

```bash
dotnet run tests/consistency/Check.cs                 # executa todos os checks
dotnet run tests/consistency/Check.cs -- --self-test  # prova que cada check detecta sua violação
```

Requer .NET SDK 10+. Execute a partir de qualquer diretório dentro do repositório. Código de saída `0` = sucesso.

| Check | Fiscaliza | NN |
|-------|-----------|----|
| `CHK-FOUNDATION-FILES` | arquivos obrigatórios da fundação (MANIFEST §45) | — |
| `CHK-SCHEMA` | `ecosystem.json`, `decisions.json`, matriz e handoffs contra seus schemas; paths normativos existem | NN-001, NN-005, NN-014, NN-019, NN-021 |
| `CHK-IDS-UNIQUE` | chaves JSON duplicadas; unicidade de IDs de componentes, ADRs, decisões, handoffs, invariantes | NN-019 |
| `CHK-SINGLE-AUTHORITY` | único manifest raiz; paths sem sobreposição; `not-migrated`/`planned` não existem no repo; `active` existe; autoridade de versão coerente | NN-001, NN-021 |
| `CHK-BOUNDARIES` | grafo declarado: Urbe↔Lunet2D, Product→Product, Hub obrigatório, não-produto→produto, qualquer dependência do portal | NN-002, NN-003, NN-007, NN-023 |
| `CHK-ARCH-REFS` | código real dos produtos ativos: referência a outro Product ou ao Hub; `ProjectReference`/`file:`/`link:` para fora do produto | NN-002, NN-003, NN-023 |
| `CHK-REGISTRY` | Fase 2: Registry de produção consistente (providers, consumers, versões, permissões, Tool sem Host) e vertical slice em `docs/contracts/examples` (positivo passa, cada negativo falha com o código esperado); exemplos de Context e Distribution Profile | NN-006, NN-007, NN-016, NN-023 |
| `CHK-MIGRATION-HISTORY` | histórico importado preservado: ponta importada ancestral do HEAD com a contagem registrada, tags de cada produto presentes no commit registrado (`docs/migration/import-*.json`, `tags-*.txt`); precisa de histórico completo, senão "não verificado" | NN-012 |
| `CHK-VALIDATION` | registros de validação por build: estado sustentado pela evidência; evidência de handoff coincide com o handoff | NN-001, NN-017, NN-018 |
| `CHK-STATE-CONSISTENCY` | deriva entre fontes estruturadas: gate `aprovado` sem itens abertos; `ecosystem.phase` coerente com o ROADMAP; ROADMAP × Issues (com `site/data/issues-snapshot.json`, senão "não verificado"); "Não decidido" de ARCHITECTURE × decisions.json; mecanismos `planned` da matriz de fase já aprovada ([`state-drift.md`](../../docs/governance/state-drift.md)) | NN-001, NN-017, NN-021 |
| `CHK-GENERIC-DIRS` | diretórios genéricos (`shared`, `common`, `utils`…) na raiz e em `platform/`, `workspaces/`, `tools/` | NN-004 |
| `CHK-SHARED-DECLARATION` | componentes compartilhados declaram responsabilidade, contrato, consumidores, compatibilidade, motivo | NN-004, NN-022 |
| `CHK-ENFORCEMENT-MATRIX` | matriz cobre exatamente as NN do MANIFEST, com os mesmos títulos; mecanismos coerentes; checks referenciados existem | MANIFEST §0.2 |
| `CHK-AGENTS-NN` | `AGENTS.md` reproduz todas as NN com o mesmo título e cláusula de enforcement; referencia o MANIFEST | NN-010 |
| `CHK-ADR` | nome, título, seções obrigatórias, status válido e índice dos ADRs | NN-011 |
| `CHK-DECISIONS` | decisão tomada aponta para registro persistido existente; decisão pendente exige objeto (`related`) existente | NN-009, NN-021 |
| `CHK-HANDOFFS` | referências válidas; `reuse_assessment` (ADR-0011): consumidor concreto + Extraction Review, segundo componente exige `external-consumer-exists`; `done` exige verificação passada com evidência e sem bloqueios; validação humana pendente exige objeto; `base_commit`/`commit`/`tested_commit` coerentes com o histórico; `timestamp` nunca no futuro | NN-008, NN-010, NN-017, NN-018, NN-021 |
| `CHK-ROADMAP` | fases 0–7 presentes; item `[x]` sem validação humana pendente | NN-017 |
| `CHK-SECRETS` | padrões comuns de tokens e chaves privadas | MANIFEST §30.2 |
| `CHK-DECISION-FLOW` | workflow de registro de decisões: guarda do dono, permissões explícitas, texto da Issue só em `env`, link de resposta no portal; o self-test executa o aplicador (feliz + recusas) | NN-009, NN-016 |
| `CHK-INTEGRATION` | integrador automático (ADR-0015, ADD-0012): política única (`integration-policy.json`) cobrindo control plane e constituição, toda regra implementada, sem `mergePolicy` paralelo; classificação com a política da `main`; checker confiável; status com o commit combinado; `land()` confere head do PR, ref = commit testado, `main` e autor da label antes do único push; rotina integra sozinha; sem force na `main`; texto do PR só em `env`; simulação no CI; CI do Ecosystem e de cada Product reutilizado no estado combinado | NN-008, NN-011, NN-016, NN-018 |
| `CHK-PORTAL` | portal sem dados canônicos escritos à mão; projeção gerada válida e coerente com as fontes (inclui gates, decisões e validações pendentes); `VALIDATED` exige evidência | NN-001, NN-017, NN-021 |

A lista de NN por check é informativa; a autoridade é [`docs/governance/enforcement-matrix.json`](../../docs/governance/enforcement-matrix.json).

## Rastreabilidade e escopo no integrador (P3-14)

`--integration-gates` usa as regras da **main**, os arquivos do diff real e os handoffs alterados pelo PR. `ChangeReview` bloqueia mudanças em `ARCHITECTURE.md`, `docs/contracts/**` ou com achados `architecture`/`compatibility` sem um ADR **Aceito/Aprovado** em `normative_sources` de um desses handoffs. O caminho qualifica o ADR do Ecosystem ou do Product; arquivo inexistente ou ADR proposto não serve. A revisão ainda precisa conferir se a decisão citada sustenta a mudança.

Migração e refatoração declaram `change_scope` no handoff (schema do handoff). Exemplo de metadados, a preencher com evidência real:

```json
{
  "change_scope": {
    "kind": "refactor",
    "product": "urbe",
    "exceptions": [],
    "before_check": "Testes do Urbe antes",
    "after_check": "Testes do Urbe depois"
  }
}
```

Cada nome aponta para uma verificação única `automated/passed`, com evidência e `tested_commit`: antes identifica `base_commit`; depois identifica o estado testado diferente da base. `CHK-HANDOFFS` confere a existência dos commits no histórico. O CI do Product continua obrigatório no estado combinado.

O diff fica no Product declarado, no próprio handoff e no ROADMAP. Arquivo externo só passa como exceção com caminho **exato** presente no diff e justificativa técnica; outro Product nunca é exceção. Não se mistura `migration` e `refactor`, nem dois Products, no mesmo PR. A passagem `not-migrated` → `active` exige declaração `migration` automaticamente. Para refatorações e outras migrações, a intenção é declarada pelo agente e conferida na revisão; o checker não deduz preservação de comportamento pelo nome de um arquivo. Uma necessidade funcional deve ser tarefa própria (NN-013).

O self-test cobre passagem de rotina, ADR ausente/proposto, importação sem declaração, mistura de trilhos/Products, exceções inválidas, path traversal e evidências ausentes/reprovadas/reutilizadas. Estas regras não substituem auditoria de histórico, revisão das exceções ou testes do Product.

## Adicionar um check

1. Implementar em `Check.cs` com novo ID `CHK-...` e adicioná-lo a `Checks.Ids`.
2. Adicionar ao menos um caso em `SelfTest.Cases` (o self-test falha se algum check não tiver caso).
3. Referenciar o ID na matriz de enforcement e nesta tabela.

## Pré-integração (ADR-0014)

```bash
dotnet run tests/consistency/Check.cs -- --integration <branch> [--base origin/main]
```

Compara a branch com a base atual usando só o git: `FRESH` (0) quando a base é ancestral da branch; `STALE` (3) quando a base andou desde que o trabalho começou; `STALE` com sobreposição (4) quando os dois lados mudaram os mesmos arquivos (arquivos de alto risco destacados). Com `STALE`, reconcilie (merge da `main` na branch) e rerode os checks no estado combinado antes de integrar. O self-test reproduz o cenário de dois agentes (`IntegrationTests`). `CHK-HANDOFFS` também verifica `base_commit`/`commit`/`tested_commit` contra o histórico (exige `fetch-depth: 0`).

## Integrador automático (ADR-0015, ADD-0012)

```bash
dotnet run tests/consistency/Check.cs -- --integration-plan --input <prs.json> [--policy docs/governance/integration-policy.json]
dotnet run tests/consistency/Check.cs -- --integration-gates --base-root <main> --combined-root <main+PR> --files <arquivos-mudados>
dotnet run tests/consistency/Check.cs -- --integration-authorization --events <json> --statuses <json> --owner <login> [--policy ...]
bash .github/integrator/simulate.sh
```

As decisões do integrador (`.github/workflows/integrate.yml`, cola em `.github/integrator/integrate.sh`) são funções puras que imprimem linhas `chave=valor` para `$GITHUB_OUTPUT`:

- `--integration-plan` escolhe `land`, `evaluate` ou `none` a partir dos PRs abertos e do status `ecosystem/integration` (que carrega o commit combinado exato testado);
- `--integration-gates` diz quais componentes e Products o PR toca, se o handoff está em ordem e a **criticidade** (`routine`/`critical`, com classes e motivos), sempre com a política e as regras de `--base-root` (a `main`);
- `--integration-authorization` confere se a label de autorização foi posta por um autorizador da política depois da avaliação do head atual.

O self-test (`IntegrationQueueTests`) cobre fila, classificação (rotina e cada classe crítica, falha fechada, escalada do agente, política que tenta se afrouxar) e autorização. `simulate.sh` prova o efeito no git com o histórico real, um `origin` local e um `gh` simulado com estado:

- rotina e rotina Urbe integram sozinhas;
- crítico espera;
- label de quem não é o proprietário é recusada;
- label do proprietário integra o commit exato;
- ref de integração mutada não entra;
- corridas do head e da `main` reavaliam;
- checker enfraquecido e política afrouxada são críticos;
- conflito e CI vermelho voltam ao autor;
- só fast-forward.

O CI roda os dois.

A simulação também confere o dispatch seletivo de `hub-release.yml` depois de integrar o Hub: mesmos caminhos do filtro `push.paths`, nenhum APK por documentação/testes isolados ou outros Products, nenhuma publicação quando checks/guardas impedem integração. Falha de dispatch deixa o código integrado, sinaliza publicação pendente no PR e reprova o job sem parar o redisparo da fila.
