# ADR-0047 — Leitores locais, navegador isolado e continuidade de estudo

## Status

Proposto — implementação local para revisão, em resposta à exigência do proprietário de concluir CST-229–246 em 02/11/2026. Publicação do código e preparação da beta.46 explicitamente autorizadas pelo proprietário em 03/11/2026. Integração crítica segue o integrador; não presume aceite físico. Complementa ADR-0032/0034/0044 sem converter a interface geral em HTML.

## Contexto

A beta.45 tinha PDF visual apenas Android, prévias limitadas e nenhum reader EPUB/navegador de estudo. O proprietário rejeitou entregar apenas painel/PDF textual: o objetivo inclui a jornada integral de leitura, anotações em notas reais e retomada. Moto g32/Android 13 e Windows são os hosts alvo. Código e referências: `apps/urbe/docs/csharp/REFERENCE-STUDY-20261102.md` e plano Numa 2.6 §4.17.

## Problema

A jornada fonte→seleção→nota→retorno não é atendida por prévias independentes.
Precisamos de motores por formato e continuidade sem duplicar texto autoral,
sem transferir arquivos privados e sem conceder acesso ao vault a páginas.

## Opções

1. Continuar somente com prévias Android e PDF textual: preserva base, não atende o pedido.
2. Reusar PdfRenderer/PdfPig e acrescentar parsers/motores específicos de conteúdo, com adapters nativos e projeções versionadas: proposta deste ADR.
3. Trocar o app por interface web ou importar notas para banco de leitores: contradiz a direção nativa e a autoridade de dados.
4. PDF.js/EPUB.js/Readium como outros motores: referências válidas, mas requerem prova e custo adicional em hosts; não foram presumidos superiores ao parser existente.

## Decisão

- **PDF visual:** manter PdfRenderer Android. Acrescentar PdfPig.Rendering.Skia 0.1.16.6 como renderer para hosts sem PdfRenderer, com o mesmo PdfPig 0.1.16 que já extrai texto. Não há processo remoto nem outro serviço. Pixels são gerados em worker serial, frames materializados por viewport, metadados de tamanho sob demanda, cache limitado à vizinhança e descarte após o trabalho pendente.
- **EPUB:** VersOne.Epub 3.3.6, leitura por spine/recursos locais. ReaderArchive valida entradas/duplicatas/orçamento e nunca extrai arquivos do pacote no filesystem; XML sem DTD/resolver. Recursos visuais são incorporados como dados somente após resolver dentro do pacote. Scripts/formulários/frames e recursos remotos são bloqueados; DRM não é removido. HTML é apenas o conteúdo do livro, hospedado no painel nativo.
- **Browser:** Android WebView e WebView2 Core 1.0.4314.35 no Windows, dentro de NativeControlHost. Sem host objects, bridge de vault, permissões de câmera/microfone ou navegação file/content. Navegação HTTP(S) remota é iniciada pela pessoa. WebView2 mantém perfis distintos de reader local/browser remoto. Android usa origem local opaca/CSP para livros, sem cookies de um site remoto acessíveis ao livro; não se afirma disponibilidade universal de multi-profile por WebView Android. Upload por seletor do sistema, onde disponível, depende da pessoa; downloads automáticos são cancelados/não implementados.
- **Localização/revisão:** página PDF e parte/spine/slide/instante com SHA-256 do original. Notas autorais, destaques/comentários/citações permanecem em Markdown real; nenhum segundo banco de texto anotado. Referências antigas não reancoram silenciosamente quando a revisão muda.
- **Continuidade:** metadados de leitura ficam em arquivo privado do app adjacente ao estado do Explorer, com schema explícito versionado e identificação por hash da identidade nativa do vault + caminho; não se escreve o vault só por ler. Renomear/mover remapeia posições; hash divergente não restaura localização como exata. Versão futura/corrupção preserva o arquivo e impede regravação; escrita serial/atômica e verificada. Nunca armazenar credenciais ou conteúdo remoto privado nessa projeção por simples navegação.
- **IA:** ações explícitas de estudo preparam fonte/trecho em rascunho do assistente existente. O botão não transmite o documento automaticamente e não amplia permissões. O envio continua pela pessoa e pelo runtime canônico.

## Alternativas rejeitadas

Rejeitar renderizador PPTX artesanal aproximado depois de confrontar objetos de
slide com Web-PPT; não rotular texto extraído como fidelidade visual. Rejeitar
CDN no OCR, remoto implícito, bridge genérica, mapeamento de pasta do vault e
bitmap de página inteira em zoom alto. Não incorporar motores AGPL sem revisão
específica. Não adotar novo runtime de IA nem banco de anotações autorais.

## Licenças e alternativas

