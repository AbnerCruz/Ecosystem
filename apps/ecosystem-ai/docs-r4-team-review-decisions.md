# P6-5 R4 — Pareceres auditáveis e decisão explícita do operador

Depois da execução da equipe com `--web-review-teams` (PR #409), o
Product agora registra a relação de **dois recibos distintos,
succeeded/verified**, as posições exatas das duas respostas e o hash
SHA-256 de cada resposta redigida no catálogo. Tudo acontece somente
após os runs terminarem. Exit code 0 sozinho não registra parecer.

A UI web localhost mostra um **inbox de pareceres por sessão**,
apresentando equipe, RunIds do produtor/revisor e estado da decisão.
O operador local pode **Aceitar parecer** ou **Rejeitar parecer**, com
justificativa obrigatória (até 512 caracteres). Nenhum modelo decide
automaticamente: a requisição POST é explícita, tem token CSRF,
Host/Origin seguros e um único conjunto estrito de campos.

**Aceitar parecer não equivale a aprovar tarefa, integrar código, validar
qualidade ou autorizar a escrita em arquivos.** É uma anotação humana
sobre o parecer. O registro fica `owner_accepted` ou `owner_rejected`;
a decisão é final e não pode ser repetida ou revertida pelo mesmo ID.
Para um novo ciclo, execute uma nova revisão.

## Evidências, isolamento e durabilidade

- A fonte primária permanece `LocalProjectStore` (`catalog.json`):
  os recibos e turnos são lidos novamente antes de registrar um parecer
  e antes de uma decisão humana.
- Um registro só é admitido se produtor e revisor forem membros
  **distintos da mesma equipe/projeto**, os dois runs tiverem status
  `succeeded` e `Verified=true`, e as duas respostas forem não vazias.
- Os índices de resposta e hashes ficam no registro. Uma tentativa de
  decidir um registro sem os mesmos recibos e conteúdo é recusada.
- O auxiliar `team-reviews.json` fica no **mesmo diretório privado**
  do catálogo, com schema v1, revisão, checksum e rename atômico.
  Nenhuma cópia de texto privado do modelo é gravada nesse auxiliar;
  só identificadores, hashes e a justificativa do operador.
- O arquivo é protegido contra alterações acidentais, mas o SHA-256
  **não autentica** um operador malicioso com acesso de escrita ao disco.
  A segurança depende das permissões do dispositivo e do localhost.
- O `catalog.json` v1, o roster e os workspaces não são alterados pelas
  decisões; nenhuma migração histórica foi adicionada.
- Limite de 2.000 pareceres e 2 MiB de metadados; notas pequenas,
  controle de duplicação por `reviewerRunId`.

O fluxo não cria mais gastos para registrar ou decidir; os custos da dupla
de agentes continuam os dois runs executados e limitados previamente
no Host. A decisão é possível com o servidor em modo `--web-ui`
somente leitura, sem iniciar `--web-tasks`.

`R4TeamReviewDecisionTests` valida dois runs reais mockados, hashes,
decisão única via HTTP, exclusão de campos extra e CSRF ausente,
ausência de permissões fs.write, rejeição de ações que fingem
`integrated`, corrupção de checksum, recibos inexistentes e ausência
de mutação de workspace.

## Próximo salto

A integração verdadeira de um **ChangeSet** só poderá ocorrer via
`ChangeIntegrator` do AgentRuntime.Core, com revisor independente,
verificação do estado combinado e autorização da política/Owner
quando aplicável. Este Product segue **somente leitura**; nunca cria
um `IntegrationReceipt` artificial. P6-5 e distribuição Android
continuam abertas.
