# ADD-0013 — Agent Runtime, Execution Runtime e organizações de agentes

> **Tipo:** diretiva explícita do proprietário, registrada (MANIFEST §24, nível 1). É **direção e delegação de planejamento**, não aprovação de implementação: a escolha do que entra e quando é a DEC-0026; nenhuma decisão crítica nova foi tomada por este arquivo.
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-03, em conversa com o agente `claude-code`, persistido aqui conforme NN-009.
> **Contexto da mensagem:** com o integrador automático (ADR-0015, ADD-0012) e o fluxo multiagente em operação, o proprietário enviou, como arquivo, a visão de uma infraestrutura universal de IA e agentes — Agent Runtime, Agent Workspace, um Product de IA, organizações de agentes, execução persistente, deploy e orçamento — e pediu como primeira entrega um **plano arquitetural completo e executável**, integrado ao ROADMAP, sem destruir a arquitetura atual.
> **Resposta e consequências:** [`docs/architecture/agent-runtime.md`](../../architecture/agent-runtime.md) (auditoria, conflitos, desenho, contratos propostos, fatiamento), [ADR-0017](../../adr/0017-agent-runtime-execution-runtime-e-organizacoes.md) (`Proposto`), **DEC-0026** em [`decisions.json`](../decisions.json) e os itens P6-1..P6-5 do [`ROADMAP.md`](../../../ROADMAP.md).
> O texto abaixo é a transcrição integral do arquivo enviado (`ecosystem-agent-runtime-prompt.md`), sem alteração de conteúdo nem de títulos. Não editar; mudanças exigem novo adendo ou decisão registrada.

---

# Prompt — Agent Runtime, Ecosystem AI e Execution Runtime

Você está trabalhando no repositório `AbnerCruz/Ecosystem`.

Sua missão é projetar e iniciar a implementação de uma nova camada fundamental do Ecosystem:

**uma infraestrutura universal de inteligência artificial e agentes, utilizável por qualquer Product do Ecosystem, incluindo um Product próprio dedicado à IA e, futuramente, execução persistente 24/7 e deploy de aplicações/serviços.**

Isto NÃO deve ser tratado como “adicionar um chat de IA”.

O objetivo é muito maior.

Queremos que o Ecosystem consiga evoluir até o ponto em que o proprietário possa expressar uma intenção de alto nível, por exemplo:

```text
"Crie um aplicativo para resolver X.
Orçamento máximo de R$ 150/mês.
Não me interrompa salvo por decisões realmente críticas."
```

e uma organização de agentes possa:

```text
entender a intenção
→ planejar
→ dividir trabalho
→ criar/agrupar agentes
→ pesquisar
→ desenvolver
→ testar
→ revisar
→ integrar
→ provisionar infraestrutura
→ fazer deploy
→ observar
→ corrigir
→ continuar evoluindo
```

O proprietário deve operar principalmente no nível de **intenção, estratégia e decisões críticas**, e não no nível de tarefas rotineiras.

---

## 1. ANTES DE FAZER QUALQUER ALTERAÇÃO

Leia o estado REAL e atual da `main`.

Não use este prompt como snapshot canônico.

Inspecione pelo menos:

```text
MANIFEST.md
AGENTS.md
ARCHITECTURE.md
ROADMAP.md
ecosystem.json
docs/adr/**
docs/governance/**
docs/contracts/**
docs/architecture/**
apps/hub/**
apps/urbe/**
apps/lunet2d/**
tests/consistency/**
.github/integrator/**
.github/workflows/**
```

Entenda especialmente:

- arquitetura Product / Product Shell / Workspace / Tool / Service / Library / Adapter;
- ComponentManifest;
- Capability;
- provider / consumer;
- Registry;
- Context;
- permission model;
- Distribution Profile;
- princípio local-first;
- isolamento entre Products;
- política de integração automática;
- trabalho rotineiro × crítico;
- fluxo multiagente atual;
- handoffs;
- integração serial da `main`;
- mecanismo de decisão pelo proprietário;
- fases futuras já previstas no ROADMAP.

Preserve as decisões já tomadas.

Não invente uma segunda arquitetura paralela ao Ecosystem.

---

# 2. IDEIA CENTRAL

A inteligência artificial deve ser uma **capacidade nativa do Ecosystem**.

Não deve pertencer:

- ao Urbe;
- ao Lunet;
- ao Hub;
- a um modelo específico;
- a um provedor específico.

