# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.46
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** workflow oficial após integração e checks; preparada não significa publicada.
**Identidade Android:** app.urbe.csharp, certificado beta existente.

## Painel e área de estudo (CST-023, CST-229–246)

- Abrir o painel apresenta um seletor vazio. A pessoa escolhe o arquivo fixado, de qualquer extensão, sem abrir automaticamente a nota atual. A fonte tem rolagem independente e permanece ao mudar de nota.
- PDF com raster nativo Android e fallback Skia no Windows, páginas virtualizadas, zoom por tiles, sumário/links, pesquisa textual, seleção sobre palavras da página e referências por página/região/revisão.
- EPUB 2/3 com capítulos, recursos locais, busca, tamanho de fonte, entrelinha, margens e temas. A busca abre e seleciona a ocorrência encontrada. Localizadores próprios preservam revisão e posição textual; não são CFI interoperável.
- Leitura visual PPTX com motor Web-PPT empacotado; DOCX/ODT/ODP com projeção semântica e aviso de fidelidade; CBZ com imagens em ordem natural. Formatos desconhecidos continuam selecionáveis e oferecem fallback explícito.
- Navegador de estudo isolado do vault e player oficial YouTube. URLs/seleções e instantes geram referências em notas; sair do vídeo remove as ações de timestamp. Não há download nem transcrição implícita.
- OCR local português/inglês com modelos empacotados, recorte, rotação, texto revisável e distinção entre resultado estimado e conferido. Não garante exatidão de equações.
- A troca de capítulo invalida seleções ainda em processamento; mídia substituída depois de abrir o player não recebe um marcador como se fosse a mesma versão.
- Citações, destaques, comentários e marcadores são gravados nas notas reais pelo editor existente. Referências verificam a revisão antes de voltar à fonte.
- Posição e preferências de leitura são privadas do app, com gravação atômica/verificada. Mídia usa o player do host e streaming, sem carregar o vídeo inteiro no contexto da prévia.
- Perguntar sobre trecho ou imagem prepara rascunho/anexo no assistente existente; o envio continua explícito.

## Compatibilidade e limites reais

A frente integral da Consolidação 2.6 permanece rastreada: CFI interoperável, fidelidade Office complexa, realces persistentes sobre todos os leitores e restauração de toda a sessão Fonte↔Nota ainda não têm aceite integral. WebView2 requer o runtime Windows. Codecs e reprodução YouTube dependem do host/rede/permissões da origem. Testes headless e builds não equivalem a validação física no moto g32/Windows.

As correções do serviço da IA da beta.45 permanecem incluídas. A beta atualiza no mesmo canal, sem desinstalar. Evidências da revisão e CI constam no PR/handoff de recuperação; nenhum teste físico é presumido.
