# ADR-0017 — Agent Runtime, Execution Runtime e organizações de agentes: fronteiras e sequência

## Status

Aceito — o proprietário decidiu em **DEC-0026**: alternativa **C** (R1 dentro de um Product novo de IA, com fronteira de extração desde o primeiro dia). A escolha inicial A foi revogada por ele no mesmo dia, por engano ([ADD-0014](../governance/addenda/ADD-0014-dec-0026-revogada-a-escolhida-c.md); histórico em [`responses/DEC-0026.md`](../governance/responses/DEC-0026.md)). **Aceitar este ADR não implementa nada:** nenhum código, diretório, componente, schema ou permissão existe; o R1 só começa depois da **DEC-0027** (id e nome do Product) e das aprovações críticas que ele exigir (entrada em `ecosystem.json`).

## Contexto

- **FATO:** o MANIFEST já classifica o **Agent Runtime como Service** (§6.5) e o **Agent Workspace como Workspace** (§6.7), diz que o Workspace "não é IA do Urbe nem do Lunet" (§20) e prevê o Agent Runtime como "uma das primeiras infraestruturas transversais" com uma estratégia de convergência (§39: mapear, contratar, preservar comportamento, extrair semanticamente, adaptar, testar cada consumidor). Fase 6 do ROADMAP: "Agent Workspace compartilhado".
- **FATO:** existem duas sementes com vocabulário próprio — a IA do Urbe (`apps/urbe/src/ai/`, JavaScript, **congelado** por DEC-0025-C; porte para C# previsto em UC-21, marco M3) e o spec do Agentic Workspace do Lunet2D (SPEC §16, Fase 8 do roadmap dele).
- **FATO:** o proprietário enviou a diretiva [ADD-0013](../governance/addenda/ADD-0013-agent-runtime-execution-runtime-e-organizacoes.md): uma infraestrutura universal de IA e agentes (Agent Runtime, Agent Workspace, Product de IA, organizações, execução persistente, deploy, orçamento), a ser construída **incrementalmente**, sem destruir a arquitetura atual, com a primeira entrega sendo um plano executável.
- **FATO:** o fluxo multiagente que desenvolve o Ecosystem (ADR-0014, ADR-0015, ADD-0012) já implementa, com GitHub, quase todos os conceitos de organização, verificação e escalonamento que o ADD pede de forma geral.
- Plano completo, auditoria e contratos propostos: [`docs/architecture/agent-runtime.md`](../architecture/agent-runtime.md).

## Problema

Sem fronteiras explícitas, três riscos: (1) a IA nascer **três vezes** (Urbe em C#, Lunet2D, Product de IA) com runtimes incompatíveis — exatamente o que MANIFEST §39 manda impedir; (2) "inteligência", "execução" e "infraestrutura externa" se misturarem num componente que ninguém consegue testar nem limitar; (3) a pressa pela visão final atropelar ADR-0011/NN-022 e as fases do ROADMAP. É preciso decidir **onde passam as fronteiras** e **em que ordem** se constrói, sem implementar nada antes.

## Opções

Fronteiras (§Decisão, D1–D6) e sequenciamento do primeiro slice:

- **A — Ordem do ROADMAP:** o R1 só começa na Fase 6; nada é adiantado. Menor risco de abstração prematura; mantém três linhas de IA nascendo sem contrato comum até lá.
- **B — Service compartilhado já:** `services/agent-runtime/` (C#, em processo) em paralelo às Fases 3–5, com consumidores declarados. Atende ao desejo de infraestrutura universal; **declara consumidores que ainda não existem** (tensão com NN-022 e ADR-0011).
- **C — Dentro de um Product novo de IA, com fronteira de extração desde o dia 1** (`Core` sem Product/UI/provedor, testes de arquitetura; precedente `Hub.Core`); promoção a Service por Extraction Review quando houver 2º consumidor real. Respeita ADR-0011 e dá ao R1 um consumidor real imediato (o próprio Product). Exige criar um Product (id estável, NN-019).
- **D — Não aceitar este ADR agora.**

## Decisão

> D1–D9 foram aceitas pela DEC-0026 (alternativa C). A alternativa C só define **quando e onde nasce o R1**; o desenho de D1–D9 vale em qualquer caso.

**D1 — Agent Runtime** é um **Service** (MANIFEST §6.5): decide e coordena (modelos, contexto, memória, ferramentas, permissões, planejamento, verificação, eventos). Núcleo **novo, em C#**, **sem dependências**; contratos em JSON Schema. Nunca abre processo, socket ou arquivo por conta própria: só chama **capabilities**. Provedores de modelo são **adapters** atrás de uma porta (`IModelProvider`), fora do Core.

**D2 — Execution Runtime** é uma **fronteira separada**: roda processos, jobs, workers, serviços e deploys, com isolamento, segredos, rede, armazenamento e quotas. **Não conhece agentes nem modelos**; recebe `JobSpec` autorizados de qualquer solicitante. Distribuição/Launcher (Fase 4) **não** é Execution Runtime.

**D3 — Agent Workspace** é um **Workspace** (superfície hospedável por qualquer Product, recebendo Context e capabilities do Host). O **Product de IA** ("Ecosystem AI", nome provisório; o **id** é decisão futura) é **consumidor privilegiado** do Runtime e só isso. O **Hub** continua control plane read-only e **observa** agentes, jobs, workers e custos por capability; Hub ≠ Runtime ≠ Product de IA (NN-003, NN-023).

**D4 — Organização, Equipe e Papel são dados**, nunca departamentos no código; equipe **efêmera** ou organização **persistente**; planejador prefere a **menor equipe suficiente** e registra o custo de coordenação.

**D5 — Orçamento, segredos e eventos como conceitos de primeira classe:** **uma** autoridade de ledger por organização (NN-001), consumida por ambos os runtimes; segredos por **referência**, nunca por valor; log de eventos auditável **sem campo de raciocínio livre**. `Completed` exige verificação aprovada (NN-018).

**D6 — Escalonamento** generaliza o modelo de `integration-policy.json`: o sistema julga o que já sabe julgar; direção nova ou efeito crítico (gasto, operação externa, segredo, perda irreversível de dados, e as classes já existentes) vai ao proprietário.

**D7 — Sequência (R1 conforme a alternativa C da DEC-0026: nasce dentro do Product de IA, com o `Core` sem referência a Product, UI ou provedor, provado por testes de arquitetura, e promoção a Service por Extraction Review quando houver um segundo consumidor real; o id e o nome do Product são a DEC-0027):** slices R1 (runtime mínimo, em processo) → R2 (duas equipes, workspace isolado, change set) → R3 (Workspace hospedado em Product existente, anfitrião por critério, §11.1 do plano) → R4 (Product de IA) → R5 (Remote Worker, depois da Fase 5) → R6 (deploy). **Nenhum slice é implementado antes da DEC-0026** nem antes das aprovações críticas que ele exigir. Fases: R1–R4 na **Fase 6**; organização persistente e Remote Workers na **Fase 8 (proposta)**; Execution Runtime de serviços, deploy, gasto real e segredos de plataforma na **Fase 9 (proposta)**.

**D8 — Dogfooding sem recursão:** o Runtime nunca ganha escrita na `main`; produz `Change Set` que passa pelo **mesmo integrador** e pelas **mesmas zonas críticas**. Git/GitHub são **adapters**, nunca parte do Core (NN-015).

**D9 — Um ADR por contrato e uma Extraction Review por extração.** Este ADR não cria contrato algum.

### NN-022, avaliação prévia (a Extraction Review refaz com evidência)

1. **Complexidade removida:** três implementações paralelas de provedores + loop agêntico + permissões (Urbe C#, Lunet2D, Product de IA) viram uma.
2. **Consumidores reais:** *previstos* — Product de IA, Urbe C# (UC-21), Lunet2D (Fase 8). Nenhum existe hoje; por isso a promoção, nas alternativas B/C, só se consuma com 2º consumidor real e teste de contrato.
3. **Contrato:** capabilities (NN-006) + contratos do plano §10, um ADR por contrato.
4. **Custo novo:** versionamento dos contratos, manutenção do Core, testes de contrato entre provedores.
5. **Saldo:** positivo **se** os consumidores adotarem o Runtime; se UC-21 e o Lunet2D seguirem caminhos próprios, o saldo é negativo e o Core **continua local ao Product de IA** (resultado válido, ADR-0011).

## Consequências

- Boundaries explícitos para os próximos ADRs; o vocabulário (Service, Workspace, Product, Capability, Context, permissões) é **reutilizado**, não substituído.
- O ROADMAP ganha itens P6-1..P6-5 e um bloco de fases propostas (8–9), sem gate nem ID até este ADR ser aceito.
- **Decisões críticas futuras** ficam previstas, não abertas (plano §13): UC-21 × Runtime compartilhado, id/nome do Product, política de orçamento, segredos por plataforma, confiança do Worker, primeiro provider de deploy, retenção em organizações persistentes. Cada nova permissão (`model.invoke`, `process.run`, `secret.use`, `budget.spend`, `deploy.production`…) altera `permissions.json` (zona crítica `security`) no slice que a exigir.
- Pela alternativa **C** (decidida), nasce um Product novo no R1: entrada em `ecosystem.json` (regra de arquitetura `manifest-structure`, crítica), `apps/<id>`, versão e CI do Product. Um componente compartilhado (`CHK-SHARED-DECLARATION`) só nasce na promoção, por Extraction Review.
- **Riscos assumidos:** contrato prematuro (mitigado por duas implementações de provedor antes de congelar) e deriva entre IAs (mitigada pela decisão futura sobre UC-21).

## Alternativas rejeitadas

- **Um "Agent Platform" monolítico** (runtime + execução + deploy + product no mesmo componente): viola NN-002/NN-020 e impede limitar custo, segredos e superfície de ataque por fronteira.
- **Colocar o runtime no Hub:** viola NN-003/NN-023 (o Hub nunca é requisito) e confunde observar com executar.
- **Copiar o fluxo Git/PR/Actions para dentro do Runtime:** acopla o Core a um fornecedor e a NN-015; extraem-se os conceitos (§9 do plano).
- **Departamentos fixos no Core ("empresa de agentes"):** a empresa é uma configuração possível, não o modelo.
- **Esperar a Fase 6 sem registrar nada:** deixa o Urbe C# (UC-21) e o Lunet2D decidirem sozinhos seus runtimes antes de existir contrato comum.

## Referências

MANIFEST §6.5, §6.7, §20, §22.4, §23, §30.2–30.3, §39, §46, §48; NN-001, NN-003, NN-006, NN-011, NN-015, NN-016, NN-018, NN-020, NN-022, NN-023; [ADD-0013](../governance/addenda/ADD-0013-agent-runtime-execution-runtime-e-organizacoes.md); ADR-0011, ADR-0012, ADR-0013, ADR-0014, ADR-0015, ADR-0016; DEC-0024-B, DEC-0025-C; **DEC-0026**; [`docs/architecture/agent-runtime.md`](../architecture/agent-runtime.md); `apps/urbe/src/ai/`; `apps/lunet2d/docs/SPEC.md` §16; `apps/urbe/docs/csharp/ROADMAP.md` (UC-21).