A arquitetura conceitual deve caminhar para algo equivalente a:

```text
                   ECOSYSTEM
                       │
             ┌─────────┴─────────┐
             │                   │
       Agent Runtime       Execution Runtime
             │                   │
       intelligence         execução externa
             │                   │
      ┌──────┼──────┐      ┌─────┼─────┐
      │      │      │      │     │     │
    Urbe   Lunet   AI    Local Cloud Server
```

Não congele esses nomes sem verificar a terminologia canônica atual.

Conceitualmente, porém, separe claramente:

```text
inteligência
interface
ferramentas
organização
execução
infraestrutura externa
```

---

# 3. AGENT RUNTIME

Deve existir uma camada reutilizável responsável pelo funcionamento dos agentes.

Conceitos esperados:

```text
Agent
AgentIdentity
ModelProvider
Model
Context
Memory
Tool
Capability
Permission
Task
Plan
Workspace
Artifact
Handoff
Event
Verification
Approval
Budget
Usage
```

E, para multiagente:

```text
Organization
Team
Role
Delegation
TaskGraph
Dependency
Communication
Review
Coordination
Escalation
```

Não é obrigatório usar exatamente esses nomes.

O importante é que os conceitos sejam explícitos, generalistas e não acoplados ao GitHub.

---

# 4. NÃO CONFUNDIR MODELO COM INTELIGÊNCIA

A arquitetura deve ser **model-agnostic e provider-agnostic**.

OpenAI, Anthropic, OpenRouter, modelos locais ou qualquer outro provedor devem ser adapters/providers substituíveis.

O modelo é apenas uma das peças.

O valor real do sistema está em:

```text
modelo
+ contexto
+ ferramentas
+ memória
+ planejamento
+ execução
+ verificação
+ coordenação
+ permissões
```

Portanto NÃO crie código do domínio central que conheça diretamente APIs específicas de um fornecedor.

Deve existir algo conceitualmente como:

```text
IModelProvider
```

com implementações concretas fora do núcleo.

---

# 5. AGENT WORKSPACE

Deve existir uma superfície reutilizável para interação com agentes.

Não crie um chat separado do zero dentro de cada Product.

Queremos algo semelhante a:

```text
Agent Workspace
```

que possa ser hospedado por Products diferentes.

Exemplo no Urbe:

```text
Context:
Ecosystem
→ Urbe
→ Meu Vault
→ Editor
→ Nota X

Tools:
ler nota
editar nota
criar nota
pesquisar vault
criar artefato
```

Exemplo no Lunet:

```text
Context:
Ecosystem
→ Lunet2D
→ Meu Jogo
→ Editor

Tools:
ler código
editar código
executar build
executar testes
analisar assets
criar/modificar recursos
```

É a mesma infraestrutura de inteligência.

O que muda é:

```text
contexto
capabilities disponíveis
permissões
```

---

# 6. PRODUCT PRÓPRIO DE IA

Além da infraestrutura compartilhada, queremos um Product first-party dedicado à IA.

Não assuma o nome definitivo sem processo de decisão adequado.

Use provisoriamente algo como:

```text
Ecosystem AI
```

Esse Product NÃO contém a inteligência em si.

Ele é um consumidor privilegiado do Agent Runtime e oferece uma experiência dedicada.

Deve poder futuramente incluir:

```text
Chat
Assistente
Projetos
Agent Workspace
Agentes
Equipes
Organizações
Tarefas
Artefatos
Memórias
Ferramentas
Execuções
Custos
Histórico
```

---

# 7. AGENT ORGANIZATION

Uma das experiências principais desse Product deve ser uma organização multiagente.

Esta é a evolução da ideia de “empresa de agentes”.

NÃO transforme todo o sistema numa empresa fictícia.

A empresa/organização é apenas uma configuração possível do sistema.

Exemplo:

```text
Projeto
│
├── coordenador
├── arquiteto
├── desenvolvedor
├── pesquisador
├── designer
├── QA
└── revisor
```

Mas a composição não deve ser fixa.

O sistema deve poder construir equipes dinamicamente conforme a intenção.

Por exemplo:

```text
"Escreva e ilustre um livro"
```

pode gerar:

```text
escritor
editor
pesquisador
ilustrador
revisor
```

enquanto:

```text
"Implemente um compilador"
```

pode gerar:

```text
arquiteto
especialista em parsing
implementadores
testes
revisor
```

