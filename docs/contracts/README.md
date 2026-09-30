# Contracts

> **Autoridade:** contratos são normativos (MANIFEST §24, nível 4). Um contrato não é "o jeito como o código atualmente funciona": é documentado, versionado e testável (MANIFEST §6.9).

## Estado atual

Ainda **não existe** nenhum contrato de capability ou de runtime. O formato de `ComponentManifest`, `Capability`, versionamento e permission model é trabalho da **Fase 2** e exigirá ADR.

Existem apenas os contratos dos registros da fundação (ADR-0002), em [`schemas/`](schemas/):

| Schema | Autoridade de | ID |
|--------|---------------|----|
| [`ecosystem.schema.json`](schemas/ecosystem.schema.json) | formato de `ecosystem.json` | `ecosystem/contracts/ecosystem-manifest/1` |
| [`handoff.schema.json`](schemas/handoff.schema.json) | handoffs de agentes | `ecosystem/contracts/handoff/1` |
| [`decisions.schema.json`](schemas/decisions.schema.json) | decisões do proprietário | `ecosystem/contracts/decisions/1` |
| [`enforcement-matrix.schema.json`](schemas/enforcement-matrix.schema.json) | matriz NN → fiscalização | `ecosystem/contracts/enforcement-matrix/1` |

Os schemas usam o subconjunto de JSON Schema 2020-12 suportado por `tests/consistency/Check.cs` (ADR-0003); o check falha se um schema usar keyword não suportada.

## Requisitos mínimos de um contrato de capability (NN-006)

Quando a Fase 2 formalizar capabilities, cada contrato público ou compartilhado deverá declarar, no mínimo:

- ID estável;
- versão;
- provider;
- inputs;
- outputs;
- erros;
- permissões;
- lifecycle;
- política de compatibilidade.

E deverá ter: schema/contract validável, contract tests e compatibility checks.

## Requisitos de um protocolo runtime (MANIFEST §14)

A especificação de IPC deve separar command, event, request, response, capability discovery, permission, error, cancellation e progress; usar envelope versionado; e não deixar o transporte vazar para o contrato. Deve existir antes da primeira dependência séria entre processos (Fase 5).
