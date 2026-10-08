# Ecosystem AI — Catálogo local de projetos e sessões (R4, slice 2)

Implementação: `src/EcosystemAi.ProjectStore/LocalProjectStore.cs`.
É **código de Product**, não de `AgentRuntime.Core` nem um novo
`IEventLog`/ledger. Nenhuma dependência de outros Products.

## Funcionalidades reais

- `new LocalProjectStore(diretorioSelecionado)`: operação explícita e local,
  sem servidor, sem conta e sem gerar secret. O usuário/Host escolhe a pasta,
  que recebe `catalog.json` e um `.writer.lock`.
- `CreateProject(nome, pastaExistente)` vincula um workspace existente e preserva
  o caminho; `CreateSession` abre uma sessão do projeto.
- `AppendTurn` grava apenas textos explicitamente passados à superfície; recebe
  `user` ou `assistant`. **Não** captura contexto de execução, prompts internos,
  tool payloads ou logs do Runtime automaticamente.
- `AppendRun` registra um receipt mínimo de uma execução já concluída:
  runId, status, custo contabilizado em unidades mínimas, moeda, verificação e data.
  Não recalcula orçamento e não cria uma segunda máquina de execução.
- `Read` devolve projetos/sessões/turnos/runs de uma nova leitura, permitindo
  reabrir o Product e mostrar histórico.

## Segurança, recuperação e limites

Catálogo v1 com `schemaVersion`, `revision`, payload e SHA-256 contra
corrupção acidental. Estruturas inválidas, versão futura, IDs repetidos e checksum
incompatível **recusam escrita**; não tenta reformatar, migrar ou sobrescrever
automaticamente. Symlinks diretamente na raiz, no arquivo canônico ou no lock
também são recusados. O checksum **não é autenticação contra adversário que
consegue alterar o arquivo**.

Cada mutação toma lock exclusivo por processo, relê o estado, valida e grava
arquivo temporário com flush antes de renomear sobre o canônico. Interrupção
antes do rename não grava revisão parcial; arquivos órfãos `.tmp` são ignorados
para recuperação manual, nunca promovidos sem checagem. Limite de arquivo 4 MiB,
100 projetos, 100 sessões por projeto, 1.000 mensagens e 1.000 receipts por sessão.
Texto individual até 16.384 caracteres. Sem exclusão/compactação/merge do catálogo
neste slice.

**Privacidade:** mensagens persistidas ficam em **texto claro** na pasta
selecionada pelo usuário. O aplicativo não salva chaves de API nem
variáveis de ambiente, mas não pode automaticamente distinguir secrets
escritos pelo próprio usuário dentro de mensagens; nunca utilize o catálogo
para armazenar senhas/chaves. Proteção de dados em repouso (Keystore,
controle de acesso e/ou criptografia) exige decisão e integração com o Host
na futura interface. Os fluxos de execução de agente não são persistidos
automaticamente e continuam efêmeros até o slice de integração específico.

## Testes reais

`dotnet test --project tests/EcosystemAi.ProjectStore.Tests`

O CI valida round-trip após reinício, checksum adulterado, versão futura,
arquivo temporário órfão, concorrência de dois stores/lock, limites, ids,
recibos e symlink para fora.

A CLI agora integra o catálogo por `--catalog` opt-in, preservando a operação
sem histórico como padrão e expondo `--project-id`/`--session-id` para reabertura.
O resultado e o receipt são gravados atomica e conjuntamente, com teste
do adapter de Product. Isso **não** é memória contextual automática do modelo.

Não conclui P6-5: ainda faltam
retomada efetiva de eventos do Runtime, interface de usuário, equipes,
artefatos navegáveis, custos por provedor e experiência completa mobile.
