# Numa — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.70
**Canal:** Numa 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

- Publicações agora têm a ação única **Nova publicação**, com escolha entre Artigo, Livro, Apresentação, Curso, Vídeo e Podcast.
- Podcast usa o mesmo projeto editável `.publication.json`, o Estúdio e as fontes Markdown de outros formatos, sem uma biblioteca separada.
- Ao criar Podcast, o Numa prepara um roteiro vinculado `.podcast.md`; roteiros legados continuam compatíveis.
- O Estúdio permite editar e narrar notas de publicações do tipo Podcast mesmo quando a fonte não tem sufixo de podcast.
- O Assistente é orientado a usar `write_publication` com `design.layout=podcast`, preservando o contrato editorial e a rastreabilidade das notas.
- Ao criar Vídeo, a publicação recebe um projeto inicial de cenas sincronizado.
- Incluídos testes de interface e de persistência para garantir o mesmo fluxo de publicação e reabertura de Podcast.
- Testes automatizados não substituem validação no aparelho com serviço real de voz.
