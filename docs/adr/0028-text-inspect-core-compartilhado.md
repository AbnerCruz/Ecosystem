# ADR-0028 — Núcleo compartilhado de `text.inspect` para o segundo Host

## Status

Proposto — aguarda DEC-0038.

## Contexto

P5-4 foi concluído e aprovado pelo proprietário na Issue #225. O Hub já executa `text.inspect@1.0.0` como primeiro Host; o Lunet é o segundo Product Shell real e precisa executar a mesma capability localmente, sem depender do Hub. ADR-0011/NN-022 exigem Extraction Review antes de qualquer promoção compartilhada.

A revisão em `docs/architecture/p5-5-text-inspect-extraction-review.md` conclui que existe saldo positivo somente para a parte pura da inspeção de texto. A implementação atual da Tool mistura esse núcleo com tipos do runtime experimental do Hub; promover o runtime inteiro congelaria detalhes que não têm segundo consumidor comprovado.

A Fase 7 continua sendo a primeira Tool compartilhada standalone. Esta decisão trata apenas de uma Library interna sem lifecycle próprio.

## Problema

Como garantir que Hub e Lunet executem a mesma implementação de `text.inspect@1.0.0`, sem duplicação e sem transformar `Hub.Core`, Binder ou `ecosystem-local/0` em dependência compartilhada?

## Opções

1. **A — Extrair somente o núcleo puro para uma Library C# compartilhada (recomendada).**
   - Criar um componente `library` host-neutro em `platform/text-inspection`.
   - A Library recebe texto em memória e retorna os três contadores definidos pelo contrato.
   - Hub e Lunet mantêm adapters/Hosts próprios e dependem apenas da Library.
   - Nenhum lifecycle, identidade, grant, Binder ou protocolo local entra na Library.

2. **B — Extrair o Capability Runtime/Host SDK do Hub junto com a Tool.**
   - Promover `LocalCapabilityHost`, contratos locais, gateway e abstrações de sessão como infraestrutura compartilhada.
   - Reduz mais código local no curto prazo, mas congela o primeiro experimento como SDK antes de existir segundo consumidor real do runtime concreto.

3. **C — Copiar a implementação de `text.inspect` para o Lunet.**
   - Evita componente compartilhado agora.
   - Cria duas implementações da mesma capability e torna drift semântico inevitável; contradiz o objetivo explícito de P5-5.

4. **D — Manter `text.inspect` apenas remoto no Hub via Binder.**
   - Reutiliza a implementação atual sem nova Library.
   - Lunet não se torna segundo Host standalone e passa a depender do Hub para essa capability; P5-5 não é atendido.

## Decisão

Pendente de DEC-0038.

Recomendação técnica: **A**.

## Consequências

Se A for escolhida:

- `text.inspect` continua sendo a mesma capability pública `1.0.0`; o contrato JSON não muda.
- Surge um componente compartilhado do tipo `library`, com responsabilidade limitada ao algoritmo/validação host-neutra.
- `ecosystem.json` deve declarar responsabilidade, contrato, consumidores `hub` e `lunet2d`, compatibilidade e motivo de extração.
- Hub deixa de possuir a implementação pura, mas mantém seu runtime, Host API binding e adapters.
- Lunet ganha Host em processo próprio para a capability, sem referência a `Hub.Core`.
- A Library não possui UI nem lifecycle de usuário e, portanto, não antecipa a Tool compartilhada da Fase 7.
- `LocalCapabilityHost`, `AuthenticatedHostGateway`, Binder e `ecosystem-local/0` continuam locais.
- Mudanças futuras na API C# da Library exigem compatibilidade explícita, mas mudanças de Host não exigem atualizar a Library se o contrato funcional permanecer estável.

## Alternativas rejeitadas

Nenhuma alternativa está rejeitada até a DEC-0038 ser decidida. A Extraction Review recomenda rejeitar B, C e D pelos custos descritos acima.

## Referências

DEC-0038; ADR-0009; ADR-0011; ADR-0024; ADR-0027; MANIFEST NN-003, NN-004, NN-006, NN-007, NN-020, NN-022, NN-023; `docs/architecture/p5-5-text-inspect-extraction-review.md`; `docs/contracts/capabilities/text.inspect.json`; ROADMAP P5-5; Issue #227.
