# Leitura e estudo — implementação local de CST-229–246

Pedido de 02/11/2026, issue #902: o painel abre sem nota automática e aceita
qualquer arquivo escolhido pela pessoa. O proprietário rejeitou encerrar a
frente em painel/PDF textual e reiterou que precisa dela completa. A implementação
segue na mesma branch `feat/urbe-reference-study-workspace`; integração, release e
aceite físico não foram presumidos. ADR-0047 registra dependências, schema e
trust boundaries novos. Não há mudança de versão/identidade da beta.45.

## Auditoria, alternativas e decisões

Base `672b612b`: `PinnedPanel` fixava a nota atual; split restringia fontes;
PDF raster só no Android, com frames para todas as páginas/render na UI;
extração/pesquisa paginada e revisão já existiam em `AssistantDocumentTools`.
A investigação anterior em `STUDY-READER-INCREMENT-2026-11-02.md` e Numa 2.6
§4.17 foi confrontada com a implementação. Reutilizar o extrator canônico e os
writers/editor evita um segundo runtime, editor ou banco de texto anotado.

Fontes inspecionadas: Avalonia NativeControlHost e discussão #7550 (limites de
composição, não prova de defeito na versão atual); PdfPig.Rendering.Skia
`PdfPigExtensions`/SkiaPageFactory; VersOne EpubBookRef/spine/navegação/loaders;
Web-PPT parser/slideToSvgFile; Tesseract.js createWorker/getCore/worker-script;
WebView2 Core e Android WebView/PdfRenderer. Versões/commits e consequências
ficam no ADR e no manifesto dos recursos. A UI continua Avalonia; HTML/SVG/WASM
só hospedam conteúdo do documento, OCR e o navegador solicitado pela pessoa.

## Comportamento implementado

- **Painel:** seletor inicialmente vazio, pesquisável por nome/caminho, qualquer
  extensão. Pin explícito de trecho continua. Fonte não muda ao trocar a nota.
  Fontes heterogêneas usam o mesmo FilePreviewPanel no painel e no split; um
  arquivo desconhecido continua selecionável, com fallback explícito.
- **PDF:** PdfRenderer Android; fallback PdfPig/Skia para Windows. Frames só
  perto do viewport; render serial fora da UI e descarte após trabalho pendente.
  Zoom usa tiles de até 1024 px por lado, sem bitmap da página inteira ampliada.
  Rolagem/página única, navegação, sumário e links existentes, busca continuável,
  modo textual selecionável e citação. Fonte escaneada é distinguida de texto.
- **EPUB:** spine, sumário nomeado, recursos locais, reflow, tamanho/entrelinha,
  pesquisa por lotes, navegação interna/rodapés e revisão SHA-256. Seleções têm
  offset/extensão da árvore textual; retorno e reflow preservam esse localizador.
  Não é implementação integral do padrão EPUB CFI. Scripts/ativos remotos,
  escapes de ZIP/DTD e DRM incompatível são recusados.
- **Apresentações/documentos:** Web-PPT empacotado renderiza PPTX como SVG;
  texto/busca continuam disponíveis. DOCX preserva leitura semântica de texto,
  títulos, tabela e imagens; ODT/ODP têm projeção semântica declarada. CBZ tem
  imagens originais e ordenação natural. Não se promete paginação Word/LibreOffice
  nem suporte universal a equações Office, animações ou objetos complexos.
- **Navegador:** chrome nativo (endereço/pesquisa, voltar/avançar, recarregar,
  copiar URL, abrir externamente, referência, seleção→nota/IA). Links web do
  editor podem abrir no navegador do app. Remoto não recebe acesso ao vault.
  YouTube usa player oficial e referência por instante, sem download/transcrição.
- **Mídia local:** player Android e recurso específico em streaming/ranges no
  WebView2. Instante→nota e retorno à versão verificada; codecs dependem do host.
  Nunca mapear a pasta inteira nem carregar vídeo grande como byte[] da prévia.
- **Estudo:** citação/destaque/comentário/marcador são Markdown em nota real,
  pelo writer verificado, sem substituir a seleção autoral. Lista de notas
  vinculadas é derivada das notas atuais, sem banco duplicado. Referências locais
  incluem revisão e página/parte/tempo, posição textual ou região. Fonte alterada
  bloqueia retorno exato. Falha de gravação não é anunciada como sucesso.
