# Contracts

> **Autoridade:** contratos são normativos (MANIFEST §24, nível 4). Um contrato não é "o jeito como o código atualmente funciona": é documentado, versionado e testável (MANIFEST §6.9).

## Estado atual

Contratos da fundação (ADR-0002), da projeção do portal (ADR-0005) e **da Fase 2** (ADR-0012, `Aceito`), em [`schemas/`](schemas/):

| Schema / arquivo | Autoridade de | ID |
|--------|---------------|----|
| [`ecosystem.schema.json`](schemas/ecosystem.schema.json) | formato de `ecosystem.json` **e do ComponentManifest** (a entrada de componente, com `provides`, `requires`, `permissions`) | `ecosystem/contracts/ecosystem-manifest/1` |
| [`capability-contract.schema.json`](schemas/capability-contract.schema.json) | contrato de uma capability (`docs/contracts/capabilities/<id>.json`; `text.inspect` 1.0.0 é estável, ainda sem provider público no Registry) | `ecosystem/contracts/capability-contract/1` |
| [`permissions-catalog.schema.json`](schemas/permissions-catalog.schema.json) · [`permissions.json`](permissions.json) | catálogo de permissões (deny-by-default) | `ecosystem/contracts/permissions-catalog/1` |
| [`context.schema.json`](schemas/context.schema.json) | Context hierárquico (exemplos em [`examples/context/`](examples/context/)) | `ecosystem/contracts/context/1` |
| [`host-api.schema.json`](schemas/host-api.schema.json) · [`host-api.v1.json`](host-api.v1.json) | Host API semântica v1, neutra de transporte (DEC-0034-A / ADR-0024) | `ecosystem/contracts/host-api/1` |
| [`host-api-conformance.schema.json`](schemas/host-api-conformance.schema.json) · [`examples/host-api/conformance.v1.json`](examples/host-api/conformance.v1.json) | matriz independente de Host para conformance da Host API v1 | `ecosystem/contracts/host-api-conformance/1` |
| [`distribution-profile.schema.json`](schemas/distribution-profile.schema.json) | Distribution Profile (nomes dos eixos congelados em P2-9; exemplo em [`examples/distribution/`](examples/distribution/); arranjo real em [`current.profile.json`](../distribution/current.profile.json) e direção decidida em [`target.profile.json`](../distribution/target.profile.json)) | `ecosystem/contracts/distribution-profile/1` |

No `Distribution Profile`, `locationFrom` nunca copia uma URL: `source.repository` e `publicUrl` resolvem campos do Product; `ecosystem.repository` resolve o repositório canônico do monorepo para canais de GitHub Releases publicados diretamente pelo Ecosystem. Isso permite que o Product preserve simultaneamente seu repositório histórico e sua URL pública própria.
| [`handoff.schema.json`](schemas/handoff.schema.json) | handoffs de agentes (inclui `reuse_assessment` opcional, ADR-0011) | `ecosystem/contracts/handoff/1` |
| [`decisions.schema.json`](schemas/decisions.schema.json) | decisões do proprietário | `ecosystem/contracts/decisions/1` |
| [`validation-record.schema.json`](schemas/validation-record.schema.json) | registro de validação por build | `ecosystem/contracts/validation-record/1` |
| [`enforcement-matrix.schema.json`](schemas/enforcement-matrix.schema.json) | matriz NN → fiscalização | `ecosystem/contracts/enforcement-matrix/1` |
| [`ecosystem-status.schema.json`](schemas/ecosystem-status.schema.json) | projeção do portal web (`authority: false`; ADR-0005) | `ecosystem/contracts/ecosystem-status/1` |

**Host API e primeira capability estável (P5-3).** [`host-api.v1.json`](host-api.v1.json) congela a semântica comum de sessão, discovery, invoke, cancelamento, revogação e fechamento sem escolher wire/IPC. [`capabilities/text.inspect.json`](capabilities/text.inspect.json) é estável em 1.0.0: recebe texto em memória, não requer permissões externas e mantém erros de domínio separados dos `hostErrors`. O protocolo `ecosystem-local/0` continua experimental e não é API pública. O Hub prova as fixtures públicas, mas ainda não declara `provides: text.inspect` em `ecosystem.json`; provider compartilhado só nasce com segundo consumidor real e Extraction Review.

**Registry e vertical slice (Fase 2).** O Registry é derivado dos manifests e contratos e mora dentro dos checks (`CHK-REGISTRY`); `dotnet run tests/consistency/Check.cs -- --registry [--file <json com "components">] [--discover <capability> [<faixa>]]` mostra o índice, os providers e consumers e valida. O slice em [`examples/registry-slice/`](examples/registry-slice/) prova o caso positivo (provider A v1, consumer B `^1.0.0`) e os negativos (`^2.0.0` incompatível, sem provider, permissão ausente, capability desconhecida, Tool que conhece Host, Product → Product).

Os schemas usam o subconjunto de JSON Schema 2020-12 suportado por `tests/consistency/Check.cs` (ADR-0003); o check falha se um schema usar keyword não suportada.

## Requisitos mínimos de um contrato de capability (NN-006)

Cada contrato público ou compartilhado declara, no mínimo (formalizado em `capability-contract.schema.json`; o **provider** é derivado dos `provides` dos manifests):

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
