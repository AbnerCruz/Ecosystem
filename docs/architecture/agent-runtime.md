# Agent Runtime, Execution Runtime e organizações de agentes — plano arquitetural

> **Autoridade:** documento de arquitetura (nível "ARCHITECTURE e contracts", MANIFEST §24), subordinado ao `MANIFEST.md` (§20 e §39 já tratam do Agent Runtime), ao [ADD-0013](../governance/addenda/ADD-0013-agent-runtime-execution-runtime-e-organizacoes.md) (a diretiva do proprietário, texto integral) e ao [ADR-0017](../adr/0017-agent-runtime-execution-runtime-e-organizacoes.md).
> **Status: plano de arquitetura.** O aceite do ADR-0017 e a sequência do primeiro slice são do ADR e da **DEC-0026** ([`decisions.json`](../governance/decisions.json)), as autoridades (não copiadas aqui, ADR-0010); o que vem depois está em §13. Contratos e fases abaixo são **propostas** até o ADR de cada contrato ser aceito: nada aqui cria código, diretório, componente em `ecosystem.json`, schema, permissão ou fase vigente (NN-011, NN-020, NN-022). Estado de andamento: ROADMAP (itens P6-1..P6-5) e Issues.
> Convenção do MANIFEST §53: **FATO** = verificado no repositório nesta tarefa; **INFERÊNCIA** = dedução; **PROPOSTA** = sugestão sujeita a decisão; **DECISÃO** = já registrada pelo proprietário.

## 1. Resumo

O ADD-0013 pede uma infraestrutura **universal** de IA e agentes: o proprietário declara objetivo, restrições, orçamento e permissões; agentes autorizados planejam, executam, verificam e escalam só o que for crítico. Este plano responde à primeira entrega pedida (§31 do ADD): auditoria, conflitos, desenho, contratos, fatiamento e ROADMAP — **sem implementar o monstro de uma vez**.

Conclusões principais:

1. **A visão já tem lugar na arquitetura vigente.** O MANIFEST classifica o **Agent Runtime como Service** (§6.5) e o **Agent Workspace como Workspace** (§6.7), diz que o Workspace "não é IA do Urbe nem do Lunet" (§20) e prevê o Agent Runtime como "uma das primeiras infraestruturas transversais" (§39). Não é preciso inventar uma segunda arquitetura; é preciso **refinar** esta e acrescentar três coisas que ela ainda não nomeia: **Execution Runtime**, **Organização** e **Orçamento** como conceitos de primeira classe.
2. **Cinco fronteiras** separam o que o ADD pede para não misturar (§4): Agent Runtime × Execution Runtime × Agent Workspace × Product de IA × Hub.
3. **O que existe hoje é matéria-prima, não ponto de partida:** a IA do Urbe (`apps/urbe/src/ai/`, 1 168 linhas, JS, congelado por DEC-0025-C) e o spec do Lunet (§16, Fase 8) descrevem o mesmo tipo de sistema com vocabulário próprio; o fluxo multiagente que desenvolve o Ecosystem (branch, PR, integrador, handoff) é a evidência prática de quase todos os conceitos de organização e verificação (§9).
4. **O primeiro slice proposto é bom, com cinco ajustes** (§11): a avaliação crítica pedida está em §11.2.
5. **A decisão crítica inicial foi a DEC-0026:** *quando e onde* nasce o primeiro slice (registro em `decisions.json`). As demais são **previstas e abertas no momento certo** (§13), para não transformar o proprietário em fila de perguntas (ADD-0012).

## 2. Auditoria: o que já existe (FATOS)

