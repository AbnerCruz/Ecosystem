# P5-5 — Extraction Review de `text.inspect`

## Escopo

Esta revisão é exigida por ADR-0011 e NN-022 antes de mover código de `apps/hub` para um componente compartilhado. O segundo consumidor agora é real: o Hub já executa `text.inspect@1.0.0` e o Lunet, após P5-4, é um Product Shell Android apto a hospedar a mesma capability sem depender do Hub.

A revisão não autoriza extração. Ela responde às cinco perguntas de NN-022 e prepara a decisão estrutural DEC-0038 / ADR-0028.

## Fatos observados

- `text.inspect@1.0.0` é contrato estável em `docs/contracts/capabilities/text.inspect.json`.
- A implementação atual está em `apps/hub/src/Hub.Core/Capabilities/TextInspectionTool.cs`.
- O algoritmo puro conta Unicode scalar values, palavras separadas por whitespace e linhas LF; aceita no máximo 100.000 unidades UTF-16.
- A classe atual também conhece tipos locais do runtime experimental do Hub (`LocalCapability`, `LocalContext`, `LocalInvocation`).
- `LocalCapabilityHost`, `AuthenticatedHostGateway` e `ecosystem-local/0` pertencem ao primeiro Host/experimento e não são necessários para compartilhar o algoritmo.
- P5-5 exige o mesmo comportamento/implementação da Tool em dois Hosts e proíbe tornar o Hub dependência essencial do Lunet.
- A Fase 7 continua reservada à primeira **Tool compartilhada standalone**. P5-5 não deve antecipá-la.

## As cinco perguntas de NN-022

### 1. Qual complexidade é removida?

A extração mínima remove uma duplicação que P5-5 de outra forma teria de criar: validação do input, limite de tamanho, contagem de caracteres Unicode, palavras e linhas e produção do resultado. Uma única implementação evita divergência semântica entre Hub e Lunet e permite a mesma suíte vetorial nos dois Hosts.

Ela **não** remove nem deve tentar unificar lifecycle de sessão, identidade, grants, discovery, Binder, UI ou Product Shell. Esses elementos continuam responsabilidade de cada Host/binding.

### 2. Quais consumidores reais existem?

- `hub`: provider/primeiro Host já executável e testado.
- `lunet2d`: segundo Product Shell real escolhido para P5-5, com Host API/IPC já validada em P5-4 e necessidade concreta de executar a capability localmente sem Hub.

Não há terceiro consumidor necessário para justificar esta revisão.

### 3. Qual contrato estabiliza a relação?

O contrato funcional já existe: `docs/contracts/capabilities/text.inspect.json`, versão `1.0.0`, status `stable`.

A Library proposta deve expor apenas uma API C# host-neutra equivalente a:

- entrada: texto em memória;
- saída: `characters`, `words`, `lines`;
- limite: 100.000 unidades UTF-16;
- cancelamento cooperativo, sem IO, rede, UI, Context ou grants.

Adapters de Host continuam responsáveis por converter JSON/Host API para essa API pura e por mapear falhas para a taxonomia pública. A Library não conhece Host, Product, Binder nem `ecosystem-local/0`.

### 4. Qual custo novo de versionamento/integração surge?

A opção mínima cria um componente C# compartilhado, provavelmente em `platform/text-inspection`, declarado como `library` em `ecosystem.json`, com versão/compatibilidade próprias e testes. Hub e Lunet passam a depender desse componente.

Custos novos: uma versão a manter, build/testes do componente, compatibilidade da API C# e packaging da DLL nos dois Products. Não há processo, UI, release standalone ou protocolo adicional.

Extrair o runtime inteiro teria custo muito maior: congelaria `LocalCapabilityHost`, sessão/journal e detalhes do primeiro Host como SDK público antes de necessidade comprovada.

### 5. Por que o saldo final é positivo?

**Positivo somente para a extração mínima do núcleo puro.** Ela elimina a única duplicação que P5-5 explicitamente proíbe, tem dois consumidores reais e se apoia em um contrato estável já existente.

**Negativo para extrair o runtime/SDK do Hub agora.** Isso aumentaria boundary, versionamento e superfície pública sem segundo consumidor do runtime concreto. `AuthenticatedHostGateway`, Binder e `ecosystem-local/0` permanecem locais.

## Veredito da Extraction Review

**Recomendação: positiva e estreita.**

Promover apenas o núcleo host-neutro de `text.inspect` para uma **Library** compartilhada. Hub e Lunet mantêm adapters/Hosts próprios. Isso não cria a Tool standalone da Fase 7 e não promove o runtime experimental.

A execução depende da DEC-0038 e do ADR-0028. Até a decisão ser registrada, nenhum arquivo de runtime/Tool é movido.

## Critérios de aceite de P5-5 após a decisão

1. Library compartilhada sem dependência de Product/Host/plataforma.
2. Hub usa a Library e mantém conformance existente.
3. Lunet implementa um Host em processo para `text.inspect@1.0.0`, com Context baseado no projeto real e sem exigir Hub.
4. Mesmos vetores de contrato passam nos dois Hosts.
5. Ausência do Hub não altera abertura, edição, build, preview ou execução do Lunet.
6. Nenhuma referência `Lunet -> Hub.Core` ou `Hub -> Lunet`.
7. `ecosystem-local/0`, Binder e autenticação P5-4 não são promovidos por esta extração.
8. P5-6/P5-7 continuam separados.

## Referências

ADR-0009; ADR-0011; ADR-0024; ADR-0027; MANIFEST NN-003, NN-004, NN-006, NN-007, NN-020, NN-022, NN-023; `docs/architecture/capability-runtime.md`; `docs/contracts/capabilities/text.inspect.json`; Issue #227.
