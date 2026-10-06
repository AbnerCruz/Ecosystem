# Cliente C# do Urbe — UC-8 a UC-15

Base de composição aprovada por DEC-0035-A / ADR-0025. O roadmap e os gates
continuam em [`../docs/csharp/ROADMAP.md`](../docs/csharp/ROADMAP.md).
A tela inicial identifica um cliente em construção; os hosts ainda não abrem nem gravam vaults. UC-9 adiciona ao Core apenas a leitura pura, em memória, dos bytes de um vault.

| Projeto | Responsabilidade | Dependências locais |
|---|---|---|
| Urbe.Core | Domínio puro .NET; identidade estável e leitor de vault UC-9 | nenhuma |
| Urbe.UI | Componentes Razor e estilos usados pelos dois hosts | Core |
| Urbe.Web | Bootstrap WASM/PWA estático | UI |
| Urbe.App | Composição MAUI Blazor Hybrid Windows/Android | UI |
| Urbe.Core.Tests | Identidade, limites arquiteturais e composição | Core |

## Leitor de vault — UC-9

`VaultReader.Read` recebe um conjunto de `VaultFile` em memória e devolve um snapshot
sem acessar filesystem nem escrever, apagar, migrar ou criar backup. Ele lê mapas 1.x,
sidecars v1/v2 com precedência v2, identidade e journal recuperável; bytes físicos
continuam disponíveis sem mutação mesmo quando o journal projeta um estado efetivo.

Formato futuro do vault torna o snapshot globalmente somente leitura. Formato futuro
de mapa, sidecar, tema ou página protege apenas o artefato correspondente. Journal
v2 futuro não cai para v1. A suíte `VaultReaderTests` executa as 12 fixtures canônicas
de `tests/fixtures/vaults/` e casos negativos de paths inseguros, duplicatas,
precedência, corrupção e recuperação. UC-10 adiciona o caminho inverso como **plano puro de mutações**: backup 1→2,
sidecars v2, journal transitório, identidade, vault.json, restauração e GC. O Core
ainda não executa IO; hosts futuros aplicarão a sequência ordenada de operações,
permitindo testar estado final e atomicidade antes de tocar arquivos reais.

`Urbe.Portable.slnx` compila em qualquer SO com .NET 10; `Urbe.Native.slnx`
exige workloads MAUI. Interfaces de plataforma serão adicionadas quando houver
operações concretas, sem pastas ou contratos vazios. Core/UI não conhecem hosts
nativos ou outros produtos do plano de controle. Nenhuma capacidade de filesystem,
credenciais, IA ou plugins foi ativada.

## Verificação reproduzível

Na pasta `apps/urbe/csharp`, com SDK .NET 10 e Node 22:

```sh
dotnet restore Urbe.Portable.slnx
dotnet build Urbe.Portable.slnx -c Release --no-restore
dotnet test --project tests/Urbe.Core.Tests -c Release --no-build
dotnet publish src/Urbe.Web -c Release --no-restore -o artifacts/web
npm ci --prefix ..
npx --prefix .. playwright install chromium
node tests/web-smoke.mjs artifacts/web/wwwroot
```

O smoke serve os arquivos estáticos publicados, abre a UI real em Chromium,
verifica o ID do Product e a RCL, recarrega offline em `/` e `/preview/` e verifica
que um escopo não apaga o cache do outro. É prova de bootstrap, **não** aceite
completo UC-23/UC-29, instalação PWA ou validação física.

Android (JDK/SDK Android compatíveis com o workload):

```sh
dotnet workload install maui-android
dotnet build src/Urbe.App/Urbe.App.csproj -c Debug -f net10.0-android -p:UrbeBuildTarget=net10.0-android
```

Windows (Windows + WebView2 + workload):

```powershell
dotnet workload install maui-windows
dotnet build src/Urbe.App/Urbe.App.csproj -c Debug -f net10.0-windows10.0.19041.0 -p:UrbeBuildTarget=net10.0-windows10.0.19041.0
```

`urbe-checks` executa os checks JS existentes, a solução portátil, testes, publish
estático/smoke e os dois builds nativos, inclusive no ref combinado passado pelo
integrador. Nenhum job publica, assina ou cria release.

## Teste manual desta base

1. Rode `dotnet run --project src/Urbe.Web`; abra a URL indicada.
2. Confirme o título Urbe e o aviso de cliente em construção.
3. Em viewport horizontal de celular, confirme leitura sem rolagem horizontal.
4. Para experimentar offline, use o build **publicado** servido por HTTP local;
   o service worker de desenvolvimento deliberadamente não oferece offline.
5. Não use esta base para notas reais: o leitor UC-9 ainda não está ligado aos hosts e escrita/migração pertencem a UC-10.