| Conceito do ADD | O que existe hoje | Onde | Observação |
|---|---|---|---|
| Agent Runtime (Service) | só o **conceito** | MANIFEST §6.5, §20, §39; `ARCHITECTURE.md` §1 | nenhum código compartilhado; ADR-0011 manda nascer local e promover por evidência |
| Agent Workspace (Workspace) | só o conceito; duas implementações-semente fora dele | MANIFEST §20; `candidates.md` U-05 | "Workspace: Agent Workspace" |
| IA do Urbe | provedores normalizados (OpenRouter, Anthropic, OpenAI, Gemini, Ollama, compatível), loop agêntico independente de modelo, 4 agentes (geral, pesquisador, escritor, organizador) com classes de acesso `read`/`write`/`destructive` e política de aprovação, memória do usuário, plano (`update_plan`), desfazer em bloco, instrução de segurança contra injeção em conteúdo de notas | `apps/urbe/src/ai/{agent,providers,store,tools,ui}.js`; teste `apps/urbe/tests/ai-agent.mjs` | **congelado** (DEC-0025-C, salvo bug crítico); porte para C# previsto em **UC-21** ("IA: providers e agente"), marco M3 |
| Agentic Workspace do Lunet | **spec**: Model Gateway, Provider Adapters, Capability Discovery, Context Engine, Agent Runtime, Tool Registry, Permissões, Verificação, Observabilidade, Custo, MCP Bridge; loop Understand→…→Finish; "não concluir só por ter escrito arquivos"; chaves no Android Keystore | `apps/lunet2d/docs/SPEC.md` §16; `apps/lunet2d/ROADMAP.md` Fase 8 | Fase 8 do Lunet; IA opcional, "nada depende dela" |
| Context | contrato `schemas/context.schema.json`: caminho `ecosystem → product → project → workspace → tool` | ADR-0012; `product-model.md` | **reutilizar**; sem IPC nem Host API (Fase 5) |
| Capability, provider/consumer, Registry | contrato + Registry local (`CHK-REGISTRY`); **nenhuma capability real** ainda | ADR-0012; `docs/contracts/` | tools do agente **são** capabilities (NN-006) |
| Permissões | catálogo com `fs.read`, `fs.write`, `network.access`, `execute.code`, `agent.act` ("nunca herdado por padrão"), `ui.display`; deny-by-default; enforcement em runtime só na Fase 5 | `docs/contracts/permissions.json`; ADR-0012 | faltam permissões de modelo, processo, segredo, gasto, deploy (§10) |
| Observabilidade de agente | handoff, Issues `state:`, comentários e status do integrador, trailers `Integration-*` | NN-008; `communication.md`; ADR-0015 | vale para agentes **de desenvolvimento**; o runtime precisa do equivalente próprio (event log) |
| Autorização e escalonamento | `integration-policy.json`: classes críticas (constituição, control plane, segurança, dados, distribuição, compatibilidade, arquitetura, estratégia) + label do proprietário conferida pelo ator | ADD-0012; ADR-0015 | é uma **política de escalonamento já em produção**; generalizar, não reinventar |
| Local-first / promoção | feature nasce no Product; segundo consumidor real → Extraction Review → NN-022 → ADR → contrato | ADR-0011; `local-first.md` | regra que o plano tem de respeitar (conflito C2) |
| Hub | control plane **read-only** (Fase 3); `Hub.Core` é biblioteca independente de UI | ADR-0013; `apps/hub` | precedente de organização de projetos para o primeiro slice |
| Execução remota / deploy / orçamento / segredos / organizações | **nada** | — | só a regra: credenciais "nunca em commits, logs, handoffs" (MANIFEST §30.2); agentes não herdam permissões (§30.3) |
| Hosts 24/7 | **nada**; Android suspende processos (premissa do ADD §12) | — | Fase 5 (Capability Runtime) é pré-requisito do que cruza processos |

## 3. Conflitos entre a visão e a arquitetura vigente, e como se resolvem

| # | Conflito | Resolução proposta | Quem decide |
|---|---|---|---|
| C1 | ADD quer o runtime "nativo do Ecosystem", sem dono; o MANIFEST já o classifica como Service | **Sem conflito**: Agent Runtime = Service; Agent Workspace = Workspace; Product de IA = consumidor. | — |
| C2 | **ADR-0011** (nasce no Product; extrai com 2º consumidor real) × "infraestrutura universal desde já" | Nascer **com fronteira de extração desde o dia 1**: biblioteca `Core` sem referência a Product/UI/provedor, testada por regras de arquitetura (precedente `Hub.Core`). A *casa* inicial (Product novo × Service direto × esperar a Fase 6) foi decidida na **DEC-0026** (ver o registro). | proprietário |
| C3 | Ordem das fases: Agent Runtime está na **Fase 6**, depois da Fase 5 (Capability Runtime) | Slice 1 é **em processo** (biblioteca): depende só dos contratos da Fase 2 (prontos), não de IPC/Host API. Antecipá-lo é sequenciamento, não pular fase — mas é decisão de direção (**DEC-0026**). Tudo que cruza processos (Workers, Tools hospedadas) continua **depois** da Fase 5. | proprietário |
| C4 | **NN-022**: nenhum componente compartilhado sem consumidor real e saldo positivo | Consumidores reais **previstos** (não ainda existentes): Product de IA, Urbe C# (UC-21), Lunet2D (Fase 8). Saldo: 3 implementações paralelas de provedores+loop viram 1. Evidência de consumidor real: teste de contrato com 2 implementações de provedor e o 1º consumidor concreto (§11). A extração só ocorre pela Extraction Review. | ADR (agente) com a DEC-0026 |
| C5 | **DEC-0024-B / DEC-0025-C**: Urbe reescrito em C#; JS congelado | A IA do Urbe JS **não é tocada**. O UC-21 passa a ter uma pergunta própria: portar a IA isoladamente ou **consumir o Agent Runtime compartilhado**? Muda o escopo de um programa que o proprietário aprovou ⇒ **decisão futura (§13), aberta depois da DEC-0026**. A paridade continua medida pelo oráculo atual (`tests/ai-agent.mjs`, UC-2). | proprietário (futura) |
| C6 | **NN-003 / NN-023**: nenhum Product depende do Hub nem de sua distribuição | O Agent Runtime **não** é requisito de nenhum Product: IA é capacidade aditiva ("IA é opcional; nada depende dela", Lunet Fase 8). Sem runtime, Urbe e Lunet2D funcionam no domínio essencial. O Hub só **observa** (§4). | — |
| C7 | **NN-015**: GitHub não é IPC; o integrador atual usa GitHub | O runtime usa **conceitos** (workspace isolado, change set, pipeline de validação), com o Git/GitHub como *adapter* fora do Core (§9). | — |
| C8 | **NN-016 / NN-006**: permissões e capabilities precedem autonomia | Ferramentas do agente = capabilities declaradas; o conjunto disponível é uma **interseção** (§5.5); deny-by-default. Novas permissões mudam `permissions.json` (zona crítica `security`), uma por slice, com ADR. | proprietário (crítico, por slice) |
| C9 | **MANIFEST §11.1** lista `platform/`, `workspaces/agent/` mas diz que a estrutura "não autoriza diretórios vazios" e ADR-0004/NN-004 proíbem `shared` sem dono | Diretórios só nascem **com código e declaração** em `ecosystem.json` (`responsibility`, `contract`, `consumers`, `owners`, `compatibility`, `extractionReason`; `CHK-SHARED-DECLARATION`). Local sugerido na promoção: `services/agent-runtime/` e `workspaces/agent/` (refinável, §11.1). | ADR de promoção |
| C10 | **NN-005**: C# é o padrão, mas não autoriza reescrita | O Core é **novo** (nada reescrito): C#. Contratos são JSON Schema (independentes de linguagem, MANIFEST §4.3), o que permite ao Urbe JS interoperar por adapter enquanto existir. | — |

