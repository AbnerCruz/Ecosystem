# R3: prova local do Agent Workspace em contextos diferentes

Escopo: P6-4 / Issue #144; direção ADR-0017 e plano agent-runtime §11.1.
Detalhes candidatos: [ADR-0021](../../docs/adr/0021-r3-sessao-local-do-agent-workspace.md).

`AgentWorkspace` é uma biblioteca **local ao Product**, separada de
`AgentRuntime.Core`. `WorkspaceSession` recebe o mesmo `AgentRunner` existente,
um Context, concessões de organização/projeto e referências aos escopos de
orçamento. Ela encaminha uma tarefa e um agente ao Runtime; não implementa loop
agêntico, cliente de modelo, ledger, log ou verificador próprios.

O Host é confiável e escolhe essas entradas. A sessão copia Context, grants e
orçamento na abertura, e copia o aceite e grant do agente na submissão. A API
não permite ao chamador da execução escolher outro projeto, permissões de Host
ou orçamento. Alterar as concessões/Context do Host exige uma sessão nova;
revogação dinâmica durante um run ainda não é prometida. Esta biblioteca não
torna um Host malicioso confiável nem fornece sandbox de sistema operacional.

Uma execução por sessão: sobreposição falha imediatamente; cancelamento ou
exceção libera a próxima tentativa. Cancelar antes da submissão não cria evento
nem cobra modelo. IDs de run e prevenção de repetição continuam no Runtime.
Retomada persistente de sessão, UI e seleção de provider ficam fora desta prova.

`WorkspaceSessionTests` executa a mesma implementação e o mesmo runner em dois
projetos simulados do Product: documento (`notes.write`) e código (`code.write`).
O provider reativo confere a lista de capabilities realmente anunciada; os
verificadores conferem os artefatos; os eventos registram o Context de cada run.
Os testes também tentam executar tools fora do grant/escopo/permissão, adulterar
coleções originais, sobrepor runs e executar sem orçamento.

Dentro de `apps/ecosystem-ai`, execute:

```bash
dotnet test --project tests/AgentRuntime.Tests
```

Os testes de arquitetura verificam o assembly do Workspace: só Core e biblioteca
base, sem referências a Products concretos, fornecedor, IO, ledger ou log próprio;
o Core não referencia o Workspace.

**P6-4 concluído como prova técnica delimitada:** PR #344 integra o adapter de teste externo `tests/integration/p6-lunet-host/`, compõe o Host real `LunetCapabilityHost.OpenForProject` em dois projetos com `GameId` distintos, chama a capability `text.inspect@1.0.0` existente, percorre o Runtime/Workspace existentes e verifica grants, Context, cancelamento e revogação. A exceção de sequencing foi autorizada pelo proprietário (DEC-0041 / ADD-0018 / ADR-0031). O teste não instala a IA no Lunet, não altera o roadmap local nem cria protocolo IPC ou componente compartilhado. A UX dos agentes do Lunet ainda é Fase 8; o Product dedicado Ecosystem AI é P6-5.