- **OCR:** recorte nativo da página/imagem, idioma por/eng e rotação explícitos;
  Tesseract/WASM/modelos locais, sem CDN. Texto editável para revisão, confiança
  do motor e prova visual alternável. Nota distingue estimado/revisado e conserva
  arquivo/revisão/página/região. Fórmula/figura não vira transcrição exata por
  inferência. Perguntar sobre região prepara anexo no assistente existente,
  sem enviar. Texto selecionado também pode preparar rascunho com proveniência.
- **Continuidade/lifecycle:** posição e preferências locais são separadas do
  editor e vinculadas à revisão/identidade nativa do vault. Escrita privada
  atômica/verificada, proteção de versão futura e tombstone de caminho movido.
  Fonte aberta é remapeada após rename. Hosts cobertos são suspensos; player é
  pausado; fechar libera a fonte. Painel compacto oferece abertura no leitor.

## Evidência e limites de aceite

Corpus original versionado em `csharp/tests/fixtures/readers`, com gerador:
PDFs 1/20/300/1001 páginas, PDF escaneado/protegido, EPUB 2/3/fixed, PPTX com
texto/forma/imagem/tabela, DOCX, ODT/ODP, CBZ e imagem de OCR. Testes de conteúdo
Chromium verificam PPTX e OCR sem requisições externas; teste de localizador
exercita seleção após reflow. Headless Avalonia verifica os fluxos nativos e
falhas de escrita/revisão. Não equivalem a toque/IME/consumo no moto g32 nem
execução do WebView2 no Windows.

| Formato/fluxo | Evidência local | Gate restante |
|---|---|---|
| Painel universal e split | Testes UI; seleção desconhecido/imagem/nota | Toque/IME físico |
| PDF | Raster real, viewport de 1000 páginas fake, texto→nota/retorno | Corpus visual longo nos hosts; seleção geométrica sobre raster |
| EPUB | Corpus 2/3/fixed, recursos/DRM/ZIP, localizador DOM | CFI interoperável completo, RTL/paginação e acessibilidade nos hosts |
| PPTX | Motor real em Chromium, imagem/tabela/forma/texto/slide | Decks complexos/centenas de slides e fontes nos hosts |
| DOCX/ODT/ODP | Texto/recursos semânticos originais | Fidelidade complexa explicitamente parcial |
| OCR | Motor por+eng offline, texto e word boxes | Região/rotação e métricas no Android/Windows físicos; fórmulas não certificadas |
| Web/YouTube | Contratos/fluxo nativo fake; código de host compilado | Rede real/player, cookies, login e identidade de instalação Windows |
| Áudio/vídeo | Ranges/streams testados; hosts compilados | Reprodução/codec/retomada física |
| Anotações | Markdown e writer canônicos; revisão/âncoras | Realce persistente sobre todos os leitores; cenários cruzados físicos |
| Continuidade | Schema/IO real em testes | Morte de processo/rotação nos hosts e sessão completa fonte↔nota |

Nenhum CST global foi marcado `[x]`. Persistem critérios amplos do plano que
não estão satisfeitos por abrir arquivos ou compilar: interoperabilidade CFI,
camada textual/realce geométrico sobre PDF, cobertura visual complexa Office,
sessão transversal completa, métricas físicas e revisão/CI/integração.
A exigência do proprietário permanece integral; este documento não redefine
esses critérios para chamar uma implementação parcial de completa.

Limites técnicos declarados: prévia em memória até 512 MiB; extração textual
PDF usa o orçamento existente de 60 MiB do extrator (raster pode continuar pelo
host); pacotes têm orçamento validado de descompressão. DRM não é removido.
Conteúdo web é mutável e a nota registra captura/URL/trecho, sem fingir revisão
imutável. Compilar em Linux não prova WebView2/Windows. Não houve release nova.

Comandos de reprodução dos motores: `csharp/tools/readers-build/README.md`.
Resultados consolidados e bloqueios: novo handoff crítico desta etapa; o handoff
inicial permanece histórico e não é reescrito como conclusão integral.