## 4. Fronteiras (ADR-0017)

```text
                         HUMANO
                  intenção · restrições · orçamento · decisões críticas
                            │
                       ┌────▼────┐
                       │  GOAL   │  Goal + Constraints + Budget + Permissions + Success Criteria
                       └────┬────┘
              ┌─────────────▼──────────────┐
              │ ORQUESTRAÇÃO (Organization) │  plano · equipe · tarefas · dependências · escalonamento
              └─────────────┬──────────────┘
         ┌──────────────────▼───────────────────┐
         │             AGENT RUNTIME              │  Service — decide e coordena; não executa nada no mundo
         │ modelos · contexto · memória · tools   │
         │ permissões · verificação · eventos     │
         └───────┬───────────────────────┬───────┘
                 │ capabilities          │ capabilities
        ┌────────▼────────┐     ┌────────▼─────────┐
        │ EXECUTION RUNTIME│     │  Tools de domínio │  files.*, notes.*, code.*, tests.run …
        │ jobs · workers   │     │  (providers: Urbe, │  (providers são Products/Tools,
        │ serviços · deploy│     │   Lunet, …)        │   nunca importados pelo runtime)
        └────────┬────────┘     └──────────────────┘
                 ▼
          MUNDO EXECUTÁVEL (local · workers · servidores · provedores de nuvem)

   Agent Workspace (Workspace)  = superfície hospedável que liga um Context + capabilities ao Agent Runtime
   Product de IA ("Ecosystem AI") = Product que consome o mesmo Runtime e o mesmo Workspace
   Hub = control plane: observa e administra (decisões pendentes, custos, jobs, workers); NÃO é o runtime nem o Product de IA
```

| Fronteira | Regra | Fiscalização (§14) |
|---|---|---|
| **Agent Runtime × Execution Runtime** | O Agent Runtime **decide**: raciocina, planeja, escolhe ferramentas, verifica. O Execution Runtime **faz**: roda processos, jobs, workers, serviços, deploys, com isolamento, segredos, rede, armazenamento e quotas. O Agent Runtime **nunca** abre processo, socket ou arquivo por conta própria: só chama **capabilities**. O Execution Runtime **não conhece agentes nem modelos**: recebe um `JobSpec` autorizado de qualquer solicitante (agente, humano, CI, Hub). | `AgentRuntime.Core` sem `System.Diagnostics.Process`, `System.Net`, `System.IO` de produção, provedores ou SDKs; testes de arquitetura |
| **Runtime × Product** | O Runtime não importa Product algum; Products o consomem por contrato. Um Product não duplica o Runtime. | `CHK-BOUNDARIES`, `CHK-ARCH-REFS` (já existentes) + teste de arquitetura do Core |
| **Runtime × Agent Workspace** | O Workspace é **superfície** (UI/orquestração de sessão) hospedável por qualquer Product; o Runtime não tem UI. O Workspace recebe Context e capabilities do Host. | contrato de Host (Fase 5) + teste de Workspace com Host de teste |
| **Product de IA × Runtime** | O Product é um consumidor privilegiado e **só isso**: chat, projetos, agentes, equipes, tarefas, artefatos, memórias, execuções, custos, histórico são **experiência**; a inteligência está no Runtime. | teste: o Product não contém loop agêntico nem cliente de provedor |
| **Hub × IA** | O Hub continua control plane **read-only** (Fase 3). Pode mostrar agentes ativos, jobs, workers, custos, decisões e alertas **lidos por capability**. Hub ≠ Agent Runtime ≠ Product de IA. Nenhum Product depende do Hub (NN-003, NN-023). | `CHK-BOUNDARIES` |

**Execution Runtime e Distribuição/Launcher (Fase 4)** são coisas diferentes: instalar e atualizar Products não é executar jobs de agentes. Compartilham no máximo capabilities de baixo nível, por contrato.

## 5. Agent Runtime: conceitos e relações

### 5.1 Conceitos (nomes provisórios; contrato nasce por ADR, §10)

```text
Agent · AgentIdentity · ModelProvider · Model · Context · Memory · Tool(=Capability) · Permission
Task · Plan · Goal · Run · Step · Artifact · Verification · Approval · Budget · Usage · Event · Handoff(Work Record)
— multiagente: Organization · Team · Role · Delegation · TaskGraph · Dependency · Communication · Review · Escalation
```

Todos **genéricos e não acoplados ao GitHub**. `IModelProvider` é uma porta; implementações concretas (Anthropic, OpenAI, OpenRouter, modelo local) vivem **fora** do Core como adapters substituíveis. O valor está em `modelo + contexto + ferramentas + memória + planejamento + execução + verificação + coordenação + permissões`, não no modelo.

### 5.2 Relações pedidas (§46 do ADD)

