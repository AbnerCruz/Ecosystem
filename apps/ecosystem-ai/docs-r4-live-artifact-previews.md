# P6-5 R4 — Prévia de artefatos textuais na interface web local

O painel C# em `127.0.0.1` pode mostrar o texto **atual** dos artefatos
referenciados por runs auditados. A autorização é explícita: só o operador do
processo pode iniciar a UI com `--embed-text-artifacts`.

```bash
dotnet run --project src/EcosystemAi.Cli -- \
  --web-ui --catalog /dados/privados/catalogo \
  --journal /dados/privados/runs --embed-text-artifacts --port 8765
```

No navegador do **mesmo dispositivo**, abra `http://127.0.0.1:8765/`.
O quadro de agentes/runs/artefatos da PR #403 continua funcionando sem a flag,
mas nenhum byte de conteúdo de arquivo vai para o HTML nesse caso.

A implementação reutiliza diretamente `CliVisualRunDetails.ReadAsync` e
`CliArtifactTextPreview.Read` da PR #396. Não grava arquivos, duplica
conteúdo no catálogo, usa outro runtime ou cria permissão de leitura nova.
A renderização faz escaping de HTML e usa `<pre>` com quebra de linhas
e rolamento vertical; scripts e HTML de um arquivo são texto literal.

Proteções do leitor existente: somente arquivo referenciado pelo journal de
um run salvo no catálogo, dentro do workspace autorizado, sem symlink,
sem traversal, extensão textual permitida e UTF-8 estrito, sem NUL;
limite de **16 KiB por arquivo**, **128 KiB no total por GET** e no máximo
**16 prévias por consulta**. Imagens/binários/arquivos muito grandes não
geram prévia. Nenhum conteúdo é declarado imutável ou comprovadamente igual
ao arquivo na data da execução — trata-se do estado **atual no disco**.

A opção `--embed-text-artifacts` exige `--journal`; as outras regras
da UI se mantêm: somente loopback, CSP sem JS, Host/Origin/CSRF,
sem modelo/execução, sem exportação remota. O conteúdo pode incluir dados
privados — ative a flag apenas num dispositivo de confiança e encerre o
servidor quando não estiver usando.

Testes `R4WebArtifactPreviewTests`: HTTP local real com ProductStore,
RunJournal e arquivos atuais, flag desativada/ativada, HTML malicioso escapado,
arquivos grandes rejeitados, catálogo e arquivos intocados, e flags inválidas.

A P6-5 ainda precisa de perfis de agentes/equipes, criação de tarefas e
disparo supervisionado com orçamento. Esta PR não fecha a fase nem produz APK.
