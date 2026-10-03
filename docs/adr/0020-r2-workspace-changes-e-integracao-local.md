# ADR-0020 — R2: workspace isolado, revisão e integração local

## Status

Proposto — implementação candidata do P6-3; somente o PR crítico autorizado pode
ativá-la na main. Não registra decisão do proprietário nem estabiliza contrato
transversal. Direção existente: ADR-0017 D4/D6/D7/D8, DEC-0026-C e P6-3.

## Contexto

O R1 executa um agente, verifica artefatos e limita custo. O R2 precisa provar
produção e revisão por identidades diferentes, tarefas dependentes e integração
sem conceder escrita do estado canônico ao produtor. Os contratos do plano §10
não existem como protocolo público. Esta proposta mantém tudo local ao Product.

## Proposta

- `WorkspaceSnapshot` imutável; `IsolatedWorkspace` overlay sobre uma base fixa;
  `ChangeSet` selado com ID, tarefa, produtor, run e alterações antes/depois.
- `TeamPlan` efêmero valida IDs, dependências, ciclos e revisor diferente. Só uma
  `IntegrationReceipt` emitida pelo integrador, com produtor/revisor designados e
  evidência, libera tarefa dependente. Produção bem-sucedida não conclui a equipe.
- Integrador serial combina com estado atual; alteração no mesmo path desde a
  base gera conflito explícito, sem sobrescrita. Mudanças independentes sobre base
  antiga são combinadas e verificadas novamente. Retrabalho sela proposta nova.
- Revisão independente vinculada ao candidato exato; verificador confiável recebe
  combinado imutável e exige tarefa correta/evidência. Cancelamento/erro/reprovação
  deixam canônico intacto. ID já integrado não é aplicado duas vezes.
- Policy/authorizer são portas confiáveis do Host, nunca conteúdo do agente.
  Deny e enum desconhecido bloqueiam; efeito crítico escala sem autorizador.
  Autorização exige candidato exato e identidade do proprietário; revogação da
  política é relida depois da aprovação. Produção/revisão não conferem aprovação.
- Esta política é local e **não substitui** integration-policy.json nem integra o
  repositório real. Nenhum adapter Git, worker, rede, processo ou IO no Core.

## Escopo do contrato candidato

API local em processo, versão experimental 0. Não é capability pública nem
protocolo IPC; nenhum schema da plataforma é estabilizado por este slice. O
contrato compartilhado/serializado exige ADR próprio com JSON Schema antes do
adapter entre processos, conforme ADR-0017 D9. Snapshots e receipts são efêmeros,
sem promessa de replay durável. Não alterar o contrato de eventos do R1.

## Alternativas

1. Agente escrever no canônico: elimina isolamento e torna revisão ineficaz.
2. Reproduzir PR/Actions no Core: acopla domínio ao fornecedor e viola NN-015.
3. Overlay e portas locais (recomendação): prova o algoritmo com custo mínimo;
   adapter durável, auditoria persistente e protocolo entram em itens posteriores.

## Consequências e limites

Sem extração: só ecosystem-ai é consumidor. A biblioteca continua local (NN-022).
Testes incluem dois AgentRunner reais com providers determinísticos e capabilities
limitadas, revisão, conflito/retrabalho e dependências. O Host deve registrar as
receipts e fornecer adapters confiáveis; ainda não há organização persistente,
coordenador autônomo/24h, filesystem de produção nem interface de usuário.

Mudança crítica de segurança: cria uma fronteira de publicação do canônico e
escalonamento. Handoff declara security, mesmo fora dos paths da política. O
integrador existente testa o PR e espera autorização do proprietário.

## Referências

MANIFEST §22.4/23/39; NN-001/008/011/015/016/018/022; ADR-0017;
docs/architecture/agent-runtime.md §8.5/9/10/11; P6-3, Issue #136.
