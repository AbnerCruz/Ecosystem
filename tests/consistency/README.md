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
| `CHK-HANDOFFS` | referências válidas; `reuse_assessment` (ADR-0011): consumidor concreto + Extraction Review, segundo componente exige `external-consumer-exists`; `done` exige verificação passada com evidência e sem bloqueios; validação humana pendente exige objeto | NN-008, NN-010, NN-017, NN-018, NN-021 |
| `CHK-ROADMAP` | fases 0–7 presentes; item `[x]` sem validação humana pendente | NN-017 |
| `CHK-SECRETS` | padrões comuns de tokens e chaves privadas | MANIFEST §30.2 |
| `CHK-DECISION-FLOW` | workflow de registro de decisões: guarda do dono, permissões explícitas, texto da Issue só em `env`, link de resposta no portal; o self-test executa o aplicador (feliz + recusas) | NN-009, NN-016 |
| `CHK-PORTAL` | portal sem dados canônicos escritos à mão; projeção gerada válida e coerente com as fontes (inclui gates, decisões e validações pendentes); `VALIDATED` exige evidência | NN-001, NN-017, NN-021 |

A lista de NN por check é informativa; a autoridade é [`docs/governance/enforcement-matrix.json`](../../docs/governance/enforcement-matrix.json).

## Adicionar um check

1. Implementar em `Check.cs` com novo ID `CHK-...` e adicioná-lo a `Checks.Ids`.
2. Adicionar ao menos um caso em `SelfTest.Cases` (o self-test falha se algum check não tiver caso).
3. Referenciar o ID na matriz de enforcement e nesta tabela.