Não codifique departamentos fixos no núcleo.

Modele:

```text
roles
agents
teams
responsibilities
capabilities
```

como composição.

---

# 8. O ECOSYSTEM ATUAL É O NOSSO LABORATÓRIO

A arquitetura multiagente que já está desenvolvendo o próprio Ecosystem é evidência prática.

Estude-a.

Hoje existem conceitos implementados através de:

```text
Git branch
Pull Request
GitHub Actions
integration branch
handoff
labels
main
```

NÃO copie esses mecanismos diretamente para o Runtime.

Extraia os conceitos universais.

Por exemplo:

```text
Git branch
→ Isolated Workspace

PR
→ Proposed Change / Change Set

GitHub Actions
→ Validation Pipeline

main
→ Canonical State

label de autorização
→ Human Authorization

handoff
→ Structured Work Record
```

Queremos que o mesmo modelo conceitual possa funcionar:

- com GitHub;
- com arquivos locais;
- com Urbe;
- com Lunet;
- com servidores;
- com outros ambientes futuros.

---

# 9. DOGFOODING

Meta de longo prazo:

o próprio Ecosystem deve conseguir usar seu Agent Runtime para continuar desenvolvendo o Ecosystem.

Fluxo desejado:

```text
Ecosystem
    ↓
fornece Agent Runtime
    ↓
executa agentes
    ↓
agentes desenvolvem Ecosystem
```

Isto NÃO significa criar recursão descontrolada.

O estado canônico continua protegido por:

- permissões;
- validação;
- política;
- orçamento;
- aprovação humana quando crítica.

---

# 10. AUTONOMIA

Princípio obrigatório:

```text
IF SYSTEM ALREADY KNOWS HOW TO JUDGE
→ SYSTEM JUDGES

IF NEW DIRECTION / CRITICAL EFFECT
→ OWNER DECIDES
```

O proprietário não deve virar supervisor de tarefas.

Não pergunte coisas como:

```text
posso criar arquivo?
posso executar teste?
posso corrigir esse bug?
posso fazer refactor local?
```

Isso deve ser determinado por:

```text
permissions
policy
budget
validation
scope
```

Escale apenas mudanças que realmente mereçam decisão humana.

---

# 11. TAREFAS AUTÔNOMAS

O Runtime deve ser projetado para permitir futuramente:

```text
Goal
↓
Plan
↓
Tasks
↓
Dependencies
↓
Agents
↓
Execution
↓
Verification
↓
Rework
↓
Completion
```

Uma tarefa não deve ser considerada concluída simplesmente porque um LLM produziu texto.

Ela precisa possuir evidência verificável de conclusão.

Quando possível:

```text
output ≠ success

verified output = success
```

---

# 12. EXECUÇÃO PERSISTENTE 24/7

O navegador/mobile não pode ser considerado suficiente para trabalho persistente.

Android, navegadores e sistemas móveis suspendem processos.

Portanto a arquitetura precisa prever Hosts de execução.

Exemplos:

```text
Browser Host
Desktop Host
Local Worker
Home Server
VPS Worker
Cloud Worker
Managed Worker
```

O usuário deve poder abrir o celular, emitir uma intenção e fechar o aplicativo enquanto a execução continua em outro Host autorizado.

Não implemente dependência obrigatória de cloud.

Preserve local-first sempre que possível.

Cloud é uma capacidade adicional.

---

# 13. EXECUTION RUNTIME

Além do Agent Runtime, investigue uma camada geral de execução.

Conceitualmente:

```text
Execution Runtime
│
├── Processes
├── Jobs
├── Workers
├── Services
├── Secrets
├── Networking
├── Storage
├── Deployment
├── Observability
├── Budgets
└── Authorization
```

Não misture isso prematuramente com Agent Runtime se forem responsabilidades diferentes.

Determine o boundary correto.

---

# 14. DEPLOYMENT RUNTIME

Queremos chegar à capacidade de agentes implantarem software real.

Porém o núcleo NÃO pode ser acoplado a Vercel, AWS, Cloudflare ou qualquer fornecedor específico.

Deve existir uma capability abstrata equivalente a:

```text
deploy.application
deploy.service
provision.runtime
provision.database
configure.domain
configure.environment
inspect.deployment
rollback.deployment
```

Providers possíveis:

```text
Docker
SSH/VPS
Vercel
Cloudflare
AWS
Azure
GCP
Fly.io
Render
Railway
outros
```