PdfPig.Rendering.Skia é Apache-2.0; depende de filtros PdfPig com suas licenças/avisos e SkiaSharp/HarfBuzz. VersOne.Epub 3.3.6 é Unlicense, conforme nuspec e fonte. WebView2 segue a licença do SDK Microsoft e exige runtime instalado no Windows; não se distribui um browser copiado. Conservar licenças/NOTICE de dependências efetivamente empacotadas; inventário de release deve conferir transições nativas e tamanho do APK. KOReader/AGPL não foi incorporado. EPUB.js/PDF.js foram referências de mecanismo; a alternativa de introduzir outros parsers JS foi rejeitada por duplicar PdfPig e aumentar o bundle sem prova necessária nesta etapa.

## Consequências

Mudança **crítica** por dependências/licenças, trust boundaries e novo schema privado de continuidade; revisão/CI e integração governada continuam obrigatórios. Prototipar/compilar não significa validar WebView2 em Windows nem lifecycle Android. Corpus e testes devem cobrir texto/imagem/fórmula, recursos inválidos, links, zip bomb/XXE, DRM, revisão alterada, cancelamento e reabertura. Não marcar a frente completa enquanto seus critérios e a matriz de formatos não tiverem evidência.

## Referências verificadas

- https://github.com/BobLd/PdfPig.Rendering.Skia — `PdfPigExtensions.cs`, csproj, LICENSE/NOTICE; render por escala e dimensões reais, baseline visual varia com fontes do host.
- https://github.com/vers-one/EpubReader — `EpubBookRef`, `ContentReader`, `SpineReader`, loaders local/remoto; usar somente referências locais e pré-validar o container.
- https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Controls/NativeControlHost.cs — lifecycle/composição.
- https://learn.microsoft.com/microsoft-edge/webview2/ — CoreWebView2Controller, navegação, perfis, permissões e recursos solicitados.
- MANIFEST NN-001/002/009/010/011/016/017/018/022; CST-229–246; issue #902.

## Conteúdo visual, OCR e mídia incorporados nesta proposta

Web-PPT `0.8.0-beta.1`, commit `88c8df92ee853fa2dc45a413fa38316527b20025`
(MIT), com fflate `0.8.2`, é compilado somente como motor de conteúdo de slides.
A interface/navegação/ações continuam Avalonia. Pacote PPTX é validado antes da
injeção como bytes; script confiável é autorizado por enum interno e nonce CSP,
nunca pelo documento. Sem bridge e sem ativos externos. Não executar animações,
macros ou scripts autorais. Parser semântico C# segue servindo busca/texto.

OCR local acrescenta capacidade que o extrator CST-212 declarava indisponível:
Tesseract.js `6.0.1`, core `6.1.2` (WASM LSTM, Apache-2.0), modelos `por`/`eng`
de tessdata_fast commit `87416418657359cb625c412a48b6e1d6d41c29bd`
(Apache-2.0). Worker/core/modelos são empacotados; nenhuma busca de CDN em runtime.
Worker usa blobs locais, fetch restrito aos dois modelos incorporados e CSP
sem rede externa. Importações não ganham filesystem, vault ou mensagens de host.
Resultados são derivados em memória, com confiança/caixas; usuário revisa antes
de classificá-los como revisados. Fórmulas/figuras não são reconstruídas como
exatas por OCR. Seleção de região conserva revisão e coordenadas da prova visual.
Idioma e rotação são escolhas explícitas. IA usa o anexo canônico pendente do
assistente existente, só após gesto específico, sem envio automático.

PDF ampliado usa tiles de até 1024 px por lado no viewport, sobre uma base de
resolução moderada. Android PdfRenderer aplica matriz/recorte; Skia desenha a
SKPicture diretamente no tile. Zoom não aloca a página inteira em sua resolução
ampliada. Uma fila serial por documento evita corrida com Dispose. Documentos
não são materializados por inteiro como bitmaps.

Áudio/vídeo Windows recebe apenas o stream do arquivo explicitamente aberto,
via recurso HTTPS sintético exato, com ranges limitados/validados. Sem mapear
pasta do vault no browser. Android mantém player do sistema com URI SAF específica.
Referências temporais verificam revisão por SHA-256 em streaming. Reproduzir
não importa conteúdo nem gera transcrição.

Build reproduzível, versões, hashes e licenças dos recursos:
`apps/urbe/csharp/tools/readers-build/` e
`src/Urbe.Client/Resources/Readers/provenance.json`. Não ampliar essa lista sem
nova revisão de licença/custo/tamanho e nova evidência dos testes de conteúdo.

YouTube usa o player oficial e identificação do aplicativo conforme
https://developers.google.com/youtube/terms/required-minimum-functionality
(seção API Client Identity and Credentials, consultada 02/11/2026). Android
informa o PackageName real; desktop usa a identidade estável proposta
`urbe.desktop`, que ainda precisa ser conciliada com a identidade de instalação
Windows antes de declarar o gate desse host aprovado. Não há download de mídia,
remoção de anúncios, controles sobrepostos ou transcrição fabricada.
