# P6-5 R4 — Snapshot visual local do Ecosystem AI

Fatia de Product, **não** novo Host, Agent Runtime, servidor ou interface Android definitiva.
Fonte única: `LocalProjectStore.Read()` (projetos, sessões, mensagens explicitamente
salvas e receipts). O HTML é uma **projeção descartável somente leitura**, não outro
formato de dados canônico. Não importa ou grava alterações no catálogo.

## Como gerar

Exige .NET 10, um catálogo já criado por `--catalog` em execuções anteriores,
e uma pasta de saída existente **fora** do catálogo e dos workspaces registrados.

```bash
cd apps/ecosystem-ai
dotnet run --project src/EcosystemAi.Cli -- \
  --export-html --catalog /diretorio/privado/catalogo \
  --output /diretorio/privado/exports/ecosystem-ai.html
```

Abra o HTML em qualquer navegador, inclusive no celular depois de transferi-lo
manualmente. O arquivo é funciona offline de forma independente e adapta a largura
a telas pequenas: links internos para projetos, sessões expansíveis, mensagens
com origem e horário, estados de runs, verificação, custos registrados
por moeda (unidades mínimas, não fatura) e indicativo de estimativa.

**Não há** API de chat, criação de tarefas, edição, publicação, sincronização,
retomada de runs nem UI de agentes/equipes neste snapshot. Alterações no
catálogo exigem **nova exportação com outro nome**. Nenhuma chamada HTTP,
script, asset remoto ou configuração de provedor participa desta operação.

## Segurança, consistência e limites

- A exportação é **opt-in** e não exige `ECOAI_API_KEY` nem `--project`.
- O catálogo ausente não é criado em modo exportação; conteúdo inválido ou
  checksum incorreto continua falhando pelo `LocalProjectStore`.
- Todas as strings de usuário (nomes, mensagens, evidências, IDs) são
  HTML-encoded; HTML possui política CSP de bloqueio de scripts/recursos
  externos e não injeta conteúdo em CSS.
- Não exporta o caminho local do workspace. A pasta de saída não pode estar
  dentro de um workspace registrado ou do diretório do catálogo; ancestrais
  simbólicos da saída são recusados.
- Cria arquivo temporário no mesmo diretório, sincroniza no disco e faz rename
  sem sobrescrever arquivo existente.
- **Atenção:** mensagens e evidências podem conter informações privadas e
  ficam em **texto claro no HTML**. O usuário controla onde guarda e com quem
  compartilha o snapshot. Não há redator universal de segredos nem criptografia.
- Não usa o journal completo de eventos. Custos são os receipts gravados no
  catálogo, potencialmente estimados; não conferem billing externo.
- É um primeiro fluxo visual de leitura, não substitui decisão futura sobre
  UI mobile-first, secret store, edição, tarefas e equipes.

## Testes

`dotnet test --project apps/ecosystem-ai/tests/AgentRuntime.Tests`

A suíte `R4VisualSnapshotTests` verifica o round-trip pela CLI sem rede,
responsividade e saída, encoding contra injeção HTML, não vazamento do path,
preservação da revisão canônica, rejeição de catálogos ausentes, sobrescrita,
saída dentro do sandbox e aliases simbólicos.

Escopo: Issue #354 (P6-5). Handoff no diretório canônico de governança.


## Auditoria opcional e artefatos do Runtime

Para enriquecer o HTML com dados **verificados por replay do journal**, especifique
também a pasta de runs configurada durante execuções anteriores:

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --export-html --catalog /diretorio/privado/catalogo \
  --journal /diretorio/privado/runs \
  --output /diretorio/privado/exports/auditoria.html
```

O relatório inclui, em seções expansíveis por run: estado reconstruído do
`RunState.Replay`, verificação, agente, modelo, passos, quantidade de chamadas
de ferramentas e **índice dos artefatos referenciados**. Somente nome-base e
tipo dos artefatos são mostrados; diretórios, argumentos de ferramenta,
payloads, resultados, textos internos e conteúdo dos arquivos ficam de fora.
Os artefatos são **referências**, não anexos navegáveis ou uma cópia dos arquivos.

O journal é obrigatório para esse modo. Cada run precisa existir no catálogo;
runs sem arquivo de journal são rotulados como ausência de evidência, e
não como sucesso. Um journal presente mas adulterado, inválido ou acima do
limite é **recusado**, nunca ignorado ou reparado. Divergência entre receipt
e estado reconstituído é sinalizada explicitamente. Máximo de 250 runs
distintos por exportação auditada para limitar memória/trabalho.

A pasta do journal não pode estar dentro de um workspace acessível ao agente
nem usar ancestrais simbólicos; a saída continua fora do workspace e do
catálogo. A operação não chama modelos, não lê conteúdo dos arquivos
referenciados, não cria sessões e não grava no catálogo ou no journal.
O JSON original do journal permanece a fonte dos eventos e o Runtime permanece
a autoridade do estado.

Esta entrega **não implementa** a navegação/abertura real dos artefatos,
gestão de tarefas nem UI interativa. Essas etapas permanecem em P6-5.


## Pré-visualização textual opcional de artefatos (P6-5)

Por padrão, mesmo com `--journal`, o relatório mostra **somente nomes e tipos**.
Para visualizar no celular os textos dos artefatos *atualmente presentes no disco*,
o usuário precisa consentir explicitamente com `--embed-text-artifacts`:

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --export-html --catalog /pasta/privada/catalogo \
  --journal /pasta/privada/runs \
  --output /pasta/privada/exports/painel-com-textos.html \
  --embed-text-artifacts
```

O mesmo `RunState.Replay` valida a proveniência das **referências** de artefatos.
A leitura usa `FileSandbox`, dentro do workspace canônico vinculado ao projeto,
e recusa symlinks em arquivo e diretórios. Apenas extensões textuais declaradas
(`.md`, `.txt`, `.json`, `.csv`, `.cs`, `.html`, `.xml`) entram no preview:
até 16 KiB por arquivo, 128 KiB no total e no máximo 16 arquivos por relatório.
Arquivos ausentes, com texto inválido (UTF-8), binários, grandes ou sem permissão
continuam com a referência, sem incluir conteúdo. Cada texto é escapado para
HTML, colocado dentro de `<details>` e `<pre>`, e não executa scripts.

**Privacidade:** esta opção insere o conteúdo completo dos arquivos selecionados
no HTML em texto claro. O HTML pode conter segredos ou outros dados privados;
não o publique. Não há sanitização confiável de segredos arbitrários, nem
criptografia. O recurso não tem efeito no catálogo, journal ou arquivos de origem.

**Integridade:** o conteúdo visualizado é o **estado atual do arquivo** na hora
da exportação; o journal comprova que uma referência foi registrada, mas
**não** comprova que o texto não foi modificado depois do run. Não se trata
de snapshot histórico imutável nem de aprovação de tarefas. O usuário pode
abrir e fechar previews pelo navegador, inclusive em celular, sem servidor
ou código JavaScript. Para um texto atualizado é necessário gerar novo HTML.
