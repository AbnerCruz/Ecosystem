# ADR-0010 — Migração do Urbe para C#

- Status: Accepted (decisão do proprietário, DEC-0024-B, 2026-10-02; Ecosystem ADR-0016)
- Data: 2026-10-02
- Requisitos: REQ-007, REQ-035, REQ-038 (dados), REQ-064 (testes de comportamento)
- Decisores: Abner P. S. Cruz (proprietário)
- Referências qualificadas: **Ecosystem ADR-0016** (decisão e consequências no Ecosystem), **Ecosystem DEC-0024** (registro). Nunca citar só «ADR-0016».

## Contexto

O proprietário decidiu, pelo portal do Ecosystem, a **reescrita completa do Urbe em C#**, trocando o produto só com paridade total (Ecosystem DEC-0024, alternativa B). Este ADR registra o efeito dentro do produto; a decisão e as consequências no Ecosystem estão no Ecosystem ADR-0016.

## Decisão

O Urbe passa a ter um programa de migração para C# regido por [`docs/csharp/`](../../csharp/README.md) (roadmap `UC-n`, fases M0–M5, gates G-C0–G-C5). O produto distribuído continua sendo o Urbe em JavaScript até o corte.

- **Ficam valendo** para o produto atual: ADR-0002 (plugins full-trust), ADR-0003 (licença), ADR-0004 (compatibilidade e proteção forward — **e é o contrato de dados do cliente novo**), ADR-0005 (release por tag), ADR-0006, ADR-0007, ADR-0008, ADR-0009.
- **ADR-0001 (runtime sem build e registro de módulos)** continua valendo para o runtime JavaScript enquanto ele for o produto; **não rege** o cliente C#.
- Pilha de UI/hosts, estratégia de transição, modelo de plugins em C# e o corte **não** estão decididos aqui; cada um tem item (UC-5, UC-6, UC-20, UC-31) e decisão própria.
- O programa de refatoração da 2.0 em JavaScript durante a migração foi decidido no Ecosystem **DEC-0025-C**: congelamento total do JavaScript, salvo bug crítico; os itens do ROADMAP 2.0 ficam adiados, nunca removidos (nota no topo de `docs/v2/ROADMAP.md`).

## Consequências

- O formato dos dados do usuário não muda; a paridade é provada contra fixtures, goldens e E2E existentes (UC-2).
- Mudança em `src/persistence/**` do JS e todo código de persistência do cliente novo seguem como zona crítica (autorização do proprietário no PR).
- Escopo «fora da 2.0: reescrita geral» (SPEC §13) permanece verdadeiro **para o programa 2.0**; a reescrita é um programa separado, com roadmap próprio.

## Alternativas consideradas

As quatro do Ecosystem ADR-0016 (A incremental pelo núcleo, B reescrita completa, C por superfície, D manter JavaScript). **B** foi a escolhida pelo proprietário.