Não implemente todos.

Faça arquitetura extensível.

---

# 15. NUNCA DAR CARTÃO DE CRÉDITO IRRESTRITO PARA UM AGENTE

O sistema precisa de orçamento como conceito de primeira classe.

Exemplo:

```text
Organization Budget
Project Budget
Agent Budget
Task Budget
Provider Budget
```

Deve ser possível declarar:

```text
limite total
limite diário
limite mensal
limite por operação
limite por provider
```

Exemplo:

```text
orçamento operacional mensal: R$ 150
operação abaixo de R$ 5: automática
gasto recorrente novo: autorização
aumento de limite: autorização
```

Não fixe esses valores.

São exemplos de política.

---

# 16. SEGREDOS

Nunca coloque secrets em:

- prompts;
- logs;
- memória de agentes;
- arquivos de projeto;
- commits;
- artefatos;
- contexto textual desnecessário.

Modele um `SecretStore`/capability equivalente.

O agente deve receber uma referência ou permissão de uso, não necessariamente o valor do segredo.

Exemplo:

```text
agent
→ permission: deploy.production
→ provider usa secret internamente
```

em vez de:

```text
agent recebe AWS_SECRET_ACCESS_KEY em texto
```

---

# 17. OBSERVABILIDADE

Tudo que um agente fizer precisa ser auditável.

Deve existir capacidade de responder:

```text
quem fez?
por quê?
em nome de qual objetivo?
com quais ferramentas?
qual modelo?
qual custo?
qual contexto?
qual resultado?
qual verificação?
quem aprovou?
```

Não confunda observabilidade com expor chain-of-thought.

Registre:

- decisões operacionais;
- ações;
- argumentos relevantes;
- inputs permitidos;
- outputs;
- eventos;
- custos;
- resultados.

Não exija nem armazene raciocínio privado interno do modelo.

---

# 18. MEMÓRIA

Diferencie:

```text
conversation memory
project memory
agent memory
organization memory
artifact history
canonical knowledge
runtime state
```

Não faça tudo virar um vetor gigantesco ou histórico de chat.

Conhecimento canônico deve ter fonte e autoridade identificáveis.

Memória não pode substituir estado real.

---

# 19. FERRAMENTAS

Ferramentas devem ser capabilities.

Exemplo:

```text
files.read
files.write
code.edit
process.run
tests.run
git.commit
github.pr.create
image.generate
web.search
deployment.deploy
database.migrate
```

Um agente nunca deve ganhar automaticamente todas as ferramentas.

Ferramentas disponíveis =

```text
capabilities do Host
∩
permissões da organização
∩
permissões do projeto
∩
permissões do agente
```

deny-by-default onde houver risco relevante.

---

# 20. CONTEXT

Reutilize o contrato de Context já existente.

Um agente precisa saber em que mundo está operando.

Exemplo:

```text
Ecosystem
→ Product
→ Project
→ Workspace
→ Tool
```

Não crie referências diretas Product → Product.

Se um agente precisar usar capacidades do Urbe e Lunet:

```text
agent
→ Registry
→ capabilities
→ providers
```

e não:

```text
Urbe importa Lunet
```

---

# 21. ORGANIZAÇÕES PERSISTENTES

O sistema deve suportar futuramente dois modos:

### Equipe efêmera

Criada para um objetivo e destruída/arquivada ao terminar.

```text
Goal
→ Team
→ Work
→ Done
```

### Organização persistente

Continua existindo, acumulando projetos, responsabilidades, memória e objetivos.

```text
Organization
├── goals
├── agents
├── teams
├── projects
├── budgets
└── history
```

Isso permite a ideia de uma “empresa de agentes”, mas sem acoplar o Runtime a uma metáfora empresarial.

---

# 22. AGENTES 24/7 NÃO DEVEM FAZER BUSY LOOP

“Trabalhar 24/7” não significa:

```text
while(true)
  gastar tokens
```

O sistema deve ser orientado a:

```text
eventos
tarefas
filas
timers
condições
dependências
novos objetivos
```

Agente ocioso deve custar aproximadamente zero.

Não permita loops de reflexão inúteis.

---

# 23. ECONOMIA DE EXECUÇÃO

O sistema precisa saber:

```text
quanto está gastando
onde está gastando
por que está gastando
quanto aquela tarefa já custou
qual orçamento resta
```

