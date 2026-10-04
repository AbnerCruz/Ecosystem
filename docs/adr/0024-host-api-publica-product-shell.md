# ADR-0024 — Host API pública e neutra de transporte para Product Shells

## Status

Proposto — aguarda decisão do proprietário em DEC-0034. Nenhuma Host API pública, transporte IPC ou extração compartilhada é autorizada por este ADR enquanto a decisão estiver pendente.

## Contexto

ADR-0012 estabilizou os contratos de Capability, versões/compatibilidade, Context, permissões e Registry. P5-2 provou no Hub um runtime local experimental e a Tool `text.inspect`; P5-1/P5-2 estão concluídos com integração, CI Android e validação humana. O primeiro slice de P5-3 adicionou o contrato `text.inspect` 1.0.0 em estado `draft` e conformance local, sem registrar provider público nem congelar `ecosystem-local/0`.

O modelo de produto já define Product Shell como Host especializado de um Product, distinto do Hub. A mesma Tool deve poder funcionar sob Hosts diferentes sem conhecer `Hub`, `Lunet` ou `Urbe` (NN-007).

FATO OBSERVADO: Lunet2D é hoje o candidato mais curto para uma futura prova Android porque já possui Activity, editor e identidade persistida de projeto. Porém sua Fase 3 continua aberta e sua governança local proíbe avançar autonomamente para trabalho de fases posteriores. Urbe C# ainda não possui Product Shell pronto. Ecosystem AI possui sessão headless, mas não constitui por si só um segundo Product Shell visual.

## Problema

Definir a fronteira pública da Host API sem:

- promover a API experimental específica do primeiro Host;
- acoplar a semântica pública a um transporte IPC antes de existir o segundo Host pronto;
- duplicar Registry, Context, versão ou permissões já canônicos;
- extrair código compartilhado antes de existir consumidor real e Extraction Review;
- fazer uma Tool conhecer o Host concreto.

A Host API precisa dar aos Product Shells uma semântica comum para discovery e execução, ao mesmo tempo em que deixa autenticação interprocessos e detalhes de wire para P5-4.

## Opções

1. **Host API semântica e neutra de transporte.** Definir primeiro operações e invariantes públicas independentes de processo; bindings em processo e futuros transportes apenas adaptam essa semântica.
2. **Promover `ecosystem-local/0` como API pública.** Congelar a API experimental atual do Hub e exigir que outros Hosts se adaptem a ela.
3. **Definir o IPC como a própria Host API.** Escolher primeiro socket/serviço Android/outro wire protocol e derivar a semântica pública dele.

## Decisão

**Proposta recomendada: opção 1.** Esta seção descreve a proposta submetida à DEC-0034; ela não está aceita enquanto a decisão permanecer pendente.

A Host API v1 deve ser um **contrato semântico**, não um transporte e não uma biblioteca obrigatória. A fonte normativa deve reutilizar os contratos aceitos da Fase 2 em vez de copiá-los.

A fronteira proposta possui estas operações conceituais:

- **abrir sessão**: o Host captura `Context`, identidade do caller/ator e grants efetivos;
- **descobrir**: retorna somente capabilities compatíveis e permitidas naquele Context;
- **invocar**: executa uma capability/version conforme contrato, input e lifecycle;
- **cancelar**: solicita cancelamento de uma operação identificada;
- **revogar**: o Host pode reduzir grants/encerrar acesso; revogação nunca eleva privilégio;
- **fechar sessão**: invalida discovery e novas chamadas e impede resultado tardio de virar sucesso.

Invariantes propostas:

- `Context` continua usando `context.schema.json`; a Host API não cria outro modelo de scope;
- capability/version/inputs/outputs/errors/lifecycle continuam vindo de `docs/contracts/capabilities/*.json`;
- compatibilidade continua seguindo ADR-0012 e Registry derivado; a Host API não cria um resolvedor paralelo;
- permissões continuam vindo de `permissions.json` e são deny-by-default;
- grants são capturados pelo Host, não pelo payload da Tool; uma sessão não pode se autoelevar;
- identidade do caller é Host-owned e não pode ser sobrescrita pelo input. P5-4 define como um caller interprocesso é autenticado e mapeado para essa identidade;
- erros de capability continuam pertencendo ao contrato da capability; erros da Host API formam uma camada separada de falhas de sessão/discovery/execução, sem expor exceções internas;
- payloads públicos são dados compatíveis com os schemas de contrato, não tipos concretos de Activity, Product Shell ou implementação de UI;
- nenhuma Tool recebe nome/tipo do Host para fazer branching;
- `ecosystem-local/0` permanece detalhe experimental e não ganha compatibilidade pública por esta decisão;
- a Host API v1 não escolhe socket, localhost, Binder/Service Android ou qualquer outro transporte;
- o segundo Host pode implementar/adaptar o contrato sem depender do Hub;
- compartilhamento de runtime/Tool só ocorre depois de consumidor real e Extraction Review positiva (NN-022). A decisão desta API, sozinha, não cria `platform/`, SDK ou package compartilhado.

A implementação normativa após aprovação deve incluir fixtures de conformance independentes de Host cobrindo pelo menos: Context inválido, capability desconhecida, incompatibilidade de versão, permissão ausente, tentativa de elevação de grant, input/output inválidos, cancelamento, revogação, sessão fechada e falha sanitizada do provider.

## Consequências

Se a opção 1 for aprovada:

- P5-3 pode estabilizar a Host API sem escolher prematuramente o wire protocol;
- P5-4 compara transportes usando a mesma semântica e adiciona autenticação de caller, timeout/disconnect e limites;
- P5-5 pode adaptar um Product Shell real quando sua própria governança permitir;
- Hub e Products continuam independentes; nenhum Product depende de `Hub.Core`;
- haverá algum custo de adaptar a implementação experimental do Hub à API pública após a decisão, em vez de declarar a implementação atual como contrato;
- a Extraction Review continua obrigatória antes de mover runtime/Tool para componente compartilhado.

Enquanto DEC-0034 estiver pendente, P5-3 não congela Host API pública e P5-4 não escolhe transporte.

## Alternativas rejeitadas

Ainda não há alternativa rejeitada pelo proprietário. A recomendação rejeita tecnicamente, mas não normativamente, as opções 2 e 3:

- **Opção 2:** congela detalhes do primeiro experimento e transforma conveniência local do Hub em compromisso transversal sem segundo Host.
- **Opção 3:** mistura semântica, autenticação e transporte, invertendo a sequência P5-3 → P5-4 e tornando multiplataforma mais cara.

## Referências

MANIFEST §6, §14–§16, §29; NN-001, NN-003, NN-006, NN-007, NN-010, NN-011, NN-015, NN-016, NN-018, NN-022, NN-023; ADR-0006, ADR-0011, ADR-0012, ADR-0023; docs/architecture/product-model.md; docs/architecture/capability-runtime.md; docs/contracts/schemas/context.schema.json; docs/contracts/schemas/capability-contract.schema.json; docs/contracts/permissions.json; ROADMAP P5-3..P5-5; Issue #175; DEC-0034.
