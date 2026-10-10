# Urbe C# — somente Android e Windows (beta 2.0.0-beta.4)

**Direção do proprietário (09/10/2026):** não criar ou distribuir uma versão Web/PWA do cliente C#. Android é prioridade; desktop/Windows vem depois. O MAUI Blazor Hybrid renderiza os componentes em uma WebView interna, não em um site com backend. O vault Android é uma pasta real, selecionada pelo usuário.

**Release Android:** versão `2.0.0-beta.4`, identificador `app.urbe.csharp`, código `2000004`, tag `urbe-csharp-v2.0.0-beta.4`. Beta.4: criar nota/casa diretamente tocando no terreno, criar bairro na própria Cidade e zoom 80–160% com controles móveis; os dados continuam gravados na pasta física pelo host SAF. A cada versão, incrementar `ApplicationDisplayVersion` e `ApplicationVersion` em `src/Urbe.App/Urbe.App.csproj`. Publicação do APK + SHA256SUMS no GitHub Releases por `.github/workflows/urbe-csharp-mobile-release.yml`, com assinatura beta fixa e testes C#.

**Verificação:** `dotnet restore Urbe.Portable.slnx && dotnet test --project tests/Urbe.Core.Tests -c Release`. Build Android: `dotnet build src/Urbe.App/Urbe.App.csproj -c Debug -f net10.0-android -p:UrbeBuildTarget=net10.0-android`. Windows usa `net10.0-windows10.0.19041.0`. Não há build ou release C# para Web.

**Dados:** testar a primeira beta em pasta vazia e confirmar abrir/editar/salvar/reabrir no Android físico. A identidade nova não sobrescreve a instalação JS 1.8.4-beta.

---

**Arquivo histórico de implementação UC-8 a UC-18:** as menções antigas a Urbe.Web/WASM/PWA abaixo não refletem a direção atual do produto.

# Cliente C# do Urbe — UC-8 a UC-16

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


### UC-15 — layout livre

A quarta fatia porta o domínio puro de `pages/free.js`: árvore de peças,
normalização de tipos/ids/estilos, breakpoints tablet/mobile, CSS escopado por
seção e renderização de containers, títulos, Markdown, imagens, botões, listas,
citações, vídeo, nota do vault, HTML avançado, tabela, código, fórmula, selo,
embed HTTPS e quebra de página. A normalização limita profundidade/quantidade,
recusa medidas/cores inválidas e limpa classes/CSS/URLs antes da renderização.

### UC-15 — conversão e arquivo compacto

A quinta fatia porta o conversor de blocos fechados para a árvore livre e a
compactação de defaults do motor de páginas. `PageFreeConversion` cobre os 13
tipos declarados convertíveis pelo cliente JS e resolve capítulos/notas contra
`DocumentStore` sem IO novo. `PageCompactor` remove somente defaults que o
`PageNormalizer` sabe restaurar e preserva chaves desconhecidas, inclusive em
meta/props/style, para manter REQ-049. A transformação é em memória: não grava
vault nem executa migração física.

## UC-16 — edição matemática pura (primeira fatia)

`MathEditing` fornece scanner compartilhado com `MarkdownEngine`, posição da
fórmula sob o cursor em UTF-16, recuperação dos delimitadores, snippets e
completação. `Commands`, `Symbols` e `Templates` portam o catálogo congelado do
cliente JS como coleções imutáveis. Não acessa DOM, filesystem, host ou rede.

A suíte compara 33 cenários de varredura e todas as posições do cursor, 91
casos de autocomplete e 172 snippets com o módulo JS. O corpus suplementar em
`tests/fixtures/math-editing.json` registra o SHA-256 da fonte; não modifica os
297 casos de aceite UC-2. `node tests/math-oracle.mjs --check` verifica catálogo e
expectativas executando a fonte congelada; `--write` é regeneração deliberada,
cujo diff precisa de revisão. A suíte C# também verifica o SHA-256 da fonte e todas as expectativas.

A biblioteca tipográfica ainda não foi escolhida (ADR-0025 / UC-4). Este passo
não implementa renderização KaTeX, macros, diagnósticos LaTeX, export visual ou
UI/seleção/teclado. UC-16 permanece aberta; UI fica na UC-18.