Os metadados `app.urbe.csharp.dev` / `Urbe Dev` / `0.0.0` são privados do build
de desenvolvimento; não alteram a identidade ou versão pública, assinatura ou
canais do produto existente. `package.json` continua a autoridade da versão do
Product até UC-32. UC-27 define os metadados de distribuição. Não há pacote de
release nesta etapa. UC-24/25/29 ainda precisam de instalação e aparelho reais.

Bootstrap e worker PWA derivam do template Microsoft .NET 10 (MIT); o worker usa
o próprio escopo como namespace do cache. JavaScript aqui só conecta APIs Web,
sem regras de domínio ou autoridade de dados.


## UC-11 — export/import portátil

UC-11 mantém o boundary do Core: `VaultExportManifest` define o contrato
`urbe-export.json` v1, hashes SHA-256, verificação e política do estado portátil;
`VaultArchive` empacota/inspeciona ZIP com `System.IO.Compression`, exclui journals
transitórios e rejeita traversal, caminhos absolutos e duplicatas normalizadas.
Nenhum download, picker, filesystem, localStorage ou host é acessado pelo Core.


## UC-12 — documentos, artefatos e conhecimento

UC-12 inicia M2 no `Urbe.Core` sem adicionar superfície de UI. `ArtifactModel`
é a fonte única C# para classificação de arquivos, editabilidade, linkabilidade e
nomes seguros. `DocumentStore` mantém documentos imutáveis, identidade, revisão
e parsing derivado de frontmatter, wikilinks e tags. `KnowledgeIndex` projeta
links/backlinks, tags, tokens, busca local e estatísticas e se reconstrói nas
mutações do store.

O domínio pode ser preenchido diretamente por `VaultSnapshot`, preservando os
IDs já resolvidos pela camada do vault. Mundo/bairros, Markdown Visual,
páginas/composições, quick-open/UI e integração de hosts permanecem nas UCs
seguintes.


## UC-13 — projeção do mundo e bairros

UC-13 porta para o Core a camada puramente semântica do mundo: `WorldStableIds`
reproduz os IDs determinísticos `reg_*`/`ast_*` da 1.x e
`WorldProjection` separa documentos canônicos de posição, sprite e regiões do
`mapa.json`. Campos desconhecidos são preservados; mapas futuros/corruptos
ficam somente leitura. Terreno, ruas, vida, AquariumWorld, câmera, desenho e
interação continuam fora do Core nesta etapa.


## UC-14 — Markdown e round-trip Visual

UC-14 porta para o Core os contratos puros de `src/editor/markdown.js` e
`src/editor/visual.js`. `MarkdownEngine` renderiza Markdown para HTML,
preserva matemática como átomos `umath` e aplica a mesma sanitização histórica.
`VisualMarkdown` usa um parser de fragmento HTML próprio, BCL-only, para
serializar o subconjunto do editor de volta a Markdown sem DOM, WebView ou JS.
A paridade é exercitada diretamente pelos goldens históricos do cliente legado.


## UC-15 — páginas e composições

UC-15 inicia o port do domínio de páginas sem ligar UI ou filesystem. `PageDocument`
lê `.page.json` de forma lossless, preserva chaves desconhecidas e recusa escrita de
versões futuras/corruptas. `PageRenderer` fornece renderização semântica pura dos
blocos já portados usando `MarkdownEngine` e `DocumentStore`, sem DOM, WebView ou
JavaScript como autoridade de domínio.

`CompositionFile` lê o sidecar legado e `CompositionPageConverter` transforma
`order/sources` em seções `note` determinísticas. Todo dado legado que ainda não
tem equivalente exato em páginas é mantido em `meta.legacyComposition` e campos
`legacyComposition*`; a conversão não grava nem apaga nada. A ligação com backup,
migração real e remoção da UI legada só poderá ocorrer em etapa crítica de dados do
usuário, conforme REQ-038/REQ-105.


### UC-15 — blocos ricos

A segunda fatia amplia o renderer puro de páginas para os blocos de conteúdo e
estrutura do cliente legado (destaques, cartões, galeria, vídeo, depoimentos,
números, timeline, FAQ, CTA, colunas, planos, contato, countdown, código,
divisores e blocos editoriais simples de livro). URLs passam por `SafeUrl`,
conteúdo textual por escape/Markdown e o bloco avançado `html` permanece raw
por paridade explícita com o produto atual.


### UC-15 — contexto entre seções e livro

A terceira fatia acrescenta um `PageRenderPlan` puro que calcula âncoras únicas,
TOC, partes, capítulos e capítulos derivados de uma pasta antes da renderização.
`PageBookRenderer` usa esse plano para capa, folha de rosto, sumário de página,
sumário de livro, partes e capítulos, preservando a numeração do cliente JS e
sem introduzir estado de UI/host. Cabeçalhos internos de capítulos são
deslocados semanticamente para manter a hierarquia do livro.
