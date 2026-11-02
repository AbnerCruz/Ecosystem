# Urbe — Leitura e Estudo: incremento de leitor imersivo (02/11/2026)

**Base:** `main` e consolidação 2.6, CST-229–246. **Escopo implementado:** parte de CST-231, CST-233, CST-238 e CST-244, sem fechar esses itens.

## Código atual auditado
- `Urbe.Client/FilePreviewPanel.cs`: a prévia modal ocupava no máximo 760px em desktop e 90% da altura em telefone; agora alterna para área integral do host, mantendo controle de fechar, tipo original de visualizador e documento aberto.
- `Urbe.Client/ContentViewers.cs`: `PdfPagesView` usa o `IPdfDocument` existente, expõe página atual e salto direto; renderização continua lazy dos bitmaps próximos à viewport.
- `Urbe.Client/MainView.AndroidBack.cs`: o primeiro Back sai do modo imersivo; o próximo fecha o arquivo, preservando o fluxo de retorno ao Explorer/editor.
- `Urbe.Core/VaultFileKinds.cs`: EPUB, PPT/PPTX/ODP e DOC/DOCX/ODT/RTF classificados com MIME adequado ao fallback externo; sem declarar um leitor interno inexistente.
- `Urbe.Client/FilePreviewPanel.cs`: formatos sem renderer interno consultam apenas metadados, evitando ler um documento de centenas de MB à toa.
- `Urbe.Android/AndroidContentViewers.cs`: o PdfRenderer nativo permanece responsável pelos pixels e pelo ciclo de vida do PDF; não houve WebView global ou mudança de permissões.
- `Urbe.Core/Assistant/AssistantDocumentTools.cs`: CST-212 já incorporou PdfPig para extração/análise sob autorização; **não duplicado** neste incremento.

## Referências técnicas conferidas
- PDF.js text layer: https://github.com/mozilla/pdf.js/blob/master/web/text_layer_builder.js
- PDF.js search controller: https://github.com/mozilla/pdf.js/blob/master/web/pdf_find_controller.js
- EPUB.js location/highlights: https://github.com/futurepress/epub.js/blob/master/src/epubcfi.js
- Readium Kotlin: https://github.com/readium/kotlin-toolkit
- W3C Web Annotation: https://www.w3.org/TR/annotation-model/
- YouTube IFrame API: https://developers.google.com/youtube/iframe_api_reference

## Testes adicionados
- `FilePreviewUiTests.FilePreview_ImmersiveMode_RestoresPreviewAndBackInTwoSteps`: interação e retomada sem alteração do arquivo.
- `ContentViewerUiTests.ContentViewer_HtmlAsAPageOrItsSourceAndPdfPagesInsideThePanel`: reforçado com navegação para página intermediária e garantia de não rasterizar todas as páginas.

- `VaultFileKindsTests` e `FilePreviewUiTests`: regressão para rótulos/MIME, aviso de suporte não disponível e abertura externa, sem modificar os bytes do original.

## Limites e próximos passos
- Este **não é** um motor PDF completo: seleção textual, OCR, busca visual, anotações, índices, EPUB, navegador, YouTube e slides continuam pendentes.
- `PdfPagesView` ainda cria um frame por página e renderiza de modo síncrono; CST-244 demanda refatoração e benchmarks em PDF extenso.
- O modo imersivo ocupa a área Avalonia, mas não oculta automaticamente as barras de sistema Android; não afirmar modo kiosk nem fullscreen do SO.
- Windows não recebeu motor adicional de PDF. Nenhuma mudança de esquema persistido, biblioteca ou licença ocorreu.
- Teste local .NET/Android não foi executado nesta sessão por ausência do SDK no ambiente. Rodar CI C# Core/Client, Android/Windows e ensaio moto g32 (Android 13), retornando para corrigir regressões antes de promover release.
- Integração somente pelo integrador governado e publicação somente pelo workflow oficial; não alterar beta.43 ainda em preparação.
