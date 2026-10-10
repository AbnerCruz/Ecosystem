# P6-5 — Agentes e equipes por projeto (R4)

Com o painel do Product em `--web-ui --catalog DIRETORIO`, cada projeto
pode cadastrar **perfis de agentes** (nome e instruções) e **equipes**
(nome, produtor e revisor independente). Sem scripts, servidor remoto,
modelo em GET, gastos de ociosidade ou novas ferramentas.

No mesmo catálogo privado, o Product grava `roster.json` com versão 1,
revisão, checksum SHA-256 e escrita com rename atômico. O `catalog.json`
canônico de projetos, sessões, conversas e custos **não é modificado**
pelo cadastro de agentes/equipes: preserva schema v1 e histórico existente.
O roster é limitado a 20 agentes e 10 equipes por projeto. Campos de
instruções têm máximo de 2.048 caracteres; não inclua senhas ou tokens.
Todo formulário usa CSRF, Origin/Host loopback e valida os IDs contra o
catálogo. Ancestrais e arquivos simbólicos são recusados.

Com `--web-ui --web-tasks` previamente autorizado e configurado,
cada sessão exibe **Executor**, permitindo selecionar o assistente padrão,
um agente registrado ou o **produtor de uma equipe**. Um ID inválido ou
de outro projeto é rejeitado antes da reserva de orçamento. O executor
usa exatamente a mesma rota CLI → WorkspaceSession → AgentRunner,
o mesmo modelo/configuração do operador, quotas e **somente files.read**.
`--agent-profile-id` também permite selecionar pelo CLI um perfil
existente do mesmo projeto, sem ampliar grants. A identidade e as
instruções do agente cadastrado são carregadas do roster, não da
requisição HTTP; nenhum token ou prompt interno vai ao formulário
de execução.

Equipes representam **designação explícita de funções**. O revisor
independente fica registrado, mas não há ainda execução automática
do revisor, nem declaração fictícia de revisão concluída ou integração.
O `TeamPlan` do AgentRuntime.Core permanece o modelo para o futuro
workflow de dependências com recibo de integração; esta entrega não
cria um segundo scheduler e não marca nenhum task como integrado.

**Segurança:** um operador com acesso ao painel loopback pode escrever
instruções do agente (mudança explícita de comportamento do modelo);
não conceda esse acesso a terceiros. Permissões e orçamento são fixados
fora do formulário de tarefas; criação/seleção de agentes não permite
fs.write ou maior teto de gasto.

**Testes:** `R4WebAgentTeamsTests` valida persistência separada do
catalog.json, equipes com revisão independente, isolamento de projetos,
checksum contra corrupção, HTML escaping, POST HTTP real com CSRF e
campos estranhos, seleção da identidade do produtor no executor e
recusa de IDs arbitrários sem gastar créditos.

**Pendências P6-5:** revisão de equipe realmente executada com recibo
verificado, fluxo multietapas, gestão mais ampla de tarefas, distribuição
Android e teste manual de provider real. Esta UI segue localhost do
dispositivo que executa .NET, não é APK.