| Relação | Definição proposta |
|---|---|
| **Tool × Capability** | Tool de agente **é** uma Capability (contrato versionado, NN-006), com permissões exigidas. Não existe "tool de IA" paralela: humano, plugin e agente usam as mesmas operações (MANIFEST §21). A Tool API interna do Lunet (§16) e as `tools.js` do Urbe são as sementes. |
| **Agent × Context** | Todo `Run` tem um **Context** (caminho do contrato existente) fixado na criação. Acesso fora do escopo é negado. Context hierárquico: o agente do Urbe vê `ecosystem → urbe → <vault> → editor → <nota>`; o do Lunet, `… → lunet2d → <jogo> → editor`. Mesma infraestrutura; muda contexto, capabilities e permissões. |
| **Agent × Permission** | O agente só **age** com `agent.act` explícito (nunca herdado) e **só** com as capabilities do §5.5. Deny-by-default. |
| **Agent × ModelProvider** | Um agente referencia um `ModelProfile` (provedor + modelo + capacidades: texto, visão, tools, saída estruturada, streaming, janela de contexto), resolvido pelo Registry de provedores. Provedor desconhecido **falha**. Trocar de provedor não muda o agente. |
| **Goal × Plan** | `Goal` = objetivo + restrições + orçamento + permissões + critérios de sucesso (o usuário fornece isto, não microtarefas). `Plan` = proposta versionada de `TaskGraph` derivada do Goal; replanejar cria nova versão, nunca sobrescreve. |
| **Task × Run** | `Task` = unidade de trabalho com critérios de aceite verificáveis. `Run` = **uma tentativa** de executar uma Task (agente, contexto, orçamento, eventos). Uma Task pode ter vários Runs (retry, outro agente, outro método). |
| **Budget × Execution** | Toda operação que gasta (modelo, job, infraestrutura) passa por um **ledger de orçamento**: pede reserva, executa, concilia o uso. Sem saldo ⇒ `Blocked(budget)`; nunca ultrapassa em silêncio. Há **uma** autoridade de ledger por organização (NN-001); Agent Runtime e Execution Runtime a consomem. |
| **Done** | `output ≠ success`: `Task` só vai a `Completed` com `VerificationResult(passed)` (evidência verificável); o tipo/estado **impede** a transição sem ela. Alinhado a NN-018. |

### 5.3 Estados (esboço)

```text
Task:  Planned → Ready → Running → Verifying → Completed
                           │            └────→ Rework → Running           (limite de tentativas)
                           └→ Blocked(budget | escalation | dependency | provider | no-progress)
                           └→ Cancelled | Failed
Run:   Created → Running → (Succeeded | Failed | Cancelled | Blocked)      resumível pelo event log
```

### 5.4 Memória (sete tipos, sem confundir)

`conversation` · `project` · `agent` · `organization` · `artifact history` · `canonical knowledge` · `runtime state`. Regras: **conhecimento canônico tem fonte e autoridade identificáveis** (referência ao documento/contrato, nunca cópia que "vira verdade"); memória **nunca substitui estado real** (NN-001); nada de "vetor gigante" como autoridade; cada registro tem escopo (Context), origem e validade. A "memória" do Urbe (fatos pedidos pelo usuário) é o caso `agent`/`project` mais simples.

### 5.5 Ferramentas disponíveis (interseção)

```text
ferramentas do agente = capabilities do Host  ∩  permissões da organização  ∩  permissões do projeto  ∩  permissões do agente
```

Vazio por padrão onde há risco relevante (deny-by-default). A interseção é uma função pura, testável (§14).

## 6. Organizações multiagente

- **Composição, não departamentos.** O núcleo **não** codifica papéis fixos. Modela `Role` (responsabilidades + conjunto de capabilities + política de revisão), `Agent` (identidade + modelo + role), `Team` (agentes + coordenação) e `Organization` — todos **dados**. "Escritor/editor/ilustrador" ou "arquiteto/parsing/testes/revisor" são configurações geradas pela intenção.
- **Duas formas:** **equipe efêmera** (Goal → Team → trabalho → arquivada) e **organização persistente** (goals, agents, teams, projects, budgets, history acumulados). A metáfora de empresa é *uma* configuração possível, não o modelo.
- **Menor equipe suficiente.** Cada agente extra custa comunicação, contexto, coordenação, tokens, latência e conflitos. O planejador **estima o custo de coordenação** e prefere: tarefa simples → 1 agente; média → implementador + revisão; complexa → equipe especializada. "Mais agentes = melhor" é um anti-padrão explícito; o custo de coordenação é uma métrica registrada (§8.5).
- **Comunicação durável e sem raciocínio privado** (MANIFEST §23): mensagens estruturadas vinculadas a task e evidência — o mesmo princípio do handoff.

## 7. Execution Runtime, Workers, 24/7 e Deployment

### 7.1 Responsabilidades

```text
Execution Runtime: Processes · Jobs · Workers · Services · Secrets (uso) · Networking · Storage · Deployment · Observability · Budgets (consumo) · Authorization
```

**Boundary:** ver §4. Um `JobSpec` carrega: capability, entradas, permissões concedidas, orçamento máximo, timeout, chave de idempotência, política de cancelamento. O Execution Runtime valida, isola, executa, emite eventos e reporta; **aceita cancelamento**.

### 7.2 Hosts de execução e trabalho persistente 24/7

`Browser Host · Desktop Host · Local Worker · Home Server · VPS Worker · Cloud Worker · Managed Worker`. Premissa: Android e navegadores suspendem processos ⇒ trabalho persistente exige **Host autorizado** fora do aparelho. O usuário emite a intenção, fecha o app, e a execução continua num Host que ele autorizou.