Deve ser possível encerrar, pausar ou degradar trabalho quando:

```text
budget exceeded
provider unavailable
repeated failure
task has no progress
```

Não deixe um agente repetir indefinidamente a mesma tentativa.

---

# 24. LOOP DE FALHA

Modele explicitamente:

```text
attempt
→ validation fails
→ diagnose
→ corrective task
→ retry
```

Mas imponha limites.

Depois de N falhas equivalentes:

```text
blocked
```

e escale para:

- outro agente;
- outro método;
- revisão;
- humano, se necessário.

Não deixe “corrigir → falhar → corrigir → falhar” consumir recursos indefinidamente.

---

# 25. QUALIDADE

O sistema multiagente NÃO deve ser baseado na premissa:

```text
mais agentes = melhor
```

Cada agente adicional tem custo de:

```text
comunicação
contexto
coordenação
tokens
latência
conflitos
```

O orquestrador deve preferir a menor equipe suficiente.

Exemplo:

```text
tarefa simples
→ 1 agente

tarefa média
→ implementador + revisão

tarefa complexa
→ equipe especializada
```

---

# 26. O HUB

O Hub NÃO deve virar o Product de IA.

O Hub continua sendo o control plane do Ecosystem.

Ele pode futuramente mostrar:

```text
agentes ativos
organizações
jobs
workers
custos
providers
models
deployments
decisões pendentes
alertas
```

Mas:

```text
Hub ≠ Agent Runtime
Hub ≠ Ecosystem AI
```

---

# 27. O PRODUCT DE IA

A experiência própria da IA deve ser um Product.

Conceitualmente:

```text
Ecosystem AI
│
├── Chat
├── Projects
├── Agent Workspace
├── Organizations
├── Agents
├── Tasks
├── Artifacts
├── Tools
├── Memory
├── Runs
└── Activity
```

O Product pode consumir infraestrutura compartilhada.

Não duplique o Runtime dentro dele.

---

# 28. VISÃO DE EXPERIÊNCIA DO USUÁRIO

O estágio final deve permitir algo parecido com:

```text
Usuário:
"Crie um pequeno SaaS para gerenciamento de X.
Quero assinatura de R$ 19,90.
O custo máximo de infraestrutura é R$ 150/mês.
Faça tudo que estiver dentro dessas regras.
Só me consulte quando houver uma decisão realmente crítica."
```

Depois:

```text
Organization created

Research
✓

Product definition
✓

Prototype
✓

Implementation
in progress

Tests
pending

Deployment
pending
```

Horas/dias depois:

```text
Product online
Tests green
Deployment healthy
Monitoring active

Requires owner:
Enable real payment processing?
```

NÃO prometa retorno financeiro.

O sistema pode automatizar produção e operação de software.

Mercado, aquisição de usuários e receita continuam sujeitos ao mundo real.

---

# 29. DESENVOLVIMENTO DE PRODUTOS COMERCIAIS

Prepare a arquitetura para que agentes possam futuramente:

```text
pesquisar mercado
identificar problemas
criar hipóteses
construir MVP
lançar
observar métricas
receber feedback
propor mudanças
iterar
```

Porém ações externas devem sempre obedecer capabilities, permissões e políticas.

Não crie autonomia irrestrita.

---

# 30. NÃO IMPLEMENTAR UM MONSTRO DE UMA VEZ

Esta é uma visão de longo prazo.

NÃO tente implementar:

```text
Agent Runtime
+ Product AI
+ Cloud
+ Deploy
+ Organization
+ Billing
+ todos os providers
```

em um único PR.

Isso seria arquiteturalmente irresponsável.

Queremos:

```text
visão completa
↓
boundaries corretos
↓
roadmap
↓
vertical slices
↓
dogfooding
↓
expansão
```

---

# 31. SUA PRIMEIRA ENTREGA

Sua primeira tarefa é produzir um **plano arquitetural completo e executável**, integrado ao ROADMAP existente.

Determine:

1. quais conceitos já existem;
2. quais podem ser reutilizados;
3. quais novos contratos são necessários;
4. quais decisões são realmente críticas;
5. quais ADRs precisam existir;
6. qual sequência de implementação reduz risco;
7. qual deve ser o primeiro vertical slice.

Não altere silenciosamente decisões estruturais já aprovadas.

Se houver uma nova decisão crítica, crie o mecanismo canônico para o proprietário decidir.

---

