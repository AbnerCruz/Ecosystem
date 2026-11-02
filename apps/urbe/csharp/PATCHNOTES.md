# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.43
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial, após os gates do repositório; versão preparada não significa publicada
**Identidade Android:** app.urbe.csharp e certificado beta existente

## Leitura documental e imagens (CST-212, PR #886)

- Agentes podem inspecionar PDFs autorizados do vault ou anexos da conversa atual: quantidade de páginas, SHA-256 da fonte e inventário paginado de texto, imagens e desenhos vetoriais.
- Leitura de trechos com página e cursor de caracteres; busca literal em livros em lotes de até 150 páginas, com retomada da posição para ocorrências repetidas.
- Análise visual de uma página do PDF por vez (texto dentro de imagens, fórmulas, tabelas e gráficos) usando modelo multimodal configurado **somente com autorização explícita de envio**; a interpretação não é garantia de exatidão.
- Inspeção local de imagens PNG/JPEG/WebP com validação de assinatura e hash; análise por IA multimodal exige autorização e provedor compatível.
- Limites atuais: até 60 MiB por PDF, 16 MiB por imagem de origem. **Ainda não existe OCR local offline, ingestão universal de arquivos nem certificação de fórmulas.** Camadas textuais e conteúdo visual exigem tratamento diferente; imagens e fórmulas em páginas mistas podem necessitar revisão visual.

## Funcionalidades herdadas da beta.42

- Editor Fonte/Visual e compositor com IME multilinha e Return; progresso nativo de checklists, numeração de linhas e operações de desfazer/refazer.
- Publicações baseadas em notas, títulos e blocos ^id, composição de artigo/livro/apresentação, HTML, EPUB3 com recursos locais e integração com ferramentas de IA.

## Limites que permanecem abertos

O trabalho documental CST-212 é uma primeira implementação auditável, não uma solução integral de extração de livros: OCR local offline, reconhecimento semântico em escala, exatidão matemática certificada e formatos DOCX/PPTX/XLSX continuam pendentes. PDFs digitalizados ou com figuras requerem análise visual e conferência humana. A validação em aparelho físico também permanece pendente.

No editor e nas Publicações ainda faltam edição bidirecional dentro do estúdio, transações de múltiplas notas, composição livre, PDF editorial direto, DOCX/ODT/PPTX, vídeo/motion/áudio, cursos e aplicações. A saída PDF usa o sistema de impressão. Testes headless, build e emulador não substituem validação no aparelho físico. Consulte o plano Numa 2.5 e os handoffs para pendências.