- **Local-first:** nada exige nuvem; nuvem é capacidade **adicional** (Lunet SPEC §17 já trata IA e cloud build como aditivos).
- **Sem busy loop.** O sistema é guiado por **eventos, tarefas, filas, timers, condições, dependências e novos objetivos**. Agente ocioso custa ≈ 0 (teste T-9). Reflexão sem progresso é detectada (`Blocked(no-progress)`).
- **Remote Worker** (só depois da fundação provada, §11): registra capabilities, recebe jobs autorizados, executa isolado, emite eventos, reporta, respeita orçamento e timeout, aceita cancelamento. Precisa de IPC/Host API (Fase 5) e de modelo de confiança do Worker (decisão futura, §13).

### 7.3 Deployment (abstrato, sem fornecedor)

Capabilities: `deploy.application`, `deploy.service`, `provision.runtime`, `provision.database`, `configure.domain`, `configure.environment`, `inspect.deployment`, `rollback.deployment`. Providers possíveis (nenhum assumido): Docker, SSH/VPS, Vercel, Cloudflare, AWS, Azure, GCP, Fly.io, Render, Railway. **Não implementar todos.** O primeiro provider é escolhido **no momento do slice** por simplicidade, isolamento, automação, rollback, custo, testabilidade e segurança; "Docker + SSH/VPS" é só candidato conceitual (**decisão futura**, §13).

## 8. Orçamento, segredos, observabilidade, falha e escalonamento

### 8.1 Orçamento (primeira classe)

`Budget` em escopos aninhados: organização · projeto · agente · tarefa · provider. Limites: total, diário, mensal, por operação, por provider. Um **política** (dado, não código) diz o que é automático e o que escala — por exemplo, "abaixo de R$ 5, automático; gasto recorrente novo, autorização; aumento de limite, autorização" são **exemplos**; **nenhum valor é fixado por este plano**. Ao estourar orçamento, indisponibilidade de provider, falha repetida ou ausência de progresso, o trabalho é **pausado, degradado ou encerrado** — nunca repetido indefinidamente. Dinheiro em unidades inteiras mínimas (centavos), nunca ponto flutuante.

### 8.2 Segredos

Segredo **nunca** em prompt, log, memória de agente, arquivo de projeto, commit, artefato ou contexto textual desnecessário (MANIFEST §30.2). O agente recebe **referência ou permissão de uso**, não o valor: `agent → permission: deploy.production → provider usa o segredo internamente`. Porta `ISecretStore` (capability `secret.use`) com implementação por plataforma (Android Keystore no Lunet; chaves só no dispositivo no Urbe). Teste de **canário**: um valor-sentinela nunca aparece em eventos, artefatos nem logs de provider (T-10). Estratégia de armazenamento por plataforma é **decisão futura** (§13).

### 8.3 Observabilidade e auditoria

Cada ação responde: **quem** fez · **por quê** (objetivo/tarefa) · com **quais ferramentas** · qual **modelo** · qual **custo** · qual **contexto** (referência + hash, não conteúdo) · qual **resultado** · qual **verificação** · **quem aprovou**. Registra decisões operacionais, ações, argumentos relevantes, entradas permitidas, saídas, eventos, custos e resultados. **Não** registra nem exige o raciocínio privado do modelo: o schema do evento **não tem campo de raciocínio livre** (T-12). Estende NN-008 (que hoje cobre agentes de desenvolvimento) aos agentes do runtime.

### 8.4 Loop de falha

`attempt → verificação falha → diagnóstico → tarefa corretiva → retry`, com limite: depois de **N falhas equivalentes** (equivalência por impressão digital do erro/resultado) ⇒ `Blocked` e escalada, em ordem: outro agente → outro método → revisão → humano, se necessário. Nunca "corrigir → falhar → corrigir → falhar" sem fim (T-8).

### 8.5 Escalonamento: a política que já existe, generalizada

O `integration-policy.json` (ADD-0012) já distingue **rotina** (sistema julga) de **crítico** (proprietário decide), por classes e por regras que **só endurecem**. O runtime reutiliza o **modelo** (classes de efeito, deny-by-default em zona crítica, autorização conferida por ator) com classes adicionais de efeito: **gasto** (recorrente novo, aumento de limite), **operação externa** (deploy em produção, pagamento, comunicação pública), **acesso a segredo**, **perda irreversível de dados**. Princípio do ADD (§10): *se o sistema já sabe julgar, julga; direção nova ou efeito crítico, o proprietário decide* — sem perguntar "posso criar arquivo?". Métrica: **taxa de trabalho concluído sem o proprietário** (o portal já projeta a análoga para integrações).

## 9. Dogfooding: o laboratório que já existe

O fluxo multiagente do Ecosystem é a prova prática dos conceitos. **Não se copia o mecanismo** (Git, PR, Actions); extrai-se o conceito universal:

