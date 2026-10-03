# ADR-0021 — R3: sessão local do Agent Workspace

## Status

Proposto — detalhes da prova local de P6-4, sem aceitar contrato compartilhado,
Host API ou escolha de Product anfitrião. Direção vigente ADR-0017 D3/D7/D9;
plano agent-runtime §11.1 autoriza Host de teste enquanto os Products não estão
prontos. A implementação local não declara o item completo.

## Contexto

R2 integrado no PR #140. O Core já executa agentes com Context, capabilities,
grants, ledger e log. R3 precisa provar a superfície de sessão em dois contextos
sem criar outro runtime ou antecipar integrações nos Products.

## Problema

Separar a sessão hospedável do loop agêntico e impedir que uma execução escolha
concessões ou Context diferentes dos fornecidos pelo Host.

## Opções

1. Duplicar runner/ledger/provider dentro da sessão: duplica autoridades.
2. Extrair Workspace compartilhado e Host API já: sem segundo consumidor real.
3. Sessão local que encaminha ao Core: prova a fronteira com baixo custo.

## Decisão

Proposta local experimental, opção 3:

- Biblioteca `AgentWorkspace` local a `apps/ecosystem-ai`, só referenciando Core.
- `WorkspaceSession` tem ID estável e Context fixado pelo Host confiável.
- Host fornece runner, grants de organização/projeto e escopos do ledger existente;
  sessão captura cópias e não concede permissões próprias.
- Submissão recebe run ID, tarefa, agente, política de limites e cancelamento;
  Context, grants do Host e orçamento não são argumentos da execução.
- Uma tentativa por sessão; sobreposição recusada e liberação em `finally`.
- Estado, eventos, artefatos, verificação e custo continuam no Runtime.
- Mesma implementação e runner em dois contextos simulados; testes por metadados
  impedem Workspace com provider/ledger/log/IO próprios ou Core dependente dele.

## Consequências

API em processo experimental 0; não é contrato público, capability ou IPC.
Não implementa UI, sessão persistente, retomada, revogação dinâmica, autenticação
do Host ou isolamento de processo. Qualquer exposição pública/compartilhada exige
contrato próprio e Extraction Review; Host real continua dependente da Fase 5.

Nenhuma nova permissão ou fonte canônica. Não muda a política de integração do
R2 nem o catálogo de capabilities; reutiliza a resolução existente no Runtime.

## Alternativas rejeitadas

Loop no Workspace e extração sem consumidor conflitam com ADR-0017 e NN-001/022.

## Referências

MANIFEST §20/21/30/39; NN-001/002/003/007/008/010/016/018/022;
ADR-0017; docs/architecture/agent-runtime.md §4/5.5/11.1;
docs/contracts/schemas/context.schema.json; P6-4; Issue #144.
