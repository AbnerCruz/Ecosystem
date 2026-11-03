# Motores locais de conteúdo

A interface do Urbe é nativa. Os bundles daqui renderizam somente slides e OCR,
sem bridge de escrita, rede de documentos ou UI de aplicação em HTML.

`Resources/Readers/provenance.json` fixa versões, commits, licenças, tamanhos e
SHA-256. Não atualizar hashes para esconder diferença não investigada.

```bash
npm ci --ignore-scripts
URBE_WEB_PPT_SOURCE=/caminho/do/checkout/web-ppt node build.mjs
node verify.mjs
```

O checkout precisa estar limpo no commit indicado pelo script. Nunca executar
scripts de instalação/build do upstream como parte deste procedimento. Modelos
tessdata_fast por/eng são dados revisados/empacotados (gzip determinístico), não
baixados durante uso do app. Os avisos acompanham os recursos.

Para testar o conteúdo após compilar Urbe.Client.Tests (a partir de `csharp`):

```bash
URBE_READER_ENGINE_TEST_DIR=/tmp/reader-proof dotnet tests/Urbe.Client.Tests/bin/Debug/net10.0/Urbe.Client.Tests.dll -class '*ReaderEngineTests' -noColor
URBE_READER_ENGINE_TEST_DIR=/tmp/reader-proof node tests/browser/readers.mjs
```

O script usa Playwright instalado no ambiente; `PLAYWRIGHT_MODULE` permite
informar caminho de instalação já existente, e `URBE_CHROMIUM_PATH` um Chromium
conhecido. Nenhuma requisição externa é admitida pelo teste. Verifica texto,
forma, imagem, tabela e navegação PPTX; EPUB reflow/localizador; OCR e word boxes
com os modelos locais. Grava provas somente no diretório de teste. Não substitui
validação de Android WebView, WebView2, dispositivo ou matriz de formatos.

Corpus versionado: `tests/fixtures/readers`. Os arquivos são originais de teste;
`generate.py` requer python-pptx, python-docx, Pillow e ReportLab. Não usar
conteúdo privado nem livros baixados para substituir os oráculos.