| Hoje (desenvolvimento do Ecosystem) | Conceito universal | Adapter possível |
|---|---|---|
| branch do agente | **Isolated Workspace** (cópia/overlay do estado canônico) | diretório local; branch Git; contêiner |
| PR | **Change Set** (mudança proposta, com base) | diff de diretório; PR do GitHub |
| GitHub Actions / checker confiável | **Validation Pipeline** (verificadores do estado combinado) | scripts locais; Actions |
| `main` | **Canonical State** | pasta do projeto; `main` |
| `base_commit` | **Base Snapshot** | hash de conteúdo; SHA |
| integrador (fila serial, estado combinado, commit exato) | **Integrator** | porte do algoritmo, sem Git no Core |
| `integration-policy.json` | **Escalation Policy** (rotina × crítico) | o mesmo arquivo de política |
| label `integrar` conferida pelo ator | **Human Authorization** | portal; app; label |
| handoff | **Structured Work Record** | arquivo JSON; evento |
| Issue com `state:` | **Task state** | tarefa no event log |
| DEC no portal / ADD | **Owner Decision / Directive** | portal; app |
| trailers `Integration-*` | **Provenance** | metadados do evento |

**Trilha de dogfooding (incremental, cada degrau com evidência):**

- **D0 (hoje):** agentes externos desenvolvem o Ecosystem pelo fluxo atual. Nada muda.
- **D1:** o Agent Runtime executa, **localmente**, um agente determinístico sobre uma cópia do repositório, gera um `Change Set` e o valida com `Check.cs` — sem integrar nada.
- **D2:** o adapter Git/GitHub transforma o `Change Set` em PR; **o integrador atual é inalterado** e continua sendo quem integra. O estado canônico continua protegido por permissões, validação, política, orçamento e aprovação humana quando crítica.
- **D3:** só então avaliar generalizar o integrador (conceito) — fora de qualquer caminho crítico até haver evidência.

Isto **não** é recursão descontrolada: o runtime nunca ganha acesso de escrita à `main`; passa pelo mesmo integrador, com as mesmas zonas críticas.

## 10. Contratos propostos (esboço; nascem por ADR e schema, um por slice)

Todos JSON Schema (MANIFEST §4.3), IDs estáveis (NN-019), `schemaVersion`. **Nada disto é schema ainda.**

| Contrato | Conteúdo mínimo | Slice |
|---|---|---|
| `goal` | `goal`, `constraints[]`, `budget`, `permissions[]`, `successCriteria[]` | R2 |
| `plan` / `task` | `taskId`, `dependsOn[]`, `acceptance[]` (verificáveis), `state`, `budgetRef` | R1/R2 |
| `run` | `runId`, `taskId`, `agent`, `context` (contrato Context), `state`, `attempt` | R1 |
| `event` (log) | `eventId`, `runId`, `agent`, `reasonRef`, `tool`, `model`, `cost`, `contextRef+hash`, `result`, `verification`, `approvedBy`; **sem campo de raciocínio** | R1 |
| `model-profile` | `provider`, `model`, `capabilities` (text, vision, tools, structuredOutput, streaming, contextWindow) | R1 |
| `tool-binding` | capability id + versão, permissões exigidas, classe de risco (`read`/`write`/`destructive`, de `tools.js`) | R1 |
| `budget` / `usage` | escopo, limites (total/dia/mês/operação/provider), unidade mínima inteira, `reservation`, `settlement` | R1 (ledger com custo sintético) |
| `escalation-policy` | classes de efeito e ação (auto / autorização / negar) — generalização de `integration-policy.json` | R2 |
| `change-set` / `workspace` | base, mudanças, verificações, proveniência | R2 |
| `job-spec` / `worker` | capability, entradas, permissões, orçamento, timeout, idempotência; capabilities do Worker | R5 |
| `deploy.*` capabilities | contratos de capability (NN-006) | R6 |

**Permissões novas** (a propor no slice que as exigir, em `permissions.json`, zona crítica `security`): `model.invoke`, `process.run`, `secret.use`, `budget.spend`, `deploy.production`, entre outras. Existentes reutilizadas: `fs.read`, `fs.write`, `network.access`, `execute.code`, `agent.act`.

## 11. Fatiamento e vertical slices

Cada slice é **pequeno, verificável e provado antes do próximo**. Nenhum é implementado por este PR.

### 11.1 Sequência

| Slice | Prova | Fica fora |
|---|---|---|
| **R1 — Runtime mínimo** | Core + provedor falso/gravado + Agent + Task + Context + Tool + Permission + Run + Event log + Verifier + Budget (custo sintético) + cancelamento + retomada, tudo local e determinístico | rede, nuvem, deploy, segredo real, provedor comercial |
| **R2 — Duas equipes, uma integração** | 2 agentes, tarefas dependentes, **workspace isolado**, produção, **revisão**, integração por **Change Set** + política de escalonamento; algoritmo do integrador sem Git no Core | Git/GitHub no Core (só adapter), orgs persistentes |
| **R3 — Agent Workspace hospedado** | o **mesmo** Workspace opera dentro de um Product existente com contexto e capabilities daquele Product | Product de IA |
| **R4 — Product de IA** | o Product dedicado consumindo **exatamente** o mesmo Runtime e o mesmo Workspace (prova: Runtime ≠ Product) | execução remota |
| **R5 — Remote Worker** | só após R1–R4: Worker que registra capabilities, recebe jobs, executa isolado, respeita orçamento/timeout/cancelamento | deploy |
| **R6 — Deploy** | provider escolhido por comparação, no momento; capabilities `deploy.*`; rollback | todos os outros providers |

