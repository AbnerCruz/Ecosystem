# ADR-0029 — Product independente de autoria matemática temporal

## Status

Proposto — identidade e constituição pendentes na DEC-0040. Não cria Product.

## Contexto

O proprietário solicitou um Product nativo independente para explicar matemática
por composição visual, relações semânticas e animação. Fixou C#, mobile-first,
local-first, uma fonte canônica, engine determinística e o slice Δx → 0 com
save/load e MP4 verdadeiro. `Math Studio` é codinome, não nome público nem ID.
O briefing exige auditar a governança e parar somente em gate humano real.

Main auditada: `43ccb54e328b7b503d3e07adfde36180014e9964`; não há Product ou
decisão matemática anterior. DEC-0027 demonstra a fixação explícita de identidade
do Product de IA; ADR-0022/DEC-0033 demonstram constituição local independente.

## Problema

Materializar a intenção sem converter codinome em identidade, acoplar domínio
ao Hub/Lunet/Urbe ou antecipar renderer/math/timeline globais. A identidade deve
ser legitimada antes de `apps/<id>` e registro, com decisão visível no portal.

## Opções

1. Product independente, ID escolhido pelo proprietário, domínio/engine/editor
   locais, C# e distribuição standalone. Recomendado; intenção da delegação.
2. Feature em Product existente. Rejeitada pela delegação e pelos boundaries.
3. Primeiro construir plataforma matemática compartilhada ou clone de Manim.
   Rejeitada: nenhum segundo consumidor real e risco de duas arquiteturas.

Identidade técnica tem alternativas reais na DEC-0040: `math-authoring` ou
`math-composer`, mantendo o nome público em aberto, ou adiamento da identidade.
O nome de exibição documental provisório descreve o domínio e não anuncia marca.

## Decisão

**Proposta, sem aceite registrado:** opção 1. A escolha A/B da DEC-0040 ratifica
esta constituição e a fundação de documento do ADR-0030, com o ID explícito da
alternativa; não equivale a aprovação de código, assinatura ou canal inexistentes.
Aplicar a decisão no repositório antes de começar MA-001.

Core/math, cena, timeline, render, persistência, operações editoriais e UI são
módulos dentro do Product. Nenhuma referência a outro Product ou nova capability
pública neste corte. Reuso só depois de consumidor concreto e Extraction Review.
O Hub/portal observam metadados; não possuem projetos, runtime nem estado de UI.

## Consequências

- Após identidade: SPEC/AGENTS/ROADMAP/VERSION/CI e registro em `apps/<id>`.
- Produto e engine funcionam offline, sem Hub e sem IA; lifecycle independente.
- Roadmaps atuais de outros Products não mudam nem viram dependências deste.
- PR de constituição/registro/CI é efeito crítico que o integrador avalia pela
  política da main; agente não integra nem coloca `integrar`.
- Marca, licença comercial, assinatura/pacote/canal e dependências tecnológicas
  não são fixados por esta proposta; escolhas relevantes seguem gates próprios
  no momento necessário, com opções/evidências prontas antes de pedir decisão.
- ADR-0030 formaliza documento/texto. SPEC e roadmap propostos dão os critérios
  observáveis de MA-001..011; não declarar implementação ou DEVICE já entregue.

## Alternativas rejeitadas

Opções 2/3; usar `math-studio` como ID permanente sem escolha; depender diretamente
do Lunet por já ter um renderer; Python/Manim como motor; IA como avaliador;
criar pasta vazia e chamar constituição pronta; alterar MANIFEST para facilitar.

## Referências

MANIFEST NN-001/002/003/004/005/008/009/010/011/014/015/016/017/018/019/020/021/022/023;
ADR-0006/0011/0015/0022; DEC-0027; AGENTS §3;
[SPEC proposta](../proposals/math-studio/SPEC.md),
[roadmap](../proposals/math-studio/ROADMAP.md),
[auditoria/plano](../proposals/math-studio/IMPLEMENTATION.md).