Investigação UC-16: [renderer C#/SVG e corpus real](../docs/csharp/UC16-RENDERER-INVESTIGATION.md). Execute `node tests/math-renderer-probe.mjs` com .NET 10 e Chromium para reproduzir o experimento isolado; isso não adota suas bibliotecas no produto.


### UC-15 — shell, temas e modelos

A sexta fatia fecha a lacuna do oráculo `pages.mjs` que não estava coberta pelos
renderers de blocos: `PageThemeCatalog` porta os dez presets e a tipografia;
`PageRenderer.Render` passa a emitir o documento HTML completo com tema,
metadados, navegação, rodapé, estilos de seção e CSS de impressão de livro; e
`PageTemplateCatalog` representa os 13 modelos embutidos e as variantes
`simple`/`skeleton`. Tudo continua puro e sem DOM/WebView/filesystem.
Studio, preview editável e ferramentas de IA continuam nas UCs de UI/IA.


### UC-16 — renderer SVG C# (proposta crítica)

`MathRenderer.RenderSvg` usa CSharpMath.Rendering com um `ICanvas` SVG escrito
no próprio Urbe. Não há VectSharp, Skia ou interop matemático em JavaScript.
Falhas de TeX viram diagnóstico, o texto original é preservado e o SVG exportado
é formado por paths/linhas/retângulos autocontidos. A dependência permanece
sujeita à autorização crítica do ADR-0012.

## UC-18 — Código em linha na Fonte (REQ-090, primeira fatia)

A barra do modo **Fonte** (também presente no **Dividido**) inclui
**Código em linha**. Com uma seleção de uma linha, envolve somente o
trecho em crases; pressionar novamente sobre a mesma seleção remove o
envoltório. Sem seleção, insere duas crases sem texto de reserva e deixa
o cursor entre elas; pressionar novamente remove o par vazio. Trechos
com crases recebem delimitador de tamanho apropriado. Uma seleção de
múltiplas linhas é recusada sem alterar o texto — código em bloco é
outro comando.

A operação é calculada em `Urbe.Core/InlineCodeEditing` usando
offsets UTF-16, compatíveis com o cursor do navegador. Um módulo JS
mínimo lê/restaura somente a seleção do `textarea`, sem lógica Markdown,
persistência ou segundo estado canônico. O fluxo mantém o histórico da
`WorkspaceSession`. Há testes de domínio e smoke Web real para seleção,
alternância e cursor.

Esta fatia não habilita a bolha/formatação sob o cursor no modo Visual,
nem conclui a totalidade de REQ-090, REQ-095 ou G-C3. Persistência
física dos hosts C# permanece pendente.

## UC-18 — modelos de nota (REQ-093)

No Editor C#, **Salvar como modelo** clona a nota Markdown atual para
`Modelos/` (sem alterar o original). Esse diretório contém **notas comuns**:
`Modelos/Reunião.md` pode ter, por exemplo, `# {{Assunto}}` e
`Responsável: {{Pessoa}}`. O Explorer oferece **Modelo da nota** ao criar
uma nota e pede o valor de cada campo uma única vez, mesmo que ele apareça
várias vezes. Campos sem valor produzem texto vazio; sintaxe não reconhecida
permanece literal. O nome/caminho da nota criada é independente do modelo.
É possível editar diretamente a nota-modelo como qualquer outro Markdown.

A expansão preserva exatamente a fonte fora dos marcadores: quebras CRLF,
frontmatter, links, códigos e espaços não são normalizados. Se o modelo
desaparecer, mudar de campos durante o formulário, contiver mais de 32
campos distintos, ou o destino não for válido, a criação é recusada antes
de modificar o documento. Os testes do Core exercitam fidelidade, valores
literais e falhas fechadas.

**Limite da UC-18:** o Explorer/Editor C# ainda usa uma sessão em memória.
A criação de modelos e notas usa `WorkspaceSession`/`DocumentStore`
canônicos; a gravação física e a recuperação após reiniciar o host dependem
do adapter planejado de persistência. Não usar este cliente experimental
para notas reais. REQ-093 e G-C3 não são declarados concluídos antes de
persistência e validação visual/toque no dispositivo.

## UC-18 — painel de referência (REQ-091)

No Editor, **Fixar nota em painel** guarda uma cópia de consulta da nota atual.
No modo Visual/Dividido, **Fixar trecho** guarda o bloco correspondente. É possível
consultar várias referências, remover uma ou fechar o painel; no celular ele fica
no topo, no desktop largo na lateral, com rolagem própria. A edição e o histórico
continuam sobre o documento canônico.

As referências são snapshots da sessão: editar/mover a nota não reescreve a cópia,
e abrir outra nota não a fecha. Recarregar o cliente ou carregar outro vault limpa
o painel. Seleção livre dentro de um bloco e seleção rich HTML ficam pendentes
na UC-18; esta fatia não representa fechamento integral do REQ-091 ou G-C3.