**R3 não pode ser atribuído a Urbe ou Lunet hoje (FATO):** o Urbe JS está **congelado** (DEC-0025-C) e a IA em C# (UC-21) está no marco M3, depois do núcleo de dados (M1) e do domínio (M2); o Lunet2D só chega ao Agentic Workspace na **Fase 8** do próprio roadmap. Critério proposto para escolher o anfitrião quando chegar a hora (não arbitrário): (a) já tem Host/Shell que consegue hospedar o Workspace; (b) já expõe as capabilities que o contexto precisa; (c) o item correspondente do roadmap dele está no ponto de consumi-lo; (d) a escolha não exige mudar a direção aprovada do Product (se exigir, decisão do proprietário). Até lá, R3 é provado com um **Host de teste** (um Product simulado), para não esperar meses por evidência de contrato.

### 11.2 Avaliação crítica do slice proposto no ADD (§32)

O slice sugerido (Agent, Task, Context, Tool, Permission, Run, Event log, provedor falso, cenário "tarefa → Context → Tool segura → Artifact → verifier → completed") é **a escolha certa** — local, determinístico, sem nuvem, sem segredo. Cinco ajustes recomendados:

1. **Orçamento e cancelamento entram no R1**, não depois: reformar um runtime para respeitar orçamento/cancelamento é caro e foi o erro clássico. Com custo **sintético** no provedor falso, o ledger já é testável.
2. **`Completed` exige `VerificationResult`** na própria máquina de estados (não só convenção): é o que impede "LLM produziu texto = sucesso".
3. **Dois provedores, não um:** um `ScriptedModelProvider` e um `RecordedModelProvider` (replay de respostas gravadas), com a **mesma suíte de contrato**. É o que prova "model-agnostic" — um único falso prova só que existe uma porta.
4. **Log de eventos como fonte do estado** (append-only): `Run` **retomável** reconstruindo do log; é o que dá auditoria e resumibilidade juntas.
5. **Ferramentas = capabilities desde o início**, ligadas ao catálogo de permissões existente (`fs.read`/`fs.write`) e a um Context de verdade (contrato existente), com **sandbox de diretório**: a "Tool segura" do cenário vira `files.read`/`files.write` presos à raiz do Context.

### 11.3 Estrutura proposta do R1 (precedente `Hub.Core`)

```text
AgentRuntime.Core              domínio + portas (IModelProvider, IToolHost, ILedger, IEventLog, IVerifier, ISecretStore); zero dependências
AgentRuntime.Testing           ScriptedModelProvider, RecordedModelProvider, relógio falso, event log em memória
AgentRuntime.Tools.Files       files.read / files.write presos à raiz do Context (exemplo de Tool segura)
AgentRuntime.Tests             contrato, comportamento e arquitetura
```

Onde isto mora foi decidido na **DEC-0026** (ver o registro): dentro do Product novo de IA, cujo id (`ecosystem-ai`) e nome ("Ecosystem AI") foram fixados na **DEC-0027** (alternativa B); o diretório é `apps/ecosystem-ai`.

## 12. Fases no ROADMAP

| Capacidade | Fase | Observação |
|---|---|---|
| Plano, auditoria, contratos propostos | **Fase 6** (P6-1) | entregue; o aceite do ADR-0017 e a sequência são do ADR e da DEC-0026 |
| Agent Runtime (R1), organização efêmera (R2) | **Fase 6** (P6-2, P6-3) | R1 pode ser antecipado (DEC-0026); depende só dos contratos da Fase 2 |
| Agent Workspace hospedado (R3) | **Fase 6** (P6-4) | anfitrião por critério (§11.1) |
| Product de IA (R4) | **Fase 6** (P6-5) | prova Runtime ≠ Product |
| Hospedagem de Tools/Workers que cruzam processos, permissões em runtime | **Fase 5** | já existe: IPC, Host API, lifecycle |
| Organização persistente, Remote Workers (R5) | **Fase 8 — proposta** | depende da Fase 5 |
| Execution Runtime de serviços, Deploy (R6), gasto real, segredos de plataforma | **Fase 9 — proposta** | só com decisões críticas próprias (§13) |

As fases 8 e 9 são **propostas**: aparecem no ROADMAP como bloco não-vigente, sem gate nem IDs, até o ADR-0017 ser aceito (MANIFEST §46 define 0–7; novas fases exigem esse aceite). Nada é marcado como concluído sem evidência.

## 13. Decisões críticas e ADRs

**Já decidida:** **DEC-0026** — quando e onde nasce o R1 (ratifica o ADR-0017; ver o registro). **DEC-0027** — id e nome do Product de IA (alternativa B: `ecosystem-ai`, "Ecosystem AI").

**Previstas** (cada uma é aberta **no momento em que a anterior a torna necessária**, nunca antes):

| Decisão futura | Gatilho |
|---|---|
| DEC — UC-21 consome o Agent Runtime compartilhado? | depois da DEC-0026; antes de UC-21 |
| DEC — política de orçamento (valores, classes de gasto) | antes de qualquer capability que gaste dinheiro real |
| DEC — armazenamento de segredos por plataforma | antes do primeiro segredo real |
| DEC — modelo de confiança do Remote Worker | antes do R5 |
| DEC — primeiro provider de deploy | no R6, por comparação |
| DEC — organizações persistentes: retenção e privacidade | antes de persistir dados de usuário |

**ADRs necessários:** **ADR-0017** (este — fronteiras e sequência; `Proposto`); depois, um por contrato: núcleo do Agent Runtime v1 · porta de provedor de modelo · ledger de orçamento/uso · SecretStore e redação · modelo de Organização/Equipe · contrato do Agent Workspace · Execution Runtime e protocolo de Worker (depois do ADR de IPC da Fase 5) · capabilities de deploy · generalização do integrador (dogfooding).