# 32. VERTICAL SLICE INICIAL SUGERIDO

Avalie criticamente esta sugestão:

```text
Agent Runtime mínimo
│
├── IModelProvider fake/local
├── Agent
├── Task
├── Context
├── Tool
├── Permission
├── Run
└── Event log
```

Cenário:

```text
usuário cria uma tarefa
↓
agente recebe Context
↓
usa uma Tool segura
↓
produz Artifact
↓
verifier valida
↓
Run = completed
```

Tudo inicialmente:

- local;
- determinístico quando possível;
- sem cloud;
- sem deploy;
- sem segredo;
- sem provedor comercial obrigatório.

Esse slice deve provar a arquitetura.

---

# 33. SEGUNDO SLICE

Depois, algo equivalente a:

```text
2 agentes
↓
tarefas dependentes
↓
workspace isolado
↓
produção
↓
review
↓
integração
```

Use o aprendizado do integrador GitHub existente sem acoplar o Runtime a Git.

---

# 34. TERCEIRO SLICE

Reutilizar o Agent Workspace dentro de um Product existente.

Urbe ou Lunet, conforme dependências e ROADMAP permitirem.

Não escolha arbitrariamente.

---

# 35. QUARTO SLICE

Criar o Product dedicado à IA consumindo exatamente o mesmo Runtime.

Isso deve provar:

```text
Runtime compartilhado
!= Product
```

---

# 36. EXECUÇÃO REMOTA

Só depois da fundação estar provada:

```text
Remote Worker
```

O Remote Worker deve:

```text
registrar capabilities
receber jobs autorizados
executar isoladamente
emitir eventos
reportar resultado
respeitar orçamento
respeitar timeout
aceitar cancelamento
```

---

# 37. DEPLOY COMO SLICE FUTURO

O primeiro provider de deploy deve ser escolhido por:

- simplicidade;
- isolamento;
- automação;
- rollback;
- custo;
- capacidade de teste;
- segurança.

Um bom candidato conceitual pode ser:

```text
Docker + SSH/VPS
```

porque reduz dependência de fornecedor.

Mas NÃO assuma isso como decisão.

Compare alternativas no momento correto.

---

# 38. PRODUTO COMO OBJETIVO

Introduza a noção de que o usuário fornece:

```text
Goal
Constraints
Budget
Permissions
Success Criteria
```

e não uma sequência de microtarefas.

Isso é central.

Exemplo:

```json
{
  "goal": "Criar um MVP para X",
  "constraints": [
    "mobile-first",
    "sem login no primeiro release"
  ],
  "budget": {
    "monthly": 150
  },
  "successCriteria": [
    "build verde",
    "deploy saudável",
    "fluxo principal testado"
  ]
}
```

O formato definitivo deve seguir contratos canônicos, não necessariamente esse JSON.

---

# 39. O PROPRIETÁRIO NÃO É O GERENTE OPERACIONAL

Preserve esta filosofia:

```text
proprietário
→ direção
→ restrições
→ orçamento
→ decisões críticas
```

O sistema deve cuidar de:

```text
planejamento operacional
divisão de tarefas
retries
atribuição
revisão
validação
integração
```

Não recrie um sistema em que o proprietário precise apertar “aprovar” em cada passo.

---

# 40. ROADMAP

Integre a visão ao ROADMAP sem atropelar as fases atuais.

O ROADMAP já possui evolução progressiva do Ecosystem.

Não pule fases simplesmente porque esta ideia é atraente.

Determine em quais fases entram:

```text
Agent Runtime
Agent Workspace
AI Product
Organizations
Remote Workers
Execution Runtime
Deployment
Budgets
Secrets
Observability
```

Se forem necessárias novas fases futuras, proponha.

Não marque nada como concluído sem evidência.

---

# 41. CRITICIDADE

Esta tarefa envolve arquitetura cross-cutting.

Alterações como:

- novo tipo fundamental;
- mudança de autoridade;
- mudança de permission model;
- novo control plane;
- armazenamento de secrets;
- execução remota;
- deploy;
- gastos automáticos;
- operação externa;

são provavelmente críticas.

Use a política canônica do repositório.

Não se autoautorize.

Mas também não transforme implementação rotineira posterior em aprovação humana eterna.

---

# 42. NON-GOALS IMEDIATOS

NÃO fazer agora:

