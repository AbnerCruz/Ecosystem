# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.42
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial, após os gates do repositório; versão preparada não significa publicada
**Identidade Android:** app.urbe.csharp e certificado beta existente

## Editor

- Enter do teclado: campos do editor Fonte/Visual e compositor da IA declaram IME multilinha e tecla Return. Quebras enviadas como LF/CR/CRLF mantêm o bloco em edição e o foco; checklists continuam sem marcar itens, e código não recebe continuação de lista. Sete regressões headless aprovadas; teclado Android físico ainda não validado.

- Progresso nativo: inserir meta numérica ou resumo automático do checklist na barra de formatação. Mostra avanço, percentual e restante; configuração no próprio Markdown.
- Tarefas aninhadas contam uma vez; exemplos em código, comentários e itens fora da seção escolhida não entram. Sem tarefas não representa 100%.
- Propriedades numéricas e referências declaradas a outra nota são consultadas sem copiar os totais.
- Numeração lateral de linhas lógicas no modo Fonte, acompanhando as quebras visuais e a rolagem.
- Comandos e alterações do Visual usam operações de entrada registráveis no histórico nativo para permitir desfazer/refazer.

## Publicações

- Fontes podem ser notas inteiras, títulos únicos e blocos com marcador ^id. Vínculo ausente ou ambíguo é informado, sem substituir pelo conteúdo da nota inteira.
- Peças da mesma nota podem ser removidas e reordenadas independentemente.
- Histórico de composição por sessão, objetivo/público, layouts artigo/livro/apresentação e temas de saída papel/oceano/tinta.
- HTML de livro com sumário; HTML de apresentação com navegação por âncoras e perfil de impressão. Prévia nativa mostra conteúdo; paginação/estilo final são do arquivo exportado.
- EPUB real com capítulos, navegação, imagens locais e SVG matemático. Imagem ausente/remota/incompatível ou fonte alterada durante a exportação geram erro útil. Piloto validado com EPUBCheck 5.4.0 sem erros ou avisos.
- IA usa publication_schema/write_publication, com prévia e revisão esperada para substituição. O registro vivo não oferece mais criação pelo Editor de Páginas antigo; conteúdo permanece em notas reais.

## Limites que permanecem abertos

Esta versão não conclui todas as features dos dois editores. Persistem: validação física de IME/seleção/toque e recuperação após morte do processo; edição bidirecional dentro do estúdio, transações de múltiplas notas, composição livre e retirada física completa dos módulos antigos; PDF editorial direto, DOCX/ODT/PPTX, vídeo/motion/áudio, cursos e aplicações. A saída PDF continua pelo sistema de impressão. EPUB não implementa toda mídia/interação, transclusão genérica ou round-trip com leitores externos.

Testes headless, build, emulador e validação humana são evidências distintas. O plano Numa mantém os itens parciais abertos; consultar o handoff e EDITORS-VERIFICATION-2026-11-02.md para resultados observados.