## 14. Enforcement: cada conceito com verificação real

Regra do ADD (§44): onde a regra puder ser validada automaticamente, **não se aceita só documentação**. Testes do R1 (cada um falha de verdade se a regra for violada, como os `--self-test` do repositório):

| # | Teste | Regra (ADD §44/§43) |
|---|---|---|
| T-1 | agente **não** usa capability sem permissão | permission-driven, deny-by-default |
| T-2 | ferramentas = interseção Host ∩ org ∩ projeto ∩ agente | capability-driven |
| T-3 | `Run` **não** acessa Context fora do escopo (inclui travessia de caminho) | Agent × Context |
| T-4 | orçamento **não** é ultrapassado em silêncio (`Blocked(budget)`) | budget-aware |
| T-5 | `Completed` exige saída **e** verificação aprovada | output ≠ success |
| T-6 | provedor desconhecido **falha** | provider-agnostic, falha fechada |
| T-7 | cancelamento interrompe a execução em um passo | cancellable |
| T-8 | laço de retry tem limite (N falhas equivalentes ⇒ `Blocked`) | limite de falha |
| T-9 | **ocioso custa zero:** sem eventos, nenhuma chamada a provedor nem timer | sem busy loop |
| T-10 | segredo-sentinela **nunca** aparece em log/evento/artefato | secret não aparece em log |
| T-11 | todo evento responde quem/por quê/ferramenta/modelo/custo/contexto/resultado/verificação/aprovação | observable, auditable |
| T-12 | evento **sem** campo de raciocínio livre; adapter descarta o que vier | não armazenar chain-of-thought |
| T-13 | arquitetura: `Core` sem SDK de provedor, HTTP, processo, Product, Hub, UI, GitHub | host-agnostic, model-agnostic |
| T-14 | suíte de contrato de provedor roda em **≥ 2** implementações | provider-agnostic |
| T-15 | `Run` retomável: estado reconstruído do log após "reinício" | resumable |
| T-16 | worker incompatível **não** recebe job (R5) | host-agnostic, capability-driven |

Além dos testes, **o que já fiscaliza este plano hoje**: `CHK-DECISIONS` (DEC-0026 com objeto), `CHK-ROADMAP`/`CHK-STATE-CONSISTENCY` (itens e Issues), `CHK-HANDOFFS`, `CHK-SECRETS`, `CHK-BOUNDARIES`/`CHK-ARCH-REFS` e o integrador (zona crítica `architecture` para `platform/**`, `workspaces/**`, `tools/**`, `services/**`).

## 15. Definition of Done da etapa de arquitetura (ADD §46)

| Item | Onde está respondido |
|---|---|
| O que é Agent Runtime | §4, §5 (Service; decide e coordena) |
| O que é Agent Workspace | §4 (Workspace; superfície hospedável) |
| O que é Product de IA | §4 (consumidor privilegiado; experiência, não inteligência) |
| O que é Organization | §6 |
| O que é Execution Runtime | §4, §7 |
| O que é Worker | §7.2 (Host de execução autorizado que registra capabilities e recebe jobs) |
| O que é Deployment | §7.3 (capabilities abstratas + providers) |
| O que é Tool | §5.2 (uma Capability) |
| Tool × Capability | §5.2 |
| Agent × Context | §5.2 |
| Agent × Permission | §5.2, §5.5 |
| Agent × ModelProvider | §5.2 |
| Task × Run | §5.2, §5.3 |
| Goal × Plan | §5.2 |
| Budget × Execution | §5.2, §8.1 |
| Boundary Hub × IA | §4 |
| Boundary Runtime × Product | §4 |
| Boundary Agent Runtime × Execution Runtime | §4 |
| Como o local-first é preservado | §3 (C2, C6), §7.2 |
| Como o 24/7 funciona | §7.2 (eventos, filas, Hosts autorizados; sem busy loop) |
| Como providers externos entram | §5.1 (porta + adapters), §7.3 |
| Como secrets são protegidos | §8.2 |
| Como custos são limitados | §8.1 |
| Como humanos são chamados só quando necessário | §8.5, §13 |
| Como o Ecosystem poderá usar essa infraestrutura | §9 |

## 16. Fora de escopo (non-goals imediatos, ADD §42)

AGI · autonomia irrestrita · compra ilimitada de nuvem · deploy em produção sem política · acesso irrestrito a segredos · marketing automatizado indiscriminado · billing completo · marketplace · centenas de agentes · suporte a todos os providers · substituir os workflows atuais. **Não se promete retorno financeiro**: o sistema pode automatizar produção e operação de software; mercado, usuários e receita continuam sujeitos ao mundo real.

## 17. Riscos

| Risco | Mitigação |
|---|---|
| Abstração compartilhada especulativa (NN-020/NN-022) | fronteira de extração desde o R1, promoção por evidência, ADR por extração; "continua local" é um resultado válido |
| Congelar contrato errado | contratos nascem **com** o 1º consumidor; versão 0 explícita; duas implementações de provedor antes de congelar |
| Deriva entre três IAs (Urbe, Lunet, nova) | decisão futura sobre UC-21; paridade medida pelo oráculo do Urbe |
| Custo imprevisto | ledger no R1; nenhuma capability de gasto real sem DEC e política |
| Vazamento de segredo | referência em vez de valor; canário T-10; `CHK-SECRETS` |
| Escopo (monstro de uma vez) | slices pequenos; cada um com prova; nenhum implementado por este PR |