```text
AGI
autonomia irrestrita
auto-compra ilimitada de cloud
deploy em produção sem política
acesso irrestrito a secrets
marketing automatizado indiscriminado
billing completo
marketplace
centenas de agentes
suporte a todos os providers
substituir todos os workflows atuais
```

---

# 43. PROPRIEDADES OBRIGATÓRIAS

A arquitetura final precisa preservar:

```text
model-agnostic
provider-agnostic
host-agnostic
local-first
capability-driven
permission-driven
observable
auditable
budget-aware
resumable
cancellable
testable
composable
multi-agent capable
human escalation only when justified
```

---

# 44. TESTES

Cada novo conceito precisa de enforcement real.

Não aceite documentação sem check correspondente quando a regra puder ser validada automaticamente.

Exemplos futuros:

```text
agent não usa capability sem permissão
agent não acessa Context fora do escopo
budget não pode ser ultrapassado silenciosamente
task completed exige output/verificação
provider desconhecido falha
worker incompatível não recebe job
secret não aparece em log
cancelamento interrompe execução
retry loop possui limite
```

---

# 45. RESULTADO ESPERADO DESTA DELEGAÇÃO

Quero que você:

1. audite o estado atual;
2. confronte esta visão com a arquitetura existente;
3. identifique conflitos;
4. resolva o que puder sem mudar decisões críticas;
5. proponha ADRs quando necessário;
6. atualize ROADMAP de forma coerente;
7. produza desenho arquitetural;
8. defina contratos;
9. decomponha em fases pequenas;
10. implemente apenas o primeiro slice que for seguro e compatível com o estágio atual;
11. escreva testes;
12. abra PR;
13. deixe o integrador tratar o trabalho rotineiro;
14. leve ao proprietário somente as decisões realmente críticas.

---

# 46. DEFINITION OF DONE DA ETAPA DE ARQUITETURA

Antes de iniciar implementação estrutural relevante, deve estar claro:

- [ ] o que é Agent Runtime;
- [ ] o que é Agent Workspace;
- [ ] o que é Product de IA;
- [ ] o que é Organization;
- [ ] o que é Execution Runtime;
- [ ] o que é Worker;
- [ ] o que é Deployment;
- [ ] o que é Tool;
- [ ] relação Tool × Capability;
- [ ] relação Agent × Context;
- [ ] relação Agent × Permission;
- [ ] relação Agent × ModelProvider;
- [ ] relação Task × Run;
- [ ] relação Goal × Plan;
- [ ] relação Budget × Execution;
- [ ] boundary Hub × IA;
- [ ] boundary Runtime × Product;
- [ ] boundary Agent Runtime × Execution Runtime;
- [ ] como local-first é preservado;
- [ ] como 24/7 funciona;
- [ ] como providers externos entram;
- [ ] como secrets são protegidos;
- [ ] como custos são limitados;
- [ ] como humanos são chamados somente quando necessário;
- [ ] como o próprio Ecosystem poderá usar essa infraestrutura.

---

# 47. VISÃO FINAL

Tudo deve convergir para isto:

```text
HUMANO
│
│ intenção
▼
GOAL
│
▼
ORCHESTRATION
│
├── plan
├── organization
├── agents
├── tasks
└── budgets
│
▼
AGENT RUNTIME
│
├── models
├── context
├── memory
├── tools
├── permissions
└── verification
│
▼
EXECUTION RUNTIME
│
├── local
├── workers
├── services
├── deployment
└── providers
│
▼
MUNDO EXECUTÁVEL
```

E por cima disso:

```text
HUB
```

observa e administra o Ecosystem.

Products como:

```text
Urbe
Lunet2D
Ecosystem AI
```

consomem as mesmas capacidades sem depender diretamente uns dos outros.

---

# 48. PRINCÍPIO FINAL

Não estamos construindo:

```text
"um chatbot com ferramentas"
```

Estamos construindo:

```text
uma infraestrutura onde o usuário declara
o que quer que exista,

e agentes autorizados dispõem de
inteligência,
ferramentas,
recursos,
execução,
coordenação
e verificação

para tentar transformar essa intenção
em um resultado real.
```

Faça isso de maneira incremental, verificável e sustentável.

Não destrua a arquitetura atual tentando chegar ao estado final cedo demais.

O próprio processo de construção deve servir como laboratório da arquitetura que estamos criando.

**Quanto mais o Ecosystem puder desenvolver o próprio Ecosystem usando esses mecanismos, mais forte é a evidência de que o desenho está correto.**
